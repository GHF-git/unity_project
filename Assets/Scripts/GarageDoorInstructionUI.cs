using System.Collections;
using UnityEngine;

/// <summary>
/// Controls the visibility of the GarageDoorInstructionSubtitle panel.
/// Show() is called directly by SteeringAssemblyController.ShowCarPhase() — i.e. exactly
/// when the user clicks "Proceed to Car Assembly". Fades out when the door starts moving.
/// </summary>
public class GarageDoorInstructionUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("GarageDoorController whose onDoorStartedMoving event dismisses the subtitle.")]
    [SerializeField] private GarageDoorController garageDoor;

    [Header("Animation")]
    [Tooltip("Duration of the fade-in and fade-out transitions in seconds.")]
    [SerializeField] private float fadeDuration = 0.4f;

    // ── Private state ────────────────────────────────────────────────────────

    private CanvasGroup _canvasGroup;
    private Coroutine   _fadeCoroutine;

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();

        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _canvasGroup.alpha          = 0f;
        _canvasGroup.interactable   = false;
        _canvasGroup.blocksRaycasts = false;
    }

    private void OnEnable()
    {
        if (garageDoor != null)
            garageDoor.onDoorStartedMoving.AddListener(Hide);
    }

    private void OnDisable()
    {
        if (garageDoor != null)
            garageDoor.onDoorStartedMoving.RemoveListener(Hide);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Fades the subtitle panel in. Called by SteeringAssemblyController.ShowCarPhase().</summary>
    public void Show()
    {
        _canvasGroup.interactable   = true;
        _canvasGroup.blocksRaycasts = true;
        StartFade(1f);
    }

    /// <summary>Fades the subtitle panel out.</summary>
    public void Hide()
    {
        _canvasGroup.interactable   = false;
        _canvasGroup.blocksRaycasts = false;
        StartFade(0f);
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private void StartFade(float targetAlpha)
    {
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);

        _fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha));
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        float startAlpha = _canvasGroup.alpha;
        float elapsed    = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed              += Time.deltaTime;
            _canvasGroup.alpha    = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        _canvasGroup.alpha = targetAlpha;
        _fadeCoroutine     = null;
    }
}
