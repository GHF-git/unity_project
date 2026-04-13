using UnityEngine;

/// <summary>
/// Physically synchronized rack-and-pinion steering animation.
///
/// All motion is derived from a single master angular velocity (rotationSpeed)
/// applied to the pinion (blackeng).  Every other gear rotates at a speed
/// proportional to its pitch-radius ratio, and the rack translates at exactly
/// v = ω_rad × r_pinion — so teeth never slip.
///
/// Link synchronization:
///   Each tie-rod link is pinned at two ball joints:
///     - Inner end (RackAnchor): the red circle on the crémaillère, moving with rack.
///     - Outer end (AxleAnchor): the grey circle on the axle, rotating with axle.
///   Assign empty GameObjects placed at those circles in the Inspector.
///   If no anchor is assigned, falls back to auto-detection from mesh bounds.
///
/// Gear pairs / shaft groups:
///   Group A (ω_main)  : blackeng, blackEngrenage1, lower, red, arbreBlack2
///   Group B (ω_white) : whiteeng1, whitearbre1  — mesh with blackeng in Z
///     ω_white = ω_main × (r_pinion / r_white)
///   Group C (ω_yellow): yellow-engrenage        — mesh with blackEngrenage1 in X
///     ω_yellow = ω_main × (r_blackEng1 / r_yellow)
///   Group D (ω_main)  : arbreBlack1             — same shaft chain as A
///   Steering wheel    : same shaft as A, opposite sign convention
/// </summary>
public class SteeringAnimationController : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────
    [Header("Assembly Reference")]
    public SteeringSnapSetup snapSetup;

    [Header("Master Speed")]
    [Tooltip("Angular velocity of the pinion (blackeng) in degrees/second.")]
    public float rotationSpeed = 90f;

    [Header("Pinion Pitch Radius")]
    [Tooltip("Distance from blackeng center to rack tooth surface in local units. " +
             "Rack speed is derived from this — tweak until teeth look in sync.")]
    public float pinionRadius = 0.0392f;

    [Header("Rack Travel")]
    [Tooltip("Maximum rack travel from rest in local units. " +
             "Decrease this until the pinion stops just before the red end-stop circle.")]
    public float rackMaxOffset = 0.12f;

    [Tooltip("Safety margin in local units subtracted from rackMaxOffset at runtime. " +
             "Increase this to stop the rack earlier (pinion stays further from the red circle).")]
    public float rackEndStopMargin = 0.0f;

    [Header("Secondary Gear Radii (local units)")]
    [Tooltip("Pitch radius of whiteeng1 / whitearbre1.")]
    public float whiteGearRadius = 0.0892f;

    [Tooltip("Pitch radius of blackEngrenage1.")]
    public float blackEng1Radius = 0.040f;

    [Tooltip("Pitch radius of yellow-engrenage.")]
    public float yellowRadius = 0.120f;

    [Header("Ackermann Angles")]
    [Tooltip("Max pivot of inner (right) wheel in degrees.")]
    public float innerMaxAngle = 40f;

    [Tooltip("Max pivot of outer (left) wheel in degrees.")]
    public float outerMaxAngle = 28f;

    [Header("Link Anchors — place empty GameObjects at the socket centers")]
    [Tooltip("Empty GameObject at the red circle on the crémaillère (inner ball joint for Right-LINK). " +
             "Must be a child of the crémaillère so it translates with it.")]
    public Transform rightLinkRackAnchor;

    [Tooltip("Empty GameObject at the grey circle on rightAxle (outer ball joint for Right-LINK). " +
             "Must be a child of rightAxle so it rotates with it.")]
    public Transform rightLinkAxleAnchor;

    [Tooltip("Empty GameObject at the red circle on the crémaillère (inner ball joint for Left-LINK). " +
             "Must be a child of the crémaillère so it translates with it.")]
    public Transform leftLinkRackAnchor;

    [Tooltip("Empty GameObject at the grey circle on LeftAxle.001 (outer ball joint for Left-LINK). " +
             "Must be a child of LeftAxle.001 so it rotates with it.")]
    public Transform leftLinkAxleAnchor;

    // ── Part references ────────────────────────────────────────────────────
    Transform steeringWheelTr;

    // Group A — same shaft as pinion, ω_main
    Transform blackeng;
    Transform blackEngrenage1;
    Transform lower;
    Transform red;
    Transform arbreBlack2;

    // Group B — mesh with blackeng (Z axis), ω_white
    Transform whiteeng1;
    Transform whitearbre1;

    // Group C — mesh with blackEngrenage1 (X axis), ω_yellow
    Transform yellowEngrenage;

    // Group D — shaft, ω_main around its own Y
    Transform arbreBlack1;

    // Translating parts
    Transform rack;
    Transform rightLink;
    Transform leftLink;

    // Axle pivots
    Transform  rightAxle;
    Transform  leftAxle;

    // ── Cached rest poses ──────────────────────────────────────────────────
    Vector3    rackRestLocalPos;

    // Inner socket (rack red-circle end) stored in rack local space → auto-translates with rack.
    Vector3    rightInnerRackLocal;
    Vector3    leftInnerRackLocal;

    // Outer socket (axle grey-circle end) stored in axle local space → auto-rotates with axle.
    Vector3    rightOuterAxleLocal;
    Vector3    leftOuterAxleLocal;

    // Socket offsets in link local space (used to compute pivot position from socket world pos).
    Vector3    rightInnerLinkLocal;   // inner socket position in Right-LINK local space
    Vector3    leftInnerLinkLocal;    // inner socket position in Left-LINK local space
    Vector3    rightOuterLinkLocal;   // outer socket position in Right-LINK local space
    Vector3    leftOuterLinkLocal;    // outer socket position in Left-LINK local space

    // Rest world rotation of each link.
    Quaternion rightLinkRestWorldRot;
    Quaternion leftLinkRestWorldRot;

    // Rest world direction inner→outer of each link (used for FromToRotation each frame).
    Vector3    rightLinkRestDir;
    Vector3    leftLinkRestDir;

    // Rest length of each tie-rod (distance between the two socket centers at rest).
    // Used to clamp rack travel so the rod never has to stretch beyond its real length.
    float      rightRodLength;
    float      leftRodLength;

    Quaternion rightAxleRestWorldRot;
    Quaternion leftAxleRestWorldRot;

    // ── Derived sync speeds (deg/s) ────────────────────────────────────────
    float omegaMain;
    float omegaWhite;
    float omegaYellow;
    float rackSpeedLocal;   // local units / second, derived from ω and r_pinion
    float effectiveMaxOffset; // = rackMaxOffset − rackEndStopMargin

    // ── State ──────────────────────────────────────────────────────────────
    // Signed rack offset: positive = right turn (R), negative = left turn (L).
    // Clamped to [−rackMaxOffset, +rackMaxOffset].
    float rackOffset;
    bool  animationReady;

    Transform physicalRoot;

    // ──────────────────────────────────────────────────────────────────────
    void Start()
    {
        if (snapSetup == null)
        {
            Debug.LogError("[SteeringAnimation] snapSetup is not assigned.", this);
            return;
        }

        physicalRoot = snapSetup.partsRoot;

        steeringWheelTr  = FindPart("steering wheel");
        blackeng         = FindPart("blackeng");
        blackEngrenage1  = FindPart("blackEngrenage1");
        lower            = FindPart("lower");
        red              = FindPart("red");
        arbreBlack2      = FindPart("arbreBlack2");
        whiteeng1        = FindPart("whiteeng1");
        whitearbre1      = FindPart("whitearbre1");
        yellowEngrenage  = FindPart("yellow-engrenage");
        arbreBlack1      = FindPart("arbreBlack1");
        rack             = FindPart("crémaillère");
        rightLink        = FindPart("Right-LINK");
        leftLink         = FindPart("Left-LINK");
        rightAxle        = FindPart("rightAxle");
        leftAxle         = FindPart("LeftAxle.001");

        LogMissingParts();
    }

    // ──────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (!animationReady)
        {
            if (snapSetup == null || !snapSetup.AllPartsFullySnapped) return;
            animationReady = true;
            OnAnimationReady();
            return;
        }

        if (!Input.GetKey(KeyCode.R) && !Input.GetKey(KeyCode.L)) return;

        // +1 = right turn (R),  −1 = left turn (L).
        // If both keys held simultaneously, cancel out.
        bool pressR = Input.GetKey(KeyCode.R);
        bool pressL = Input.GetKey(KeyCode.L);
        if (pressR && pressL) return;
        float direction = pressR ? 1f : -1f;

        float dt = Time.deltaTime;

        // ── Advance signed rack offset, clamped to [−max, +max] ────────
        float newOffset = Mathf.Clamp(
            rackOffset + direction * rackSpeedLocal * dt,
            -effectiveMaxOffset, effectiveMaxOffset);

        // Already at the limit in the requested direction — nothing to do.
        if (Mathf.Approximately(newOffset, rackOffset)) return;

        rackOffset = newOffset;

        // t ∈ [−1, +1]: negative = full left, positive = full right.
        float t = effectiveMaxOffset > 0f ? rackOffset / effectiveMaxOffset : 0f;

        // ── Rack translates: local −X = world +X ────────────────────────
        if (rack != null) rack.localPosition = rackRestLocalPos + Vector3.left * rackOffset;

        // ── Gear rotations — all multiplied by direction ────────────────
        Rot(steeringWheelTr, 0f, 0f,  omegaMain   * direction * dt);
        Rot(blackeng,        0f, 0f, -omegaMain   * direction * dt);
        Rot(blackEngrenage1, 0f, 0f, -omegaMain   * direction * dt);
        Rot(lower,           0f, 0f, -omegaMain   * direction * dt);
        Rot(red,             0f, 0f, -omegaMain   * direction * dt);
        Rot(arbreBlack2,     0f,  omegaMain   * direction * dt, 0f);
        Rot(whiteeng1,       0f, 0f,  omegaWhite  * direction * dt);
        Rot(whitearbre1,     0f, 0f,  omegaWhite  * direction * dt);
        Rot(yellowEngrenage, 0f, 0f,  omegaYellow * direction * dt);
        Rot(arbreBlack1,     0f,  omegaMain   * direction * dt, 0f);

        // ── Axle pivots: absolute rotation around world Y ────────────────
        if (rightAxle != null)
            rightAxle.rotation = Quaternion.AngleAxis(-innerMaxAngle * t, Vector3.up) * rightAxleRestWorldRot;
        if (leftAxle != null)
            leftAxle.rotation  = Quaternion.AngleAxis(-outerMaxAngle * t, Vector3.up) * leftAxleRestWorldRot;

        // ── Links: reposition and reorient to stay connected ────────────
        // Each link spans between:
        //   inner socket → rack red circle (in rack local space, auto-translates with rack)
        //   outer socket → axle grey circle (in axle local space, auto-rotates with axle)
        SyncLink(rightLink, rack, rightInnerRackLocal, rightInnerLinkLocal, rightOuterLinkLocal,
                 rightAxle, rightOuterAxleLocal,
                 rightLinkRestDir, rightLinkRestWorldRot, rightRodLength);
        SyncLink(leftLink,  rack, leftInnerRackLocal,  leftInnerLinkLocal,  leftOuterLinkLocal,
                 leftAxle,  leftOuterAxleLocal,
                 leftLinkRestDir,  leftLinkRestWorldRot, leftRodLength);
    }

    // ──────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Called once, guaranteed AFTER every SnapNextFrame coroutine has finished.
    /// Computes synchronized speeds, captures rest poses, unlocks constraints.
    /// </summary>
    void OnAnimationReady()
    {
        // Derive synchronized angular speeds from gear radii.
        omegaMain   = rotationSpeed;
        omegaWhite  = whiteGearRadius  > 0f ? omegaMain * (pinionRadius    / whiteGearRadius)  : omegaMain;
        omegaYellow = yellowRadius      > 0f ? omegaMain * (blackEng1Radius / yellowRadius)     : omegaMain;

        // Rack linear speed: v [local/s] = ω [rad/s] × r_pinion [local]
        // This is the only correct formula — rackSpeed is NOT independent.
        rackSpeedLocal = omegaMain * Mathf.Deg2Rad * pinionRadius;

        // Effective travel limit — subtract margin so pinion stops before the red end-stop circle.
        effectiveMaxOffset = Mathf.Max(0f, rackMaxOffset - rackEndStopMargin);

        Debug.Log($"[SteeringAnimation] Synchronized — " +
                  $"ω_main={omegaMain:F1}°/s  ω_white={omegaWhite:F1}°/s  " +
                  $"ω_yellow={omegaYellow:F1}°/s  v_rack={rackSpeedLocal:F5} local/s");

        // Capture rest poses (post-snap world positions guaranteed by AllPartsFullySnapped).
        if (rack      != null) rackRestLocalPos      = rack.localPosition;
        if (rightAxle != null) rightAxleRestWorldRot = rightAxle.rotation;
        if (leftAxle  != null) leftAxleRestWorldRot  = leftAxle.rotation;

        // Capture link two-endpoint geometry.
        // Inner socket (rack red circle) stored in rack local space → auto-translates with rack.
        // Outer socket (axle grey circle) stored in axle local space → auto-rotates with axle.
        // Inner socket also stored in link local space → used to compute link pivot position.
        CaptureLink(rightLink, rack, rightAxle,
                    rightLinkRackAnchor, rightLinkAxleAnchor,
                    out rightInnerRackLocal, out rightOuterAxleLocal,
                    out rightInnerLinkLocal, out rightOuterLinkLocal,
                    out rightLinkRestDir, out rightLinkRestWorldRot,
                    out rightRodLength);
        CaptureLink(leftLink, rack, leftAxle,
                    leftLinkRackAnchor, leftLinkAxleAnchor,
                    out leftInnerRackLocal, out leftOuterAxleLocal,
                    out leftInnerLinkLocal, out leftOuterLinkLocal,
                    out leftLinkRestDir, out leftLinkRestWorldRot,
                    out leftRodLength);

        // Auto-limit rack travel to the tie-rod geometry.
        // At max offset the rod must not stretch — the rack can only move as far as
        // the rod length allows given the fixed outer (axle) anchor distance.
        // We use the smaller of the two rods as the binding constraint.
        float minRod           = Mathf.Min(rightRodLength, leftRodLength);
        float rackToAxleDist   = rack != null && rightAxle != null
                                 ? Vector3.Distance(rack.position, rightAxle.position)
                                 : 0f;
        // Maximum safe rack offset = sqrt(rod² − perp²), where perp is the
        // lateral (Y + Z) distance from inner socket to outer socket at rest.
        // Simplified: we keep the inspector rackMaxOffset but cap it so the
        // separation never exceeds rodLength.
        if (minRod > 0f)
        {
            // Distance between inner and outer sockets at rest (= rodLength at rest).
            // As rack moves by Δ, the inner socket moves by Δ while outer stays fixed →
            // new separation = sqrt(rodLength² − perp² + Δ²).  We want separation ≤ rodLength,
            // so Δ ≤ 0 — the rod can only compress, not stretch.  This means we just
            // ensure effectiveMaxOffset doesn't let the inner socket move more than rodLength
            // away from the outer socket along the rack axis.
            effectiveMaxOffset = Mathf.Min(effectiveMaxOffset, minRod);
        }

        // Links need both position AND rotation freedom (they reposition each frame).
        AllowPosition(rack);
        AllowFreedom(rightLink);
        AllowFreedom(leftLink);

        // Rotating parts: allow rotation, keep position locked.
        AllowRotation(steeringWheelTr);
        AllowRotation(blackeng);
        AllowRotation(blackEngrenage1);
        AllowRotation(lower);
        AllowRotation(red);
        AllowRotation(arbreBlack2);
        AllowRotation(whiteeng1);
        AllowRotation(whitearbre1);
        AllowRotation(yellowEngrenage);
        AllowRotation(arbreBlack1);
        AllowRotation(rightAxle);
        AllowRotation(leftAxle);
    }

    // ──────────────────────────────────────────────────────────────────────
    static void Rot(Transform tr, float x, float y, float z)
    {
        if (tr != null) tr.Rotate(x, y, z, Space.Self);
    }

    /// <summary>FreezeAll → FreezeRotation: position changes are allowed.</summary>
    static void AllowPosition(Transform tr)
    {
        if (tr == null) return;
        Rigidbody rb = tr.GetComponent<Rigidbody>();
        if (rb != null) rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    /// <summary>FreezeAll → FreezePosition: rotation changes are allowed.</summary>
    static void AllowRotation(Transform tr)
    {
        if (tr == null) return;
        Rigidbody rb = tr.GetComponent<Rigidbody>();
        if (rb != null) rb.constraints = RigidbodyConstraints.FreezePosition;
    }

    /// <summary>Removes all Rigidbody constraints: both position and rotation are free.</summary>
    static void AllowFreedom(Transform tr)
    {
        if (tr == null) return;
        Rigidbody rb = tr.GetComponent<Rigidbody>();
        if (rb != null) rb.constraints = RigidbodyConstraints.None;
    }

    // ──────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Finds the two actual end-socket world positions of a tie-rod link at rest.
    ///
    /// Strategy (no manual anchor placement required):
    ///   1. If Inspector anchors are provided, use them directly.
    ///   2. Otherwise scan mesh vertices for the two extreme ends along the long axis.
    ///   3. Store inner in rack local space  → auto-translates with rack each frame.
    ///      Store outer in axle local space  → auto-rotates  with axle each frame.
    ///      Store inner AND outer in link local space → used to reconstruct link transform.
    ///
    /// Both link-local socket offsets are needed so SyncLink can place the link pivot
    /// in a way that satisfies BOTH endpoints simultaneously (no drift).
    /// </summary>
    static void CaptureLink(Transform  link,
                             Transform  rackTr,
                             Transform  axle,
                             Transform  rackAnchor,
                             Transform  axleAnchor,
                             out Vector3    innerRackLocal,
                             out Vector3    outerAxleLocal,
                             out Vector3    innerLinkLocal,
                             out Vector3    outerLinkLocal,
                             out Vector3    restDir,
                             out Quaternion restRot,
                             out float      rodLength)
    {
        rodLength = 0f;

        if (link == null || rackTr == null || axle == null)
        {
            innerRackLocal = Vector3.zero;
            outerAxleLocal = Vector3.zero;
            innerLinkLocal = Vector3.zero;
            outerLinkLocal = Vector3.zero;
            restDir        = Vector3.right;
            restRot        = Quaternion.identity;
            return;
        }

        Vector3 innerWorld, outerWorld;

        if (rackAnchor != null && axleAnchor != null)
        {
            // Inspector anchors provided — use them directly.
            innerWorld = rackAnchor.position;
            outerWorld = axleAnchor.position;
        }
        else
        {
            // ── Auto-detect: scan mesh vertices for the two extreme ends ──
            MeshFilter mf   = link.GetComponent<MeshFilter>();
            Vector3    tipA = link.position;
            Vector3    tipB = link.position;

            if (mf != null && mf.sharedMesh != null)
            {
                Vector3[] verts = mf.sharedMesh.vertices;
                Bounds  b        = mf.sharedMesh.bounds;
                Vector3 extents  = b.extents;
                Vector3 longAxis;
                if (extents.x >= extents.y && extents.x >= extents.z)
                    longAxis = Vector3.right;
                else if (extents.y >= extents.x && extents.y >= extents.z)
                    longAxis = Vector3.up;
                else
                    longAxis = Vector3.forward;

                float minProj =  float.MaxValue;
                float maxProj = -float.MaxValue;
                Vector3 minVert = verts[0];
                Vector3 maxVert = verts[0];

                foreach (Vector3 v in verts)
                {
                    float proj = Vector3.Dot(v, longAxis);
                    if (proj < minProj) { minProj = proj; minVert = v; }
                    if (proj > maxProj) { maxProj = proj; maxVert = v; }
                }

                tipA = link.TransformPoint(minVert);
                tipB = link.TransformPoint(maxVert);
            }

            float dA = Vector3.Distance(tipA, rackTr.position);
            float dB = Vector3.Distance(tipB, rackTr.position);
            innerWorld = dA <= dB ? tipA : tipB;
            outerWorld = dA <= dB ? tipB : tipA;
        }

        rodLength = Vector3.Distance(innerWorld, outerWorld);

        // Store each socket in the coordinate space that auto-tracks its parent's motion.
        innerRackLocal = rackTr.InverseTransformPoint(innerWorld);
        outerAxleLocal = axle.InverseTransformPoint(outerWorld);

        // Store both sockets in link local space so SyncLink can satisfy both ends.
        innerLinkLocal = link.InverseTransformPoint(innerWorld);
        outerLinkLocal = link.InverseTransformPoint(outerWorld);

        // Rest direction inner→outer and rest world rotation.
        Vector3 dir = outerWorld - innerWorld;
        restDir     = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.right;
        restRot     = link.rotation;

        Debug.Log($"[SteeringAnimation] CaptureLink '{link.name}': " +
                  $"rodLength={rodLength:F4}  innerWorld={innerWorld:F3}  outerWorld={outerWorld:F3}");
    }

    // ──────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Repositions and reorients a tie-rod link every frame so both socket ends
    /// stay locked to their respective pivot points (rack inner circle, axle outer circle).
    ///
    /// The pivot is placed via midpoint constraint: the midpoint of the two world-space
    /// socket positions must equal the midpoint of the two link-local socket offsets
    /// transformed through the current world rotation and lossyScale. This satisfies
    /// BOTH endpoints simultaneously, eliminating drift and separation.
    /// </summary>
    static void SyncLink(Transform  link,
                         Transform  rackTr,
                         Vector3    innerRackLocal,
                         Vector3    innerLinkLocal,
                         Vector3    outerLinkLocal,
                         Transform  axle,
                         Vector3    outerAxleLocal,
                         Vector3    restDir,
                         Quaternion restRot,
                         float      rodLength)
    {
        if (link == null || rackTr == null || axle == null) return;

        // 1. Recover both socket world positions from their respective parent spaces.
        //    innerRackLocal is in rack-local space → auto-follows rack translation.
        //    outerAxleLocal is in axle-local space → auto-follows axle rotation.
        Vector3 innerWorld = rackTr.TransformPoint(innerRackLocal);
        Vector3 outerWorld = axle.TransformPoint(outerAxleLocal);

        // 2. Clamp: if sockets are farther apart than the rigid rod, pull inner back
        //    along the line toward outer so the fixed rod length is preserved.
        Vector3 diff = outerWorld - innerWorld;
        float   dist = diff.magnitude;
        if (dist > rodLength && dist > 0.0001f)
            innerWorld = outerWorld - diff.normalized * rodLength;

        // 3. Compute current direction; bail out if degenerate.
        Vector3 currentDir = outerWorld - innerWorld;
        if (currentDir.sqrMagnitude < 0.0001f) return;

        // 4. Derive world rotation from geometry only — no per-frame accumulation, no drift.
        //    FromToRotation(restDir, currentDir) is the minimal rotation mapping the rest
        //    long-axis onto the current one. Multiplying by restRot preserves roll.
        Quaternion worldRot = Quaternion.FromToRotation(restDir, currentDir.normalized) * restRot;
        link.rotation = worldRot;

        // 5. Place pivot via midpoint constraint — satisfies both socket endpoints at once.
        //    TransformPoint(p) = position + rotation * (lossyScale ⊙ p)
        //    Midpoint world  = position + rotation * (lossyScale ⊙ midLinkLocal)
        //    → position = midWorld − rotation * (lossyScale ⊙ midLinkLocal)
        Vector3 midWorld     = (innerWorld + outerWorld) * 0.5f;
        Vector3 midLinkLocal = (innerLinkLocal + outerLinkLocal) * 0.5f;
        Vector3 ls           = link.lossyScale;
        Vector3 scaledMid    = new Vector3(
            midLinkLocal.x * ls.x,
            midLinkLocal.y * ls.y,
            midLinkLocal.z * ls.z);
        link.position = midWorld - worldRot * scaledMid;
    }


    // ──────────────────────────────────────────────────────────────────────
    Transform FindPart(string partName) =>
        physicalRoot != null ? physicalRoot.Find(partName) : null;

    void LogMissingParts()
    {
        (Transform tr, string name)[] all =
        {
            (steeringWheelTr,  "steering wheel"),
            (blackeng,         "blackeng"),
            (blackEngrenage1,  "blackEngrenage1"),
            (lower,            "lower"),
            (red,              "red"),
            (arbreBlack2,      "arbreBlack2"),
            (whiteeng1,        "whiteeng1"),
            (whitearbre1,      "whitearbre1"),
            (yellowEngrenage,  "yellow-engrenage"),
            (arbreBlack1,      "arbreBlack1"),
            (rack,             "crémaillère"),
            (rightLink,        "Right-LINK"),
            (leftLink,         "Left-LINK"),
            (rightAxle,        "rightAxle"),
            (leftAxle,         "LeftAxle.001"),
        };

        foreach (var (tr, name) in all)
            if (tr == null)
                Debug.LogWarning($"[SteeringAnimation] Part not found: '{name}'", this);
    }
}
