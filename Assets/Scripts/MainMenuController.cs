using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives the main menu track intro animation and scene loading.
/// Music is handled by MusicManager which persists across all scenes.
/// Attach to the MainMenuController GameObject in MainMenu.unity.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Track Animation")]
    [Tooltip("Transform of the CartoonRaceTrackOval model in the scene.")]
    [SerializeField] private Transform trackTransform;

    [Tooltip("Duration of the zoom-in intro animation in seconds.")]
    [SerializeField] private float introDuration = 3.5f;

    [Tooltip("Easing curve for the intro animation. Defaults to EaseInOut if not set.")]
    [SerializeField] private AnimationCurve introCurve;

    [Tooltip("Target world scale of the track after the intro finishes.")]
    [SerializeField] private Vector3 targetTrackScale = Vector3.one;

    [Tooltip("Starting Z world position of the track (far from camera).")]
    [SerializeField] private float trackStartZ = 80f;

    [Tooltip("Final Z world position of the track after the intro.")]
    [SerializeField] private float trackEndZ = 18f;

    [Tooltip("Degrees per second for the continuous idle Y-axis rotation.")]
    [SerializeField] private float rotationSpeed = 12f;

    [Header("UI")]
    [Tooltip("CanvasGroup on the ButtonsGroup — starts hidden, fades in after intro.")]
    [SerializeField] private CanvasGroup buttonsCanvasGroup;

    [Tooltip("Duration of the button fade-in after the intro completes.")]
    [SerializeField] private float buttonFadeInDuration = 0.8f;

    private const string RaceSceneName = "race";
    private const string BuildSceneName = "SampleScene";

    private static readonly Vector3 StartScale = new Vector3(0.01f, 0.01f, 0.01f);

    private bool _introComplete;

    private void Awake()
    {
        if (introCurve == null || introCurve.length == 0)
            introCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        if (buttonsCanvasGroup != null)
        {
            buttonsCanvasGroup.alpha = 0f;
            buttonsCanvasGroup.interactable = false;
            buttonsCanvasGroup.blocksRaycasts = false;
        }

        if (trackTransform != null)
        {
            Vector3 startPos = trackTransform.localPosition;
            startPos.z = trackStartZ;
            trackTransform.localPosition = startPos;
            trackTransform.localScale = StartScale;
        }
    }

    private void Start()
    {
        StartCoroutine(TrackIntroRoutine());
    }

    private void Update()
    {
        if (!_introComplete || trackTransform == null)
            return;

        trackTransform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }

    private IEnumerator TrackIntroRoutine()
    {
        if (trackTransform == null)
        {
            Debug.LogWarning("[MainMenuController] trackTransform is not assigned.");
            yield break;
        }

        float elapsed = 0f;
        Vector3 startPos = trackTransform.localPosition;
        Vector3 endPos = new Vector3(startPos.x, startPos.y, trackEndZ);

        while (elapsed < introDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / introDuration);
            float curved = introCurve.Evaluate(t);

            trackTransform.localScale = Vector3.LerpUnclamped(StartScale, targetTrackScale, curved);
            trackTransform.localPosition = Vector3.LerpUnclamped(startPos, endPos, curved);

            yield return null;
        }

        trackTransform.localScale = targetTrackScale;
        trackTransform.localPosition = endPos;
        _introComplete = true;

        yield return FadeInButtons();
    }

    private IEnumerator FadeInButtons()
    {
        if (buttonsCanvasGroup == null)
            yield break;

        float elapsed = 0f;

        while (elapsed < buttonFadeInDuration)
        {
            elapsed += Time.deltaTime;
            buttonsCanvasGroup.alpha = Mathf.Clamp01(elapsed / buttonFadeInDuration);
            yield return null;
        }

        buttonsCanvasGroup.alpha = 1f;
        buttonsCanvasGroup.interactable = true;
        buttonsCanvasGroup.blocksRaycasts = true;
    }

    /// <summary>Button callback — loads the race scene.</summary>
    public void LoadRaceScene()
    {
        SceneManager.LoadScene(RaceSceneName);
    }

    /// <summary>Button callback — loads the build and learn (garage) scene.</summary>
    public void LoadBuildScene()
    {
        SceneManager.LoadScene(BuildSceneName);
    }
}
