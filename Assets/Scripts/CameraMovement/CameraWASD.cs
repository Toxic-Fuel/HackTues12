using UnityEngine;
using UnityEngine.InputSystem;

public class CameraWASD : MonoBehaviour
{
    [SerializeField] private float movementSpeed = 10f;
    [SerializeField] private float maxX = 50f;
    [SerializeField] private float maxZ = 50f;
    [SerializeField] private float minX = -50f;
    [SerializeField] private float minZ = -50f;

    private Keyboard keyboard;
    private Vector3 movementDirection;

    private void OnEnable()
    {
        keyboard = Keyboard.current;
    }

    private void Update()
    {
        HandleMovementInput();
        MoveCamera();
    }

    private void HandleMovementInput()
    {
        if (keyboard == null)
            return;

        movementDirection = Vector3.zero;

        if (keyboard.wKey.isPressed)
            movementDirection += transform.forward;

        if (keyboard.sKey.isPressed)
            movementDirection -= transform.forward;

        if (keyboard.dKey.isPressed)
            movementDirection += transform.right;

        if (keyboard.aKey.isPressed)
            movementDirection -= transform.right;

        movementDirection.y = 0;

        if (movementDirection.magnitude > 0)
            movementDirection.Normalize();
    }

    private void MoveCamera()
    {
        Vector3 newPosition = transform.localPosition + (movementDirection * movementSpeed * Time.deltaTime);

        newPosition.x = Mathf.Clamp(newPosition.x, minX, maxX);
        newPosition.z = Mathf.Clamp(newPosition.z, minZ, maxZ);

        transform.localPosition = newPosition;
    }
}
