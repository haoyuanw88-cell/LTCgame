using UnityEngine;
using UnityEngine.InputSystem;

public class SimpleFirstPersonController : MonoBehaviour
{
    public float moveSpeed = 3.0f;
    public float mouseSensitivity = 0.12f;
    public Transform cameraTarget;

    private CharacterController controller;
    private float pitch;
    private float gravityVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTarget == null && Camera.main != null)
        {
            cameraTarget = Camera.main.transform;
        }
    }

    void Start()
    {
        LockCursor();
    }

    void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockCursor();
        }

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            LookAround();
            MovePlayer();
        }
    }

    void LookAround()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;
        transform.Rotate(Vector3.up * mouseDelta.x);

        pitch -= mouseDelta.y;
        pitch = Mathf.Clamp(pitch, -75f, 75f);

        if (cameraTarget != null)
        {
            cameraTarget.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    void MovePlayer()
    {
        if (controller == null || Keyboard.current == null) return;

        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;
        if (input.magnitude > 1f) input.Normalize();

        Vector3 move = transform.right * input.x + transform.forward * input.y;

        if (controller.isGrounded && gravityVelocity < 0f)
        {
            gravityVelocity = -1f;
        }

        gravityVelocity += Physics.gravity.y * Time.deltaTime;
        move.y = gravityVelocity;

        controller.Move(move * moveSpeed * Time.deltaTime);
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
