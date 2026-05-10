using UnityEngine;

/// <summary>
/// Single source of truth for the enforced car assembly order.
///
/// Holds the fixed 21-part sequence and tracks which part is expected next.
/// Alert UI is owned entirely by AssemblyOrderValidator — this class only
/// manages the sequence index and delegates show/hide to the validator.
/// </summary>
public class AssemblyOrderManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static AssemblyOrderManager Instance { get; private set; }

    // ── Assembly sequence (index 0–21, final and never changed) ──────────────

    private static readonly string[] AssemblySequence = new[]
    {
        "base",            // 0  — matches Base.asset itemName
        "Rear Axles",      // 1  — matches Rear Axles.asset itemName
        "Engine",          // 2
        "Steering System", // 3
        "wheels",          // 4  — single wheels.asset used for all 4 wheels (dynamicZoneAssignment)
        "wheels",          // 5
        "wheels",          // 6
        "wheels",          // 7
        "body Pannels",    // 8  — matches Body Pannels.asset itemName (double-n)
        "Front Bumper",    // 9
        "Rear Bumper",     // 10
        "Hood",            // 11
        "Car Roof",        // 12
        "Left Door",       // 13
        "Right Door",      // 14
        "Trunk Lid",       // 15
        "Spoiler",         // 16
        "Left Headlight",  // 17
        "Right Headlight", // 18
        "Left Taillight",  // 19
        "Right Taillight", // 20
        "Differantiel",    // 21  — matches Differantiel.asset itemName (original spelling)
    };

    // Human-readable names shown inside the alert pill — parallel to AssemblySequence.
    private static readonly string[] DisplayNames = new[]
    {
        "Base",            // 0
        "Rear Axles",      // 1
        "Engine",          // 2
        "Steering System", // 3
        "Wheels (×4)",     // 4
        "Wheels (×4)",     // 5
        "Wheels (×4)",     // 6
        "Wheels (×4)",     // 7
        "Body Panels",     // 8
        "Front Bumper",    // 9
        "Rear Bumper",     // 10
        "Hood",            // 11
        "Car Roof",        // 12
        "Left Door",       // 13
        "Right Door",      // 14
        "Trunk Lid",       // 15
        "Spoiler",         // 16
        "Left Headlight",  // 17
        "Right Headlight", // 18
        "Left Taillight",  // 19
        "Right Taillight", // 20
        "Differential",    // 21
    };

    // ── Runtime state ─────────────────────────────────────────────────────────

    private int currentExpectedIndex;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        currentExpectedIndex = 0;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns the name of the part currently expected to be placed.</summary>
    public string ExpectedPartName =>
        currentExpectedIndex < AssemblySequence.Length
            ? AssemblySequence[currentExpectedIndex]
            : string.Empty;

    /// <summary>Returns the human-readable display name of the currently expected part.</summary>
    public string ExpectedPartDisplayName =>
        currentExpectedIndex < DisplayNames.Length
            ? DisplayNames[currentExpectedIndex]
            : string.Empty;

    /// <summary>
    /// Returns true if the item assigned to the given slot matches the currently
    /// expected part. Used by AssemblyOrderValidator to suppress proximity alerts
    /// while the correct part is being dragged.
    /// </summary>
    public bool IsCorrectPartBeingDragged(DraggableInventoryItem slot)
        => IsCorrectPart(slot != null ? slot.CurrentItemName : null);

    /// <summary>
    /// Returns true if partName matches the part expected at this moment.
    /// Comparison is case-insensitive and ignores surrounding whitespace.
    /// </summary>
    public bool IsCorrectPart(string partName)
    {
        if (currentExpectedIndex >= AssemblySequence.Length)
            return true; // sequence complete — allow anything

        return string.Equals(
            partName?.Trim(),
            AssemblySequence[currentExpectedIndex].Trim(),
            System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Advances the sequence to the next expected part.
    /// Must only be called after the current part has been successfully snapped.
    /// </summary>
    public void AdvanceToNextPart()
    {
        if (currentExpectedIndex < AssemblySequence.Length)
            currentExpectedIndex++;
    }

    /// <summary>
    /// Shows the wrong-order alert via AssemblyOrderValidator.
    /// Called by DraggableInventoryItem when a wrong part is dropped.
    /// </summary>
    public void ShowWrongOrderAlert()
    {
        AssemblyOrderValidator validator = FindFirstObjectByType<AssemblyOrderValidator>();
        if (validator != null)
            validator.ShowAlert();
    }
}
