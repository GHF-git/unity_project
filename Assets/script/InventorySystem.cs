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

        // Apply slot color via the Button's ColorBlock so Unity's color-tint
        // transition doesn't override what we set on the Image directly.
        Color slotColor = GetSlotColor(itemData.itemName);
        Button btn = slot.GetComponent<Button>();
        if (btn != null)
        {
            ColorBlock cb = btn.colors;
            cb.normalColor      = slotColor;
            cb.highlightedColor = slotColor * 1.2f;
            cb.pressedColor     = slotColor * 0.8f;
            cb.selectedColor    = slotColor;
            btn.colors = cb;
        }

        // Also set the Image color directly so it's correct immediately.
        Image bg = slot.GetComponent<Image>();
        if (bg != null) bg.color = slotColor;
        
        InventorySlotVisuals visuals = slot.GetComponent<InventorySlotVisuals>();
        if (visuals == null)
            visuals = slot.AddComponent<InventorySlotVisuals>();
        
        visuals.backgroundImage = bg;
        visuals.SetBaseColor(slotColor);
        
        InventoryTooltip tooltip = slot.GetComponent<InventoryTooltip>();
        if (tooltip == null)
            tooltip = slot.AddComponent<InventoryTooltip>();
        
        tooltip.SetItemData(itemData);
    }

    /// <summary>Returns the background color for a slot based on its part name.</summary>
    private Color GetSlotColor(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return Color.black;

        string lower = itemName.ToLowerInvariant();

        if (lower.Contains("base"))   return Color.yellow;
        if (lower.Contains("button")) return Color.red;

        return Color.black;
    }
}