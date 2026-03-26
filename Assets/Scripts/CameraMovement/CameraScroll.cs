using UnityEngine;
using UnityEngine.InputSystem;

public class CameraScroll : MonoBehaviour
{
    [SerializeField] private float scrollSpeed = 1f;
    [SerializeField] private float minDistance = 0.2f;
    [SerializeField] private float maxDistance = 3f;

    private Mouse mouse;

    private void OnEnable()
    {
        mouse = Mouse.current;
    }

    private void Update()
    {
        if (mouse == null)
            return;

        float scrollDelta = mouse.scroll.ReadValue().y;
        if (scrollDelta != 0)
        {
            Vector3 flatForward = transform.forward;
            flatForward.y = 0;
            flatForward.Normalize();

            Vector3 scrollDirection = flatForward * scrollDelta * scrollSpeed;
            Vector3 newPosition = transform.position + scrollDirection;

            Vector3 referencePoint = transform.parent != null ? transform.parent.position : Vector3.zero;
            float distance = Vector3.Distance(newPosition, referencePoint);
            
            if (distance >= minDistance && distance <= maxDistance)
            {
                transform.position = newPosition;
            }
        }
    }
}
