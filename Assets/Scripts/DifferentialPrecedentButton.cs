using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the "Précédent" back-navigation button shown in the Differential Assembly phase.
///
/// Lifecycle:
///   - Starts hidden; revealed by DifferentialAssemblyManager.BeginPhase().
///   - On click: deactivates Differential Assembly and reactivates Steering System Assembly.
///   - Steering phase is restored showing only the "Proceed to Differential Assembly" button.
/// </summary>
public class DifferentialPrecedentButton : MonoBehaviour
{
    [Header("Self References")]
    [Tooltip("The Button component on this GameObject.")]
    public Button button;

    [Tooltip("Background Image — matches the other Précédent button style.")]
    public Image buttonImage;

    [Tooltip("GlowBorder Image child.")]
    public Image borderImage;

    [Header("Button Colors")]
    public Color normalColor = new Color(0.10f, 0.11f, 0.14f, 0.92f);
    public Color borderColor = new Color(0.25f, 0.27f, 0.30f, 0.95f);

    [Header("Differential Assembly Manager")]
    [Tooltip("DifferentialAssemblyManager — handles the phase transition back to Steering.")]
    public DifferentialAssemblyManager differentialAssemblyManager;

    // ── Unity ──────────────────────────────────────────────────────────────────

    void Start()
    {
        // activeSelf is already false in the scene — BeginPhase() reveals this button.
        // Do NOT call SetActive(false) here: Start() runs the first time the GameObject
        // is enabled, which is exactly when BeginPhase() activates it, so calling
        // SetActive(false) here would immediately hide it again.
        if (buttonImage != null) buttonImage.color = normalColor;
        if (borderImage  != null) borderImage.color  = borderColor;
    }

    // ── Button click ──────────────────────────────────────────────────────────

    /// <summary>
    /// Navigates back to the Steering System Assembly phase.
    /// Wired as the onClick on DifferentialPrecedentButton.
    /// </summary>
    public void OnPrecedentClicked()
    {
        if (differentialAssemblyManager != null)
            differentialAssemblyManager.NavigateBackToSteering();
    }
}
