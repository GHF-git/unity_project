using UnityEngine;

/// <summary>
/// Singleton that tracks how many wheels have been snapped into place.
/// Subscribes to OnObjectSnappedEvent on every SnapToPlace child of
/// wheelsSnapParent. Disables the wheelSlotButton when all wheels are placed.
/// </summary>
public class WheelPlacementTracker : MonoBehaviour
{
    public static WheelPlacementTracker Instance { get; private set; }

    [Header("Wheel Snap Zones")]
    [Tooltip("Drag /car-snap/wheels-snap here. All SnapToPlace children are tracked.")]
    public Transform wheelsSnapParent;

    [Header("UI")]
    [Tooltip("The Slot_wheels UI GameObject to disable once all wheels are placed.")]
    public GameObject wheelSlotButton;

    /// <summary>How many wheels still need to be placed.</summary>
    public int WheelsRemaining { get; private set; }

    /// <summary>True when every wheel zone is filled.</summary>
    public bool AllPlaced => WheelsRemaining <= 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (wheelsSnapParent == null)
        {
            Debug.LogWarning("[WheelPlacementTracker] wheelsSnapParent is not assigned.");
            return;
        }

        SnapToPlace[] zones = wheelsSnapParent.GetComponentsInChildren<SnapToPlace>();
        WheelsRemaining = zones.Length;

        foreach (SnapToPlace zone in zones)
            zone.OnObjectSnappedEvent += OnWheelSnapped;
    }

    void OnDestroy()
    {
        // Unsubscribe cleanly to avoid stale delegates.
        if (wheelsSnapParent == null) return;
        SnapToPlace[] zones = wheelsSnapParent.GetComponentsInChildren<SnapToPlace>();
        foreach (SnapToPlace zone in zones)
            if (zone != null) zone.OnObjectSnappedEvent -= OnWheelSnapped;
    }

    private void OnWheelSnapped(GameObject obj, SnapToPlace zone)
    {
        WheelsRemaining = Mathf.Max(0, WheelsRemaining - 1);

        if (InventoryAudioManager.Instance != null)
            InventoryAudioManager.Instance.PlaySnapSuccess();

        if (WheelsRemaining <= 0 && wheelSlotButton != null)
            wheelSlotButton.SetActive(false);
    }
}
