using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventoryTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Tooltip Settings")]
    public GameObject tooltipPanel;
    public Text tooltipText;
    public float showDelay = 0.5f;
    
    private InventoryItemData itemData;
    private float hoverTime;
    private bool isHovering;
    
    void Update()
    {
        if (isHovering)
        {
            hoverTime += Time.deltaTime;
            if (hoverTime >= showDelay && tooltipPanel != null)
            {
                ShowTooltip();
            }
        }
        
        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            Vector3 mousePos = Input.mousePosition;
            tooltipPanel.transform.position = mousePos + new Vector3(15f, -15f, 0f);
        }
    }
    
    public void SetItemData(InventoryItemData data)
    {
        itemData = data;
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        hoverTime = 0f;
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        hoverTime = 0f;
        HideTooltip();
    }
    
    private void ShowTooltip()
    {
        if (tooltipPanel == null || itemData == null)
            return;
        
        tooltipPanel.SetActive(true);
        
        if (tooltipText != null)
        {
            string tooltipContent = $"<b>{itemData.itemName}</b>\n";
            
            if (!string.IsNullOrEmpty(itemData.targetSnapZoneName))
            {
                tooltipContent += $"Place at: {itemData.targetSnapZoneName}";
            }
            else
            {
                tooltipContent += "Drag to place in world";
            }
            
            tooltipText.text = tooltipContent;
        }
    }
    
    private void HideTooltip()
    {
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }
    
    void OnDisable()
    {
        HideTooltip();
    }
}
