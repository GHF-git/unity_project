using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines a named group of steering parts that share a single inventory button.
/// Duplicate or paired parts (e.g., Left + Right tie rod ends) should be in the same group.
/// </summary>
[CreateAssetMenu(fileName = "New Steering Part Group",
                 menuName  = "Inventory/Steering Part Group")]
public class SteeringPartGroup : ScriptableObject
{
    [Tooltip("Display name shown on the inventory button, e.g. 'Tie Rod Ends (×2)'.")]
    public string groupName;

    [Tooltip("All InventoryItemData entries that belong to this group. " +
             "One prefab is spawned per entry when the button is clicked.")]
    public List<InventoryItemData> items = new List<InventoryItemData>();
}
