using GridGeneration;
using UnityEngine;

public class RandomSeed : MonoBehaviour
{
    [SerializeField] private GridMap gridMap;
    [SerializeField] private bool regenerateMapOnAwake = false;

    private static bool _hasForcedSeedForNextLoad;
    private static int _forcedSeedForNextLoad;

    public int minValue;
    public int maxValue;

    public static void ForceSeedForNextLoad(int seed)
    {
        _forcedSeedForNextLoad = seed;
        _hasForcedSeedForNextLoad = true;
    }

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

        if (_hasForcedSeedForNextLoad)
        {
            gridMap.seed = _forcedSeedForNextLoad;
            _hasForcedSeedForNextLoad = false;
        }
        else
        {
            gridMap.seed = Random.Range(minValue, maxValue);
        }

        if (regenerateMapOnAwake)
        {
            gridMap.GenerateLandMap();
        }
    }
}
