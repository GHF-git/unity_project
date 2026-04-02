using UnityEngine;

/// <summary>
/// Placed on the start-line GameObject (requires a trigger Collider).
/// Notifies LapTimer each time the car's root GameObject enters the trigger.
/// </summary>
public class StartLineTrigger : MonoBehaviour
{
    [Tooltip("The LapTimer to notify on each crossing.")]
    public LapTimer lapTimer;

    [Tooltip("Tag of the car's root GameObject.")]
    public string carTag = "Player";

    void OnTriggerEnter(Collider other)
    {
        if (lapTimer == null) return;
        if (!other.CompareTag(carTag)) return;

        lapTimer.RegisterLineCross();
    }
}
