using UnityEngine;
using UnityEngine.InputSystem;

public class CameraRightClick : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 1f;

    private Vector2 lastMousePosition;
    private bool isDragging = false;
    private Mouse mouse;

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
            lastMousePosition = mouse.position.ReadValue();
        }

        if (mouse.rightButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        if (isDragging)
        {
            Vector2 currentMousePosition = mouse.position.ReadValue();
            Vector2 mouseDelta = currentMousePosition - lastMousePosition;
            RotateObject(mouseDelta);
            lastMousePosition = currentMousePosition;
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
