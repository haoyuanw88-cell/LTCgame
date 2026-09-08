using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class PetGardenCameraDrag : MonoBehaviour
{
    [Header("可拖曳範圍")]
    [SerializeField] private SpriteRenderer backgroundRenderer;

    [Header("操作")]
    [SerializeField, Min(0.1f)] private float dragSpeed = 1f;
    [SerializeField] private bool ignorePointerOverUI = true;
    [SerializeField, Range(0.6f, 0.95f)] private float visibleGrassFraction = 0.85f;

    private Camera targetCamera;
    private Vector2 lastPointerPosition;
    private bool isDragging;
    private float cameraZ;

    public SpriteRenderer BackgroundRenderer
    {
        get => backgroundRenderer;
        set
        {
            backgroundRenderer = value;
            ClampToBackground();
        }
    }

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        cameraZ = transform.position.z;
    }

    private void Start()
    {
        FitCameraInsideBackground();
        ClampToBackground();
    }

    private void LateUpdate()
    {
        if (!TryReadPointer(out Vector2 pointerPosition, out bool pressedThisFrame, out bool isPressed, out bool releasedThisFrame))
        {
            isDragging = false;
            return;
        }

        if (pressedThisFrame)
        {
            if (ignorePointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                isDragging = false;
                return;
            }

            isDragging = true;
            lastPointerPosition = pointerPosition;
        }

        if (isDragging && isPressed)
        {
            Vector2 pixelDelta = pointerPosition - lastPointerPosition;
            lastPointerPosition = pointerPosition;

            float worldUnitsPerPixel = (targetCamera.orthographicSize * 2f) / Mathf.Max(1, Screen.height);
            Vector3 position = transform.position;
            position.x -= pixelDelta.x * worldUnitsPerPixel * dragSpeed;
            position.y -= pixelDelta.y * worldUnitsPerPixel * dragSpeed;
            position.z = cameraZ;
            transform.position = position;

            ClampToBackground();
        }

        if (releasedThisFrame)
        {
            isDragging = false;
        }
    }

    public void SnapToCenter()
    {
        if (backgroundRenderer == null)
        {
            return;
        }

        Vector3 center = backgroundRenderer.bounds.center;
        transform.position = new Vector3(center.x, center.y, cameraZ);
        ClampToBackground();
    }

    private void FitCameraInsideBackground()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (backgroundRenderer == null || targetCamera == null || !targetCamera.orthographic)
            return;

        Bounds bounds = backgroundRenderer.bounds;
        float safeAspect = Mathf.Max(0.01f, targetCamera.aspect);
        float heightLimit = bounds.extents.y * visibleGrassFraction;
        float widthLimit = (bounds.extents.x / safeAspect) * visibleGrassFraction;
        float fittedSize = Mathf.Min(heightLimit, widthLimit);

        if (fittedSize > 0.1f && targetCamera.orthographicSize > fittedSize)
            targetCamera.orthographicSize = fittedSize;
    }

    private bool TryReadPointer(out Vector2 position, out bool pressedThisFrame, out bool isPressed, out bool releasedThisFrame)
    {
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            position = mouse.position.ReadValue();
            pressedThisFrame = mouse.leftButton.wasPressedThisFrame;
            isPressed = mouse.leftButton.isPressed;
            releasedThisFrame = mouse.leftButton.wasReleasedThisFrame;
            return true;
        }

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            position = touchscreen.primaryTouch.position.ReadValue();
            pressedThisFrame = touchscreen.primaryTouch.press.wasPressedThisFrame;
            isPressed = touchscreen.primaryTouch.press.isPressed;
            releasedThisFrame = touchscreen.primaryTouch.press.wasReleasedThisFrame;
            return true;
        }

        position = default;
        pressedThisFrame = false;
        isPressed = false;
        releasedThisFrame = false;
        return false;
    }

    private void ClampToBackground()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (backgroundRenderer == null || targetCamera == null || !targetCamera.orthographic)
        {
            return;
        }

        Bounds bounds = backgroundRenderer.bounds;
        float halfHeight = targetCamera.orthographicSize;
        float halfWidth = halfHeight * targetCamera.aspect;

        Vector3 position = transform.position;
        position.x = bounds.size.x <= halfWidth * 2f
            ? bounds.center.x
            : Mathf.Clamp(position.x, bounds.min.x + halfWidth, bounds.max.x - halfWidth);
        position.y = bounds.size.y <= halfHeight * 2f
            ? bounds.center.y
            : Mathf.Clamp(position.y, bounds.min.y + halfHeight, bounds.max.y - halfHeight);
        position.z = cameraZ;
        transform.position = position;
    }
}
