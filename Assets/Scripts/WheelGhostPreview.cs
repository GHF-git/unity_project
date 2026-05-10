using UnityEngine;

/// <summary>
/// Singleton that manages a single semi-transparent ghost wheel shown at the
/// nearest unoccupied snap zone while the player is dragging a wheel from the
/// inventory. Call Activate() on drag start, UpdatePosition() each drag frame,
/// and Deactivate() on drag end.
/// </summary>
public class WheelGhostPreview : MonoBehaviour
{
    public static WheelGhostPreview Instance { get; private set; }

    [Header("Ghost Appearance")]
    [Tooltip("Semi-transparent URP/Lit material (Surface: Transparent, alpha ~0.4).")]
    public Material ghostMaterial;

    [Tooltip("World-space distance to the nearest zone within which the ghost appears.")]
    public float activationRange = 3f;

    // ── Runtime ──────────────────────────────────────────────────────────────

    private GameObject ghostInstance;
    private SnapToPlace[] cachedZones;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Instantiates a collider-stripped, material-replaced copy of the wheel
    /// prefab and caches the wheel snap zones to check each frame.
    /// </summary>
    public void Activate(GameObject prefab, SnapToPlace[] zones)
    {
        cachedZones = zones;

        if (ghostInstance != null)
            Destroy(ghostInstance);

        if (prefab == null) return;

        ghostInstance = Instantiate(prefab);
        ghostInstance.name = "WheelGhost";

        // Strip all colliders so the ghost doesn't interfere with trigger detection.
        foreach (Collider col in ghostInstance.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        // Remove rigidbody so physics doesn't move the ghost.
        Rigidbody rb = ghostInstance.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        // Replace every MeshRenderer material with the ghost material.
        if (ghostMaterial != null)
        {
            foreach (MeshRenderer mr in ghostInstance.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] mats = new Material[mr.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = ghostMaterial;
                mr.sharedMaterials = mats;
            }
        }

        ghostInstance.SetActive(false);
    }

    /// <summary>
    /// Moves the ghost to the nearest unoccupied zone when within activationRange;
    /// hides it otherwise. Call every drag frame.
    /// </summary>
    public void UpdatePosition(Vector3 cursorWorldPos)
    {
        if (ghostInstance == null || cachedZones == null) return;

        SnapToPlace nearest = FindNearestUnoccupied(cursorWorldPos, out float nearestDist);

        if (nearest != null && nearestDist <= activationRange)
        {
            ghostInstance.SetActive(true);
            ghostInstance.transform.SetPositionAndRotation(
                nearest.snapPoint.position,
                nearest.snapPoint.rotation);
        }
        else
        {
            ghostInstance.SetActive(false);
        }
    }

    /// <summary>Hides and destroys the ghost when drag ends.</summary>
    public void Deactivate()
    {
        if (ghostInstance != null)
        {
            Destroy(ghostInstance);
            ghostInstance = null;
        }
        cachedZones = null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private SnapToPlace FindNearestUnoccupied(Vector3 pos, out float bestDist)
    {
        SnapToPlace best = null;
        bestDist = float.MaxValue;

        if (cachedZones == null) return null;

        foreach (SnapToPlace zone in cachedZones)
        {
            if (zone == null || zone.IsOccupied()) continue;
            float d = Vector3.Distance(pos, zone.snapPoint.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = zone;
            }
        }
        return best;
    }
}
