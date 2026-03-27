using GridGeneration;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class WinCondition : MonoBehaviour
{
    [SerializeField] private GridMap gridMap;
    [SerializeField] private Turns turns;
    [SerializeField] private TileType[] walkableTypes = { TileType.City, TileType.Village, TileType.Road };

    public GridTile[,] road;
    private GridTile currentTile;
    private bool hasWon;

    private TileBuilding tileBuilding;
    private HashSet<Vector2Int> builtRoads;
    private FieldInfo builtRoadsField;
    private int lastBuiltRoadCount = -1;

    private void Awake()
    {
        if (gridMap == null)
        {
            gridMap = GetComponent<GridMap>();
        }

        if (turns == null)
        {
            turns = GetComponent<Turns>();
        }

        if (gridMap == null)
        {
            gridMap = FindObjectOfType<GridMap>();
        }

        if (turns == null)
        {
            turns = FindObjectOfType<Turns>();
        }

        tileBuilding = FindObjectOfType<TileBuilding>();
    }

    private IEnumerator Start()
    {
        if (gridMap == null)
        {
            Debug.LogError("WinCondition: GridMap reference is missing.", this);
            yield break;
        }

        yield return new WaitUntil(() => gridMap.tileMap != null);

        CacheBuiltRoadReference();
        EvaluateWinCondition();
    }

    private void Update()
    {
        if (gridMap == null || gridMap.tileMap == null)
        {
            return;
        }

        if (!TryGetBuiltRoadCount(out int currentBuiltRoadCount))
        {
            return;
        }

        if (currentBuiltRoadCount == lastBuiltRoadCount)
        {
            return;
        }

        lastBuiltRoadCount = currentBuiltRoadCount;
        EvaluateWinCondition();
    }

    private void CacheBuiltRoadReference()
    {
        if (tileBuilding == null)
        {
            tileBuilding = FindObjectOfType<TileBuilding>();
        }

        if (tileBuilding == null)
        {
            return;
        }

        builtRoadsField = typeof(TileBuilding).GetField("builtRoads", BindingFlags.Instance | BindingFlags.NonPublic);
        if (builtRoadsField == null)
        {
            Debug.LogWarning("WinCondition: Could not access TileBuilding.builtRoads.", this);
            return;
        }

        builtRoads = builtRoadsField.GetValue(tileBuilding) as HashSet<Vector2Int>;
    }

    private bool TryGetBuiltRoadCount(out int count)
    {
        count = 0;

        if (builtRoads == null)
        {
            CacheBuiltRoadReference();
            if (builtRoads == null)
            {
                return false;
            }
        }

        count = builtRoads.Count;
        return true;
    }

    public void EvaluateWinCondition()
    {
        if (!TryFindCity(out Vector2Int cityPos))
        {
            Debug.LogError("WinCondition: No city tile found.", this);
            return;
        }

        List<Vector2Int> villages = FindVillages();
        if (villages.Count == 0)
        {
            Debug.LogWarning("WinCondition: No villages found.", this);
            return;
        }

        Dictionary<Vector2Int, List<Vector2Int>> paths = FindShortestPaths(cityPos, villages);

        bool allConnected = paths.Count == villages.Count;
        if (allConnected && !hasWon)
        {
            hasWon = true;

            if (turns != null)
            {
                turns.TriggerWin();
            }
            else
            {
                Debug.LogError("WinCondition: Turns reference is missing. Cannot trigger win.", this);
            }
        }
        else if (!allConnected)
        {
            hasWon = false;
        }
    }

    private bool TryFindCity(out Vector2Int cityPos)
    {
        for (int x = 0; x < gridMap.Width; x++)
        {
            for (int y = 0; y < gridMap.Height; y++)
            {
                if (gridMap.tileMap[x, y].tileType == TileType.City)
                {
                    cityPos = new Vector2Int(x, y);
                    currentTile = gridMap.tileMap[x, y];
                    return true;
                }
            }
        }

        cityPos = default;
        return false;
    }

    private List<Vector2Int> FindVillages()
    {
        var villages = new List<Vector2Int>();
        for (int x = 0; x < gridMap.Width; x++)
        {
            for (int y = 0; y < gridMap.Height; y++)
            {
                if (gridMap.tileMap[x, y].tileType == TileType.Village)
                {
                    villages.Add(new Vector2Int(x, y));
                }
            }
        }

        return villages;
    }

    private Dictionary<Vector2Int, List<Vector2Int>> FindShortestPaths(Vector2Int start, List<Vector2Int> targets)
    {
        var paths = new Dictionary<Vector2Int, List<Vector2Int>>();
        bool[,] visited = new bool[gridMap.Width, gridMap.Height];
        Vector2Int?[,] parent = new Vector2Int?[gridMap.Width, gridMap.Height];

        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        visited[start.x, start.y] = true;

        var targetSet = new HashSet<Vector2Int>(targets);

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (queue.Count > 0 && targetSet.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            for (int dir = 0; dir < 4; dir++)
            {
                int nx = current.x + dx[dir];
                int ny = current.y + dy[dir];

                if (!IsInBounds(nx, ny) || visited[nx, ny])
                {
                    continue;
                }

                if (!IsTraversable(nx, ny))
                {
                    continue;
                }

                visited[nx, ny] = true;
                parent[nx, ny] = current;
                var next = new Vector2Int(nx, ny);
                queue.Enqueue(next);

                if (targetSet.Contains(next))
                {
                    paths[next] = ReconstructPath(next, parent);
                    targetSet.Remove(next);
                    if (targetSet.Count == 0)
                    {
                        break;
                    }
                }
            }
        }

        return paths;
    }

    private bool IsTraversable(int x, int y)
    {
        TileType tileType = gridMap.tileMap[x, y].tileType;
        if (IsWalkable(tileType))
        {
            return true;
        }

        return builtRoads != null && builtRoads.Contains(new Vector2Int(x, y));
    }

    private bool IsWalkable(TileType tileType)
    {
        for (int i = 0; i < walkableTypes.Length; i++)
        {
            if (walkableTypes[i] == tileType)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsInBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < gridMap.Width && y < gridMap.Height;
    }

    private List<Vector2Int> ReconstructPath(Vector2Int end, Vector2Int?[,] parent)
    {
        var path = new List<Vector2Int>();
        Vector2Int? current = end;

        while (current.HasValue)
        {
            path.Add(current.Value);
            current = parent[current.Value.x, current.Value.y];
        }

        path.Reverse();
        return path;
    }
}
