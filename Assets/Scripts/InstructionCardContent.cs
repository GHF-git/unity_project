using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds and drives the Step 01 instruction card at runtime.
///
/// Lifecycle:
///   Awake  → BuildContent() creates the full hierarchy once, subscribes to snap zones.
///   ShowCard() (via InstructionCardController) → card animates in, content already built.
///   Any snap event                             → SetSuccessState() — once, guarded.
///   ContinueButton click                       → hides canvas root.
/// </summary>
[RequireComponent(typeof(InstructionCardController))]
public class InstructionCardContent : MonoBehaviour
{
    // ── Per-step success data ─────────────────────────────────────────────────

    private struct StepSuccessData
    {
        public string SuccessBarTitle;
        public string SuccessBarSub;
        public string NextStepLabel;      // null = hide next-step preview

        // Optional per-step overrides consumed by SetSuccessState():
        public string SuccessSubtitle;    // null → "Perfectly placed — well done"
        public string SuccessPieceName;   // null → main instruction unchanged
        public string SuccessSnapValue;   // null → $"{_currentSnapZoneName} ✓"
        public string SuccessDescription; // null → hide; non-null → show with this text
        public string SuccessFactText;    // null → fact card unchanged
    }

    // ── Colours ───────────────────────────────────────────────────────────────

    private static readonly Color ColBackground    = new Color(0.055f, 0.055f, 0.055f, 0.92f);
    private static readonly Color ColBorderDefault = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color ColDivider       = new Color(1f, 1f, 1f, 0.06f);
    private static readonly Color ColBreadcrumb    = new Color(0.33f, 0.33f, 0.33f, 1f);
    private static readonly Color ColPhaseBg       = new Color(0.10f, 0.22f, 0.45f, 0.35f);
    private static readonly Color ColPhaseText     = new Color(0.45f, 0.72f, 1.00f, 1f);
    private static readonly Color ColTaskLabel     = new Color(0.75f, 0.75f, 0.75f, 1f);
    private static readonly Color ColMainInstr     = new Color(1.00f, 1.00f, 1.00f, 1f);
    private static readonly Color ColSubtitle      = new Color(0.88f, 0.88f, 0.88f, 1f);
    private static readonly Color ColDescription   = new Color(0.82f, 0.82f, 0.82f, 1f);
    private static readonly Color ColSnapBg        = new Color(0.06f, 0.14f, 0.25f, 0.50f);
    private static readonly Color ColSnapBorder    = new Color(0.25f, 0.55f, 1.00f, 0.30f);
    private static readonly Color ColSnapLabel     = new Color(0.35f, 0.55f, 0.85f, 1f);
    private static readonly Color ColSnapValue     = new Color(0.55f, 0.75f, 1.00f, 1f);
    private static readonly Color ColSnapIndicator = new Color(0.35f, 0.65f, 1.00f, 1f);
    private static readonly Color ColFactBg        = new Color(1f, 1f, 1f, 0.03f);
    private static readonly Color ColFactAccent    = new Color(0.45f, 0.72f, 1.00f, 0.40f);
    private static readonly Color ColFactText      = new Color(0.90f, 0.90f, 0.90f, 1f);
    private static readonly Color ColBtnDisabledBg = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color ColBtnDisabledBd = new Color(1f, 1f, 1f, 0.15f);
    private static readonly Color ColBtnDisabledTx = new Color(0.47f, 0.47f, 0.47f, 1f);
    private static readonly Color ColSuccessGreen     = new Color(0.10f,  0.42f,  0.24f,  1f);
    private static readonly Color ColSuccessText      = new Color(0.20f,  0.85f,  0.45f,  1f);
    private static readonly Color ColSuccessBorder    = new Color(0.15f,  0.70f,  0.35f,  0.60f);
    private static readonly Color ColBtnSuccessBorder = new Color(0.15f,  0.70f,  0.35f,  1.00f);
    private static readonly Color ColSuccessBg        = new Color(0.039f, 0.122f, 0.071f, 0.95f); // rgba(10,31,18,0.95)
    private static readonly Color ColSuccessCardBd    = new Color(0.102f, 0.420f, 0.235f, 1f);    // #1a6b3c
    private static readonly Color ColDescriptionSuccess = new Color(0.65f, 0.90f, 0.72f, 1f);

    // Phase 3 — Body Panels & Closures (orange, #b86a00)
    private static readonly Color ColPhase3Bg        = new Color(0.722f, 0.416f, 0f, 0.15f);
    private static readonly Color ColPhase3Border     = new Color(0.722f, 0.416f, 0f, 0.35f);
    private static readonly Color ColPhase3Text       = new Color(0.478f, 0.275f, 0f, 1f);
    private static readonly Color ColPhase3TextDark   = new Color(0.282f, 0.165f, 0f, 1f);
    private static readonly Color ColPhase3SnapBg     = new Color(0.722f, 0.416f, 0f, 0.07f);
    private static readonly Color ColPhase3SnapBorder = new Color(0.722f, 0.416f, 0f, 0.40f);

    // Phase 4 — Lighting (dark purple, #4a2e82)
    private static readonly Color ColPhase4Bg        = new Color(0.290f, 0.180f, 0.510f, 0.15f);
    private static readonly Color ColPhase4Border     = new Color(0.290f, 0.180f, 0.510f, 0.35f);
    private static readonly Color ColPhase4Text       = new Color(0.290f, 0.180f, 0.510f, 1f);
    private static readonly Color ColPhase4TextDark   = new Color(0.165f, 0.098f, 0.306f, 1f);
    private static readonly Color ColPhase4SnapBg     = new Color(0.290f, 0.180f, 0.510f, 0.07f);
    private static readonly Color ColPhase4SnapBorder = new Color(0.290f, 0.180f, 0.510f, 0.40f);

    // Phase 5 — Final Assembly (red, #8b1a1a)
    private static readonly Color ColPhase5Bg        = new Color(0.545f, 0.102f, 0.102f, 0.15f);
    private static readonly Color ColPhase5Border     = new Color(0.545f, 0.102f, 0.102f, 0.35f);
    private static readonly Color ColPhase5Text       = new Color(0.320f, 0.020f, 0.020f, 1f);
    private static readonly Color ColPhase5TextDark   = new Color(0.360f, 0.040f, 0.040f, 1f);
    private static readonly Color ColPhase5SnapBg     = new Color(0.545f, 0.102f, 0.102f, 0.07f);
    private static readonly Color ColPhase5SnapBorder = new Color(0.545f, 0.102f, 0.102f, 0.40f);

    // ── Runtime references ────────────────────────────────────────────────────

    [Header("End State")]
    public ReadyForTheRoadButton readyForTheRoadButton;

    private TextMeshProUGUI _taskLabel;
    private TextMeshProUGUI _subtitle;
    private TextMeshProUGUI _description;
    private Image           _snapBorderImg;
    private TextMeshProUGUI _snapLabelTmp;
    private TextMeshProUGUI _snapValueTmp;
    private TextMeshProUGUI _snapIndicator;
    private Button          _continueButton;
    private Image           _continueBgImg;
    private Image           _continueBorderImg;
    private TextMeshProUGUI _continueLabelTmp;
    private Image           _cardBgImg;
    private GameObject      _successBarGo;
    private TextMeshProUGUI _successBarTitle;
    private TextMeshProUGUI _successBarSub;
    private GameObject      _nextStepPreviewGo;
    private TextMeshProUGUI _mainInstructionTmp;
    private TextMeshProUGUI _factTextTmp;

    // ── State ─────────────────────────────────────────────────────────────────

    private bool            _isBuilt;
    private bool            _successTriggered;
    private bool            _isPulsing;
    private Coroutine       _pulseCoroutine;
    private string          _currentSnapZoneName = "base-snap";
    private StepSuccessData _successData;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (_isBuilt) return;
        _isBuilt = true;

        _cardBgImg = GetComponent<Image>();

