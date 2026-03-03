using UnityEngine;
using UnityEngine.UI;

public class DragPreview : MonoBehaviour
{
    private RectTransform rectTransform;
    private Image image;
    private CanvasGroup canvasGroup;
    
    public static DragPreview Create(Canvas parentCanvas, Sprite icon)
    {
        GameObject previewObj = new GameObject("DragPreview");
        previewObj.transform.SetParent(parentCanvas.transform, false);
        
        DragPreview preview = previewObj.AddComponent<DragPreview>();
        preview.rectTransform = previewObj.AddComponent<RectTransform>();
        preview.rectTransform.sizeDelta = new Vector2(100, 100);
        
        preview.image = previewObj.AddComponent<Image>();
        preview.image.sprite = icon;
        preview.image.raycastTarget = false;
        
        preview.canvasGroup = previewObj.AddComponent<CanvasGroup>();
        preview.canvasGroup.alpha = 0.8f;
        preview.canvasGroup.blocksRaycasts = false;
        
        return preview;
    }
    
    public void UpdatePosition(Vector2 screenPosition)
    {
        if (rectTransform != null)
            rectTransform.position = screenPosition;
    }
    
    public void SetValid(bool isValid)
    {
        if (image != null)
            image.color = isValid ? Color.white : new Color(1f, 1f, 1f, 0.5f);
    }
    
    public void Destroy()
    {
        if (gameObject != null)
            Destroy(gameObject);
    }
}
