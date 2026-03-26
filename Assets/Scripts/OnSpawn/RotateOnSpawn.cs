using UnityEngine;

public class RotateOnSpawn : MonoBehaviour
{
    [SerializeField] private float angleStep = 90f;

    [SerializeField] private int axis = 0;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        int maxSteps = Mathf.Max(1, Mathf.FloorToInt(360f / Mathf.Max(0.001f, angleStep)));
        int randomSteps = Random.Range(0, maxSteps);
        float randomAngle = randomSteps * angleStep;

        Vector3 eulerRotation;
        switch (axis)
        {
            case 0:
                eulerRotation = new Vector3(randomAngle, 0f, 0f);
                break;
            case 2:
                eulerRotation = new Vector3(0f, 0f, randomAngle);
                break;
            default:
                eulerRotation = new Vector3(0f, randomAngle, 0f);
                break;
        }

        transform.localRotation = Quaternion.Euler(eulerRotation);
    }   

    // Update is called once per frame
    void Update()
    {
        
    }
}
