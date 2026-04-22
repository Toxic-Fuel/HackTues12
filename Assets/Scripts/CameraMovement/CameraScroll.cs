using UnityEngine;
using UnityEngine.InputSystem;

public class CameraScroll : MonoBehaviour
{
    [SerializeField] private float scrollSpeed = 0.02f;
    [SerializeField] private float pinchSpeed = 0.01f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 30f;
    [SerializeField] private float zoomSmoothTime = 0.12f;
    [Header("Portrait View")]
    [SerializeField] private bool widenViewInPortrait = true;
    [SerializeField, Range(1f, 2f)] private float portraitDistanceMultiplier = 2.0f;
    [SerializeField, Min(0f)] private float portraitDistanceBonus = 2f;
    [SerializeField, Min(0f)] private float portraitMaxDistanceBonus = 12f;
    [SerializeField] private SelectTile selectTile;
    [SerializeField] private CameraHoldMove cameraHoldMove;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference scrollAction;
    [SerializeField] private InputActionReference touch0PressAction;
    [SerializeField] private InputActionReference touch1PressAction;
    [SerializeField] private InputActionReference touch0PositionAction;
    [SerializeField] private InputActionReference touch1PositionAction;
    [SerializeField] private InputActionReference movePressAction;

    public float settingsZoomSpeed = 1.0f;
    private Vector3 zoomDirectionLocal;
    private float currentDistance;
    private float targetDistance;
    private float zoomVelocity;
    private bool pinchActive;
    private float lastPinchDistance;
    private bool movePressTemporarilyDisabled;
    private bool wasPortraitScreen;

    private void OnEnable()
    {
        if (selectTile == null)
        {
            selectTile = FindAnyObjectByType<SelectTile>();
        }

        if (cameraHoldMove == null)
        {
            cameraHoldMove = FindAnyObjectByType<CameraHoldMove>();
        }

        EnableAction(scrollAction);
        EnableAction(touch0PressAction);
        EnableAction(touch1PressAction);
        EnableAction(touch0PositionAction);
        EnableAction(touch1PositionAction);

        zoomDirectionLocal = transform.localPosition.sqrMagnitude > 0f
            ? transform.localPosition.normalized
            : Vector3.back;

        wasPortraitScreen = IsPortraitScreen();
        float effectiveMaxDistance = GetEffectiveMaxDistance(wasPortraitScreen);
        currentDistance = Mathf.Clamp(transform.localPosition.magnitude, minDistance, effectiveMaxDistance);
        if (widenViewInPortrait && wasPortraitScreen)
        {
            currentDistance = Mathf.Clamp(
                currentDistance * portraitDistanceMultiplier + portraitDistanceBonus,
                minDistance,
                effectiveMaxDistance
            );
        }

        targetDistance = currentDistance;
        transform.localPosition = zoomDirectionLocal * currentDistance;
    }

    private void OnDisable()
    {
        // Ensure move press is restored when this component is turned off.
        SetMovePressEnabled(true);

        if (cameraHoldMove != null)
        {
            cameraHoldMove.SetInputSuppressed(false);
        }

        DisableAction(scrollAction);
        DisableAction(touch0PressAction);
        DisableAction(touch1PressAction);
        DisableAction(touch0PositionAction);
        DisableAction(touch1PositionAction);

        pinchActive = false;
        lastPinchDistance = 0f;
    }

