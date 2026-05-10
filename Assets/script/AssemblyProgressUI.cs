using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tracks assembly progress across all counted SnapToPlace zones and drives
/// a smoothly animated progress bar with a live percentage and parts counter.
///
/// Set <see cref="excludeSnapRoot"/> to skip zones that belong to a sub-assembly
/// (e.g. SteeringSystem-snap) so the car progress bar excludes steering parts.
/// </summary>
public class AssemblyProgressUI : MonoBehaviour
{
    [Header("UI References")]
    public Image progressBarFill;   // Filled image — fillAmount drives the bar
    public Image progressLineFill;  // Thin accent line at top of panel, same fill
    public Text percentLabel;       // e.g. "73%"
    public Text partsLabel;         // e.g. "11 / 15 parts"

    [Tooltip("The inventory panel to hide once assembly reaches 100%.")]
    public GameObject inventoryPanel;

    [Header("Filter")]
    [Tooltip("If assigned, SnapToPlace zones that are children of this Transform " +
             "will be excluded from the count (e.g. SteeringSystem-snap).")]
    public Transform excludeSnapRoot;

    [Header("Animation")]
    [Tooltip("Speed at which the bar and line slide toward the target fill amount.")]
    public float smoothSpeed = 4f;

    private readonly List<SnapToPlace> trackedZones = new List<SnapToPlace>();
    private float targetFill;
    private int cachedPlaced;
    private int cachedTotal;

    void Start()
    {
        RegisterZones();
        // Snap instantly to initial value — no slide-in on load
        if (progressBarFill != null)
            progressBarFill.fillAmount = targetFill;

        if (progressLineFill != null)
            progressLineFill.fillAmount = targetFill;

        UpdateLabels();
    }

    void Update()
    {
        float smoothed = progressBarFill != null
            ? Mathf.Lerp(progressBarFill.fillAmount, targetFill, Time.deltaTime * smoothSpeed)
            : targetFill;

        if (progressBarFill != null)
            progressBarFill.fillAmount = smoothed;

        if (progressLineFill != null)
            progressLineFill.fillAmount = smoothed;
    }

    private void RegisterZones()
    {
        SnapToPlace[] allZones = FindObjectsByType<SnapToPlace>(FindObjectsSortMode.None);
        foreach (SnapToPlace zone in allZones)
        {
            if (!zone.countInProgress) continue;
            if (IsExcluded(zone)) continue;

            trackedZones.Add(zone);
            zone.OnObjectSnappedEvent += OnSnapped;
            zone.OnObjectRemovedEvent += OnRemoved;
        }

        RecalculateCounts();
    }

    /// <summary>Returns true if the zone is a descendant of the excluded root.</summary>
    private bool IsExcluded(SnapToPlace zone)
    {
        if (excludeSnapRoot == null) return false;

        Transform t = zone.transform;
        while (t != null)
        {
            if (t == excludeSnapRoot) return true;
            t = t.parent;
        }
        return false;
    }

    private void OnSnapped(GameObject obj, SnapToPlace zone) => Refresh();
    private void OnRemoved(GameObject obj, SnapToPlace zone) => Refresh();

    /// <summary>Recalculates counts and sets the animated target fill.</summary>
    public void Refresh()
    {
        RecalculateCounts();
        UpdateLabels();
    }

    private void RecalculateCounts()
    {
        cachedTotal = trackedZones.Count;
        cachedPlaced = 0;

        foreach (SnapToPlace zone in trackedZones)
        {
            if (zone != null && zone.IsOccupied())
                cachedPlaced++;
        }

        targetFill = cachedTotal > 0 ? (float)cachedPlaced / cachedTotal : 0f;

        if (targetFill >= 1f && inventoryPanel != null && inventoryPanel.activeSelf)
            inventoryPanel.SetActive(false);
    }

    private void UpdateLabels()
    {
        int pct = Mathf.RoundToInt(targetFill * 100f);

        if (percentLabel != null)
            percentLabel.text = $"{pct}%";

        if (partsLabel != null)
            partsLabel.text = $"{cachedPlaced} / {cachedTotal} parts";
    }

    void OnDestroy()
    {
        foreach (SnapToPlace zone in trackedZones)
        {
            if (zone == null) continue;
            zone.OnObjectSnappedEvent -= OnSnapped;
            zone.OnObjectRemovedEvent -= OnRemoved;
        }
    }
}
