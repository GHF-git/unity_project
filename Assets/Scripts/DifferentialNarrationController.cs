using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Drives the differential educational narration sequence.
///
/// Call StartSequence() once all differential parts are assembled (triggered by DifferentialAssemblyManager).
/// The controller fades in the instructor panel, then cycles through each DiffNarrationStep:
///   - Updates the UI step label and narration subtitle text.
///   - Executes the DiffStepAction for that step (PlayLockedTurn, IntroduceDifferential, highlight calls, etc.)
///   - Plays the voice-over AudioClip; falls back to a timed delay if the clip is null.
///   - Auto-advances when the clip finishes, or immediately when Space is pressed.
/// After the last step, clears all differential highlights and fades the panel out.
/// </summary>

public enum DiffStepAction
{
    None,
    PlayLockedTurn,
    IntroduceDifferential,
    HighlightRingGear,
    HighlightSpiderGears,
    HighlightSideGears,
    PlayDifferentialTurn,
    ClearHighlight,
}

[System.Serializable]
public class DiffNarrationStep
{
    public string stepLabel;
    [TextArea(2, 5)] public string narrationText;
    public AudioClip voiceOver;
    public DiffStepAction action = DiffStepAction.None;
}

public class DifferentialNarrationController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Steps")]
    public List<DiffNarrationStep> steps = new();

    [Header("UI References")]
    [Tooltip("CanvasGroup on the InstructorPanel root — used to fade in/out.")]
    public CanvasGroup instructorPanel;

    [Tooltip("TextMeshProUGUI showing the step label (e.g. 'THE PROBLEM').")]
    public TextMeshProUGUI stepLabel;

    [Tooltip("TextMeshProUGUI showing the narration subtitle.")]
    public TextMeshProUGUI narrationText;

    [Header("Audio")]
    public AudioSource audioSource;

    [Header("Controllers")]
    [Tooltip("DifferentialDemoController on /dif_demo_wheels — receives animation and highlight commands.")]
    public DifferentialDemoController demoController;

    [Header("Encouragement")]
    [Tooltip("Optional panel shown after the last narration step.")]
    public EngineEncouragementPanel encouragementPanel;

    [Header("Timing")]
    [Tooltip("Duration in seconds for the instructor panel fade in/out.")]
    public float panelFadeDuration = 0.4f;

    [Tooltip("Fallback wait time in seconds when a step has no voice-over clip.")]
    public float fallbackDelay = 2.5f;

    // ── Runtime ───────────────────────────────────────────────────────────────

    /// <summary>True while the narration sequence is running.</summary>
    public bool IsSequenceActive { get; private set; }

    Coroutine _sequenceCoroutine;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        if (instructorPanel != null)
        {
            instructorPanel.alpha          = 0f;
            instructorPanel.blocksRaycasts = false;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Begins the narration sequence from step 0.
    /// Safe to call only once — guarded by IsSequenceActive.
    /// </summary>
    public void StartSequence()
    {
        if (IsSequenceActive)
        {
            Debug.LogWarning("[DifferentialNarrationController] StartSequence called while already active.", this);
            return;
        }

        if (steps == null || steps.Count == 0)
        {
            Debug.LogWarning("[DifferentialNarrationController] No narration steps assigned.", this);
            return;
        }

        IsSequenceActive = true;

        if (_sequenceCoroutine != null)
            StopCoroutine(_sequenceCoroutine);

        _sequenceCoroutine = StartCoroutine(RunSequence());
    }

    // ── Sequence coroutine ────────────────────────────────────────────────────

    IEnumerator RunSequence()
    {
        yield return FadePanel(targetAlpha: 1f);

        // Show diagram from step 0 (INTRODUCTION).
        demoController?.ShowDiagram();

        for (int i = 0; i < steps.Count; i++)
        {
            DiffNarrationStep step = steps[i];

            if (step == null)
            {
                Debug.LogWarning($"[DifferentialNarrationController] Step {i} is null — skipping.", this);
                continue;
            }

            UpdateUI(step);
            ExecuteAction(step.action);

            yield return PlayStep(step);
        }

        demoController?.HighlightDifferential(false);

        // Stop demo: hides HUD and fades diagram out.
        demoController?.StopDemo();

        yield return FadePanel(targetAlpha: 0f);

        encouragementPanel?.Show();

        IsSequenceActive = false;
    }

    // ── Action dispatch ───────────────────────────────────────────────────────

    /// <summary>
    /// Dispatches the step action to the DifferentialDemoController.
    /// </summary>
    void ExecuteAction(DiffStepAction action)
    {
        if (demoController == null) return;

        switch (action)
        {
            case DiffStepAction.PlayLockedTurn:
                demoController.PlayLockedTurn();
                break;

            case DiffStepAction.IntroduceDifferential:
                demoController.IntroduceDifferential();
                break;

            case DiffStepAction.HighlightRingGear:
                demoController.HighlightComponent(demoController.ringGear);
                break;

            case DiffStepAction.HighlightSpiderGears:
                demoController.HighlightComponent(demoController.spiderGearLeft, demoController.spiderGearRight);
                break;

            case DiffStepAction.HighlightSideGears:
                demoController.HighlightComponent(demoController.differentialCarrier);
                break;

            case DiffStepAction.PlayDifferentialTurn:
                demoController.PlayDifferentialTurn();
                break;

            case DiffStepAction.ClearHighlight:
                demoController.HighlightDifferential(false);
                break;

            case DiffStepAction.None:
            default:
                break;
        }
    }

    // ── Step playback ─────────────────────────────────────────────────────────

    /// <summary>Plays the voice-over (or waits fallbackDelay), yielding until finished or Space is pressed.</summary>
    IEnumerator PlayStep(DiffNarrationStep step)
    {
        if (step.voiceOver != null && audioSource != null)
        {
            audioSource.clip = step.voiceOver;
            audioSource.Play();

            while (audioSource.isPlaying)
            {
                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    audioSource.Stop();
                    break;
                }

                yield return null;
            }
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < fallbackDelay)
            {
                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                    break;

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    void UpdateUI(DiffNarrationStep step)
    {
        if (stepLabel != null)
            stepLabel.text = step.stepLabel;

        if (narrationText != null)
            narrationText.text = step.narrationText;
    }

    // ── Panel fade ────────────────────────────────────────────────────────────

    IEnumerator FadePanel(float targetAlpha)
    {
        if (instructorPanel == null) yield break;

        float startAlpha = instructorPanel.alpha;
        float elapsed    = 0f;

        instructorPanel.blocksRaycasts = targetAlpha > 0f;

        while (elapsed < panelFadeDuration)
        {
            elapsed += Time.deltaTime;
            instructorPanel.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / panelFadeDuration);
            yield return null;
        }

        instructorPanel.alpha = targetAlpha;
    }
}
