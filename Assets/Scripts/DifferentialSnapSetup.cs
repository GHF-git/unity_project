using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sets up the Differential Assembly snap system at runtime, mirroring SteeringSnapSetup.
///
/// ghostRoot  (DifferentialPartsSnap) — transparent ghost meshes in assembled positions.
/// partsRoot  (DifferentialParts)     — physical grabbable parts on the table.
///
/// On BeginPhase():
///   1. All ghostRoot children → transparent material.
///   2. All partsRoot children → Rigidbody + BoxCollider + Grabbable tag/layer (if missing).
///   3. Parts are linked to ghosts by exact name match.
///   4. When a linked part is grabbed → matching ghost highlights green.
///   5. When released → part snaps to ghost position on the next frame.
///   6. AllPartsFullySnapped becomes true when every part is snapped.
/// </summary>
public class DifferentialSnapSetup : MonoBehaviour
{
    [Header("Roots")]
    [Tooltip("Ghost/snap target root — children are the transparent ghost meshes.")]
    public Transform ghostRoot;

    [Tooltip("Physical parts root — children are the 4 grabbable differential pieces.")]
    public Transform partsRoot;

    [Header("Materials")]
    [Tooltip("Transparent/ghost material applied to all ghostRoot children.")]
    public Material ghostMaterial;

    [Header("Snap Settings")]
    [Tooltip("Color shown on the ghost when its matching part is being held.")]
    public Color highlightColor = new Color(0f, 1f, 0.2f, 1f);

    [Tooltip("Emission intensity of the highlight.")]
    public float emissionIntensity = 2.5f;

    // ── Internal ───────────────────────────────────────────────────────────────

    class PartEntry
    {
        public Transform      ghostChild;
        public Transform      physicalPart;
        public MeshRenderer   ghostRenderer;
        public MaterialPropertyBlock mpb;
        public bool isHeld;
        public bool isSnapped;
    }

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    readonly List<PartEntry> entries = new List<PartEntry>();

    int pendingSnaps;

    bool setupDone;

    /// <summary>
    /// True once every matched part has been snapped AND all SnapNextFrame coroutines
    /// have completed. Safe to read from DifferentialAssemblyManager.
    /// </summary>
    public bool AllPartsFullySnapped =>
        setupDone && pendingSnaps == 0 && IsAssemblyComplete();

    CenterUICursor cursor;

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises ghosts and physical parts. Called by DifferentialAssemblyManager.BeginPhase().
    /// </summary>
    public void BeginSetup()
    {
        if (setupDone) return;
        setupDone = true;

        cursor = FindFirstObjectByType<CenterUICursor>();

        if (ghostRoot == null || partsRoot == null)
        {
            Debug.LogError("[DifferentialSnapSetup] ghostRoot or partsRoot is not assigned.", this);
            return;
        }

        // Build name → ghost lookup (case-insensitive)
        var ghostByName = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);
        foreach (Transform ghost in ghostRoot)
            ghostByName[ghost.name] = ghost;

        // 1. Apply ghost material to all ghost children
        foreach (Transform ghost in ghostRoot)
        {
            MeshRenderer mr = ghost.GetComponent<MeshRenderer>();
            if (mr != null && ghostMaterial != null)
            {
                var mats = new Material[mr.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = ghostMaterial;
                mr.sharedMaterials = mats;
            }
            foreach (Collider col in ghost.GetComponentsInChildren<Collider>())
                if (!col.isTrigger) col.enabled = false;
        }

        // 2. Set up each physical part and link it to its ghost
        foreach (Transform part in partsRoot)
        {
            EnsurePhysics(part);

            if (!ghostByName.TryGetValue(part.name, out Transform ghostChild))
                continue;

            MeshRenderer ghostMr = ghostChild.GetComponent<MeshRenderer>();
            if (ghostMr == null) continue;

            if (ghostMr.sharedMaterial != null)
                ghostMr.sharedMaterial.EnableKeyword("_EMISSION");

            var entry = new PartEntry
            {
                ghostChild    = ghostChild,
                physicalPart  = part,
                ghostRenderer = ghostMr,
                mpb           = new MaterialPropertyBlock()
            };
            SetGhostHighlight(entry, false);
            entries.Add(entry);
        }
    }

    // ── Unity ──────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!setupDone) return;

        foreach (PartEntry entry in entries)
        {
            if (entry.isSnapped) continue;

            bool heldNow = cursor != null
                && cursor.HeldObject == entry.physicalPart.gameObject;

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

    // ── Internal snap ──────────────────────────────────────────────────────────

    IEnumerator SnapNextFrame(PartEntry entry)
    {
        pendingSnaps++;
        yield return null;

        Rigidbody rb = entry.physicalPart.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic     = false;
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

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Returns true when every matched part has been snapped.</summary>
    public bool IsAssemblyComplete()
    {
        if (entries.Count == 0) return false;
        foreach (PartEntry e in entries)
            if (!e.isSnapped) return false;
        return true;
    }

    void SetGhostHighlight(PartEntry entry, bool on)
    {
        if (entry.ghostRenderer == null) return;
        entry.ghostRenderer.GetPropertyBlock(entry.mpb);
        entry.mpb.SetColor(EmissionId,
            on ? highlightColor * emissionIntensity : Color.black);
        entry.ghostRenderer.SetPropertyBlock(entry.mpb);
    }

    static void EnsurePhysics(Transform part)
    {
        part.gameObject.tag   = "Grabbable";
        part.gameObject.layer = LayerMask.NameToLayer("Interactable");

        if (part.GetComponent<Collider>() == null)
        {
            BoxCollider bc = part.gameObject.AddComponent<BoxCollider>();
            MeshFilter mf  = part.GetComponent<MeshFilter>();
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
}
