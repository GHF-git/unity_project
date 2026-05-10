using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives procedural animation for the differential educational demo.
/// Attach to /dif_demo_wheels. Requires rightWheel and leftWheel.
///
/// PlayLockedTurn()        — Step 1 (problem):  both wheels spin at baseSpeed.
///                           Left wheel = SCRUB bar. Right wheel = OK bar. Diagram fades in. HUD shown.
/// PlayStraight()          — Both wheels at baseSpeed, no HUD bars.
/// PlayDifferentialTurn()  — Step 2 (solution): outer wheel faster, inner slower, speed bars update.
/// IntroduceDifferential() — Shows differential model only. Diagram stays visible.
/// HighlightComponent()    — Pulses named parts; dims others.
/// HighlightDifferential() — Pulses all parts or resets emission.
/// StopDemo()              — Stops coroutines, hides HUD, fades diagram out.
/// </summary>
public class DifferentialDemoController : MonoBehaviour
{
    // ── Inspector — Wheels ────────────────────────────────────────────────────

    [Header("Demo Wheels")]
    [Tooltip("Transform of /dif_demo_wheels/right.")]
    public Transform rightWheel;

    [Tooltip("Transform of /dif_demo_wheels/left.")]
    public Transform leftWheel;

    // ── Inspector — Differential Model ───────────────────────────────────────

    [Header("Differential Model")]
    [Tooltip("Root GameObject of /differanciel. Hidden until IntroduceDifferential is called.")]
    public GameObject differentialModelRoot;

    // ── Inspector — Speeds ────────────────────────────────────────────────────

    [Header("Speeds (degrees / second)")]
    [Tooltip("Baseline wheel spin speed for all phases.")]
    public float baseSpeed = 120f;

    [Tooltip("Outer wheel speed = baseSpeed * outerMultiplier during differential turn.")]
    public float outerMultiplier = 1.45f;

    [Tooltip("Inner wheel speed = baseSpeed * innerMultiplier during differential turn.")]
    public float innerMultiplier = 0.55f;

    // ── Inspector — Spin Axis ─────────────────────────────────────────────────

    [Header("Spin")]
    [Tooltip("World-space axis for RotateAround. Default Vector3.right = world X axle. " +
             "Matches the red gizmo arrow between the two wheels in Scene view.")]
    public Vector3 spinAxis = Vector3.right;

    // ── Inspector — Differential Parts ───────────────────────────────────────

    [Header("Differential Parts")]
    [Tooltip("Transform of /differanciel/Ring_Gear.")]
    public Transform ringGear;

    [Tooltip("Transform of /differanciel/Differential_Carrier.")]
    public Transform differentialCarrier;

    [Tooltip("Transform of /differanciel/Spider_Gear_Left.")]
    public Transform spiderGearLeft;

    [Tooltip("Transform of /differanciel/Spider_Gear_Right.")]
    public Transform spiderGearRight;

    // ── Inspector — Highlight ─────────────────────────────────────────────────

    [Header("Highlight")]
    [Tooltip("HDR emission color used for component highlight pulses.")]
    public Color highlightColor = new Color(1f, 0.6f, 0f);

    [Tooltip("Highlight pulse frequency in Hz.")]
    public float highlightPulseSpeed = 2f;

    // ── Inspector — UI Speed Bars ─────────────────────────────────────────────

    [Header("Speed Bars (optional)")]
    [Tooltip("Filled Image strip for the left wheel speed indicator.")]
    public Image leftSpeedBar;

    [Tooltip("Filled Image strip for the right wheel speed indicator.")]
    public Image rightSpeedBar;

    [Tooltip("Color of speed bars during locked turn (scrub phase).")]
    public Color scrubColor = new Color(0.9f, 0.2f, 0.2f);

    [Tooltip("Color of the slower wheel bar during differential phase.")]
    public Color normalColor = new Color(0.2f, 0.85f, 0.3f);

    [Tooltip("Color of the faster wheel bar during differential phase.")]
    public Color fastColor = new Color(0.2f, 0.5f, 1f);

    // ── Inspector — UI Labels ─────────────────────────────────────────────────

    [Header("Labels (optional)")]
    [Tooltip("TextMeshProUGUI label overlaid on the left wheel.")]
    public TextMeshProUGUI leftLabel;

    [Tooltip("TextMeshProUGUI label overlaid on the right wheel.")]
    public TextMeshProUGUI rightLabel;

    // ── Inspector — HUD Canvas ────────────────────────────────────────────────

    [Header("HUD Canvas (optional)")]
    [Tooltip("RectTransform of DemoWheelHUD. Positioned automatically in Awake to sit above the wheels.")]
    public RectTransform demoWheelHUD;

