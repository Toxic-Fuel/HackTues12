using UnityEngine;
using UnityEngine.InputSystem;

public class CameraLeftClick : MonoBehaviour
{
    [SerializeField] private float dragSensitivity = 0.2f;
    [SerializeField] private float dragSmoothing = 0.05f;
    [SerializeField] private float dragStartThresholdPixels = 6f;
    [SerializeField] private float maxX = 50f;
    [SerializeField] private float maxZ = 50f;
    [SerializeField] private float minX = -50f;
    [SerializeField] private float minZ = -50f;

    private bool isDragging = false;
    private bool pendingDragStart = false;
    private Mouse mouse;
    private Vector2 smoothedDelta;
    private Vector2 deltaVelocity;
    private Vector2 dragPressScreenPosition;
    [SerializeField] private SelectTile selectTile;

    private void OnEnable()
    {
        mouse = Mouse.current;
        if (selectTile == null)
        {
            selectTile = FindAnyObjectByType<SelectTile>();
        }
    }

    private void Update()
    {
        HandleDragInput();
    }

    private void HandleDragInput()
    {
        if (mouse == null)
            return;

        if (selectTile != null && selectTile.HasSelection)
        {
            ResetDragState();
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            pendingDragStart = true;
            isDragging = false;
            dragPressScreenPosition = mouse.position.ReadValue();
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            ResetDragState();
            return;
        }

        if (pendingDragStart)
        {
            Vector2 currentPos = mouse.position.ReadValue();
            float thresholdSquared = dragStartThresholdPixels * dragStartThresholdPixels;
            if ((currentPos - dragPressScreenPosition).sqrMagnitude >= thresholdSquared)
            {
                pendingDragStart = false;
                isDragging = true;
                smoothedDelta = Vector2.zero;
                deltaVelocity = Vector2.zero;
            }
        }

        if (isDragging)
        {
            Vector2 rawDelta = mouse.delta.ReadValue();
            smoothedDelta = Vector2.SmoothDamp(smoothedDelta, rawDelta, ref deltaVelocity, dragSmoothing);
            MoveCamera(smoothedDelta);
        }
    }

    private void MoveCamera(Vector2 mouseDelta)
    {
        float moveX = -mouseDelta.x * dragSensitivity;
        float moveZ = -mouseDelta.y * dragSensitivity;

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        Vector3 worldMovement = (right * moveX) + (forward * moveZ);

        Vector3 newPosition = transform.localPosition + worldMovement;

        newPosition.x = Mathf.Clamp(newPosition.x, minX, maxX);
        newPosition.z = Mathf.Clamp(newPosition.z, minZ, maxZ);

        transform.localPosition = newPosition;
    }

    private void ResetDragState()
    {
        isDragging = false;
        pendingDragStart = false;
        smoothedDelta = Vector2.zero;
        deltaVelocity = Vector2.zero;
    }
}
