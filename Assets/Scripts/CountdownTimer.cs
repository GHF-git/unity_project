using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Displays an animated 3-2-1-GO! countdown in a race-game style.
/// Each number punches in large, hits a peak scale, then shrinks away.
/// "GO!" blasts in with a bright accent color and holds briefly before fading.
/// Locks the car via CarController.IsLocked until GO! fires.
/// </summary>
public class CountdownTimer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The full-screen countdown text element.")]
    public TextMeshProUGUI countdownText;

    [Tooltip("The car controller to unlock when GO! fires.")]
    public CarController carController;

    [Tooltip("The lap timer to start when GO! fires.")]
    public LapTimer lapTimer;

    [Header("Timing")]
    [Tooltip("Seconds each countdown number is held at full scale.")]
    public float holdSeconds = 0.65f;

    [Tooltip("Seconds for the punch-in scale animation.")]
    public float punchInDuration = 0.18f;

    [Tooltip("Seconds for the shrink-out animation.")]
    public float shrinkOutDuration = 0.22f;

    [Header("Colors")]
    [Tooltip("Color for 3-2-1 numbers.")]
    public Color numberColor = new Color(1f, 0.90f, 0.10f);   // bright yellow

    [Tooltip("Color for GO!")]
    public Color goColor = new Color(0.05f, 1f, 0.25f);        // vivid green

    [Tooltip("Outline color.")]
    public Color outlineColor = new Color(0f, 0f, 0f, 1f);

    [Header("Scale")]
    [Tooltip("Scale the text starts from on punch-in.")]
    public float startScale = 3.5f;

    [Tooltip("Overshoot scale reached at punch peak.")]
    public float peakScale = 1.15f;

    [Tooltip("Final resting scale for numbers (GO! holds at 1.4).")]
    public float restScale = 1f;

    void Start()
    {
        if (carController != null)
            carController.IsLocked = true;

        countdownText.gameObject.SetActive(false);
        StartCoroutine(RunCountdown());
    }

    IEnumerator RunCountdown()
    {
        // Brief pause before the countdown appears.
        yield return new WaitForSeconds(0.8f);

        countdownText.gameObject.SetActive(true);
        countdownText.outlineWidth = 0.30f;
        countdownText.outlineColor = outlineColor;

        string[] steps = { "3", "2", "1" };

        foreach (string step in steps)
        {
            yield return PlayStep(step, numberColor, restScale);
        }

        // GO! — unlock car and start timer the moment it appears.
        if (carController != null) carController.IsLocked = false;
        if (lapTimer      != null) lapTimer.StartTimer();

        yield return PlayStep("GO!", goColor, 1.4f);

        // Fade out.
        yield return FadeOut(0.3f);
        countdownText.gameObject.SetActive(false);
    }

    /// <summary>Animates a single countdown step: punch → hold → shrink.</summary>
    IEnumerator PlayStep(string label, Color color, float finalScale)
    {
        countdownText.text  = label;
        countdownText.color = color;

        // ── Punch in ─────────────────────────────────────────────────────────
        float elapsed = 0f;
        while (elapsed < punchInDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / punchInDuration;
            // Ease-out cubic: fast start, decelerates to peak.
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float s = Mathf.Lerp(startScale, peakScale, eased);
            countdownText.transform.localScale = Vector3.one * s;

            // Alpha punches from 0 → 1 in the first half.
            countdownText.alpha = Mathf.Clamp01(t * 2.5f);
            yield return null;
        }

        // ── Hold ─────────────────────────────────────────────────────────────
        countdownText.transform.localScale = Vector3.one * peakScale;
        countdownText.alpha = 1f;
        yield return new WaitForSeconds(holdSeconds);

        // ── Shrink to rest ────────────────────────────────────────────────────
        elapsed = 0f;
        while (elapsed < shrinkOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shrinkOutDuration;
            float eased = t * t;   // ease-in: slow start, accelerates away
            countdownText.transform.localScale = Vector3.one * Mathf.Lerp(peakScale, finalScale, eased);
            yield return null;
        }

        countdownText.transform.localScale = Vector3.one * finalScale;
    }

    /// <summary>Fades alpha from current value to zero over <paramref name="duration"/> seconds.</summary>
    IEnumerator FadeOut(float duration)
    {
        float startAlpha = countdownText.alpha;
        float elapsed    = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            countdownText.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
            yield return null;
        }
        countdownText.alpha = 0f;
    }
}
