using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Enforces a strict sequential car assembly order without modifying the snap-to-place system.
///
/// INTERCEPTION STRATEGY (Fix 1)
/// ─────────────────────────────
/// Rather than reacting after a snap has already registered (which caused the part to
/// disappear from the inventory), this validator gates snaps *before* they happen by
/// controlling which snap-zone colliders are enabled.
///
/// Every frame only the collider(s) belonging to the current expected step are enabled.
/// All other zone colliders are disabled. Because SnapToPlace.OnTriggerStay and
/// SnapToPlace.TrySnapObject both require a functioning collider to set candidateInside,
/// a disabled collider means SnapCandidate() is never reached on a wrong zone.
/// OnObjectSnappedEvent therefore never fires for out-of-order zones — no part is locked,
/// no inventory entry is removed, and no ejection logic is needed.
///
/// ALERT TRIGGER
/// ─────────────
/// The alert is shown when the player drags an object within wrongZoneAlertRange of any
/// disabled (wrong-step) snap point. This is checked each frame in Update() while a drag
/// is active.
/// </summary>
public class AssemblyOrderValidator : MonoBehaviour
{
    // ── Assembly sequence ─────────────────────────────────────────────────────
    // Each inner array is one logical step. ALL zones in a group must snap before
    // currentStep advances. Zone names must match GameObject names in /car-snap exactly.

    private static readonly string[][] AssemblySequence =
    {
        new[] { "base-snap" },
        new[] { "LeftAxle-snap", "LeftAxlee-snap", "RightAxle-snap", "RightAxlee-snap" },
        new[] { "movingPieces-snap", "staticPieces-snap" },
        new[] { "SteeringSystem-snap" },
        new[] { "wheelFL-snap", "wheelFR-snap", "wheelRL-snap", "wheelRR-snap" },
        new[] { "bodyPannels-snap" },
        new[] { "frontBumper-snap" },
        new[] { "rearBumper-snap" },
        new[] { "hood-snap" },
        new[] { "CarRoof-snap" },
        new[] { "leftDoor-snap" },
        new[] { "RightDoor-snap" },
        new[] { "trunk lid-snap" },
        new[] { "spoiler-snap" },
        new[] { "LeftHeadLight-snap" },
        new[] { "RightHeadLight-snap" },
        new[] { "LeftTaillights-snap" },
        new[] { "RightTaillights-snap" },
        new[] { "Differential_Carrier-snap", "Ring_Gear-snap",
                "Spider_Gear_Left-snap", "Spider_Gear_Right-snap" },
    };

