using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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

    [Header("Cursor")]
    [Tooltip("The red dot crosshair image shown when the cursor is locked.")]
    public Image redCursorImage;

    CharacterController controller;
    float pitch;

    /// <summary>Whether the camera look / cursor lock mode is currently active.</summary>
    bool isLocked;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraRoot == null && Camera.main != null)
            cameraRoot = Camera.main.transform;

        // Start in locked mode: hide OS cursor, show red dot, enable camera look.
        SetLockMode(true);
    }

    void Update()
    {
        if (controller == null || cameraRoot == null)
            return;

        // ESC → unlock
        if (Input.GetKeyDown(KeyCode.Escape) && isLocked)
        {
            SetLockMode(false);
            return;
        }

        // Any mouse click while unlocked → re-lock only when NOT over UI
        if (!isLocked && Input.GetMouseButtonDown(0))
        {
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!overUI)
            {
                SetLockMode(true);
                return;
            }
        }

        // Camera look (only while locked)
        if (isLocked)
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

            transform.Rotate(Vector3.up * mouseX);
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // Movement (WASD + Q/E) — always active regardless of lock state
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

    /// <summary>
    /// Toggles between locked (camera look, red dot visible) and
    /// unlocked (OS cursor visible, red dot hidden) modes.
    /// </summary>
    void SetLockMode(bool locked)
    {
        isLocked = locked;

        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !locked;

        if (redCursorImage != null)
            redCursorImage.enabled = locked;
    }
}