        BuildContent();
        SubscribeToSnapZone("base-snap");
    }

    void OnDestroy()
    {
        UnsubscribeFromAllSnapZones();
    }

    // ── Snap subscription ─────────────────────────────────────────────────────

    /// <summary>Subscribes to every SnapToPlace in the scene.</summary>
    private void SubscribeToAllSnapZones()
    {
        SnapToPlace[] zones = Object.FindObjectsByType<SnapToPlace>(FindObjectsSortMode.None);
        foreach (SnapToPlace z in zones)
            z.OnObjectSnappedEvent += OnAnySnapCompleted;
    }

    private void UnsubscribeFromAllSnapZones()
    {
        SnapToPlace[] zones = Object.FindObjectsByType<SnapToPlace>(FindObjectsSortMode.None);
        foreach (SnapToPlace z in zones)
            z.OnObjectSnappedEvent -= OnAnySnapCompleted;
    }

    private void OnAnySnapCompleted(GameObject obj, SnapToPlace zone)
    {
        if (_successTriggered) return;

        // Only trigger success when the correct zone for this card is snapped.
        if (!ZoneNameMatchesCurrent(zone.gameObject.name)) return;

        SetSuccessState();
    }

    /// <summary>
    /// Returns true if the snapped zone name matches the zone this card is currently watching.
    /// Comparison is case-insensitive and ignores hyphens, underscores, and spaces.
    /// </summary>
    private bool ZoneNameMatchesCurrent(string zoneName)
    {
        if (string.IsNullOrEmpty(_currentSnapZoneName)) return true;
        return NormalizeZone(zoneName).Contains(NormalizeZone(_currentSnapZoneName));
    }

    private static string NormalizeZone(string s)
        => s.ToLowerInvariant().Replace("-", "").Replace("_", "").Replace(" ", "");

    // ── Success state ─────────────────────────────────────────────────────────

    /// <summary>Transitions the card to the success / completed state.</summary>
    public void SetSuccessState()
    {
        _successTriggered = true;

        // Stop snap indicator pulse
        _isPulsing = false;
        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }
        if (_snapIndicator != null)
            _snapIndicator.color = ColSuccessText;

        // Task label
        if (_taskLabel != null)
        {
            _taskLabel.text  = "COMPLETED";
            _taskLabel.color = ColSuccessText;
        }

        // Subtitle
        if (_subtitle != null)
        {
            _subtitle.text  = _successData.SuccessSubtitle ?? "Perfectly placed \u2014 well done";
            _subtitle.color = ColSuccessText;
        }

        // Main instruction / piece name — update only if override provided
        if (_mainInstructionTmp != null && !string.IsNullOrEmpty(_successData.SuccessPieceName))
        {
            _mainInstructionTmp.text  = _successData.SuccessPieceName;
            _mainInstructionTmp.color = ColSuccessText;
        }

        // Description — show with success text if provided, otherwise hide
        if (_description != null)
        {
            if (!string.IsNullOrEmpty(_successData.SuccessDescription))
            {
                _description.text  = _successData.SuccessDescription;
                _description.color = ColDescriptionSuccess;
                _description.gameObject.SetActive(true);
            }
            else
            {
                _description.gameObject.SetActive(false);
            }
        }

        // Snap box: border green, label/value updated
        if (_snapBorderImg  != null) _snapBorderImg.color  = ColSuccessBorder;
        if (_snapLabelTmp   != null) _snapLabelTmp.text    = "CONFIRMED";
        if (_snapValueTmp   != null)
            _snapValueTmp.text = _successData.SuccessSnapValue ?? $"{_currentSnapZoneName} \u2713";

        // Fact card — update only if override provided
        if (_factTextTmp != null && !string.IsNullOrEmpty(_successData.SuccessFactText))
            _factTextTmp.text = _successData.SuccessFactText;

        // Continue button — enable + green
        if (_continueButton != null)
            _continueButton.interactable = true;

        if (_continueBgImg     != null) _continueBgImg.color     = ColSuccessGreen;
        if (_continueBorderImg != null) _continueBorderImg.color = ColBtnSuccessBorder;
        if (_continueLabelTmp  != null)
        {
            _continueLabelTmp.text  = "Continue \u2192";
            _continueLabelTmp.color = Color.white;
        }

        // Card background and border go green
        if (_cardBgImg != null)
            _cardBgImg.color = ColSuccessBg;

        Transform borderChild = transform.Find("Border");
        if (borderChild != null)
        {
            Image borderImg = borderChild.GetComponent<Image>();
            if (borderImg != null) borderImg.color = ColSuccessCardBd;
        }

        // Reveal success bar (Step 02+)
        if (_successBarGo != null)
            _successBarGo.SetActive(true);

        // Reveal next-step preview (Step 02+)
        if (_nextStepPreviewGo != null && !string.IsNullOrEmpty(_successData.NextStepLabel))
            _nextStepPreviewGo.SetActive(true);
    }

    // ── Continue ──────────────────────────────────────────────────────────────

    /// <summary>Called by the Continue button's onClick. Advances to the next step.</summary>
    public void OnContinueClicked()
    {
        if (!_successTriggered) return;

        if (_currentSnapZoneName == "base-snap")
            LoadStep02();
        else if (_currentSnapZoneName == "rearAxles-snap")
            LoadStep03();
        else if (_currentSnapZoneName == "engine-snap")
            LoadStep04();
        else if (_currentSnapZoneName == "SteeringSystem-snap")
            LoadStep06();
        else if (_currentSnapZoneName == "wheels-snap")
            LoadStep07();
        else if (_currentSnapZoneName == "bodyPannels-snap")
            LoadStep08();
        else if (_currentSnapZoneName == "frontBumper-snap")
            LoadStep09();
        else if (_currentSnapZoneName == "rearBumper-snap")
            LoadStep10();
        else if (_currentSnapZoneName == "hood-snap")
            LoadStep11();
        else if (_currentSnapZoneName == "CarRoof-snap")
            LoadStep12();
        else if (_currentSnapZoneName == "leftDoor-snap")
            LoadStep13();
        else if (_currentSnapZoneName == "RightDoor-snap")
            LoadStep14();
        else if (_currentSnapZoneName == "trunk lid-snap")
            LoadStep15();
        else if (_currentSnapZoneName == "spoiler-snap")
            LoadStep16();
        else if (_currentSnapZoneName == "LeftHeadLight-snap")
            LoadStep17();
        else if (_currentSnapZoneName == "RightHeadLight-snap")
            LoadStep18();
        else if (_currentSnapZoneName == "LeftTaillights-snap")
            LoadStep19();
        else if (_currentSnapZoneName == "RightTaillights-snap")
            LoadStep20();
    }

    private void LoadStep02()
    {
        // Update active snap zone for this step
        _currentSnapZoneName = "rearAxles-snap";

        // Step 02 success content data
        _successData = new StepSuccessData
        {
            SuccessBarTitle = "Rear Axles locked in!",
            SuccessBarSub   = "The drivetrain foundation is set.",
            NextStepLabel   = null
        };

        // Reset card background and border to dark default before rebuilding
        if (_cardBgImg != null)
            _cardBgImg.color = ColBackground;

        Transform borderChild = transform.Find("Border");
        if (borderChild != null)
        {
            Image borderImg = borderChild.GetComponent<Image>();
            if (borderImg != null) borderImg.color = ColBorderDefault;
        }

        // Reset state
        _successTriggered = false;
        _isPulsing        = false;

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        UnsubscribeFromAllSnapZones();

        // Null-reset new runtime refs so stale pointers can't survive the rebuild
        _successBarGo       = null;
        _successBarTitle    = null;
        _successBarSub      = null;
        _nextStepPreviewGo  = null;
        _mainInstructionTmp = null;
        _factTextTmp        = null;

        // Rebuild card content with Step 02 data
        CleanStaleChildren();
        BuildHeader02();
        BuildMainContent02();
        BuildSnapTarget02();
        BuildFactCard02();
        BuildSuccessBar02();
        BuildFooter();

        // Re-subscribe — only react to rearAxles-snap
        SubscribeToSnapZone("rearAxles-snap");

        // Replay pop-in animation
        InstructionCardController ctrl = GetComponent<InstructionCardController>();
        if (ctrl != null)
            ctrl.ReplayAnimation();
    }

    private void LoadStep03()
    {
        _currentSnapZoneName = "engine-snap";

        _successData = new StepSuccessData
        {
            SuccessBarTitle    = "Engine locked in!",
            SuccessBarSub      = "The heart of the 911S is mounted.",
            NextStepLabel      = null,
            SuccessSubtitle    = "Perfectly seated \u2014 excellent work",
            SuccessPieceName   = "Engine Installed",
            SuccessSnapValue   = "engine-snap \u2713",
            SuccessDescription = "The flat-six engine is now securely mounted in the rear engine bay. " +
                                 "Alignment with the gearbox input shaft is confirmed.",
            SuccessFactText    = "The 2.4L flat-6 engine sits entirely behind the rear axle \u2014 " +
                                 "this rear-biased weight distribution is a key factor in the " +
                                 "distinctive handling of the Porsche 911."
        };

        if (_cardBgImg != null)
            _cardBgImg.color = ColBackground;

        Transform borderChild = transform.Find("Border");
        if (borderChild != null)
        {
            Image borderImg = borderChild.GetComponent<Image>();
            if (borderImg != null) borderImg.color = ColBorderDefault;
        }

        _successTriggered = false;
        _isPulsing        = false;

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        UnsubscribeFromAllSnapZones();

        _successBarGo       = null;
        _successBarTitle    = null;
        _successBarSub      = null;
        _nextStepPreviewGo  = null;
        _mainInstructionTmp = null;
        _factTextTmp        = null;

        CleanStaleChildren();
        BuildHeader03();
        BuildMainContent03();
        BuildSnapTarget03();
        BuildFactCard03();
        BuildSuccessBar03();
        BuildFooter();

        SubscribeToSnapZone("engine-snap");

        InstructionCardController ctrl = GetComponent<InstructionCardController>();
        if (ctrl != null)
            ctrl.ReplayAnimation();
    }

    private void LoadStep04()
    {
        _currentSnapZoneName = "SteeringSystem-snap";

        _successData = new StepSuccessData
        {
            SuccessBarTitle    = "Steering System locked in!",
            SuccessBarSub      = "The car can now be directed.",
            NextStepLabel      = null,
            SuccessSubtitle    = "Perfectly aligned \u2014 excellent work",
            SuccessPieceName   = "Steering System Installed",
            SuccessSnapValue   = "steering_snap \u2713",
            SuccessDescription = "The steering system is now mounted and aligned. " +
                                 "The column connects the wheel to the rack with zero play.",
            SuccessFactText    = "The rack-and-pinion system provides direct, unfiltered road feedback " +
                                 "\u2014 making the 911S highly responsive to even the smallest steering inputs."
        };

        if (_cardBgImg != null)
            _cardBgImg.color = ColBackground;

        Transform borderChild = transform.Find("Border");
        if (borderChild != null)
        {
            Image borderImg = borderChild.GetComponent<Image>();
            if (borderImg != null) borderImg.color = ColBorderDefault;
        }

        _successTriggered = false;
        _isPulsing        = false;

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        UnsubscribeFromAllSnapZones();

        _successBarGo       = null;
        _successBarTitle    = null;
        _successBarSub      = null;
        _nextStepPreviewGo  = null;
        _mainInstructionTmp = null;
        _factTextTmp        = null;

        CleanStaleChildren();
        BuildHeader04();
        BuildMainContent04();
        BuildSnapTarget04();
        BuildFactCard04();
        BuildSuccessBar04();
        BuildFooter();

        SubscribeToSnapZone("SteeringSystem-snap");

        InstructionCardController ctrl = GetComponent<InstructionCardController>();
        if (ctrl != null)
            ctrl.ReplayAnimation();
    }

    private void LoadStep06()
    {
        _currentSnapZoneName = "wheels-snap";

        _successData = new StepSuccessData
        {
            SuccessBarTitle    = "All wheels mounted!",
            SuccessBarSub      = "The car is ready to roll.",
            NextStepLabel      = null,
            SuccessSubtitle    = "All four wheels locked in \u2014 excellent work",
            SuccessPieceName   = "Wheels Installed",
            SuccessSnapValue   = "wheels_snap \u2713",
            SuccessDescription = "Mount all four wheels onto the hub flanges. Start with rear then front. " +
                                 "The wider 185/70 tires go on the rear, narrower set on the front. " +
                                 "Apply equal pressure to all lug positions.",
            SuccessFactText    = "The staggered wheel sizing compensates for rear-weight bias \u2014 " +
                                 "more grip exactly where the engine pushes."
        };

        if (_cardBgImg != null)
            _cardBgImg.color = ColBackground;

        Transform borderChild = transform.Find("Border");
        if (borderChild != null)
        {
            Image borderImg = borderChild.GetComponent<Image>();
            if (borderImg != null) borderImg.color = ColBorderDefault;
        }

        _successTriggered = false;
        _isPulsing        = false;

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        UnsubscribeFromAllSnapZones();
        UnsubscribeFromWheelZones();

        _successBarGo       = null;
        _successBarTitle    = null;
        _successBarSub      = null;
        _nextStepPreviewGo  = null;
        _mainInstructionTmp = null;
        _factTextTmp        = null;

        CleanStaleChildren();
        BuildHeader06();
        BuildMainContent06();
        BuildSnapTarget06();
        BuildFactCard06();
        BuildSuccessBar06();
        BuildFooter();

        // Wheels need all 4 placed — subscribe to wheel zones, not a single zone.
        SubscribeToWheelZones();

        InstructionCardController ctrl = GetComponent<InstructionCardController>();
        if (ctrl != null)
            ctrl.ReplayAnimation();
    }

    private void LoadStep07()
    {
        _currentSnapZoneName = "bodyPannels-snap";

        _successData = new StepSuccessData
        {
            SuccessBarTitle    = "Body Panels fitted!",
            SuccessBarSub      = "The shell is taking shape.",
            NextStepLabel      = null,
            SuccessSubtitle    = "All panels locked in \u2014 excellent work",
            SuccessPieceName   = "Body Panels Installed",
            SuccessSnapValue   = "body_panels_snap \u2713",
            SuccessDescription = "Lay the main body panel shell over the chassis. Align the sill channels " +
                                 "with the chassis longitudinals first, then snap from center outward. " +
                                 "This panel forms the floor, sills and inner arch structure.",
            SuccessFactText    = "The 911 unibody relies on this shell for torsional rigidity \u2014 " +
                                 "it defines how precisely the car handles."
        };

        if (_cardBgImg != null)
            _cardBgImg.color = ColBackground;

        Transform borderChild = transform.Find("Border");
        if (borderChild != null)
        {
            Image borderImg = borderChild.GetComponent<Image>();
            if (borderImg != null) borderImg.color = ColBorderDefault;
        }

        _successTriggered = false;
        _isPulsing        = false;

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        UnsubscribeFromAllSnapZones();
        UnsubscribeFromWheelZones();

        _successBarGo       = null;
        _successBarTitle    = null;
        _successBarSub      = null;
        _nextStepPreviewGo  = null;
        _mainInstructionTmp = null;
        _factTextTmp        = null;

        CleanStaleChildren();
        BuildHeader07();
        BuildMainContent07();
        BuildSnapTarget07();
        BuildFactCard07();
        BuildSuccessBar07();
        BuildFooter();

        SubscribeToSnapZone("bodyPannels-snap");

        InstructionCardController ctrl = GetComponent<InstructionCardController>();
        if (ctrl != null)
            ctrl.ReplayAnimation();
    }

    private void LoadStep08()
    {
        _currentSnapZoneName = "frontBumper-snap";

        _successData = new StepSuccessData
        {
            SuccessBarTitle    = "Front Bumper fitted!",
            SuccessBarSub      = "The front end is coming together.",
            NextStepLabel      = null,
            SuccessSubtitle    = "Front bumper locked in \u2014 great work",
            SuccessPieceName   = "Front Bumper Installed",
            SuccessSnapValue   = "front_bumper_snap \u2713",
            SuccessDescription = "Clip the front bumper to the front body panel. Two lower clips, two upper pins. " +
                                 "The lower valance lip must sit flush with the front apron \u2014 " +
                                 "no gap should be visible at the bottom edge.",
            SuccessFactText    = "Pre-1974 911S bumpers were slim chrome units \u2014 purely aesthetic, " +
                                 "before impact regulations changed the design."
        };

        if (_cardBgImg != null)
            _cardBgImg.color = ColBackground;

        Transform borderChild = transform.Find("Border");
        if (borderChild != null)
        {
            Image borderImg = borderChild.GetComponent<Image>();
            if (borderImg != null) borderImg.color = ColBorderDefault;
        }

        _successTriggered = false;
        _isPulsing        = false;

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        UnsubscribeFromAllSnapZones();

        _successBarGo       = null;
        _successBarTitle    = null;
        _successBarSub      = null;
        _nextStepPreviewGo  = null;
        _mainInstructionTmp = null;
        _factTextTmp        = null;

        CleanStaleChildren();
        BuildHeader08();
        BuildMainContent08();
        BuildSnapTarget08();
        BuildFactCard08();
        BuildSuccessBar08();
        BuildFooter();

        SubscribeToSnapZone("frontBumper-snap");

        InstructionCardController ctrl = GetComponent<InstructionCardController>();
        if (ctrl != null)
            ctrl.ReplayAnimation();
    }

    private void LoadStep09()
    {
        _currentSnapZoneName = "rearBumper-snap";

        _successData = new StepSuccessData
        {
            SuccessBarTitle    = "Rear Bumper fitted!",
            SuccessBarSub      = "The tail end is sealed up.",
            NextStepLabel      = null,
            SuccessSubtitle    = "Rear bumper locked in \u2014 excellent work",
            SuccessPieceName   = "Rear Bumper Installed",
            SuccessSnapValue   = "rear_bumper_snap \u2713",
            SuccessDescription = "Snap the rear bumper over the tail panel. Align the number plate recess centrally. " +
                                 "The exhaust cut-out must clear both tailpipes. " +
                                 "Two side clips lock the bumper flush to the quarter panels.",
            SuccessFactText    = "The classic 911S rear bumper houses the fog light and reflector \u2014 " +
                                 "minimal but iconic in proportion."
        };

        if (_cardBgImg != null)
            _cardBgImg.color = ColBackground;

        Transform borderChild = transform.Find("Border");
        if (borderChild != null)
        {
            Image borderImg = borderChild.GetComponent<Image>();
            if (borderImg != null) borderImg.color = ColBorderDefault;
        }

        _successTriggered = false;
        _isPulsing        = false;

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        UnsubscribeFromAllSnapZones();

        _successBarGo       = null;
        _successBarTitle    = null;
        _successBarSub      = null;
        _nextStepPreviewGo  = null;
        _mainInstructionTmp = null;
        _factTextTmp        = null;

        CleanStaleChildren();
        BuildHeader09();
        BuildMainContent09();
        BuildSnapTarget09();
        BuildFactCard09();
        BuildSuccessBar09();
        BuildFooter();

        SubscribeToSnapZone("rearBumper-snap");

        InstructionCardController ctrl = GetComponent<InstructionCardController>();
        if (ctrl != null)
            ctrl.ReplayAnimation();
    }

    private void LoadStep10()
    {
        _currentSnapZoneName = "hood-snap";

        _successData = new StepSuccessData
        {
            SuccessBarTitle    = "Hood fitted!",
            SuccessBarSub      = "The frunk is closed up.",
            NextStepLabel      = null,
            SuccessSubtitle    = "Hood locked in \u2014 great work",
            SuccessPieceName   = "Hood Installed",
            SuccessSnapValue   = "hood_snap \u2713",
            SuccessDescription = "Hinge the front hood (frunk lid) onto the front body. " +
                                 "Insert the hinge pins from the inside outward. " +
                                 "The 911 hood opens forward \u2014 verify hinge orientation before snapping.",
            SuccessFactText    = "The front trunk stores the spare wheel \u2014 the engine is at the rear, " +
                                 "freeing the entire nose for storage."
        };

        if (_cardBgImg != null)
            _cardBgImg.color = ColBackground;

        Transform borderChild = transform.Find("Border");
        if (borderChild != null)
        {
            Image borderImg = borderChild.GetComponent<Image>();
            if (borderImg != null) borderImg.color = ColBorderDefault;
        }

        _successTriggered = false;
        _isPulsing        = false;

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        UnsubscribeFromAllSnapZones();

        _successBarGo       = null;
        _successBarTitle    = null;
        _successBarSub      = null;
        _nextStepPreviewGo  = null;
        _mainInstructionTmp = null;
        _factTextTmp        = null;

        CleanStaleChildren();
        BuildHeader10();
        BuildMainContent10();
        BuildSnapTarget10();
        BuildFactCard10();
        BuildSuccessBar10();
        BuildFooter();

        SubscribeToSnapZone("hood-snap");

        InstructionCardController ctrl = GetComponent<InstructionCardController>();
        if (ctrl != null)
            ctrl.ReplayAnimation();
    }

    private void LoadStep11()
    {
        _currentSnapZoneName = "CarRoof-snap";

        _successData = new StepSuccessData
        {
            SuccessBarTitle    = "Car Roof fitted!",
            SuccessBarSub      = "The roofline is sealed up.",
            NextStepLabel      = null,
            SuccessSubtitle    = "Roof locked in \u2014 great work",
            SuccessPieceName   = "Car Roof Installed",
            SuccessSnapValue   = "roof_snap \u2713",
            SuccessDescription = "Lower the roof panel onto the A and B pillars simultaneously \u2014 " +
                                 "this is a large structural piece, so align both sides at once. " +
                                 "The drip rail channels must sit precisely inside the door aperture " +
                                 "seal groove for correct fitment.",
            SuccessFactText    = "The 911 roof is structural \u2014 it contributes to rollover " +
                                 "protection and overall body stiffness."
        };

        if (_cardBgImg != null)
            _cardBgImg.color = ColBackground;

        Transform borderChild = transform.Find("Border");
        if (borderChild != null)
        {
            Image borderImg = borderChild.GetComponent<Image>();
            if (borderImg != null) borderImg.color = ColBorderDefault;
        }

        _successTriggered = false;
        _isPulsing        = false;

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        UnsubscribeFromAllSnapZones();

        _successBarGo       = null;
        _successBarTitle    = null;
        _successBarSub      = null;
        _nextStepPreviewGo  = null;
        _mainInstructionTmp = null;
        _factTextTmp        = null;

        CleanStaleChildren();
        BuildHeader11();
        BuildMainContent11();
        BuildSnapTarget11();
        BuildFactCard11();
        BuildSuccessBar11();
        BuildFooter();

        SubscribeToSnapZone("CarRoof-snap");

        InstructionCardController ctrl = GetComponent<InstructionCardController>();
        if (ctrl != null)
            ctrl.ReplayAnimation();
    }

    private void BuildHeader11()
    {
        GameObject header = CreatePanel("Header", transform);
        AddVerticalLayout(header, new RectOffset(0, 0, 0, 0), 10, false);

        GameObject breadcrumb = CreateTextObject("Breadcrumb", header.transform,
            "STEP 10 OF 19  \u00B7  PORSCHE 911S ASSEMBLY",
            12, ColTaskLabel, FontStyles.Normal);
        breadcrumb.GetComponent<TextMeshProUGUI>().characterSpacing = 1.2f;

        GameObject badgeRow = CreatePanel("PhaseBadge", header.transform);
        AddHorizontalLayout(badgeRow, new RectOffset(12, 12, 7, 7), 6);
        AddImageToGameObject(badgeRow, ColPhase3Bg);
        AddLayoutElement(badgeRow, preferredHeight: 30f);
        CreateOverlayBorder("PhaseBadgeBorder", badgeRow.transform, ColPhase3Border);

        CreateTextObject("PhaseLabel", badgeRow.transform,
            "\u25CF  PHASE 3 \u2014 BODY PANELS & CLOSURES",
            12, ColPhase3TextDark, FontStyles.Bold);

        GameObject divider = CreatePanel("Divider", header.transform);
        AddImageToGameObject(divider, ColDivider);
        AddLayoutElement(divider, preferredHeight: 1f);
    }

    private void BuildMainContent11()
    {
        GameObject main = CreatePanel("MainContent", transform);
        AddVerticalLayout(main, new RectOffset(0, 0, 0, 0), 12, false);

        _taskLabel = CreateTextObject("TaskLabel", main.transform,
            "YOUR TASK", 12, ColTaskLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _taskLabel.characterSpacing = 1.2f;

        _mainInstructionTmp = CreateTextObject("MainInstruction", main.transform,
            "Now place the Car Roof", 20, ColMainInstr, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _subtitle = CreateTextObject("Subtitle", main.transform,
            "Find it in the inventory \u2192 snap to the target below",
            14, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _description = CreateTextObject("Description", main.transform,
            "Lower the roof panel onto the A and B pillars simultaneously \u2014 " +
            "this is a large structural piece, so align both sides at once. " +
            "The drip rail channels must sit precisely inside the door aperture " +
            "seal groove for correct fitment.",
            13, ColDescription, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _description.lineSpacing = 6f;
    }

    private void BuildSnapTarget11()
    {
        GameObject snap = CreatePanel("SnapTarget", transform);
        AddHorizontalLayout(snap, new RectOffset(10, 10, 8, 8), 10);
        AddImageToGameObject(snap, ColPhase3SnapBg);

        _snapBorderImg = CreateOverlayBorder("SnapBorder", snap.transform, ColPhase3SnapBorder);

        GameObject snapLeft = CreatePanel("SnapLeft", snap.transform);
        AddVerticalLayout(snapLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(snapLeft, flexibleWidth: 1f);

        _snapLabelTmp = CreateTextObject("SnapLabel", snapLeft.transform,
            "SNAP TARGET", 11, ColPhase3TextDark, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _snapLabelTmp.characterSpacing = 1.2f;

        _snapValueTmp = CreateTextObject("SnapValue", snapLeft.transform,
            "roof_snap", 13, ColPhase3TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _snapIndicator = CreateTextObject("SnapIndicator", snap.transform,
            "\u25CF", 22, ColPhase3TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _snapIndicator.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(_snapIndicator.gameObject, preferredWidth: 28f);

        _isPulsing      = true;
        _pulseCoroutine = StartCoroutine(PulseIndicator());
    }

    private void BuildFactCard11()
    {
        GameObject fact = CreatePanel("FactCard", transform);
        AddVerticalLayout(fact, new RectOffset(10, 10, 8, 8), 0, false);
        AddImageToGameObject(fact, ColPhase3SnapBg);

        CreateOverlayBorder("FactBorder", fact.transform, ColPhase3Border, leftOnly: true);

        _factTextTmp = CreateTextObject("FactText", fact.transform,
            "The 911 roof is structural \u2014 it contributes to rollover " +
            "protection and overall body stiffness.",
            12, ColPhase3TextDark, FontStyles.Italic)
            .GetComponent<TextMeshProUGUI>();
        _factTextTmp.lineSpacing = 6f;
    }

    private void BuildSuccessBar11()
    {
        _successBarGo = CreatePanel("SuccessBar", transform);
        AddVerticalLayout(_successBarGo, new RectOffset(12, 12, 10, 10), 4, false);
        AddImageToGameObject(_successBarGo, ColSuccessBg);
        CreateOverlayBorder("SuccessBarBorder", _successBarGo.transform, ColSuccessCardBd);

        _successBarTitle = CreateTextObject("SuccessTitle", _successBarGo.transform,
            _successData.SuccessBarTitle, 15, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _successBarSub = CreateTextObject("SuccessSub", _successBarGo.transform,
            _successData.SuccessBarSub, 12, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _successBarGo.SetActive(false);
    }

    private void LoadStep12()
    {
        _currentSnapZoneName = "leftDoor-snap";

        _successData = new StepSuccessData
        {
            SuccessBarTitle    = "Left Door fitted!",
            SuccessBarSub      = "Driver's side is on.",
            NextStepLabel      = null,
            SuccessSubtitle    = "Left door locked in \u2014 great work",
            SuccessPieceName   = "Left Door Installed",
            SuccessSnapValue   = "left_door_snap \u2713",
            SuccessDescription = "Hang the left door on the B-pillar hinge mount. Upper hinge first, " +
                                 "then lower. Check the door sits flush with both the A-pillar and " +
                                 "rear quarter panel before confirming the snap.",
            SuccessFactText    = "Porsche\u2019s obsessive door gap tolerances were already present " +
                                 "in the 1970s \u2014 a hallmark of Stuttgart craftsmanship."
        };

        if (_cardBgImg != null)
            _cardBgImg.color = ColBackground;

        Transform borderChild = transform.Find("Border");
        if (borderChild != null)
        {
            Image borderImg = borderChild.GetComponent<Image>();
            if (borderImg != null) borderImg.color = ColBorderDefault;
        }

        _successTriggered = false;
        _isPulsing        = false;

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        UnsubscribeFromAllSnapZones();

        _successBarGo       = null;
        _successBarTitle    = null;
        _successBarSub      = null;
        _nextStepPreviewGo  = null;
        _mainInstructionTmp = null;
        _factTextTmp        = null;

        CleanStaleChildren();
        BuildHeader12();
        BuildMainContent12();
        BuildSnapTarget12();
        BuildFactCard12();
        BuildSuccessBar12();
        BuildFooter();

        SubscribeToSnapZone("leftDoor-snap");

        InstructionCardController ctrl = GetComponent<InstructionCardController>();
        if (ctrl != null)
            ctrl.ReplayAnimation();
    }

    private void BuildHeader12()
    {
        GameObject header = CreatePanel("Header", transform);
        AddVerticalLayout(header, new RectOffset(0, 0, 0, 0), 10, false);

        GameObject breadcrumb = CreateTextObject("Breadcrumb", header.transform,
            "STEP 11 OF 19  \u00B7  PORSCHE 911S ASSEMBLY",
            12, ColTaskLabel, FontStyles.Normal);
        breadcrumb.GetComponent<TextMeshProUGUI>().characterSpacing = 1.2f;

        GameObject badgeRow = CreatePanel("PhaseBadge", header.transform);
        AddHorizontalLayout(badgeRow, new RectOffset(12, 12, 7, 7), 6);
        AddImageToGameObject(badgeRow, ColPhase3Bg);
        AddLayoutElement(badgeRow, preferredHeight: 30f);
        CreateOverlayBorder("PhaseBadgeBorder", badgeRow.transform, ColPhase3Border);

        CreateTextObject("PhaseLabel", badgeRow.transform,
            "\u25CF  PHASE 3 \u2014 BODY PANELS & CLOSURES",
            12, ColPhase3TextDark, FontStyles.Bold);

        GameObject divider = CreatePanel("Divider", header.transform);
        AddImageToGameObject(divider, ColDivider);
        AddLayoutElement(divider, preferredHeight: 1f);
    }

    private void BuildMainContent12()
    {
        GameObject main = CreatePanel("MainContent", transform);
        AddVerticalLayout(main, new RectOffset(0, 0, 0, 0), 12, false);

        _taskLabel = CreateTextObject("TaskLabel", main.transform,
            "YOUR TASK", 12, ColTaskLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _taskLabel.characterSpacing = 1.2f;

        _mainInstructionTmp = CreateTextObject("MainInstruction", main.transform,
            "Now place the Left Door", 20, ColMainInstr, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _subtitle = CreateTextObject("Subtitle", main.transform,
            "Find it in the inventory \u2192 snap to the target below",
            14, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _description = CreateTextObject("Description", main.transform,
            "Hang the left door on the B-pillar hinge mount. Upper hinge first, " +
            "then lower. Check the door sits flush with both the A-pillar and " +
            "rear quarter panel before confirming the snap.",
            13, ColDescription, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _description.lineSpacing = 6f;
    }

    private void BuildSnapTarget12()
    {
        GameObject snap = CreatePanel("SnapTarget", transform);
        AddHorizontalLayout(snap, new RectOffset(10, 10, 8, 8), 10);
        AddImageToGameObject(snap, ColPhase3SnapBg);

        _snapBorderImg = CreateOverlayBorder("SnapBorder", snap.transform, ColPhase3SnapBorder);

        GameObject snapLeft = CreatePanel("SnapLeft", snap.transform);
        AddVerticalLayout(snapLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(snapLeft, flexibleWidth: 1f);

        _snapLabelTmp = CreateTextObject("SnapLabel", snapLeft.transform,
            "SNAP TARGET", 11, ColPhase3TextDark, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _snapLabelTmp.characterSpacing = 1.2f;

        _snapValueTmp = CreateTextObject("SnapValue", snapLeft.transform,
            "left_door_snap", 13, ColPhase3TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _snapIndicator = CreateTextObject("SnapIndicator", snap.transform,
            "\u25CF", 22, ColPhase3TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _snapIndicator.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(_snapIndicator.gameObject, preferredWidth: 28f);

        _isPulsing      = true;
        _pulseCoroutine = StartCoroutine(PulseIndicator());
    }

    private void BuildFactCard12()
    {
        GameObject fact = CreatePanel("FactCard", transform);
        AddVerticalLayout(fact, new RectOffset(10, 10, 8, 8), 0, false);
        AddImageToGameObject(fact, ColPhase3SnapBg);

        CreateOverlayBorder("FactBorder", fact.transform, ColPhase3Border, leftOnly: true);

        _factTextTmp = CreateTextObject("FactText", fact.transform,
            "Porsche\u2019s obsessive door gap tolerances were already present " +
            "in the 1970s \u2014 a hallmark of Stuttgart craftsmanship.",
            12, ColPhase3TextDark, FontStyles.Italic)
            .GetComponent<TextMeshProUGUI>();
        _factTextTmp.lineSpacing = 6f;
    }

    private void BuildSuccessBar12()
    {
        _successBarGo = CreatePanel("SuccessBar", transform);
        AddVerticalLayout(_successBarGo, new RectOffset(12, 12, 10, 10), 4, false);
        AddImageToGameObject(_successBarGo, ColSuccessBg);
        CreateOverlayBorder("SuccessBarBorder", _successBarGo.transform, ColSuccessCardBd);

        _successBarTitle = CreateTextObject("SuccessTitle", _successBarGo.transform,
            _successData.SuccessBarTitle, 15, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _successBarSub = CreateTextObject("SuccessSub", _successBarGo.transform,
            _successData.SuccessBarSub, 12, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _successBarGo.SetActive(false);
    }

    private void LoadStep13()
    {
        _currentSnapZoneName = "RightDoor-snap";

        _successData = new StepSuccessData
        {
            SuccessBarTitle    = "Right Door fitted!",
            SuccessBarSub      = "Passenger side is on.",
            NextStepLabel      = null,
            SuccessSubtitle    = "Right door locked in \u2014 great work",
            SuccessPieceName   = "Right Door Installed",
            SuccessSnapValue   = "right_door_snap \u2713",
            SuccessDescription = "Hang the right door symmetrically to the left \u2014 upper hinge first. " +
                                 "The door seal groove must compress evenly all the way around the " +
                                 "aperture when the snap confirms.",
            SuccessFactText    = "Equal door gaps on both sides ensure the aerodynamic seal \u2014 " +
                                 "even 2mm difference affects wind noise at speed."
        };

        if (_cardBgImg != null)
            _cardBgImg.color = ColBackground;

        Transform borderChild = transform.Find("Border");
        if (borderChild != null)
        {
            Image borderImg = borderChild.GetComponent<Image>();
            if (borderImg != null) borderImg.color = ColBorderDefault;
        }

        _successTriggered = false;
        _isPulsing        = false;

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        UnsubscribeFromAllSnapZones();

        _successBarGo       = null;
        _successBarTitle    = null;
        _successBarSub      = null;
        _nextStepPreviewGo  = null;
        _mainInstructionTmp = null;
        _factTextTmp        = null;

        CleanStaleChildren();
        BuildHeader13();
        BuildMainContent13();
        BuildSnapTarget13();
        BuildFactCard13();
        BuildSuccessBar13();
        BuildFooter();

        SubscribeToSnapZone("RightDoor-snap");

        InstructionCardController ctrl = GetComponent<InstructionCardController>();
        if (ctrl != null)
            ctrl.ReplayAnimation();
    }

    private void BuildHeader13()
    {
        GameObject header = CreatePanel("Header", transform);
        AddVerticalLayout(header, new RectOffset(0, 0, 0, 0), 10, false);

        GameObject breadcrumb = CreateTextObject("Breadcrumb", header.transform,
            "STEP 12 OF 19  \u00B7  PORSCHE 911S ASSEMBLY",
            12, ColTaskLabel, FontStyles.Normal);
        breadcrumb.GetComponent<TextMeshProUGUI>().characterSpacing = 1.2f;

        GameObject badgeRow = CreatePanel("PhaseBadge", header.transform);
        AddHorizontalLayout(badgeRow, new RectOffset(12, 12, 7, 7), 6);
        AddImageToGameObject(badgeRow, ColPhase3Bg);
        AddLayoutElement(badgeRow, preferredHeight: 30f);
        CreateOverlayBorder("PhaseBadgeBorder", badgeRow.transform, ColPhase3Border);

        CreateTextObject("PhaseLabel", badgeRow.transform,
            "\u25CF  PHASE 3 \u2014 BODY PANELS & CLOSURES",
            12, ColPhase3TextDark, FontStyles.Bold);

        GameObject divider = CreatePanel("Divider", header.transform);
        AddImageToGameObject(divider, ColDivider);
        AddLayoutElement(divider, preferredHeight: 1f);
    }

    private void BuildMainContent13()
    {
        GameObject main = CreatePanel("MainContent", transform);
        AddVerticalLayout(main, new RectOffset(0, 0, 0, 0), 12, false);

        _taskLabel = CreateTextObject("TaskLabel", main.transform,
            "YOUR TASK", 12, ColTaskLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _taskLabel.characterSpacing = 1.2f;

        _mainInstructionTmp = CreateTextObject("MainInstruction", main.transform,
            "Now place the Right Door", 20, ColMainInstr, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _subtitle = CreateTextObject("Subtitle", main.transform,
            "Find it in the inventory \u2192 snap to the target below",
            14, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _description = CreateTextObject("Description", main.transform,
            "Hang the right door symmetrically to the left \u2014 upper hinge first. " +
            "The door seal groove must compress evenly all the way around the " +
            "aperture when the snap confirms.",
            13, ColDescription, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _description.lineSpacing = 6f;
    }

    private void BuildSnapTarget13()
    {
        GameObject snap = CreatePanel("SnapTarget", transform);
        AddHorizontalLayout(snap, new RectOffset(10, 10, 8, 8), 10);
        AddImageToGameObject(snap, ColPhase3SnapBg);

        _snapBorderImg = CreateOverlayBorder("SnapBorder", snap.transform, ColPhase3SnapBorder);

        GameObject snapLeft = CreatePanel("SnapLeft", snap.transform);
        AddVerticalLayout(snapLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(snapLeft, flexibleWidth: 1f);

        _snapLabelTmp = CreateTextObject("SnapLabel", snapLeft.transform,
            "SNAP TARGET", 11, ColPhase3TextDark, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _snapLabelTmp.characterSpacing = 1.2f;

        _snapValueTmp = CreateTextObject("SnapValue", snapLeft.transform,
            "right_door_snap", 13, ColPhase3TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _snapIndicator = CreateTextObject("SnapIndicator", snap.transform,
            "\u25CF", 22, ColPhase3TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _snapIndicator.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(_snapIndicator.gameObject, preferredWidth: 28f);

        _isPulsing      = true;
        _pulseCoroutine = StartCoroutine(PulseIndicator());
    }

    private void BuildFactCard13()
    {
        GameObject fact = CreatePanel("FactCard", transform);
        AddVerticalLayout(fact, new RectOffset(10, 10, 8, 8), 0, false);
        AddImageToGameObject(fact, ColPhase3SnapBg);

        CreateOverlayBorder("FactBorder", fact.transform, ColPhase3Border, leftOnly: true);

        _factTextTmp = CreateTextObject("FactText", fact.transform,
            "Equal door gaps on both sides ensure the aerodynamic seal \u2014 " +
            "even 2mm difference affects wind noise at speed.",
            12, ColPhase3TextDark, FontStyles.Italic)
            .GetComponent<TextMeshProUGUI>();
        _factTextTmp.lineSpacing = 6f;
    }

    private void BuildSuccessBar13()
    {
        _successBarGo = CreatePanel("SuccessBar", transform);
        AddVerticalLayout(_successBarGo, new RectOffset(12, 12, 10, 10), 4, false);
        AddImageToGameObject(_successBarGo, ColSuccessBg);
        CreateOverlayBorder("SuccessBarBorder", _successBarGo.transform, ColSuccessCardBd);

        _successBarTitle = CreateTextObject("SuccessTitle", _successBarGo.transform,
            _successData.SuccessBarTitle, 15, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _successBarSub = CreateTextObject("SuccessSub", _successBarGo.transform,
            _successData.SuccessBarSub, 12, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _successBarGo.SetActive(false);
    }

    private void LoadStep14()
    {
        _currentSnapZoneName = "trunk lid-snap";

        _successData = new StepSuccessData
        {
            SuccessBarTitle    = "Trunk Lid fitted!",
            SuccessBarSub      = "Engine bay is sealed.",
            NextStepLabel      = null,
            SuccessSubtitle    = "Trunk lid locked in \u2014 great work",
            SuccessPieceName   = "Trunk Lid Installed",
            SuccessSnapValue   = "trunk_snap \u2713",
            SuccessDescription = "Mount the engine lid on the rear hinges above the engine bay. " +
                                 "The rear lid must clear the engine cooling intake \u2014 " +
                                 "do not force the snap if resistance is felt.",
            SuccessFactText    = "The engine lid doubles as a cooling air duct \u2014 the gap size " +
                                 "between lid and body directly affects engine temperature."
        };

        if (_cardBgImg != null)
            _cardBgImg.color = ColBackground;

        Transform borderChild = transform.Find("Border");
        if (borderChild != null)
        {
            Image borderImg = borderChild.GetComponent<Image>();
            if (borderImg != null) borderImg.color = ColBorderDefault;
        }

        _successTriggered = false;
        _isPulsing        = false;

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        UnsubscribeFromAllSnapZones();

        _successBarGo       = null;
        _successBarTitle    = null;
        _successBarSub      = null;
        _nextStepPreviewGo  = null;
        _mainInstructionTmp = null;
        _factTextTmp        = null;

        CleanStaleChildren();
        BuildHeader14();
        BuildMainContent14();
        BuildSnapTarget14();
        BuildFactCard14();
        BuildSuccessBar14();
        BuildFooter();

        SubscribeToSnapZone("trunk lid-snap");

        InstructionCardController ctrl = GetComponent<InstructionCardController>();
        if (ctrl != null)
            ctrl.ReplayAnimation();
    }

    private void BuildHeader14()
    {
        GameObject header = CreatePanel("Header", transform);
        AddVerticalLayout(header, new RectOffset(0, 0, 0, 0), 10, false);

        GameObject breadcrumb = CreateTextObject("Breadcrumb", header.transform,
            "STEP 13 OF 19  \u00B7  PORSCHE 911S ASSEMBLY",
            12, ColTaskLabel, FontStyles.Normal);
        breadcrumb.GetComponent<TextMeshProUGUI>().characterSpacing = 1.2f;

        GameObject badgeRow = CreatePanel("PhaseBadge", header.transform);
        AddHorizontalLayout(badgeRow, new RectOffset(12, 12, 7, 7), 6);
        AddImageToGameObject(badgeRow, ColPhase3Bg);
        AddLayoutElement(badgeRow, preferredHeight: 30f);
        CreateOverlayBorder("PhaseBadgeBorder", badgeRow.transform, ColPhase3Border);

        CreateTextObject("PhaseLabel", badgeRow.transform,
            "\u25CF  PHASE 3 \u2014 BODY PANELS & CLOSURES",
            12, ColPhase3TextDark, FontStyles.Bold);

        GameObject divider = CreatePanel("Divider", header.transform);
        AddImageToGameObject(divider, ColDivider);
        AddLayoutElement(divider, preferredHeight: 1f);
    }

    private void BuildMainContent14()
    {
        GameObject main = CreatePanel("MainContent", transform);
        AddVerticalLayout(main, new RectOffset(0, 0, 0, 0), 12, false);

        _taskLabel = CreateTextObject("TaskLabel", main.transform,
            "YOUR TASK", 12, ColTaskLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _taskLabel.characterSpacing = 1.2f;

        _mainInstructionTmp = CreateTextObject("MainInstruction", main.transform,
            "Now place the Trunk Lid", 20, ColMainInstr, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _subtitle = CreateTextObject("Subtitle", main.transform,
            "Find it in the inventory \u2192 snap to the target below",
            14, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _description = CreateTextObject("Description", main.transform,
            "Mount the engine lid on the rear hinges above the engine bay. " +
            "The rear lid must clear the engine cooling intake \u2014 " +
            "do not force the snap if resistance is felt.",
            13, ColDescription, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _description.lineSpacing = 6f;
    }

    private void BuildSnapTarget14()
    {
        GameObject snap = CreatePanel("SnapTarget", transform);
        AddHorizontalLayout(snap, new RectOffset(10, 10, 8, 8), 10);
        AddImageToGameObject(snap, ColPhase3SnapBg);

        _snapBorderImg = CreateOverlayBorder("SnapBorder", snap.transform, ColPhase3SnapBorder);

        GameObject snapLeft = CreatePanel("SnapLeft", snap.transform);
        AddVerticalLayout(snapLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(snapLeft, flexibleWidth: 1f);

        _snapLabelTmp = CreateTextObject("SnapLabel", snapLeft.transform,
            "SNAP TARGET", 11, ColPhase3TextDark, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _snapLabelTmp.characterSpacing = 1.2f;

        _snapValueTmp = CreateTextObject("SnapValue", snapLeft.transform,
            "trunk_snap", 13, ColPhase3TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _snapIndicator = CreateTextObject("SnapIndicator", snap.transform,
            "\u25CF", 22, ColPhase3TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _snapIndicator.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(_snapIndicator.gameObject, preferredWidth: 28f);

        _isPulsing      = true;
        _pulseCoroutine = StartCoroutine(PulseIndicator());
    }

    private void BuildFactCard14()
    {
        GameObject fact = CreatePanel("FactCard", transform);
        AddVerticalLayout(fact, new RectOffset(10, 10, 8, 8), 0, false);
        AddImageToGameObject(fact, ColPhase3SnapBg);

        CreateOverlayBorder("FactBorder", fact.transform, ColPhase3Border, leftOnly: true);

        _factTextTmp = CreateTextObject("FactText", fact.transform,
            "The engine lid doubles as a cooling air duct \u2014 the gap size " +
            "between lid and body directly affects engine temperature.",
            12, ColPhase3TextDark, FontStyles.Italic)
            .GetComponent<TextMeshProUGUI>();
        _factTextTmp.lineSpacing = 6f;
    }

    private void BuildSuccessBar14()
    {
        _successBarGo = CreatePanel("SuccessBar", transform);
        AddVerticalLayout(_successBarGo, new RectOffset(12, 12, 10, 10), 4, false);
        AddImageToGameObject(_successBarGo, ColSuccessBg);
        CreateOverlayBorder("SuccessBarBorder", _successBarGo.transform, ColSuccessCardBd);

        _successBarTitle = CreateTextObject("SuccessTitle", _successBarGo.transform,
            _successData.SuccessBarTitle, 15, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _successBarSub = CreateTextObject("SuccessSub", _successBarGo.transform,
            _successData.SuccessBarSub, 12, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _successBarGo.SetActive(false);
    }

    private void LoadStep15()
    {
        _currentSnapZoneName = "spoiler-snap";

        _successData = new StepSuccessData
        {
            SuccessBarTitle    = "Spoiler fitted!",
            SuccessBarSub      = "Duck-tail is locked in.",
            NextStepLabel      = null,
            SuccessSubtitle    = "Spoiler locked in \u2014 great work",
            SuccessPieceName   = "Spoiler Installed",
            SuccessSnapValue   = "spoiler_snap \u2713",
            SuccessDescription = "Attach the rear duck-tail spoiler to the engine lid trailing edge. " +
                                 "Four mounting bolts pattern \u2014 two inboard, two outboard. " +
                                 "The spoiler angle is fixed \u2014 no adjustment needed.",
            SuccessFactText    = "The duck-tail generates rear downforce that stabilizes the " +
                                 "rear-heavy 911 at high speed \u2014 form meets function."
        };

        if (_cardBgImg != null)
            _cardBgImg.color = ColBackground;

        Transform borderChild = transform.Find("Border");
        if (borderChild != null)
        {
            Image borderImg = borderChild.GetComponent<Image>();
            if (borderImg != null) borderImg.color = ColBorderDefault;
        }

        _successTriggered = false;
        _isPulsing        = false;

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        UnsubscribeFromAllSnapZones();

        _successBarGo       = null;
        _successBarTitle    = null;
        _successBarSub      = null;
        _nextStepPreviewGo  = null;
        _mainInstructionTmp = null;
        _factTextTmp        = null;

        CleanStaleChildren();
        BuildHeader15();
        BuildMainContent15();
        BuildSnapTarget15();
        BuildFactCard15();
        BuildSuccessBar15();
        BuildFooter();

        SubscribeToSnapZone("spoiler-snap");

        InstructionCardController ctrl = GetComponent<InstructionCardController>();
        if (ctrl != null)
            ctrl.ReplayAnimation();
    }

    private void BuildHeader15()
    {
        GameObject header = CreatePanel("Header", transform);
        AddVerticalLayout(header, new RectOffset(0, 0, 0, 0), 10, false);

        GameObject breadcrumb = CreateTextObject("Breadcrumb", header.transform,
            "STEP 14 OF 19  \u00B7  PORSCHE 911S ASSEMBLY",
            12, ColTaskLabel, FontStyles.Normal);
        breadcrumb.GetComponent<TextMeshProUGUI>().characterSpacing = 1.2f;

        GameObject badgeRow = CreatePanel("PhaseBadge", header.transform);
        AddHorizontalLayout(badgeRow, new RectOffset(12, 12, 7, 7), 6);
        AddImageToGameObject(badgeRow, ColPhase3Bg);
        AddLayoutElement(badgeRow, preferredHeight: 30f);
        CreateOverlayBorder("PhaseBadgeBorder", badgeRow.transform, ColPhase3Border);

        CreateTextObject("PhaseLabel", badgeRow.transform,
            "\u25CF  PHASE 3 \u2014 BODY PANELS & CLOSURES",
            12, ColPhase3TextDark, FontStyles.Bold);

        GameObject divider = CreatePanel("Divider", header.transform);
        AddImageToGameObject(divider, ColDivider);
        AddLayoutElement(divider, preferredHeight: 1f);
    }

    private void BuildMainContent15()
    {
        GameObject main = CreatePanel("MainContent", transform);
        AddVerticalLayout(main, new RectOffset(0, 0, 0, 0), 12, false);

        _taskLabel = CreateTextObject("TaskLabel", main.transform,
            "YOUR TASK", 12, ColTaskLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _taskLabel.characterSpacing = 1.2f;

        _mainInstructionTmp = CreateTextObject("MainInstruction", main.transform,
            "Now place the Spoiler", 20, ColMainInstr, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _subtitle = CreateTextObject("Subtitle", main.transform,
            "Find it in the inventory \u2192 snap to the target below",
            14, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _description = CreateTextObject("Description", main.transform,
            "Attach the rear duck-tail spoiler to the engine lid trailing edge. " +
            "Four mounting bolts pattern \u2014 two inboard, two outboard. " +
            "The spoiler angle is fixed \u2014 no adjustment needed.",
            13, ColDescription, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _description.lineSpacing = 6f;
    }

    private void BuildSnapTarget15()
    {
        GameObject snap = CreatePanel("SnapTarget", transform);
        AddHorizontalLayout(snap, new RectOffset(10, 10, 8, 8), 10);
        AddImageToGameObject(snap, ColPhase3SnapBg);

        _snapBorderImg = CreateOverlayBorder("SnapBorder", snap.transform, ColPhase3SnapBorder);

        GameObject snapLeft = CreatePanel("SnapLeft", snap.transform);
        AddVerticalLayout(snapLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(snapLeft, flexibleWidth: 1f);

        _snapLabelTmp = CreateTextObject("SnapLabel", snapLeft.transform,
            "SNAP TARGET", 11, ColPhase3TextDark, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _snapLabelTmp.characterSpacing = 1.2f;

        _snapValueTmp = CreateTextObject("SnapValue", snapLeft.transform,
            "spoiler_snap", 13, ColPhase3TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _snapIndicator = CreateTextObject("SnapIndicator", snap.transform,
            "\u25CF", 22, ColPhase3TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _snapIndicator.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(_snapIndicator.gameObject, preferredWidth: 28f);

        _isPulsing      = true;
        _pulseCoroutine = StartCoroutine(PulseIndicator());
    }

    private void BuildFactCard15()
    {
        GameObject fact = CreatePanel("FactCard", transform);
        AddVerticalLayout(fact, new RectOffset(10, 10, 8, 8), 0, false);
        AddImageToGameObject(fact, ColPhase3SnapBg);

        CreateOverlayBorder("FactBorder", fact.transform, ColPhase3Border, leftOnly: true);

        _factTextTmp = CreateTextObject("FactText", fact.transform,
            "The duck-tail generates rear downforce that stabilizes the " +
            "rear-heavy 911 at high speed \u2014 form meets function.",
            12, ColPhase3TextDark, FontStyles.Italic)
            .GetComponent<TextMeshProUGUI>();
        _factTextTmp.lineSpacing = 6f;
    }

    private void BuildSuccessBar15()
    {
        _successBarGo = CreatePanel("SuccessBar", transform);
        AddVerticalLayout(_successBarGo, new RectOffset(12, 12, 10, 10), 4, false);
        AddImageToGameObject(_successBarGo, ColSuccessBg);
        CreateOverlayBorder("SuccessBarBorder", _successBarGo.transform, ColSuccessCardBd);

        _successBarTitle = CreateTextObject("SuccessTitle", _successBarGo.transform,
            _successData.SuccessBarTitle, 15, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _successBarSub = CreateTextObject("SuccessSub", _successBarGo.transform,
            _successData.SuccessBarSub, 12, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _successBarGo.SetActive(false);
    }

    private void LoadStep16()
    {
        _currentSnapZoneName = "LeftHeadLight-snap";

        _successData = new StepSuccessData
        {
            SuccessBarTitle    = "Left Headlight fitted!",
            SuccessBarSub      = "Front lighting is taking shape.",
            NextStepLabel      = null,
            SuccessSubtitle    = "Headlight locked in \u2014 great work",
            SuccessPieceName   = "Left Headlight Installed",
            SuccessSnapValue   = "left_headlight_snap \u2713",
            SuccessDescription = "Press the left headlight unit into the front-left quarter panel aperture. " +
                                 "Rotate clockwise 15 degrees to lock. " +
                                 "The wiring connector is at the 7 o\u2019clock position inside the bucket.",
            SuccessFactText    = "The round headlights are the most recognized feature of the original 911 \u2014 " +
                                 "unchanged in proportion for 50 years."
        };

        if (_cardBgImg != null)
            _cardBgImg.color = ColBackground;

        Transform borderChild = transform.Find("Border");
        if (borderChild != null)
        {
            Image borderImg = borderChild.GetComponent<Image>();
            if (borderImg != null) borderImg.color = ColBorderDefault;
        }

        _successTriggered = false;
        _isPulsing        = false;

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        UnsubscribeFromAllSnapZones();

        _successBarGo       = null;
        _successBarTitle    = null;
        _successBarSub      = null;
        _nextStepPreviewGo  = null;
        _mainInstructionTmp = null;
        _factTextTmp        = null;

        CleanStaleChildren();
        BuildHeader16();
        BuildMainContent16();
        BuildSnapTarget16();
        BuildFactCard16();
        BuildSuccessBar16();
        BuildFooter();

        SubscribeToSnapZone("LeftHeadLight-snap");

        InstructionCardController ctrl = GetComponent<InstructionCardController>();
        if (ctrl != null)
            ctrl.ReplayAnimation();
    }

    private void BuildHeader16()
    {
        GameObject header = CreatePanel("Header", transform);
        AddVerticalLayout(header, new RectOffset(0, 0, 0, 0), 10, false);

        GameObject breadcrumb = CreateTextObject("Breadcrumb", header.transform,
            "STEP 15 OF 19  \u00B7  PORSCHE 911S ASSEMBLY",
            12, ColTaskLabel, FontStyles.Normal);
        breadcrumb.GetComponent<TextMeshProUGUI>().characterSpacing = 1.2f;

        GameObject badgeRow = CreatePanel("PhaseBadge", header.transform);
        AddHorizontalLayout(badgeRow, new RectOffset(12, 12, 7, 7), 6);
        AddImageToGameObject(badgeRow, ColPhase4Bg);
        AddLayoutElement(badgeRow, preferredHeight: 30f);
        CreateOverlayBorder("PhaseBadgeBorder", badgeRow.transform, ColPhase4Border);

        CreateTextObject("PhaseLabel", badgeRow.transform,
            "\u25CF  PHASE 4 \u2014 LIGHTING",
            12, ColPhase4TextDark, FontStyles.Bold);

        GameObject divider = CreatePanel("Divider", header.transform);
        AddImageToGameObject(divider, ColDivider);
        AddLayoutElement(divider, preferredHeight: 1f);
    }

    private void BuildMainContent16()
    {
        GameObject main = CreatePanel("MainContent", transform);
        AddVerticalLayout(main, new RectOffset(0, 0, 0, 0), 12, false);

        _taskLabel = CreateTextObject("TaskLabel", main.transform,
            "YOUR TASK", 12, ColTaskLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _taskLabel.characterSpacing = 1.2f;

        _mainInstructionTmp = CreateTextObject("MainInstruction", main.transform,
            "Now place the Left Headlight", 20, ColMainInstr, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _subtitle = CreateTextObject("Subtitle", main.transform,
            "Find it in the inventory \u2192 snap to the target below",
            14, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _description = CreateTextObject("Description", main.transform,
            "Press the left headlight unit into the front-left quarter panel aperture. " +
            "Rotate clockwise 15 degrees to lock. " +
            "The wiring connector is at the 7 o\u2019clock position inside the bucket.",
            13, ColDescription, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _description.lineSpacing = 6f;
    }

    private void BuildSnapTarget16()
    {
        GameObject snap = CreatePanel("SnapTarget", transform);
        AddHorizontalLayout(snap, new RectOffset(10, 10, 8, 8), 10);
        AddImageToGameObject(snap, ColPhase4SnapBg);

        _snapBorderImg = CreateOverlayBorder("SnapBorder", snap.transform, ColPhase4SnapBorder);

        GameObject snapLeft = CreatePanel("SnapLeft", snap.transform);
        AddVerticalLayout(snapLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(snapLeft, flexibleWidth: 1f);

        _snapLabelTmp = CreateTextObject("SnapLabel", snapLeft.transform,
            "SNAP TARGET", 11, ColPhase4TextDark, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _snapLabelTmp.characterSpacing = 1.2f;

        _snapValueTmp = CreateTextObject("SnapValue", snapLeft.transform,
            "left_headlight_snap", 13, ColPhase4TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _snapIndicator = CreateTextObject("SnapIndicator", snap.transform,
            "\u25CF", 22, ColPhase4TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _snapIndicator.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(_snapIndicator.gameObject, preferredWidth: 28f);

        _isPulsing      = true;
        _pulseCoroutine = StartCoroutine(PulseIndicator());
    }

    private void BuildFactCard16()
    {
        GameObject fact = CreatePanel("FactCard", transform);
        AddVerticalLayout(fact, new RectOffset(10, 10, 8, 8), 0, false);
        AddImageToGameObject(fact, ColPhase4SnapBg);

        CreateOverlayBorder("FactBorder", fact.transform, ColPhase4Border, leftOnly: true);

        _factTextTmp = CreateTextObject("FactText", fact.transform,
            "The round headlights are the most recognized feature of the original 911 \u2014 " +
            "unchanged in proportion for 50 years.",
            12, ColPhase4TextDark, FontStyles.Italic)
            .GetComponent<TextMeshProUGUI>();
        _factTextTmp.lineSpacing = 6f;
    }

    private void BuildSuccessBar16()
    {
        _successBarGo = CreatePanel("SuccessBar", transform);
        AddVerticalLayout(_successBarGo, new RectOffset(12, 12, 10, 10), 4, false);
        AddImageToGameObject(_successBarGo, ColSuccessBg);
        CreateOverlayBorder("SuccessBarBorder", _successBarGo.transform, ColSuccessCardBd);

        _successBarTitle = CreateTextObject("SuccessTitle", _successBarGo.transform,
            _successData.SuccessBarTitle, 15, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _successBarSub = CreateTextObject("SuccessSub", _successBarGo.transform,
            _successData.SuccessBarSub, 12, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _successBarGo.SetActive(false);
    }

    private void LoadStep17()
    {
        _currentSnapZoneName = "RightHeadLight-snap";

        _successData = new StepSuccessData
        {
            SuccessBarTitle    = "Right Headlight fitted!",
            SuccessBarSub      = "Both headlights are now in place.",
            NextStepLabel      = null,
            SuccessSubtitle    = "Headlight locked in \u2014 great work",
            SuccessPieceName   = "Right Headlight Installed",
            SuccessSnapValue   = "right_headlight_snap \u2713",
            SuccessDescription = "Press the right headlight unit into the right aperture. " +
                                 "Same clockwise rotation to lock. " +
                                 "Both headlights must sit at identical height \u2014 " +
                                 "check from directly in front before confirming.",
            SuccessFactText    = "Symmetric headlight height is a legal requirement and a quality benchmark \u2014 " +
                                 "Porsche measured this to fractions of a millimeter."
        };

        if (_cardBgImg != null)
            _cardBgImg.color = ColBackground;

        Transform borderChild = transform.Find("Border");
        if (borderChild != null)
        {
            Image borderImg = borderChild.GetComponent<Image>();
            if (borderImg != null) borderImg.color = ColBorderDefault;
        }

        _successTriggered = false;
        _isPulsing        = false;

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        UnsubscribeFromAllSnapZones();

        _successBarGo       = null;
        _successBarTitle    = null;
        _successBarSub      = null;
        _nextStepPreviewGo  = null;
        _mainInstructionTmp = null;
        _factTextTmp        = null;

        CleanStaleChildren();
        BuildHeader17();
        BuildMainContent17();
        BuildSnapTarget17();
        BuildFactCard17();
        BuildSuccessBar17();
        BuildFooter();

        SubscribeToSnapZone("RightHeadLight-snap");

        InstructionCardController ctrl = GetComponent<InstructionCardController>();
        if (ctrl != null)
            ctrl.ReplayAnimation();
    }

    private void BuildHeader17()
    {
        GameObject header = CreatePanel("Header", transform);
        AddVerticalLayout(header, new RectOffset(0, 0, 0, 0), 10, false);

        GameObject breadcrumb = CreateTextObject("Breadcrumb", header.transform,
            "STEP 16 OF 19  \u00B7  PORSCHE 911S ASSEMBLY",
            12, ColTaskLabel, FontStyles.Normal);
        breadcrumb.GetComponent<TextMeshProUGUI>().characterSpacing = 1.2f;

        GameObject badgeRow = CreatePanel("PhaseBadge", header.transform);
        AddHorizontalLayout(badgeRow, new RectOffset(12, 12, 7, 7), 6);
        AddImageToGameObject(badgeRow, ColPhase4Bg);
        AddLayoutElement(badgeRow, preferredHeight: 30f);
        CreateOverlayBorder("PhaseBadgeBorder", badgeRow.transform, ColPhase4Border);

        CreateTextObject("PhaseLabel", badgeRow.transform,
            "\u25CF  PHASE 4 \u2014 LIGHTING",
            12, ColPhase4TextDark, FontStyles.Bold);

        GameObject divider = CreatePanel("Divider", header.transform);
        AddImageToGameObject(divider, ColDivider);
        AddLayoutElement(divider, preferredHeight: 1f);
    }

    private void BuildMainContent17()
    {
        GameObject main = CreatePanel("MainContent", transform);
        AddVerticalLayout(main, new RectOffset(0, 0, 0, 0), 12, false);

        _taskLabel = CreateTextObject("TaskLabel", main.transform,
            "YOUR TASK", 12, ColTaskLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _taskLabel.characterSpacing = 1.2f;

        _mainInstructionTmp = CreateTextObject("MainInstruction", main.transform,
            "Now place the Right Headlight", 20, ColMainInstr, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _subtitle = CreateTextObject("Subtitle", main.transform,
            "Find it in the inventory \u2192 snap to the target below",
            14, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _description = CreateTextObject("Description", main.transform,
            "Press the right headlight unit into the right aperture. " +
            "Same clockwise rotation to lock. " +
            "Both headlights must sit at identical height \u2014 " +
            "check from directly in front before confirming.",
            13, ColDescription, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _description.lineSpacing = 6f;
    }

    private void BuildSnapTarget17()
    {
        GameObject snap = CreatePanel("SnapTarget", transform);
        AddHorizontalLayout(snap, new RectOffset(10, 10, 8, 8), 10);
        AddImageToGameObject(snap, ColPhase4SnapBg);

        _snapBorderImg = CreateOverlayBorder("SnapBorder", snap.transform, ColPhase4SnapBorder);

        GameObject snapLeft = CreatePanel("SnapLeft", snap.transform);
        AddVerticalLayout(snapLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(snapLeft, flexibleWidth: 1f);

        _snapLabelTmp = CreateTextObject("SnapLabel", snapLeft.transform,
            "SNAP TARGET", 11, ColPhase4TextDark, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _snapLabelTmp.characterSpacing = 1.2f;

        _snapValueTmp = CreateTextObject("SnapValue", snapLeft.transform,
            "right_headlight_snap", 13, ColPhase4TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _snapIndicator = CreateTextObject("SnapIndicator", snap.transform,
            "\u25CF", 22, ColPhase4TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _snapIndicator.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(_snapIndicator.gameObject, preferredWidth: 28f);

        _isPulsing      = true;
        _pulseCoroutine = StartCoroutine(PulseIndicator());
    }

    private void BuildFactCard17()
    {
        GameObject fact = CreatePanel("FactCard", transform);
        AddVerticalLayout(fact, new RectOffset(10, 10, 8, 8), 0, false);
        AddImageToGameObject(fact, ColPhase4SnapBg);

        CreateOverlayBorder("FactBorder", fact.transform, ColPhase4Border, leftOnly: true);

        _factTextTmp = CreateTextObject("FactText", fact.transform,
            "Symmetric headlight height is a legal requirement and a quality benchmark \u2014 " +
            "Porsche measured this to fractions of a millimeter.",
            12, ColPhase4TextDark, FontStyles.Italic)
            .GetComponent<TextMeshProUGUI>();
        _factTextTmp.lineSpacing = 6f;
    }

    private void BuildSuccessBar17()
    {
        _successBarGo = CreatePanel("SuccessBar", transform);
        AddVerticalLayout(_successBarGo, new RectOffset(12, 12, 10, 10), 4, false);
        AddImageToGameObject(_successBarGo, ColSuccessBg);
        CreateOverlayBorder("SuccessBarBorder", _successBarGo.transform, ColSuccessCardBd);

        _successBarTitle = CreateTextObject("SuccessTitle", _successBarGo.transform,
            _successData.SuccessBarTitle, 15, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _successBarSub = CreateTextObject("SuccessSub", _successBarGo.transform,
            _successData.SuccessBarSub, 12, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _successBarGo.SetActive(false);
    }

    private void LoadStep18()
    {
        _currentSnapZoneName = "LeftTaillights-snap";

        _successData = new StepSuccessData
        {
            SuccessBarTitle    = "Left Taillight fitted!",
            SuccessBarSub      = "Rear lighting is coming together.",
            NextStepLabel      = null,
            SuccessSubtitle    = "Taillight locked in \u2014 great work",
            SuccessPieceName   = "Left Taillight Installed",
            SuccessSnapValue   = "LeftTaillights-snap \u2713",
            SuccessDescription = "Clip the left taillight lens assembly into the rear-left quarter panel. " +
                                 "Two clips top, two clips bottom. " +
                                 "The reverse light is at the bottom of the cluster \u2014 " +
                                 "verify orientation before snapping.",
            SuccessFactText    = "The horizontal taillight cluster is one of Porsche\u2019s most protected " +
                                 "design elements \u2014 no other car uses this layout."
        };

        if (_cardBgImg != null)
            _cardBgImg.color = ColBackground;

        Transform borderChild = transform.Find("Border");
        if (borderChild != null)
        {
            Image borderImg = borderChild.GetComponent<Image>();
            if (borderImg != null) borderImg.color = ColBorderDefault;
        }

        _successTriggered = false;
        _isPulsing        = false;

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        UnsubscribeFromAllSnapZones();

        _successBarGo       = null;
        _successBarTitle    = null;
        _successBarSub      = null;
        _nextStepPreviewGo  = null;
        _mainInstructionTmp = null;
        _factTextTmp        = null;

        CleanStaleChildren();
        BuildHeader18();
        BuildMainContent18();
        BuildSnapTarget18();
        BuildFactCard18();
        BuildSuccessBar18();
        BuildFooter();

        SubscribeToSnapZone("LeftTaillights-snap");

        InstructionCardController ctrl = GetComponent<InstructionCardController>();
        if (ctrl != null)
            ctrl.ReplayAnimation();
    }

    private void BuildHeader18()
    {
        GameObject header = CreatePanel("Header", transform);
        AddVerticalLayout(header, new RectOffset(0, 0, 0, 0), 10, false);

        GameObject breadcrumb = CreateTextObject("Breadcrumb", header.transform,
            "STEP 17 OF 19  \u00B7  PORSCHE 911S ASSEMBLY",
            12, ColTaskLabel, FontStyles.Normal);
        breadcrumb.GetComponent<TextMeshProUGUI>().characterSpacing = 1.2f;

        GameObject badgeRow = CreatePanel("PhaseBadge", header.transform);
        AddHorizontalLayout(badgeRow, new RectOffset(12, 12, 7, 7), 6);
        AddImageToGameObject(badgeRow, ColPhase4Bg);
        AddLayoutElement(badgeRow, preferredHeight: 30f);
        CreateOverlayBorder("PhaseBadgeBorder", badgeRow.transform, ColPhase4Border);

        CreateTextObject("PhaseLabel", badgeRow.transform,
            "\u25CF  PHASE 4 \u2014 LIGHTING",
            12, ColPhase4TextDark, FontStyles.Bold);

        GameObject divider = CreatePanel("Divider", header.transform);
        AddImageToGameObject(divider, ColDivider);
        AddLayoutElement(divider, preferredHeight: 1f);
    }

    private void BuildMainContent18()
    {
        GameObject main = CreatePanel("MainContent", transform);
        AddVerticalLayout(main, new RectOffset(0, 0, 0, 0), 12, false);

        _taskLabel = CreateTextObject("TaskLabel", main.transform,
            "YOUR TASK", 12, ColTaskLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _taskLabel.characterSpacing = 1.2f;

        _mainInstructionTmp = CreateTextObject("MainInstruction", main.transform,
            "Now place the Left Taillight", 20, ColMainInstr, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _subtitle = CreateTextObject("Subtitle", main.transform,
            "Find it in the inventory \u2192 snap to the target below",
            14, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _description = CreateTextObject("Description", main.transform,
            "Clip the left taillight lens assembly into the rear-left quarter panel. " +
            "Two clips top, two clips bottom. " +
            "The reverse light is at the bottom of the cluster \u2014 " +
            "verify orientation before snapping.",
            13, ColDescription, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _description.lineSpacing = 6f;
    }

    private void BuildSnapTarget18()
    {
        GameObject snap = CreatePanel("SnapTarget", transform);
        AddHorizontalLayout(snap, new RectOffset(10, 10, 8, 8), 10);
        AddImageToGameObject(snap, ColPhase4SnapBg);

        _snapBorderImg = CreateOverlayBorder("SnapBorder", snap.transform, ColPhase4SnapBorder);

        GameObject snapLeft = CreatePanel("SnapLeft", snap.transform);
        AddVerticalLayout(snapLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(snapLeft, flexibleWidth: 1f);

        _snapLabelTmp = CreateTextObject("SnapLabel", snapLeft.transform,
            "SNAP TARGET", 11, ColPhase4TextDark, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _snapLabelTmp.characterSpacing = 1.2f;

        _snapValueTmp = CreateTextObject("SnapValue", snapLeft.transform,
            "left_taillight_snap", 13, ColPhase4TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _snapIndicator = CreateTextObject("SnapIndicator", snap.transform,
            "\u25CF", 22, ColPhase4TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _snapIndicator.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(_snapIndicator.gameObject, preferredWidth: 28f);

        _isPulsing      = true;
        _pulseCoroutine = StartCoroutine(PulseIndicator());
    }

    private void BuildFactCard18()
    {
        GameObject fact = CreatePanel("FactCard", transform);
        AddVerticalLayout(fact, new RectOffset(10, 10, 8, 8), 0, false);
        AddImageToGameObject(fact, ColPhase4SnapBg);

        CreateOverlayBorder("FactBorder", fact.transform, ColPhase4Border, leftOnly: true);

        _factTextTmp = CreateTextObject("FactText", fact.transform,
            "The horizontal taillight cluster is one of Porsche\u2019s most protected " +
            "design elements \u2014 no other car uses this layout.",
            12, ColPhase4TextDark, FontStyles.Italic)
            .GetComponent<TextMeshProUGUI>();
        _factTextTmp.lineSpacing = 6f;
    }

    private void BuildSuccessBar18()
    {
        _successBarGo = CreatePanel("SuccessBar", transform);
        AddVerticalLayout(_successBarGo, new RectOffset(12, 12, 10, 10), 4, false);
        AddImageToGameObject(_successBarGo, ColSuccessBg);
        CreateOverlayBorder("SuccessBarBorder", _successBarGo.transform, ColSuccessCardBd);

        _successBarTitle = CreateTextObject("SuccessTitle", _successBarGo.transform,
            _successData.SuccessBarTitle, 15, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _successBarSub = CreateTextObject("SuccessSub", _successBarGo.transform,
            _successData.SuccessBarSub, 12, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _successBarGo.SetActive(false);
    }

    private void LoadStep19()
    {
        _currentSnapZoneName = "RightTaillights-snap";

        _successData = new StepSuccessData
        {
            SuccessBarTitle    = "Right Taillight fitted!",
            SuccessBarSub      = "Both taillights are now in place.",
            NextStepLabel      = null,
            SuccessSubtitle    = "Taillight locked in \u2014 great work",
            SuccessPieceName   = "Right Taillight Installed",
            SuccessSnapValue   = "RightTaillights-snap \u2713",
            SuccessDescription = "Clip the right taillight symmetrically to the left. " +
                                 "The central chrome trim strip bridges both clusters \u2014 " +
                                 "it must align perfectly between left and right units when both are in place.",
            SuccessFactText    = "When the full-width light bar is complete, the 911S is immediately " +
                                 "recognizable \u2014 a design still referenced today."
        };

        if (_cardBgImg != null)
            _cardBgImg.color = ColBackground;

        Transform borderChild = transform.Find("Border");
        if (borderChild != null)
        {
            Image borderImg = borderChild.GetComponent<Image>();
            if (borderImg != null) borderImg.color = ColBorderDefault;
        }

        _successTriggered = false;
        _isPulsing        = false;

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        UnsubscribeFromAllSnapZones();

        _successBarGo       = null;
        _successBarTitle    = null;
        _successBarSub      = null;
        _nextStepPreviewGo  = null;
        _mainInstructionTmp = null;
        _factTextTmp        = null;

        CleanStaleChildren();
        BuildHeader19();
        BuildMainContent19();
        BuildSnapTarget19();
        BuildFactCard19();
        BuildSuccessBar19();
        BuildFooter();

        SubscribeToSnapZone("RightTaillights-snap");

        InstructionCardController ctrl = GetComponent<InstructionCardController>();
        if (ctrl != null)
            ctrl.ReplayAnimation();
    }

    private void BuildHeader19()
    {
        GameObject header = CreatePanel("Header", transform);
        AddVerticalLayout(header, new RectOffset(0, 0, 0, 0), 10, false);

        GameObject breadcrumb = CreateTextObject("Breadcrumb", header.transform,
            "STEP 18 OF 19  \u00B7  PORSCHE 911S ASSEMBLY",
            12, ColTaskLabel, FontStyles.Normal);
        breadcrumb.GetComponent<TextMeshProUGUI>().characterSpacing = 1.2f;

        GameObject badgeRow = CreatePanel("PhaseBadge", header.transform);
        AddHorizontalLayout(badgeRow, new RectOffset(12, 12, 7, 7), 6);
        AddImageToGameObject(badgeRow, ColPhase4Bg);
        AddLayoutElement(badgeRow, preferredHeight: 30f);
        CreateOverlayBorder("PhaseBadgeBorder", badgeRow.transform, ColPhase4Border);

        CreateTextObject("PhaseLabel", badgeRow.transform,
            "\u25CF  PHASE 4 \u2014 LIGHTING",
            12, ColPhase4TextDark, FontStyles.Bold);

        GameObject divider = CreatePanel("Divider", header.transform);
        AddImageToGameObject(divider, ColDivider);
        AddLayoutElement(divider, preferredHeight: 1f);
    }

    private void BuildMainContent19()
    {
        GameObject main = CreatePanel("MainContent", transform);
        AddVerticalLayout(main, new RectOffset(0, 0, 0, 0), 12, false);

        _taskLabel = CreateTextObject("TaskLabel", main.transform,
            "YOUR TASK", 12, ColTaskLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _taskLabel.characterSpacing = 1.2f;

        _mainInstructionTmp = CreateTextObject("MainInstruction", main.transform,
            "Now place the Right Taillight", 20, ColMainInstr, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _subtitle = CreateTextObject("Subtitle", main.transform,
            "Find it in the inventory \u2192 snap to the target below",
            14, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _description = CreateTextObject("Description", main.transform,
            "Clip the right taillight symmetrically to the left. " +
            "The central chrome trim strip bridges both clusters \u2014 " +
            "it must align perfectly between left and right units when both are in place.",
            13, ColDescription, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _description.lineSpacing = 6f;
    }

    private void BuildSnapTarget19()
    {
        GameObject snap = CreatePanel("SnapTarget", transform);
        AddHorizontalLayout(snap, new RectOffset(10, 10, 8, 8), 10);
        AddImageToGameObject(snap, ColPhase4SnapBg);

        _snapBorderImg = CreateOverlayBorder("SnapBorder", snap.transform, ColPhase4SnapBorder);

        GameObject snapLeft = CreatePanel("SnapLeft", snap.transform);
        AddVerticalLayout(snapLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(snapLeft, flexibleWidth: 1f);

        _snapLabelTmp = CreateTextObject("SnapLabel", snapLeft.transform,
            "SNAP TARGET", 11, ColPhase4TextDark, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _snapLabelTmp.characterSpacing = 1.2f;

        _snapValueTmp = CreateTextObject("SnapValue", snapLeft.transform,
            "right_taillight_snap", 13, ColPhase4TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _snapIndicator = CreateTextObject("SnapIndicator", snap.transform,
            "\u25CF", 22, ColPhase4TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _snapIndicator.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(_snapIndicator.gameObject, preferredWidth: 28f);

        _isPulsing      = true;
        _pulseCoroutine = StartCoroutine(PulseIndicator());
    }

    private void BuildFactCard19()
    {
        GameObject fact = CreatePanel("FactCard", transform);
        AddVerticalLayout(fact, new RectOffset(10, 10, 8, 8), 0, false);
        AddImageToGameObject(fact, ColPhase4SnapBg);

        CreateOverlayBorder("FactBorder", fact.transform, ColPhase4Border, leftOnly: true);

        _factTextTmp = CreateTextObject("FactText", fact.transform,
            "When the full-width light bar is complete, the 911S is immediately " +
            "recognizable \u2014 a design still referenced today.",
            12, ColPhase4TextDark, FontStyles.Italic)
            .GetComponent<TextMeshProUGUI>();
        _factTextTmp.lineSpacing = 6f;
    }

    private void BuildSuccessBar19()
    {
        _successBarGo = CreatePanel("SuccessBar", transform);
        AddVerticalLayout(_successBarGo, new RectOffset(12, 12, 10, 10), 4, false);
        AddImageToGameObject(_successBarGo, ColSuccessBg);
        CreateOverlayBorder("SuccessBarBorder", _successBarGo.transform, ColSuccessCardBd);

        _successBarTitle = CreateTextObject("SuccessTitle", _successBarGo.transform,
            _successData.SuccessBarTitle, 15, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _successBarSub = CreateTextObject("SuccessSub", _successBarGo.transform,
            _successData.SuccessBarSub, 12, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _successBarGo.SetActive(false);
    }

    private void LoadStep20()
    {
        _currentSnapZoneName = "differanciel-snap";

        _successData = new StepSuccessData
        {
            SuccessBarTitle    = "Last piece \u2014 snap to complete the build",
            SuccessBarSub      = "Assembly complete \u2014 the 911S is ready.",
            NextStepLabel      = null,
            SuccessSubtitle    = "Final piece in place \u2014 outstanding work",
            SuccessPieceName   = "Differential Installed",
            SuccessSnapValue   = "diff_snap \u2713",
            SuccessDescription = "The drive flanges are locked into both axle shafts. " +
                                 "The 911S drivetrain is complete.",
            SuccessFactText    = "The open differential allows each rear wheel to spin at " +
                                 "different speeds \u2014 essential for clean cornering and " +
                                 "traction on the 911S."
        };

        if (_cardBgImg != null)
            _cardBgImg.color = ColBackground;

        Transform borderChild = transform.Find("Border");
        if (borderChild != null)
        {
            Image borderImg = borderChild.GetComponent<Image>();
            if (borderImg != null) borderImg.color = ColBorderDefault;
        }

        _successTriggered = false;
        _isPulsing        = false;

        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        UnsubscribeFromAllSnapZones();

        _successBarGo       = null;
        _successBarTitle    = null;
        _successBarSub      = null;
        _nextStepPreviewGo  = null;
        _mainInstructionTmp = null;
        _factTextTmp        = null;

        CleanStaleChildren();
        BuildHeader20();
        BuildMainContent20();
        BuildSnapTarget20();
        BuildFactCard20();
        BuildSuccessBar20();
        BuildFooter();

        // Override the Continue button listener — this is the final step so
        // instead of advancing to a next step, unlock the Road button and hide the card.
        if (_continueButton != null)
        {
            _continueButton.onClick.RemoveAllListeners();
            _continueButton.onClick.AddListener(() =>
            {
                if (!_successTriggered) return;

                if (readyForTheRoadButton != null)
                    readyForTheRoadButton.Unlock();

                InstructionCardController ctrl = GetComponent<InstructionCardController>();
                if (ctrl != null && ctrl.CanvasRoot != null)
                    ctrl.CanvasRoot.SetActive(false);
            });
        }

        SubscribeToSnapZone("differanciel-snap");

        InstructionCardController ctrl = GetComponent<InstructionCardController>();
        if (ctrl != null)
            ctrl.ReplayAnimation();
    }

    private void BuildHeader20()
    {
        GameObject header = CreatePanel("Header", transform);
        AddVerticalLayout(header, new RectOffset(0, 0, 0, 0), 10, false);

        GameObject breadcrumb = CreateTextObject("Breadcrumb", header.transform,
            "STEP 19 OF 19  \u00B7  PORSCHE 911S ASSEMBLY",
            12, new Color(0.35f, 0.35f, 0.35f, 1f), FontStyles.Normal);
        breadcrumb.GetComponent<TextMeshProUGUI>().characterSpacing = 1.2f;

        GameObject badgeRow = CreatePanel("PhaseBadge", header.transform);
        AddHorizontalLayout(badgeRow, new RectOffset(12, 12, 7, 7), 6);
        AddImageToGameObject(badgeRow, ColPhase5Bg);
        AddLayoutElement(badgeRow, preferredHeight: 30f);
        CreateOverlayBorder("PhaseBadgeBorder", badgeRow.transform, ColPhase5Border);

        CreateTextObject("PhaseLabel", badgeRow.transform,
            "\u25CF  PHASE 5 \u2014 FINAL ASSEMBLY",
            12, ColPhase5Text, FontStyles.Bold);

        GameObject divider = CreatePanel("Divider", header.transform);
        AddImageToGameObject(divider, ColDivider);
        AddLayoutElement(divider, preferredHeight: 1f);
    }

    private void BuildMainContent20()
    {
        GameObject main = CreatePanel("MainContent", transform);
        AddVerticalLayout(main, new RectOffset(0, 0, 0, 0), 12, false);

        _taskLabel = CreateTextObject("TaskLabel", main.transform,
            "YOUR TASK \u2014 FINAL PIECE", 12, ColTaskLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _taskLabel.characterSpacing = 1.2f;

        _mainInstructionTmp = CreateTextObject("MainInstruction", main.transform,
            "Now place the Differential", 20, ColMainInstr, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _subtitle = CreateTextObject("Subtitle", main.transform,
            "The last piece \u2014 snap to complete your 911S",
            14, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _description = CreateTextObject("Description", main.transform,
            "Slide the differential unit into the rear axle housing. " +
            "It interfaces with both axle shafts. The drive flanges must click " +
            "into the axle shafts on both sides before the snap confirms.",
            13, ColDescription, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _description.lineSpacing = 6f;
    }

    private void BuildSnapTarget20()
    {
        GameObject snap = CreatePanel("SnapTarget", transform);
        AddHorizontalLayout(snap, new RectOffset(10, 10, 8, 8), 10);
        AddImageToGameObject(snap, ColPhase5SnapBg);

        _snapBorderImg = CreateOverlayBorder("SnapBorder", snap.transform, ColPhase5SnapBorder);

        GameObject snapLeft = CreatePanel("SnapLeft", snap.transform);
        AddVerticalLayout(snapLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(snapLeft, flexibleWidth: 1f);

        _snapLabelTmp = CreateTextObject("SnapLabel", snapLeft.transform,
            "SNAP TARGET", 11, ColPhase5TextDark, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _snapLabelTmp.characterSpacing = 1.2f;

        _snapValueTmp = CreateTextObject("SnapValue", snapLeft.transform,
            "diff_snap", 13, ColPhase5TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _snapIndicator = CreateTextObject("SnapIndicator", snap.transform,
            "\u25CF", 22, ColPhase5TextDark, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _snapIndicator.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(_snapIndicator.gameObject, preferredWidth: 28f);

        _isPulsing      = true;
        _pulseCoroutine = StartCoroutine(PulseIndicator());
    }

    private void BuildFactCard20()
    {
        GameObject fact = CreatePanel("FactCard", transform);
        AddVerticalLayout(fact, new RectOffset(10, 10, 8, 8), 0, false);
        AddImageToGameObject(fact, ColPhase5SnapBg);

        CreateOverlayBorder("FactBorder", fact.transform, ColPhase5Border, leftOnly: true);

        _factTextTmp = CreateTextObject("FactText", fact.transform,
            "The open differential allows each rear wheel to spin at different speeds " +
            "\u2014 essential for clean cornering and traction on the 911S.",
            12, ColPhase5TextDark, FontStyles.Italic)
            .GetComponent<TextMeshProUGUI>();
        _factTextTmp.lineSpacing = 6f;
    }

    private void BuildSuccessBar20()
    {
        _successBarGo = CreatePanel("SuccessBar", transform);
        AddVerticalLayout(_successBarGo, new RectOffset(12, 12, 10, 10), 4, false);
        AddImageToGameObject(_successBarGo, ColSuccessBg);
        CreateOverlayBorder("SuccessBarBorder", _successBarGo.transform, ColSuccessCardBd);

        _successBarTitle = CreateTextObject("SuccessTitle", _successBarGo.transform,
            _successData.SuccessBarTitle, 15, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _successBarSub = CreateTextObject("SuccessSub", _successBarGo.transform,
            _successData.SuccessBarSub, 12, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _successBarGo.SetActive(false);
    }

    private void BuildHeader10()
    {
        GameObject header = CreatePanel("Header", transform);
        AddVerticalLayout(header, new RectOffset(0, 0, 0, 0), 10, false);

        GameObject breadcrumb = CreateTextObject("Breadcrumb", header.transform,
            "STEP 09 OF 19  \u00B7  PORSCHE 911S ASSEMBLY",
            12, ColTaskLabel, FontStyles.Normal);
        breadcrumb.GetComponent<TextMeshProUGUI>().characterSpacing = 1.2f;

        GameObject badgeRow = CreatePanel("PhaseBadge", header.transform);
        AddHorizontalLayout(badgeRow, new RectOffset(12, 12, 7, 7), 6);
        AddImageToGameObject(badgeRow, ColSuccessGreen);
        AddLayoutElement(badgeRow, preferredHeight: 30f);

        CreateTextObject("PhaseLabel", badgeRow.transform,
            "\u25CF  PHASE 2 \u2014 BODY STRUCTURE",
            12, ColSuccessText, FontStyles.Bold);

        GameObject divider = CreatePanel("Divider", header.transform);
        AddImageToGameObject(divider, ColDivider);
        AddLayoutElement(divider, preferredHeight: 1f);
    }

    private void BuildMainContent10()
    {
        GameObject main = CreatePanel("MainContent", transform);
        AddVerticalLayout(main, new RectOffset(0, 0, 0, 0), 12, false);

        _taskLabel = CreateTextObject("TaskLabel", main.transform,
            "YOUR TASK", 12, ColTaskLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _taskLabel.characterSpacing = 1.2f;

        _mainInstructionTmp = CreateTextObject("MainInstruction", main.transform,
            "Now place the Hood", 20, ColMainInstr, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _subtitle = CreateTextObject("Subtitle", main.transform,
            "Find it in the inventory \u2192 snap to the target below",
            14, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _description = CreateTextObject("Description", main.transform,
            "Hinge the front hood (frunk lid) onto the front body. " +
            "Insert the hinge pins from the inside outward. " +
            "The 911 hood opens forward \u2014 verify hinge orientation before snapping.",
            13, ColDescription, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _description.lineSpacing = 6f;
    }

    private void BuildSnapTarget10()
    {
        GameObject snap = CreatePanel("SnapTarget", transform);
        AddHorizontalLayout(snap, new RectOffset(10, 10, 8, 8), 10);
        AddImageToGameObject(snap, ColSuccessBg);

        _snapBorderImg = CreateOverlayBorder("SnapBorder", snap.transform, ColSuccessBorder);

        GameObject snapLeft = CreatePanel("SnapLeft", snap.transform);
        AddVerticalLayout(snapLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(snapLeft, flexibleWidth: 1f);

        _snapLabelTmp = CreateTextObject("SnapLabel", snapLeft.transform,
            "SNAP TARGET", 11, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _snapLabelTmp.characterSpacing = 1.2f;

        _snapValueTmp = CreateTextObject("SnapValue", snapLeft.transform,
            "hood_snap", 13, ColSuccessText, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _snapIndicator = CreateTextObject("SnapIndicator", snap.transform,
            "\u25CF", 22, ColSuccessText, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _snapIndicator.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(_snapIndicator.gameObject, preferredWidth: 28f);

        _isPulsing      = true;
        _pulseCoroutine = StartCoroutine(PulseIndicator());
    }

    private void BuildFactCard10()
    {
        GameObject fact = CreatePanel("FactCard", transform);
        AddVerticalLayout(fact, new RectOffset(10, 10, 8, 8), 0, false);
        AddImageToGameObject(fact, ColSuccessBg);

        CreateOverlayBorder("FactBorder", fact.transform, ColSuccessCardBd, leftOnly: true);

        _factTextTmp = CreateTextObject("FactText", fact.transform,
            "The front trunk stores the spare wheel \u2014 the engine is at the rear, " +
            "freeing the entire nose for storage.",
            12, ColDescriptionSuccess, FontStyles.Italic)
            .GetComponent<TextMeshProUGUI>();
        _factTextTmp.lineSpacing = 6f;
    }

    private void BuildSuccessBar10()
    {
        _successBarGo = CreatePanel("SuccessBar", transform);
        AddVerticalLayout(_successBarGo, new RectOffset(12, 12, 10, 10), 4, false);
        AddImageToGameObject(_successBarGo, ColSuccessBg);
        CreateOverlayBorder("SuccessBarBorder", _successBarGo.transform, ColSuccessCardBd);

        _successBarTitle = CreateTextObject("SuccessTitle", _successBarGo.transform,
            _successData.SuccessBarTitle, 15, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _successBarSub = CreateTextObject("SuccessSub", _successBarGo.transform,
            _successData.SuccessBarSub, 12, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _successBarGo.SetActive(false);
    }

    private void BuildHeader09()
    {
        GameObject header = CreatePanel("Header", transform);
        AddVerticalLayout(header, new RectOffset(0, 0, 0, 0), 10, false);

        GameObject breadcrumb = CreateTextObject("Breadcrumb", header.transform,
            "STEP 08 OF 19  \u00B7  PORSCHE 911S ASSEMBLY",
            12, ColTaskLabel, FontStyles.Normal);
        breadcrumb.GetComponent<TextMeshProUGUI>().characterSpacing = 1.2f;

        GameObject badgeRow = CreatePanel("PhaseBadge", header.transform);
        AddHorizontalLayout(badgeRow, new RectOffset(12, 12, 7, 7), 6);
        AddImageToGameObject(badgeRow, ColSuccessGreen);
        AddLayoutElement(badgeRow, preferredHeight: 30f);

        CreateTextObject("PhaseLabel", badgeRow.transform,
            "\u25CF  PHASE 2 \u2014 BODY STRUCTURE",
            12, ColSuccessText, FontStyles.Bold);

        GameObject divider = CreatePanel("Divider", header.transform);
        AddImageToGameObject(divider, ColDivider);
        AddLayoutElement(divider, preferredHeight: 1f);
    }

    private void BuildMainContent09()
    {
        GameObject main = CreatePanel("MainContent", transform);
        AddVerticalLayout(main, new RectOffset(0, 0, 0, 0), 12, false);

        _taskLabel = CreateTextObject("TaskLabel", main.transform,
            "YOUR TASK", 12, ColTaskLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _taskLabel.characterSpacing = 1.2f;

        _mainInstructionTmp = CreateTextObject("MainInstruction", main.transform,
            "Now place the Rear Bumper", 20, ColMainInstr, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _subtitle = CreateTextObject("Subtitle", main.transform,
            "Find it in the inventory \u2192 snap to the target below",
            14, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _description = CreateTextObject("Description", main.transform,
            "Snap the rear bumper over the tail panel. Align the number plate recess centrally. " +
            "The exhaust cut-out must clear both tailpipes. " +
            "Two side clips lock the bumper flush to the quarter panels.",
            13, ColDescription, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _description.lineSpacing = 6f;
    }

    private void BuildSnapTarget09()
    {
        GameObject snap = CreatePanel("SnapTarget", transform);
        AddHorizontalLayout(snap, new RectOffset(10, 10, 8, 8), 10);
        AddImageToGameObject(snap, ColSuccessBg);

        _snapBorderImg = CreateOverlayBorder("SnapBorder", snap.transform, ColSuccessBorder);

        GameObject snapLeft = CreatePanel("SnapLeft", snap.transform);
        AddVerticalLayout(snapLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(snapLeft, flexibleWidth: 1f);

        _snapLabelTmp = CreateTextObject("SnapLabel", snapLeft.transform,
            "SNAP TARGET", 11, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _snapLabelTmp.characterSpacing = 1.2f;

        _snapValueTmp = CreateTextObject("SnapValue", snapLeft.transform,
            "rear_bumper_snap", 13, ColSuccessText, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _snapIndicator = CreateTextObject("SnapIndicator", snap.transform,
            "\u25CF", 22, ColSuccessText, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _snapIndicator.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(_snapIndicator.gameObject, preferredWidth: 28f);

        _isPulsing      = true;
        _pulseCoroutine = StartCoroutine(PulseIndicator());
    }

    private void BuildFactCard09()
    {
        GameObject fact = CreatePanel("FactCard", transform);
        AddVerticalLayout(fact, new RectOffset(10, 10, 8, 8), 0, false);
        AddImageToGameObject(fact, ColSuccessBg);

        CreateOverlayBorder("FactBorder", fact.transform, ColSuccessCardBd, leftOnly: true);

        _factTextTmp = CreateTextObject("FactText", fact.transform,
            "The classic 911S rear bumper houses the fog light and reflector \u2014 " +
            "minimal but iconic in proportion.",
            12, ColDescriptionSuccess, FontStyles.Italic)
            .GetComponent<TextMeshProUGUI>();
        _factTextTmp.lineSpacing = 6f;
    }

    private void BuildSuccessBar09()
    {
        _successBarGo = CreatePanel("SuccessBar", transform);
        AddVerticalLayout(_successBarGo, new RectOffset(12, 12, 10, 10), 4, false);
        AddImageToGameObject(_successBarGo, ColSuccessBg);
        CreateOverlayBorder("SuccessBarBorder", _successBarGo.transform, ColSuccessCardBd);

        _successBarTitle = CreateTextObject("SuccessTitle", _successBarGo.transform,
            _successData.SuccessBarTitle, 15, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _successBarSub = CreateTextObject("SuccessSub", _successBarGo.transform,
            _successData.SuccessBarSub, 12, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _successBarGo.SetActive(false);
    }

    private void BuildHeader03()
    {
        GameObject header = CreatePanel("Header", transform);
        AddVerticalLayout(header, new RectOffset(0, 0, 0, 0), 10, false);

        GameObject breadcrumb = CreateTextObject("Breadcrumb", header.transform,
            "STEP 03 OF 19  \u00B7  PORSCHE 911S ASSEMBLY",
            12, ColTaskLabel, FontStyles.Normal);
        breadcrumb.GetComponent<TextMeshProUGUI>().characterSpacing = 1.2f;

        GameObject badgeRow = CreatePanel("PhaseBadge", header.transform);
        AddHorizontalLayout(badgeRow, new RectOffset(12, 12, 7, 7), 6);
        AddImageToGameObject(badgeRow, ColPhaseBg);
        AddLayoutElement(badgeRow, preferredHeight: 30f);

        CreateTextObject("PhaseLabel", badgeRow.transform,
            "\u25CF  PHASE 1 \u2014 CHASSIS & DRIVETRAIN",
            12, ColPhaseText, FontStyles.Bold);

        GameObject divider = CreatePanel("Divider", header.transform);
        AddImageToGameObject(divider, ColDivider);
        AddLayoutElement(divider, preferredHeight: 1f);
    }

    private void BuildMainContent03()
    {
        GameObject main = CreatePanel("MainContent", transform);
        AddVerticalLayout(main, new RectOffset(0, 0, 0, 0), 12, false);

        _taskLabel = CreateTextObject("TaskLabel", main.transform,
            "YOUR TASK", 12, ColTaskLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _taskLabel.characterSpacing = 1.2f;

        _mainInstructionTmp = CreateTextObject("MainInstruction", main.transform,
            "Now place the Engine", 20, ColMainInstr, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _subtitle = CreateTextObject("Subtitle", main.transform,
            "Find it in the inventory and snap it to the correct position below.",
            14, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _description = CreateTextObject("Description", main.transform,
            "Lower the flat-6 engine into the rear engine bay. Align the three engine mount points " +
            "with the chassis brackets. Make sure the cooling fan is facing upward, and the oil " +
            "return lines are oriented toward the firewall.",
            13, ColDescription, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _description.lineSpacing = 6f;
    }

    private void BuildSnapTarget03()
    {
        GameObject snap = CreatePanel("SnapTarget", transform);
        AddHorizontalLayout(snap, new RectOffset(10, 10, 8, 8), 10);
        AddImageToGameObject(snap, ColSnapBg);

        _snapBorderImg = CreateOverlayBorder("SnapBorder", snap.transform, ColSnapBorder);

        GameObject snapLeft = CreatePanel("SnapLeft", snap.transform);
        AddVerticalLayout(snapLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(snapLeft, flexibleWidth: 1f);

        _snapLabelTmp = CreateTextObject("SnapLabel", snapLeft.transform,
            "SNAP TARGET", 11, ColSnapLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _snapLabelTmp.characterSpacing = 1.2f;

        _snapValueTmp = CreateTextObject("SnapValue", snapLeft.transform,
            "engine-snap", 13, ColSnapValue, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _snapIndicator = CreateTextObject("SnapIndicator", snap.transform,
            "\u25CF", 22, ColSnapIndicator, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _snapIndicator.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(_snapIndicator.gameObject, preferredWidth: 28f);

        _isPulsing      = true;
        _pulseCoroutine = StartCoroutine(PulseIndicator());
    }

    private void BuildFactCard03()
    {
        GameObject fact = CreatePanel("FactCard", transform);
        AddVerticalLayout(fact, new RectOffset(10, 10, 8, 8), 0, false);
        AddImageToGameObject(fact, ColFactBg);

        CreateOverlayBorder("FactBorder", fact.transform, ColFactAccent, leftOnly: true);

        _factTextTmp = CreateTextObject("FactText", fact.transform,
            "The 2.4L flat-6 engine sits entirely behind the rear axle \u2014 this rear-biased " +
            "weight distribution is a key factor in the distinctive handling of the Porsche 911.",
            12, ColFactText, FontStyles.Italic)
            .GetComponent<TextMeshProUGUI>();
        _factTextTmp.lineSpacing = 6f;
    }

    private void BuildSuccessBar03()
    {
        _successBarGo = CreatePanel("SuccessBar", transform);
        AddVerticalLayout(_successBarGo, new RectOffset(12, 12, 10, 10), 4, false);
        AddImageToGameObject(_successBarGo, ColSuccessBg);
        CreateOverlayBorder("SuccessBarBorder", _successBarGo.transform, ColSuccessCardBd);

        _successBarTitle = CreateTextObject("SuccessTitle", _successBarGo.transform,
            _successData.SuccessBarTitle, 15, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _successBarSub = CreateTextObject("SuccessSub", _successBarGo.transform,
            _successData.SuccessBarSub, 12, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _successBarGo.SetActive(false);
    }

    private void BuildNextStepPreview03()
    {
        _nextStepPreviewGo = CreatePanel("NextStepPreview", transform);
        AddHorizontalLayout(_nextStepPreviewGo, new RectOffset(10, 10, 8, 8), 8);
        AddImageToGameObject(_nextStepPreviewGo, ColSnapBg);
        CreateOverlayBorder("NextBorder", _nextStepPreviewGo.transform, ColSnapBorder);

        GameObject nextLeft = CreatePanel("NextLeft", _nextStepPreviewGo.transform);
        AddVerticalLayout(nextLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(nextLeft, flexibleWidth: 1f);

        TextMeshProUGUI labelTmp = CreateTextObject("NextLabel", nextLeft.transform,
            "NEXT UP", 11, ColSnapLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        labelTmp.characterSpacing = 1.2f;

        CreateTextObject("NextValue", nextLeft.transform,
            _successData.NextStepLabel, 13, ColSnapValue, FontStyles.Normal);

        _nextStepPreviewGo.SetActive(false);
    }

    private void BuildHeader04()
    {
        GameObject header = CreatePanel("Header", transform);
        AddVerticalLayout(header, new RectOffset(0, 0, 0, 0), 10, false);

        GameObject breadcrumb = CreateTextObject("Breadcrumb", header.transform,
            "STEP 04 OF 19  \u00B7  PORSCHE 911S ASSEMBLY",
            12, ColTaskLabel, FontStyles.Normal);
        breadcrumb.GetComponent<TextMeshProUGUI>().characterSpacing = 1.2f;

        GameObject badgeRow = CreatePanel("PhaseBadge", header.transform);
        AddHorizontalLayout(badgeRow, new RectOffset(12, 12, 7, 7), 6);
        AddImageToGameObject(badgeRow, ColPhaseBg);
        AddLayoutElement(badgeRow, preferredHeight: 30f);

        CreateTextObject("PhaseLabel", badgeRow.transform,
            "\u25CF  PHASE 1 \u2014 CHASSIS & DRIVETRAIN",
            12, ColPhaseText, FontStyles.Bold);

        GameObject divider = CreatePanel("Divider", header.transform);
        AddImageToGameObject(divider, ColDivider);
        AddLayoutElement(divider, preferredHeight: 1f);
    }

    private void BuildMainContent04()
    {
        GameObject main = CreatePanel("MainContent", transform);
        AddVerticalLayout(main, new RectOffset(0, 0, 0, 0), 12, false);

        _taskLabel = CreateTextObject("TaskLabel", main.transform,
            "YOUR TASK", 12, ColTaskLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _taskLabel.characterSpacing = 1.2f;

        _mainInstructionTmp = CreateTextObject("MainInstruction", main.transform,
            "Now place the Steering System", 20, ColMainInstr, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _subtitle = CreateTextObject("Subtitle", main.transform,
            "Find it in the inventory and snap it to the correct position below.",
            14, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _description = CreateTextObject("Description", main.transform,
            "Install the rack-and-pinion steering assembly at the front chassis subframe. " +
            "Snap the steering column into the firewall bracket. Ensure the tie-rod ends are " +
            "aligned parallel to the lower control arms for proper steering geometry.",
            13, ColDescription, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _description.lineSpacing = 6f;
    }

    private void BuildSnapTarget04()
    {
        GameObject snap = CreatePanel("SnapTarget", transform);
        AddHorizontalLayout(snap, new RectOffset(10, 10, 8, 8), 10);
        AddImageToGameObject(snap, ColSnapBg);

        _snapBorderImg = CreateOverlayBorder("SnapBorder", snap.transform, ColSnapBorder);

        GameObject snapLeft = CreatePanel("SnapLeft", snap.transform);
        AddVerticalLayout(snapLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(snapLeft, flexibleWidth: 1f);

        _snapLabelTmp = CreateTextObject("SnapLabel", snapLeft.transform,
            "SNAP TARGET", 11, ColSnapLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _snapLabelTmp.characterSpacing = 1.2f;

        _snapValueTmp = CreateTextObject("SnapValue", snapLeft.transform,
            "steering_snap", 13, ColSnapValue, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _snapIndicator = CreateTextObject("SnapIndicator", snap.transform,
            "\u25CF", 22, ColSnapIndicator, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _snapIndicator.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(_snapIndicator.gameObject, preferredWidth: 28f);

        _isPulsing      = true;
        _pulseCoroutine = StartCoroutine(PulseIndicator());
    }

    private void BuildFactCard04()
    {
        GameObject fact = CreatePanel("FactCard", transform);
        AddVerticalLayout(fact, new RectOffset(10, 10, 8, 8), 0, false);
        AddImageToGameObject(fact, ColFactBg);

        CreateOverlayBorder("FactBorder", fact.transform, ColFactAccent, leftOnly: true);

        _factTextTmp = CreateTextObject("FactText", fact.transform,
            "The rack-and-pinion system provides direct, unfiltered road feedback \u2014 " +
            "making the 911S highly responsive to even the smallest steering inputs.",
            12, ColFactText, FontStyles.Italic)
            .GetComponent<TextMeshProUGUI>();
        _factTextTmp.lineSpacing = 6f;
    }

    private void BuildSuccessBar04()
    {
        _successBarGo = CreatePanel("SuccessBar", transform);
        AddVerticalLayout(_successBarGo, new RectOffset(12, 12, 10, 10), 4, false);
        AddImageToGameObject(_successBarGo, ColSuccessBg);
        CreateOverlayBorder("SuccessBarBorder", _successBarGo.transform, ColSuccessCardBd);

        _successBarTitle = CreateTextObject("SuccessTitle", _successBarGo.transform,
            _successData.SuccessBarTitle, 15, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _successBarSub = CreateTextObject("SuccessSub", _successBarGo.transform,
            _successData.SuccessBarSub, 12, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _successBarGo.SetActive(false);
    }

    private void BuildHeader06()
    {
        GameObject header = CreatePanel("Header", transform);
        AddVerticalLayout(header, new RectOffset(0, 0, 0, 0), 10, false);

        GameObject breadcrumb = CreateTextObject("Breadcrumb", header.transform,
            "STEP 05 OF 19  \u00B7  PORSCHE 911S ASSEMBLY",
            12, ColTaskLabel, FontStyles.Normal);
        breadcrumb.GetComponent<TextMeshProUGUI>().characterSpacing = 1.2f;

        GameObject badgeRow = CreatePanel("PhaseBadge", header.transform);
        AddHorizontalLayout(badgeRow, new RectOffset(12, 12, 7, 7), 6);
        AddImageToGameObject(badgeRow, ColPhaseBg);
        AddLayoutElement(badgeRow, preferredHeight: 30f);

        CreateTextObject("PhaseLabel", badgeRow.transform,
            "\u25CF  PHASE 1 \u2014 CHASSIS & DRIVETRAIN",
            12, ColPhaseText, FontStyles.Bold);

        GameObject divider = CreatePanel("Divider", header.transform);
        AddImageToGameObject(divider, ColDivider);
        AddLayoutElement(divider, preferredHeight: 1f);
    }

    private void BuildMainContent06()
    {
        GameObject main = CreatePanel("MainContent", transform);
        AddVerticalLayout(main, new RectOffset(0, 0, 0, 0), 12, false);

        _taskLabel = CreateTextObject("TaskLabel", main.transform,
            "YOUR TASK", 12, ColTaskLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _taskLabel.characterSpacing = 1.2f;

        _mainInstructionTmp = CreateTextObject("MainInstruction", main.transform,
            "Now place the Wheels", 20, ColMainInstr, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _subtitle = CreateTextObject("Subtitle", main.transform,
            "Find it in the inventory \u2192 snap to the target below",
            14, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _description = CreateTextObject("Description", main.transform,
            "Mount all four wheels onto the hub flanges. Start with rear then front. " +
            "The wider 185/70 tires go on the rear, narrower set on the front. " +
            "Apply equal pressure to all lug positions.",
            13, ColDescription, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _description.lineSpacing = 6f;
    }

    private void BuildSnapTarget06()
    {
        GameObject snap = CreatePanel("SnapTarget", transform);
        AddHorizontalLayout(snap, new RectOffset(10, 10, 8, 8), 10);
        AddImageToGameObject(snap, ColSnapBg);

        _snapBorderImg = CreateOverlayBorder("SnapBorder", snap.transform, ColSnapBorder);

        GameObject snapLeft = CreatePanel("SnapLeft", snap.transform);
        AddVerticalLayout(snapLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(snapLeft, flexibleWidth: 1f);

        _snapLabelTmp = CreateTextObject("SnapLabel", snapLeft.transform,
            "SNAP TARGET", 11, ColSnapLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _snapLabelTmp.characterSpacing = 1.2f;

        _snapValueTmp = CreateTextObject("SnapValue", snapLeft.transform,
            "wheels_snap", 13, ColSnapValue, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _snapIndicator = CreateTextObject("SnapIndicator", snap.transform,
            "\u25CF", 22, ColSnapIndicator, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _snapIndicator.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(_snapIndicator.gameObject, preferredWidth: 28f);

        _isPulsing      = true;
        _pulseCoroutine = StartCoroutine(PulseIndicator());
    }

    private void BuildFactCard06()
    {
        GameObject fact = CreatePanel("FactCard", transform);
        AddVerticalLayout(fact, new RectOffset(10, 10, 8, 8), 0, false);
        AddImageToGameObject(fact, ColFactBg);

        CreateOverlayBorder("FactBorder", fact.transform, ColFactAccent, leftOnly: true);

        _factTextTmp = CreateTextObject("FactText", fact.transform,
            "The staggered wheel sizing compensates for rear-weight bias \u2014 " +
            "more grip exactly where the engine pushes.",
            12, ColFactText, FontStyles.Italic)
            .GetComponent<TextMeshProUGUI>();
        _factTextTmp.lineSpacing = 6f;
    }

    private void BuildSuccessBar06()
    {
        _successBarGo = CreatePanel("SuccessBar", transform);
        AddVerticalLayout(_successBarGo, new RectOffset(12, 12, 10, 10), 4, false);
        AddImageToGameObject(_successBarGo, ColSuccessBg);
        CreateOverlayBorder("SuccessBarBorder", _successBarGo.transform, ColSuccessCardBd);

        _successBarTitle = CreateTextObject("SuccessTitle", _successBarGo.transform,
            _successData.SuccessBarTitle, 15, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _successBarSub = CreateTextObject("SuccessSub", _successBarGo.transform,
            _successData.SuccessBarSub, 12, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _successBarGo.SetActive(false);
    }

    private void SubscribeToSnapZone(string zoneName)
    {
        SnapToPlace[] zones = Object.FindObjectsByType<SnapToPlace>(FindObjectsSortMode.None);
        foreach (SnapToPlace z in zones)
            if (z.gameObject.name == zoneName)
                z.OnObjectSnappedEvent += OnAnySnapCompleted;
    }

    /// <summary>Subscribes to all four individual wheel snap zones.</summary>
    private void SubscribeToWheelZones()
    {
        if (WheelPlacementTracker.Instance?.wheelsSnapParent == null) return;
        SnapToPlace[] zones = WheelPlacementTracker.Instance.wheelsSnapParent
                              .GetComponentsInChildren<SnapToPlace>();
        foreach (SnapToPlace z in zones)
            if (z != null) z.OnObjectSnappedEvent += OnWheelZoneSnapped;
    }

    private void UnsubscribeFromWheelZones()
    {
        if (WheelPlacementTracker.Instance?.wheelsSnapParent == null) return;
        SnapToPlace[] zones = WheelPlacementTracker.Instance.wheelsSnapParent
                              .GetComponentsInChildren<SnapToPlace>();
        foreach (SnapToPlace z in zones)
            if (z != null) z.OnObjectSnappedEvent -= OnWheelZoneSnapped;
    }

    private void OnWheelZoneSnapped(GameObject obj, SnapToPlace zone)
    {
        if (_successTriggered) return;

        // Only trigger success once every wheel is placed.
        if (WheelPlacementTracker.Instance != null && !WheelPlacementTracker.Instance.AllPlaced)
            return;

        SetSuccessState();
    }

    private void BuildHeader07()
    {
        GameObject header = CreatePanel("Header", transform);
        AddVerticalLayout(header, new RectOffset(0, 0, 0, 0), 10, false);

        GameObject breadcrumb = CreateTextObject("Breadcrumb", header.transform,
            "STEP 06 OF 19  \u00B7  PORSCHE 911S ASSEMBLY",
            12, ColTaskLabel, FontStyles.Normal);
        breadcrumb.GetComponent<TextMeshProUGUI>().characterSpacing = 1.2f;

        GameObject badgeRow = CreatePanel("PhaseBadge", header.transform);
        AddHorizontalLayout(badgeRow, new RectOffset(12, 12, 7, 7), 6);
        AddImageToGameObject(badgeRow, ColSuccessGreen);
        AddLayoutElement(badgeRow, preferredHeight: 30f);

        CreateTextObject("PhaseLabel", badgeRow.transform,
            "\u25CF  PHASE 2 \u2014 BODY STRUCTURE",
            12, ColSuccessText, FontStyles.Bold);

        GameObject divider = CreatePanel("Divider", header.transform);
        AddImageToGameObject(divider, ColDivider);
        AddLayoutElement(divider, preferredHeight: 1f);
    }

    private void BuildMainContent07()
    {
        GameObject main = CreatePanel("MainContent", transform);
        AddVerticalLayout(main, new RectOffset(0, 0, 0, 0), 12, false);

        _taskLabel = CreateTextObject("TaskLabel", main.transform,
            "YOUR TASK", 12, ColTaskLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _taskLabel.characterSpacing = 1.2f;

        _mainInstructionTmp = CreateTextObject("MainInstruction", main.transform,
            "Now place the Body Panels", 20, ColMainInstr, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _subtitle = CreateTextObject("Subtitle", main.transform,
            "Find it in the inventory \u2192 snap to the target below",
            14, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _description = CreateTextObject("Description", main.transform,
            "Lay the main body panel shell over the chassis. Align the sill channels with the chassis " +
            "longitudinals first, then snap from center outward. This panel forms the floor, sills and " +
            "inner arch structure.",
            13, ColDescription, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _description.lineSpacing = 6f;
    }

    private void BuildSnapTarget07()
    {
        GameObject snap = CreatePanel("SnapTarget", transform);
        AddHorizontalLayout(snap, new RectOffset(10, 10, 8, 8), 10);
        AddImageToGameObject(snap, ColSuccessBg);

        _snapBorderImg = CreateOverlayBorder("SnapBorder", snap.transform, ColSuccessBorder);

        GameObject snapLeft = CreatePanel("SnapLeft", snap.transform);
        AddVerticalLayout(snapLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(snapLeft, flexibleWidth: 1f);

        _snapLabelTmp = CreateTextObject("SnapLabel", snapLeft.transform,
            "SNAP TARGET", 11, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _snapLabelTmp.characterSpacing = 1.2f;

        _snapValueTmp = CreateTextObject("SnapValue", snapLeft.transform,
            "body_panels_snap", 13, ColSuccessText, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _snapIndicator = CreateTextObject("SnapIndicator", snap.transform,
            "\u25CF", 22, ColSuccessText, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _snapIndicator.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(_snapIndicator.gameObject, preferredWidth: 28f);

        _isPulsing      = true;
        _pulseCoroutine = StartCoroutine(PulseIndicator());
    }

    private void BuildFactCard07()
    {
        GameObject fact = CreatePanel("FactCard", transform);
        AddVerticalLayout(fact, new RectOffset(10, 10, 8, 8), 0, false);
        AddImageToGameObject(fact, ColSuccessBg);

        CreateOverlayBorder("FactBorder", fact.transform, ColSuccessCardBd, leftOnly: true);

        _factTextTmp = CreateTextObject("FactText", fact.transform,
            "The 911 unibody relies on this shell for torsional rigidity \u2014 " +
            "it defines how precisely the car handles.",
            12, ColDescriptionSuccess, FontStyles.Italic)
            .GetComponent<TextMeshProUGUI>();
        _factTextTmp.lineSpacing = 6f;
    }

    private void BuildSuccessBar07()
    {
        _successBarGo = CreatePanel("SuccessBar", transform);
        AddVerticalLayout(_successBarGo, new RectOffset(12, 12, 10, 10), 4, false);
        AddImageToGameObject(_successBarGo, ColSuccessBg);
        CreateOverlayBorder("SuccessBarBorder", _successBarGo.transform, ColSuccessCardBd);

        _successBarTitle = CreateTextObject("SuccessTitle", _successBarGo.transform,
            _successData.SuccessBarTitle, 15, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _successBarSub = CreateTextObject("SuccessSub", _successBarGo.transform,
            _successData.SuccessBarSub, 12, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _successBarGo.SetActive(false);
    }

    private void BuildHeader08()
    {
        GameObject header = CreatePanel("Header", transform);
        AddVerticalLayout(header, new RectOffset(0, 0, 0, 0), 10, false);

        GameObject breadcrumb = CreateTextObject("Breadcrumb", header.transform,
            "STEP 07 OF 19  \u00B7  PORSCHE 911S ASSEMBLY",
            12, ColTaskLabel, FontStyles.Normal);
        breadcrumb.GetComponent<TextMeshProUGUI>().characterSpacing = 1.2f;

        GameObject badgeRow = CreatePanel("PhaseBadge", header.transform);
        AddHorizontalLayout(badgeRow, new RectOffset(12, 12, 7, 7), 6);
        AddImageToGameObject(badgeRow, ColSuccessGreen);
        AddLayoutElement(badgeRow, preferredHeight: 30f);

        CreateTextObject("PhaseLabel", badgeRow.transform,
            "\u25CF  PHASE 2 \u2014 BODY STRUCTURE",
            12, ColSuccessText, FontStyles.Bold);

        GameObject divider = CreatePanel("Divider", header.transform);
        AddImageToGameObject(divider, ColDivider);
        AddLayoutElement(divider, preferredHeight: 1f);
    }

    private void BuildMainContent08()
    {
        GameObject main = CreatePanel("MainContent", transform);
        AddVerticalLayout(main, new RectOffset(0, 0, 0, 0), 12, false);

        _taskLabel = CreateTextObject("TaskLabel", main.transform,
            "YOUR TASK", 12, ColTaskLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _taskLabel.characterSpacing = 1.2f;

        _mainInstructionTmp = CreateTextObject("MainInstruction", main.transform,
            "Now place the Front Bumper", 20, ColMainInstr, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _subtitle = CreateTextObject("Subtitle", main.transform,
            "Find it in the inventory \u2192 snap to the target below",
            14, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _description = CreateTextObject("Description", main.transform,
            "Clip the front bumper to the front body panel. Two lower clips, two upper pins. " +
            "The lower valance lip must sit flush with the front apron \u2014 " +
            "no gap should be visible at the bottom edge.",
            13, ColDescription, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _description.lineSpacing = 6f;
    }

    private void BuildSnapTarget08()
    {
        GameObject snap = CreatePanel("SnapTarget", transform);
        AddHorizontalLayout(snap, new RectOffset(10, 10, 8, 8), 10);
        AddImageToGameObject(snap, ColSuccessBg);

        _snapBorderImg = CreateOverlayBorder("SnapBorder", snap.transform, ColSuccessBorder);

        GameObject snapLeft = CreatePanel("SnapLeft", snap.transform);
        AddVerticalLayout(snapLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(snapLeft, flexibleWidth: 1f);

        _snapLabelTmp = CreateTextObject("SnapLabel", snapLeft.transform,
            "SNAP TARGET", 11, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _snapLabelTmp.characterSpacing = 1.2f;

        _snapValueTmp = CreateTextObject("SnapValue", snapLeft.transform,
            "front_bumper_snap", 13, ColSuccessText, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _snapIndicator = CreateTextObject("SnapIndicator", snap.transform,
            "\u25CF", 22, ColSuccessText, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _snapIndicator.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(_snapIndicator.gameObject, preferredWidth: 28f);

        _isPulsing      = true;
        _pulseCoroutine = StartCoroutine(PulseIndicator());
    }

    private void BuildFactCard08()
    {
        GameObject fact = CreatePanel("FactCard", transform);
        AddVerticalLayout(fact, new RectOffset(10, 10, 8, 8), 0, false);
        AddImageToGameObject(fact, ColSuccessBg);

        CreateOverlayBorder("FactBorder", fact.transform, ColSuccessCardBd, leftOnly: true);

        _factTextTmp = CreateTextObject("FactText", fact.transform,
            "Pre-1974 911S bumpers were slim chrome units \u2014 purely aesthetic, " +
            "before impact regulations changed the design.",
            12, ColDescriptionSuccess, FontStyles.Italic)
            .GetComponent<TextMeshProUGUI>();
        _factTextTmp.lineSpacing = 6f;
    }

    private void BuildSuccessBar08()
    {
        _successBarGo = CreatePanel("SuccessBar", transform);
        AddVerticalLayout(_successBarGo, new RectOffset(12, 12, 10, 10), 4, false);
        AddImageToGameObject(_successBarGo, ColSuccessBg);
        CreateOverlayBorder("SuccessBarBorder", _successBarGo.transform, ColSuccessCardBd);

        _successBarTitle = CreateTextObject("SuccessTitle", _successBarGo.transform,
            _successData.SuccessBarTitle, 15, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _successBarSub = CreateTextObject("SuccessSub", _successBarGo.transform,
            _successData.SuccessBarSub, 12, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _successBarGo.SetActive(false);
    }

    private void BuildHeader02()
    {
        GameObject header = CreatePanel("Header", transform);
        AddVerticalLayout(header, new RectOffset(0, 0, 0, 0), 10, false);

        GameObject breadcrumb = CreateTextObject("Breadcrumb", header.transform,
            "STEP 02 OF 19  \u00B7  PORSCHE 911S ASSEMBLY",
            12, ColTaskLabel, FontStyles.Normal);
        breadcrumb.GetComponent<TextMeshProUGUI>().characterSpacing = 1.2f;

        GameObject badgeRow = CreatePanel("PhaseBadge", header.transform);
        AddHorizontalLayout(badgeRow, new RectOffset(12, 12, 7, 7), 6);
        AddImageToGameObject(badgeRow, ColPhaseBg);
        AddLayoutElement(badgeRow, preferredHeight: 30f);

        CreateTextObject("PhaseLabel", badgeRow.transform,
            "\u25CF  PHASE 1 \u2014 CHASSIS & DRIVETRAIN",
            12, ColPhaseText, FontStyles.Bold);

        GameObject divider = CreatePanel("Divider", header.transform);
        AddImageToGameObject(divider, ColDivider);
        AddLayoutElement(divider, preferredHeight: 1f);
    }

    private void BuildMainContent02()
    {
        GameObject main = CreatePanel("MainContent", transform);
        AddVerticalLayout(main, new RectOffset(0, 0, 0, 0), 12, false);

        _taskLabel = CreateTextObject("TaskLabel", main.transform,
            "YOUR TASK", 12, ColTaskLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _taskLabel.characterSpacing = 1.2f;

        CreateTextObject("MainInstruction", main.transform,
            "Now place the Rear Axles", 20, ColMainInstr, FontStyles.Bold);

        _subtitle = CreateTextObject("Subtitle", main.transform,
            "Find it in the inventory \u2192 snap to the target below",
            14, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _description = CreateTextObject("Description", main.transform,
            "Attach the rear axle assembly to the rear subframe mounts on the chassis. " +
            "Align all four mounting points simultaneously. The differential housing opening " +
            "must face toward the engine bay before snapping.",
            13, ColDescription, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _description.lineSpacing = 6f;
    }

    private void BuildSnapTarget02()
    {
        GameObject snap = CreatePanel("SnapTarget", transform);
        AddHorizontalLayout(snap, new RectOffset(10, 10, 8, 8), 10);
        AddImageToGameObject(snap, ColSnapBg);

        _snapBorderImg = CreateOverlayBorder("SnapBorder", snap.transform, ColSnapBorder);

        GameObject snapLeft = CreatePanel("SnapLeft", snap.transform);
        AddVerticalLayout(snapLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(snapLeft, flexibleWidth: 1f);

        _snapLabelTmp = CreateTextObject("SnapLabel", snapLeft.transform,
            "SNAP TARGET", 11, ColSnapLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _snapLabelTmp.characterSpacing = 1.2f;

        _snapValueTmp = CreateTextObject("SnapValue", snapLeft.transform,
            "rearAxles-snap", 13, ColSnapValue, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _snapIndicator = CreateTextObject("SnapIndicator", snap.transform,
            "\u25CF", 22, ColSnapIndicator, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _snapIndicator.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(_snapIndicator.gameObject, preferredWidth: 28f);

        _isPulsing      = true;
        _pulseCoroutine = StartCoroutine(PulseIndicator());
    }

    private void BuildFactCard02()
    {
        GameObject fact = CreatePanel("FactCard", transform);
        AddVerticalLayout(fact, new RectOffset(10, 10, 8, 8), 0, false);
        AddImageToGameObject(fact, ColFactBg);

        CreateOverlayBorder("FactBorder", fact.transform, ColFactAccent, leftOnly: true);

        CreateTextObject("FactText", fact.transform,
            "The 911S uses a semi-trailing arm rear suspension \u2014 unconventional " +
            "but key to its precise cornering behavior at speed.",
            12, ColFactText, FontStyles.Italic)
            .GetComponent<TextMeshProUGUI>().lineSpacing = 6f;
    }

    /// <summary>
    /// Creates the success message bar shown after snap.
    /// Hidden at build time; revealed by SetSuccessState().
    /// </summary>
    private void BuildSuccessBar02()
    {
        _successBarGo = CreatePanel("SuccessBar", transform);
        AddVerticalLayout(_successBarGo, new RectOffset(12, 12, 10, 10), 4, false);
        AddImageToGameObject(_successBarGo, ColSuccessBg);
        CreateOverlayBorder("SuccessBarBorder", _successBarGo.transform, ColSuccessCardBd);

        _successBarTitle = CreateTextObject("SuccessTitle", _successBarGo.transform,
            _successData.SuccessBarTitle, 15, ColSuccessText, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();

        _successBarSub = CreateTextObject("SuccessSub", _successBarGo.transform,
            _successData.SuccessBarSub, 12, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        _successBarGo.SetActive(false);
    }

    /// <summary>
    /// Creates the "NEXT UP" preview row shown after snap.
    /// Hidden at build time; revealed by SetSuccessState().
    /// </summary>
    private void BuildNextStepPreview02()
    {
        _nextStepPreviewGo = CreatePanel("NextStepPreview", transform);
        AddHorizontalLayout(_nextStepPreviewGo, new RectOffset(10, 10, 8, 8), 8);
        AddImageToGameObject(_nextStepPreviewGo, ColSnapBg);
        CreateOverlayBorder("NextBorder", _nextStepPreviewGo.transform, ColSnapBorder);

        GameObject nextLeft = CreatePanel("NextLeft", _nextStepPreviewGo.transform);
        AddVerticalLayout(nextLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(nextLeft, flexibleWidth: 1f);

        TextMeshProUGUI labelTmp = CreateTextObject("NextLabel", nextLeft.transform,
            "NEXT UP", 11, ColSnapLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        labelTmp.characterSpacing = 1.2f;

        CreateTextObject("NextValue", nextLeft.transform,
            _successData.NextStepLabel, 13, ColSnapValue, FontStyles.Normal);

        _nextStepPreviewGo.SetActive(false);
    }

    // ── Content builder ───────────────────────────────────────────────────────

    private void BuildContent()
    {
        // Remove stale children (old Header / Body) — keep Border (index 0)
        CleanStaleChildren();

        BuildHeader();
        BuildMainContent();
        BuildSnapTarget();
        BuildFactCard();
        BuildFooter();
    }

    /// <summary>Destroys any children that are not named "Border".</summary>
    private void CleanStaleChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name != "Border")
                Destroy(child.gameObject);
        }
    }

    // ── Section builders ──────────────────────────────────────────────────────

    private void BuildHeader()
    {
        GameObject header = CreatePanel("Header", transform);
        AddVerticalLayout(header, new RectOffset(0, 0, 0, 0), 10, false);

        // Breadcrumb
        GameObject breadcrumb = CreateTextObject("Breadcrumb", header.transform,
            "STEP 01 OF 19  \u00B7  PORSCHE 911S ASSEMBLY",
            12, ColTaskLabel, FontStyles.Normal);
        breadcrumb.GetComponent<TextMeshProUGUI>().characterSpacing = 1.2f;

        // Phase badge row
        GameObject badgeRow = CreatePanel("PhaseBadge", header.transform);
        AddHorizontalLayout(badgeRow, new RectOffset(12, 12, 7, 7), 6);
        AddImageToGameObject(badgeRow, ColPhaseBg);
        AddLayoutElement(badgeRow, preferredHeight: 30f);

        CreateTextObject("PhaseLabel", badgeRow.transform,
            "\u25CF  PHASE 1 \u2014 CHASSIS & DRIVETRAIN",
            12, ColPhaseText, FontStyles.Bold);

        // Divider
        GameObject divider = CreatePanel("Divider", header.transform);
        AddImageToGameObject(divider, ColDivider);
        AddLayoutElement(divider, preferredHeight: 1f);
    }

    private void BuildMainContent()
    {
        GameObject main = CreatePanel("MainContent", transform);
        AddVerticalLayout(main, new RectOffset(0, 0, 0, 0), 12, false);

        // Task label
        _taskLabel = CreateTextObject("TaskLabel", main.transform,
            "YOUR TASK", 12, ColTaskLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _taskLabel.characterSpacing = 1.2f;

        // Main instruction
        CreateTextObject("MainInstruction", main.transform,
            "Now place the Base / Chassis", 20, ColMainInstr, FontStyles.Bold);

        // Subtitle
        _subtitle = CreateTextObject("Subtitle", main.transform,
            "Find it in the inventory \u2192 snap to the target below",
            14, ColSubtitle, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        // Description
        _description = CreateTextObject("Description", main.transform,
            "The chassis forms the structural backbone of the Porsche 911S. " +
            "Align the mounting points and release when the zone highlights blue.",
            13, ColDescription, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _description.lineSpacing = 6f;
    }

    private void BuildSnapTarget()
    {
        // Outer panel with HLG
        GameObject snap = CreatePanel("SnapTarget", transform);
        AddHorizontalLayout(snap, new RectOffset(10, 10, 8, 8), 10);
        AddImageToGameObject(snap, ColSnapBg);

        // Border overlay (ignoreLayout, full stretch)
        _snapBorderImg = CreateOverlayBorder("SnapBorder", snap.transform, ColSnapBorder);

        // Left side
        GameObject snapLeft = CreatePanel("SnapLeft", snap.transform);
        AddVerticalLayout(snapLeft, new RectOffset(0, 0, 0, 0), 4, false);
        AddLayoutElement(snapLeft, flexibleWidth: 1f);

        _snapLabelTmp = CreateTextObject("SnapLabel", snapLeft.transform,
            "SNAP TARGET", 11, ColSnapLabel, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _snapLabelTmp.characterSpacing = 1.2f;

        _snapValueTmp = CreateTextObject("SnapValue", snapLeft.transform,
            "chassis_snap", 13, ColSnapValue, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();

        // Right indicator
        _snapIndicator = CreateTextObject("SnapIndicator", snap.transform,
            "\u25CF", 22, ColSnapIndicator, FontStyles.Normal)
            .GetComponent<TextMeshProUGUI>();
        _snapIndicator.alignment = TextAlignmentOptions.Right;
        AddLayoutElement(_snapIndicator.gameObject, preferredWidth: 28f);

        // Start pulsing
        _isPulsing = true;
        _pulseCoroutine = StartCoroutine(PulseIndicator());
    }

    private void BuildFactCard()
    {
        GameObject fact = CreatePanel("FactCard", transform);
        AddVerticalLayout(fact, new RectOffset(10, 10, 8, 8), 0, false);
        AddImageToGameObject(fact, ColFactBg);

        // Left accent border
        CreateOverlayBorder("FactBorder", fact.transform, ColFactAccent, leftOnly: true);

        CreateTextObject("FactText", fact.transform,
            "\u201CThe Porsche 911S chassis is a unibody steel construction. " +
            "Its torsional rigidity is the foundation of the car\u2019s legendary handling.\u201D",
            12, ColFactText, FontStyles.Italic)
            .GetComponent<TextMeshProUGUI>().lineSpacing = 6f;
    }

    private void BuildFooter()
    {
        GameObject footer = CreatePanel("Footer", transform);
        AddHorizontalLayout(footer, new RectOffset(0, 0, 0, 0), 0);

        // Continue button
        GameObject btnGo = CreatePanel("ContinueButton", footer.transform);
        AddLayoutElement(btnGo, preferredHeight: 48f, flexibleWidth: 1f);
        _continueBgImg = AddImageToGameObject(btnGo, ColBtnDisabledBg);
        _continueBgImg.raycastTarget = true;

        // Button border overlay
        _continueBorderImg = CreateOverlayBorder("ContinueBorder", btnGo.transform, ColBtnDisabledBd);

        // Label
        _continueLabelTmp = CreateTextObject("ContinueLabel", btnGo.transform,
            "Continue \u2192", 14, ColBtnDisabledTx, FontStyles.Bold)
            .GetComponent<TextMeshProUGUI>();
        _continueLabelTmp.alignment = TextAlignmentOptions.Center;

        // Button component — disabled, no transition (we drive colours manually)
        // targetGraphic must be assigned so GraphicRaycaster can route pointer events
        _continueButton               = btnGo.AddComponent<Button>();
        _continueButton.targetGraphic = _continueBgImg;
        _continueButton.interactable  = false;
        _continueButton.transition    = Selectable.Transition.None;
        _continueButton.onClick.AddListener(OnContinueClicked);
    }

    // ── Pulse coroutine ───────────────────────────────────────────────────────

    private IEnumerator PulseIndicator()
    {
        while (_isPulsing)
        {
            float alpha = (Mathf.Sin(Time.time * Mathf.PI * 1.4f) * 0.5f) + 0.5f;
            if (_snapIndicator != null)
                _snapIndicator.color = new Color(
                    ColSnapIndicator.r, ColSnapIndicator.g, ColSnapIndicator.b,
                    Mathf.Lerp(0.25f, 1f, alpha));
            yield return null;
        }
    }

    // ── Factory helpers ───────────────────────────────────────────────────────

    private static GameObject CreatePanel(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject CreateTextObject(string name, Transform parent,
        string text, float fontSize, Color color, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text            = text;
        tmp.fontSize        = fontSize;
        tmp.color           = color;
        tmp.fontStyle       = style;
        tmp.raycastTarget   = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode    = TextOverflowModes.Overflow;
        return go;
    }

    private static Image AddImageToGameObject(GameObject go, Color color)
    {
        Image img   = go.AddComponent<Image>();
        img.color   = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Creates a full-stretch overlay image used as a border frame.</summary>
    private static Image CreateOverlayBorder(string name, Transform parent,
        Color color, bool leftOnly = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = leftOnly ? new Vector2(-4f, -4f) : new Vector2(-4f, -4f);
        rt.anchoredPosition = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        return img;
    }

    private static void AddVerticalLayout(GameObject go, RectOffset padding,
        float spacing, bool forceExpandHeight = false)
    {
        VerticalLayoutGroup vlg  = go.AddComponent<VerticalLayoutGroup>();
        vlg.padding              = padding;
        vlg.spacing              = spacing;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = forceExpandHeight;

        ContentSizeFitter csf    = go.AddComponent<ContentSizeFitter>();
        csf.horizontalFit        = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit          = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static void AddHorizontalLayout(GameObject go, RectOffset padding, float spacing)
    {
        HorizontalLayoutGroup hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.padding               = padding;
        hlg.spacing               = spacing;
        hlg.childControlWidth     = true;
        hlg.childControlHeight    = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;

        ContentSizeFitter csf    = go.AddComponent<ContentSizeFitter>();
        csf.horizontalFit        = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit          = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static void AddLayoutElement(GameObject go,
        float preferredWidth  = -1f,
        float preferredHeight = -1f,
        float flexibleWidth   = -1f,
        float flexibleHeight  = -1f)
    {
        LayoutElement le    = go.AddComponent<LayoutElement>();
        le.preferredWidth   = preferredWidth;
        le.preferredHeight  = preferredHeight;
        le.flexibleWidth    = flexibleWidth;
        le.flexibleHeight   = flexibleHeight;
    }
}
