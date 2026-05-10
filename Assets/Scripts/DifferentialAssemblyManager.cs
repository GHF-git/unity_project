using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Orchestrates the Differential Assembly phase, inserted between
/// Steering System Assembly and Full Car Assembly.
///
/// Flow:
///   • CarAssemblyManager.OnProceedClicked() activates this GameObject and calls BeginPhase().
///   • Polls DifferentialSnapSetup.AllPartsFullySnapped each frame.
///   • "Proceed to Car Assembly" button (ProceedToCarAssemblyButton_Diff):
///       - Dark slate + non-interactable until all 4 differential pieces are snapped.
///       - Emerald green + clickable + pulse once assembly is complete.
///   • DifferentialPrecedentButton navigates back to Steering System Assembly.
/// </summary>
public class DifferentialAssemblyManager : MonoBehaviour
{
    [Header("Differential Snap Setup")]
    [Tooltip("DifferentialSnapSetup component that manages grab → snap for the 4 pieces.")]
    public DifferentialSnapSetup differentialSnapSetup;

    [Header("Differential Phase 3D Objects")]
    [Tooltip("Root containing the 4 physical differential pieces.")]
    public GameObject differentialPartsRoot;

    [Tooltip("Ghost/snap target root for differential (DifferentialPartsSnap).")]
    public GameObject differentialGhostRoot;

    [Header("Proceed to Car Assembly Button")]
    [Tooltip("ProceedToCarAssemblyButton_Diff — unlocked when all 4 parts are placed.")]
    public Button proceedButton;

    [Tooltip("Background Image of the proceed button.")]
    public Image buttonImage;

    [Tooltip("GlowBorder Image of the proceed button.")]
    public Image borderImage;

    [Header("Précédent Button")]
    [Tooltip("DifferentialPrecedentButton GameObject — shown when this phase is active.")]
    public GameObject differentialPrecedentButton;

    [Header("Steering Phase Objects — restored when going back")]
    [Tooltip("SteeringSystem1 root — reactivated when navigating back.")]
    public GameObject steeringSystem1;

    [Tooltip("SteeringSystemSnap root — reactivated when navigating back.")]
    public GameObject steeringSystemSnap;

    [Tooltip("PrecédentButton (steering phase) — re-shown when navigating back to Steering.")]
    public PrecedentButton steeringPrecedentButton;

    [Tooltip("car-snap/SteeringSystem-snap — hidden during Differential phase to isolate it.")]
    public GameObject carSteeringSnapZone;

    [Header("Car Assembly Transition")]
    [Tooltip("SteeringAssemblyController — called to show the car UI phase.")]
    public SteeringAssemblyController steeringAssemblyController;

    [Tooltip("CarAssemblyManager — used to activate the door button highlight.")]
    public CarAssemblyManager carAssemblyManager;

    [Tooltip("SteeringEncouragementPanel — hidden when navigating back to Steering so its " +
             "proceed button does not remain visible alongside the restored steering assembly.")]
    public EngineEncouragementPanel steeringEncouragementPanel;

    [Header("Phase 1 — Demo Animation")]
    [Tooltip("DifferentialDemoController on /dif_demo_wheels — called when all parts are snapped.")]
    public DifferentialDemoController demoController;

    [Tooltip("DifferentialNarrationController — drives the full educational sequence.")]
    public DifferentialNarrationController narrationController;

    [Tooltip("Root GameObject for the demo wheels — activated on assembly complete.")]
    public GameObject demoWheelRoot;

    [Tooltip("DifferentialEncouragementPanel — shown after narration completes. Contains the Proceed to Car Assembly button.")]
    public EngineEncouragementPanel differentialEncouragementPanel;

    [Header("Button Colors")]
    public Color lockedColor         = new Color(0.10f, 0.11f, 0.14f, 0.92f);
    public Color unlockedColor       = new Color(0.08f, 0.62f, 0.25f, 1.00f);
    public Color highlightFlashColor = new Color(0.12f, 0.78f, 0.32f, 1.00f);
    public Color borderLockedColor   = new Color(0.25f, 0.27f, 0.30f, 0.95f);
    public Color borderUnlockedColor = new Color(0.10f, 0.80f, 0.35f, 1.00f);

    // ── Runtime ────────────────────────────────────────────────────────────────

    bool differentialComplete;

    // ── Unity ──────────────────────────────────────────────────────────────────

    void Start()
    {
        SetButtonLocked(true);
    }

