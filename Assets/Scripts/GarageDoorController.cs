using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Attach to the GarageDoor root.
///
/// Simulates a real sectional overhead garage door:
///   - Each DoorSection* independently follows the L-shaped track of frame1.
///   - Phase 1 (vertical):  section rises straight up along the frame rail.
///   - Phase 2 (bend):      section rotates 90° through a circular arc, going from
///                          vertical to horizontal as it rounds the top of the frame.
///   - Phase 3 (horizontal): section slides along the ceiling rail into the garage.
///
/// Because every section has a different starting offset on the track, the top section
/// reaches the bend first — exactly like a real garage door.
///
/// O = open | C = close
/// frame1 and frame2 are never touched.
/// </summary>
public class GarageDoorController : MonoBehaviour
{
    [Header("Alarm Lights")]
    [Tooltip("All AlarmLightController instances that should activate while the door is moving.")]
    public AlarmLightController[] alarmLights = Array.Empty<AlarmLightController>();

    [Header("Steering Gate")]
    [Tooltip("Door cannot be opened until steering assembly is complete.")]
    public SteeringAssemblyController steeringAssembly;

    [Header("Events")]
    [Tooltip("Fired when the door begins opening or closing.")]
    public UnityEvent onDoorStartedMoving;

    [Tooltip("Fired when the door finishes opening or closing.")]
    public UnityEvent onDoorStoppedMoving;

    [Header("Speed")]
    [Tooltip("Local track units per second. 5 ≈ ~12 s full travel for a 28-unit door.")]
    public float speed = 5f;

    [Header("Track Shape")]
    [Tooltip("Extra local Y units added on top of the section span before the bend starts. " +
             "Increase this to push the bend point higher up the frame to match frame1.")]
    public float extraVerticalHeight = 0f;

    [Tooltip("Radius of the bend arc in local units. Determines how tight the corner is.")]
    public float bendRadius = 3f;

    [Tooltip("How many local units each section travels along the ceiling rail when fully open.")]
    public float horizontalLength = 20f;

    [Tooltip("Local rotation X (degrees) a section has when lying flat on the ceiling rail. " +
             "Try 180 if sections face the wrong way after the bend.")]
    public float horizontalRotationX = 0f;

    // ── Runtime data ────────────────────────────────────────────────────────
    Transform[] sections;

    // Closed-state position of each section.
    float[] closedY;
    float[] closedZ;

    // Each section's starting offset along the track (0 = bottom section).
    float[] initialTrackPos;

    // Track geometry (computed in Start).
    float minClosedY;       // closed Y of the bottom-most section
    float verticalLength;   // length of the vertical track portion
    float maxDisplacement;  // total track displacement for fully open

    // Shared door displacement (0 = closed, maxDisplacement = fully open).
    float displacement;

    enum State { Closed, Open, Opening, Closing }
    State state = State.Closed;

    // ── Unity ───────────────────────────────────────────────────────────────

