using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float verticalSpeed = 5f;   // Q/E
    public bool flyMode = true;

    [Header("Mouse Look")]
    public float mouseSensitivity = 2.0f;
    public Transform cameraRoot;
    public float pitchMin = -90f;
    public float pitchMax = 90f;

    CharacterController controller;
    float pitch;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraRoot == null && Camera.main != null)
            cameraRoot = Camera.main.transform;

        // Cursor always free — no locking, no ESC needed
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (controller == null || cameraRoot == null)
            return;

        // Mouse look only while Right Mouse Button is held
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

            transform.Rotate(Vector3.up * mouseX);
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // Movement (WASD + Q/E)
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        Vector3 move = (transform.right * moveX + transform.forward * moveZ).normalized * moveSpeed;

        float upDown = 0f;
        if (flyMode)
        {
            if (Input.GetKey(KeyCode.Q)) upDown += 1f;
            if (Input.GetKey(KeyCode.E)) upDown -= 1f;
        }

        controller.Move((move + Vector3.up * (upDown * verticalSpeed)) * Time.deltaTime);
    }
}
