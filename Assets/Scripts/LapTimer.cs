using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using TMPro;

/// <summary>
/// Starts when CountdownTimer calls StartTimer() (on GO!),
/// stops when the car crosses the start-line trigger a second time,
/// updates the HUD timer bar, plays the finish video, and shows the results screen.
/// </summary>
public class LapTimer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The TMP label inside the timer bar that ticks while racing.")]
    public TextMeshProUGUI timerLabel;

    [Tooltip("The timer bar root GameObject — hidden until GO! fires.")]
    public GameObject timerBar;

    [Tooltip("VideoPlayer that plays the finish clip.")]
    public VideoPlayer finishVideo;

    [Tooltip("Results screen controller shown when the lap finishes.")]
    public RaceResultsUI resultsUI;

    [Header("Settings")]
    [Tooltip("Seconds to wait after crossing the finish line before showing the results overlay.")]
    public float resultsDelaySeconds = 2.5f;

    // State
    bool isRunning;
    bool isFinished;
    float elapsedSeconds;

    // First crossing starts the lap; second crossing ends it.
    int crossCount;

    void Awake()
    {
        if (timerBar != null)
            timerBar.SetActive(false);
    }

    void Update()
    {
        if (!isRunning || isFinished) return;

        elapsedSeconds += Time.deltaTime;
        UpdateLabel();
    }

    /// <summary>Called by CountdownTimer when GO! fires.</summary>
    public void StartTimer()
    {
        elapsedSeconds = 0f;
        crossCount     = 0;
        isFinished     = false;
        isRunning      = true;

        if (timerBar != null)
            timerBar.SetActive(true);

        UpdateLabel();
    }

    /// <summary>
    /// Called by StartLineTrigger on each car crossing.
    /// First crossing is the lap start gate; second crossing ends the lap.
    /// </summary>
    public void RegisterLineCross()
    {
        if (!isRunning || isFinished) return;

        crossCount++;

        if (crossCount >= 2)
        {
            isRunning  = false;
            isFinished = true;
            UpdateLabel();

            if (finishVideo != null)
            {
                finishVideo.gameObject.SetActive(true);
                finishVideo.Play();
            }

            if (resultsUI != null)
                StartCoroutine(ShowResultsDelayed(FormatTime()));
        }
    }

    IEnumerator ShowResultsDelayed(string formattedTime)
    {
        yield return new WaitForSeconds(resultsDelaySeconds);
        resultsUI.ShowResults(formattedTime);
    }

    /// <summary>Returns the formatted elapsed time string MM:SS:cc.</summary>
    string FormatTime()
    {
        int minutes      = (int)(elapsedSeconds / 60f);
        int seconds      = (int)(elapsedSeconds % 60f);
        int centiseconds = (int)((elapsedSeconds * 100f) % 100f);
        return string.Format("{0:00}:{1:00}:{2:00}", minutes, seconds, centiseconds);
    }

    void UpdateLabel()
    {
        if (timerLabel == null) return;
        timerLabel.text = FormatTime();
    }
}
