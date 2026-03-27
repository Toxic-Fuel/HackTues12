using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GridGeneration
{
    public class GridMap : MonoBehaviour
    {
        private static readonly Vector2Int[] CardinalDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        [SerializeField] private int width = 20;
        [SerializeField] private int height = 20;
        public int Width => width;
        public int Height => height;
        [SerializeField] private float spacing, tileSize;
        [SerializeField] private GridTile[] tiles;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private float playerSpawnYOffset = 0f;
        [SerializeField] private int seed;
        [SerializeField, Range(0f, 1f)] private float obstaclePercent = 0.30f;
        [SerializeField, Min(0.001f)] private float obstacleNoiseScale = 0.2f;
        [SerializeField, Min(0.001f)] private float landNoiseScale = 0.2f;
        [SerializeField, Range(0f, 1f)] private float grassDensity = 0.7f;
        [SerializeField, Min(0)] private int minDistance = 1;
        [SerializeField, Min(0)] private int minQuests = 1;
        [SerializeField, Min(0)] private int maxQuests = 3;
        public GridTile[,] tileMap { get; private set; }
        private GameObject[,] gameObjectMap;
        private bool[,] protectedTileMap;
        private int placementTries = 1000;
        private const int maxVillages = 4, minVillages = 1;
        private float landNoiseMin;
        private float landNoiseMax;
        private float[] nonGrassVariantThresholds;

        private struct NodeEdge
        {
            public int from;
            public int to;

            public NodeEdge(int from, int to)
            {
                this.from = from;
                this.to = to;
            }
        }

        // Cache the inspected values so OnValidate regenerates only on relevant changes.
        private int _lastConfigHash;

#if UNITY_EDITOR
        private bool _isEditorRegenQueued;
#endif

        private void Start()
        {
            GenerateLandMap();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            int configHash = GetConfigHash();
            if (configHash == _lastConfigHash)
            {
                return;
            }

            _lastConfigHash = configHash;

#if UNITY_EDITOR
            QueueEditorRegeneration();
#endif
        }

#if UNITY_EDITOR
        private void QueueEditorRegeneration()
        {
            if (_isEditorRegenQueued)
            {
                return;
            }

            _isEditorRegenQueued = true;
            EditorApplication.delayCall += RegenerateInEditor;
        }

        private void RegenerateInEditor()
        {
            EditorApplication.delayCall -= RegenerateInEditor;
            _isEditorRegenQueued = false;

            if (this == null || Application.isPlaying)
            {
                return;
            }

            GenerateLandMap();
        }
#endif

        private void GenerateLandMap()
        {
            if (tiles == null || tiles.Length == 0 || tiles[0] == null)
            {
                Debug.LogError("GridMap: Missing base tile in the tiles array.", this);
                return;
            }

            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null)
                {
                    Debug.LogError($"GridMap: Tile at index {i} is not assigned.", this);
                    return;
                }

                if (tiles[i].tilePrefab == null)
                {
                    Debug.LogError($"GridMap: Tile at index {i} does not have a prefab assigned.", this);
                    return;
                }
            }

            if (Width <= 0 || Height <= 0)
            {
                Debug.LogError("GridMap: Width and height must be greater than 0.", this);
                return;
            }

            if (tileSize <= 0)
            {
                Debug.LogError("GridMap: Tile size must be greater than 0.", this);
                return;
            }

            List<GridTile> landTiles = FindTilesByType(TileType.Land);
            if (landTiles.Count == 0)
            {
                Debug.LogError("GridMap: No tile with type Land found.", this);
                return;
            }

            List<GridTile> obstacleTiles = FindTilesByType(TileType.Obstacle);
            if (obstacleTiles.Count == 0)
            {
                Debug.LogError("GridMap: No tile with type Obstacle found.", this);
                return;
            }

            ClearGeneratedTiles();

            tileMap = new GridTile[Width, Height];
            gameObjectMap = new GameObject[Width, Height];
            protectedTileMap = new bool[Width, Height];

            ComputeLandNoiseRange();
            BuildNonGrassVariantThresholds(landTiles.Count);


            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    GridTile landTile = SelectLandTileForCoordinate(x, y, landTiles);
                    ReplaceTileAt(x, y, landTile);
                }
            }

            var rng = new System.Random(seed);
            var settlementNodes = new List<Vector2Int>();

            if (GenerateVillageMap(rng, true, settlementNodes, out Vector2Int cityCoordinate))
            {
                settlementNodes.Add(cityCoordinate);
                PlacePlayerAtCity(cityCoordinate);
            }

            int villageCount = rng.Next(minVillages, maxVillages + 1);
            int villagesPlaced = 0;
            for (int villageIndex = 0; villageIndex < villageCount; ++villageIndex)
            {
                if (!GenerateVillageMap(rng, false, settlementNodes, out Vector2Int villageCoordinate))
                {
                    break;
                }

                settlementNodes.Add(villageCoordinate);
                villagesPlaced++;
            }

            if (villagesPlaced == 0)
            {
                Debug.LogError("GridMap: No villages were placed.", this);
            }

            GenerateQuests(rng);

            ReservePathsForSettlements(rng, settlementNodes);
            PlaceObstacles(rng, obstacleTiles);
        }

        private void GenerateQuests(System.Random rng)
        {
            if (rng == null || tileMap == null || protectedTileMap == null)
            {
                return;
            }

            GridTile questTile = FindFirstTileByType(TileType.Quest);
            if (questTile == null)
            {
                Debug.LogWarning("GridMap: No tile with type Quest found. Skipping quest placement.", this);
                return;
            }

            var villageCoordinates = new List<Vector2Int>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    GridTile tile = tileMap[x, y];
                    if (tile != null && tile.tileType == TileType.Village)
                    {
                        villageCoordinates.Add(new Vector2Int(x, y));
                    }
                }
            }

            if (villageCoordinates.Count == 0)
            {
                return;
            }

            var candidateCoordinates = new List<Vector2Int>();
            var seenCoordinates = new HashSet<Vector2Int>();
            for (int i = 0; i < villageCoordinates.Count; i++)
            {
                Vector2Int village = villageCoordinates[i];
                for (int directionIndex = 0; directionIndex < CardinalDirections.Length; directionIndex++)
                {
                    Vector2Int candidate = village + CardinalDirections[directionIndex];
                    if (!IsInsideGrid(candidate))
                    {
                        continue;
                    }

                    if (protectedTileMap[candidate.x, candidate.y])
                    {
                        continue;
                    }

                    GridTile candidateTile = tileMap[candidate.x, candidate.y];
                    if (candidateTile == null || candidateTile.tileType != TileType.Land)
                    {
                        continue;
                    }

                    if (seenCoordinates.Add(candidate))
                    {
                        candidateCoordinates.Add(candidate);
                    }
                }
            }

            if (candidateCoordinates.Count == 0)
            {
                Debug.LogWarning("GridMap: No valid tile next to a village for quest placement.", this);
                return;
            }

            for (int i = candidateCoordinates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (candidateCoordinates[i], candidateCoordinates[j]) = (candidateCoordinates[j], candidateCoordinates[i]);
            }

            int questMin = Mathf.Min(minQuests, maxQuests);
            int questMax = Mathf.Max(minQuests, maxQuests);
            int requestedQuestCount = rng.Next(questMin, questMax + 1);
            int placedQuestCount = Mathf.Min(requestedQuestCount, candidateCoordinates.Count);

            for (int i = 0; i < placedQuestCount; i++)
            {
                Vector2Int coordinate = candidateCoordinates[i];
                ReplaceTileAt(coordinate.x, coordinate.y, questTile);
                protectedTileMap[coordinate.x, coordinate.y] = true;
            }
        }

        private bool GenerateVillageMap(System.Random rng, bool placeCity, List<Vector2Int> existingSettlements, out Vector2Int placedCoordinate)
        {
            placedCoordinate = new Vector2Int(-1, -1);
            if (rng == null || tileMap == null || gameObjectMap == null || protectedTileMap == null || Width <= 0 || Height <= 0)
            {
                return false;
            }

            TileType tileType = placeCity ? TileType.City : TileType.Village;
            GridTile protectedTile = FindFirstTileByType(tileType);

            if (protectedTile == null)
            {
                Debug.LogWarning($"GridMap: No tile with type {tileType} found. Skipping placement.", this);
                return false;
            }

            int randomX;
            int randomY;
            bool foundCoordinate = placeCity
                ? TryGetRandomUnprotectedCoordinate(rng, out randomX, out randomY)
                : TryGetRandomVillageCoordinate(rng, existingSettlements, out randomX, out randomY);

            if (!foundCoordinate)
            {
                Debug.LogWarning("GridMap: No available tile left for city/village placement.", this);
                return false;
            }

            ReplaceTileAt(randomX, randomY, protectedTile);
            protectedTileMap[randomX, randomY] = true;
            placedCoordinate = new Vector2Int(randomX, randomY);
            return true;
        }

        private void PlacePlayerAtCity(Vector2Int cityCoordinate)
        {
            if (playerTransform == null)
            {
                return;
            }

            if (gameObjectMap == null || !IsInsideGrid(cityCoordinate))
            {
                return;
            }

            GameObject cityTile = gameObjectMap[cityCoordinate.x, cityCoordinate.y];
            if (cityTile == null)
            {
                return;
            }

            Vector3 cityCenter = cityTile.transform.position;
            playerTransform.position = new Vector3(cityCenter.x, cityCenter.y + playerSpawnYOffset, cityCenter.z);
        }

        private void ReservePathsForSettlements(System.Random rng, List<Vector2Int> settlementNodes)
        {
            if (rng == null || settlementNodes == null || settlementNodes.Count < 2)
            {
                return;
            }

            List<NodeEdge> edges = BuildMinimumSpanningTree(settlementNodes);
            for (int i = 0; i < edges.Count; i++)
            {
                Vector2Int nodeA = settlementNodes[edges[i].from];
                Vector2Int nodeB = settlementNodes[edges[i].to];
                Vector2Int pivot = rng.NextDouble() < 0.5
                    ? new Vector2Int(nodeA.x, nodeB.y)
                    : new Vector2Int(nodeB.x, nodeA.y);

                ProtectWalk(nodeA, pivot);
                ProtectWalk(pivot, nodeB);
            }
        }

        private List<NodeEdge> BuildMinimumSpanningTree(List<Vector2Int> nodes)
        {
            var edges = new List<NodeEdge>();
            if (nodes == null || nodes.Count < 2)
            {
                return edges;
            }

            var visited = new bool[nodes.Count];
            visited[0] = true;

            for (int edgeCount = 0; edgeCount < nodes.Count - 1; edgeCount++)
            {
                int bestFrom = -1;
                int bestTo = -1;
                int bestDistance = int.MaxValue;

                for (int from = 0; from < nodes.Count; from++)
                {
                    if (!visited[from])
                    {
                        continue;
                    }

                    for (int to = 0; to < nodes.Count; to++)
                    {
                        if (visited[to])
                        {
                            continue;
                        }

                        int distance = ManhattanDistance(nodes[from], nodes[to]);
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestFrom = from;
                            bestTo = to;
                        }
                    }
                }

                if (bestTo == -1)
                {
                    break;
                }

                visited[bestTo] = true;
                edges.Add(new NodeEdge(bestFrom, bestTo));
            }

            return edges;
        }

        private static int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private void ProtectWalk(Vector2Int start, Vector2Int end)
        {
            int currentX = start.x;
            int currentY = start.y;
            ProtectCoordinate(currentX, currentY);

            while (currentX != end.x)
            {
                currentX += currentX < end.x ? 1 : -1;
                ProtectCoordinate(currentX, currentY);
            }

            while (currentY != end.y)
            {
                currentY += currentY < end.y ? 1 : -1;
                ProtectCoordinate(currentX, currentY);
            }
        }

        private void ProtectCoordinate(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return;
            }

            protectedTileMap[x, y] = true;
        }

        private void PlaceObstacles(System.Random rng, List<GridTile> obstacleTiles)
        {
            if (rng == null || obstacleTiles == null || obstacleTiles.Count == 0)
            {
                return;
            }

            var availableCoordinates = new List<Vector2Int>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (!protectedTileMap[x, y])
                    {
                        availableCoordinates.Add(new Vector2Int(x, y));
                    }
                }
            }

            for (int i = availableCoordinates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (availableCoordinates[i], availableCoordinates[j]) = (availableCoordinates[j], availableCoordinates[i]);
            }

            int obstacleCount = Mathf.FloorToInt(availableCoordinates.Count * Mathf.Clamp01(obstaclePercent));
            for (int i = 0; i < obstacleCount; i++)
            {
                Vector2Int coordinate = availableCoordinates[i];
                GridTile obstacleTile = SelectObstacleTileForCoordinate(coordinate.x, coordinate.y, obstacleTiles);
                ReplaceTileAt(coordinate.x, coordinate.y, obstacleTile);
            }
        }

        private GridTile SelectObstacleTileForCoordinate(int x, int y, List<GridTile> obstacleTiles)
        {
            if (obstacleTiles == null || obstacleTiles.Count == 0)
            {
                return null;
            }

            if (obstacleTiles.Count == 1)
            {
                return obstacleTiles[0];
            }

            float sampleScale = Mathf.Max(0.001f, obstacleNoiseScale);
            float sampleX = (x + seed * 0.137f) * sampleScale;
            float sampleY = (y - seed * 0.173f) * sampleScale;
            float noiseValue = Mathf.PerlinNoise(sampleX, sampleY);
            int obstacleIndex = Mathf.Clamp(Mathf.FloorToInt(noiseValue * obstacleTiles.Count), 0, obstacleTiles.Count - 1);
            return obstacleTiles[obstacleIndex];
        }

        private GridTile SelectLandTileForCoordinate(int x, int y, List<GridTile> landTiles)
        {
            if (landTiles == null || landTiles.Count == 0)
            {
                return null;
            }

            GridTile grassTile = landTiles[0];
            if (landTiles.Count == 1)
            {
                return grassTile;
            }

            float normalizedNoise = SampleNormalizedLandNoise(x, y);

            // grassDensity controls how much of the map stays on the first Land tile.
            if (normalizedNoise <= Mathf.Clamp01(grassDensity))
            {
                return grassTile;
            }

            int variantCount = landTiles.Count - 1;
            if (variantCount <= 1)
            {
                return landTiles[1];
            }

            if (nonGrassVariantThresholds != null && nonGrassVariantThresholds.Length == variantCount - 1)
            {
                for (int i = 0; i < nonGrassVariantThresholds.Length; i++)
                {
                    if (normalizedNoise <= nonGrassVariantThresholds[i])
                    {
                        return landTiles[i + 1];
                    }
                }

                return landTiles[^1];
            }

            float remapped = Mathf.InverseLerp(Mathf.Clamp01(grassDensity), 1f, normalizedNoise);
            int variantIndex = Mathf.Clamp(Mathf.FloorToInt(remapped * variantCount), 0, variantCount - 1);
            return landTiles[variantIndex + 1];
        }

        private void ComputeLandNoiseRange()
        {
            landNoiseMin = float.MaxValue;
            landNoiseMax = float.MinValue;

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    float value = SampleRawLandNoise(x, y);
                    if (value < landNoiseMin)
                    {
                        landNoiseMin = value;
                    }

                    if (value > landNoiseMax)
                    {
                        landNoiseMax = value;
                    }
                }
            }

            if (landNoiseMin > landNoiseMax)
            {
                landNoiseMin = 0f;
                landNoiseMax = 1f;
            }
        }

        private void BuildNonGrassVariantThresholds(int landTileCount)
        {
            nonGrassVariantThresholds = null;

            int variantCount = landTileCount - 1;
            if (variantCount <= 1)
            {
                return;
            }

            float grassCutoff = Mathf.Clamp01(grassDensity);
            var nonGrassSamples = new List<float>(Width * Height);
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    float normalizedNoise = SampleNormalizedLandNoise(x, y);
                    if (normalizedNoise > grassCutoff)
                    {
                        nonGrassSamples.Add(normalizedNoise);
                    }
                }
            }

            if (nonGrassSamples.Count == 0)
            {
                return;
            }

            nonGrassSamples.Sort();
            nonGrassVariantThresholds = new float[variantCount - 1];
            for (int i = 1; i < variantCount; i++)
            {
                float quantile = i / (float)variantCount;
                int sampleIndex = Mathf.Clamp(Mathf.CeilToInt(nonGrassSamples.Count * quantile) - 1, 0, nonGrassSamples.Count - 1);
                nonGrassVariantThresholds[i - 1] = nonGrassSamples[sampleIndex];
            }
        }

        private float SampleRawLandNoise(int x, int y)
        {
            float sampleScale = Mathf.Max(0.001f, landNoiseScale);
            float sampleX = (x + seed * 0.211f) * sampleScale;
            float sampleY = (y - seed * 0.319f) * sampleScale;
            return Mathf.PerlinNoise(sampleX, sampleY);
        }

        private float SampleNormalizedLandNoise(int x, int y)
        {
            float rawNoise = SampleRawLandNoise(x, y);
            if (Mathf.Approximately(landNoiseMin, landNoiseMax))
            {
                return 0f;
            }

            return Mathf.InverseLerp(landNoiseMin, landNoiseMax, rawNoise);
        }

        public bool IsInsideGrid(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        public bool IsInsideGrid(Vector2Int coordinate)
        {
            return IsInsideGrid(coordinate.x, coordinate.y);
        }

        public GridTile GetTileAt(int x, int y)
        {
            if (!IsInsideGrid(x, y) || tileMap == null)
            {
                return null;
            }

            return tileMap[x, y];
        }

        public GameObject GetTileInstanceAt(int x, int y)
        {
            if (!IsInsideGrid(x, y) || gameObjectMap == null)
            {
                return null;
            }

            return gameObjectMap[x, y];
        }

        public bool TryWorldToGridCoordinate(Vector3 worldPosition, out Vector2Int coordinate)
        {
            coordinate = new Vector2Int(-1, -1);
            if (Width <= 0 || Height <= 0)
            {
                return false;
            }

            float step = tileSize + spacing;
            if (step <= 0f)
            {
                return false;
            }

            Vector3 localPos = transform.InverseTransformPoint(worldPosition);
            int x = Mathf.RoundToInt(localPos.x / step);
            int y = Mathf.RoundToInt(localPos.z / step);

            if (!IsInsideGrid(x, y))
            {
                return false;
            }

            coordinate = new Vector2Int(x, y);
            return true;
        }

        public bool TryBuildRoadAt(int x, int y)
        {
            if (!IsInsideGrid(x, y))
            {
                return false;
            }

            GridTile roadTile = FindFirstTileByType(TileType.Road);
            if (roadTile == null)
            {
                Debug.LogError("GridMap: No tile with type Road found.", this);
                return false;
            }

            ReplaceTileAt(x, y, roadTile);
            return true;
        }

        public bool TryReplaceTileVisualAt(int x, int y, GameObject tilePrefab, Quaternion localRotation)
        {
            if (!IsInsideGrid(x, y) || tilePrefab == null || gameObjectMap == null)
            {
                return false;
            }

            GameObject oldTileInstance = gameObjectMap[x, y];
            if (oldTileInstance != null)
            {
                if (Application.isPlaying) Destroy(oldTileInstance);
                else DestroyImmediate(oldTileInstance);
            }

            float step = tileSize + spacing;
            Vector3 localPos = new Vector3(x * step, 0f, y * step);
            GameObject tileInstance = Instantiate(tilePrefab, transform);
            tileInstance.transform.localPosition = localPos;
            tileInstance.name = $"Tile_{x}_{y}";
            gameObjectMap[x, y] = tileInstance;

            return true;
        }

        private List<GridTile> FindTilesByType(TileType tileType)
        {
            var matchingTiles = new List<GridTile>();
            if (tiles == null)
            {
                return matchingTiles;
            }

            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] != null && tiles[i].tileType == tileType)
                {
                    matchingTiles.Add(tiles[i]);
                }
            }

            return matchingTiles;
        }

        public GridTile FindFirstTileByType(TileType tileType)
        {
            if (tiles == null)
            {
                return null;
            }

            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] != null && tiles[i].tileType == tileType)
                {
                    return tiles[i];
                }
            }

            return null;
        }

        private void ReplaceTileAt(int x, int y, GridTile tile)
        {
            if (tile == null)
            {
                return;
            }

            GameObject oldTileInstance = gameObjectMap[x, y];
            if (oldTileInstance != null)
            {
                if (Application.isPlaying) Destroy(oldTileInstance);
                else DestroyImmediate(oldTileInstance);
            }

            tileMap[x, y] = tile;

            float step = tileSize + spacing;
            Vector3 localPos = new Vector3(x * step, 0f, y * step);
            GameObject tileInstance = Instantiate(tile.tilePrefab, transform);
            tileInstance.transform.localPosition = localPos;
            tileInstance.name = $"Tile_{x}_{y}";
            gameObjectMap[x, y] = tileInstance;
        }

        private bool TryGetRandomVillageCoordinate(System.Random rng, List<Vector2Int> existingSettlements, out int x, out int y)
        {
            int maxAttempts = Mathf.Max(1, placementTries);
            for (int i = 0; i < maxAttempts; i++)
            {
                int candidateX = rng.Next(0, Width);
                int candidateY = rng.Next(0, Height);
                if (protectedTileMap[candidateX, candidateY])
                {
                    continue;
                }

                if (IsInVillageRadius(candidateX, candidateY, existingSettlements))
                {
                    continue;
                }

                x = candidateX;
                y = candidateY;
                return true;
            }

            x = -1;
            y = -1;
            return false;
        }

        private bool IsInVillageRadius(int x, int y, List<Vector2Int> existingSettlements)
        {
            if (existingSettlements == null)
            {
                return false;
            }

            for (int i = 0; i < existingSettlements.Count; i++)
            {
                int dx = Mathf.Abs(x - existingSettlements[i].x);
                int dy = Mathf.Abs(y - existingSettlements[i].y);

                if (dx > minDistance || dy > minDistance)
                {
                    continue;
                }

                if (minDistance > 1 && dx == minDistance && dy == minDistance)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool TryGetRandomUnprotectedCoordinate(System.Random rng, out int x, out int y)
        {
            int attempts = Width * Height * 2;
            for (int i = 0; i < attempts; i++)
            {
                int candidateX = rng.Next(0, Width);
                int candidateY = rng.Next(0, Height);
                if (!protectedTileMap[candidateX, candidateY])
                {
                    x = candidateX;
                    y = candidateY;
                    return true;
                }
            }

            for (int ix = 0; ix < Width; ix++)
            {
                for (int iy = 0; iy < Height; iy++)
                {
                    if (!protectedTileMap[ix, iy])
                    {
                        x = ix;
                        y = iy;
                        return true;
                    }
                }
            }

            x = -1;
            y = -1;
            return false;
        }

        private int GetConfigHash()
        {
            int baseTileId = 0;
            int basePrefabId = 0;

            if (tiles != null && tiles.Length > 0 && tiles[0] != null)
            {
                baseTileId = tiles[0].GetEntityId().GetHashCode();
                if (tiles[0].tilePrefab != null)
                {
                    basePrefabId = tiles[0].tilePrefab.GetEntityId().GetHashCode();
                }
            }

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Width;
                hash = hash * 31 + Height;
                hash = hash * 31 + spacing.GetHashCode();
                hash = hash * 31 + tileSize.GetHashCode();
                hash = hash * 31 + seed;
                hash = hash * 31 + obstaclePercent.GetHashCode();
                hash = hash * 31 + obstacleNoiseScale.GetHashCode();
                hash = hash * 31 + landNoiseScale.GetHashCode();
                hash = hash * 31 + grassDensity.GetHashCode();
                hash = hash * 31 + minDistance;
                hash = hash * 31 + minQuests;
                hash = hash * 31 + maxQuests;
                hash = hash * 31 + baseTileId;
                hash = hash * 31 + basePrefabId;

                if (tiles != null)
                {
                    for (int i = 0; i < tiles.Length; i++)
                    {
                        GridTile tile = tiles[i];
                        if (tile == null || tile.tileType != TileType.Land)
                        {
                            continue;
                        }

                        hash = hash * 31 + tile.GetEntityId().GetHashCode();
                        if (tile.tilePrefab != null)
                        {
                            hash = hash * 31 + tile.tilePrefab.GetEntityId().GetHashCode();
                        }
                    }
                }

                return hash;
            }
        }

        private void ClearGeneratedTiles()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;

                if (Application.isPlaying)
                {
                    Destroy(child);
                }
#if UNITY_EDITOR
                else
                {
                    DestroyImmediate(child);
                }
#endif
            }

            gameObjectMap = null;
            tileMap = null;
            protectedTileMap = null;
        }
    }
}