    /// <summary>
    /// Enters the Differential Assembly phase.
    /// Called by CarAssemblyManager.OnProceedClicked().
    /// </summary>
    public void BeginPhase()
    {
        differentialComplete = false;

        // Show 3D parts and ghost snaps
        SetActive(differentialPartsRoot, true);
        SetActive(differentialGhostRoot, true);

        // Hide any steering phase 3D objects that may still be active
        SetActive(steeringSystem1,    false);
        SetActive(steeringSystemSnap, false);

        // Hide the car-phase SteeringSystem snap zone — not needed during table assembly
        SetActive(carSteeringSnapZone, false);

        // Initialise snap-to-place for the 4 differential pieces
        if (differentialSnapSetup != null)
            differentialSnapSetup.BeginSetup();

        // Start locked — unlocked once all 4 parts are snapped
        SetButtonLocked(true);

        // Hide the old proceed button — encouragement panel is the new exit point
        if (proceedButton != null)
            proceedButton.gameObject.SetActive(false);
        SetActive(differentialPrecedentButton, true);
    }

    void Update()
    {
        if (differentialComplete) return;
        if (differentialSnapSetup == null) return;

        if (differentialSnapSetup.AllPartsFullySnapped)
        {
            differentialComplete = true;

            // Activate demo wheels and start the full narration sequence.
            // The encouragement panel is shown by DifferentialNarrationController at sequence end.
            if (demoWheelRoot != null) demoWheelRoot.SetActive(true);
            narrationController?.StartSequence();
        }
    }

    // ── Button clicks ──────────────────────────────────────────────────────────

    /// <summary>
    /// Transitions to Full Car Assembly.
    /// Wired as the onClick on ProceedToCarAssemblyButton_Diff.
    /// </summary>
    public void OnProceedToCarClicked()
    {
        if (!differentialComplete) return;

        // Hide differential UI
        differentialEncouragementPanel?.Hide();
        SetActive(differentialPrecedentButton, false);
        if (proceedButton != null) proceedButton.gameObject.SetActive(false);

        // Hide differential 3D objects
        SetActive(differentialPartsRoot,  false);
        SetActive(differentialGhostRoot,  false);
        if (demoWheelRoot != null) demoWheelRoot.SetActive(false);

        // Permanently hide the steering Précédent — no going back from Car Assembly
        if (steeringPrecedentButton != null)
            steeringPrecedentButton.HidePermanently();

        // Restore car-phase SteeringSystem snap zone — was hidden during Differential phase
        SetActive(carSteeringSnapZone, true);

        // Show the car assembly UI
        if (steeringAssemblyController != null)
            steeringAssemblyController.ShowCarPhase();

        // Start door-button pulse
        if (carAssemblyManager != null)
            carAssemblyManager.ActivateDoorButtonHighlight();
    }

    /// <summary>
    /// Returns to Steering System Assembly.
    /// Called by DifferentialPrecedentButton.OnPrecedentClicked().
    /// </summary>
    public void NavigateBackToSteering()
    {
        // Hide the steering and differential encouragement panels — their buttons must not remain
        // visible alongside the restored steering assembly UI.
        steeringEncouragementPanel?.Hide();
        differentialEncouragementPanel?.Hide();

        // Hide this phase's UI
        SetActive(differentialPrecedentButton, false);
        if (proceedButton != null) proceedButton.gameObject.SetActive(false); // Bug 2: explicitly hidden

        // Hide differential 3D objects
        SetActive(differentialPartsRoot, false);
        SetActive(differentialGhostRoot, false);

        // Deactivate self — CarAssemblyManager will re-activate on next forward transition
        gameObject.SetActive(false);

        // Restore steering phase 3D objects
        SetActive(steeringSystem1,    true);
        SetActive(steeringSystemSnap, true);

        // Restore SteeringSystem-snap on the car so it becomes active again
        // (it is only used in the car phase, not the table phase, but restore to its prior state)
        SetActive(carSteeringSnapZone, true);

        // Show Steering Précédent again (Bug 1: steering Précédent was hidden on entering Differential)
        if (steeringPrecedentButton != null)
            steeringPrecedentButton.Show();

    }

    // ── Unlock animation ──────────────────────────────────────────────────────

    IEnumerator UnlockPulse()
    {
        if (proceedButton == null) yield break;

        RectTransform rt     = proceedButton.GetComponent<RectTransform>();
        const float Duration = 0.45f;
        float t              = 0f;

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

    static void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }
}
