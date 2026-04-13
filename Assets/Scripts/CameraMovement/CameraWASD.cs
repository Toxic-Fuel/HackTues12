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
    [SerializeField] private SelectTile selectTile;

    private void OnEnable()
    {
        keyboard = Keyboard.current;
        if (selectTile == null)
        {
            selectTile = FindAnyObjectByType<SelectTile>();
        }
    }

    private void Update()
    {
        if (InGameGenerationMenu.IsAnyMenuOpen)
        {
            movementDirection = Vector3.zero;
            return;
        }

        HandleMovementInput();
        MoveCamera();
    }

    private void HandleMovementInput()
    {
        if (keyboard == null)
            return;

        if (selectTile != null && selectTile.HasSelection)
        {
            movementDirection = Vector3.zero;
            return;
        }

        movementDirection = Vector3.zero;

        Vector3 flatForward = transform.forward;
        flatForward.y = 0;
        flatForward.Normalize();

        Vector3 flatRight = transform.right;
        flatRight.y = 0;
        flatRight.Normalize();

        if (keyboard.wKey.isPressed)
            movementDirection += flatForward;

        if (keyboard.sKey.isPressed)
            movementDirection -= flatForward;

        if (keyboard.dKey.isPressed)
            movementDirection += flatRight;

        if (keyboard.aKey.isPressed)
            movementDirection -= flatRight;

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
