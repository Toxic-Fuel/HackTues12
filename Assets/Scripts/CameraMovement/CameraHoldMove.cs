using UnityEngine;
using UnityEngine.InputSystem;

public class CameraHoldMove : MonoBehaviour
{
    [SerializeField] private float dragSensitivity = 0.2f;
    [SerializeField] private float dragSmoothing = 0.05f;
    [SerializeField] private float dragStartThresholdPixels = 6f;
    [SerializeField] private float maxX = 50f;
    [SerializeField] private float maxZ = 50f;
    [SerializeField] private float minX = -50f;
    [SerializeField] private float minZ = -50f;
    [Header("Input Actions")]
    [SerializeField] private InputActionReference holdAction;
    [SerializeField] private InputActionReference deltaPositionAction;
    [SerializeField] private InputActionReference pointerPositionAction;

    private bool isDragging = false;
    private bool pendingDragStart = false;
    private Vector2 smoothedDelta;
    private Vector2 deltaVelocity;
    private Vector2 dragPressScreenPosition;
    public float settingsDragSensitivity = 1.0f;
    [SerializeField] private SelectTile selectTile;
    private bool inputSuppressed;

    private void OnEnable()
    {
        if (selectTile == null)
        {
            selectTile = FindAnyObjectByType<SelectTile>();
        }

        if (holdAction != null && holdAction.action != null)
        {
            holdAction.action.Enable();
        }

        if (deltaPositionAction != null && deltaPositionAction.action != null)
        {
            deltaPositionAction.action.Enable();
        }

        if (pointerPositionAction != null && pointerPositionAction.action != null)
        {
            pointerPositionAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (holdAction != null && holdAction.action != null)
        {
            holdAction.action.Disable();
        }

        if (deltaPositionAction != null && deltaPositionAction.action != null)
        {
            deltaPositionAction.action.Disable();
        }

        if (pointerPositionAction != null && pointerPositionAction.action != null)
        {
            pointerPositionAction.action.Disable();
        }

        ResetDragState();
    }

    private void Update()
    {
        HandleDragInput();
    }

    private void HandleDragInput()
    {
        if (InGameGenerationMenu.IsAnyMenuOpen)
        {
            ResetDragState();
            return;
        }

        if (inputSuppressed)
        {
            ResetDragState();
            return;
        }

        if (holdAction == null || holdAction.action == null
            || deltaPositionAction == null || deltaPositionAction.action == null
            || pointerPositionAction == null || pointerPositionAction.action == null)
        {
            ResetDragState();
            return;
        }

        if (selectTile != null && selectTile.HasSelection)
        {
            ResetDragState();
            return;
        }

        if (holdAction.action.WasPressedThisFrame())
        {
            pendingDragStart = true;
            isDragging = false;
            dragPressScreenPosition = pointerPositionAction.action.ReadValue<Vector2>();
        }

        if (holdAction.action.WasReleasedThisFrame())
        {
            ResetDragState();
            return;
        }

        if (pendingDragStart)
        {
            Vector2 currentPos = pointerPositionAction.action.ReadValue<Vector2>();
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
            Vector2 rawDelta = deltaPositionAction.action.ReadValue<Vector2>();
            smoothedDelta = Vector2.SmoothDamp(smoothedDelta, rawDelta, ref deltaVelocity, dragSmoothing);
            MoveCamera(smoothedDelta);
        }
    }

    private void MoveCamera(Vector2 mouseDelta)
    {
        float moveX = -mouseDelta.x * dragSensitivity * settingsDragSensitivity;
        float moveZ = -mouseDelta.y * dragSensitivity * settingsDragSensitivity;

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

    public void SetInputSuppressed(bool suppressed)
    {
        if (inputSuppressed == suppressed)
        {
            return;
        }

        inputSuppressed = suppressed;
        if (inputSuppressed)
        {
            ResetDragState();
        }
    }
}
