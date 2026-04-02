using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phase 1 — Steering (part-by-part):
///   • SteeringSystem1 children → transparent frozen ghost guides (NO physics).
///   • SteeringSystem-snap children → get SnapToPlace + trigger collider at runtime.
///   • Parts with the same mesh sub-asset → one grouped inventory button.
///   • Clicking a button → spawns one new grabbable GameObject per unoccupied zone.
///   • 100 % → car assembly phase, garage door unlocked.
/// Phase 2 — Car: SteeringSystem1 hidden, car UI shown.
/// </summary>
public class SteeringAssemblyController : MonoBehaviour
{
    [Header("Ghost Preview")]
    [Tooltip("SteeringSystem1 root — children become transparent frozen ghost guides.")]
    public GameObject steeringSystem1;

    [Tooltip("The transparent material used by SteeringSystem-snap ghost meshes.")]
    public Material transparentMaterial;

    [Header("Snap Root")]
    [Tooltip("Parent whose direct children are ghost meshes for each steering part.")]
    public Transform steeringSnapRoot;

    [Header("Snap Settings")]
    public float snapRange    = 5f;
    public float snapZoneSize = 0.3f;
    public Color highlightColor = new Color(0f, 1f, 0.4f, 1f);

    [Header("Part Spawn")]
    [Tooltip("Opaque car material applied to newly spawned grabbable parts.")]
    public Material opaquePartMaterial;

    [Tooltip("Optional world-space spawn position. Defaults to in front of the camera.")]
    public Transform spawnPoint;

    [Header("Steering UI")]
    public GameObject steeringProgressPanel;
    public Image      steeringBarFill;
    public Image      steeringLineFill;
    public Text       steeringPercentLabel;
    public Text       steeringPartsLabel;
    public GameObject steeringInventoryPanel;
    public GameObject slotPrefab;

    [Header("Car UI — shown after steering completes")]
    public GameObject carProgressPanel;
    public GameObject carInventoryPanel;

    [Header("Animation")]
    public float smoothSpeed = 4f;

    // ── Runtime ───────────────────────────────────────────────────────────────

    readonly List<SnapToPlace>                      allZones   = new List<SnapToPlace>();
    readonly Dictionary<string, List<SnapToPlace>>  meshGroups = new Dictionary<string, List<SnapToPlace>>();
    readonly Dictionary<string, Transform>          meshSource = new Dictionary<string, Transform>();
    readonly Dictionary<string, GameObject>         groupSlots = new Dictionary<string, GameObject>();

    bool  isSteeringComplete;
    float currentFill;
    Camera mainCam;

    public bool IsSteeringComplete => isSteeringComplete;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        mainCam = Camera.main;

        // Skip steering phase — show car assembly directly.
        if (steeringSystem1 != null) steeringSystem1.SetActive(false);
        SetActive(steeringProgressPanel,  false);
        SetActive(steeringInventoryPanel, false);
        SetActive(carProgressPanel,       true);
        SetActive(carInventoryPanel,      true);

