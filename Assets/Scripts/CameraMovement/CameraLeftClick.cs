using UnityEngine;
using UnityEngine.InputSystem;

public class Camera : MonoBehaviour
{
    [SerializeField] private float dragSensitivity = 0.2f;
    [SerializeField] private float maxX = 50f;
    [SerializeField] private float maxZ = 50f;
    [SerializeField] private float minX = -50f;
    [SerializeField] private float minZ = -50f;

    private Vector2 lastMousePosition;
    private bool isDragging = false;
    private Mouse mouse;

    private void OnEnable()
    {
        mouse = Mouse.current;
    }

    private void Update()
    {
        HandleDragInput();
    }

    private void HandleDragInput()
    {
        if (mouse == null)
            return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            isDragging = true;
            lastMousePosition = mouse.position.ReadValue();
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        if (isDragging)
        {
            Vector2 currentMousePosition = mouse.position.ReadValue();
            Vector2 mouseDelta = currentMousePosition - lastMousePosition;
            MoveCamera(mouseDelta);
            lastMousePosition = currentMousePosition;
        }
    }

    private void MoveCamera(Vector2 mouseDelta)
    {
        float moveX = -mouseDelta.x * dragSensitivity;
        float moveZ = -mouseDelta.y * dragSensitivity;

        Vector3 movementDirection = new Vector3(moveX, 0, moveZ);
        
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
}
