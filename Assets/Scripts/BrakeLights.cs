using UnityEngine;

/// <summary>
/// Swaps the taillight renderers between a dim base material and a bright
/// brake material whenever the car is braking (S key or handbrake).
/// Attach to any persistent GameObject — the car root is ideal.
/// </summary>
public class BrakeLights : MonoBehaviour
{
    [Header("Renderers")]
    [Tooltip("MeshRenderer of the left taillight mesh.")]
    public MeshRenderer leftTaillight;

    [Tooltip("MeshRenderer of the right taillight mesh.")]
    public MeshRenderer rightTaillight;

    [Header("Materials")]
    [Tooltip("Material used when the brakes are NOT pressed (dim / off).")]
    public Material baseMaterial;

    [Tooltip("Material used when the brakes ARE pressed (bright red).")]
    public Material brakeMaterial;

    bool isBraking;

    void Update()
    {
        bool braking = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.Space);

        if (braking == isBraking) return;

        isBraking = braking;
        Material target = isBraking ? brakeMaterial : baseMaterial;

        if (leftTaillight  != null) leftTaillight.material  = target;
        if (rightTaillight != null) rightTaillight.material = target;
    }
}
