using UnityEngine;

/// <summary>
/// Formerly rotated the three assembly gears (input_gear / intermediate_gear / lower_gear)
/// independently. That logic has been consolidated into SteeringAnimationController so all
/// gears share the same Update loop, the same speed (omegaMain), and the same rack-limit stop.
///
/// This component is kept as an empty stub so existing Inspector references on SteeringSystem1
/// are not broken. It does nothing at runtime.
/// </summary>
public class SteeringSystemGearRotation : MonoBehaviour { }