    [Tooltip("Offset above the wheel center in dif_demo_wheels local units. " +
             "Increase to push the HUD higher above the tyres.")]
    public float hudHeightOffset = 0.6f;

    // ── Inspector — Diagram Image ─────────────────────────────────────────────

    [Header("Diagram Image (optional)")]
    [Tooltip("CanvasGroup of the DiffDiagramPanel. Fades in on PlayLockedTurn, fades out on StopDemo.")]
    public CanvasGroup diagramImage;

    [Tooltip("Duration of the diagram fade in/out in seconds.")]
    public float diagramFadeDuration = 0.5f;

    // ── Private cache ─────────────────────────────────────────────────────────

    MeshRenderer[] _diffRenderers;
    Coroutine      _highlightCoroutine;
    Coroutine      _diagramFadeCoroutine;

    // ── Constants ─────────────────────────────────────────────────────────────

    private const float SpeedLerpDuration = 0.4f;
    private const string LabelScrub = "SCRUB !";
    private const string LabelOk    = "OK";
    private const string LabelFast  = "FAST";
    private const string LabelSlow  = "SLOW";

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        // Cache differential renderers and enable emission keyword on runtime instances.
        // Using .material (not .sharedMaterial) auto-creates per-instance copies so
        // FBX-embedded materials are never modified permanently.
        var diffParts = new Transform[] { ringGear, differentialCarrier, spiderGearLeft, spiderGearRight };
        var list = new List<MeshRenderer>();
        foreach (var part in diffParts)
        {
            if (part == null) continue;
            if (part.TryGetComponent<MeshRenderer>(out var mr))
            {
                mr.material.EnableKeyword("_EMISSION");
                list.Add(mr);
            }
        }
        _diffRenderers = list.ToArray();

        // Differential model hidden until IntroduceDifferential fires.
        if (differentialModelRoot != null)
            differentialModelRoot.SetActive(false);

        // HUD hidden until PlayLockedTurn fires.
        if (demoWheelHUD != null)
            demoWheelHUD.gameObject.SetActive(false);

