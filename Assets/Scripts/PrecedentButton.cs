using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the "Précédent" back-navigation button shown in the upper-left corner.
///
/// Lifecycle:
///   - Starts hidden (SetActive false in Start).
///   - Revealed via Show() when "Proceed to Steering System" is clicked.
///   - On click: restores engine phase objects and re-enables "Proceed to Steering System".
///   - Hidden permanently via HidePermanently() when "Proceed to Car Assembly" is clicked.
/// </summary>
public class PrecedentButton : MonoBehaviour
{
    [Header("Self References")]
    [Tooltip("The Button component on this GameObject.")]
    public Button button;

    [Tooltip("Background Image — tinted to match the other proceed buttons.")]
    public Image buttonImage;

    [Tooltip("GlowBorder Image child — accent border.")]
    public Image borderImage;

    [Header("Button Colors")]
    public Color normalColor = new Color(0.10f, 0.11f, 0.14f, 0.92f);
    public Color borderColor = new Color(0.25f, 0.27f, 0.30f, 0.95f);

    [Header("Engine Phase Objects")]
    [Tooltip("moteur2 root — shown when going back.")]
    public GameObject moteur2Root;

    [Tooltip("moteurSnap root — shown when going back.")]
    public GameObject moteurSnapRoot;

    [Tooltip("SteeringSystem1 — hidden when going back.")]
    public GameObject steeringSystem1;

    [Tooltip("SteeringSystemSnap — hidden when going back.")]
    public GameObject steeringSystemSnap;

    [Header("Managers")]
    [Tooltip("MoteurAssemblyManager — used to restore the Proceed to Steering button.")]
    public MoteurAssemblyManager moteurAssemblyManager;

    // ── Runtime ───────────────────────────────────────────────────────────────

    bool _permanentlyHidden;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        // Always starts hidden — revealed only after Proceed to Steering is clicked
        gameObject.SetActive(false);

        if (buttonImage != null) buttonImage.color = normalColor;
        if (borderImage  != null) borderImage.color  = borderColor;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Reveals and enables the button. Called by MoteurAssemblyManager when
    /// "Proceed to Steering System" is clicked. No-op if permanently hidden.
    /// </summary>
    public void Show()
    {
        if (_permanentlyHidden) return;

        gameObject.SetActive(true);

        if (button != null)
            button.interactable = true;
    }

    /// <summary>
    /// Hides the button permanently for the rest of the session.
    /// Called by CarAssemblyManager when "Proceed to Car Assembly" is clicked.
    /// </summary>
    public void HidePermanently()
    {
        _permanentlyHidden = true;
        gameObject.SetActive(false);
    }

    // ── Button click ──────────────────────────────────────────────────────────

    public void OnPrecedentClicked()
    {
        // Restore engine phase
        SetActive(moteur2Root,        true);
        SetActive(moteurSnapRoot,     true);

        // Hide steering phase
        SetActive(steeringSystem1,    false);
        SetActive(steeringSystemSnap, false);

        // Re-enable the "Proceed to Steering System" button in its correct state
        if (moteurAssemblyManager != null)
            moteurAssemblyManager.RestoreProceedButton();

        // Hide self — Show() will re-reveal it if the player proceeds to steering again
        gameObject.SetActive(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }
}
