using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the final phase transition: steering assembly → car assembly → door open.
///
/// Flow:
///   1. This GameObject starts disabled; MoteurAssemblyManager activates it when
///      the player clicks "Proceed to Steering System".
///   2. Polls SteeringAssemblyController.IsSteeringComplete each frame.
///   3. When complete → "Proceed to Car Assembly" button turns green + pulse.
///   4. Player clicks button → 3D garage Button pulses red (emission) as a hint.
///   5. Player aims cursor at 3D Button and left-clicks → door opens via
///      the existing DoorButtonInteraction / GarageDoorController.Toggle() path.
/// </summary>
public class CarAssemblyManager : MonoBehaviour
{
    [Header("Assembly Gate")]
    [Tooltip("SteeringSnapSetup on SteeringSystem1 — AllPartsFullySnapped drives completion.")]
    public SteeringSnapSetup steeringSnapSetup;

    [Tooltip("SteeringAssemblyController whose IsSteeringComplete gate must be unlocked " +
             "for GarageDoorController.Toggle() to work.")]
    public SteeringAssemblyController steeringAssembly;

    [Header("Proceed Button UI")]
    public Button proceedButton;
    public Image  buttonImage;
    public Image  borderImage;

    [Tooltip("PrecedentButton — hidden permanently once the car assembly phase begins.")]
    public PrecedentButton precedentButton;

    [Header("Button Colors")]
    public Color lockedColor         = new Color(0.10f, 0.11f, 0.14f, 0.92f);
    public Color unlockedColor       = new Color(0.08f, 0.62f, 0.25f, 1.00f);
    public Color highlightFlashColor = new Color(0.12f, 0.78f, 0.32f, 1.00f);
    public Color borderLockedColor   = new Color(0.25f, 0.27f, 0.30f, 0.95f);
    public Color borderUnlockedColor = new Color(0.10f, 0.80f, 0.35f, 1.00f);

    [Header("Differential Assembly — inserted phase")]
    [Tooltip("DifferentialAssemblyManager — activated when the player proceeds from Steering.")]
    public DifferentialAssemblyManager differentialAssemblyManager;

    [Header("Steering Narration")]
    [Tooltip("Steering narration controller — started when all steering parts are snapped.")]
    public SteeringNarrationController steeringNarrationController;

    [Header("3D Button Highlight")]
    [Tooltip("MeshRenderer of the physical garage button in the scene.")]
    public MeshRenderer doorButtonRenderer;

    [Tooltip("Sub-material index of the red face on the 3D Button mesh (0-based).")]
    public int redMaterialIndex = 0;

    [Tooltip("Peak emission color applied to the red face while pulsing.")]
    public Color emissionColor = new Color(3f, 0.05f, 0.05f, 1f);

    [Tooltip("Pulse frequency in Hz.")]
    public float pulseFrequency = 1.8f;

    // ── Internal ──────────────────────────────────────────────────────────────

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    static readonly int BaseColorId     = Shader.PropertyToID("_BaseColor");

