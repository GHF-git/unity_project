using UnityEngine;

/// <summary>
/// Realistic car controller using WheelColliders.
/// Rear-wheel drive with progressive torque build-up (accelerator feel).
/// Attach to the root car GameObject that has a Rigidbody.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider wheelColliderFL;
    public WheelCollider wheelColliderFR;
    public WheelCollider wheelColliderRL;
    public WheelCollider wheelColliderRR;

    [Header("Wheel Meshes")]
    public Transform wheelMeshFL;
    public Transform wheelMeshFR;
    public Transform wheelMeshRL;
    public Transform wheelMeshRR;

    [Header("Engine")]
    [Tooltip("Maximum torque applied to rear wheels (Nm).")]
    public float maxMotorTorque = 3000f;

    [Tooltip("How fast torque builds up when pressing W (seconds to reach max).")]
    public float torqueRampTime = 2f;

    [Tooltip("Maximum speed in km/h. Torque cuts off above this.")]
    public float maxSpeedKmh = 180f;

    [Header("Brakes")]
    [Tooltip("Braking torque applied when pressing the opposite direction.")]
    public float brakeTorque = 4000f;

    [Tooltip("Engine braking torque applied when coasting (no input).")]
    public float engineBrakeTorque = 300f;

    [Header("Steering")]
    [Tooltip("Maximum steering angle in degrees.")]
    public float maxSteeringAngle = 30f;

    [Tooltip("Speed (km/h) at which steering angle is halved for stability.")]
    public float steeringSpeedReduction = 80f;

    // Set to true by CountdownTimer to prevent input until GO! fires.
    [HideInInspector] public bool IsLocked = false;

    // Internal state
    Rigidbody carRigidbody;
    float currentTorque;

    // Baked rotation offsets from the FBX — each wheel mesh has its own axis convention.
    Quaternion offsetFL, offsetFR, offsetRL, offsetRR;

    void Awake()
    {
        carRigidbody = GetComponent<Rigidbody>();

        // Store the initial local rotations of the wheel meshes.
        // The FBX exports them with a non-standard roll axis, so we carry
        // this offset and apply it on top of the WheelCollider world pose.
        if (wheelMeshFL != null) offsetFL = wheelMeshFL.localRotation;
        if (wheelMeshFR != null) offsetFR = wheelMeshFR.localRotation;
        if (wheelMeshRL != null) offsetRL = wheelMeshRL.localRotation;
        if (wheelMeshRR != null) offsetRR = wheelMeshRR.localRotation;
    }

    void FixedUpdate()
    {
        // While the countdown is active, hold full brakes and block all input.
        if (IsLocked)
        {
            currentTorque = 0f;
            wheelColliderFL.motorTorque = 0f;
            wheelColliderFR.motorTorque = 0f;
            wheelColliderRL.motorTorque = 0f;
            wheelColliderRR.motorTorque = 0f;
            wheelColliderFL.brakeTorque = brakeTorque;
            wheelColliderFR.brakeTorque = brakeTorque;
            wheelColliderRL.brakeTorque = brakeTorque;
            wheelColliderRR.brakeTorque = brakeTorque;
            SyncWheelMeshes();
            return;
        }

        float input = 0f;
        if (Input.GetKey(KeyCode.W)) input = 1f;
        else if (Input.GetKey(KeyCode.S)) input = -1f;

        float steerInput = Input.GetAxis("Horizontal");
        bool handbrake = Input.GetKey(KeyCode.Space);

        // Signed speed along the car's own forward axis (positive = forward, negative = backward).
        float forwardSpeed = Vector3.Dot(carRigidbody.linearVelocity, transform.forward);
        float speedKmh = Mathf.Abs(forwardSpeed) * 3.6f;

        ApplyDrive(input, forwardSpeed, speedKmh, handbrake);
        ApplySteering(steerInput, speedKmh);
        SyncWheelMeshes();
    }

    /// <summary>
    /// Unified drive method that handles forward, reverse, and directional braking.
    /// Pressing the opposite direction always brakes first — motor only engages once
    /// the car has fully stopped, giving a real accelerator/brake pedal feel.
    /// </summary>
    void ApplyDrive(float input, float forwardSpeed, float speedKmh, bool handbrake)
    {
        float torqueStep = (maxMotorTorque / torqueRampTime) * Time.fixedDeltaTime;

        float motor = 0f;
        float brake = 0f;

        if (handbrake)
        {
            // Space bar = full stop, no motor.
            currentTorque = 0f;
            brake = brakeTorque;
        }
        else if (input > 0f)
        {
            if (forwardSpeed < -StopThreshold)
            {
                // Moving backward — W acts as a brake until stopped.
                currentTorque = 0f;
                brake = brakeTorque;
            }
            else
            {
                // Stopped or already moving forward — accelerate.
                brake = 0f;
                if (speedKmh < maxSpeedKmh)
                    currentTorque = Mathf.MoveTowards(currentTorque, maxMotorTorque, torqueStep);
                motor = currentTorque;
            }
        }
        else if (input < 0f)
        {
            if (forwardSpeed > StopThreshold)
            {
                // Moving forward — S acts as a brake until stopped.
                currentTorque = 0f;
                brake = brakeTorque;
            }
            else
            {
                // Stopped or already moving backward — reverse.
                brake = 0f;
                if (speedKmh < maxSpeedKmh)
                    currentTorque = Mathf.MoveTowards(currentTorque, -maxMotorTorque, torqueStep);
                motor = currentTorque;
            }
        }
        else
        {
            // No input — coast with light engine braking.
            currentTorque = Mathf.MoveTowards(currentTorque, 0f, torqueStep * 2f);
            brake = engineBrakeTorque;
        }

        // Rear-wheel drive.
        wheelColliderRL.motorTorque = motor;
        wheelColliderRR.motorTorque = motor;
        wheelColliderFL.motorTorque = 0f;
        wheelColliderFR.motorTorque = 0f;

        wheelColliderFL.brakeTorque = brake;
        wheelColliderFR.brakeTorque = brake;
        wheelColliderRL.brakeTorque = brake;
        wheelColliderRR.brakeTorque = brake;
    }

    /// <summary>
    /// Reduces steering angle at high speed for stability.
    /// </summary>
    void ApplySteering(float steerInput, float speedKmh)
    {
        float speedFactor = Mathf.Clamp01(speedKmh / steeringSpeedReduction);
        float dynamicMaxAngle = Mathf.Lerp(maxSteeringAngle, maxSteeringAngle * 0.4f, speedFactor);
        float angle = steerInput * dynamicMaxAngle;

        wheelColliderFL.steerAngle = angle;
        wheelColliderFR.steerAngle = angle;
    }

    // Speed threshold in m/s below which the car is considered stopped
    // and the motor direction can switch.
    const float StopThreshold = 0.3f;

    /// <summary>
    /// Syncs each wheel mesh position and rotation to its WheelCollider,
    /// preserving the FBX baked axis offset so the mesh rolls on the correct axis.
    /// </summary>
    void SyncWheelMeshes()
    {
        SyncWheel(wheelColliderFL, wheelMeshFL, offsetFL);
        SyncWheel(wheelColliderFR, wheelMeshFR, offsetFR);
        SyncWheel(wheelColliderRL, wheelMeshRL, offsetRL);
        SyncWheel(wheelColliderRR, wheelMeshRR, offsetRR);
    }

    void SyncWheel(WheelCollider col, Transform mesh, Quaternion offset)
    {
        if (col == null || mesh == null) return;

        // Only extract the rotation from GetWorldPose — never touch position.
        // The FBX hierarchy already places each wheel mesh at the correct
        // visual position; moving it to GetWorldPose's position would displace
        // it to the WheelCollider's physics origin which doesn't match the mesh.
        col.GetWorldPose(out _, out Quaternion worldRot);

        // Convert world physics rotation to the mesh parent's local space,
        // then apply the FBX baked axis offset so spin and steer act on the
        // correct axis for this mesh.
        Quaternion parentInverse = Quaternion.Inverse(mesh.parent.rotation);
        Quaternion localPhysicsRot = parentInverse * worldRot;

        mesh.localRotation = localPhysicsRot * offset;
    }
}
