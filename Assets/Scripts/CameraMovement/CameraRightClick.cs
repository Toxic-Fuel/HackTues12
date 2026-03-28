using UnityEngine;
using UnityEngine.InputSystem;

public class CameraRightClick : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 0.2f;
    [SerializeField] private float rotationSmoothing = 0.05f;
    [SerializeField] private float minVerticalRotation = 9.5f;
    [SerializeField] private float maxVerticalRotation = 89.9f;
    [SerializeField] private SelectTile selectTile;

    private bool isDragging = false;
    private Mouse mouse;
    private Vector2 smoothedDelta;
    private Vector2 deltaVelocity;
    private float currentVerticalRotation;
    private float currentHorizontalRotation;

    private void OnEnable()
    {
        mouse = Mouse.current;

        if (selectTile == null)
        {
            selectTile = FindAnyObjectByType<SelectTile>();
        }

        Vector3 euler = transform.localEulerAngles;
        currentVerticalRotation = Mathf.Clamp(NormalizeAngle(euler.x), minVerticalRotation, maxVerticalRotation);
        currentHorizontalRotation = euler.y;

        transform.localRotation = Quaternion.Euler(currentVerticalRotation, currentHorizontalRotation, 0f);
    }

    private void Update()
    {
        if (mouse == null)
            return;

        if (selectTile != null && selectTile.HasSelection)
        {
            isDragging = false;
            smoothedDelta = Vector2.zero;
            deltaVelocity = Vector2.zero;
            return;
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            isDragging = true;
        }

        if (mouse.rightButton.wasReleasedThisFrame)
        {
            isDragging = false;
            smoothedDelta = Vector2.zero;
            deltaVelocity = Vector2.zero;
        }

        if (isDragging)
        {
            Vector2 rawDelta = mouse.delta.ReadValue();
            smoothedDelta = Vector2.SmoothDamp(smoothedDelta, rawDelta, ref deltaVelocity, rotationSmoothing);
            RotateObject(smoothedDelta);
        }
    }

    private void RotateObject(Vector2 mouseDelta)
    {
        float rotationY = mouseDelta.x * rotationSpeed;
        float rotationX = -mouseDelta.y * rotationSpeed;

        currentHorizontalRotation += rotationY;
        currentVerticalRotation = Mathf.Clamp(currentVerticalRotation + rotationX, minVerticalRotation, maxVerticalRotation);

        transform.localRotation = Quaternion.Euler(currentVerticalRotation, currentHorizontalRotation, 0f);
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }
}
