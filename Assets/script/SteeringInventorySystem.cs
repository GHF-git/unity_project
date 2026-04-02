using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Populates a steering-specific inventory panel.
///
/// Groups identical or paired parts into a single button so the list is concise.
/// Each button spawns ALL parts in its group when clicked (they come pre-assigned
/// to their snap zones via SnapZoneAssignment on each spawned prefab).
///
/// Part groups are defined via <see cref="SteeringPartGroup"/> entries on this component.
/// </summary>
public class SteeringInventorySystem : MonoBehaviour
{
    [Header("UI")]
    public GameObject inventoryPanel;
    public GameObject slotPrefab;

    [Header("Steering Part Groups")]
    [Tooltip("Each entry represents one button in the list. " +
             "Add one entry per unique part group (paired or singular parts).")]
    public List<SteeringPartGroup> partGroups = new List<SteeringPartGroup>();

    // Track which groups are still available (not yet fully snapped)
    readonly Dictionary<SteeringPartGroup, GameObject> activeSlots =
        new Dictionary<SteeringPartGroup, GameObject>();

    InventoryManager inventoryManager;

    void Awake()
    {
        inventoryManager = GetComponentInParent<InventoryManager>();
        if (inventoryManager == null)
            inventoryManager = FindFirstObjectByType<InventoryManager>();
    }

    void Start()
    {
        BuildUI();
    }

    /// <summary>Rebuilds the steering part button list from scratch.</summary>
    public void BuildUI()
    {
        // Clear existing slots
        foreach (Transform child in inventoryPanel.transform)
            Destroy(child.gameObject);

        activeSlots.Clear();

        foreach (SteeringPartGroup group in partGroups)
        {
            if (group == null || group.items == null || group.items.Count == 0) continue;
            CreateGroupSlot(group);
        }
    }

    void CreateGroupSlot(SteeringPartGroup group)
    {
        GameObject slot = Instantiate(slotPrefab, inventoryPanel.transform);
        slot.name = "SteeringSlot_" + group.groupName;

        // Set the label to the group name
        Text nameText = slot.transform.Find("PartName")?.GetComponent<Text>();
        if (nameText != null)
            nameText.text = group.groupName;

        // Set icon from first item in group
        Image icon = slot.transform.Find("PartIcon")?.GetComponent<Image>();
        if (icon != null && group.items.Count > 0 && group.items[0] != null && group.items[0].itemIcon != null)
            icon.sprite = group.items[0].itemIcon;

        // Wire click — spawns the group via InventoryManager for each item
        Button btn = slot.GetComponent<Button>();
        if (btn == null) btn = slot.AddComponent<Button>();

        SteeringPartGroup capturedGroup = group;
        GameObject capturedSlot = slot;
        btn.onClick.AddListener(() => OnGroupClicked(capturedGroup, capturedSlot));

        activeSlots[group] = slot;

        // Subscribe to snap events for each item so we can remove the button when done
        SubscribeGroupToSnapEvents(group, slot);
    }

    void OnGroupClicked(SteeringPartGroup group, GameObject slot)
    {
        if (inventoryManager == null) return;

        // Spawn every item in the group via InventoryManager
        foreach (InventoryItemData item in group.items)
        {
            if (item == null) continue;
            inventoryManager.AddItem(item);
        }

        // Disable the button immediately — items are now in the world
        slot.SetActive(false);
    }

    void SubscribeGroupToSnapEvents(SteeringPartGroup group, GameObject slot)
    {
        // When all parts of this group are snapped, hide the button permanently
        int[] snappedCount = { 0 };
        int total = group.items.Count;

        SnapToPlace[] allZones = FindObjectsByType<SnapToPlace>(FindObjectsSortMode.None);

        foreach (InventoryItemData item in group.items)
        {
            if (item == null) continue;

            foreach (SnapToPlace zone in allZones)
            {
                if (!ZoneNameMatches(zone.gameObject.name, item.targetSnapZoneName)) continue;

                zone.OnObjectSnappedEvent += (obj, z) =>
                {
                    snappedCount[0]++;
                    if (snappedCount[0] >= total && slot != null)
                        Destroy(slot);
                };
                break;
            }
        }
    }

    static bool ZoneNameMatches(string zoneName, string targetName)
    {
        if (string.IsNullOrEmpty(zoneName) || string.IsNullOrEmpty(targetName)) return false;
        string a = zoneName.Replace("-snap", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();
        string b = targetName.Replace(" Zone", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();
        return a == b || a.Contains(b) || b.Contains(a);
    }
}
