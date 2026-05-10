using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Self-contained drag handler for the "wheels" inventory slot.
/// Each drag spawns ONE individual wheel child (wheelFL/FR/RL/RR) from the
/// combined wheels prefab and assigns it to its matching snap zone by name.
/// A count badge decrements from 4 to 0; the slot hides when exhausted.
/// Completely isolated from all other inventory item flows.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class WheelInventorySlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Wheel Prefab")]
    [Tooltip("The combined wheels.prefab whose children are the 4 individual wheels.")]
    public GameObject wheelsPrefab;

    [Header("Bezier Arc")]
    public float arcHeight = 1.5f;

    [Header("Ghost Preview")]
    public float ghostActivationRange = 3f;

    [Header("UI References")]
    public Image iconImage;
    [Tooltip("Text element that shows remaining count, e.g. 'x4'.")]
    public Text countText;

    // ── Constants ─────────────────────────────────────────────────────────────

    private const float SpawnScale = 5f;   // matches spawnScale in wheels.asset

    // Spawn order — must match child names inside wheels.prefab.
    private static readonly string[] WheelNames =
        { "wheelFL", "wheelFR", "wheelRL", "wheelRR" };

    // ── Private state ─────────────────────────────────────────────────────────

    private readonly Queue<string> spawnQueue = new Queue<string>();

    private int           remaining;
    private SnapToPlace[] allWheelZones;
    private Camera        mainCam;
    private CanvasGroup   canvasGroup;

    private GameObject draggedObject;
    private Vector3    originWorldPos;
    private bool       isDragging;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    /// <summary>Returns the wheel GameObject currently being dragged, or null.</summary>
    public GameObject GetDraggedObject() => draggedObject;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        mainCam     = Camera.main;

        foreach (string name in WheelNames)
            spawnQueue.Enqueue(name);

        remaining = WheelNames.Length;
    }

    void Start()
    {
        CollectWheelZones();

        if (WheelGhostPreview.Instance != null)
            WheelGhostPreview.Instance.activationRange = ghostActivationRange;

        RefreshBadge();
    }

    // ── IBeginDragHandler ─────────────────────────────────────────────────────

    /// <summary>Spawns the next single wheel child and begins the drag.</summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (remaining <= 0 || spawnQueue.Count == 0 || wheelsPrefab == null) return;

        string    childName     = spawnQueue.Peek();
        Transform childTemplate = wheelsPrefab.transform.Find(childName);

        if (childTemplate == null)
        {
            Debug.LogWarning($"[WheelInventorySlot] Child '{childName}' not found in wheels prefab.");
            return;
        }

        isDragging     = true;
        originWorldPos = transform.position;

        canvasGroup.alpha          = 0.6f;
        canvasGroup.blocksRaycasts = false;

        SnapToPlace targetZone = FindZoneForWheel(childName);
        Quaternion  spawnRot   = targetZone != null
            ? targetZone.snapPoint.rotation
            : childTemplate.rotation;

        Vector3 spawnPos = mainCam.transform.position + mainCam.transform.forward * 3f;
        draggedObject    = Instantiate(childTemplate.gameObject, spawnPos, spawnRot);

        SetupDraggedObject(draggedObject, targetZone);

        if (WheelGhostPreview.Instance != null)
            WheelGhostPreview.Instance.Activate(childTemplate.gameObject, allWheelZones);

        if (InventoryAudioManager.Instance != null)
            InventoryAudioManager.Instance.PlayDragStart();
    }

    // ── IDragHandler ──────────────────────────────────────────────────────────

    /// <summary>Moves the spawned wheel along a Bezier arc toward the cursor.</summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || draggedObject == null) return;

        Vector3 cursor = ResolveCursorWorld(eventData);
        Vector3 p1     = (originWorldPos + cursor) * 0.5f + Vector3.up * arcHeight;
        draggedObject.transform.position = EvaluateBezier(originWorldPos, p1, cursor, 1f);

        if (WheelGhostPreview.Instance != null)
            WheelGhostPreview.Instance.UpdatePosition(cursor);
    }

    // ── IEndDragHandler ───────────────────────────────────────────────────────

    /// <summary>Hands the wheel to CenterUICursor; decrements the count badge.</summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.alpha          = 1f;
        canvasGroup.blocksRaycasts = true;

        if (!isDragging || draggedObject == null)
        {
            isDragging = false;
            return;
        }

        isDragging = false;

        if (WheelGhostPreview.Instance != null)
            WheelGhostPreview.Instance.Deactivate();

        // ── Assembly order validation ─────────────────────────────────────────
        // Wheels are always itemName "wheels" — check before any snap logic runs.
        if (AssemblyOrderManager.Instance != null &&
            !AssemblyOrderManager.Instance.IsCorrectPart("wheels"))
        {
            CancelWheelDrag();
            AssemblyOrderManager.Instance.ShowWrongOrderAlert();
            draggedObject = null;
            return;
        }
        // ─────────────────────────────────────────────────────────────────────

        // Reassign to nearest free zone in case player aimed elsewhere.
        AssignNearestZone(draggedObject);

        Rigidbody      rb     = draggedObject.GetComponent<Rigidbody>();
        CenterUICursor cursor = FindFirstObjectByType<CenterUICursor>();

        if (cursor != null && rb != null)
        {
            // Subscribe snap/remove events before handing off.
            SnapZoneAssignment assign = draggedObject.GetComponent<SnapZoneAssignment>();
            if (assign?.assignedSnapZone != null)
            {
                assign.assignedSnapZone.OnObjectSnappedEvent += OnWheelSnapped;
                assign.assignedSnapZone.OnObjectRemovedEvent += OnWheelRemoved;
            }

            spawnQueue.Dequeue();
            remaining--;
            RefreshBadge();

            cursor.GrabDirect(rb);
        }
        else
        {
            // Fallback: place on ground.
            Ray ray = mainCam.ScreenPointToRay(eventData.position);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                draggedObject.transform.position = hit.point + Vector3.up * 0.1f;
                if (rb != null) { rb.useGravity = true; rb.isKinematic = false; }
                spawnQueue.Dequeue();
                remaining--;
                RefreshBadge();
            }
            else
            {
                Destroy(draggedObject);
            }
        }

        draggedObject = null;
    }

    // ── Snap event callbacks ──────────────────────────────────────────────────

    private void OnWheelSnapped(GameObject obj, SnapToPlace zone)
    {
        // Wheel permanently placed — unsubscribe and advance the assembly sequence.
        zone.OnObjectSnappedEvent -= OnWheelSnapped;
        zone.OnObjectRemovedEvent -= OnWheelRemoved;

        if (AssemblyOrderManager.Instance != null)
            AssemblyOrderManager.Instance.AdvanceToNextPart();
    }

    private void OnWheelRemoved(GameObject obj, SnapToPlace zone)
    {
        zone.OnObjectSnappedEvent -= OnWheelSnapped;
        zone.OnObjectRemovedEvent -= OnWheelRemoved;

        // Return this wheel name to the front of the queue.
        string[] pending = spawnQueue.ToArray();
        spawnQueue.Clear();
        spawnQueue.Enqueue(obj.name);
        foreach (string n in pending)
            spawnQueue.Enqueue(n);

        remaining++;
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        RefreshBadge();

        if (InventoryAudioManager.Instance != null)
            InventoryAudioManager.Instance.PlayItemRespawn();
    }

    // ── Setup helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Cancels a wrong-order drag: destroys the preview wheel and fully restores
    /// the slot so the player can drag again immediately.
    /// </summary>
    private void CancelWheelDrag()
    {
        if (draggedObject != null)
        {
            Rigidbody rb = draggedObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic     = true;
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            Destroy(draggedObject);
        }

        canvasGroup.alpha          = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable   = true;
        gameObject.SetActive(true);
    }

    /// <summary>Configures physics, layer, tag, scale and snap assignment on the spawned wheel.</summary>
    private void SetupDraggedObject(GameObject obj, SnapToPlace targetZone)
    {
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        foreach (Transform t in obj.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = interactableLayer;

        obj.tag = "Grabbable";
        obj.transform.localScale = Vector3.one * SpawnScale;

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null) rb = obj.AddComponent<Rigidbody>();

        rb.useGravity             = false;
        rb.isKinematic            = false;
        rb.linearVelocity         = Vector3.zero;
        rb.angularVelocity        = Vector3.zero;
        rb.isKinematic            = true;
        rb.mass                   = 1f;
        rb.linearDamping          = 5f;
        rb.angularDamping         = 5f;
        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.detectCollisions       = true;

        SnapZoneAssignment assignment = obj.GetComponent<SnapZoneAssignment>();
        if (assignment == null) assignment = obj.AddComponent<SnapZoneAssignment>();
        assignment.assignedSnapZone = targetZone;
    }

    // ── Zone helpers ──────────────────────────────────────────────────────────

    private void CollectWheelZones()
    {
        if (WheelPlacementTracker.Instance?.wheelsSnapParent != null)
            allWheelZones = WheelPlacementTracker.Instance.wheelsSnapParent
                            .GetComponentsInChildren<SnapToPlace>();
        else
            allWheelZones = FindObjectsByType<SnapToPlace>(FindObjectsSortMode.None);
    }

    /// <summary>Finds the first unoccupied zone whose name contains the wheel name.</summary>
    private SnapToPlace FindZoneForWheel(string wheelName)
    {
        if (allWheelZones == null) return null;
        string wn = Normalize(wheelName);
        foreach (SnapToPlace zone in allWheelZones)
        {
            if (zone == null || zone.IsOccupied()) continue;
            if (Normalize(zone.gameObject.name).Contains(wn))
                return zone;
        }
        return null;
    }

    /// <summary>
    /// If the assigned zone is already occupied, finds the nearest free zone
    /// and updates the SnapZoneAssignment on the dragged object.
    /// </summary>
    private void AssignNearestZone(GameObject obj)
    {
        SnapZoneAssignment assign = obj?.GetComponent<SnapZoneAssignment>();
        if (assign == null || allWheelZones == null) return;
        if (assign.assignedSnapZone != null && !assign.assignedSnapZone.IsOccupied()) return;

        Vector3     pos      = obj.transform.position;
        SnapToPlace best     = null;
        float       bestDist = float.MaxValue;

        foreach (SnapToPlace zone in allWheelZones)
        {
            if (zone == null || zone.IsOccupied()) continue;
            float d = Vector3.Distance(pos, zone.snapPoint.position);
            if (d < bestDist) { bestDist = d; best = zone; }
        }

        if (best != null) assign.assignedSnapZone = best;
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    private void RefreshBadge()
    {
        if (countText != null)
            countText.text = $"x{remaining}";

        if (remaining <= 0)
            gameObject.SetActive(false);
    }

    // ── Math ──────────────────────────────────────────────────────────────────

    private Vector3 ResolveCursorWorld(PointerEventData eventData)
    {
        if (mainCam == null) return originWorldPos;
        Ray       ray  = mainCam.ScreenPointToRay(eventData.position);
        LayerMask mask = LayerMask.GetMask("Ground", "Default");
        return Physics.Raycast(ray, out RaycastHit hit, 100f, mask)
            ? hit.point
            : mainCam.transform.position + mainCam.transform.forward * 3f;
    }

    private static Vector3 EvaluateBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    private static string Normalize(string s)
        => s.ToLowerInvariant().Replace("-", "").Replace("_", "");
}
