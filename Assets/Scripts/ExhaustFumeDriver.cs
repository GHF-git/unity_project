using UnityEngine;

/// <summary>
/// Sits on the car root and activates/deactivates the ExhaustFumeQuad
/// based on whether the player is pressing the accelerate key.
/// Controlling activeSelf (not renderer.enabled) guarantees the quad
/// is completely invisible and the VideoPlayer is stopped when idle.
/// </summary>
public class ExhaustFumeDriver : MonoBehaviour
{
    [Tooltip("The ExhaustFumeQuad GameObject — child of this car.")]
    public GameObject fumeQuad;

    void Update()
    {
        bool accelerating = Input.GetKey(KeyCode.W);

        if (fumeQuad != null && fumeQuad.activeSelf != accelerating)
            fumeQuad.SetActive(accelerating);
    }
}