    void Start()
    {
        // Collect every child named "DoorSection*".
        var list = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("DoorSection"))
                list.Add(child);
        }
        sections        = list.ToArray();
        closedY         = new float[sections.Length];
        closedZ         = new float[sections.Length];
        initialTrackPos = new float[sections.Length];

        float minY = float.MaxValue;
        float maxY = float.MinValue;

        for (int i = 0; i < sections.Length; i++)
        {
            closedY[i] = sections[i].localPosition.y;
            closedZ[i] = sections[i].localPosition.z;
            if (closedY[i] < minY) minY = closedY[i];
            if (closedY[i] > maxY) maxY = closedY[i];
        }

        minClosedY     = minY;
        verticalLength = (maxY - minY) + extraVerticalHeight; // span + extra height before bend

        // Each section's starting track offset (0 for the bottom, span for the top).
        for (int i = 0; i < sections.Length; i++)
            initialTrackPos[i] = closedY[i] - minY;

        // The bottom section (offset 0) must travel the full vertical portion,
        // through the bend arc (π/2 * bendRadius), and along the ceiling.
        float bendArcLength = (Mathf.PI / 2f) * bendRadius;
        maxDisplacement     = verticalLength + bendArcLength + horizontalLength;

        displacement = 0f;
    }

    void Update()
    {
        HandleInput();
        Animate();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Toggles the door open or closed. Called by DoorButtonInteraction when
    /// the player clicks the physical button in the scene.
    /// </summary>
    public void Toggle()
    {
        if (state == State.Closed || state == State.Closing)
        {
            if (steeringAssembly != null && !steeringAssembly.IsSteeringComplete)
                return;

            state = State.Opening;
            StartAlarms();
        }
        else if (state == State.Open || state == State.Opening)
        {
            state = State.Closing;
            StartAlarms();
        }
    }

    // ── Input ────────────────────────────────────────────────────────────────

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.O) && state != State.Open && state != State.Opening)
        {
            state = State.Opening;
            StartAlarms();
        }

        if (Input.GetKeyDown(KeyCode.C) && state != State.Closed && state != State.Closing)
        {
            state = State.Closing;
            StartAlarms();
        }
    }

    // ── Animation ────────────────────────────────────────────────────────────

    void Animate()
    {
        if (state == State.Open || state == State.Closed) return;

        displacement += (state == State.Opening ? 1f : -1f) * speed * Time.deltaTime;
        displacement  = Mathf.Clamp(displacement, 0f, maxDisplacement);

        if (displacement >= maxDisplacement)
        {
            state = State.Open;
            StopAlarms();
        }

        if (displacement <= 0f)
        {
            state = State.Closed;
            StopAlarms();
        }

        float bendArcLength = (Mathf.PI / 2f) * bendRadius;

        for (int i = 0; i < sections.Length; i++)
        {
            // Where is this section along the track right now?
            float t = initialTrackPos[i] + displacement;

            Vector3 pos   = sections[i].localPosition;
            Vector3 euler = sections[i].localEulerAngles;

            if (t <= verticalLength)
            {
                // ── Phase 1: vertical rise ───────────────────────────────────
                // Section travels straight up; rotation unchanged (stays vertical).
                pos.y   = minClosedY + t;
                pos.z   = closedZ[i];
                euler.x = 270f;
            }
            else if (t <= verticalLength + bendArcLength)
            {
                // ── Phase 2: circular bend ───────────────────────────────────
                // Section travels along a 90° arc, rotating from vertical to horizontal.
                // Arc centre is at (Y = topOfVertical, Z = closedZ - bendRadius).
                float arcTravel    = t - verticalLength;         // 0 → bendArcLength
                float theta        = arcTravel / bendRadius;     // 0 → π/2 radians

                pos.y   = minClosedY + verticalLength + Mathf.Sin(theta) * bendRadius;
                pos.z   = closedZ[i] - (1f - Mathf.Cos(theta)) * bendRadius;
                euler.x = Mathf.LerpAngle(270f, horizontalRotationX, theta / (Mathf.PI / 2f));
            }
            else
            {
                // ── Phase 3: horizontal ceiling rail ─────────────────────────
                // Section lies flat and slides into the garage.
                float horizTravel = t - (verticalLength + bendArcLength); // 0 → horizontalLength

                pos.y   = minClosedY + verticalLength + bendRadius;
                pos.z   = closedZ[i] - bendRadius - horizTravel;
                euler.x = horizontalRotationX;
            }

            sections[i].localPosition    = pos;
            sections[i].localEulerAngles = euler;
        }
    }

    // ── Alarm helpers ────────────────────────────────────────────────────────

    void StartAlarms()
    {
        foreach (AlarmLightController alarm in alarmLights)
        {
            if (alarm != null)
                alarm.Activate();
        }
        onDoorStartedMoving?.Invoke();
    }

    void StopAlarms()
    {
        foreach (AlarmLightController alarm in alarmLights)
        {
            if (alarm != null)
                alarm.Deactivate();
        }
        onDoorStoppedMoving?.Invoke();
    }
}
