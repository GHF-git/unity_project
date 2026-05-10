using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton that persists across all scenes, playing and looping the background
/// music. Volume is adjusted automatically per scene on load.
/// Place this on a GameObject in MainMenu.unity — it carries itself forward.
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Audio")]
    [Tooltip("The background music clip to loop across all scenes.")]
    [SerializeField] private AudioClip musicClip;

    [Tooltip("Default volume used in scenes that are not listed below.")]
    [SerializeField] private float defaultVolume = 1f;

    [Header("Per-Scene Volume Overrides")]
    [Tooltip("Override volume for specific scenes. Scene name must match exactly.")]
    [SerializeField] private List<SceneVolumeEntry> sceneVolumeOverrides = new List<SceneVolumeEntry>
    {
        new SceneVolumeEntry { sceneName = "SampleScene", volume = 0.03f }
    };

    [Tooltip("Duration of the volume crossfade when switching scenes.")]
    [SerializeField] private float fadeDuration = 1f;

    private AudioSource _audioSource;
    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        // Singleton guard — destroy duplicates that arise when returning to MainMenu
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _audioSource = GetComponent<AudioSource>();

        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.clip = musicClip;
        _audioSource.loop = true;
        _audioSource.spatialBlend = 0f;
        _audioSource.priority = 128;
        _audioSource.volume = defaultVolume;

        if (!_audioSource.isPlaying)
            _audioSource.Play();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        float targetVolume = GetVolumeForScene(scene.name);

        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);

        _fadeCoroutine = StartCoroutine(FadeVolume(targetVolume));
    }

    private float GetVolumeForScene(string sceneName)
    {
        foreach (SceneVolumeEntry entry in sceneVolumeOverrides)
        {
            if (entry.sceneName == sceneName)
                return entry.volume;
        }

        return defaultVolume;
    }

    private IEnumerator FadeVolume(float targetVolume)
    {
        float startVolume = _audioSource.volume;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            _audioSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / fadeDuration);
            yield return null;
        }

        _audioSource.volume = targetVolume;
    }

    [System.Serializable]
    public class SceneVolumeEntry
    {
        public string sceneName;
        [Range(0f, 1f)]
        public float volume = 1f;
    }
}
