using UnityEngine;

/// <summary>
/// Generates a short procedural chime sound via OnAudioFilterRead — no AudioClip required.
/// Requires an AudioSource on the same GameObject (no clip, playOnAwake: false).
/// Call Play() to trigger the chime.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class EncouragementChime : MonoBehaviour
{
    // ── Constants ─────────────────────────────────────────────────────────────

    private const float ChimeFrequency  = 880f;   // A5 — bright, celebratory
    private const float ChimeDuration   = 0.6f;   // seconds
    private const float InitialAmplitude = 0.5f;

    // ── Private state (audio-thread safe) ─────────────────────────────────────

    private AudioSource _audioSource;

    private volatile int   _samplesRemaining = 0;
    private volatile bool  _triggerPlay      = false;

    // These are only written on trigger (main thread) or read/written on audio thread.
    // Reset happens before _samplesRemaining is set, so ordering is safe.
    private double _phase     = 0.0;
    private float  _amp       = 0f;
    private float  _decayRate = 0f;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;

        // Pre-compute decay rate so amplitude reaches ~1% of initial at end of chime.
        int totalSamples = Mathf.RoundToInt(ChimeDuration * AudioSettings.outputSampleRate);
        _decayRate = totalSamples > 0
            ? Mathf.Pow(0.01f, 1f / totalSamples)
            : 1f;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Triggers the chime. Safe to call from the main thread at any time.</summary>
    public void Play()
    {
        _phase            = 0.0;
        _amp              = InitialAmplitude;
        _samplesRemaining = Mathf.RoundToInt(ChimeDuration * AudioSettings.outputSampleRate);

        if (!_audioSource.isPlaying)
            _audioSource.Play();
    }

    // ── Audio thread ──────────────────────────────────────────────────────────

    /// <summary>
    /// Called by Unity's audio system on a background thread.
    /// Fills the buffer with a decaying sine wave while _samplesRemaining > 0.
    /// NEVER call Unity APIs here.
    /// </summary>
    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (_samplesRemaining <= 0) return;

        double phaseIncrement = 2.0 * System.Math.PI * ChimeFrequency
                                / AudioSettings.outputSampleRate;

        int sampleCount = data.Length / channels;

        for (int s = 0; s < sampleCount; s++)
        {
            if (_samplesRemaining <= 0) break;

            float sample = _amp * (float)System.Math.Sin(_phase);
            _phase += phaseIncrement;
            _amp   *= _decayRate;
            _samplesRemaining--;

            for (int c = 0; c < channels; c++)
                data[s * channels + c] = sample;
        }
    }
}
