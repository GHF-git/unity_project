using UnityEngine;

/// <summary>
/// Smooth third-person follow camera for racing games.
/// Lags behind the car with independent position and rotation damping,
/// giving the floaty-but-connected feel typical of arcade racing cameras.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The car Transform to follow.")]
    public Transform target;

    [Header("Offset")]
    [Tooltip("Distance behind the car.")]
    public float distance = 6f;

    [Tooltip("Height above the car pivot.")]
    public float height = 2.5f;

    [Header("Damping")]
    [Tooltip("How quickly the camera position catches up to the target (higher = snappier).")]
    public float positionDamping = 5f;

    [Tooltip("How quickly the camera rotation catches up (higher = snappier).")]
    public float rotationDamping = 4f;

    // Desired world position the camera smoothly moves toward.
    Vector3 desiredPosition;

    void LateUpdate()
    {
        if (target == null) return;

        // Desired camera position = behind and above the car based on its current yaw.
        // We only use the car's Y rotation so the camera doesn't pitch/roll with the car.
        Quaternion flatRotation = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
        Vector3 offset = flatRotation * new Vector3(0f, height, -distance);
        desiredPosition = target.position + offset;

        // Smoothly move position.
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            positionDamping * Time.deltaTime
        );

        // Smoothly rotate to look at a point slightly above the car pivot
        // so the car hood is visible and not cut off at the bottom.
        Vector3 lookTarget = target.position + Vector3.up * (height * 0.3f);
        Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationDamping * Time.deltaTime
        );
    }
}
