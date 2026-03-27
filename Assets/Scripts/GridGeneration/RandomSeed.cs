using GridGeneration;
using UnityEngine;

public class RandomSeed : MonoBehaviour
{
    [SerializeField] private GridMap gridMap;
    [SerializeField] private bool regenerateMapOnAwake = false;

    public int minValue;
    public int maxValue;

    private void Awake()
    {
        if (gridMap == null)
        {
            gridMap = GetComponent<GridMap>();
        }

        if (gridMap == null)
        {
            gridMap = FindObjectOfType<GridMap>();
        }

        if (gridMap == null)
        {
            Debug.LogError("RandomSeed: GridMap reference is missing.", this);
            return;
        }

        gridMap.seed = Random.Range(minValue, maxValue);

        if (regenerateMapOnAwake)
        {
            gridMap.GenerateLandMap();
        }
    }
}
