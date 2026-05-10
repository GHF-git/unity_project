using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Drives the steering system educational narration sequence.
///
/// Call StartSequence() once all steering parts are assembled (triggered by CarAssemblyManager).
/// The controller fades in the instructor panel, then cycles through each EngineNarrationStep:
///   - Updates the UI label and subtitle text.
///   - Highlights named parts via MaterialPropertyBlock (HighlightParts).
///   - Highlights entire groups (e.g. steering_column sub-children) via HighlightGroups.
///   - Plays the voice-over AudioClip; falls back to a timed delay if the clip is null.
///   - Auto-advances when the clip finishes, or immediately when Space is pressed.
/// After the last step, clears all highlights and fades the panel out.
/// </summary>
public class SteeringNarrationController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Steps")]
    public List<EngineNarrationStep> steps = new();

    [Header("UI References")]
    [Tooltip("CanvasGroup on the InstructorPanel root — used to fade in/out.")]
    public CanvasGroup instructorPanel;

    [Tooltip("Image component showing the instructor avatar sprite.")]
    public Image instructorAvatar;

    [Tooltip("TextMeshProUGUI showing the step label (e.g. 'STEERING WHEEL').")]
    public TextMeshProUGUI stepLabel;

    [Tooltip("TextMeshProUGUI showing the narration subtitle.")]
    public TextMeshProUGUI narrationText;

    [Header("Audio")]
    public AudioSource audioSource;

    [Header("Highlight")]
    [Tooltip("Emissive tint applied to highlighted parts.")]
    public Color highlightColor = new Color(1f, 0.55f, 0f, 1f);

    [Tooltip("Multiplier applied to highlightColor for the emissive intensity.")]
    public float highlightIntensity = 2f;

    [Header("Encouragement")]
    [Tooltip("Panel shown after the last narration step — displays the badge and proceed button.")]
    public EngineEncouragementPanel encouragementPanel;

    [Tooltip("The top-level proceed button shown when steering is complete — hidden at sequence " +
             "start so only the encouragement panel button acts as the proceed trigger.")]
    public GameObject proceedButtonToHide;

    [Header("Timing")]
    [Tooltip("Duration in seconds for the instructor panel fade in/out.")]
    public float panelFadeDuration = 0.4f;

    [Tooltip("Fallback wait time in seconds when a step has no voice-over clip.")]
    public float fallbackDelay = 2.5f;

    [Header("Steering Root")]
    [Tooltip("Root transform of the physical steering parts (SteeringSystem1). Renderer cache is built from this.")]
    public Transform steeringRoot;

    // ── Runtime ───────────────────────────────────────────────────────────────

    /// <summary>True while the narration sequence is running.</summary>
    public bool IsSequenceActive { get; private set; }

    // Named-part renderer cache: keyed by gameObject.name.
    // Built in StartSequence() so no per-instance material copies are created before assembly.
    readonly Dictionary<string, List<MeshRenderer>> _renderersByName = new();

    // Renderers currently highlighted via HighlightGroups — cleared each step.
    readonly List<MeshRenderer> _activeGroupRenderers = new();

    // Shared empty block used to clear highlights — reused to avoid per-frame allocation.
    MaterialPropertyBlock _emptyBlock;

    Coroutine _sequenceCoroutine;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private const string EmissionKeyword        = "_EMISSION";

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        _emptyBlock = new MaterialPropertyBlock();

        // Ensure panel starts invisible and non-interactive.
        if (instructorPanel != null)
        {
            instructorPanel.alpha          = 0f;
            instructorPanel.blocksRaycasts = false;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Begins the steering narration sequence from step 0.
    /// Safe to call only once — guarded by IsSequenceActive.
    /// </summary>
    public void StartSequence()
    {
        if (IsSequenceActive)
        {
            Debug.LogWarning("[SteeringNarrationController] StartSequence called while already active.", this);
            return;
        }

        if (steps == null || steps.Count == 0)
        {
            Debug.LogWarning("[SteeringNarrationController] No narration steps assigned.", this);
            return;
        }

        // Cache renderers post-assembly so no per-instance material copies are created early.
        CacheRenderers();

        IsSequenceActive = true;

        if (_sequenceCoroutine != null)
            StopCoroutine(_sequenceCoroutine);

        _sequenceCoroutine = StartCoroutine(RunSequence());
    }

    // ── Sequence coroutine ────────────────────────────────────────────────────

    IEnumerator RunSequence()
    {
        yield return FadePanel(targetAlpha: 1f);

        for (int i = 0; i < steps.Count; i++)
        {
            EngineNarrationStep step = steps[i];

            if (step == null)
            {
                Debug.LogWarning($"[SteeringNarrationController] Step {i} is null — skipping.", this);
                continue;
            }

            UpdateUI(step);
            ClearHighlights();
            HighlightParts(step.highlightPartNames);
            HighlightGroups(step.highlightGroupNames);

            yield return PlayStep(step);
        }

        ClearHighlights();
        yield return FadePanel(targetAlpha: 0f);

        if (proceedButtonToHide != null)
            proceedButtonToHide.SetActive(false);

        encouragementPanel?.Show();

        IsSequenceActive = false;
    }

    // ── Step playback ─────────────────────────────────────────────────────────

    /// <summary>Plays the voice-over (or waits fallbackDelay), yielding until finished or Space is pressed.</summary>
    IEnumerator PlayStep(EngineNarrationStep step)
    {
        if (step.voiceOver != null)
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

    void UpdateUI(EngineNarrationStep step)
    {
        if (stepLabel != null)
            stepLabel.text = step.stepLabel;

        if (narrationText != null)
            narrationText.text = step.narrationText;
    }

    // ── Highlighting ──────────────────────────────────────────────────────────

    /// <summary>
    /// Caches all MeshRenderers under steeringRoot by gameObject.name.
    /// Uses sharedMaterial.EnableKeyword — never r.material — to avoid
    /// creating per-instance copies that break the SteeringSnapSetup ghost system.
    /// </summary>
    void CacheRenderers()
    {
        _renderersByName.Clear();

        if (steeringRoot == null)
        {
            Debug.LogWarning("[SteeringNarrationController] steeringRoot not assigned — highlight system disabled.", this);
            return;
        }

        MeshRenderer[] renderers = steeringRoot.GetComponentsInChildren<MeshRenderer>(includeInactive: true);

        foreach (MeshRenderer r in renderers)
        {
            string goName = r.gameObject.name;

            if (!_renderersByName.TryGetValue(goName, out List<MeshRenderer> list))
            {
                list = new List<MeshRenderer>();
                _renderersByName[goName] = list;
            }

            list.Add(r);

            // Enable keyword on the shared material so MPB emission values take effect.
            r.sharedMaterial?.EnableKeyword(EmissionKeyword);
        }
    }

    /// <summary>
    /// Applies an emissive tint to all renderers matching the given exact GameObject names.
    /// Uses MaterialPropertyBlock — never touches the shared material data.
    /// </summary>
    void HighlightParts(string[] partNames)
    {
        if (partNames == null || partNames.Length == 0) return;

        var block = new MaterialPropertyBlock();
        block.SetColor(EmissionColorId, highlightColor * highlightIntensity);

        foreach (string partName in partNames)
        {
            if (string.IsNullOrEmpty(partName)) continue;

            if (_renderersByName.TryGetValue(partName, out List<MeshRenderer> renderers))
            {
                foreach (MeshRenderer r in renderers)
                    r.SetPropertyBlock(block);
            }
            else
            {
                Debug.LogWarning($"[SteeringNarrationController] Part '{partName}' not found under steeringRoot.", this);
            }
        }
    }

    /// <summary>
    /// Highlights all renderers in the sub-tree of each named group transform under steeringRoot.
    /// Handles group containers whose parent has no renderer (e.g. steering_column).
    /// </summary>
    void HighlightGroups(string[] groupNames)
    {
        if (groupNames == null || groupNames.Length == 0) return;

        foreach (string groupName in groupNames)
        {
            if (string.IsNullOrEmpty(groupName)) continue;
            HighlightGroup(groupName);
        }
    }

    /// <summary>
    /// Finds a group Transform by name under steeringRoot and applies the emissive highlight
    /// to every MeshRenderer in its children (recursive). Tracks them in _activeGroupRenderers
    /// so ClearHighlights() can reset them.
    /// </summary>
    void HighlightGroup(string groupName)
    {
        if (steeringRoot == null) return;

        Transform group = steeringRoot.Find(groupName);

        if (group == null)
        {
            Debug.LogWarning($"[SteeringNarrationController] Group '{groupName}' not found under steeringRoot.", this);
            return;
        }

        var block = new MaterialPropertyBlock();
        block.SetColor(EmissionColorId, highlightColor * highlightIntensity);

        MeshRenderer[] groupRenderers = group.GetComponentsInChildren<MeshRenderer>(includeInactive: true);

        foreach (MeshRenderer r in groupRenderers)
        {
            r.SetPropertyBlock(block);
            _activeGroupRenderers.Add(r);
        }
    }

    /// <summary>
    /// Resets the MaterialPropertyBlock on all named-part and group renderers, removing the emissive tint.
    /// </summary>
    void ClearHighlights()
    {
        foreach (List<MeshRenderer> list in _renderersByName.Values)
            foreach (MeshRenderer r in list)
                r.SetPropertyBlock(_emptyBlock);

        foreach (MeshRenderer r in _activeGroupRenderers)
            r.SetPropertyBlock(_emptyBlock);

        _activeGroupRenderers.Clear();
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
