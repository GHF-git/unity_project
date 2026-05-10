using UnityEngine;

/// <summary>
/// Configures the ParticleSystem sibling on the same GameObject as a one-shot confetti burst.
/// Applied to the ConfettiParticles child of each encouragement panel.
/// Runs in Awake so the system is fully set up before the first Play() call.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class ConfettiConfigurator : MonoBehaviour
{
    // ── Colours ───────────────────────────────────────────────────────────────

    private static readonly Color[] ConfettiColors =
    {
        new Color(1.00f, 0.84f, 0.00f), // gold
        new Color(0.29f, 0.86f, 0.50f), // green
        new Color(1.00f, 0.42f, 0.70f), // pink
        new Color(0.27f, 0.65f, 1.00f), // blue
        new Color(1.00f, 0.60f, 0.20f), // orange
    };

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        var ps = GetComponent<ParticleSystem>();

        // ── Main module ──────────────────────────────────────────────────────
        var main = ps.main;
        main.duration          = 0.1f;
        main.loop              = false;
        main.startLifetime     = new ParticleSystem.MinMaxCurve(1.0f, 1.5f);
        main.startSpeed        = new ParticleSystem.MinMaxCurve(200f, 500f);
        main.startSize         = new ParticleSystem.MinMaxCurve(6f, 14f);
        main.startRotation     = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.gravityModifier   = 0.8f;
        main.simulationSpace   = ParticleSystemSimulationSpace.World;
        main.stopAction        = ParticleSystemStopAction.Disable;
        main.playOnAwake       = false;

        // Cycle through the confetti colour palette
        main.startColor = new ParticleSystem.MinMaxGradient(BuildColorGradient());

        // ── Emission module — single burst ───────────────────────────────────
        var emission = ps.emission;
        emission.enabled    = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 40, 1, 0.01f),
        });

        // ── Shape module — cone spread ───────────────────────────────────────
        var shape = ps.shape;
        shape.enabled    = true;
        shape.shapeType  = ParticleSystemShapeType.Cone;
        shape.angle      = 45f;
        shape.radius     = 0.1f;

        // ── Renderer — keep default Particles material, enable sorting ────────
        var renderer = GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder  = 100;        // Draw above panel UI
        renderer.renderMode    = ParticleSystemRenderMode.Billboard;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Gradient BuildColorGradient()
    {
        var gradient   = new Gradient();
        var colorKeys  = new GradientColorKey[ConfettiColors.Length];
        var alphaKeys  = new GradientAlphaKey[2];

        for (int i = 0; i < ConfettiColors.Length; i++)
            colorKeys[i] = new GradientColorKey(ConfettiColors[i], (float)i / (ConfettiColors.Length - 1));

        alphaKeys[0] = new GradientAlphaKey(1f, 0f);
        alphaKeys[1] = new GradientAlphaKey(0f, 1f);

        gradient.SetKeys(colorKeys, alphaKeys);
        return gradient;
    }
}
