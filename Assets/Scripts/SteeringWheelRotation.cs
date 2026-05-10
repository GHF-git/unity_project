using UnityEngine;

/// <summary>
/// Rotates the steering wheel, the column rectangle, and the three gear pieces
/// based on horizontal input, only while first-person mode is active.
/// Set <see cref="isFirstPerson"/> from <see cref="CameraSwitcher"/>.
/// </summary>
public class SteeringWheelRotation : MonoBehaviour
{
    [Tooltip("Set to true by CameraSwitcher when first-person is active.")]
    public bool isFirstPerson = false;

    [Tooltip("Maximum rotation angle in degrees (applied symmetrically left and right).")]
    public float maxAngle = 180f;

    [Tooltip("Lerp speed for smoothing all rotations.")]
    public float smoothSpeed = 5f;

    [Header("Additional rotating parts")]
    [Tooltip("The rectangle object on the steering column (part.294).")]
    public GameObject columnRectangle;

    [Tooltip("Gear piece below the steering wheel (part.196).")]
    public GameObject gearPiece1;

    [Tooltip("Gear piece below the steering wheel (part.1575).")]
    public GameObject gearPiece2;

    [Tooltip("Gear piece below the steering wheel (part.1576).")]
    public GameObject gearPiece3;

    // Smoothed angle shared by all parts.
    private float _currentAngle;

    // Rest rotations captured on Awake for each part.
    private Quaternion _restRotation;
    private Quaternion _restRectangle;
    private Quaternion _restGear1;
    private Quaternion _restGear2;
    private Quaternion _restGear3;

    private void Awake()
    {
        _restRotation  = transform.localRotation;
        _restRectangle = columnRectangle != null ? columnRectangle.transform.localRotation : Quaternion.identity;
        _restGear1     = gearPiece1     != null ? gearPiece1.transform.localRotation     : Quaternion.identity;
        _restGear2     = gearPiece2     != null ? gearPiece2.transform.localRotation     : Quaternion.identity;
        _restGear3     = gearPiece3     != null ? gearPiece3.transform.localRotation     : Quaternion.identity;
    }

    private void Update()
    {
        float targetAngle = 0f;

        if (isFirstPerson)
        {
            float input = Input.GetAxis("Horizontal");
            targetAngle = input * maxAngle;
        }

        // Smooth towards target — returns smoothly to zero when not first-person.
        _currentAngle = Mathf.Lerp(_currentAngle, targetAngle, Time.deltaTime * smoothSpeed);

        // part.622 (script host) is upright — spins on local Y.
        transform.localRotation = _restRotation * Quaternion.AngleAxis(_currentAngle, Vector3.up);

        // part.294 (steering wheel ring) and gears are tipped 90° on X —
        // their spin axis is local Z, so use Vector3.forward.
        Quaternion deltaZ = Quaternion.AngleAxis(_currentAngle, Vector3.forward);

        if (columnRectangle != null)
            columnRectangle.transform.localRotation = _restRectangle * deltaZ;

        if (gearPiece1 != null)
            gearPiece1.transform.localRotation = _restGear1 * deltaZ;

        if (gearPiece2 != null)
            gearPiece2.transform.localRotation = _restGear2 * deltaZ;

        if (gearPiece3 != null)
            gearPiece3.transform.localRotation = _restGear3 * deltaZ;
    }
}
