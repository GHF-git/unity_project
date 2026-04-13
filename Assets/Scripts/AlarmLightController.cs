using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Drives a realistic rotating red alarm beacon with room-filling light spread.
///
/// Creates two lights at runtime:
///   - A bright Point light (AlarmBeaconLight) attached near the lamp head that
///     rotates with the mesh and strobes sharply.
///   - A large-range Point light (AlarmFillLight) at the alarm root that pulses
///     red across the entire room with no shadows for maximum spread.
///
/// Requires an AudioSource on the same GameObject with the alarm clip assigned.
/// Call Activate() / Deactivate() to start and stop the effect.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AlarmLightController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The child transform that visually rotates (the Lamp mesh).")]
    public Transform lampRotator;

    [Header("Rotation")]
    [Tooltip("Degrees per second the lamp rotates around its local Y axis.")]
    public float rotationSpeed = 200f;

    [Header("Beacon Light  —  Point light on the lamp head")]
    [Tooltip("Peak intensity of the beacon in Candela.")]
    public float beaconIntensity = 3000f;

    [Tooltip("Range of the beacon point light in world units.")]
    public float beaconRange = 80f;

    [Header("Room Fill Light  —  large ambient red flood")]
    [Tooltip("Peak intensity of the room fill light in Candela.")]
    public float fillIntensity = 800f;

    [Tooltip("Range of the room fill light in world units. Should cover the whole garage.")]
    public float fillRange = 150f;

    [Header("Strobe Timing")]
    [Tooltip("Full on/off flash cycles per second.")]
    public float strobeFrequency = 2.5f;

    [Tooltip("Fraction of each strobe cycle the light is ON (0–1).")]
    [Range(0.1f, 0.9f)]
    public float strobeDutyCycle = 0.45f;

    // ── Runtime ──────────────────────────────────────────────────────────────
    Light beaconLight;
    Light fillLight;
    AudioSource audioSource;
    float currentAngle;
    float strobeTimer;
    bool isActive;

    static readonly Color AlarmRed = new Color(1f, 0.02f, 0.02f, 1f);

    // ── Unity ────────────────────────────────────────────────────────────────

    void Awake()
    {
        audioSource             = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop        = true;
        audioSource.Stop();   // Unity reads playOnAwake from the serialized scene value
                              // before Awake() runs, so we force-stop any auto-play here.

        ResolveRotator();
        CreateBeaconLight();
        CreateFillLight();
    }

    void Update()
    {
        if (!isActive) return;

        RotateLamp();
        UpdateStrobe();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Starts the rotating alarm light effect.</summary>
    public void Activate()
    {
        isActive    = true;
        strobeTimer = 0f;

        if (beaconLight != null) beaconLight.enabled = true;
        if (fillLight   != null) fillLight.enabled   = true;

        if (audioSource != null && !audioSource.isPlaying)
            audioSource.Play();
    }

    /// <summary>Stops the rotating alarm light effect and kills all lights.</summary>
    public void Deactivate()
    {
        isActive = false;

        if (beaconLight != null) { beaconLight.intensity = 0f; beaconLight.enabled = false; }
        if (fillLight   != null) { fillLight.intensity   = 0f; fillLight.enabled   = false; }

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    void RotateLamp()
    {
        if (lampRotator == null) return;

        currentAngle = (currentAngle + rotationSpeed * Time.deltaTime) % 360f;
        lampRotator.localRotation = Quaternion.Euler(0f, currentAngle, 0f);
    }

    void UpdateStrobe()
    {
        float cycleLength = 1f / strobeFrequency;
        strobeTimer = (strobeTimer + Time.deltaTime) % cycleLength;

        bool flashOn = strobeTimer < cycleLength * strobeDutyCycle;

        // ── Beacon: sharp strobe with a sine ease inside the ON window ───────
        if (beaconLight != null)
        {
            if (flashOn)
            {
                float phase         = strobeTimer / (cycleLength * strobeDutyCycle);
                float curve         = Mathf.Sin(phase * Mathf.PI);            // 0 → 1 → 0
                beaconLight.intensity = Mathf.Lerp(beaconIntensity * 0.25f, beaconIntensity, curve);
            }
            else
            {
                beaconLight.intensity = 0f;
            }
        }

        // ── Fill: slower continuous sinusoidal pulse — never goes fully dark ─
        if (fillLight != null)
        {
            float fillCurve     = Mathf.Sin(strobeTimer / cycleLength * Mathf.PI * 2f) * 0.5f + 0.5f;
            fillLight.intensity = Mathf.Lerp(fillIntensity * 0.08f, fillIntensity, fillCurve);
        }
    }

    void CreateBeaconLight()
    {
        // Attach to LampMain (the bulb position) so it moves with the rotating lamp head
        Transform parent = transform.Find("LampMain")
                        ?? transform.Find("Lamp")
                        ?? transform;

        GameObject go         = new GameObject("AlarmBeaconLight");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        beaconLight             = go.AddComponent<Light>();
        beaconLight.type        = LightType.Point;
        beaconLight.color       = AlarmRed;
        beaconLight.intensity   = 0f;
        beaconLight.range       = beaconRange;
        beaconLight.shadows     = LightShadows.Soft;
        beaconLight.lightUnit   = LightUnit.Candela;
        beaconLight.enabled     = false;
    }

    void CreateFillLight()
    {
        // Attach to the alarm root — floods the entire room with no shadow cost
        GameObject go       = new GameObject("AlarmFillLight");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        fillLight           = go.AddComponent<Light>();
        fillLight.type      = LightType.Point;
        fillLight.color     = AlarmRed;
        fillLight.intensity = 0f;
        fillLight.range     = fillRange;
        fillLight.shadows   = LightShadows.None;
        fillLight.lightUnit = LightUnit.Candela;
        fillLight.enabled   = false;
    }

    void ResolveRotator()
    {
        if (lampRotator != null) return;

        Transform found = transform.Find("Lamp");
        if (found != null)
            lampRotator = found;
        else
            Debug.LogWarning($"[AlarmLightController] No 'Lamp' child found on '{name}'. Assign lampRotator manually.", this);
    }
}