        // Auto-position the HUD canvas so it sits just above the wheel centres.
        // anchoredPosition on a World Space canvas child of a plain Transform maps
        // directly to local XY in scene units — no canvas-unit conversion needed.
        if (demoWheelHUD != null && rightWheel != null)
        {
            float wheelCenterY = rightWheel.localPosition.y;
            float wheelCenterZ = rightWheel.localPosition.z;
            demoWheelHUD.anchoredPosition = new Vector2(0f, wheelCenterY + hudHeightOffset);
            demoWheelHUD.localPosition    = new Vector3(
                demoWheelHUD.localPosition.x,
                demoWheelHUD.localPosition.y,
                wheelCenterZ);

            // Stand the canvas upright so it faces forward instead of lying flat.
            demoWheelHUD.localEulerAngles = new Vector3(90f, 0f, 0f);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 1 — Problem phase. Both wheels spin at equal speed.
    /// Left bar = red/SCRUB. Right bar = green/OK. Diagram fades in. HUD becomes visible.
    /// </summary>
    public void PlayLockedTurn()
    {
        if (!WheelRefsValid()) return;
        StopAllCoroutines();
        _highlightCoroutine   = null;
        _diagramFadeCoroutine = null;

        // Show HUD only now.
        demoWheelHUD?.gameObject.SetActive(true);

        // Diagram fades in when the problem step starts.
        _diagramFadeCoroutine = StartCoroutine(FadeDiagram(1f));

        StartCoroutine(RunSpin(baseSpeed, baseSpeed, isLocked: true, rightNormal: true));
    }

    /// <summary>
    /// Straight-line intro. Both wheels at baseSpeed. No HUD bars.
    /// Differential model visibility is unchanged — controlled by IntroduceDifferential.
    /// </summary>
    public void PlayStraight()
    {
        if (!WheelRefsValid()) return;
        StopAllCoroutines();
        _highlightCoroutine   = null;
        _diagramFadeCoroutine = null;

        StartCoroutine(RunSpin(baseSpeed, baseSpeed, isLocked: false));
    }

    /// <summary>
    /// Step 2 — Solution phase. Outer wheel faster, inner slower. Speed bars show split.
    /// Differential model visibility is unchanged — controlled by IntroduceDifferential.
    /// </summary>
    public void PlayDifferentialTurn()
    {
        if (!WheelRefsValid()) return;
        StopAllCoroutines();
        _highlightCoroutine   = null;
        _diagramFadeCoroutine = null;

        HighlightDifferential(false);

        float outerSpeed = baseSpeed * outerMultiplier;
        float innerSpeed = baseSpeed * innerMultiplier;
        StartCoroutine(RunSpin(outerSpeed, innerSpeed, isLocked: false));
    }

    /// <summary>
    /// Shows the differential model. Diagram stays visible — StopDemo owns the fade-out.
    /// Called by the "THE DIFFERENTIAL" narration step.
    /// </summary>
    public void IntroduceDifferential()
    {
        if (differentialModelRoot != null)
            differentialModelRoot.SetActive(true);
    }

    /// <summary>
    /// Fades the diagram image in. Called at narration sequence start so the
    /// diagram is visible from step 0 (INTRODUCTION).
    /// </summary>
    public void ShowDiagram()
    {
        if (_diagramFadeCoroutine != null) StopCoroutine(_diagramFadeCoroutine);
        _diagramFadeCoroutine = StartCoroutine(FadeDiagram(1f));
    }

    /// <summary>
    /// Pulses the specified differential parts at full HDR intensity;
    /// dims all other differential parts to a subtle ambient glow.
    /// </summary>
    public void HighlightComponent(params Transform[] targets)
    {
        StopHighlightCoroutine();
        _highlightCoroutine = StartCoroutine(RunHighlightPulse(targets));
    }

    /// <summary>
    /// Pulses all differential parts at once (on=true) or resets all emission to black (on=false).
    /// </summary>
    public void HighlightDifferential(bool on)
    {
        StopHighlightCoroutine();
        if (on)
            _highlightCoroutine = StartCoroutine(RunHighlightPulse(null));
        else
            foreach (var mr in _diffRenderers)
                mr.material.SetColor("_EmissionColor", Color.black);
    }

    /// <summary>
    /// Stops all animation, hides HUD, clears bars and labels, fades diagram out.
    /// Called by DifferentialNarrationController at the end of the sequence.
    /// </summary>
    public void StopDemo()
    {
        StopAllCoroutines();
        _highlightCoroutine   = null;
        _diagramFadeCoroutine = null;

        // Hide HUD when sequence ends.
        demoWheelHUD?.gameObject.SetActive(false);

        ClearBarsAndLabels();

        // Diagram fades out only here — sole owner of the fade-out.
        _diagramFadeCoroutine = StartCoroutine(FadeDiagram(0f));
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    /// <summary>
    /// Unified spin coroutine. Lerps 0 → target speeds over SpeedLerpDuration, then holds.
    /// Uses RotateAround with the world-space spinAxis — correct for meshes whose
    /// localRotation is not identity (both wheels ship with ~270° local X rotation).
    /// rightNormal: when true (locked turn) the right bar shows green/OK instead of red/SCRUB.
    /// </summary>
    IEnumerator RunSpin(float rightTargetSpeed, float leftTargetSpeed,
                        bool isLocked, bool rightNormal = false)
    {
        float rightCurrentSpeed = 0f;
        float leftCurrentSpeed  = 0f;
        float elapsed           = 0f;

        // Speed lerp phase.
        while (elapsed < SpeedLerpDuration)
        {
            float t = elapsed / SpeedLerpDuration;
            rightCurrentSpeed = Mathf.Lerp(0f, rightTargetSpeed, t);
            leftCurrentSpeed  = Mathf.Lerp(0f, leftTargetSpeed,  t);

            float dt = Time.deltaTime;
            SpinWheels(rightCurrentSpeed, leftCurrentSpeed, dt);
            UpdateBarsAndLabels(rightCurrentSpeed, leftCurrentSpeed, rightTargetSpeed,
                                isLocked, rightNormal);

            elapsed += dt;
            yield return null;
        }

        rightCurrentSpeed = rightTargetSpeed;
        leftCurrentSpeed  = leftTargetSpeed;

        // Steady-state loop.
        while (true)
        {
            float dt = Time.deltaTime;
            SpinWheels(rightCurrentSpeed, leftCurrentSpeed, dt);
            UpdateBarsAndLabels(rightCurrentSpeed, leftCurrentSpeed, rightTargetSpeed,
                                isLocked, rightNormal);
            yield return null;
        }
    }

    /// <summary>
    /// Rotates wheels and gears via world-space RotateAround — identical to the original
    /// implementation, correctly handling meshes with non-identity local rotations.
    /// </summary>
    void SpinWheels(float rightSpeed, float leftSpeed, float dt)
    {
        if (rightWheel != null)
            rightWheel.RotateAround(rightWheel.position, spinAxis, rightSpeed * dt);

        if (leftWheel != null)
            leftWheel.RotateAround(leftWheel.position, spinAxis, leftSpeed * dt);

        // Gear animation — ring gear and carrier at baseSpeed; spider gears at their wheel speeds.
        if (ringGear != null)
            ringGear.RotateAround(ringGear.position, spinAxis, baseSpeed * dt);
        if (differentialCarrier != null)
            differentialCarrier.RotateAround(differentialCarrier.position, spinAxis, baseSpeed * dt);
        if (spiderGearLeft != null)
            spiderGearLeft.RotateAround(spiderGearLeft.position, spinAxis, leftSpeed * dt);
        if (spiderGearRight != null)
            spiderGearRight.RotateAround(spiderGearRight.position, spinAxis, rightSpeed * dt);
    }

    // targets == null means pulse all renderers at full intensity.
    IEnumerator RunHighlightPulse(Transform[] targets)
    {
        var active = new HashSet<MeshRenderer>();
        if (targets != null)
            foreach (var t in targets)
                if (t != null && t.TryGetComponent<MeshRenderer>(out var mr))
                    active.Add(mr);

        while (true)
        {
            float pulse = Mathf.Abs(Mathf.Sin(Time.time * highlightPulseSpeed * Mathf.PI));
            Color full  = highlightColor * pulse * 2f;
            Color dim   = highlightColor * 0.15f;

            foreach (var mr in _diffRenderers)
            {
                bool isFocused = targets == null || active.Contains(mr);
                mr.material.SetColor("_EmissionColor", isFocused ? full : dim);
            }
            yield return null;
        }
    }

    /// <summary>
    /// Fades the diagramImage CanvasGroup to targetAlpha over diagramFadeDuration.
    /// </summary>
    IEnumerator FadeDiagram(float targetAlpha)
    {
        if (diagramImage == null) yield break;

        float startAlpha = diagramImage.alpha;
        float elapsed    = 0f;

        diagramImage.blocksRaycasts = targetAlpha > 0f;

        while (elapsed < diagramFadeDuration)
        {
            elapsed += Time.deltaTime;
            diagramImage.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / diagramFadeDuration);
            yield return null;
        }

        diagramImage.alpha = targetAlpha;
    }

    // ── UI Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates speed bars and labels each frame.
    /// isLocked:    locked-axle phase — left = SCRUB, right = OK (when rightNormal) or SCRUB.
    /// rightNormal: true when the right wheel should show green/OK during a locked turn.
    /// </summary>
    void UpdateBarsAndLabels(float rightSpeed, float leftSpeed, float maxSpeed,
                             bool isLocked, bool rightNormal = false)
    {
        if (isLocked)
        {
            // Left wheel: inner wheel physically scrubbing the tarmac.
            SetBar(leftSpeedBar,  leftSpeed  / maxSpeed, scrubColor);
            SetLabel(leftLabel,  LabelScrub);

            // Right wheel: outer wheel rolling normally.
            SetBar(rightSpeedBar, rightSpeed / maxSpeed, rightNormal ? normalColor : scrubColor);
            SetLabel(rightLabel,  rightNormal ? LabelOk : LabelScrub);
        }
        else
        {
            bool  rightIsFaster = rightSpeed >= leftSpeed;
            float totalMax      = baseSpeed * outerMultiplier;
            if (totalMax <= 0f) totalMax = 1f;

            SetBar(rightSpeedBar, rightSpeed / totalMax, rightIsFaster ? fastColor   : normalColor);
            SetBar(leftSpeedBar,  leftSpeed  / totalMax, rightIsFaster ? normalColor : fastColor);

            bool differential = Mathf.Abs(rightSpeed - leftSpeed) > 0.5f;
            SetLabel(rightLabel, differential ? (rightIsFaster ? LabelFast : LabelSlow) : "");
            SetLabel(leftLabel,  differential ? (rightIsFaster ? LabelSlow : LabelFast) : "");
        }
    }

    void ClearBarsAndLabels()
    {
        SetBar(rightSpeedBar, 0f, scrubColor);
        SetBar(leftSpeedBar,  0f, scrubColor);
        SetLabel(rightLabel, "");
        SetLabel(leftLabel,  "");
    }

    static void SetBar(Image bar, float fill, Color color)
    {
        if (bar == null) return;
        bar.fillAmount = Mathf.Clamp01(fill);
        bar.color      = color;
    }

    static void SetLabel(TextMeshProUGUI label, string text)
    {
        if (label == null) return;
        label.text = text;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Returns true when both required wheel Transforms are assigned.</summary>
    bool WheelRefsValid()
    {
        if (rightWheel && leftWheel) return true;

        Debug.LogWarning(
            "[DifferentialDemoController] rightWheel or leftWheel is null. " +
            "Assign both in the Inspector.", this);
        return false;
    }

    void StopHighlightCoroutine()
    {
        if (_highlightCoroutine != null)
        {
            StopCoroutine(_highlightCoroutine);
            _highlightCoroutine = null;
        }
    }
}
