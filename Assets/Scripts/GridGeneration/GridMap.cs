using UnityEngine;
using System.Collections.Generic;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GridGeneration
{
    public class GridMap : MonoBehaviour
    {
        public event Action<GridMap> MapGenerated;

        private static readonly Vector2Int[] CardinalDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        private static readonly Vector2Int[] SurroundingDirections =
        {
            new Vector2Int(-1, -1),
            new Vector2Int(0, -1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 1),
            new Vector2Int(0, 1),
            new Vector2Int(1, 1)
        };

        [SerializeField] private int width = 20;
        [SerializeField] private int height = 20;
        public int Width => width;
        public int Height => height;
        [SerializeField] private float spacing, tileSize;
        [SerializeField] private GridTile[] tiles;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private float playerSpawnYOffset = 0f;
        [Header("City Start Marker")]
        [SerializeField] private bool showCityStartMarker = true;
        [SerializeField] private GameObject cityStartMarkerPrefab;
        [SerializeField] private bool hideCityStartMarkerAfterTurns = true;
        [SerializeField, Min(1)] private int cityStartMarkerVisibleTurns = 1;
        [SerializeField, Min(0f)] private float cityStartMarkerYOffset = 0.35f;
        [SerializeField, Min(0.1f)] private float cityStartMarkerScale = 0.8f;
        [SerializeField] private Color cityStartMarkerColor = new Color(1f, 0.85f, 0.2f, 0.95f);
        [SerializeField, Min(0)] private int cityStartMarkerSortingOrder = 70;
        [SerializeField] private bool pulseCityStartMarker = true;
        [SerializeField, Min(0.1f)] private float cityStartMarkerPulseSpeed = 3f;
        [SerializeField, Range(0f, 0.4f)] private float cityStartMarkerPulseAmplitude = 0.12f;
        [SerializeField] public int seed;
        [SerializeField, Range(0f, 1f)] private float obstaclePercent = 0.30f;
        [SerializeField, Min(0.001f)] private float obstacleNoiseScale = 0.2f;
        [SerializeField, Min(0.001f)] private float landNoiseScale = 0.2f;
        [SerializeField, Range(0f, 1f)] private float grassDensity = 0.7f;
        [SerializeField, Min(0)] private int minDistance = 1;
        [SerializeField, Min(0)] private int minQuests = 1;
        [SerializeField, Min(0)] private int maxQuests = 3;
        [Header("Starter Economy Guarantee")]
        [SerializeField] private bool guaranteeStarterResourceNodes = true;
        [SerializeField, Min(1)] private int starterResourceMinDistanceFromCity = 2;
        [SerializeField, Min(1)] private int starterResourceRadius = 3;
        [SerializeField] private bool protectStarterResourcePaths = true;
        [SerializeField] private bool logStarterResourcePlacement = false;
        [Header("Mine Source Balance")]
        [SerializeField] private bool limitMineSourceTiles = true;
        [SerializeField, Range(0f, 1f)] private float maxMineSourcePercent = 0.05f;
        [SerializeField, Min(0)] private int minMineSourceTiles = 6;
        [SerializeField] private bool preserveNearestMineSourceToCity = true;
        [SerializeField] private bool logMineSourceBalancing = false;
        public GridTile[,] tileMap { get; private set; }
        private GameObject[,] gameObjectMap;
        private bool[,] protectedTileMap;
        private int placementTries = 1000;
        private const int maxVillages = 5, minVillages = 3;
        private float landNoiseMin;
        private float landNoiseMax;
        private float[] nonGrassVariantThresholds;
        private GameObject cityStartMarkerInstance;
        private int cityStartMarkerTurnsRemaining;
        private Vector3 cityStartMarkerBaseScale = Vector3.one;
        [SerializeField] private Turns turns;
        private bool isTurnEventsBound;

        public float ObstaclePercent
        {
            get => obstaclePercent;
            set => obstaclePercent = Mathf.Clamp01(value);
        }

        public bool LimitMineSourceTiles
        {
            get => limitMineSourceTiles;
            set => limitMineSourceTiles = value;
        }

        public float MaxMineSourcePercent
        {
            get => maxMineSourcePercent;
            set => maxMineSourcePercent = Mathf.Clamp01(value);
        }

        public int MinMineSourceTiles
        {
            get => minMineSourceTiles;
            set => minMineSourceTiles = Mathf.Max(0, value);
        }

        public bool GuaranteeStarterResourceNodes
        {
            get => guaranteeStarterResourceNodes;
            set => guaranteeStarterResourceNodes = value;
        }

        public int StarterResourceMinDistanceFromCity
        {
            get => starterResourceMinDistanceFromCity;
            set
            {
                starterResourceMinDistanceFromCity = Mathf.Max(1, value);
                if (starterResourceRadius < starterResourceMinDistanceFromCity)
                {
                    starterResourceRadius = starterResourceMinDistanceFromCity;
                }
            }
        }

        public int StarterResourceRadius
        {
            get => starterResourceRadius;
            set => starterResourceRadius = Mathf.Max(StarterResourceMinDistanceFromCity, value);
        }

        public bool PreserveNearestMineSourceToCity
        {
            get => preserveNearestMineSourceToCity;
            set => preserveNearestMineSourceToCity = value;
        }

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

        private void Awake()
        {
            if (seed == 0)
            {
                seed = GenerateRandomSeed();
            }
        }

        private void Start()
        {
            GenerateLandMap();
        }

        private void OnEnable()
        {
            BindTurnEvents();
        }

        private void OnDisable()
        {
            UnbindTurnEvents();
        }

        private void Update()
        {
            if (pulseCityStartMarker && cityStartMarkerInstance != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * cityStartMarkerPulseSpeed) * cityStartMarkerPulseAmplitude;
                cityStartMarkerInstance.transform.localScale = cityStartMarkerBaseScale * pulse;
            }
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

        public void GenerateLandMap()
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

            List<GridTile> obstacleTiles = FindObstacleCandidateTiles();
            if (obstacleTiles.Count == 0)
            {
                Debug.LogError("GridMap: No tiles with type Obstacle, Mountain, or River found.", this);
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
            Vector2Int cityCoordinate = new Vector2Int(-1, -1);

            bool cityPlaced = GenerateVillageMap(rng, true, settlementNodes, out cityCoordinate);
            if (cityPlaced)
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

            if (cityPlaced)
            {
                EnsureStarterResourcesAroundCity(rng, cityCoordinate);
            }

            // Reserve protected paths first so quest placement logic can avoid them.
            ReservePathsForSettlements(rng, settlementNodes);
            GenerateQuests(rng);
            PlaceObstacles(rng, obstacleTiles);
            BalanceMineSourceTiles(rng, landTiles, cityPlaced ? cityCoordinate : (Vector2Int?)null);
            RefreshCityStartMarker(cityPlaced, cityCoordinate);

            MapGenerated?.Invoke(this);
        }

        private void BalanceMineSourceTiles(System.Random rng, List<GridTile> landTiles, Vector2Int? cityCoordinate)
        {
            if (!limitMineSourceTiles || rng == null || tileMap == null)
            {
                return;
            }

            var mineCoordinates = new List<Vector2Int>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (IsMineSourceTile(tileMap[x, y]))
                    {
                        mineCoordinates.Add(new Vector2Int(x, y));
                    }
                }
            }

            if (mineCoordinates.Count == 0)
            {
                return;
            }

            int byPercent = Mathf.RoundToInt(Width * Height * Mathf.Clamp01(maxMineSourcePercent));
            int maxAllowed = Mathf.Max(minMineSourceTiles, byPercent);
            if (mineCoordinates.Count <= maxAllowed)
            {
                return;
            }

            bool hasPreservedMine = false;
            Vector2Int preservedMineCoordinate = new Vector2Int(-1, -1);
            if (preserveNearestMineSourceToCity
                && cityCoordinate.HasValue
                && TryFindNearestMineSourceToCity(cityCoordinate.Value, mineCoordinates, out preservedMineCoordinate))
            {
                hasPreservedMine = true;
                mineCoordinates.Remove(preservedMineCoordinate);
            }

            ShuffleList(mineCoordinates, rng);

            int effectiveAllowedAfterPreserve = Mathf.Max(0, maxAllowed - (hasPreservedMine ? 1 : 0));
            int removableCount = mineCoordinates.Count - effectiveAllowedAfterPreserve;
            if (removableCount <= 0)
            {
                return;
            }

            GridTile defaultLandTile = (landTiles != null && landTiles.Count > 0)
                ? landTiles[0]
                : FindFirstTileByType(TileType.Land);

            int removedCount = 0;
            for (int i = 0; i < mineCoordinates.Count && removedCount < removableCount; i++)
            {
                Vector2Int coordinate = mineCoordinates[i];
                GridTile currentTile = tileMap[coordinate.x, coordinate.y];
                GridTile replacementTile = ResolveMineSourceReplacement(currentTile, defaultLandTile);
                if (replacementTile == null || replacementTile == currentTile)
                {
                    continue;
                }

                ReplaceTileAt(coordinate.x, coordinate.y, replacementTile);
                removedCount++;
            }

            if (logMineSourceBalancing)
            {
                int finalMineCount = CountMineSourceTiles();
                Debug.Log($"GridMap: Mine source balancing removed {removedCount} tiles. Remaining mine sources: {finalMineCount}.", this);
            }
        }

        private bool TryFindNearestMineSourceToCity(Vector2Int cityCoordinate, List<Vector2Int> mineCoordinates, out Vector2Int nearestMine)
        {
            nearestMine = new Vector2Int(-1, -1);
            if (mineCoordinates == null || mineCoordinates.Count == 0)
            {
                return false;
            }

            int bestDistance = int.MaxValue;
            for (int i = 0; i < mineCoordinates.Count; i++)
            {
                int distance = ManhattanDistance(cityCoordinate, mineCoordinates[i]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearestMine = mineCoordinates[i];
                }
            }

            return bestDistance != int.MaxValue;
        }

        private static GridTile ResolveMineSourceReplacement(GridTile currentTile, GridTile defaultLandTile)
        {
            if (currentTile == null)
            {
                return null;
            }

            if (IsNamedTile(currentTile, "Valley"))
            {
                return defaultLandTile;
            }

            return null;
        }

        private int CountMineSourceTiles()
        {
            if (tileMap == null)
            {
                return 0;
            }

            int count = 0;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (IsMineSourceTile(tileMap[x, y]))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static bool IsMineSourceTile(GridTile tile)
        {
            return IsNamedTile(tile, "Valley") || (tile != null && tile.tileType == TileType.Valley);
        }

        private static bool IsNamedTile(GridTile tile, string expectedName)
        {
            return tile != null
                && !string.IsNullOrWhiteSpace(expectedName)
                && string.Equals(tile.tileName, expectedName, StringComparison.OrdinalIgnoreCase);
        }

        private void GenerateQuests(System.Random rng)
        {
            if (rng == null || tileMap == null || protectedTileMap == null)
            {
                return;
            }

            List<GridTile> questTiles = FindTilesByType(TileType.Quest);
            if (questTiles.Count == 0)
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
            int maxCandidateTries = villageCoordinates.Count * 8;
            for (int attempt = 0; attempt < maxCandidateTries; attempt++)
            {
                Vector2Int village = villageCoordinates[rng.Next(0, villageCoordinates.Count)];
                Vector2Int direction = SurroundingDirections[rng.Next(0, SurroundingDirections.Length)];
                Vector2Int candidate = village + direction;
                if (!IsInsideGrid(candidate))
                {
                    continue;
                }

                // Quests are never allowed on protected tiles.
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

            // Build a deterministic shuffled pool so quest variants do not repeat
            // until all available quest variants have been used once.
            var questPool = new List<GridTile>(questTiles);
            ShuffleList(questPool, rng);
            int questPoolIndex = 0;

            for (int i = 0; i < placedQuestCount; i++)
            {
                Vector2Int coordinate = candidateCoordinates[i];

                if (protectedTileMap[coordinate.x, coordinate.y])
                {
                    continue;
                }

                if (questPoolIndex >= questPool.Count)
                {
                    // Not enough different quest tiles for all placements: reshuffle and reuse.
                    ShuffleList(questPool, rng);
                    questPoolIndex = 0;
                }

                GridTile questTile = questPool[questPoolIndex++];
                ReplaceTileAt(coordinate.x, coordinate.y, questTile);
                protectedTileMap[coordinate.x, coordinate.y] = true;
            }
        }

        private static void ShuffleList<T>(List<T> list, System.Random rng)
        {
            if (list == null || rng == null)
            {
                return;
            }

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
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

        private void RefreshCityStartMarker(bool cityPlaced, Vector2Int cityCoordinate)
        {
            ClearCityStartMarker();
            cityStartMarkerTurnsRemaining = 0;

            if (!showCityStartMarker || !cityPlaced)
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

            Vector3 markerPosition = cityTile.transform.position + Vector3.up * Mathf.Max(0f, cityStartMarkerYOffset) * 0.9f;
            cityStartMarkerInstance = new GameObject("CityStartMarker");
            cityStartMarkerInstance.transform.SetParent(transform);
            cityStartMarkerInstance.transform.position = markerPosition;
            cityStartMarkerBaseScale = Vector3.one * Mathf.Max(0.1f, cityStartMarkerScale);
            cityStartMarkerInstance.transform.localScale = cityStartMarkerBaseScale;
            LookAtCamera lookAtCamera = cityStartMarkerInstance.AddComponent<LookAtCamera>();
            lookAtCamera.InvertForward = true;

            if (hideCityStartMarkerAfterTurns)
            {
                cityStartMarkerTurnsRemaining = 1;
                BindTurnEvents();
            }

            float safeScale = Mathf.Max(0.1f, cityStartMarkerScale);

            if (cityStartMarkerPrefab != null)
            {
                GameObject markerPrefabInstance = Instantiate(cityStartMarkerPrefab, cityStartMarkerInstance.transform);
                markerPrefabInstance.name = "IconPrefab";
                markerPrefabInstance.transform.localPosition = Vector3.zero;
                markerPrefabInstance.transform.localRotation = Quaternion.identity;
                markerPrefabInstance.transform.localScale = Vector3.one * safeScale;
            }
            else
            {
                CreateFallbackCityStartArrow(safeScale);
            }
        }

        private void CreateFallbackCityStartArrow(float safeScale)
        {
            var arrowObject = new GameObject("Arrow");
            arrowObject.transform.SetParent(cityStartMarkerInstance.transform, false);
            arrowObject.transform.localPosition = new Vector3(0f, -0.12f * safeScale, 0f);

            var arrowMesh = arrowObject.AddComponent<TextMesh>();
            arrowMesh.text = "▼";
            arrowMesh.anchor = TextAnchor.MiddleCenter;
            arrowMesh.alignment = TextAlignment.Center;
            arrowMesh.fontStyle = FontStyle.Bold;
            arrowMesh.fontSize = 120;
            arrowMesh.characterSize = 0.17f * safeScale;
            arrowMesh.color = cityStartMarkerColor;
            ApplyMarkerFontAndSorting(arrowMesh, cityStartMarkerSortingOrder);
        }

        private static void ApplyMarkerFontAndSorting(TextMesh textMesh, int sortingOrder)
        {
            if (textMesh == null)
            {
                return;
            }

            Font markerFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (markerFont == null)
            {
                return;
            }

            textMesh.font = markerFont;
            MeshRenderer meshRenderer = textMesh.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = markerFont.material;
                meshRenderer.sortingOrder = sortingOrder;
            }
        }

        private void ClearCityStartMarker()
        {
            if (cityStartMarkerInstance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(cityStartMarkerInstance);
            }
            else
            {
                DestroyImmediate(cityStartMarkerInstance);
            }

            cityStartMarkerInstance = null;
            cityStartMarkerTurnsRemaining = 0;
            cityStartMarkerBaseScale = Vector3.one;
        }

        private void BindTurnEvents()
        {
            if (isTurnEventsBound)
            {
                return;
            }

            if (turns == null)
            {
                turns = FindAnyObjectByType<Turns>();
            }

            if (turns == null)
            {
                return;
            }

            turns.TurnEnded -= OnTurnEndedForCityStartMarker;
            turns.TurnEnded += OnTurnEndedForCityStartMarker;
            isTurnEventsBound = true;
        }

        private void UnbindTurnEvents()
        {
            if (!isTurnEventsBound)
            {
                return;
            }

            if (turns != null)
            {
                turns.TurnEnded -= OnTurnEndedForCityStartMarker;
            }

            isTurnEventsBound = false;
        }

        private void OnTurnEndedForCityStartMarker(Turns _)
        {
            if (!hideCityStartMarkerAfterTurns || cityStartMarkerInstance == null)
            {
                return;
            }

            cityStartMarkerTurnsRemaining = Mathf.Max(0, cityStartMarkerTurnsRemaining - 1);
            if (cityStartMarkerTurnsRemaining == 0)
            {
                ClearCityStartMarker();
            }
        }

        private static int GenerateRandomSeed()
        {
            int generatedSeed;
            do
            {
                generatedSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            }
            while (generatedSeed == 0);

            return generatedSeed;
        }

        private void EnsureStarterResourcesAroundCity(System.Random rng, Vector2Int cityCoordinate)
        {
            if (!guaranteeStarterResourceNodes || rng == null || !IsInsideGrid(cityCoordinate))
            {
                return;
            }

            GridTile guaranteedForestTile = FindFirstTileByExactName("Forest") ?? FindFirstTileByType(TileType.Forest);
            GridTile guaranteedMineTile = FindFirstTileByExactName("Valley")
                                          ?? FindFirstTileByType(TileType.Valley);

            var reservedCoordinates = new HashSet<Vector2Int>();

            if (guaranteedForestTile != null)
            {
                if (TryPlaceStarterTileNearCity(rng, cityCoordinate, guaranteedForestTile, reservedCoordinates, out Vector2Int forestCoordinate))
                {
                    if (protectStarterResourcePaths)
                    {
                        ProtectWalk(cityCoordinate, forestCoordinate);
                    }

                    reservedCoordinates.Add(forestCoordinate);
                    if (logStarterResourcePlacement)
                    {
                        Debug.Log($"GridMap: Guaranteed forest tile at ({forestCoordinate.x}, {forestCoordinate.y}).", this);
                    }
                }
                else
                {
                    Debug.LogWarning("GridMap: Could not place guaranteed forest tile near city.", this);
                }
            }

            if (guaranteedMineTile != null)
            {
                if (TryPlaceStarterTileNearCity(rng, cityCoordinate, guaranteedMineTile, reservedCoordinates, out Vector2Int mineCoordinate))
                {
                    if (protectStarterResourcePaths)
                    {
                        ProtectWalk(cityCoordinate, mineCoordinate);
                    }

                    reservedCoordinates.Add(mineCoordinate);
                    if (logStarterResourcePlacement)
                    {
                        Debug.Log($"GridMap: Guaranteed mine tile '{guaranteedMineTile.tileName}' at ({mineCoordinate.x}, {mineCoordinate.y}).", this);
                    }
                }
                else
                {
                    Debug.LogWarning("GridMap: Could not place guaranteed mine tile near city.", this);
                }
            }
        }

        private bool TryPlaceStarterTileNearCity(System.Random rng, Vector2Int cityCoordinate, GridTile targetTile, HashSet<Vector2Int> blockedCoordinates, out Vector2Int placedCoordinate)
        {
            placedCoordinate = new Vector2Int(-1, -1);
            if (targetTile == null)
            {
                return false;
            }

            int radius = Mathf.Max(1, starterResourceRadius);
            int minDistance = Mathf.Clamp(starterResourceMinDistanceFromCity, 1, radius);
            var allCandidates = new List<Vector2Int>();

            for (int distance = minDistance; distance <= radius; distance++)
            {
                for (int dx = -distance; dx <= distance; dx++)
                {
                    int remaining = distance - Mathf.Abs(dx);
                    int[] ys = { remaining, -remaining };

                    for (int i = 0; i < ys.Length; i++)
                    {
                        int dy = ys[i];
                        Vector2Int candidate = new Vector2Int(cityCoordinate.x + dx, cityCoordinate.y + dy);
                        if (!IsStarterPlacementCandidate(candidate, blockedCoordinates))
                        {
                            continue;
                        }

                        if (!allCandidates.Contains(candidate))
                        {
                            allCandidates.Add(candidate);
                        }
                    }
                }
            }

            if (allCandidates.Count == 0)
            {
                return false;
            }

            Vector2Int chosenCoordinate = allCandidates[rng.Next(0, allCandidates.Count)];
            ReplaceTileAt(chosenCoordinate.x, chosenCoordinate.y, targetTile);
            ProtectCoordinate(chosenCoordinate.x, chosenCoordinate.y);
            placedCoordinate = chosenCoordinate;
            return true;
        }

        private bool IsStarterPlacementCandidate(Vector2Int coordinate, HashSet<Vector2Int> blockedCoordinates)
        {
            if (!IsInsideGrid(coordinate))
            {
                return false;
            }

            if (blockedCoordinates != null && blockedCoordinates.Contains(coordinate))
            {
                return false;
            }

            GridTile tile = tileMap[coordinate.x, coordinate.y];
            if (tile == null)
            {
                return false;
            }

            return tile.tileType != TileType.City && tile.tileType != TileType.Village;
        }

        private GridTile FindFirstTileByExactName(string tileName)
        {
            if (tiles == null || string.IsNullOrWhiteSpace(tileName))
            {
                return null;
            }

            for (int i = 0; i < tiles.Length; i++)
            {
                GridTile tile = tiles[i];
                if (tile == null)
                {
                    continue;
                }

                if (string.Equals(tile.tileName, tileName, StringComparison.OrdinalIgnoreCase))
                {
                    return tile;
                }
            }

            return null;
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

                edges.Add(new NodeEdge(bestFrom, bestTo));
                visited[bestTo] = true;
            }

            return edges;
        }

        private void ProtectWalk(Vector2Int start, Vector2Int end)
        {
            if (protectedTileMap == null)
            {
                return;
            }

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

        private static int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
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

            // Use deterministic hashing for an even distribution of obstacle variants.
            unchecked
            {
                int hash = x * 73856093;
                hash ^= y * 19349663;
                hash ^= seed * 83492791;
                int obstacleIndex = (hash & 0x7fffffff) % obstacleTiles.Count;
                return obstacleTiles[obstacleIndex];
            }
        }

        private List<GridTile> FindObstacleCandidateTiles()
        {
            var result = new List<GridTile>();
            var seen = new HashSet<GridTile>();
            AddTilesByTypeUnique(TileType.Obstacle, result, seen);
            AddTilesByTypeUnique(TileType.Mountain, result, seen);
            AddTilesByTypeUnique(TileType.River, result, seen);
            return result;
        }

        private void AddTilesByTypeUnique(TileType tileType, List<GridTile> result, HashSet<GridTile> seen)
        {
            List<GridTile> found = FindTilesByType(tileType);
            for (int i = 0; i < found.Count; i++)
            {
                GridTile tile = found[i];
                if (tile != null && seen.Add(tile))
                {
                    result.Add(tile);
                }
            }
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
            int cityStartMarkerPrefabId = 0;

            if (tiles != null && tiles.Length > 0 && tiles[0] != null)
            {
                baseTileId = tiles[0].GetEntityId().GetHashCode();
                if (tiles[0].tilePrefab != null)
                {
                    basePrefabId = tiles[0].tilePrefab.GetEntityId().GetHashCode();
                }
            }

            if (cityStartMarkerPrefab != null)
            {
                cityStartMarkerPrefabId = cityStartMarkerPrefab.GetEntityId().GetHashCode();
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
                hash = hash * 31 + guaranteeStarterResourceNodes.GetHashCode();
                hash = hash * 31 + starterResourceMinDistanceFromCity;
                hash = hash * 31 + starterResourceRadius;
                hash = hash * 31 + protectStarterResourcePaths.GetHashCode();
                hash = hash * 31 + logStarterResourcePlacement.GetHashCode();
                hash = hash * 31 + limitMineSourceTiles.GetHashCode();
                hash = hash * 31 + maxMineSourcePercent.GetHashCode();
                hash = hash * 31 + minMineSourceTiles;
                hash = hash * 31 + preserveNearestMineSourceToCity.GetHashCode();
                hash = hash * 31 + logMineSourceBalancing.GetHashCode();
                hash = hash * 31 + showCityStartMarker.GetHashCode();
                hash = hash * 31 + hideCityStartMarkerAfterTurns.GetHashCode();
                hash = hash * 31 + cityStartMarkerVisibleTurns;
                hash = hash * 31 + cityStartMarkerYOffset.GetHashCode();
                hash = hash * 31 + cityStartMarkerScale.GetHashCode();
                hash = hash * 31 + cityStartMarkerColor.GetHashCode();
                hash = hash * 31 + cityStartMarkerSortingOrder;
                hash = hash * 31 + cityStartMarkerPrefabId;
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
            cityStartMarkerInstance = null;
            cityStartMarkerTurnsRemaining = 0;
            cityStartMarkerBaseScale = Vector3.one;
        }
    }
}
