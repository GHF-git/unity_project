using UnityEngine;
using System.Collections.Generic;

public class InventoryManager : MonoBehaviour
{
    [Header("Inventory Data")]
    public List<InventoryItemData> availableItems = new List<InventoryItemData>();
    
    private Dictionary<InventoryItemData, GameObject> spawnedObjects = new Dictionary<InventoryItemData, GameObject>();
    private Dictionary<GameObject, InventoryItemData> objectToItemMap = new Dictionary<GameObject, InventoryItemData>();
    private Dictionary<InventoryItemData, GameObject> itemSlots = new Dictionary<InventoryItemData, GameObject>();
    private InventorySystem inventorySystem;
    
    void Awake()
    {
        inventorySystem = GetComponent<InventorySystem>();
    }
    
    void Start()
    {
        SubscribeToSnapZones();
    }
    
    private void SubscribeToSnapZones()
    {
        SnapToPlace[] snapZones = FindObjectsByType<SnapToPlace>(FindObjectsSortMode.None);
        foreach (SnapToPlace zone in snapZones)
        {
            zone.OnObjectSnappedEvent += HandleObjectSnapped;
            zone.OnObjectRemovedEvent += HandleObjectRemoved;
        }
    }
    
    public void OnItemSpawned(InventoryItemData itemData, GameObject spawnedObject, GameObject slotUI)
    {
        if (spawnedObject == null || itemData == null)
            return;
        
        if (spawnedObjects.ContainsKey(itemData))
        {
            GameObject oldObject = spawnedObjects[itemData];
            if (oldObject != null)
                Destroy(oldObject);
        }
        
        spawnedObjects[itemData] = spawnedObject;
        objectToItemMap[spawnedObject] = itemData;
        itemSlots[itemData] = slotUI;
        
        if (slotUI != null)
        {
            slotUI.SetActive(false);
            
            InventorySlotVisuals visuals = slotUI.GetComponent<InventorySlotVisuals>();
            if (visuals != null)
                visuals.SetDisabled(true);
        }
        
        StartCoroutine(MonitorSpawnedObject(itemData, spawnedObject));
    }
    
    private System.Collections.IEnumerator MonitorSpawnedObject(InventoryItemData itemData, GameObject spawnedObject)
    {
        float timeout = 30f;
        float elapsed = 0f;
        
        while (elapsed < timeout && spawnedObject != null)
        {
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
            
            if (spawnedObject == null)
            {
                RespawnItem(itemData);
                yield break;
            }
            
            SnapZoneAssignment assignment = spawnedObject.GetComponent<SnapZoneAssignment>();
            if (assignment != null && assignment.assignedSnapZone != null)
            {
                if (assignment.assignedSnapZone.IsOccupied())
                {
                    yield break;
                }
            }
        }
    }
    
    private void HandleObjectSnapped(GameObject snappedObject, SnapToPlace snapZone)
    {
        if (objectToItemMap.ContainsKey(snappedObject))
        {
            InventoryItemData itemData = objectToItemMap[snappedObject];
            
            SnapZoneAssignment assignment = snappedObject.GetComponent<SnapZoneAssignment>();
            if (assignment != null && assignment.assignedSnapZone == snapZone)
            {
                RemoveItemPermanently(itemData);

                // Notify AssemblyOrderManager that this part was correctly placed
                // so it can advance the sequence index to the next expected part.
                if (AssemblyOrderManager.Instance != null)
                    AssemblyOrderManager.Instance.AdvanceToNextPart();
            }
            else
            {
                if (InventoryAudioManager.Instance != null)
                    InventoryAudioManager.Instance.PlaySnapFail();
                
                RespawnItem(itemData);
                Destroy(snappedObject);
            }
        }
    }
    
    private void HandleObjectRemoved(GameObject removedObject, SnapToPlace snapZone)
    {
        if (objectToItemMap.ContainsKey(removedObject))
        {
            InventoryItemData itemData = objectToItemMap[removedObject];
            RespawnItem(itemData);
        }
    }
    
    public void RespawnItem(InventoryItemData itemData)
    {
        if (spawnedObjects.ContainsKey(itemData))
        {
            GameObject obj = spawnedObjects[itemData];
            if (obj != null)
                objectToItemMap.Remove(obj);
            spawnedObjects.Remove(itemData);
        }
        
        if (itemSlots.ContainsKey(itemData))
        {
            GameObject slot = itemSlots[itemData];
            if (slot != null)
            {
                slot.SetActive(true);
                
                InventorySlotVisuals visuals = slot.GetComponent<InventorySlotVisuals>();
                if (visuals != null)
                    visuals.SetDisabled(false);
            }
            itemSlots.Remove(itemData);
        }
        
        if (InventoryAudioManager.Instance != null)
            InventoryAudioManager.Instance.PlayItemRespawn();
    }
    
    public void RemoveItemPermanently(InventoryItemData itemData)
    {
        if (spawnedObjects.ContainsKey(itemData))
        {
            GameObject obj = spawnedObjects[itemData];
            if (obj != null)
                objectToItemMap.Remove(obj);
            spawnedObjects.Remove(itemData);
        }
        
        if (itemSlots.ContainsKey(itemData))
        {
            itemSlots.Remove(itemData);
        }
        
        availableItems.Remove(itemData);
    }
    
    public void AddItem(InventoryItemData itemData)
    {
        if (!availableItems.Contains(itemData))
        {
            availableItems.Add(itemData);
            if (inventorySystem != null)
                inventorySystem.RebuildInventoryUI();
        }
    }
    
    public List<InventoryItemData> GetAvailableItems()
    {
        return availableItems;
    }
}
