using UnityEngine;
using UnityEngine.InputSystem;

public class CameraRightClick : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 0.2f;
    [SerializeField] private float rotationSmoothing = 0.05f;

    private bool isDragging = false;
    private Mouse mouse;
    private Vector2 smoothedDelta;
    private Vector2 deltaVelocity;

    private void OnEnable()
    {
        mouse = Mouse.current;
    }

    private void Update()
    {
        if (mouse == null)
            return;

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
        float absDeltaX = Mathf.Abs(mouseDelta.x);
        float absDeltaY = Mathf.Abs(mouseDelta.y);

        if (absDeltaX > absDeltaY)
        {
            float rotationY = mouseDelta.x * rotationSpeed;
            transform.Rotate(0, rotationY, 0, Space.World);
        }
        else
        {
            float rotationX = -mouseDelta.y * rotationSpeed;
            transform.Rotate(rotationX, 0, 0, Space.Self);
        }
    }
}