    // Human-readable name for each step group (shown in the alert).
    private static readonly string[] StepDisplayNames = new[]
    {
        "Base",
        "Rear Axle",
        "Engine",
        "Steering System",
        "Wheels (×4)",
        "Body Panels",
        "Front Bumper",
        "Rear Bumper",
        "Hood",
        "Car Roof",
        "Left Door",
        "Right Door",
        "Trunk Lid",
        "Spoiler",
        "Left Headlight",
        "Right Headlight",
        "Left Taillight",
        "Right Taillight",
        "Differential",
    };

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Tooltip("Canvas that contains all car-phase UI. The alert overlay is spawned inside it.")]
    public Canvas targetCanvas;

    [Tooltip("How close the player's dragged object must be to a wrong snap point before the alert shows (world units).")]
    public float wrongZoneAlertRange = 2f;

    // ── Colours ───────────────────────────────────────────────────────────────

    // Card
    private static readonly Color ColOverlay    = new Color(0.00f, 0.00f, 0.00f, 0.72f);
    private static readonly Color ColCardBg     = new Color(0.07f, 0.07f, 0.07f, 0.98f);
    private static readonly Color ColCardBorder = new Color(0.25f, 0.25f, 0.25f, 1.00f);

    // Text
    private static readonly Color ColTitle      = new Color(1.00f, 1.00f, 1.00f, 1.00f);   // white bold
    private static readonly Color ColBody       = new Color(0.80f, 0.80f, 0.80f, 1.00f);   // light grey
    private static readonly Color ColMuted      = new Color(0.50f, 0.50f, 0.50f, 1.00f);   // dim label

    // Orange accent (used ONLY on pill bg → text is white; button bg → text is white)
    private static readonly Color ColAccent     = new Color(1.00f, 0.42f, 0.00f, 1.00f);   // #FF6B00

    // Part-name pill: very dark bg, orange text — high contrast
    private static readonly Color ColPillBg     = new Color(0.12f, 0.06f, 0.00f, 1.00f);   // near-black warm
    private static readonly Color ColPillBorder = new Color(1.00f, 0.42f, 0.00f, 0.55f);   // orange tint
    private static readonly Color ColPillText   = new Color(1.00f, 0.52f, 0.10f, 1.00f);   // bright orange

    // Button
    private static readonly Color ColBtnBg      = new Color(1.00f, 0.42f, 0.00f, 1.00f);
    private static readonly Color ColBtnText     = new Color(1.00f, 1.00f, 1.00f, 1.00f);

    // Divider
    private static readonly Color ColDivider    = new Color(1.00f, 1.00f, 1.00f, 0.07f);

    // ── Runtime state ─────────────────────────────────────────────────────────

    // All SnapToPlace zones found in the scene, keyed by their normalised name.
    private readonly Dictionary<string, SnapToPlace> zoneByName    = new Dictionary<string, SnapToPlace>();
    // zone name → step index
    private readonly Dictionary<string, int>         zoneToStep    = new Dictionary<string, int>();
    // zones that have successfully completed
    private readonly HashSet<string>                 completedZones = new HashSet<string>();

    private int currentStep;

    // Alert
    private GameObject      alertRoot;
    private TextMeshProUGUI alertExpectedText;

    // Wrong-zone detection: cached list of zones NOT in the current step
    private readonly List<SnapToPlace> wrongZonesCache = new List<SnapToPlace>();
    private bool alertAlreadyShown;   // debounce — show once per drag attempt

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        CollectZones();
        BuildZoneLookup();
        BuildAlertUI();
        SubscribeToAllZones();
        RefreshColliderGates();
    }

    void OnDestroy()
    {
        UnsubscribeFromAllZones();
    }

    void Update()
    {
        CheckForWrongZoneAttempt();
    }

    // ── Zone collection & lookup ──────────────────────────────────────────────

    private void CollectZones()
    {
        SnapToPlace[] all = FindObjectsByType<SnapToPlace>(FindObjectsSortMode.None);
        foreach (SnapToPlace z in all)
            zoneByName[Norm(z.gameObject.name)] = z;
    }

    private void BuildZoneLookup()
    {
        for (int step = 0; step < AssemblySequence.Length; step++)
            foreach (string name in AssemblySequence[step])
                zoneToStep[Norm(name)] = step;
    }

    private static string Norm(string s)
        => s.ToLowerInvariant()
             .Replace("-snap", "")
             .Replace("-", "")
             .Replace("_", "")
             .Replace(" ", "");

    // ── Collider gating ───────────────────────────────────────────────────────

    /// <summary>
    /// Enables colliders only for zones in the current step; disables all others.
    /// This is the core pre-snap gate — a disabled collider can never fire
    /// OnTriggerStay, so SnapCandidate() is never called on a wrong zone.
    /// </summary>
    private void RefreshColliderGates()
    {
        wrongZonesCache.Clear();

        foreach (KeyValuePair<string, SnapToPlace> kvp in zoneByName)
        {
            string key      = kvp.Key;
            SnapToPlace zone = kvp.Value;
            if (zone == null) continue;

            Collider col = zone.GetComponent<Collider>();
            if (col == null) continue;

            // Zones already completed → keep enabled so InventoryManager doesn't choke.
            if (completedZones.Contains(key))
            {
                col.enabled = true;
                continue;
            }

            // Zone not in our sequence → always enabled (not managed here).
            if (!zoneToStep.TryGetValue(key, out int stepIndex))
            {
                col.enabled = true;
                continue;
            }

            bool isCurrentStep = (stepIndex == currentStep);
            col.enabled = isCurrentStep;

            if (!isCurrentStep)
                wrongZonesCache.Add(zone);
        }

        alertAlreadyShown = false;
    }

    // ── Snap event subscription ───────────────────────────────────────────────

    private SnapToPlace[] allZones;

    private void SubscribeToAllZones()
    {
        allZones = FindObjectsByType<SnapToPlace>(FindObjectsSortMode.None);
        foreach (SnapToPlace z in allZones)
            z.OnObjectSnappedEvent += OnZoneSnapped;
    }

    private void UnsubscribeFromAllZones()
    {
        if (allZones == null) return;
        foreach (SnapToPlace z in allZones)
            if (z != null)
                z.OnObjectSnappedEvent -= OnZoneSnapped;
    }

    // ── Snap event handler ────────────────────────────────────────────────────

    private void OnZoneSnapped(GameObject snappedObject, SnapToPlace zone)
    {
        string key = Norm(zone.gameObject.name);

        if (!zoneToStep.TryGetValue(key, out int stepIndex)) return;
        if (completedZones.Contains(key)) return;

        // Collider gating ensures only correct-step snaps reach here.
        if (stepIndex == currentStep)
        {
            completedZones.Add(key);
            TryAdvanceStep();
        }
    }

    private void TryAdvanceStep()
    {
        if (currentStep >= AssemblySequence.Length) return;

        foreach (string name in AssemblySequence[currentStep])
            if (!completedZones.Contains(Norm(name))) return;

        if (currentStep < AssemblySequence.Length - 1)
        {
            currentStep++;
            RefreshColliderGates();
        }
    }

    // ── Wrong-zone attempt detection ──────────────────────────────────────────

    private void CheckForWrongZoneAttempt()
    {
        if (alertAlreadyShown) return;
        if (wrongZonesCache.Count == 0) return;

        GameObject dragged = FindDraggedObject();
        if (dragged == null) return;

        // If the item being dragged is the correct next part, never show the alert.
        if (AssemblyOrderManager.Instance != null)
        {
            // Check DraggableInventoryItem slots
            DraggableInventoryItem[] items =
                FindObjectsByType<DraggableInventoryItem>(FindObjectsSortMode.None);
            foreach (DraggableInventoryItem item in items)
            {
                if (item.GetDraggedObject() == dragged)
                {
                    if (AssemblyOrderManager.Instance.IsCorrectPartBeingDragged(item))
                        return;
                    break;
                }
            }

            // Check WheelInventorySlot — wheels are always itemName "wheels"
            WheelInventorySlot[] wheelSlots =
                FindObjectsByType<WheelInventorySlot>(FindObjectsSortMode.None);
            foreach (WheelInventorySlot slot in wheelSlots)
            {
                if (slot.GetDraggedObject() == dragged)
                {
                    if (AssemblyOrderManager.Instance.IsCorrectPart("wheels"))
                        return;
                    break;
                }
            }
        }

        Vector3 dragPos = dragged.transform.position;

        foreach (SnapToPlace zone in wrongZonesCache)
        {
            if (zone == null || zone.snapPoint == null) continue;
            if (Vector3.Distance(dragPos, zone.snapPoint.position) <= wrongZoneAlertRange)
            {
                alertAlreadyShown = true;
                ShowAlert();
                return;
            }
        }
    }

    private static GameObject FindDraggedObject()
    {
        DraggableInventoryItem[] items =
            FindObjectsByType<DraggableInventoryItem>(FindObjectsSortMode.None);
        foreach (DraggableInventoryItem item in items)
        {
            GameObject obj = item.GetDraggedObject();
            if (obj != null) return obj;
        }

        WheelInventorySlot[] wheels =
            FindObjectsByType<WheelInventorySlot>(FindObjectsSortMode.None);
        foreach (WheelInventorySlot slot in wheels)
        {
            GameObject obj = slot.GetDraggedObject();
            if (obj != null) return obj;
        }

        return null;
    }

    // ── Alert UI construction ─────────────────────────────────────────────────

    private void BuildAlertUI()
    {
        Canvas canvas = targetCanvas != null
            ? targetCanvas
            : FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // Root overlay — fills entire canvas, blocks raycasts while shown
        alertRoot = new GameObject("AssemblyOrderAlert");
        alertRoot.SetActive(false);
        alertRoot.transform.SetParent(canvas.transform, false);
        alertRoot.transform.SetAsLastSibling();

        RectTransform rootRt = alertRoot.AddComponent<RectTransform>();
        StretchFull(rootRt);

        Image overlayImg = alertRoot.AddComponent<Image>();
        overlayImg.color = ColOverlay;
        overlayImg.raycastTarget = true;

        // ── Card ─────────────────────────────────────────────────────────────
        GameObject card = MakeChild(alertRoot, "Card");
        RectTransform cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin        = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax        = new Vector2(0.5f, 0.5f);
        cardRt.pivot            = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta        = new Vector2(440f, 260f);
        cardRt.anchoredPosition = Vector2.zero;

        Image cardImg = card.AddComponent<Image>();
        cardImg.color = ColCardBg;

        Outline cardOutline = card.AddComponent<Outline>();
        cardOutline.effectColor    = ColCardBorder;
        cardOutline.effectDistance = new Vector2(1f, -1f);

        // ── Orange top stripe ─────────────────────────────────────────────────
        GameObject stripe = MakeChild(card, "Stripe");
        RectTransform stripeRt = stripe.GetComponent<RectTransform>();
        stripeRt.anchorMin        = new Vector2(0f, 1f);
        stripeRt.anchorMax        = new Vector2(1f, 1f);
        stripeRt.pivot            = new Vector2(0.5f, 1f);
        stripeRt.sizeDelta        = new Vector2(0f, 3f);
        stripeRt.anchoredPosition = Vector2.zero;
        stripe.AddComponent<Image>().color = ColAccent;

        // ── Content ───────────────────────────────────────────────────────────
        GameObject content = MakeChild(card, "Content");
        RectTransform contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.offsetMin = new Vector2(28f, 22f);
        contentRt.offsetMax = new Vector2(-28f, -22f);

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing                = 12f;
        vlg.childAlignment         = TextAnchor.UpperLeft;
        vlg.childControlHeight     = false;
        vlg.childControlWidth      = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth  = true;

        // Title — white bold
        GameObject titleGo = MakeChild(content, "Title");
        titleGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 26f);
        titleGo.AddComponent<LayoutElement>().preferredHeight = 26f;
        TextMeshProUGUI titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text      = "Wrong Order";
        titleTmp.fontSize  = 15f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color     = ColTitle;

        // Divider
        GameObject divGo = MakeChild(content, "Divider");
        divGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 1f);
        divGo.AddComponent<LayoutElement>().preferredHeight = 1f;
        Image divImg = divGo.AddComponent<Image>();
        divImg.color         = ColDivider;
        divImg.raycastTarget = false;

        // Body message — light grey, normal weight
        GameObject msgGo = MakeChild(content, "Message");
        msgGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 38f);
        msgGo.AddComponent<LayoutElement>().preferredHeight = 38f;
        TextMeshProUGUI msgTmp = msgGo.AddComponent<TextMeshProUGUI>();
        msgTmp.text               = "This is not the correct part. Please follow the assembly order.";
        msgTmp.fontSize           = 13f;
        msgTmp.fontStyle          = FontStyles.Normal;
        msgTmp.color              = ColBody;
        msgTmp.enableWordWrapping = true;

        // Part-name pill: dark background, bright orange text — high contrast
        GameObject pill = MakeChild(content, "PartPill");
        pill.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 38f);

        HorizontalLayoutGroup pillHlg = pill.AddComponent<HorizontalLayoutGroup>();
        pillHlg.childAlignment         = TextAnchor.MiddleLeft;
        pillHlg.childControlHeight     = false;
        pillHlg.childControlWidth      = false;
        pillHlg.childForceExpandWidth  = false;
        pillHlg.childForceExpandHeight = true;
        pill.AddComponent<LayoutElement>().preferredHeight = 38f;

        GameObject pillInner = MakeChild(pill, "PillInner");
        RectTransform pillInnerRt = pillInner.GetComponent<RectTransform>();
        pillInnerRt.sizeDelta = new Vector2(180f, 34f);

        pillInner.AddComponent<Image>().color = ColPillBg;
        Outline pillBorder = pillInner.AddComponent<Outline>();
        pillBorder.effectColor    = ColPillBorder;
        pillBorder.effectDistance = new Vector2(1f, -1f);

        HorizontalLayoutGroup pillInnerHlg = pillInner.AddComponent<HorizontalLayoutGroup>();
        pillInnerHlg.padding                = new RectOffset(14, 14, 6, 6);
        pillInnerHlg.childAlignment         = TextAnchor.MiddleLeft;
        pillInnerHlg.childControlHeight     = true;
        pillInnerHlg.childControlWidth      = true;
        pillInnerHlg.childForceExpandWidth  = true;
        pillInnerHlg.childForceExpandHeight = true;

        LayoutElement pillInnerLe = pillInner.AddComponent<LayoutElement>();
        pillInnerLe.preferredHeight = 34f;
        pillInnerLe.minWidth        = 80f;
        pillInnerLe.flexibleWidth   = 0f;

        GameObject partNameGo = MakeChild(pillInner, "PartName");
        alertExpectedText = partNameGo.AddComponent<TextMeshProUGUI>();
        alertExpectedText.text               = "";
        alertExpectedText.fontSize           = 15f;
        alertExpectedText.fontStyle          = FontStyles.Bold;
        alertExpectedText.color              = ColPillText;
        alertExpectedText.enableWordWrapping = false;
        alertExpectedText.alignment          = TextAlignmentOptions.MidlineLeft;

        // Try Again button — solid orange, white bold text
        GameObject btnGo = MakeChild(content, "TryAgainButton");
        btnGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 42f);
        btnGo.AddComponent<LayoutElement>().preferredHeight = 42f;

        Image btnImg = btnGo.AddComponent<Image>();
        btnImg.color = ColBtnBg;

        Button btn = btnGo.AddComponent<Button>();
        ColorBlock cb       = btn.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        cb.pressedColor     = new Color(0.70f, 0.70f, 0.70f, 1f);
        cb.colorMultiplier  = 1f;
        btn.colors          = cb;
        btn.onClick.AddListener(HideAlert);

        GameObject btnLabelGo = MakeChild(btnGo, "Label");
        StretchFull(btnLabelGo.GetComponent<RectTransform>());
        TextMeshProUGUI btnTmp = btnLabelGo.AddComponent<TextMeshProUGUI>();
        btnTmp.text      = "Try Again";
        btnTmp.fontSize  = 14f;
        btnTmp.fontStyle = FontStyles.Bold;
        btnTmp.color     = ColBtnText;
        btnTmp.alignment = TextAlignmentOptions.Center;
    }

    // ── Alert show / hide ─────────────────────────────────────────────────────

    /// <summary>
    /// Shows the alert, displaying the name of the currently expected part.
    /// Public so DraggableInventoryItem can trigger it immediately on a wrong drop.
    /// </summary>
    public void ShowAlert()
    {
        if (alertRoot == null) return;

        if (alertExpectedText != null && AssemblyOrderManager.Instance != null)
            alertExpectedText.text = AssemblyOrderManager.Instance.ExpectedPartDisplayName;

        alertRoot.SetActive(true);
        alertRoot.transform.SetAsLastSibling();
    }

    /// <summary>
    /// Hides the alert and fully restores drag interaction.
    /// Resets every CanvasGroup on every DraggableInventoryItem so no item
    /// is left in a blocksRaycasts=false state from the cancelled drag.
    /// </summary>
    private void HideAlert()
    {
        if (alertRoot != null)
            alertRoot.SetActive(false);

        alertAlreadyShown = false;

        // Guarantee all inventory slots are fully interactive again.
        // A cancelled drag leaves canvasGroup.blocksRaycasts = false on the
        // dragged slot; this is the single authoritative restore point.
        DraggableInventoryItem[] slots =
            FindObjectsByType<DraggableInventoryItem>(FindObjectsSortMode.None);
        foreach (DraggableInventoryItem slot in slots)
        {
            CanvasGroup cg = slot.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha           = 1f;
                cg.blocksRaycasts  = true;
                cg.interactable    = true;
            }
        }
    }

    /// <summary>Resets the per-drag debounce flag (used by external callers).</summary>
    public void ResetAlertDebounce()
    {
        alertAlreadyShown = false;
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private static GameObject MakeChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }
}
