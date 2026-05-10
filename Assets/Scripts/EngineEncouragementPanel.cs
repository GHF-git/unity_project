using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the encouragement panel shown after the engine narration sequence ends.
/// Call Show() from EngineNarrationController once the last narration step completes.
///
/// Sequence:
///   1. Fades the panel CanvasGroup from alpha 0 to 1 over fadeDuration.
///   2. Plays a sine-curve scale bounce on headlineText.
///   3. Plays an overshoot spring pop-in on badgeRoot (0 → 1.15 → 1).
/// The panel stays visible; the ProceedToSteeringButton (child of this panel) handles its own click.
/// </summary>
public class EngineEncouragementPanel : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Panel")]
    [Tooltip("CanvasGroup on this panel root — used for alpha fade in.")]
    public CanvasGroup panelGroup;

    [Tooltip("Duration in seconds for the panel to fade in.")]
    public float fadeDuration = 0.45f;

    [Header("Content")]
    [Tooltip("Large headline TMP label, e.g. 'Great job!'. Receives a bounce animation on reveal.")]
    public TextMeshProUGUI headlineText;

    [Tooltip("Smaller subtitle TMP label shown below the headline.")]
    public TextMeshProUGUI subtitleText;

    [Header("Badge")]
    [Tooltip("Root RectTransform of the badge (engine icon + label). Receives a spring pop-in animation.")]
    public RectTransform badgeRoot;

    [Tooltip("Total duration in seconds for the badge pop-in spring animation.")]
    public float badgePopDuration = 0.4f;

    [Header("Effects")]
    [Tooltip("EncouragementChime component on this GameObject — plays a procedural chime on Show().")]
    [SerializeField] private EncouragementChime chime;

    [Tooltip("EncouragementFlash component on this GameObject — triggers a full-screen white flash on Show().")]
    [SerializeField] private EncouragementFlash flash;

    [Tooltip("ParticleSystem child (ConfettiParticles) — burst-played on Show().")]
    [SerializeField] private ParticleSystem confettiParticles;

    // ── Constants ─────────────────────────────────────────────────────────────

    private const float HeadlineBounceDuration = 0.35f;
    private const float BadgeOvershoot         = 1.15f;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        // Ensure the panel starts invisible and non-interactive.
        if (panelGroup != null)
        {
            panelGroup.alpha          = 0f;
            panelGroup.blocksRaycasts = false;
            panelGroup.interactable   = false;
        }

        // Badge starts at scale zero — hidden until the pop-in plays.
        if (badgeRoot != null)
            badgeRoot.localScale = Vector3.zero;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Reveals the panel with a fade-in, then plays the headline bounce and badge pop-in.
    /// Safe to call multiple times — subsequent calls are ignored if the panel is already visible.
    /// </summary>
    public void Show()
    {
        if (panelGroup != null && panelGroup.alpha >= 1f) return;
        StopAllCoroutines();
        // Always reset badge to zero so the pop-in plays from scratch on every Show().
        if (badgeRoot != null) badgeRoot.localScale = Vector3.zero;

        chime?.Play();
        flash?.Flash();
        confettiParticles?.Play();

        StartCoroutine(RevealCoroutine());
    }

    /// <summary>
    /// Fades the panel out and makes it non-interactive.
    /// Wire this to ProceedToSteeringButton.onClick in the inspector so the panel
    /// dismisses when the player proceeds to the steering assembly.
    /// </summary>
    public void Hide()
    {
        StopAllCoroutines();
        StartCoroutine(FadeOut());
    }

    // ── Reveal sequence ───────────────────────────────────────────────────────

    IEnumerator RevealCoroutine()
    {
        yield return FadeIn();
        yield return BounceHeadline();
        yield return BadgePopIn();
    }

    // ── Fade in ───────────────────────────────────────────────────────────────

    /// <summary>Lerps the panel CanvasGroup alpha from its current value to 1 over fadeDuration.</summary>
    IEnumerator FadeIn()
    {
        if (panelGroup == null) yield break;

        panelGroup.blocksRaycasts = true;
        panelGroup.interactable   = true;

        float startAlpha = panelGroup.alpha;
        float elapsed    = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed          += Time.deltaTime;
            panelGroup.alpha  = Mathf.Lerp(startAlpha, 1f, elapsed / fadeDuration);
            yield return null;
        }

        panelGroup.alpha = 1f;
    }

    // ── Fade out ──────────────────────────────────────────────────────────────

    /// <summary>Lerps the panel CanvasGroup alpha from its current value to 0 over fadeDuration.</summary>
    IEnumerator FadeOut()
    {
        if (panelGroup == null) yield break;

        float startAlpha = panelGroup.alpha;
        float elapsed    = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed          += Time.deltaTime;
            panelGroup.alpha  = Mathf.Lerp(startAlpha, 0f, elapsed / fadeDuration);
            yield return null;
        }

        panelGroup.alpha          = 0f;
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable   = false;
    }

    // ── Headline bounce ───────────────────────────────────────────────────────

    /// <summary>
    /// Sine-curve scale bounce on headlineText — same pattern as ReadyForTheRoadButton.UnlockPulse().
    /// Scale peaks at 1.12 at the midpoint then returns to 1.
    /// </summary>
    IEnumerator BounceHeadline()
    {
        if (headlineText == null) yield break;

        RectTransform rt = headlineText.GetComponent<RectTransform>();
        float elapsed = 0f;

        while (elapsed < HeadlineBounceDuration)
        {
            elapsed += Time.deltaTime;
            float norm  = elapsed / HeadlineBounceDuration;
            float scale = 1f + 0.12f * Mathf.Sin(norm * Mathf.PI);
            rt.localScale = Vector3.one * scale;
            yield return null;
        }

        rt.localScale = Vector3.one;
    }

    // ── Badge pop-in ──────────────────────────────────────────────────────────

    /// <summary>
    /// Two-phase spring animation on badgeRoot:
    ///   Phase 1 (first half of badgePopDuration): scale 0 → BadgeOvershoot (1.15).
    ///   Phase 2 (second half): scale BadgeOvershoot → 1.
    /// </summary>
    IEnumerator BadgePopIn()
    {
        if (badgeRoot == null) yield break;

        float halfDuration = badgePopDuration * 0.5f;
        float elapsed = 0f;

        // Phase 1 — scale up to overshoot.
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            badgeRoot.localScale = Vector3.one * Mathf.Lerp(0f, BadgeOvershoot, t);
            yield return null;
        }

        badgeRoot.localScale = Vector3.one * BadgeOvershoot;
        elapsed = 0f;

        // Phase 2 — settle back to 1.
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            badgeRoot.localScale = Vector3.one * Mathf.Lerp(BadgeOvershoot, 1f, t);
            yield return null;
        }

        badgeRoot.localScale = Vector3.one;
    }
}
