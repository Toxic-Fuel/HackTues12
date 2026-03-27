using UnityEngine;
using UnityEngine.InputSystem;

public class CameraScroll : MonoBehaviour
{
    [SerializeField] private float scrollSpeed = 0.02f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 30f;
    [SerializeField] private float zoomSmoothTime = 0.12f;
    [SerializeField] private SelectTile selectTile;

    private Mouse mouse;
    private Vector3 zoomDirectionLocal;
    private float currentDistance;
    private float targetDistance;
    private float zoomVelocity;

    private void OnEnable()
    {
        mouse = Mouse.current;

        if (selectTile == null)
        {
            selectTile = FindAnyObjectByType<SelectTile>();
        }

        zoomDirectionLocal = transform.localPosition.sqrMagnitude > 0f
            ? transform.localPosition.normalized
            : Vector3.back;

        currentDistance = Mathf.Clamp(transform.localPosition.magnitude, minDistance, maxDistance);
        targetDistance = currentDistance;
        transform.localPosition = zoomDirectionLocal * currentDistance;
    }

    private void Update()
    {
        if (mouse == null)
            return;

        if (selectTile != null && selectTile.HasSelection)
        {
            targetDistance = currentDistance;
            return;
        }

        float scrollDelta = mouse.scroll.ReadValue().y;
        if (!Mathf.Approximately(scrollDelta, 0f))
        {
            targetDistance -= scrollDelta * scrollSpeed;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }

        currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref zoomVelocity, zoomSmoothTime);
        transform.localPosition = zoomDirectionLocal * currentDistance;
    }
}
