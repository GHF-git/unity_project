using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class InventorySystem : MonoBehaviour
{
    [Header("UI References")]
    public GameObject inventoryPanel;
    public GameObject slotPrefab;

    private InventoryManager inventoryManager;
    private Camera mainCamera;

    void Awake()
    {
        inventoryManager = GetComponent<InventoryManager>();
        if (inventoryManager == null)
            inventoryManager = gameObject.AddComponent<InventoryManager>();
    }

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null) Debug.LogError("No main camera found!");
        RebuildInventoryUI();
    }

    public void RebuildInventoryUI()
    {
        foreach (Transform child in inventoryPanel.transform)
        {
            Destroy(child.gameObject);
        }

        List<InventoryItemData> items = inventoryManager.GetAvailableItems();
        
        foreach (InventoryItemData itemData in items)
        {
            CreateInventorySlot(itemData);
        }
    }

    private void CreateInventorySlot(InventoryItemData itemData)
    {
        GameObject slot = Instantiate(slotPrefab, inventoryPanel.transform);
        slot.name = "Slot_" + itemData.itemName;

        Image icon = slot.transform.Find("PartIcon")?.GetComponent<Image>();
        Text nameText = slot.transform.Find("PartName")?.GetComponent<Text>();

        if (icon != null && itemData.itemIcon != null)
            icon.sprite = itemData.itemIcon;

        if (nameText != null)
            nameText.text = itemData.itemName;

        DraggableInventoryItem draggable = slot.GetComponent<DraggableInventoryItem>();
        if (draggable == null)
            draggable = slot.AddComponent<DraggableInventoryItem>();

        draggable.iconImage = icon;
        draggable.Initialize(itemData, inventoryManager);
        draggable.placementLayerMask = LayerMask.GetMask("Ground", "Default");
        
        InventorySlotVisuals visuals = slot.GetComponent<InventorySlotVisuals>();
        if (visuals == null)
            visuals = slot.AddComponent<InventorySlotVisuals>();
        
        visuals.backgroundImage = slot.GetComponent<Image>();
        
        InventoryTooltip tooltip = slot.GetComponent<InventoryTooltip>();
        if (tooltip == null)
            tooltip = slot.AddComponent<InventoryTooltip>();
        
        tooltip.SetItemData(itemData);
    }
}