    bool             assemblyComplete;
    bool             buttonHighlightActive;
    MaterialPropertyBlock mpb;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
    }

    void Start()
    {
        // Hide completely — proceed is handled exclusively by the steering encouragement panel.
        if (proceedButton != null)
            proceedButton.gameObject.SetActive(false);

        // Ensure emission keyword is enabled on the door button material instance
        // so MaterialPropertyBlock can drive it at runtime.
        EnableDoorButtonEmission();
    }

    void Update()
    {
        // Poll SteeringSnapSetup — the real source of completion truth
        if (!assemblyComplete
            && steeringSnapSetup != null
            && steeringSnapSetup.AllPartsFullySnapped)
        {
            assemblyComplete = true;

            // Unlock the GarageDoorController gate so Toggle() can proceed
            if (steeringAssembly != null)
                steeringAssembly.IsSteeringComplete = true;

            // Start the steering narration sequence — the encouragement panel
            // owns the proceed action, so the top-right button is never shown.
            steeringNarrationController?.StartSequence();
        }

        // Pulse the 3D door button emission once activated
        if (buttonHighlightActive)
            PulseDoorButton();
    }

    // ── Button click ──────────────────────────────────────────────────────────

    /// <summary>
    /// Transitions to the Differential Assembly phase (inserted between Steering and Car).
    /// Wired as a persistent onClick call on the "Proceed to Differential Assembly" button.
    /// </summary>
    public void OnProceedClicked()
    {
        if (!assemblyComplete) return;

        // Hide this button — Differential phase manages its own forward button
        if (proceedButton != null)
            proceedButton.gameObject.SetActive(false);

        // Hide the steering Précédent button while in Differential phase (Bug 1 fix):
        // it should not be visible or clickable — DifferentialPrecedentButton handles back-nav.
        // We don't use HidePermanently() because we need to restore it if player goes back.
        if (precedentButton != null)
            precedentButton.gameObject.SetActive(false);

        // Hide SteeringSystem visuals — Differential phase owns the screen now (Bug 4 fix)
        if (steeringSnapSetup != null)
            steeringSnapSetup.gameObject.SetActive(false);

        // Activate Differential phase — BeginPhase() handles showing parts and ghosts
        if (differentialAssemblyManager != null)
        {
            differentialAssemblyManager.gameObject.SetActive(true);
            differentialAssemblyManager.BeginPhase();
        }
    }

    /// <summary>
    /// Activates the pulsing door-button highlight without requiring the steering assembly
    /// completion gate. Called by DifferentialAssemblyManager after differential is done
    /// and the car UI phase has already been shown via SteeringAssemblyController.
    /// </summary>
    public void ActivateDoorButtonHighlight()
    {
        buttonHighlightActive = true;
    }

    // ── 3D Button pulsing emission ────────────────────────────────────────────

    /// <summary>
    /// Creates a material instance for the red face (if not already done) and
    /// enables the _EMISSION keyword so the property block can drive brightness.
    /// </summary>
    void EnableDoorButtonEmission()
    {
        if (doorButtonRenderer == null) return;

        // .materials creates per-renderer instances — does not affect the shared asset
        Material[] mats = doorButtonRenderer.materials;
        if (redMaterialIndex >= mats.Length) return;

        mats[redMaterialIndex].EnableKeyword("_EMISSION");
        mats[redMaterialIndex].SetColor(EmissionColorId, Color.black);
        doorButtonRenderer.materials = mats;
    }

    /// <summary>
    /// Sine-wave pulse on the red sub-material using a per-renderer MaterialPropertyBlock.
    /// </summary>
    void PulseDoorButton()
    {
        if (doorButtonRenderer == null) return;

        float t       = (Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) * 0.5f) + 0.5f;
        Color pulsed  = emissionColor * Mathf.Lerp(0.5f, 2.8f, t);

        doorButtonRenderer.GetPropertyBlock(mpb, redMaterialIndex);
        mpb.SetColor(EmissionColorId, pulsed);
        doorButtonRenderer.SetPropertyBlock(mpb, redMaterialIndex);
    }

    // ── Unlock animation (mirrors MoteurAssemblyManager.UnlockPulse) ──────────

    IEnumerator UnlockPulse()
    {
        if (proceedButton == null) yield break;

        RectTransform rt = proceedButton.GetComponent<RectTransform>();
        const float Duration = 0.45f;
        float t = 0f;

        while (t < Duration)
        {
            t += Time.deltaTime;
            float norm  = t / Duration;
            float scale = 1f + 0.12f * Mathf.Sin(norm * Mathf.PI);
            rt.localScale = Vector3.one * scale;

            float flash = Mathf.Sin(norm * Mathf.PI);
            if (buttonImage != null)
                buttonImage.color = Color.Lerp(unlockedColor, highlightFlashColor, flash);
            if (borderImage != null)
                borderImage.color = Color.Lerp(borderUnlockedColor,
                    new Color(0.40f, 1.00f, 0.55f, 1f), flash);

            yield return null;
        }

        rt.localScale = Vector3.one;
        if (buttonImage != null) buttonImage.color = unlockedColor;
        if (borderImage != null) borderImage.color = borderUnlockedColor;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SetButtonLocked(bool locked)
    {
        if (proceedButton != null)
        {
            proceedButton.interactable = !locked;

            if (!locked)
            {
                ColorBlock cb        = proceedButton.colors;
                cb.normalColor       = Color.white;
                cb.highlightedColor  = new Color(1f, 1f, 1f, 0.88f);
                cb.pressedColor      = new Color(0.78f, 0.78f, 0.78f, 1f);
                cb.disabledColor     = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                cb.colorMultiplier   = 1f;
                cb.fadeDuration      = 0.12f;
                proceedButton.colors = cb;
            }
        }

        if (buttonImage != null)
            buttonImage.color = locked ? lockedColor : unlockedColor;
        if (borderImage != null)
            borderImage.color = locked ? borderLockedColor : borderUnlockedColor;
    }

    void OnDestroy()
    {
        // Clear emission on cleanup so the material isn't left glowing
        if (doorButtonRenderer != null && mpb != null)
        {
            doorButtonRenderer.GetPropertyBlock(mpb, redMaterialIndex);
            mpb.SetColor(EmissionColorId, Color.black);
            doorButtonRenderer.SetPropertyBlock(mpb, redMaterialIndex);
        }
    }

    static void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }
}
