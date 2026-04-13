using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mirrors SteeringSnapSetup for the engine assembly phase.
///
/// moteurSnap children  = transparent ghost meshes at the assembled position.
/// moteur2 children     = physical grabbable parts on the table.
///
/// On Start:
///   1. All moteurSnap children → ghostMaterial.
///   2. All moteur2 children   → Rigidbody + BoxCollider + Grabbable tag/layer.
///   3. Parts linked to ghosts by exact child name.
///   4. Grab → ghost highlights green. Drop → part snaps to ghost world transform.
///
/// AllPartsFullySnapped becomes true once every part has snapped and its
/// coroutine has finished — safe to poll from MoteurAssemblyManager.
/// </summary>
public class MoteurSnapSetup : MonoBehaviour
{
    [Header("Roots")]
    [Tooltip("moteurSnap — children are the transparent ghost meshes (snap targets).")]
    public Transform ghostRoot;

    [Tooltip("moteur2 — children are the physical parts on the table.")]
    public Transform partsRoot;

    [Header("Materials")]
    [Tooltip("Transparent/ghost material applied to all moteurSnap children.")]
    public Material ghostMaterial;

    [Header("Snap Settings")]
    [Tooltip("Highlight color on the ghost when its part is being held.")]
    public Color highlightColor = Color.green;

    [Tooltip("Emission intensity of the highlight.")]
    public float emissionIntensity = 2.5f;

    [System.Serializable]
    public struct NameMapping
    {
        [Tooltip("Name of the child in partsRoot (moteur2).")]
        public string physicalName;
        [Tooltip("Name of the matching child in ghostRoot (moteurSnap).")]
        public string ghostName;
    }

    [Header("Name Overrides")]
    [Tooltip("Explicit mappings for parts whose names differ between the two hierarchies.")]
    public NameMapping[] nameOverrides;

    // ── Internal ──────────────────────────────────────────────────────────────

    class PartEntry
    {
        public Transform     ghostChild;
        public Transform     physicalPart;
        public MeshRenderer  ghostRenderer;
        public bool          isHeld;
        public bool          isSnapped;
        public MaterialPropertyBlock mpb;
    }

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    readonly List<PartEntry> entries = new List<PartEntry>();
    int pendingSnaps;

    /// <summary>
    /// True once every matched part is fully snapped and its coroutine completed.
    /// </summary>
    public bool AllPartsFullySnapped =>
        pendingSnaps == 0 && IsAssemblyComplete();

    CenterUICursor cursor;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        cursor = FindFirstObjectByType<CenterUICursor>();

        if (ghostRoot == null || partsRoot == null)
        {
            Debug.LogError("[MoteurSnapSetup] ghostRoot or partsRoot is not assigned.", this);
            return;
        }

        // Ghost name → Transform lookup
        var ghostByName = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);
        foreach (Transform ghost in ghostRoot)
            ghostByName[ghost.name] = ghost;

        // Override map: physicalName → ghostName
        var overrideMap = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        if (nameOverrides != null)
            foreach (var m in nameOverrides)
                if (!string.IsNullOrEmpty(m.physicalName) && !string.IsNullOrEmpty(m.ghostName))
                    overrideMap[m.physicalName] = m.ghostName;

        // 1. Make ghost children transparent
        foreach (Transform ghost in ghostRoot)
        {
            MeshRenderer mr = ghost.GetComponent<MeshRenderer>();
            if (mr != null && ghostMaterial != null)
            {
                var mats = new Material[mr.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = ghostMaterial;
                mr.sharedMaterials = mats;
            }

            // Disable solid colliders on ghost
            foreach (Collider col in ghost.GetComponentsInChildren<Collider>())
                if (!col.isTrigger) col.enabled = false;
        }

        // 2. Set up physical parts and link to ghosts
        foreach (Transform part in partsRoot)
        {
            SetupPhysicalPart(part);

            string lookupName = part.name;
            if (overrideMap.TryGetValue(part.name, out string mapped))
                lookupName = mapped;

            if (!ghostByName.TryGetValue(lookupName, out Transform ghost)) continue;

            MeshRenderer ghostMr = ghost.GetComponent<MeshRenderer>();
            if (ghostMr == null) continue;

            var entry = new PartEntry
            {
                ghostChild    = ghost,
                physicalPart  = part,
                ghostRenderer = ghostMr,
                mpb           = new MaterialPropertyBlock()
            };

            if (ghostMr.sharedMaterial != null)
                ghostMr.sharedMaterial.EnableKeyword("_EMISSION");

            SetGhostHighlight(entry, false);
            entries.Add(entry);
        }
    }

    /// <summary>Adds Rigidbody, BoxCollider, tag, and layer to a physical part.</summary>
    void SetupPhysicalPart(Transform part)
    {
        part.gameObject.tag   = "Grabbable";
        part.gameObject.layer = LayerMask.NameToLayer("Interactable");

        if (part.GetComponent<Collider>() == null)
        {
            BoxCollider bc = part.gameObject.AddComponent<BoxCollider>();
            MeshFilter mf = part.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                bc.center = mf.sharedMesh.bounds.center;
                bc.size   = mf.sharedMesh.bounds.size;
            }
        }

        if (part.GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb              = part.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic            = true;
            rb.useGravity             = false;
            rb.mass                   = 1f;
            rb.linearDamping          = 5f;
            rb.angularDamping         = 5f;
            rb.interpolation          = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        foreach (PartEntry entry in entries)
        {
            if (entry.isSnapped) continue;

            bool heldNow = cursor != null && cursor.HeldObject == entry.physicalPart.gameObject;

            if (heldNow && !entry.isHeld)
            {
                entry.isHeld = true;
                SetGhostHighlight(entry, true);
            }

            if (!heldNow && entry.isHeld)
            {
                entry.isHeld    = false;
                entry.isSnapped = true;
                SetGhostHighlight(entry, false);
                StartCoroutine(SnapNextFrame(entry));
            }
        }
    }

    /// <summary>
    /// Waits one frame so the cursor fully releases the part, then
    /// hard-freezes and teleports it to the ghost's world transform.
    /// </summary>
    IEnumerator SnapNextFrame(PartEntry entry)
    {
        pendingSnaps++;
        yield return null;

        Rigidbody rb = entry.physicalPart.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic     = true;
            rb.useGravity      = false;
            rb.constraints     = RigidbodyConstraints.FreezeAll;
        }

        entry.physicalPart.SetPositionAndRotation(
            entry.ghostChild.position,
            entry.ghostChild.rotation);

        if (rb != null)
        {
            rb.position = entry.ghostChild.position;
            rb.rotation = entry.ghostChild.rotation;
        }

        Collider col = entry.physicalPart.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        pendingSnaps--;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SetGhostHighlight(PartEntry entry, bool on)
    {
        if (entry.ghostRenderer == null) return;
        entry.ghostRenderer.GetPropertyBlock(entry.mpb);
        entry.mpb.SetColor(EmissionId,
            on ? highlightColor * emissionIntensity : Color.black);
        entry.ghostRenderer.SetPropertyBlock(entry.mpb);
    }

    /// <summary>Returns true when every matched part has been snapped.</summary>
    public bool IsAssemblyComplete()
    {
        if (entries.Count == 0) return false;
        foreach (PartEntry e in entries)
            if (!e.isSnapped) return false;
        return true;
    }
}
