using UnityEngine;
using TMPro;

/// <summary>
/// Drives the speedometer needle and speed label from the car's Rigidbody velocity.
/// The needle pivot (Arrow) rotates on its Z axis:
///   -135 deg = 0 km/h  (left stop)
///   +135 deg = maxSpeedKmh  (right stop)
/// </summary>
public class SpeedometerUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The car's Rigidbody — drag the car root here.")]
    public Rigidbody carRigidbody;

    [Tooltip("The RectTransform used as the needle pivot (Arrow GameObject).")]
    public RectTransform needlePivot;

    [Tooltip("TextMeshPro label that shows the numeric speed.")]
    public TextMeshProUGUI speedLabel;

    [Header("Settings")]
    [Tooltip("Speed in km/h that corresponds to the full-scale needle position.")]
    public float maxSpeedKmh = 180f;

    [Tooltip("Needle Z angle when speed = 0 km/h. " +
             "Unity UI Z rotates counter-clockwise: 135 = bottom-left (standard 0 position).")]
    public float minAngle = 135f;

    [Tooltip("Needle Z angle when speed = maxSpeedKmh. " +
             "-135 = bottom-right (standard max position).")]
    public float maxAngle = -135f;

    [Tooltip("How smoothly the needle follows the real speed (higher = snappier).")]
    public float needleDamping = 8f;

    // Current displayed angle, smoothly interpolated.
    float currentAngle;

    void Awake()
    {
        currentAngle = minAngle;
    }

    void Update()
    {
        if (carRigidbody == null || needlePivot == null) return;

        // Signed speed along the car's forward axis so reversing reads near 0.
        float forwardSpeed = Vector3.Dot(carRigidbody.linearVelocity, carRigidbody.transform.forward);
        float speedKmh = Mathf.Max(0f, forwardSpeed * 3.6f);

        // Map speed to needle angle.
        float targetAngle = Mathf.Lerp(minAngle, maxAngle, Mathf.Clamp01(speedKmh / maxSpeedKmh));

        // Smooth needle movement — fast rise, same fall.
        currentAngle = Mathf.Lerp(currentAngle, targetAngle, needleDamping * Time.deltaTime);

        needlePivot.localRotation = Quaternion.Euler(0f, 0f, currentAngle);

        if (speedLabel != null)
            speedLabel.text = Mathf.RoundToInt(speedKmh) + " km/h";
    }
}
