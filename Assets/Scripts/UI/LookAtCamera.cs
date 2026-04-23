using System;
using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    [SerializeField] private bool invertForward;

    public bool InvertForward
    {
        get => invertForward;
        set => invertForward = value;
    }

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindAnyObjectByType<Camera>();
        }
    }

    private void LateUpdate()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindAnyObjectByType<Camera>();
                if (mainCamera == null)
                {
                    return;
                }
            }
        }

        Vector3 viewDirection = mainCamera.transform.position - transform.position;
        if (invertForward)
        {
            viewDirection = -viewDirection;
        }

        if (viewDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(viewDirection.normalized, Vector3.up);
    }
}
