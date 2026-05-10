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

    [Header("Wheel Pivot")]
    [Tooltip("SteeringWheelPivot on /SteeringSystem1/wheels — notified when animation becomes ready.")]
    public SteeringWheelPivot wheelPivot;

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

    [Tooltip("Empty GO at the socket hole at the outer (knuckle) end of Right-LINK. " +
             "Must be a child of Right-LINK so it translates with it.")]
    public Transform rightLinkKnuckleSocket;

    [Tooltip("Empty GO at the socket hole at the outer (knuckle) end of Left-LINK. " +
             "Must be a child of Left-LINK so it translates with it.")]
    public Transform leftLinkKnuckleSocket;

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

    // Four visible assembly gears, ω_main
    // part.196  → intermediate_gear
    // part.1575 → upper_gear
    // part.1576 → lower_gear
    // fourth    → output_gear
    Transform inputGear;
    Transform intermediateGear;
    Transform upperGear;
    Transform lowerGear;
    Transform outputGear;

    // Column shaft group — all spin at ω_main around their local Z axis.
    Transform columnShaft1;
    Transform columnShaft2;
    Transform intermediateShaft;
    Transform universalJointUpper;
    Transform universalJointLower;

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
    Vector3    rightLinkRestLocalPos;
    Vector3    leftLinkRestLocalPos;
    Quaternion rightAxleRestLocalRot;
    Quaternion leftAxleRestLocalRot;
    float      rightArmXZ;   // XZ radius: knuckle origin → link socket in knuckle local space
    float      leftArmXZ;

    // Rest direction: knuckle kingpin → ball, in knuckle-parent local XZ.
    Vector3    rightRestBallParentDir;
    Vector3    leftRestBallParentDir;

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

    /// <summary>Normalized rack offset in [−1, +1]. Negative = left turn, positive = right turn.</summary>
    public float NormalizedRackOffset => effectiveMaxOffset > 0f ? rackOffset / effectiveMaxOffset : 0f;

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
        rack             = FindPart("rack_bar");
        rightLink        = FindPart("Right-LINK");
        leftLink         = FindPart("Left-LINK");
        rightAxle        = FindPart("steering_knuckle_right");
        leftAxle         = FindPart("steering_knuckle_left");

        // Four visible assembly gears — searched recursively to handle nesting.
        inputGear        = FindPartRecursive("input_gear");
        intermediateGear = FindPartRecursive("intermediate_gear");
        upperGear        = FindPartRecursive("upper_gear");
        lowerGear        = FindPartRecursive("lower_gear");
        outputGear       = FindPartRecursive("output_gear");

        columnShaft1        = FindPartRecursive("column_shaft1");
        columnShaft2        = FindPartRecursive("column_shaft2");
        intermediateShaft   = FindPartRecursive("intermediate_shaft");
        universalJointUpper = FindPartRecursive("universal_joint_upper ");  // trailing space — keep it
        universalJointLower = FindPartRecursive("universal_joint_lower ");  // trailing space — keep it

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

        // Four visible assembly gears (part.196 / part.1575 / part.1576 / output_gear) — same ω_main, same axis.
        // Multiplied by 2 so the visual spin is clearly perceptible without affecting rack speed.
        float gearOmega = omegaMain * 2f;
        Rot(inputGear,        0f, 0f, gearOmega * direction * dt);
        Rot(intermediateGear, 0f, 0f, gearOmega * direction * dt);
        Rot(upperGear,        0f, 0f, gearOmega * direction * dt);
        Rot(lowerGear,        0f, 0f, gearOmega * direction * dt);
        Rot(outputGear,       0f, 0f, gearOmega * direction * dt);

        // ── Steering column — same ω_main, local Z axis ─────────────────────
        // column_shaft1/2 and intermediate_shaft are modelled along local Y (X-rot ≈ 270°),
        // so they spin on local Y. Universal joints are modelled along local Z (Z-rot ≈ 90°).
        // ── Steering column — same ω_main ───────────────────────────────────
        // column_shaft1/2 are modelled along local Y (X-rot ≈ 270°) → spin on local Y.
        // intermediate_shaft sits at a complex arbitrary angle so we derive its spin axis
        // at runtime from the world positions of the two universal joints it connects.
        // universal joints are modelled along local Z (Z-rot ≈ 90°) → spin on local Z.
        Rot(columnShaft1,        0f, omegaMain * direction * dt, 0f);
        Rot(columnShaft2,        0f, omegaMain * direction * dt, 0f);
        if (intermediateShaft != null && universalJointUpper != null && universalJointLower != null)
        {
            Vector3 axisWorld = (universalJointLower.position - universalJointUpper.position).normalized;
            intermediateShaft.Rotate(axisWorld, omegaMain * direction * dt, Space.World);
        }
        Rot(universalJointUpper, 0f, 0f, omegaMain * direction * dt);
        Rot(universalJointLower, 0f, 0f, omegaMain * direction * dt);

        // ── Links: translate 1:1 with rack ──────────────────────────────
        // Links are rigidly bolted to rack ends — same axis, same offset.
        if (rightLink != null) rightLink.localPosition = rightLinkRestLocalPos + Vector3.left * rackOffset;
        if (leftLink  != null) leftLink.localPosition  = leftLinkRestLocalPos  + Vector3.left * rackOffset;

        // ── Knuckles: rotate so white ball stays seated in link socket hole ──
        // Link outer socket (child of Right/Left-LINK) slides with the link.
        // SyncKnuckleToLink rotates the knuckle around its kingpin (Y) so its
        // ball tracks that socket — enforcing the physical ball-in-hole constraint.
        SyncKnuckleToLink(rightAxle, rightLinkKnuckleSocket,
                          rightRestBallParentDir, rightAxleRestLocalRot);
        SyncKnuckleToLink(leftAxle,  leftLinkKnuckleSocket,
                          leftRestBallParentDir,  leftAxleRestLocalRot);
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
                  $"ω_yellow={omegaYellow:F1}°/s  v_rack={rackSpeedLocal:F5} local/s  " +
                  $"rightArm={rightArmXZ:F4}  leftArm={leftArmXZ:F4}");

        // Capture rest poses (post-snap positions guaranteed by AllPartsFullySnapped).
        if (rack      != null) rackRestLocalPos     = rack.localPosition;
        if (rightLink != null) rightLinkRestLocalPos = rightLink.localPosition;
        if (leftLink  != null) leftLinkRestLocalPos  = leftLink.localPosition;
        if (rightAxle != null)
        {
            rightAxleRestWorldRot = rightAxle.rotation;
            rightAxleRestLocalRot = rightAxle.localRotation;
        }
        if (leftAxle != null)
        {
            leftAxleRestWorldRot = leftAxle.rotation;
            leftAxleRestLocalRot = leftAxle.localRotation;
        }

        // Steering arm XZ radius: XZ distance from knuckle origin to its link socket.
        // Determines the knuckle rotation per unit of rack displacement.
        if (rightLinkAxleAnchor != null)
        {
            Vector3 p = rightLinkAxleAnchor.localPosition;
            rightArmXZ = Mathf.Max(0.001f, Mathf.Sqrt(p.x * p.x + p.z * p.z));
        }
        else rightArmXZ = 1f;

        if (leftLinkAxleAnchor != null)
        {
            Vector3 p = leftLinkAxleAnchor.localPosition;
            leftArmXZ = Mathf.Max(0.001f, Mathf.Sqrt(p.x * p.x + p.z * p.z));
        }
        else leftArmXZ = 1f;

        // Snap each link socket to the ball's rest world position so the angle starts
        // at 0°. The socket is a child of the link so it drifts with the link from here.
        if (rightLinkKnuckleSocket != null && rightLinkAxleAnchor != null)
            rightLinkKnuckleSocket.position = rightLinkAxleAnchor.position;
        if (leftLinkKnuckleSocket != null && leftLinkAxleAnchor != null)
            leftLinkKnuckleSocket.position = leftLinkAxleAnchor.position;

        // Capture rest direction knuckle-kingpin → socket (= ball at rest) in parent-local XZ.
        // SyncKnuckleToLink uses this as the 0° baseline each frame.
        if (rightAxle != null && rightLinkKnuckleSocket != null && rightAxle.parent != null)
        {
            Vector3 d = rightAxle.parent.InverseTransformPoint(rightLinkKnuckleSocket.position)
                        - rightAxle.localPosition;
            rightRestBallParentDir = new Vector3(d.x, 0f, d.z).normalized;
        }
        if (leftAxle != null && leftLinkKnuckleSocket != null && leftAxle.parent != null)
        {
            Vector3 d = leftAxle.parent.InverseTransformPoint(leftLinkKnuckleSocket.position)
                        - leftAxle.localPosition;
            leftRestBallParentDir = new Vector3(d.x, 0f, d.z).normalized;
        }

        effectiveMaxOffset = Mathf.Max(0f, rackMaxOffset - rackEndStopMargin);

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

        // Unlock the four visible assembly gears so the Rigidbody doesn't fight our Rotate calls.
        AllowRotation(inputGear);
        AllowRotation(intermediateGear);
        AllowRotation(upperGear);
        AllowRotation(lowerGear);
        AllowRotation(outputGear);

        AllowRotation(columnShaft1);
        AllowRotation(columnShaft2);
        AllowRotation(intermediateShaft);
        AllowRotation(universalJointUpper);
        AllowRotation(universalJointLower);

        if (wheelPivot != null) wheelPivot.OnSteeringReady();
    }

    // ──────────────────────────────────────────────────────────────────────
    static void Rot(Transform tr, float x, float y, float z)
    {
        if (tr != null) tr.Rotate(x, y, z, Space.Self);
    }

    /// <summary>
    /// Rotates a knuckle around its kingpin (local Y) so its white ball joint
    /// tracks the world position of the link's outer socket hole each frame.
    /// restBallDirParent is captured at rest: XZ direction from knuckle pivot
    /// to ball, in the knuckle's parent local space.
    /// </summary>
    void SyncKnuckleToLink(Transform knuckle, Transform linkSocket,
                           Vector3 restBallDirParent, Quaternion restLocalRot)
    {
        if (knuckle == null || linkSocket == null || knuckle.parent == null) return;

        // Direction from knuckle kingpin to link socket, in parent-local XZ.
        Vector3 toSocket   = knuckle.parent.InverseTransformPoint(linkSocket.position)
                             - knuckle.localPosition;
        Vector3 targetDir2D = new Vector3(toSocket.x, 0f, toSocket.z);

        if (targetDir2D.sqrMagnitude < 0.0001f) return;

        // Signed angle from rest direction to current socket direction, around Y.
        float angle = Vector3.SignedAngle(restBallDirParent, targetDir2D, Vector3.up);
        knuckle.localRotation = restLocalRot * Quaternion.AngleAxis(angle, Vector3.up);
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
    /// <summary>Searches only direct children of partsRoot (fastest path).</summary>
    Transform FindPart(string partName) =>
        physicalRoot != null ? physicalRoot.Find(partName) : null;

    /// <summary>
    /// Searches the entire subtree under partsRoot so parts survive hierarchy
    /// nesting changes (e.g. intermediate_gear nested under a group parent).
    /// </summary>
    Transform FindPartRecursive(string partName)
    {
        if (physicalRoot == null || string.IsNullOrEmpty(partName)) return null;
        return FindInChildren(physicalRoot, partName);
    }

    static Transform FindInChildren(Transform root, string partName)
    {
        Transform direct = root.Find(partName);
        if (direct != null) return direct;
        foreach (Transform child in root)
        {
            Transform found = FindInChildren(child, partName);
            if (found != null) return found;
        }
        return null;
    }

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
            (rack,      "rack_bar"),
            (rightLink, "Right-LINK"),
            (leftLink,  "Left-LINK"),
            (rightAxle, "steering_knuckle_right"),
            (leftAxle,  "steering_knuckle_left"),
            (inputGear,        "input_gear"),
            (intermediateGear, "intermediate_gear"),
            (upperGear,        "upper_gear"),
            (lowerGear,        "lower_gear"),
            (outputGear,       "output_gear"),
            (columnShaft1,        "column_shaft1"),
            (columnShaft2,        "column_shaft2"),
            (intermediateShaft,   "intermediate_shaft"),
            (universalJointUpper, "universal_joint_upper "),
            (universalJointLower, "universal_joint_lower "),
        };

        foreach (var (tr, name) in all)
            if (tr == null)
                Debug.LogWarning($"[SteeringAnimation] Part not found: '{name}'", this);
    }
}
