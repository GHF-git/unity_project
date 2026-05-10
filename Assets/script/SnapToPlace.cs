using UnityEngine;

/// <summary>
/// Snap zone that highlights when a Grabbable object is nearby and snaps it into
/// place when the player releases the object close enough to the snap point.
/// Works with both physics trigger detection (OnTriggerStay) and direct calls
/// from CenterUICursor.TrySnapObject().
/// </summary>
[RequireComponent(typeof(Collider))]
public class SnapToPlace : MonoBehaviour
{
    [Header("Snap Settings")]
    [Tooltip("The exact transform the part snaps to. Defaults to this transform if left empty.")]
    public Transform snapPoint;

    [Tooltip("World-space distance within which releasing the part triggers a snap.")]
    public float snapRange = 5f;

    [Tooltip("If set, only this specific GameObject can snap here.")]
    public GameObject objectToSnap;

    [Tooltip("Only objects with this tag can snap. Leave empty to accept any tag.")]
    public string grabbableTag = "Grabbable";

    [Header("Visual Feedback")]
    public Renderer zoneRenderer;
    public Color highlightColor = Color.green;
    public float emissionIntensity = 3f;

    [Header("Snap Behaviour")]
    public bool disableGravityAfterSnap = true;
    public bool lockObjectAfterSnap = true;

    [Header("Progress Tracking")]
    [Tooltip("When true, this zone is counted in the AssemblyProgressUI bar. " +
             "Uncheck for helper or sub-zones that shouldn't affect the overall count.")]
    public bool countInProgress = true;

    // Events fired after snap / removal
    public System.Action<GameObject, SnapToPlace> OnObjectSnappedEvent;
    public System.Action<GameObject, SnapToPlace> OnObjectRemovedEvent;

    private bool isGrabbed;
    private bool candidateInside;
    private GameObject candidate;
    private GameObject snappedObject;

    private MaterialPropertyBlock mpb;
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        if (!snapPoint) snapPoint = transform;
        if (!zoneRenderer) zoneRenderer = GetComponent<Renderer>();

        mpb = new MaterialPropertyBlock();
        if (zoneRenderer != null && zoneRenderer.sharedMaterial != null)
            zoneRenderer.sharedMaterial.EnableKeyword("_EMISSION");

        SetHighlight(false);
    }

    // -------------------------------------------------------------------------
    // Public API used by CenterUICursor
    // -------------------------------------------------------------------------

    /// <summary>Returns true if an object has already been snapped to this zone.</summary>
    public bool IsOccupied() => snappedObject != null;

    /// <summary>
    /// Call with true when the player grabs an object, false when they release it.
    /// Triggers the snap attempt on release.
    /// </summary>
    public void OnObjectGrabbed(bool grabbed)
    {
        isGrabbed = grabbed;
        if (!grabbed)
        {
            TrySnap();
            SetHighlight(false);
        }
        else
        {
            UpdateHighlight();
        }
    }

    /// <summary>
    /// Directly supply the object to snap. Called by CenterUICursor when the
    /// distance check already passed, bypassing the need for trigger entry.
    /// </summary>
    public void TrySnapObject(GameObject obj)
    {
        if (obj == null) return;
        candidate = obj;
        candidateInside = true;
        SnapCandidate();
    }

    // -------------------------------------------------------------------------
    // Physics trigger detection
    // -------------------------------------------------------------------------

    void OnTriggerStay(Collider other)
    {
        if (!IsValid(other)) return;

        // Prefer the rigidbody root so child-collider parts still register correctly
        candidateInside = true;
        candidate = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.gameObject;

        UpdateHighlight();
    }

    void OnTriggerExit(Collider other)
    {
        GameObject exitObj = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.gameObject;

        if (candidate != null && candidate == exitObj)
        {
            candidateInside = false;
            candidate = null;
            SetHighlight(false);
        }
    }

    // -------------------------------------------------------------------------
    // Internal snap logic
    // -------------------------------------------------------------------------

    void TrySnap()
    {
        if (!candidateInside || candidate == null) return;

        float d = Vector3.Distance(candidate.transform.position, snapPoint.position);
        if (d > snapRange) return;

        SnapCandidate();
    }

    void SnapCandidate()
    {
        if (candidate == null) return;

        Rigidbody rb = candidate.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = snapPoint.position;
            rb.rotation = snapPoint.rotation;

            if (disableGravityAfterSnap) rb.useGravity = false;
            if (lockObjectAfterSnap)
            {
                rb.isKinematic = true;
                rb.constraints = RigidbodyConstraints.FreezeAll;
            }
        }
        else
        {
            candidate.transform.SetPositionAndRotation(snapPoint.position, snapPoint.rotation);
        }

        snappedObject = candidate;
        OnObjectSnappedEvent?.Invoke(snappedObject, this);
    }

    void UpdateHighlight()
    {
        if (!isGrabbed || !candidateInside || candidate == null)
        {
            SetHighlight(false);
            return;
        }

        float d = Vector3.Distance(candidate.transform.position, snapPoint.position);
        SetHighlight(d <= snapRange);
    }

    bool IsValid(Collider other)
    {
        if (other == null) return false;

        // Check against the rigidbody root to handle child-collider prefabs
        GameObject obj = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.gameObject;

        if (objectToSnap != null && obj != objectToSnap) return false;
        if (!string.IsNullOrEmpty(grabbableTag) && !obj.CompareTag(grabbableTag)) return false;

        return true;
    }

    void SetHighlight(bool on)
    {
        if (zoneRenderer == null) return;
        zoneRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(EmissionId, on ? highlightColor * emissionIntensity : Color.black);
        zoneRenderer.SetPropertyBlock(mpb);
    }
}
