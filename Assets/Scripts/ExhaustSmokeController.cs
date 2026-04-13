using UnityEngine;

/// <summary>
/// Sits on the car root. Drives all exhaust emitters.
/// Smoke triggers on:
///   - W held (wheel-spin / burnout acceleration)
///   - S held while rolling forward above BrakeSmokeSpeedThreshold (hard brake lock)
///   - Space held while rolling forward (handbrake)
/// Supports any number of emitters.
/// </summary>
public class ExhaustSmokeController : MonoBehaviour
{
    [Tooltip("All exhaust smoke emitters to drive simultaneously.")]
    public ExhaustSmoke[] exhaustEmitters = new ExhaustSmoke[0];

    [Tooltip("Minimum forward speed (m/s) required for S or Space to trigger brake smoke.")]
    public float brakeSmokeSpeedThreshold = 3f;

    Rigidbody carRigidbody;
    bool wasEmitting;

    void Awake()
    {
        carRigidbody = GetComponent<Rigidbody>();
    }

    void Update()
    {
        bool accelerating = Input.GetKey(KeyCode.W);
        bool braking      = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.Space);

        // Forward speed along the car's local forward axis (positive = moving forward).
        float forwardSpeed = carRigidbody != null
            ? Vector3.Dot(carRigidbody.linearVelocity, transform.forward)
            : 0f;

        // Brake smoke only fires when actually rolling forward fast enough to lock wheels.
        bool brakingWhileMoving = braking && forwardSpeed > brakeSmokeSpeedThreshold;

        bool shouldEmit = accelerating || brakingWhileMoving;
        if (shouldEmit == wasEmitting) return;
        wasEmitting = shouldEmit;

        foreach (ExhaustSmoke emitter in exhaustEmitters)
        {
            if (emitter == null) continue;
            if (shouldEmit) emitter.StartSmoke();
            else            emitter.StopSmoke();
        }
    }
}
