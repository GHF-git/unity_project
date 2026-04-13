using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Shows a world-space video quad of exhaust fumes when the player accelerates (W key).
/// Controls the entire quad GameObject's active state so nothing is visible between frames.
/// The quad must be a child of the car so it moves with it automatically.
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
public class ExhaustFume : MonoBehaviour
{
    VideoPlayer videoPlayer;

    void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        // Deactivate the quad immediately — before the first rendered frame.
        gameObject.SetActive(false);
    }

    void OnEnable()
    {
        videoPlayer.Play();
    }

    void OnDisable()
    {
        videoPlayer.Stop();
    }

    // Update runs on the parent car — activation is driven from there.
    // But since this script is on the quad itself, we use a workaround:
    // keep a static reference driven by a separate driver on the car.
    // Simpler: the driver script calls SetActive directly.
}
