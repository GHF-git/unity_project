using UnityEngine;

/// <summary>
/// Attach to the Button GameObject in the scene.
/// Left-click while aiming the crosshair at the button to toggle the garage door.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorButtonInteraction : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The GarageDoorController to toggle.")]
    public GarageDoorController door;

    [Header("Interaction")]
    [Tooltip("Maximum distance from which the player can interact with the button.")]
    public float maxInteractDistance = 15f;

    [Tooltip("Camera used to cast the interaction ray. Defaults to Camera.main.")]
    public Camera interactionCamera;

    void Start()
    {
        if (interactionCamera == null)
            interactionCamera = Camera.main;

        // Ensure MeshCollider is convex so raycasts register.
        if (TryGetComponent(out MeshCollider mc) && !mc.convex)
            mc.convex = true;
    }

    void Update()
    {
        // Uses legacy Input — matches how PlayerController reads input.
        if (!Input.GetMouseButtonDown(0)) return;

        if (interactionCamera == null) return;

        // Ray from screen center (crosshair) — ignores cursor position and UI overlap.
        Ray ray = interactionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(ray, out RaycastHit hit, maxInteractDistance)) return;

        if (hit.collider.gameObject != gameObject) return;

        door?.Toggle();
    }
}
