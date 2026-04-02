using UnityEngine;

[CreateAssetMenu(fileName = "New Inventory Item", menuName = "Inventory/Item Data")]
public class InventoryItemData : ScriptableObject
{
    [Header("Item Information")]
    public string itemName;
    public Sprite itemIcon;
    
    [Header("3D Object")]
    public GameObject prefabToSpawn;

    [Tooltip("Scale applied to the spawned prefab. Adjust until the part visually matches the car. " +
             "Default is 5 because car parts in the scene are displayed at world scale 5 (snap zones: 100 × car-snap 0.05).")]
    public float spawnScale = 5f;
    
    [Header("Validation")]
    public string targetSnapZoneName;
}

