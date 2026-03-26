using System.Collections.Generic;
using GridGeneration;
using UnityEngine;

public class WinCondition : MonoBehaviour
{
    [SerializeField] private GridMap gridMap;
    [SerializeField] private TileType[] walkableTypes = { TileType.City, TileType.Village, TileType.Road };

    public GridTile[,] road;
    public GridTile currentTile;

    private void Awake()
    {
        if (gridMap == null)
        {
            gridMap = GetComponent<GridMap>();
        }
    }

    private void Start()
    {
        if (gridMap == null || gridMap.tileMap == null)
        {
            Debug.LogError("WinCondition: GridMap is missing or not initialized.", this);
            return;
        }

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

        if (paths.Count == villages.Count)
        {
            Debug.Log("path found");
        }
        else
        {
            Debug.LogWarning("WinCondition: No walkable path found.");
        }

        foreach (KeyValuePair<Vector2Int, List<Vector2Int>> path in paths)
        {
            if (path.Value.Count > 0)
            {
                Vector2Int last = path.Value[path.Value.Count - 1];
                currentTile = gridMap.tileMap[last.x, last.y];
            }

            break;
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

                if (!IsWalkable(gridMap.tileMap[nx, ny].tileType))
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
