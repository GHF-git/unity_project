using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Extends the wheel inventory slot with:
///   • Bezier arc movement — the dragged wheel sweeps from its spawn origin to
///     the cursor each frame along a quadratic arc.
///   • Ghost preview — a semi-transparent wheel appears at the nearest unoccupied
///     snap zone while the cursor is within ghostActivationRange.
///   • Drag lock — dragging is blocked once WheelPlacementTracker.AllPlaced is true.
///   • Nearest-zone assignment — assigns the closest unoccupied SnapToPlace to the
///     spawned part just before DraggableInventoryItem hands it to CenterUICursor.
///
/// Must be placed BELOW DraggableInventoryItem in the component list so that
/// DraggableInventoryItem.OnBeginDrag runs first and spawns the 3D object.
///
/// Added automatically by InventorySystem.CreateInventorySlot() for "wheels" items.
/// </summary>
[RequireComponent(typeof(DraggableInventoryItem))]
public class WheelDragExtension : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Bezier Arc")]
    [Tooltip("World-unit height of the arc peak above the midpoint between origin and cursor.")]
    public float arcHeight = 1.5f;

    [Header("Ghost Preview")]
    [Tooltip("World-distance to nearest zone that activates the ghost. Forwarded to WheelGhostPreview.")]
    public float ghostActivationRange = 3f;

    // ── Private state ─────────────────────────────────────────────────────────

    private InventoryItemData itemData;
    private DraggableInventoryItem draggable;
    private SnapToPlace[] wheelZones;

    private Vector3 originWorldPos;
    private Camera mainCam;
    private bool isExtensionDragging;

    // ── Setup ─────────────────────────────────────────────────────────────────

    /// <summary>Called by InventorySystem.CreateInventorySlot() to provide item context.</summary>
    public void Configure(InventoryItemData data)
    {
        itemData = data;
    }

    void Awake()
    {
        draggable = GetComponent<DraggableInventoryItem>();
        mainCam   = Camera.main;
    }

    void Start()
    {
        // Cache all wheel snap zones once the scene is fully loaded.
        if (WheelPlacementTracker.Instance != null && WheelPlacementTracker.Instance.wheelsSnapParent != null)
            wheelZones = WheelPlacementTracker.Instance.wheelsSnapParent.GetComponentsInChildren<SnapToPlace>();
        else
            wheelZones = FindObjectsByType<SnapToPlace>(FindObjectsSortMode.None);

        // Propagate ghost activation range to the preview singleton if it exists.
        if (WheelGhostPreview.Instance != null)
            WheelGhostPreview.Instance.activationRange = ghostActivationRange;
    }

    // ── IBeginDragHandler ─────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Block dragging if all wheels are already placed.
        if (WheelPlacementTracker.Instance != null && WheelPlacementTracker.Instance.AllPlaced)
        {
            eventData.pointerDrag = null;   // cancels drag propagation
            return;
        }

        // Record the slot's world position as the Bezier start point.
        originWorldPos = transform.position;
        isExtensionDragging = true;

        // Activate ghost — DraggableInventoryItem has already run at this point.
        if (WheelGhostPreview.Instance != null && itemData != null)
            WheelGhostPreview.Instance.Activate(itemData.prefabToSpawn, wheelZones);
    }

    // ── IDragHandler ──────────────────────────────────────────────────────────

    public void OnDrag(PointerEventData eventData)
    {
        if (!isExtensionDragging) return;

        // Obtain the cursor's world position via a raycast against Ground/Default.
        Vector3 cursorWorldPos = ResolveCursorWorldPos(eventData);

        // Move the dragged 3D object along the Bezier arc (t = 1 → always at cursor).
        GameObject draggedObj = draggable.GetDraggedObject();
        if (draggedObj != null)
        {
            Vector3 p0 = originWorldPos;
            Vector3 p2 = cursorWorldPos;
            Vector3 p1 = (p0 + p2) * 0.5f + Vector3.up * arcHeight; // control point
            draggedObj.transform.position = EvaluateBezier(p0, p1, p2, t: 1f);
        }

        // Update ghost to track nearest unoccupied zone.
        if (WheelGhostPreview.Instance != null)
            WheelGhostPreview.Instance.UpdatePosition(cursorWorldPos);
    }

    // ── IEndDragHandler ───────────────────────────────────────────────────────

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isExtensionDragging) return;
        isExtensionDragging = false;

        // Assign the nearest unoccupied zone so CenterUICursor.DropObject() can snap to it.
        AssignNearestZone();

        // Deactivate ghost — DraggableInventoryItem.OnEndDrag hands off to CenterUICursor next.
        if (WheelGhostPreview.Instance != null)
            WheelGhostPreview.Instance.Deactivate();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Casts a ray from the camera through eventData.position against Ground and Default layers.
    /// Falls back to a fixed distance in front of the camera if nothing is hit.
    /// </summary>
    private Vector3 ResolveCursorWorldPos(PointerEventData eventData)
    {
        if (mainCam == null) return originWorldPos;

        Ray ray = mainCam.ScreenPointToRay(eventData.position);
        LayerMask mask = LayerMask.GetMask("Ground", "Default");

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, mask))
            return hit.point;

        return mainCam.transform.position + mainCam.transform.forward * draggable.dragDistance;
    }

    /// <summary>
    /// Evaluates a quadratic Bezier B(t) = (1-t)²·p0 + 2(1-t)t·p1 + t²·p2.
    /// At t=1 this always returns p2 (cursor position), so the object tracks the
    /// cursor exactly while the arc shape is still visible during the initial sweep.
    /// </summary>
    private static Vector3 EvaluateBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    /// <summary>
    /// Finds the nearest unoccupied wheel snap zone to the dragged object's current
    /// position and writes it into the spawned part's SnapZoneAssignment so that
    /// CenterUICursor.DropObject() knows which zone to attempt snapping into.
    /// </summary>
    private void AssignNearestZone()
    {
        GameObject draggedObj = draggable.GetDraggedObject();
        if (draggedObj == null || wheelZones == null) return;

        SnapZoneAssignment assignment = draggedObj.GetComponent<SnapZoneAssignment>();
        if (assignment == null) return;

        Vector3 partPos   = draggedObj.transform.position;
        SnapToPlace best  = null;
        float bestDist    = float.MaxValue;

        foreach (SnapToPlace zone in wheelZones)
        {
            if (zone == null || zone.IsOccupied()) continue;
            float d = Vector3.Distance(partPos, zone.snapPoint.position);
            if (d < bestDist) { bestDist = d; best = zone; }
        }

        if (best != null)
            assignment.assignedSnapZone = best;
    }
}
