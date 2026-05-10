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
/// Group containers (direct children with no MeshRenderer, e.g. cranckshaft):
///   The parent itself becomes the single grabbable (one Rigidbody, one compound
///   BoxCollider sized from all sub-child meshes). Sub-children carry no physics.
///   All sub-ghost renderers highlight together on grab. On release the parent
///   snaps to the ghost group's world transform — since sub-children share
///   identical local transforms in both hierarchies, every piece lands at the
///   correct assembled world position automatically.
///
/// AllPartsFullySnapped becomes true once every part and group has snapped and
/// all coroutines have finished — safe to poll from MoteurAssemblyManager.
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

    /// <summary>Single-mesh part: one physical object → one ghost target.</summary>
    class PartEntry
    {
        public Transform     ghostChild;
        public Transform     physicalPart;
        public MeshRenderer  ghostRenderer;
        public bool          isHeld;
        public bool          isSnapped;
        public MaterialPropertyBlock mpb;
    }

    /// <summary>
    /// Group container: the parent is the single grabbable; all sub-ghost
    /// renderers highlight together; the parent snaps to the ghost group parent.
    /// </summary>
    class GroupEntry
    {
        public Transform               physicalGroup;
        public Transform               ghostGroup;
        public MeshRenderer[]          ghostRenderers;
        public MaterialPropertyBlock[] mpbs;
        public bool isHeld;
        public bool isSnapped;
    }

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    readonly List<PartEntry>  entries      = new List<PartEntry>();
    readonly List<GroupEntry> groupEntries = new List<GroupEntry>();
    int pendingSnaps;

    /// <summary>
    /// True once every matched part and group is fully snapped and its
    /// coroutine has finished — safe to poll from MoteurAssemblyManager.
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

            // Group container (e.g. cranckshaft): no renderer on the parent itself —
            // apply ghost material and disable colliders on each sub-child instead.
            if (mr == null)
            {
                foreach (Transform subGhost in ghost)
                {
                    MeshRenderer subMr = subGhost.GetComponent<MeshRenderer>();
                    if (subMr != null && ghostMaterial != null)
                    {
                        var subMats = new Material[subMr.sharedMaterials.Length];
                        for (int i = 0; i < subMats.Length; i++) subMats[i] = ghostMaterial;
                        subMr.sharedMaterials = subMats;
                    }
                    foreach (Collider col in subGhost.GetComponentsInChildren<Collider>())
                        if (!col.isTrigger) col.enabled = false;
                }
                continue;
            }

            if (ghostMaterial != null)
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
            // ── Group container (no MeshRenderer on the direct child) ─────────
            if (part.GetComponent<MeshRenderer>() == null)
            {
                string groupLookup = part.name;
                if (overrideMap.TryGetValue(part.name, out string groupMapped))
                    groupLookup = groupMapped;

                if (!ghostByName.TryGetValue(groupLookup, out Transform ghostGroup))
                    continue;

                // The parent becomes the single grabbable.
                SetupGroupPhysics(part);

                // Collect all sub-ghost renderers for simultaneous highlighting.
                var renderers = new List<MeshRenderer>();
                foreach (Transform subGhost in ghostGroup)
                {
                    MeshRenderer subMr = subGhost.GetComponent<MeshRenderer>();
                    if (subMr == null) continue;
                    if (subMr.sharedMaterial != null)
                        subMr.sharedMaterial.EnableKeyword("_EMISSION");
                    renderers.Add(subMr);
                }

                var mpbs = new MaterialPropertyBlock[renderers.Count];
                for (int i = 0; i < mpbs.Length; i++) mpbs[i] = new MaterialPropertyBlock();

                var grp = new GroupEntry
                {
                    physicalGroup  = part,
                    ghostGroup     = ghostGroup,
                    ghostRenderers = renderers.ToArray(),
                    mpbs           = mpbs
                };

                SetGroupHighlight(grp, false);
                groupEntries.Add(grp);
                continue;
            }

            // ── Single-mesh part ──────────────────────────────────────────────
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

    /// <summary>
    /// Adds a single compound BoxCollider (sized from all sub-child mesh bounds)
    /// and a Rigidbody to the group parent. Sub-children get no physics.
    /// </summary>
    void SetupGroupPhysics(Transform group)
    {
        group.gameObject.tag   = "Grabbable";
        group.gameObject.layer = LayerMask.NameToLayer("Interactable");

        if (group.GetComponent<Collider>() == null)
        {
            // Accumulate world-space bounds across all sub-child renderers.
            Bounds worldBounds = new Bounds();
            bool first = true;
            foreach (Transform child in group)
            {
                MeshRenderer mr = child.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                if (first) { worldBounds = mr.bounds; first = false; }
                else worldBounds.Encapsulate(mr.bounds);
            }

            if (!first)
            {
                BoxCollider bc = group.gameObject.AddComponent<BoxCollider>();
                bc.center = group.InverseTransformPoint(worldBounds.center);
                Vector3 localSize = group.InverseTransformVector(worldBounds.size);
                bc.size = new Vector3(
                    Mathf.Abs(localSize.x),
                    Mathf.Abs(localSize.y),
                    Mathf.Abs(localSize.z));
            }
        }

        if (group.GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb              = group.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic            = true;
            rb.useGravity             = false;
            rb.mass                   = 1f;
            rb.linearDamping          = 5f;
            rb.angularDamping         = 5f;
            rb.interpolation          = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
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
        // Single-mesh parts
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

        // Group parts — grabbed and snapped as a single unit
        foreach (GroupEntry grp in groupEntries)
        {
            if (grp.isSnapped) continue;

            bool heldNow = cursor != null && cursor.HeldObject == grp.physicalGroup.gameObject;

            if (heldNow && !grp.isHeld)
            {
                grp.isHeld = true;
                SetGroupHighlight(grp, true);
            }

            if (!heldNow && grp.isHeld)
            {
                grp.isHeld    = false;
                grp.isSnapped = true;
                SetGroupHighlight(grp, false);
                StartCoroutine(SnapGroupNextFrame(grp));
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

    /// <summary>
    /// Waits one frame, then snaps the group parent to its ghost group's world
    /// transform. All sub-children follow automatically as Transform children.
    /// </summary>
    IEnumerator SnapGroupNextFrame(GroupEntry grp)
    {
        pendingSnaps++;
        yield return null;

        Rigidbody rb = grp.physicalGroup.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic     = true;
            rb.useGravity      = false;
            rb.constraints     = RigidbodyConstraints.FreezeAll;
        }

        grp.physicalGroup.SetPositionAndRotation(
            grp.ghostGroup.position,
            grp.ghostGroup.rotation);

        if (rb != null)
        {
            rb.position = grp.ghostGroup.position;
            rb.rotation = grp.ghostGroup.rotation;
        }

        Collider col = grp.physicalGroup.GetComponent<Collider>();
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

    /// <summary>Highlights or clears all sub-ghost renderers in a group simultaneously.</summary>
    void SetGroupHighlight(GroupEntry grp, bool on)
    {
        for (int i = 0; i < grp.ghostRenderers.Length; i++)
        {
            if (grp.ghostRenderers[i] == null) continue;
            grp.ghostRenderers[i].GetPropertyBlock(grp.mpbs[i]);
            grp.mpbs[i].SetColor(EmissionId,
                on ? highlightColor * emissionIntensity : Color.black);
            grp.ghostRenderers[i].SetPropertyBlock(grp.mpbs[i]);
        }
    }

    /// <summary>Returns true when every matched part and group has been snapped.</summary>
    public bool IsAssemblyComplete()
    {
        if (entries.Count == 0 && groupEntries.Count == 0) return false;
        foreach (PartEntry e in entries)
            if (!e.isSnapped) return false;
        foreach (GroupEntry g in groupEntries)
            if (!g.isSnapped) return false;
        return true;
    }
}
