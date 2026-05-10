using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives a full-screen white Image overlay (FlashOverlay) from alpha 0 → peakAlpha → 0.
/// Attach to the encouragement panel root alongside EngineEncouragementPanel.
/// Call Flash() when the panel appears.
/// </summary>
public class EncouragementFlash : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Tooltip("Full-stretch white Image used as the flash overlay. Must be last sibling to render on top.")]
    [SerializeField] private Image flashImage;

    [Tooltip("Peak alpha reached at the top of the flash pulse.")]
    [SerializeField] private float peakAlpha = 0.45f;

    [Tooltip("Seconds to rise from 0 to peakAlpha.")]
    [SerializeField] private float riseTime = 0.08f;

    [Tooltip("Seconds to fall from peakAlpha back to 0.")]
    [SerializeField] private float fallTime = 0.35f;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (flashImage != null)
            flashImage.color = new Color(1f, 1f, 1f, 0f);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Triggers the flash. Stops any in-progress flash and restarts from zero.</summary>
    public void Flash()
    {
        if (flashImage == null) return;
        StopAllCoroutines();
        StartCoroutine(FlashCoroutine());
    }

    // ── Coroutine ─────────────────────────────────────────────────────────────

    private IEnumerator FlashCoroutine()
    {
        // Rise phase
        float elapsed = 0f;
        while (elapsed < riseTime)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(0f, peakAlpha, elapsed / riseTime));
            yield return null;
        }

        SetAlpha(peakAlpha);

        // Fall phase
        elapsed = 0f;
        while (elapsed < fallTime)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(peakAlpha, 0f, elapsed / fallTime));
            yield return null;
        }

        SetAlpha(0f);
    }

    private void SetAlpha(float alpha)
    {
        Color c = flashImage.color;
        c.a = alpha;
        flashImage.color = c;
    }
}
