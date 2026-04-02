using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class InventorySlotVisuals : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Hover Effects")]
    public float hoverScale = 1.1f;
    public Color hoverColor = new Color(1f, 1f, 0.8f);
    public float animationSpeed = 10f;
    
    [Header("Disabled State")]
    public float disabledAlpha = 0.3f;
    public Color disabledColor = new Color(0.5f, 0.5f, 0.5f);
    
    [Header("References")]
    public Image backgroundImage;
    public CanvasGroup canvasGroup;
    
    private Vector3 originalScale;
    private Color originalColor;
    private bool isDisabled;
    private Vector3 targetScale;
    private Color targetColor;
    
    void Awake()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
        
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        
        originalScale = transform.localScale;
        if (backgroundImage != null)
            originalColor = backgroundImage.color;
        
        targetScale = originalScale;
        targetColor = originalColor;
    }
    
    void Update()
    {
        if (transform.localScale != targetScale)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * animationSpeed);
        }
        
        if (backgroundImage != null && backgroundImage.color != targetColor)
        {
            backgroundImage.color = Color.Lerp(backgroundImage.color, targetColor, Time.deltaTime * animationSpeed);
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isDisabled) return;
        
        targetScale = originalScale * hoverScale;
        targetColor = hoverColor;
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
        targetColor = originalColor;
    }
    
    public void SetDisabled(bool disabled)
    {
        isDisabled = disabled;
        
        if (disabled)
        {
            targetScale = originalScale;
            targetColor = disabledColor;
            if (canvasGroup != null)
                canvasGroup.alpha = disabledAlpha;
        }
        else
        {
            targetColor = originalColor;
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }
    }

    /// <summary>
    /// Sets the base (idle) color of the slot background.
    /// Must be called after Awake so originalColor is initialised.
    /// </summary>
    public void SetBaseColor(Color color)
    {
        originalColor = color;
        targetColor   = color;
        if (backgroundImage != null)
            backgroundImage.color = color;
    }
}
