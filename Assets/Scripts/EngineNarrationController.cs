using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Drives the engine educational narration sequence.
///
/// Call StartSequence() once all engine parts are assembled (triggered by MoteurAssemblyManager).
/// The controller fades in the instructor panel, then cycles through each EngineNarrationStep:
///   - Updates the UI label and subtitle text.
///   - Highlights named parts via MaterialPropertyBlock (HighlightParts).
///   - Highlights entire groups (e.g. engine block sub-children) via HighlightGroups.
///   - On Step 1 (engine base), concurrently fades the engine block transparent.
///   - Plays the voice-over AudioClip; falls back to a timed delay if the clip is null.
///   - Auto-advances when the clip finishes, or immediately when Space is pressed.
///   - After Step 1 finishes, auto-starts the engine animation via MoteurAnimationController.
/// After the last step, clears all highlights and fades the panel out.
/// </summary>
public class EngineNarrationController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Steps")]
    public List<EngineNarrationStep> steps = new();

    [Header("UI References")]
    [Tooltip("CanvasGroup on the InstructorPanel root — used to fade in/out.")]
    public CanvasGroup instructorPanel;

    [Tooltip("Image component showing the instructor avatar sprite.")]
    public Image instructorAvatar;

    [Tooltip("TextMeshProUGUI showing the step label (e.g. 'PISTONS').")]
    public TextMeshProUGUI stepLabel;

    [Tooltip("TextMeshProUGUI showing the narration subtitle.")]
    public TextMeshProUGUI narrationText;

    [Header("Audio")]
    public AudioSource audioSource;

    [Header("Animation")]
    [Tooltip("Called after the engine base step (index 1) to auto-start the engine animation.")]
    public MoteurAnimationController animationController;

    [Header("Highlight")]
    [Tooltip("Emissive tint applied to highlighted parts.")]
    public Color highlightColor = new Color(1f, 0.55f, 0f, 1f);

    [Tooltip("Multiplier applied to highlightColor for the emissive intensity.")]
    public float highlightIntensity = 2f;

    [Header("Base Transparency")]
    [Tooltip("URP Surface Type: Transparent material swapped onto engine block renderers during Step 1.")]
    public Material transparentMaterial;

    [Tooltip("Duration in seconds for the engine base to fade from opaque to fully transparent.")]
    public float baseFadeDuration = 1.5f;

    [Header("Encouragement")]
    [Tooltip("Panel shown after the last narration step — displays the badge and proceed button.")]
    public EngineEncouragementPanel encouragementPanel;

    [Header("Timing")]
    [Tooltip("Duration in seconds for the instructor panel fade in/out.")]
    public float panelFadeDuration = 0.4f;

    [Tooltip("Fallback wait time in seconds when a step has no voice-over clip.")]
    public float fallbackDelay = 2.5f;

    [Header("Engine Root")]
    [Tooltip("Root transform of the physical engine parts (moteur2). Renderer cache is built from this.")]
    public Transform engineRoot;

    // ── Runtime ───────────────────────────────────────────────────────────────

    /// <summary>True while the narration sequence is running.</summary>
    public bool IsSequenceActive { get; private set; }

    // Named-part renderer cache: keyed by gameObject.name.
    // Built in StartSequence() — not Start() — so no per-instance material copies are created before assembly.
    readonly Dictionary<string, List<MeshRenderer>> _renderersByName = new();

    // Renderers currently highlighted via HighlightGroups — cleared alongside named parts each step.
    readonly List<MeshRenderer> _activeGroupRenderers = new();

    // Shared empty block used to clear highlights — reused to avoid per-frame allocation.
    MaterialPropertyBlock _emptyBlock;

    Coroutine _sequenceCoroutine;

    private static readonly int EmissionColorId   = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId       = Shader.PropertyToID("_BaseColor");
    private const string EmissionKeyword          = "_EMISSION";

    // Step index at which the engine animation is auto-started (engine base step).
    private const int AnimationTriggerStepIndex = 1;

    // Step index at which the engine block fades transparent (crankshaft step).
    private const int BaseFadeStepIndex = 2;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        _emptyBlock = new MaterialPropertyBlock();

        // Ensure panel starts invisible and non-interactive.
        if (instructorPanel != null)
        {
            instructorPanel.alpha = 0f;
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
            Debug.LogWarning("[EngineNarrationController] StartSequence called while already active.", this);
            return;
        }

        if (steps == null || steps.Count == 0)
        {
            Debug.LogWarning("[EngineNarrationController] No narration steps assigned.", this);
            return;
        }

        // Bug 1 fix: cache renderers here (post-assembly), not in Start().
        // Using sharedMaterial avoids creating per-instance material copies that break the snap ghost system.
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
                Debug.LogWarning($"[EngineNarrationController] Step {i} is null — skipping.", this);
                continue;
            }

            UpdateUI(step);
            ClearHighlights();
            HighlightParts(step.highlightPartNames);
            HighlightGroups(step.highlightGroupNames);

            // Fade engine block transparent concurrently with Step2_Crankshaft audio.
            // Group names come from Step1_Base (AnimationTriggerStepIndex) because "engine block"
            // is listed there — Step2 highlights the crankshaft, not the block.
            if (i == BaseFadeStepIndex && transparentMaterial != null)
                StartCoroutine(FadeBaseTransparent(steps[AnimationTriggerStepIndex].highlightGroupNames));

            yield return PlayStep(step);

            // Bug 2 fix: auto-start the engine animation after the engine base step completes.
            if (i == AnimationTriggerStepIndex)
                animationController?.StartAnimation();
        }

        ClearHighlights();
        yield return FadePanel(targetAlpha: 0f);

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
    /// Caches all MeshRenderers under engineRoot by gameObject.name.
    /// Uses sharedMaterial.EnableKeyword — never r.material — to avoid
    /// creating per-instance copies that break the MoteurSnapSetup ghost system.
    /// </summary>
    void CacheRenderers()
    {
        _renderersByName.Clear();

        if (engineRoot == null)
        {
            Debug.LogWarning("[EngineNarrationController] engineRoot not assigned — highlight system disabled.", this);
            return;
        }

        MeshRenderer[] renderers = engineRoot.GetComponentsInChildren<MeshRenderer>(includeInactive: true);

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
                Debug.LogWarning($"[EngineNarrationController] Part '{partName}' not found under engineRoot.", this);
            }
        }
    }

    /// <summary>
    /// Highlights all renderers in the sub-tree of each named group transform under engineRoot.
    /// Handles group containers (parent has no renderer) like the engine block.
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
    /// Finds a group Transform by name under engineRoot and applies the emissive highlight
    /// to every MeshRenderer in its children (recursive). Tracks them in _activeGroupRenderers
    /// so ClearHighlights() can reset them.
    /// </summary>
    void HighlightGroup(string groupName)
    {
        Transform group = engineRoot.Find(groupName);

        if (group == null)
        {
            Debug.LogWarning($"[EngineNarrationController] Group '{groupName}' not found under engineRoot.", this);
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
    /// Resets the MaterialPropertyBlock on all named-part and group renderers, removing any emissive tint.
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

    // ── Base transparency ─────────────────────────────────────────────────────

    /// <summary>
    /// Swaps each renderer in the given groups to transparentMaterial, then lerps
    /// _BaseColor alpha from 1 to 0 over baseFadeDuration using MaterialPropertyBlock.
    /// Runs concurrently with Step 1's audio playback via StartCoroutine.
    /// </summary>
    IEnumerator FadeBaseTransparent(string[] groupNames)
    {
        if (transparentMaterial == null || groupNames == null || groupNames.Length == 0)
            yield break;

        // Collect all renderers in the named groups.
        var targetRenderers = new List<MeshRenderer>();
        foreach (string groupName in groupNames)
        {
            if (string.IsNullOrEmpty(groupName)) continue;
            Transform group = engineRoot != null ? engineRoot.Find(groupName) : null;
            if (group == null) continue;
            targetRenderers.AddRange(group.GetComponentsInChildren<MeshRenderer>(includeInactive: true));
        }

        if (targetRenderers.Count == 0) yield break;

        // Swap to the transparent material on all slots.
        foreach (MeshRenderer r in targetRenderers)
        {
            int slotCount = r.sharedMaterials.Length;
            var mats = new Material[slotCount];
            for (int i = 0; i < slotCount; i++) mats[i] = transparentMaterial;
            r.sharedMaterials = mats;
        }

        // Lerp _BaseColor alpha 1 → 0.
        var block = new MaterialPropertyBlock();
        float elapsed = 0f;

        while (elapsed < baseFadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / baseFadeDuration);
            block.SetColor(BaseColorId, new Color(1f, 1f, 1f, alpha));

            foreach (MeshRenderer r in targetRenderers)
                r.SetPropertyBlock(block);

            yield return null;
        }

        // Fully hidden — disable renderers so they don't cost any draw calls.
        foreach (MeshRenderer r in targetRenderers)
        {
            r.SetPropertyBlock(_emptyBlock);
            r.enabled = false;
        }
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