    private void Update()
    {
        bool twoTouchesPressed = AreTwoTouchesPressed();
        SetMovePressEnabled(!twoTouchesPressed);

        if (cameraHoldMove != null)
        {
            cameraHoldMove.SetInputSuppressed(twoTouchesPressed);
        }

        if (selectTile != null && selectTile.HasSelection)
        {
            targetDistance = currentDistance;
            pinchActive = false;
            return;
        }

        bool isPortraitScreen = IsPortraitScreen();
        float effectiveMaxDistance = GetEffectiveMaxDistance(isPortraitScreen);
        if (widenViewInPortrait && isPortraitScreen && !wasPortraitScreen)
        {
            targetDistance = Mathf.Clamp(
                targetDistance * portraitDistanceMultiplier + portraitDistanceBonus,
                minDistance,
                effectiveMaxDistance
            );
        }
        wasPortraitScreen = isPortraitScreen;

        float zoomDelta = ReadScrollDelta() + ReadPinchDelta();
        if (!Mathf.Approximately(zoomDelta, 0f))
        {
            targetDistance -= zoomDelta;
        }

        targetDistance = Mathf.Clamp(targetDistance, minDistance, effectiveMaxDistance);

        currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref zoomVelocity, zoomSmoothTime);
        transform.localPosition = zoomDirectionLocal * currentDistance;
    }

    private float ReadScrollDelta()
    {
        if (scrollAction == null || scrollAction.action == null)
        {
            return 0f;
        }

        return scrollAction.action.ReadValue<Vector2>().y * scrollSpeed * settingsZoomSpeed;
    }

    private float ReadPinchDelta()
    {
        if (!AreTwoTouchesPressed())
        {
            pinchActive = false;
            return 0f;
        }

        if (!TryReadTouchPositions(out Vector2 touch0, out Vector2 touch1))
        {
            pinchActive = false;
            return 0f;
        }

        float currentPinchDistance = Vector2.Distance(touch0, touch1);

        if (!pinchActive)
        {
            pinchActive = true;
            lastPinchDistance = currentPinchDistance;
            return 0f;
        }

        float pinchDelta = (currentPinchDistance - lastPinchDistance) * pinchSpeed * settingsZoomSpeed;
        lastPinchDistance = currentPinchDistance;
        return pinchDelta;
    }

    private bool AreTwoTouchesPressed()
    {
        return ReadButtonValue(touch0PressAction) > 0.5f && ReadButtonValue(touch1PressAction) > 0.5f;
    }

    private bool TryReadTouchPositions(out Vector2 touch0, out Vector2 touch1)
    {
        touch0 = Vector2.zero;
        touch1 = Vector2.zero;

        if (touch0PositionAction == null || touch0PositionAction.action == null
            || touch1PositionAction == null || touch1PositionAction.action == null)
        {
            return false;
        }

        touch0 = touch0PositionAction.action.ReadValue<Vector2>();
        touch1 = touch1PositionAction.action.ReadValue<Vector2>();
        return true;
    }

    private static float ReadButtonValue(InputActionReference actionReference)
    {
        if (actionReference == null || actionReference.action == null)
        {
            return 0f;
        }

        return actionReference.action.ReadValue<float>();
    }

    private static void EnableAction(InputActionReference actionReference)
    {
        if (actionReference != null && actionReference.action != null)
        {
            actionReference.action.Enable();
        }
    }

    private static void DisableAction(InputActionReference actionReference)
    {
        if (actionReference != null && actionReference.action != null)
        {
            actionReference.action.Disable();
        }
    }

    private void SetMovePressEnabled(bool shouldEnable)
    {
        if (movePressAction == null || movePressAction.action == null)
        {
            movePressTemporarilyDisabled = false;
            return;
        }

        if (shouldEnable)
        {
            if (movePressTemporarilyDisabled)
            {
                movePressAction.action.Enable();
                movePressTemporarilyDisabled = false;
            }
            return;
        }

        if (!movePressTemporarilyDisabled)
        {
            movePressAction.action.Disable();
            movePressTemporarilyDisabled = true;
        }
    }

    private static bool IsPortraitScreen()
    {
        return Screen.height > Screen.width;
    }

    private float GetEffectiveMaxDistance(bool isPortraitScreen)
    {
        if (widenViewInPortrait && isPortraitScreen)
        {
            return maxDistance + Mathf.Max(0f, portraitMaxDistanceBonus);
        }

        return maxDistance;
    }
}
