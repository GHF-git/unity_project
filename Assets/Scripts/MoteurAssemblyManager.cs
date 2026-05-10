using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Orchestrates the engine assembly phase:
///
///  • Polls MoteurSnapSetup.AllPartsFullySnapped every frame.
///  • "Proceed to Steering System" button:
///      - Dark slate + non-interactable until the engine is fully assembled.
///      - Emerald green + clickable + pulse animation once assembly is complete.
///  • Clicking the button:
///      - Hides moteur2 and moteurSnap.
///      - Shows SteeringSystem1 and SteeringSystemSnap.
///  • Blocks MoteurAnimationController from running (M key) until assembly is done.
/// </summary>
public class MoteurAssemblyManager : MonoBehaviour
{
    [Header("Snap Setup")]
    public MoteurSnapSetup moteurSnapSetup;

    [Header("Scene Roots")]
    public GameObject moteur2Root;
    public GameObject moteurSnapRoot;
    public GameObject steeringSystem1;
    public GameObject steeringSystemSnap;

    [Tooltip("PrecedentButton — shown when the player enters the steering phase.")]
    public PrecedentButton precedentButton;

    [Header("Engine Animation")]
    public MoteurAnimationController moteurAnimationController;

    [Header("Narration")]
    [Tooltip("Drives the instructor narration sequence once all engine parts are assembled.")]
    public EngineNarrationController narrationController;

    [Header("Proceed Button")]
    public Button proceedButton;
    public Image  buttonImage;
    [Tooltip("GlowBorder Image child — accent border that turns green on unlock.")]
    public Image  borderImage;

    [Header("Button Colors")]
    public Color lockedColor      = new Color(0.10f, 0.11f, 0.14f, 0.92f);
    public Color unlockedColor    = new Color(0.08f, 0.62f, 0.25f, 1.00f);
    public Color highlightColor   = new Color(0.12f, 0.78f, 0.32f, 1.00f);
    public Color pressedColor     = new Color(0.05f, 0.45f, 0.18f, 1.00f);
    public Color borderLockedColor   = new Color(0.25f, 0.27f, 0.30f, 0.95f);
    public Color borderUnlockedColor = new Color(0.10f, 0.80f, 0.35f, 1.00f);

    // ── Runtime ───────────────────────────────────────────────────────────────

    bool engineAssembled;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        SetActive(steeringSystem1,    false);
        SetActive(steeringSystemSnap, false);

        SetButtonLocked(true);

        if (proceedButton != null)
            proceedButton.onClick.AddListener(OnProceedClicked);

        if (moteurAnimationController != null)
            moteurAnimationController.IsUnlocked = false;
    }

    void Update()
    {
        if (engineAssembled) return;

        if (moteurSnapSetup != null && moteurSnapSetup.AllPartsFullySnapped)
        {
            engineAssembled = true;
            SetButtonLocked(false);

            if (moteurAnimationController != null)
                moteurAnimationController.IsUnlocked = true;

            narrationController?.StartSequence();

            StartCoroutine(UnlockPulse());
        }
    }

    // ── Unlock animation ──────────────────────────────────────────────────────

    /// <summary>
    /// Scale-pulse + colour flash when the button becomes active,
    /// drawing the player's eye to the newly enabled action.
    /// </summary>
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
                buttonImage.color = Color.Lerp(unlockedColor, highlightColor, flash);
            if (borderImage != null)
                borderImage.color = Color.Lerp(borderUnlockedColor,
                    new Color(0.40f, 1.00f, 0.55f, 1f), flash);

            yield return null;
        }

        rt.localScale = Vector3.one;
        if (buttonImage != null) buttonImage.color = unlockedColor;
        if (borderImage != null) borderImage.color = borderUnlockedColor;
    }

    // ── Button click ──────────────────────────────────────────────────────────

    void OnProceedClicked()
    {
        if (!engineAssembled) return;

        SetActive(moteur2Root,     false);
        SetActive(moteurSnapRoot,  false);
        SetActive(steeringSystem1,    true);
        SetActive(steeringSystemSnap, true);

        if (proceedButton != null)
            proceedButton.gameObject.SetActive(false);

        // Show Précédent — player can now navigate back to the engine phase
        if (precedentButton != null)
            precedentButton.Show();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SetButtonLocked(bool locked)
    {
        if (proceedButton != null)
        {
            proceedButton.interactable = !locked;

            if (!locked)
            {
                ColorBlock cb          = proceedButton.colors;
                cb.normalColor         = Color.white;
                cb.highlightedColor    = new Color(1f, 1f, 1f, 0.88f);
                cb.pressedColor        = new Color(0.78f, 0.78f, 0.78f, 1f);
                cb.disabledColor       = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                cb.colorMultiplier     = 1f;
                cb.fadeDuration        = 0.12f;
                proceedButton.colors   = cb;
            }
        }

        if (buttonImage != null)
            buttonImage.color = locked ? lockedColor : unlockedColor;

        if (borderImage != null)
            borderImage.color = locked ? borderLockedColor : borderUnlockedColor;
    }

    static void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }

    /// <summary>
    /// Restores the Proceed to Steering System button to its unlocked green state.
    /// Called by PrecedentButton when the player navigates back to the engine phase.
    /// Does not reset engineAssembled — the engine remains considered complete.
    /// </summary>
    public void RestoreProceedButton()
    {
        if (proceedButton == null) return;

        proceedButton.gameObject.SetActive(true);
        SetButtonLocked(false);
    }
}