        InventorySystem invSys = carInventoryPanel != null
            ? carInventoryPanel.GetComponentInParent<InventorySystem>()
            : FindFirstObjectByType<InventorySystem>();
        if (invSys != null) invSys.RebuildInventoryUI();
    }

    void Update() { }

    void OnDestroy()
    {
        foreach (SnapToPlace z in allZones)
            if (z != null) z.OnObjectSnappedEvent -= OnPartSnapped;
    }

    // ── Freeze SteeringSystem1 children as pure static ghost meshes ───────────

    void FreezeGhost()
    {
        if (steeringSystem1 == null) return;

        foreach (Transform child in steeringSystem1.transform)
        {
            // Kill physics so parts don't fall
            Rigidbody rb = child.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

            // Disable solid colliders so the ghost can't be grabbed or bumped
            foreach (Collider col in child.GetComponentsInChildren<Collider>())
                if (!col.isTrigger) col.enabled = false;

            // Swap to transparent material
            if (transparentMaterial == null) continue;
            MeshRenderer mr = child.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            Material[] slots = new Material[mr.sharedMaterials.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = transparentMaterial;
            mr.sharedMaterials = slots;
        }
    }

    // ── Add SnapToPlace to each SteeringSystem-snap child ────────────────────

    void BuildSnapZones()
    {
        if (steeringSnapRoot == null || steeringSystem1 == null) return;

        // Build name → Transform lookup from SteeringSystem1
        var s1Lookup = new Dictionary<string, Transform>();
        foreach (Transform child in steeringSystem1.transform)
            s1Lookup[child.name] = child;

        foreach (Transform snapChild in steeringSnapRoot)
        {
            // Ensure trigger collider exists
            Collider col = snapChild.GetComponent<Collider>();
            if (col == null)
            {
                BoxCollider bc = snapChild.gameObject.AddComponent<BoxCollider>();
                bc.size = Vector3.one * snapZoneSize;
                col = bc;
            }
            col.isTrigger = true;

            // Add SnapToPlace
            SnapToPlace snap = snapChild.GetComponent<SnapToPlace>();
            if (snap == null) snap = snapChild.gameObject.AddComponent<SnapToPlace>();

            snap.snapPoint               = snapChild;
            snap.snapRange               = snapRange;
            snap.objectToSnap            = null;           // accept any Grabbable part
            snap.grabbableTag            = "Grabbable";
            snap.countInProgress         = true;
            snap.highlightColor          = highlightColor;
            snap.zoneRenderer            = snapChild.GetComponent<Renderer>();
            snap.disableGravityAfterSnap = true;
            snap.lockObjectAfterSnap     = true;

            snap.OnObjectSnappedEvent += OnPartSnapped;
            allZones.Add(snap);

            // Group by shared mesh sub-asset name
            MeshFilter mf  = snapChild.GetComponent<MeshFilter>();
            string meshKey = (mf != null && mf.sharedMesh != null)
                                ? mf.sharedMesh.name
                                : snapChild.name;

            if (!meshGroups.ContainsKey(meshKey))
                meshGroups[meshKey] = new List<SnapToPlace>();
            meshGroups[meshKey].Add(snap);

            // Store one source Transform per mesh key (for spawning)
            if (!meshSource.ContainsKey(meshKey) && s1Lookup.TryGetValue(snapChild.name, out Transform src))
                meshSource[meshKey] = src;
        }
    }

    // ── One inventory button per unique mesh group ────────────────────────────

    void BuildInventoryUI()
    {
        if (steeringInventoryPanel == null || slotPrefab == null) return;

        foreach (Transform child in steeringInventoryPanel.transform)
            Destroy(child.gameObject);
        groupSlots.Clear();

        foreach (var kvp in meshGroups)
        {
            string meshKey = kvp.Key;
            int    count   = kvp.Value.Count;
            string label   = count > 1 ? $"{meshKey}  ×{count}" : meshKey;

            GameObject slot = Instantiate(slotPrefab, steeringInventoryPanel.transform);
            slot.name = "Slot_" + meshKey;

            // Set label text
            Text nameText = slot.transform.Find("PartName")?.GetComponent<Text>();
            if (nameText != null) nameText.text = label;

            // Hide icon — FBX sub-assets have no sprites
            Image icon = slot.transform.Find("PartIcon")?.GetComponent<Image>();
            if (icon != null) icon.enabled = false;

            // Wire button click
            Button btn = slot.GetComponent<Button>();
            if (btn == null) btn = slot.AddComponent<Button>();

            string     capturedKey  = meshKey;
            GameObject capturedSlot = slot;
            btn.onClick.AddListener(() => OnGroupButtonClicked(capturedKey, capturedSlot));

            groupSlots[meshKey] = slot;
        }
    }

    // ── Spawn new grabbable GameObjects when a button is clicked ──────────────

    void OnGroupButtonClicked(string meshKey, GameObject slot)
    {
        if (!meshGroups.TryGetValue(meshKey, out var zones)) return;
        if (!meshSource.TryGetValue(meshKey, out Transform source)) return;

        int pending = 0;
        foreach (SnapToPlace z in zones)
            if (!z.IsOccupied()) pending++;

        if (pending == 0) { slot.SetActive(false); return; }

        GameObject firstPart = null;
        for (int i = 0; i < pending; i++)
        {
            GameObject part = SpawnGrabbablePart(source, meshKey, i);
            if (i == 0) firstPart = part;
        }

        // Hand the first spawned part directly to the cursor so the player
        // is holding it immediately — same behaviour as the car parts drag.
        if (firstPart != null)
        {
            CenterUICursor cursor = FindFirstObjectByType<CenterUICursor>();
            Rigidbody rb = firstPart.GetComponent<Rigidbody>();
            if (cursor != null && rb != null)
                cursor.GrabDirect(rb);
        }

        slot.SetActive(false);
    }

    GameObject SpawnGrabbablePart(Transform sourceTransform, string meshKey, int offset)
    {
        // Compute spawn position
        Vector3 basePos = spawnPoint != null
            ? spawnPoint.position
            : mainCam != null
                ? mainCam.transform.position + mainCam.transform.forward * 2f + Vector3.up * 0.5f
                : sourceTransform.position + Vector3.up * 2f;

        // Spread multiple parts slightly so they don't overlap
        Vector3 spawnPos = basePos + new Vector3(
            offset * 0.15f + Random.Range(-0.05f, 0.05f),
            offset * 0.05f,
            Random.Range(-0.05f, 0.05f));

        GameObject part = new GameObject("Spawned_" + meshKey);
        part.transform.position   = spawnPos;
        part.transform.rotation   = sourceTransform.rotation;
        part.transform.localScale = sourceTransform.lossyScale;

        // Copy mesh
        MeshFilter srcMf = sourceTransform.GetComponent<MeshFilter>();
        MeshFilter mf    = part.AddComponent<MeshFilter>();
        if (srcMf != null) mf.sharedMesh = srcMf.sharedMesh;

        // Opaque material
        MeshRenderer mr = part.AddComponent<MeshRenderer>();
        Material mat = opaquePartMaterial != null
            ? opaquePartMaterial
            : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mr.sharedMaterial = mat;

        // Collider sized to mesh bounds
        BoxCollider col = part.AddComponent<BoxCollider>();
        if (mf.sharedMesh != null)
        {
            col.center = mf.sharedMesh.bounds.center;
            col.size   = mf.sharedMesh.bounds.size;
        }

        // Kinematic Rigidbody — CenterUICursor takes over once grabbed
        Rigidbody rb              = part.AddComponent<Rigidbody>();
        rb.isKinematic            = true;
        rb.useGravity             = false;
        rb.mass                   = 1f;
        rb.linearDamping          = 5f;
        rb.angularDamping         = 5f;
        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // Tag and layer
        part.tag   = "Grabbable";
        part.layer = LayerMask.NameToLayer("Interactable");

        return part;
    }

    // ── Snap event ────────────────────────────────────────────────────────────

    void OnPartSnapped(GameObject obj, SnapToPlace zone)
    {
        // Hide a group's button once all its zones are filled
        foreach (var kvp in meshGroups)
        {
            if (!kvp.Value.Contains(zone)) continue;

            bool allFilled = true;
            foreach (SnapToPlace z in kvp.Value)
                if (!z.IsOccupied()) { allFilled = false; break; }

            if (allFilled && groupSlots.TryGetValue(kvp.Key, out GameObject slot) && slot != null)
                slot.SetActive(false);
            break;
        }
    }

    // ── Phase transition ──────────────────────────────────────────────────────

    void CompleteSteeringPhase()
    {
        isSteeringComplete = true;
        currentFill = 1f;
        UpdateBar(1f, allZones.Count, allZones.Count);

        if (steeringSystem1 != null) steeringSystem1.SetActive(false);

        SetActive(steeringProgressPanel,  false);
        SetActive(steeringInventoryPanel, false);
        SetActive(carProgressPanel,       true);
        SetActive(carInventoryPanel,      true);

        InventorySystem invSys = carInventoryPanel != null
            ? carInventoryPanel.GetComponentInParent<InventorySystem>()
            : FindFirstObjectByType<InventorySystem>();
        if (invSys != null) invSys.RebuildInventoryUI();
    }

    void ShowSteeringPhase()
    {
        SetActive(steeringProgressPanel,  true);
        SetActive(steeringInventoryPanel, true);
        SetActive(carProgressPanel,       false);
        SetActive(carInventoryPanel,      false);
    }

    void UpdateBar(float fill, int placed, int total)
    {
        if (steeringBarFill  != null) steeringBarFill.fillAmount  = fill;
        if (steeringLineFill != null) steeringLineFill.fillAmount = fill;
        int pct = Mathf.RoundToInt(fill * 100f);
        if (steeringPercentLabel != null) steeringPercentLabel.text = $"{pct}%";
        if (steeringPartsLabel   != null) steeringPartsLabel.text   = $"{placed} / {total} parts";
    }

    static void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }
}
