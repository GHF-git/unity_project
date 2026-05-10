using UnityEngine;

/// <summary>
/// Switches the main camera between first-person and third-person views.
/// Press '1' for first-person, '3' for third-person.
///
/// During first-person the camera tracks <see cref="firstPersonAnchor"/> every
/// LateUpdate frame — the anchor is a child of the car, so the camera rides
/// with it continuously even while the car is moving and rotating.
/// During third-person, <see cref="CameraFollow"/> takes over as normal.
///
/// Transitions are smooth-stepped over <see cref="transitionDuration"/> seconds.
/// The lerp destination is read live from the anchor every frame so the target
/// never freezes mid-transition while the car is moving.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraSwitcher : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("View Anchors")]
    [Tooltip("Direct child of the car — camera tracks this every frame in first-person.")]
    public Transform firstPersonAnchor;

    [Tooltip("Direct child of the car — used as the lerp destination when returning to third-person.")]
    public Transform thirdPersonAnchor;

    [Header("Cockpit")]
    [Tooltip("CockpitOverlay root — enabled in first-person, disabled in third-person.")]
    public GameObject cockpitOverlay;

    [Tooltip("Steering wheel rotation script — activated only in first-person.")]
    public SteeringWheelRotation steeringWheel;

    [Header("Transition")]
    [Tooltip("Duration in seconds for the smooth-step lerp between views.")]
    public float transitionDuration = 0.3f;

    // ── Private state ──────────────────────────────────────────────────────────

    CameraFollow cameraFollow;

    enum ViewMode { ThirdPerson, FirstPerson }
    ViewMode currentMode = ViewMode.ThirdPerson;

    bool  isTransitioning;
    float transitionElapsed;

    // World-space snapshot of where the camera was when this transition began.
    Vector3    fromPosition;
    Quaternion fromRotation;

    // ── Unity lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        cameraFollow = GetComponent<CameraFollow>();
    }

    void LateUpdate()
    {
        HandleInput();

        if (isTransitioning)
            TickTransition();
        else if (currentMode == ViewMode.FirstPerson)
            TrackFirstPersonAnchor();
        // Third-person settled state: CameraFollow handles positioning on its own.
    }

    // ── Input ──────────────────────────────────────────────────────────────────

    /// <summary>Reads '1' and '3' keys and starts the appropriate transition.</summary>
    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) && currentMode != ViewMode.FirstPerson)
            BeginTransition(ViewMode.FirstPerson);

        if (Input.GetKeyDown(KeyCode.Alpha3) && currentMode != ViewMode.ThirdPerson)
            BeginTransition(ViewMode.ThirdPerson);
    }

    // ── Steady first-person tracking ───────────────────────────────────────────

    /// <summary>
    /// Locks the camera to the anchor's live world transform every frame.
    /// Because the anchor is parented to the car, the camera automatically
    /// follows the car's position and rotation with zero lag.
    /// </summary>
    void TrackFirstPersonAnchor()
    {
        transform.position = firstPersonAnchor.position;
        transform.rotation = firstPersonAnchor.rotation;
    }

    // ── Transition ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Snapshots the camera's current world pose and begins the lerp.
    /// Disables CameraFollow immediately when entering first-person so the
    /// two systems never write to the camera transform at the same time.
    /// </summary>
    void BeginTransition(ViewMode target)
    {
        currentMode       = target;
        isTransitioning   = true;
        transitionElapsed = 0f;

        fromPosition = transform.position;
        fromRotation = transform.rotation;

        if (target == ViewMode.FirstPerson && cameraFollow != null)
            cameraFollow.enabled = false;

        if (cockpitOverlay != null)
            cockpitOverlay.SetActive(target == ViewMode.FirstPerson);

        if (steeringWheel != null)
            steeringWheel.isFirstPerson = (target == ViewMode.FirstPerson);
    }

    /// <summary>
    /// Advances the lerp each LateUpdate. The destination is sampled from the
    /// live anchor transform every frame so the target moves with the car
    /// throughout the transition — no stale world-space snapshot.
    /// Re-enables CameraFollow at the end of a third-person transition.
    /// </summary>
    void TickTransition()
    {
        transitionElapsed += Time.deltaTime;
        float t      = Mathf.Clamp01(transitionElapsed / transitionDuration);
        float smooth = t * t * (3f - 2f * t);   // smooth-step ease-in/out

        // Sample the live anchor this frame — car may have moved since BeginTransition.
        Transform activeAnchor = currentMode == ViewMode.FirstPerson
            ? firstPersonAnchor
            : thirdPersonAnchor;

        transform.position = Vector3.Lerp(fromPosition, activeAnchor.position, smooth);
        transform.rotation = Quaternion.Slerp(fromRotation, activeAnchor.rotation, smooth);

        if (t >= 1f)
        {
            isTransitioning = false;

            if (currentMode == ViewMode.ThirdPerson && cameraFollow != null)
                cameraFollow.enabled = true;
        }
    }
}
