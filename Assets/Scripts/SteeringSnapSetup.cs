using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sets up the steering system snap assembly at runtime.
///
/// SteeringSystemSnap children = ghost/preview (transparent, shown in assembled position).
/// SteeringSystem1 children    = physical grabbable parts on the table.
///
/// On Start:
///   1. All SteeringSystemSnap children → transparent material.
///   2. ALL SteeringSystem1 children → Rigidbody + BoxCollider + Grabbable tag/layer.
///   3. Parts are linked to ghosts by exact name, then by the NameOverrides mapping.
///   4. When a linked part is grabbed → matching ghost highlights green.
///   5. When released → part snaps to ghost position after one physics frame.
///   6. Parts with no ghost match are still grabbable but have no snap target.
/// </summary>
public class SteeringSnapSetup : MonoBehaviour
{
    [Header("Roots")]
    [Tooltip("SteeringSystemSnap — children are the transparent ghost meshes (snap targets).")]
    public Transform ghostRoot;

    [Tooltip("SteeringSystem1 — children are the physical parts on the table.")]
    public Transform partsRoot;

    [Header("Materials")]
    [Tooltip("Transparent/ghost material applied to all SteeringSystemSnap children.")]
    public Material ghostMaterial;

    [Header("Snap Settings")]
    [Tooltip("Color shown on the ghost when its matching part is being held.")]
    public Color highlightColor = Color.green;

    [Tooltip("Emission intensity of the highlight.")]
    public float emissionIntensity = 2.5f;

    [System.Serializable]
    public struct NameMapping
    {
        [Tooltip("Name of the child in partsRoot (SteeringSystem1).")]
        public string physicalName;
        [Tooltip("Name of the matching child in ghostRoot (SteeringSystemSnap).")]
        public string ghostName;
    }

    [Header("Name Overrides")]
    [Tooltip("Explicit mappings for parts whose names differ between the two hierarchies.")]
    public NameMapping[] nameOverrides;

    class PartEntry
    {
        public Transform ghostChild;
        public Transform physicalPart;
        public MeshRenderer ghostRenderer;
        public bool isHeld;
        public bool isSnapped;
        public MaterialPropertyBlock mpb;
    }

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    readonly List<PartEntry> entries = new List<PartEntry>();

    // Tracks how many SnapNextFrame coroutines are still running.
    // Only when this reaches 0 AND all parts are logically snapped
    // is it safe to capture rest poses and unlock constraints.
    int pendingSnaps;

    /// <summary>
    /// True once every matched part is both logically snapped AND its
    /// SnapNextFrame coroutine has fully completed (FreezeAll applied,
    /// world position set).  Safe to read from another MonoBehaviour.
    /// </summary>
    public bool AllPartsFullySnapped =>
        pendingSnaps == 0 && IsAssemblyComplete();

    CenterUICursor cursor;

    void Start()
    {
        cursor = FindFirstObjectByType<CenterUICursor>();

        if (ghostRoot == null || partsRoot == null)
        {
            Debug.LogError("[SteeringSnapSetup] ghostRoot or partsRoot is not assigned.", this);
            return;
        }

        // Build name → ghost child lookup
        var ghostByName = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);
        foreach (Transform ghost in ghostRoot)
            ghostByName[ghost.name] = ghost;

        // Build override lookup: physicalName → ghostName
        var overrideMap = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        if (nameOverrides != null)
            foreach (var m in nameOverrides)
                if (!string.IsNullOrEmpty(m.physicalName) && !string.IsNullOrEmpty(m.ghostName))
                    overrideMap[m.physicalName] = m.ghostName;

        // 1. Make ghost children transparent
        foreach (Transform ghost in ghostRoot)
        {
            MeshRenderer mr = ghost.GetComponent<MeshRenderer>();
            if (mr == null) continue;

            if (ghostMaterial != null)
            {
                var mats = new Material[mr.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = ghostMaterial;
                mr.sharedMaterials = mats;
            }

            foreach (Collider col in ghost.GetComponentsInChildren<Collider>())
                if (!col.isTrigger) col.enabled = false;
        }

        // 2. Set up ALL physical parts as grabbable, link those that have a ghost match
        foreach (Transform part in partsRoot)
        {
            // Always make grabbable regardless of ghost match
            SetupPhysicalPart(part);

            // Resolve ghost: try exact name first, then override map
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
        // Tag + Layer
        part.gameObject.tag   = "Grabbable";
        part.gameObject.layer = LayerMask.NameToLayer("Interactable");

        // BoxCollider sized from mesh bounds
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

        // Rigidbody — kinematic by default; CenterUICursor takes over during grab
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

    void Update()
    {
        foreach (PartEntry entry in entries)
        {
            if (entry.isSnapped) continue;

            bool heldNow = cursor != null && cursor.HeldObject == entry.physicalPart.gameObject;

            // Grab started
            if (heldNow && !entry.isHeld)
            {
                entry.isHeld = true;
                SetGhostHighlight(entry, true);
            }

            // Released: cursor dropped this part this frame
            if (!heldNow && entry.isHeld)
            {
                entry.isHeld    = false;
                entry.isSnapped = true; // Prevent re-entry before coroutine completes
                SetGhostHighlight(entry, false);
                StartCoroutine(SnapNextFrame(entry));
            }
        }
    }

    /// <summary>
    /// Waits one frame so CenterUICursor.DropObject() fully finishes restoring physics,
    /// then hard-freezes and teleports the part to its ghost's world transform.
    /// </summary>
    IEnumerator SnapNextFrame(PartEntry entry)
    {
        pendingSnaps++;

        // Skip the frame where DropObject() ran — physics may override position same frame
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

        // Set Transform and Rigidbody in sync to avoid one-frame discrepancy
        entry.physicalPart.SetPositionAndRotation(
            entry.ghostChild.position,
            entry.ghostChild.rotation);

        if (rb != null)
        {
            rb.position = entry.ghostChild.position;
            rb.rotation = entry.ghostChild.rotation;
        }

        // Disable collider so this part no longer interferes with grabbing other parts
        Collider col = entry.physicalPart.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        pendingSnaps--;
    }

    void SetGhostHighlight(PartEntry entry, bool on)
    {
        if (entry.ghostRenderer == null) return;
        entry.ghostRenderer.GetPropertyBlock(entry.mpb);
        entry.mpb.SetColor(EmissionId,
            on ? highlightColor * emissionIntensity : Color.black);
        entry.ghostRenderer.SetPropertyBlock(entry.mpb);
    }

    /// <summary>
    /// Returns true when every matched part has been snapped into place.
    /// </summary>
    public bool IsAssemblyComplete()
    {
        if (entries.Count == 0) return false;
        foreach (PartEntry e in entries)
            if (!e.isSnapped) return false;
        return true;
    }
}
