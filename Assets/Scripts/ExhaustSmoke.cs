using UnityEngine;

/// <summary>
/// Drives a heavy burnout/tyre-smoke particle system under the rear of the car.
/// Configured entirely in code — no asset dependency. Activated by ExhaustSmokeController.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class ExhaustSmoke : MonoBehaviour
{
    [Tooltip("Peak particles emitted per second while fully accelerating.")]
    public float emissionRate = 350f;

    [Tooltip("How quickly emission ramps up to full rate (seconds).")]
    public float rampUpTime = 0.35f;

    [Tooltip("Base smoke tint — near-black for hot tyre smoke.")]
    public Color smokeColor = new Color(0.18f, 0.18f, 0.18f, 0.85f);

    ParticleSystem ps;
    ParticleSystem.EmissionModule emission;

    bool isAccelerating;
    float currentRate;

    void Awake()
    {
        ps       = GetComponent<ParticleSystem>();
        emission = ps.emission;
        ConfigureParticleSystem();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void Update()
    {
        // Smoothly ramp emission rate up / down so smoke fades gracefully.
        float target = isAccelerating ? emissionRate : 0f;
        currentRate = Mathf.MoveTowards(currentRate, target, (emissionRate / rampUpTime) * Time.deltaTime);
        emission.rateOverTime = currentRate;
    }

    /// <summary>Called by ExhaustSmokeController when acceleration starts.</summary>
    public void StartSmoke()
    {
        isAccelerating = true;
        if (!ps.isPlaying) ps.Play();
    }

    /// <summary>Called by ExhaustSmokeController when acceleration ends.</summary>
    public void StopSmoke()
    {
        isAccelerating = false;
        // Let the ramp-down in Update() gracefully fade emission to zero,
        // then stop the system once no particles remain.
        ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    void ConfigureParticleSystem()
    {
        // ── Main ──────────────────────────────────────────────────────────────
        var main = ps.main;
        main.loop            = true;
        main.playOnAwake     = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(2.8f, 5.0f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.startRotation   = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.startColor      = new ParticleSystem.MinMaxGradient(smokeColor);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.04f);   // very slow rise
        main.maxParticles    = 2000;

        // ── Emission ──────────────────────────────────────────────────────────
        emission.enabled      = true;
        emission.rateOverTime = 0f;

        // ── Shape — wide hemisphere so smoke erupts sideways under the chassis ─
        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius    = 0.35f;
        shape.radiusThickness = 1f;

        // ── Velocity over lifetime — mostly backward + upward spread ──────────
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.Local;
        vel.x       = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
        vel.y       = new ParticleSystem.MinMaxCurve(0.3f,  1.8f);
        vel.z       = new ParticleSystem.MinMaxCurve(-4.5f, -1.5f);

        // ── Size over lifetime — tiny birth → huge billowing cloud ────────────
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        AnimationCurve expand = new AnimationCurve(
            new Keyframe(0f,   0.05f, 0f, 4f),
            new Keyframe(0.2f, 0.5f),
            new Keyframe(0.6f, 0.9f),
            new Keyframe(1f,   1.0f)
        );
        sizeOL.size = new ParticleSystem.MinMaxCurve(4.5f, expand);

        // ── Color over lifetime — pitch-black → blue-grey cloud → transparent ─
        var colorOL = ps.colorOverLifetime;
        colorOL.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.08f, 0.08f, 0.10f), 0f),
                new GradientColorKey(new Color(0.30f, 0.30f, 0.35f), 0.25f),
                new GradientColorKey(new Color(0.60f, 0.60f, 0.65f), 0.60f),
                new GradientColorKey(new Color(0.85f, 0.85f, 0.88f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f,    0f),
                new GradientAlphaKey(0.90f, 0.05f),
                new GradientAlphaKey(0.80f, 0.40f),
                new GradientAlphaKey(0.50f, 0.70f),
                new GradientAlphaKey(0f,    1f)
            }
        );
        colorOL.color = new ParticleSystem.MinMaxGradient(grad);

        // ── Rotation over lifetime — strong spin for chaotic tyre smoke ───────
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-120f * Mathf.Deg2Rad, 120f * Mathf.Deg2Rad);

        // ── Noise — heavy turbulence so clouds roll and churn ─────────────────
        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = new ParticleSystem.MinMaxCurve(0.80f);
        noise.frequency   = 0.30f;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.4f);
        noise.octaveCount = 3;
        noise.octaveMultiplier = 0.5f;
        noise.octaveScale      = 2f;
        noise.damping     = true;

        // Trails are intentionally disabled — enabling them requires a dedicated
        // trail material assigned on the ParticleSystemRenderer, otherwise Unity
        // renders them pink (missing material fallback).
        var trails = ps.trails;
        trails.enabled = false;
    }
}

