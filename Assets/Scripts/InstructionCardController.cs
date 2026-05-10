using System.Collections;
using UnityEngine;

public class InstructionCardController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform _cardRect;
    [SerializeField] private GameObject _canvasRoot;

    private const float AnimDuration = 0.22f;

    private bool _isVisible;
    private Coroutine _animCoroutine;

    /// <summary>Exposes the canvas root so InstructionCardContent can hide it on Continue.</summary>
    public GameObject CanvasRoot => _canvasRoot;

    /// <summary>Activates and animates the instruction card. Safe to call once — guarded against repeats.</summary>
    public void ShowCard()
    {
        if (_isVisible) return;
        _isVisible = true;

        if (_canvasRoot == null)
        {
            Debug.LogWarning("[InstructionCard] canvasRoot not assigned!");
            return;
        }

        _canvasRoot.SetActive(true);

        // Always start animation from zero scale
        _cardRect.localScale = Vector3.zero;

        if (_animCoroutine != null)
            StopCoroutine(_animCoroutine);

        _animCoroutine = StartCoroutine(AnimateIn());
    }

    /// <summary>Replays the pop-in animation without toggling canvas visibility.</summary>
    public void ReplayAnimation()
    {
        _isVisible = false;
        _cardRect.localScale = Vector3.zero;

        if (_animCoroutine != null)
            StopCoroutine(_animCoroutine);

        _animCoroutine = StartCoroutine(AnimateIn());
    }

    private IEnumerator AnimateIn()
    {
        float elapsed = 0f;

        while (elapsed < AnimDuration)
        {
            float t = elapsed / AnimDuration;
            float scale;

            if (t < 0.40f)
            {
                float tPhase = t / 0.40f;
                scale = Mathf.Lerp(0f, 0.88f, Mathf.SmoothStep(0f, 1f, tPhase));
            }
            else if (t < 0.75f)
            {
                float tPhase = (t - 0.40f) / 0.35f;
                scale = Mathf.Lerp(0.88f, 1.05f, Mathf.SmoothStep(0f, 1f, tPhase));
            }
            else
            {
                float tPhase = (t - 0.75f) / 0.25f;
                scale = Mathf.Lerp(1.05f, 1.0f, Mathf.SmoothStep(0f, 1f, tPhase));
            }

            _cardRect.localScale = Vector3.one * scale;
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Hard-fix final state — no drift
        _cardRect.localScale = Vector3.one;
        _animCoroutine = null;
    }
}
