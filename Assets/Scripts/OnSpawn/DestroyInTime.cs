using UnityEngine;

public class DestroyInTime : MonoBehaviour
{
    [SerializeField] private float destroyDelaySeconds = 1f;

    private void Start()
    {
        Destroy(gameObject, Mathf.Max(0f, destroyDelaySeconds));
    }
}
