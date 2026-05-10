using TMPro;
using UnityEngine;

/// <summary>
/// Reads the car Rigidbody velocity every frame, updates the TMP speed label,
/// and rotates the needle between minAngle and maxAngle based on current speed.
/// </summary>
public class SpeedometerUI : MonoBehaviour
{
    [Tooltip("Rigidbody of the car.")]
    public Rigidbody carRigidbody;

    [Tooltip("TMP label that shows the numeric speed.")]
    public TextMeshProUGUI speedText;

    [Tooltip("RectTransform of the Arrow — rotated on Z to indicate speed.")]
    public RectTransform arrow;

    [Tooltip("Maximum speed the dial is calibrated for (km/h).")]
    public float maxSpeed = 280f;

    [Tooltip("Needle Z angle at speed = 0.")]
    public float minSpeedArrowAngle = 135f;

    [Tooltip("Needle Z angle at speed = maxSpeed.")]
    public float maxSpeedArrowAngle = -135f;

    const float MetersPerSecondToKmh = 3.6f;

    void Update()
    {
        if (carRigidbody == null) return;

        float speedKmh = carRigidbody.linearVelocity.magnitude * MetersPerSecondToKmh;

        if (speedText != null)
            speedText.text = ((int)speedKmh) + " km/h";

        if (arrow != null)
            arrow.localEulerAngles = new Vector3(0f, 0f,
                Mathf.Lerp(minSpeedArrowAngle, maxSpeedArrowAngle, speedKmh / maxSpeed));
    }
}
