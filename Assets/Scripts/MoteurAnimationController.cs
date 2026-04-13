using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Procedural 6-piston crank-slider engine animation.
///
/// Activated by pressing M, but only after MoteurAssemblyManager has set
/// IsUnlocked = true (i.e., all engine parts have been fully snapped).
///
/// All motion is derived from a single crankshaft angle accumulator.
/// No Animator or keyframes are used — transforms are driven directly
/// in Update using the standard crank-slider kinematic formula:
///
///   x(θ) = r·cos(θ) + sqrt(l² − (r·sin(θ))²)
///
/// Coordinate convention (all parts are flat children of moteur2):
///   X axis  → piston translation direction
///   Y axis  → vertical row offset between piston pairs
///   Z axis  → crankshaft rotation axis
///
/// Piston phase groups:
///   Group A (0°)   — piston1, piston2, piston5, piston6
///   Group B (180°) — piston3, piston4
///
/// Right-bank pistons (2, 4, 6) use mirroredSide = true so their
/// X displacement is negated relative to the left-bank pistons (1, 3, 5).
/// </summary>
public class MoteurAnimationController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Crankshaft Assembly — rotate rigidly around Z")]
    public Transform crankshaft;
    public Transform crankshaftAxle;
    public Transform crankshaftGear;
    public Transform axleSpacer1;
    public Transform axleSpacer2;

    [Header("Driven Gear Assembly — counter-rotate around Z")]
    public Transform drivenGear;
    public Transform drivenAxle;

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
    [Tooltip("Crank pin radius from crankshaft centre in metres. Controls piston stroke length.")]
    public float crankRadius = 0.08f;

    [Tooltip("Connecting rod length in metres. Must satisfy (crankRadius / connectingRodLength) < 1.")]
    public float connectingRodLength = 0.18f;

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
    /// Triggers CacheAssembledRestTransforms() so piston and rod positions are read
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

    bool  isRunning;
    float crankAngle;

    // Cached rest local positions — all animation is applied as delta from these
    Vector3 pistonRest1, pistonRest2, pistonRest3;
    Vector3 pistonRest4, pistonRest5, pistonRest6;

    Vector3 rodRest1, rodRest2, rodRest3;
    Vector3 rodRest4, rodRest5, rodRest6;

    // Cached rest euler angles — rotation is added on top of these each frame
    Vector3 crankshaftRestEuler;
    Vector3 crankshaftGearRestEuler;
    Vector3 drivenGearRestEuler;

    // Axle and spacers rotate on Y axis — cached separately
    Vector3 crankshaftAxleRestEuler;
    Vector3 axleSpacer1RestEuler;
    Vector3 axleSpacer2RestEuler;
    Vector3 drivenAxleRestEuler;

    // Cached rest euler for connecting rods — rod euler is written every frame,
    // so it must be read once in Start() rather than re-read in Update()
    Vector3 rodEulerRest1, rodEulerRest2, rodEulerRest3;
    Vector3 rodEulerRest4, rodEulerRest5, rodEulerRest6;

    // Phase offset constants (degrees)
    const float PhaseA = 0f;    // pistons 1, 2, 5, 6
    const float PhaseB = 180f;  // pistons 3, 4

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Start()
    {
        // Only cache rotating assembly euler angles here — these parts never translate,
        // so their rest euler is stable from scene load onward.
        // Pistons and rods are cached in CacheAssembledRestTransforms(), which is called
        // the moment IsUnlocked = true (parts are in their snapped/assembled positions).
        if (crankshaft)     crankshaftRestEuler     = crankshaft.localEulerAngles;
        if (crankshaftGear) crankshaftGearRestEuler = crankshaftGear.localEulerAngles;
        if (drivenGear)     drivenGearRestEuler     = drivenGear.localEulerAngles;
        if (crankshaftAxle) crankshaftAxleRestEuler = crankshaftAxle.localEulerAngles;
        if (axleSpacer1)    axleSpacer1RestEuler    = axleSpacer1.localEulerAngles;
        if (axleSpacer2)    axleSpacer2RestEuler    = axleSpacer2.localEulerAngles;
        if (drivenAxle)     drivenAxleRestEuler     = drivenAxle.localEulerAngles;
    }

    /// <summary>
    /// Reads the current local positions and euler angles of all pistons and rods.
    /// Called by the IsUnlocked setter the moment assembly is complete, so cached
    /// values reflect assembled positions — not the table positions at scene load.
    /// </summary>
    void CacheAssembledRestTransforms()
    {
        if (piston1) pistonRest1 = piston1.localPosition;
        if (piston2) pistonRest2 = piston2.localPosition;
        if (piston3) pistonRest3 = piston3.localPosition;
        if (piston4) pistonRest4 = piston4.localPosition;
        if (piston5) pistonRest5 = piston5.localPosition;
        if (piston6) pistonRest6 = piston6.localPosition;

        if (connectingRod1) { rodRest1 = connectingRod1.localPosition; rodEulerRest1 = connectingRod1.localEulerAngles; }
        if (connectingRod2) { rodRest2 = connectingRod2.localPosition; rodEulerRest2 = connectingRod2.localEulerAngles; }
        if (connectingRod3) { rodRest3 = connectingRod3.localPosition; rodEulerRest3 = connectingRod3.localEulerAngles; }
        if (connectingRod4) { rodRest4 = connectingRod4.localPosition; rodEulerRest4 = connectingRod4.localEulerAngles; }
        if (connectingRod5) { rodRest5 = connectingRod5.localPosition; rodEulerRest5 = connectingRod5.localEulerAngles; }
        if (connectingRod6) { rodRest6 = connectingRod6.localPosition; rodEulerRest6 = connectingRod6.localEulerAngles; }
    }

    void Update()
    {
        if (!IsUnlocked) return;

        // M key toggles the animation on/off
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            isRunning = !isRunning;

        if (!isRunning) return;

        // Advance crankshaft angle at constant RPM
        float degreesPerSecond = rpm * 360f / 60f;
        crankAngle = (crankAngle + degreesPerSecond * Time.deltaTime) % 360f;

        ApplyCrankshaftAssembly(crankAngle);
        ApplyDrivenGearAssembly(crankAngle);

        // Group A — phase 0°
        ApplyPistonAndRod(piston1, connectingRod1, pistonRest1, rodRest1, rodEulerRest1, crankAngle + PhaseA, false);
        ApplyPistonAndRod(piston2, connectingRod2, pistonRest2, rodRest2, rodEulerRest2, crankAngle + PhaseA, true);

        // Group B — phase 180°
        ApplyPistonAndRod(piston3, connectingRod3, pistonRest3, rodRest3, rodEulerRest3, crankAngle + PhaseB, false);
        ApplyPistonAndRod(piston4, connectingRod4, pistonRest4, rodRest4, rodEulerRest4, crankAngle + PhaseB, true);

        // Group A — phase 0° (same phase as pistons 1/2)
        ApplyPistonAndRod(piston5, connectingRod5, pistonRest5, rodRest5, rodEulerRest5, crankAngle + PhaseA, false);
        ApplyPistonAndRod(piston6, connectingRod6, pistonRest6, rodRest6, rodEulerRest6, crankAngle + PhaseA, true);
    }

    // ── Crankshaft assembly ────────────────────────────────────────────────────

    /// <summary>
    /// Spins the crankshaft and crankshaft gear on Z (their rotation axis).
    /// Axle and spacers share the same angular speed but rotate on Y.
    /// </summary>
    void ApplyCrankshaftAssembly(float angle)
    {
        SetLocalEulerZ(crankshaft,     crankshaftRestEuler,     angle);
        SetLocalEulerZ(crankshaftGear, crankshaftGearRestEuler, angle);

        SetLocalEulerY(crankshaftAxle, crankshaftAxleRestEuler, angle);
        SetLocalEulerY(axleSpacer1,    axleSpacer1RestEuler,    angle);
        SetLocalEulerY(axleSpacer2,    axleSpacer2RestEuler,    angle);
    }

    /// <summary>
    /// Spins the driven gear on Z and the driven axle on Y, both counter-rotating
    /// relative to the crankshaft assembly.
    /// </summary>
    void ApplyDrivenGearAssembly(float angle)
    {
        float drivenAngle = -angle * gearRatio;
        SetLocalEulerZ(drivenGear, drivenGearRestEuler,  drivenAngle);
        SetLocalEulerY(drivenAxle, drivenAxleRestEuler,  drivenAngle);
    }

    // ── Crank-slider kinematics ────────────────────────────────────────────────

    /// <summary>
    /// Applies the crank-slider formula to one piston and the oscillation angle
    /// to its connecting rod, using cached rest transforms as baselines.
    /// </summary>
    /// <param name="piston">Piston transform — translates along X only.</param>
    /// <param name="rod">Connecting rod transform — translates midpoint along X and Y, rotates on Z.</param>
    /// <param name="pistonRest">Cached rest local position of the piston.</param>
    /// <param name="rodRest">Cached rest local position of the rod.</param>
    /// <param name="rodEulerRest">Cached rest local euler angles of the rod.</param>
    /// <param name="theta">Crankshaft angle for this piston group, in degrees.</param>
    /// <param name="mirroredSide">True for right-bank pistons (2, 4, 6) — flips the X direction.</param>
    void ApplyPistonAndRod(
        Transform piston,
        Transform rod,
        Vector3 pistonRest,
        Vector3 rodRest,
        Vector3 rodEulerRest,
        float theta,
        bool mirroredSide)
    {
        float thetaRad = theta * Mathf.Deg2Rad;
        float r        = crankRadius;
        float l        = connectingRodLength;
        float sinT     = Mathf.Sin(thetaRad);
        float cosT     = Mathf.Cos(thetaRad);

        // Crank-slider: distance of piston pin from crank centre along the cylinder axis
        float inner   = Mathf.Sqrt(Mathf.Max(0f, l * l - r * r * sinT * sinT));
        float x       = r * cosT + inner;

        // Displacement from TDC (top dead centre, where x is maximum: r + l)
        float xOffset = x - (r + l);   // always ≤ 0

        float side = mirroredSide ? -1f : 1f;

        // ── Piston: pure X translation, no rotation allowed ─────────────────
        if (piston != null)
        {
            Vector3 pos = pistonRest;
            pos.x      += xOffset * side;
            piston.localPosition = pos;
        }

        // ── Connecting rod: midpoint translation (X + Y) + Z oscillation ────
        if (rod != null)
        {
            // Crank pin orbits in both X and Y around the crank centre
            float crankPinX = r * cosT;
            float crankPinY = r * sinT;

            // Rod midpoint = average of the two pin world positions.
            // At θ=0 (rest): midpoint is at (r + l/2, 0) from crank centre for the left bank.
            // At angle θ:    midpoint is at ((pistonPinX + crankPinX)/2, crankPinY/2).
            // Delta from rest:
            //   ΔX = r·(cosT - 1) + (inner - l) / 2
            //   ΔY = crankPinY / 2 = r·sinT / 2  — same for both banks (shared crank pin)
            float deltaX = r * (cosT - 1f) + (inner - l) * 0.5f;
            float deltaY = crankPinY * 0.5f;

            Vector3 rodPos = rodRest;
            rodPos.x      += deltaX * side;
            rodPos.y      += deltaY;           // no side flip: Y is identical for both banks
            rod.localPosition = rodPos;

            // Exact rod angle from crank pin → piston pin vector:
            //   rod vector = (pistonPinX - crankPinX, 0 - crankPinY) = (inner, -crankPinY)
            //   β = atan2(-crankPinY, inner)   — exact, no small-angle approximation
            float rodAngleDeg = Mathf.Atan2(-crankPinY, inner) * Mathf.Rad2Deg;

            // Apply oscillation on top of the rod's rest Z euler
            rod.localEulerAngles = new Vector3(
                rodEulerRest.x,
                rodEulerRest.y,
                rodEulerRest.z + rodAngleDeg * side);
        }
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
