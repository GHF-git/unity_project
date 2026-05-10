using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Procedural 6-piston crank-slider engine animation.
///
/// Activated by pressing M, but only after MoteurAssemblyManager has set
/// IsUnlocked = true (i.e., all engine parts have been fully snapped).
///
/// Piston positions are driven by reading the real world position of each
/// crank pin (child of the rotating cranckshaft group) every frame, so the
/// throw radius and phase are always geometrically correct — no explicit
/// crankRadius or phase-offset constants are needed.
///
/// Pin → piston/rod mapping:
///   crankPin12 (part.723) → rod1 + rod2, piston1 + piston2
///   crankPin34 (part.724) → rod3 + rod4, piston3 + piston4
///   crankPin56 (part.610) → rod5 + rod6, piston5 + piston6
///
/// Pistons slide along world X (left bank odd pistons X ≈ 0.349, right bank
/// even pistons X ≈ 0.658). Both pistons in a pair ride the same pin and
/// therefore receive the same X displacement — no per-piston mirroring.
/// </summary>
public class MoteurAnimationController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Crankshaft Assembly — crankshaft group rotates around Y; gear rotates around Z")]
    public Transform crankshaft;
    public Transform crankshaftAxle;
    public Transform crankshaftGear;
    public Transform axleSpacer1;
    public Transform axleSpacer2;

    [Header("Driven Gear Assembly — counter-rotate around Z")]
    public Transform drivenGear;
    public Transform drivenAxle;

    [Header("Crank Pins — children of cranckshaft, one per piston pair")]
    [Tooltip("Crank pin for piston pair 1+2 (part.723).")]
    public Transform crankPin12;

    [Tooltip("Crank pin for piston pair 3+4 (part.724).")]
    public Transform crankPin34;

    [Tooltip("Crank pin for piston pair 5+6 (part.610).")]
    public Transform crankPin56;

    [Header("Pistons")]
    public Transform piston1;
    public Transform piston2;
    public Transform piston3;
    public Transform piston4;
    public Transform piston5;
    public Transform piston6;

    [Header("Connecting Rods")]
    public Transform connectingRod1;
    public Transform connectingRod2;
    public Transform connectingRod3;
    public Transform connectingRod4;
    public Transform connectingRod5;
    public Transform connectingRod6;

    [Header("Crank-Slider Parameters")]
    [Tooltip("Connecting rod length in metres. Must be longer than the crank throw radius.")]
    public float connectingRodLength = 0.18f;

    [Tooltip("Maximum rod rotation change per frame in degrees. Prevents single-frame flips near dead centre.")]
    public float maxRodDegreesDelta = 45f;

    [Header("Speed")]
    [Tooltip("Crankshaft rotation speed in RPM.")]
    public float rpm = 60f;

    [Header("Gear Ratio")]
    [Tooltip("Driven gear speed relative to crankshaft gear. 1 = same speed, opposite direction.")]
    public float gearRatio = 1f;

    // ── Runtime ───────────────────────────────────────────────────────────────

    bool isUnlocked;

    /// <summary>
    /// Set to true by MoteurAssemblyManager once all engine parts are fully snapped.
    /// Triggers CacheAssembledRestTransforms() so piston and rod rest positions are read
    /// from the assembled state, not from their table positions at scene load.
    /// </summary>
    public bool IsUnlocked
    {
        get => isUnlocked;
        set
        {
            isUnlocked = value;
            if (value) CacheAssembledRestTransforms();
        }
    }

    /// <summary>
    /// Starts the engine animation programmatically.
    /// Called by EngineNarrationController after the engine base step completes.
    /// The M key toggle continues to work independently alongside this.
    /// </summary>
    public void StartAnimation() => isRunning = true;

    bool isRunning;
    float crankAngle;

    // Cached rest world X of each piston — pistons slide along world X only.
    float pistonRestX1, pistonRestX2, pistonRestX3;
    float pistonRestX4, pistonRestX5, pistonRestX6;

    // Cached rest world rotation of each connecting rod.
    Quaternion rodRotRest1, rodRotRest2, rodRotRest3;
    Quaternion rodRotRest4, rodRotRest5, rodRotRest6;

    // Previous frame world rotation of each connecting rod — used by RotateTowards clamp.
    Quaternion rodPrevRot1, rodPrevRot2, rodPrevRot3;
    Quaternion rodPrevRot4, rodPrevRot5, rodPrevRot6;

    // Cached rest pin→piston direction (world space, normalized) for each connecting rod.
    Vector3 rodDirRest1, rodDirRest2, rodDirRest3;
    Vector3 rodDirRest4, rodDirRest5, rodDirRest6;

    // Cached offset of each rod pivot from its pin↔piston midpoint at assembly time.
    // Kept constant every frame so the rod doesn't drift from its snapped position.
    Vector3 rodMidOffset1, rodMidOffset2, rodMidOffset3;
    Vector3 rodMidOffset4, rodMidOffset5, rodMidOffset6;

    // Cached rest euler angles for rotating-assembly parts.
    Vector3 crankshaftRestEuler;
    Vector3 crankshaftGearRestEuler;
    Vector3 drivenGearRestEuler;
    Vector3 crankshaftAxleRestEuler;
    Vector3 axleSpacer1RestEuler;
    Vector3 axleSpacer2RestEuler;
    Vector3 drivenAxleRestEuler;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        // Cache rotating-assembly euler angles here — these parts never translate,
        // so their rest euler is stable from scene load onward.
        // Pistons and rods are cached in CacheAssembledRestTransforms(), called
        // the moment IsUnlocked = true (parts are in their snapped/assembled positions).
        if (crankshaft) crankshaftRestEuler = crankshaft.localEulerAngles;
        if (crankshaftGear) crankshaftGearRestEuler = crankshaftGear.localEulerAngles;
        if (drivenGear) drivenGearRestEuler = drivenGear.localEulerAngles;
        if (crankshaftAxle) crankshaftAxleRestEuler = crankshaftAxle.localEulerAngles;
        if (axleSpacer1) axleSpacer1RestEuler = axleSpacer1.localEulerAngles;
        if (axleSpacer2) axleSpacer2RestEuler = axleSpacer2.localEulerAngles;
        if (drivenAxle) drivenAxleRestEuler = drivenAxle.localEulerAngles;
    }

    /// <summary>
    /// Caches the rest state of each connecting rod from its snapped assembled position.
    /// The rod is never moved here — the ghost snap system has already placed it correctly.
    /// We record the rod's offset from the pin↔piston midpoint so ApplyRod can
    /// preserve that exact offset every frame, keeping both rods in a pair aligned.
    /// </summary>
    void CacheAssembledRestTransforms()
    {
        if (piston1) pistonRestX1 = piston1.position.x;
        if (piston2) pistonRestX2 = piston2.position.x;
        if (piston3) pistonRestX3 = piston3.position.x;
        if (piston4) pistonRestX4 = piston4.position.x;
        if (piston5) pistonRestX5 = piston5.position.x;
        if (piston6) pistonRestX6 = piston6.position.x;

        CacheRod(connectingRod1, crankPin12, piston1,
                 ref rodRotRest1, ref rodDirRest1, ref rodMidOffset1);
        CacheRod(connectingRod2, crankPin12, piston2,
                 ref rodRotRest2, ref rodDirRest2, ref rodMidOffset2);
        CacheRod(connectingRod3, crankPin34, piston3,
                 ref rodRotRest3, ref rodDirRest3, ref rodMidOffset3);
        CacheRod(connectingRod4, crankPin34, piston4,
                 ref rodRotRest4, ref rodDirRest4, ref rodMidOffset4);
        CacheRod(connectingRod5, crankPin56, piston5,
                 ref rodRotRest5, ref rodDirRest5, ref rodMidOffset5);
        CacheRod(connectingRod6, crankPin56, piston6,
                 ref rodRotRest6, ref rodDirRest6, ref rodMidOffset6);

        // Initialise previous-rotation fields to the rest rotation so the first
        // frame clamp has a sensible starting point.
        rodPrevRot1 = rodRotRest1; rodPrevRot2 = rodRotRest2;
        rodPrevRot3 = rodRotRest3; rodPrevRot4 = rodRotRest4;
        rodPrevRot5 = rodRotRest5; rodPrevRot6 = rodRotRest6;
    }

    /// <summary>
    /// Reads the rod's world rotation and pin-to-piston direction from its current
    /// (assembled) position, and records its pivot offset from the pin↔piston midpoint.
    /// Does not move the rod.
    /// </summary>
    void CacheRod(Transform rod, Transform pin, Transform piston,
                  ref Quaternion rotRest, ref Vector3 dirRest, ref Vector3 midOffset)
    {
        if (rod == null || pin == null || piston == null) return;

        Vector3 mid = (pin.position + piston.position) * 0.5f;
        midOffset   = rod.position - mid;
        rotRest     = rod.rotation;
        dirRest     = (piston.position - pin.position).normalized;
    }

    void Update()
    {
        if (!IsUnlocked) return;

        // M key toggles the animation on/off.
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            isRunning = !isRunning;

        if (!isRunning) return;

        // Advance crankshaft angle at constant RPM.
        float degreesPerSecond = rpm * 360f / 60f;
        crankAngle = (crankAngle + degreesPerSecond * Time.deltaTime) % 360f;

        ApplyCrankshaftAssembly(crankAngle);
        ApplyDrivenGearAssembly(crankAngle);

        // Each pair shares one pin — read its world position once per pair.
        // ApplyPistonPair is always called before ApplyRod so piston.position
        // is frame-current when the rod reads it.
        if (crankPin12 != null)
        {
            Vector3 pin12World = crankPin12.position;
            ApplyPistonPair(piston1, piston2, pistonRestX1, pistonRestX2, pin12World);
            ApplyRod(connectingRod1, rodRotRest1, rodDirRest1, rodMidOffset1, pin12World, piston1, ref rodPrevRot1);
            ApplyRod(connectingRod2, rodRotRest2, rodDirRest2, rodMidOffset2, pin12World, piston2, ref rodPrevRot2);
        }

        if (crankPin34 != null)
        {
            Vector3 pin34World = crankPin34.position;
            ApplyPistonPair(piston3, piston4, pistonRestX3, pistonRestX4, pin34World);
            ApplyRod(connectingRod3, rodRotRest3, rodDirRest3, rodMidOffset3, pin34World, piston3, ref rodPrevRot3);
            ApplyRod(connectingRod4, rodRotRest4, rodDirRest4, rodMidOffset4, pin34World, piston4, ref rodPrevRot4);
        }

        if (crankPin56 != null)
        {
            Vector3 pin56World = crankPin56.position;
            ApplyPistonPair(piston5, piston6, pistonRestX5, pistonRestX6, pin56World);
            ApplyRod(connectingRod5, rodRotRest5, rodDirRest5, rodMidOffset5, pin56World, piston5, ref rodPrevRot5);
            ApplyRod(connectingRod6, rodRotRest6, rodDirRest6, rodMidOffset6, pin56World, piston6, ref rodPrevRot6);
        }
    }

    // ── Crankshaft assembly ────────────────────────────────────────────────────

    /// <summary>
    /// Spins the crankshaft group on Y (its geometric spin axis) and its gear on Z.
    /// Axle and spacers also spin on Y.
    /// </summary>
    void ApplyCrankshaftAssembly(float angle)
    {
        SetLocalEulerY(crankshaft, crankshaftRestEuler, angle);
        SetLocalEulerZ(crankshaftGear, crankshaftGearRestEuler, angle);
        SetLocalEulerY(crankshaftAxle, crankshaftAxleRestEuler, angle);
        SetLocalEulerY(axleSpacer1, axleSpacer1RestEuler, angle);
        SetLocalEulerY(axleSpacer2, axleSpacer2RestEuler, angle);
    }

    /// <summary>
    /// Spins the driven gear on Z and the driven axle on Y, counter-rotating
    /// relative to the crankshaft assembly.
    /// </summary>
    void ApplyDrivenGearAssembly(float angle)
    {
        float drivenAngle = -angle * gearRatio;
        SetLocalEulerZ(drivenGear, drivenGearRestEuler, drivenAngle);
        SetLocalEulerY(drivenAxle, drivenAxleRestEuler, drivenAngle);
    }

    // ── Crank-slider kinematics ────────────────────────────────────────────────

    /// <summary>
    /// Translates a piston pair along world X using the crank-slider formula driven
    /// by the real world position of their shared crank pin.
    ///
    /// Both pistons in the pair receive the same X displacement — no mirroring.
    /// Their individual rest X values keep them on their respective banks
    /// (left ≈ 0.349, right ≈ 0.658).
    /// </summary>
    /// <param name="pistonLeft">Left-bank piston (odd index).</param>
    /// <param name="pistonRight">Right-bank piston (even index).</param>
    /// <param name="restXLeft">Cached rest world X of the left-bank piston.</param>
    /// <param name="restXRight">Cached rest world X of the right-bank piston.</param>
    /// <param name="pinWorld">Current world position of the crank pin.</param>
    void ApplyPistonPair(
        Transform pistonLeft,
        Transform pistonRight,
        float restXLeft,
        float restXRight,
        Vector3 pinWorld)
    {
        if (crankshaft == null) return;

        float l  = connectingRodLength;
        float dX = pinWorld.x - crankshaft.position.x;
        float dY = pinWorld.y - crankshaft.position.y;
        float discriminant = l * l - dY * dY;

        if (discriminant < 0f)
        {
            Debug.LogWarning(
                "[MoteurAnimationController] connectingRodLength is shorter than the " +
                "current crank throw — increase connectingRodLength to fix piston kinematics.",
                this);
        }

        float inner   = Mathf.Sqrt(Mathf.Max(0f, discriminant));

        // Reach = distance from crank centre to piston pin along world X.
        // xOffset = reach − TDC reach (l), always ≤ 0.
        float xOffset = (dX + inner) - l;

        if (pistonLeft != null)
        {
            Vector3 p = pistonLeft.position;
            p.x = restXLeft + xOffset;
            pistonLeft.position = p;
        }

        if (pistonRight != null)
        {
            Vector3 p = pistonRight.position;
            p.x = restXRight + xOffset;
            pistonRight.position = p;
        }
    }

    /// <summary>
    /// Positions and rotates a connecting rod each frame.
    ///
    /// Position: pin↔piston midpoint + the fixed offset cached at assembly time.
    /// All three axes (X, Y, Z) use the midpoint so the rod never drifts in Z
    /// as the crankshaft rotates (fixes the previous pinWorld.z hard-lock bug).
    ///
    /// Rotation: Quaternion.LookRotation with a stable cross-product up axis avoids
    /// the degenerate case that Quaternion.FromToRotation produces when the current
    /// direction is exactly antiparallel to the rest direction (TDC / BDC).
    /// Quaternion.RotateTowards clamps the per-frame delta as a final safety rail.
    /// </summary>
    /// <param name="rod">Connecting rod transform.</param>
    /// <param name="rodRotRest">Cached rest world rotation of the rod.</param>
    /// <param name="rodDirRest">Cached rest pin→piston direction, world space, normalised.</param>
    /// <param name="midOffset">Cached pivot offset from pin↔piston midpoint at assembly time.</param>
    /// <param name="pinWorld">Current world position of the crank pin (big end).</param>
    /// <param name="piston">Piston transform — its world position is the small end.</param>
    /// <param name="prevRot">Previous frame rotation, updated in place for the clamp.</param>
    void ApplyRod(Transform rod, Quaternion rodRotRest, Vector3 rodDirRest,
                  Vector3 midOffset, Vector3 pinWorld, Transform piston,
                  ref Quaternion prevRot)
    {
        if (rod == null || piston == null) return;

        Vector3 pistonWorld = piston.position;
        Vector3 mid         = (pinWorld + pistonWorld) * 0.5f;

        // Bug fix #2: use mid.z instead of pinWorld.z — the pin's world Z orbits
        // continuously as the crankshaft rotates, which was dragging the rod in Z
        // and causing it to clip into the engine block geometry.
        rod.position = new Vector3(
            mid.x + midOffset.x,
            mid.y + midOffset.y,
            mid.z + midOffset.z);

        Vector3 currentDir = (pistonWorld - pinWorld).normalized;

        // Guard: pin and piston are coincident — skip rotation this frame.
        if (currentDir.sqrMagnitude < 0.001f) return;

        // Fixed world-up side axis — stable across the full 360° crank rotation.
        // The cross-product approach collapses at TDC/BDC where currentDir is
        // (anti)parallel to rodDirRest, causing LookRotation to flip 180° in one frame.
        // Vector3.up is always well-defined for this V6 layout (rods swing in world XY).
        Vector3 sideAxis = Vector3.up;

        // Map the rod from its rest orientation to the current crank direction
        // using stable LookRotation calls — no gimbal lock, no antiparallel undefined case.
        Quaternion restLook    = Quaternion.LookRotation(rodDirRest, sideAxis);
        Quaternion currentLook = Quaternion.LookRotation(currentDir, sideAxis);
        Quaternion targetRot   = currentLook * Quaternion.Inverse(restLook) * rodRotRest;

        // Clamp per-frame rotation delta — catches any remaining edge cases near
        // dead centre without letting the rod snap to a wrong orientation.
        rod.rotation = Quaternion.RotateTowards(prevRot, targetRot, maxRodDegreesDelta);
        prevRot      = rod.rotation;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets localEulerAngles, preserving rest X and Y, and adding zOffset to rest Z.
    /// </summary>
    static void SetLocalEulerZ(Transform t, Vector3 restEuler, float zOffset)
    {
        if (t == null) return;
        t.localEulerAngles = new Vector3(restEuler.x, restEuler.y, restEuler.z + zOffset);
    }

    /// <summary>
    /// Sets localEulerAngles, preserving rest X and Z, and adding yOffset to rest Y.
    /// Used for axles and spacers whose spin axis is Y in local space.
    /// </summary>
    static void SetLocalEulerY(Transform t, Vector3 restEuler, float yOffset)
    {
        if (t == null) return;
        t.localEulerAngles = new Vector3(restEuler.x, restEuler.y + yOffset, restEuler.z);
    }
}