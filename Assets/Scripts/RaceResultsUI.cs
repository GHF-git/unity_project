using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Manages the post-race results overlay.
/// Activated by LapTimer when the lap ends.
/// </summary>
public class RaceResultsUI : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Root GameObject of the results overlay — hidden at runtime start.")]
    public GameObject resultsOverlay;

    [Tooltip("TMP label that shows the final lap time.")]
    public TextMeshProUGUI finalTimeLabel;

    [Header("Scene Names")]
    [Tooltip("Exact build-settings name of the race scene.")]
    public string raceSceneName = "race";

    [Tooltip("Exact build-settings name of the garage / menu scene.")]
    public string garageSceneName = "SampleScene";

    void Awake()
    {
        if (resultsOverlay != null)
            resultsOverlay.SetActive(false);
    }

    /// <summary>Called by LapTimer with the formatted final time string.</summary>
    public void ShowResults(string formattedTime)
    {
        if (finalTimeLabel != null)
            finalTimeLabel.text = formattedTime;

        if (resultsOverlay != null)
            resultsOverlay.SetActive(true);
    }

    /// <summary>Button callback — restarts the race from scratch.</summary>
    public void Restart()
    {
        SceneManager.LoadScene(raceSceneName);
    }

    /// <summary>Button callback — returns to the garage / menu scene.</summary>
    public void ReturnToGarage()
    {
        SceneManager.LoadScene(garageSceneName);
    }
}
