using GridGeneration;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class TileBuilding : MonoBehaviour
{
    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    [Header("References")]
    [SerializeField] private GridMap gridMap;
    [SerializeField] private Turns turns;
    [SerializeField] private UnityEngine.Camera mainCamera;
    [SerializeField] private SelectTile selectTile;
    [SerializeField] private TileBuildContextPanel tileBuildContextPanel;
    [SerializeField] private FogOfWarController fogOfWar;
    [SerializeField] private VillageCrisisSystem villageCrisisSystem;

    [Header("Road Prefabs")]
    [SerializeField] private GameObject roadVerticalPrefab;
    [SerializeField] private GameObject roadHorizontalPrefab;
    [SerializeField] private GameObject roadNorthEastPrefab;
    [SerializeField] private GameObject roadNorthWestPrefab;
    [SerializeField] private GameObject roadSouthEastPrefab;
    [SerializeField] private GameObject roadSouthWestPrefab;
    [SerializeField] private GameObject roadTcrossNWEPrefab;
    [SerializeField] private GameObject roadTcrossNESPrefab;
    [SerializeField] private GameObject roadTcrossNWSPrefab;
    [SerializeField] private GameObject roadTcrossSEWPrefab;
    [SerializeField] private GameObject roadCrossPrefab;

    [Header("Hover Animation")]
    [SerializeField] private float hoverLiftHeight = 0.2f;
    [SerializeField] private float hoverAnimationSpeed = 8f;

    public float HoverLiftHeight => hoverLiftHeight;
    public float HoverAnimationSpeed => hoverAnimationSpeed;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Testing")]
    [SerializeField] private bool allowBuildOnAnyTileForTesting = true;
    [SerializeField] private bool bypassTurnAndResourceChecksForTesting = true;

    [Header("Effects")]
    [SerializeField] GameObject buildEffectPrefab;
    [SerializeField] private float yEffectOffset = 0.5f;

    [Header("Road Cost Scaling")]
    [SerializeField] private bool useDynamicRoadCostScaling = true;
    [SerializeField, Min(0)] private int freeRoadsBeforeScaling = 2;
    [SerializeField, Min(1)] private int roadsPerCostStep = 1;
    [SerializeField, Min(0)] private int woodPerStep = 1;
    [SerializeField, Min(0)] private int stonePerStep = 1;

    [Header("Map Size Scaling")]
    [SerializeField] private bool scaleRoadCostByMapSize = false;
    [SerializeField, Range(0f, 0.25f)] private float freeRoadsMapAreaRatio = 0.01f;
    [SerializeField, Range(0.001f, 0.25f)] private float roadsPerStepMapAreaRatio = 0.0075f;
    [SerializeField, Range(0f, 1f)] private float mapSizeScalingStrength = 1f;

    private GameObject hoveredTile;
    private Vector3 hoveredBasePosition;
    private Vector2Int hoveredCoordinate = new Vector2Int(-1, -1);
    private readonly HashSet<Vector2Int> builtRoads = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> cachedConnectedNodes = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> countedConnectedVillages = new HashSet<Vector2Int>();
    private bool connectedNodesDirty = true;
    private int connectedVillageCounter;

    public event Action RoadPlaced;
    public event Action<Vector2Int> RoadBuiltAt;
    public event Action<Vector2Int> StructureBuiltAt;

    [SerializeField] private UnityEvent onRoadPlaced;
    [SerializeField] private UnityEvent onBuildingPlaced;

    public bool TryGetHoveredCoordinate(out Vector2Int coordinate)
    {
        coordinate = hoveredCoordinate;
        return hoveredCoordinate.x >= 0 && hoveredCoordinate.y >= 0;
    }

    public bool TryGetHoveredWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        if (hoveredCoordinate.x < 0 || hoveredCoordinate.y < 0)
        {
            return false;
        }

        GameObject tileObject = gridMap.GetTileInstanceAt(hoveredCoordinate.x, hoveredCoordinate.y);
        if (tileObject == null)
        {
            return false;
        }

        worldPosition = tileObject.transform.position;
        return true;
    }

    public bool IsRoadAlreadyBuilt(Vector2Int coordinate)
    {
        return builtRoads.Contains(coordinate);
    }

    public bool IsBlockedForHoverOrSelection(Vector2Int coordinate)
    {
        if (gridMap == null || !gridMap.IsInsideGrid(coordinate))
        {
            return true;
        }

        if (fogOfWar != null && fogOfWar.IsInitialized && !fogOfWar.IsRevealed(coordinate))
        {
            return true;
        }

        if (builtRoads.Contains(coordinate))
        {
            return true;
        }

        GridTile tile = gridMap.GetTileAt(coordinate.x, coordinate.y);
        if (tile == null)
        {
            return true;
        }

        if (tile.tileType == TileType.Road || IsSettlementTile(tile))
        {
            return true;
        }

        return tileBuildContextPanel != null && tileBuildContextPanel.HasCollectorBuildingAt(coordinate);
    }

    public bool CanBuildRoadAt(Vector2Int coordinate)
    {
        if (!gridMap.IsInsideGrid(coordinate))
        {
            return false;
        }

        if (!CanBuildOnTile(coordinate, out _))
        {
            return false;
        }

        if (!allowBuildOnAnyTileForTesting && !HasConnectedNeighbor(coordinate))
        {
            return false;
        }

        return true;
    }

    public bool TryBuildRoadAt(Vector2Int coordinate)
    {
        return TryBuildRoadInternal(coordinate);
    }

    public bool TryGetRoadBuildCost(Vector2Int coordinate, out int[] cost)
    {
        cost = new int[turns != null ? Mathf.Max(0, turns.resourceTypesCount) : 0];

        if (gridMap == null || !gridMap.IsInsideGrid(coordinate))
        {
            return false;
        }

        GridTile tile = gridMap.GetTileAt(coordinate.x, coordinate.y);
        if (tile == null)
        {
            return false;
        }

        return TryGetRoadCostByTile(tile, out cost);
    }

    public int GetConnectedVillageCount()
    {
        return connectedVillageCounter;
    }

    public void NotifyRoadPlacementChanged()
    {
        RoadPlaced?.Invoke();
    }

    public void NotifyStructureBuiltAt(Vector2Int coordinate)
    {
        StructureBuiltAt?.Invoke(coordinate);
        onBuildingPlaced?.Invoke();
    }

    private void Awake()
    {
        if (gridMap == null)
        {
            gridMap = FindAnyObjectByType<GridMap>();
        }

        if (turns == null)
        {
            turns = FindAnyObjectByType<Turns>();
        }

        if (tileBuildContextPanel == null)
        {
            tileBuildContextPanel = FindAnyObjectByType<TileBuildContextPanel>();
        }

        if (fogOfWar == null)
        {
            fogOfWar = FindAnyObjectByType<FogOfWarController>();
        }

        if (villageCrisisSystem == null)
        {
            villageCrisisSystem = FindAnyObjectByType<VillageCrisisSystem>();
        }

        if (mainCamera == null)
        {
            mainCamera = UnityEngine.Camera.main;
        }

        if (selectTile == null)
        {
            selectTile = GetComponent<SelectTile>();
            if (selectTile == null)
            {
                selectTile = FindAnyObjectByType<SelectTile>();
            }
        }

    }

    private void Update()
    {
        if (InGameGenerationMenu.IsAnyMenuOpen)
        {
            ClearHoveredTile();
            AnimateTiles();
            return;
        }

        if (gridMap == null || mainCamera == null)
        {
            return;
        }

        UpdateHoveredTile();
        AnimateTiles();
    }

    private void UpdateHoveredTile()
    {
        if (selectTile == null)
        {
            selectTile = GetComponent<SelectTile>();
            if (selectTile == null)
            {
                selectTile = FindAnyObjectByType<SelectTile>();
            }
        }

        if (Mouse.current == null)
        {
            ClearHoveredTile();
            return;
        }

        if (!TryGetMouseGridCoordinate(out Vector2Int hoveredCoordinate))
        {
            ClearHoveredTile();
            return;
        }

        if (selectTile != null && selectTile.IsSelectedCoordinate(hoveredCoordinate))
        {
            ClearHoveredTile();
            return;
        }

        if (selectTile != null && selectTile.IsRecentlyDeselectedCoordinate(hoveredCoordinate))
        {
            ClearHoveredTile();
            return;
        }

        if (IsBlockedForHoverOrSelection(hoveredCoordinate))
        {
            ClearHoveredTile();
            return;
        }

        if (!allowBuildOnAnyTileForTesting && !HasConnectedNeighbor(hoveredCoordinate))
        {
            ClearHoveredTile();
            return;
        }

        GameObject tileObject = gridMap.GetTileInstanceAt(hoveredCoordinate.x, hoveredCoordinate.y);
        if (tileObject == null)
        {
            ClearHoveredTile();
            return;
        }

        if (selectTile != null && (selectTile.IsSelectedCoordinate(hoveredCoordinate) || selectTile.SelectedTile == tileObject))
        {
            ClearHoveredTile();
            return;
        }

        SetHoveredTile(tileObject, hoveredCoordinate);
    }

    private bool TryBuildRoadInternal(Vector2Int tileCoordinate)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"Build input pressed on ({tileCoordinate.x}, {tileCoordinate.y}).", this);
        }

        if (!CanBuildRoadAt(tileCoordinate))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"Build blocked: tile ({tileCoordinate.x}, {tileCoordinate.y}) is not buildable.", this);
            }
            return false;
        }

        if (!CanBuildOnTile(tileCoordinate, out int[] cost))
        {
            return false;
        }

        if (!bypassTurnAndResourceChecksForTesting)
        {
            if (!turns.CanTakeAction)
            {
                if (enableDebugLogs)
                {
                    Debug.Log("Build blocked: no action available this turn.", this);
                }
                return false;
            }

            if (!turns.CanAffordResources(cost))
            {
                Debug.Log($"Not enough resources.");
                return false;
            }

            int roadActionCost = 1;
            if (tileBuildContextPanel != null)
            {
                tileBuildContextPanel.GetBuildingActionAndTurnCost(Building.Road, out roadActionCost, out _);
            }

            if (roadActionCost > 0 && (!turns.CanTakeAction || turns.ActionsRemaining < roadActionCost))
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"Build blocked: not enough actions ({roadActionCost} required).", this);
                }
                return false;
            }

            if (roadActionCost > 0 && !turns.TrySpendAction(roadActionCost))
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"Build blocked: TrySpendAction({roadActionCost}) failed.", this);
                }
                return false;
            }

            if (!turns.TrySpendResources(cost))
            {
                if (enableDebugLogs)
                {
                    Debug.Log("Build blocked: TrySpendResources failed.", this);
                }
                return false;
            }
        }

        if (!builtRoads.Add(tileCoordinate))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"Build blocked: road already exists at ({tileCoordinate.x}, {tileCoordinate.y}).", this);
            }
            return false;
        }

        connectedNodesDirty = true;

        bool mainTileBuilt = RebuildRoadVisualAt(tileCoordinate);
        if (!mainTileBuilt)
        {
            if (enableDebugLogs)
            {
                Debug.LogError($"Build failed: could not place road visual at ({tileCoordinate.x}, {tileCoordinate.y}).", this);
            }

            builtRoads.Remove(tileCoordinate);
            connectedNodesDirty = true;
            return false;
        }

        RebuildRoadVisualAt(tileCoordinate + Vector2Int.up);
        RebuildRoadVisualAt(tileCoordinate + Vector2Int.right);
        RebuildRoadVisualAt(tileCoordinate + Vector2Int.down);
        RebuildRoadVisualAt(tileCoordinate + Vector2Int.left);

        UpdateConnectedVillageCounter();
        RoadPlaced?.Invoke();
        RoadBuiltAt?.Invoke(tileCoordinate);
        onRoadPlaced?.Invoke();

        SpawnBuildEffectAt(tileCoordinate);
        //Debug.Log($"Built road at ({tileCoordinate.x}, {tileCoordinate.y}) | Cost: W{woodCost} S{stoneCost}");
        ClearHoveredTile();
        return true;
    }

    private bool TryGetMouseGridCoordinate(out Vector2Int coordinate)
    {
        coordinate = new Vector2Int(-1, -1);
        if (mainCamera == null || Mouse.current == null)
        {
            return false;
        }

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, gridMap.transform.position.y, 0f));

        if (!groundPlane.Raycast(ray, out float enterDistance))
        {
            return false;
        }

        Vector3 worldPoint = ray.GetPoint(enterDistance);
        return gridMap.TryWorldToGridCoordinate(worldPoint, out coordinate);
    }

    private bool CanBuildOnTile(Vector2Int coordinate, out int[] cost)
    {
        cost = new int[turns.resourceTypesCount];

        if (builtRoads.Contains(coordinate))
        {
            return false;
        }

        GridTile tile = gridMap.GetTileAt(coordinate.x, coordinate.y);
        if (tile == null)
        {
            return false;
        }

        return TryGetRoadCostByTile(tile, out cost);
    }

    private bool RebuildRoadVisualAt(Vector2Int coordinate)
    {
        if (!builtRoads.Contains(coordinate))
        {
            return false;
        }

        if (!gridMap.IsInsideGrid(coordinate))
        {
            return false;
        }

        GameObject tileInstance = gridMap.GetTileInstanceAt(coordinate.x, coordinate.y);
        if (tileInstance == null)
        {
            return false;
        }

        bool north = IsRoadConnectionNode(coordinate + Vector2Int.up);
        bool east = IsRoadConnectionNode(coordinate + Vector2Int.right);
        bool south = IsRoadConnectionNode(coordinate + Vector2Int.down);
        bool west = IsRoadConnectionNode(coordinate + Vector2Int.left);

        int connections = (north ? 1 : 0) + (east ? 1 : 0) + (south ? 1 : 0) + (west ? 1 : 0);

        float yRotation;
        GameObject prefabToUse = ResolveRoadPrefab(north, east, south, west, connections, out yRotation);

        if (prefabToUse == null)
        {
            Debug.LogWarning($"TileBuilding: Missing road prefab assignment for pattern at ({coordinate.x}, {coordinate.y}) [N:{north} E:{east} S:{south} W:{west}]", this);
            return false;
        }

        if (!gridMap.TryReplaceTileVisualAt(coordinate.x, coordinate.y, prefabToUse, Quaternion.Euler(0f, yRotation, 0f)))
        {
            Debug.LogWarning($"TileBuilding: Failed to replace tile visual at ({coordinate.x}, {coordinate.y}).", this);
            return false;
        }

        if (hoveredCoordinate == coordinate)
        {
            hoveredTile = gridMap.GetTileInstanceAt(coordinate.x, coordinate.y);
            if (hoveredTile != null)
            {
                hoveredBasePosition = GetFlatBasePosition(hoveredTile.transform.localPosition);
            }
        }

        return true;
    }

    private GameObject ResolveRoadPrefab(bool north, bool east, bool south, bool west, int connections, out float yRotation)
    {
        yRotation = 0f;

        if (connections == 4)
        {
            return roadCrossPrefab != null ? roadCrossPrefab : roadVerticalPrefab;
        }

        if (connections == 3)
        {
            if (north && west && east)
            {
                return roadTcrossNWEPrefab != null ? roadTcrossNWEPrefab : roadCrossPrefab;
            }

            if (north && east && south)
            {
                return roadTcrossNESPrefab != null ? roadTcrossNESPrefab : roadCrossPrefab;
            }

            if (north && west && south)
            {
                return roadTcrossNWSPrefab != null ? roadTcrossNWSPrefab : roadCrossPrefab;
            }

            return roadTcrossSEWPrefab != null ? roadTcrossSEWPrefab : roadCrossPrefab;
        }

        if (connections == 2)
        {
            if (north && south)
            {
                yRotation = 0f;
                return roadVerticalPrefab;
            }

            if (east && west)
            {
                yRotation = 0f;
                return roadHorizontalPrefab;
            }

            if (north && east)
            {
                yRotation = 0f;
                return roadNorthEastPrefab;
            }

            if (north && west)
            {
                yRotation = 0f;
                return roadNorthWestPrefab;
            }

            if (south && east)
            {
                yRotation = 0f;
                return roadSouthEastPrefab;
            }

            yRotation = 0f;
            return roadSouthWestPrefab;
        }

        if (connections == 1)
        {
            yRotation = 0f;
            return (east || west) ? roadHorizontalPrefab : roadVerticalPrefab;
        }

        yRotation = 0f;
        return roadVerticalPrefab;
    }

    private bool IsRoadConnectionNode(Vector2Int coordinate)
    {
        if (allowBuildOnAnyTileForTesting)
        {
            if (builtRoads.Contains(coordinate))
            {
                return true;
            }

            GridTile tile = gridMap.GetTileAt(coordinate.x, coordinate.y);
            return IsSettlementTile(tile);
        }

        HashSet<Vector2Int> connectedNodes = GetConnectedRoadNetworkNodes();
        return connectedNodes.Contains(coordinate);
    }

    public bool HasConnectedNeighbor(Vector2Int coordinate)
    {
        HashSet<Vector2Int> connectedNodes = GetConnectedRoadNetworkNodes();
        for (int i = 0; i < CardinalDirections.Length; i++)
        {
            if (connectedNodes.Contains(coordinate + CardinalDirections[i]))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateConnectedVillageCounter()
    {
        HashSet<Vector2Int> connectedNodes = GetConnectedRoadNetworkNodes();

        foreach (Vector2Int coordinate in connectedNodes)
        {
            GridTile tile = gridMap.GetTileAt(coordinate.x, coordinate.y);
            if (HasExactTileName(tile, "Village") && countedConnectedVillages.Add(coordinate))
            {
                connectedVillageCounter++;
            }
        }
    }

    private HashSet<Vector2Int> GetConnectedRoadNetworkNodes()
    {
        if (!connectedNodesDirty)
        {
            return cachedConnectedNodes;
        }

        if (gridMap == null)
        {
            return cachedConnectedNodes;
        }

        cachedConnectedNodes.Clear();
        var queue = new Queue<Vector2Int>();
        bool foundCity = false;

        for (int x = 0; x < gridMap.Width; x++)
        {
            for (int y = 0; y < gridMap.Height; y++)
            {
                GridTile tile = gridMap.GetTileAt(x, y);
                if (HasExactTileName(tile, "City"))
                {
                    Vector2Int cityCoordinate = new Vector2Int(x, y);
                    cachedConnectedNodes.Add(cityCoordinate);
                    queue.Enqueue(cityCoordinate);
                    foundCity = true;
                }
            }
        }

        // Grid may not be generated yet during early lifecycle; keep dirty so we refresh again later.
        if (!foundCity)
        {
            connectedNodesDirty = true;
            return cachedConnectedNodes;
        }

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            for (int i = 0; i < CardinalDirections.Length; i++)
            {
                Vector2Int next = current + CardinalDirections[i];
                if (cachedConnectedNodes.Contains(next) || !gridMap.IsInsideGrid(next))
                {
                    continue;
                }

                if (IsRoadNetworkTraversable(next))
                {
                    cachedConnectedNodes.Add(next);
                    queue.Enqueue(next);
                }
            }
        }

        connectedNodesDirty = false;
        return cachedConnectedNodes;
    }

    private bool IsRoadNetworkTraversable(Vector2Int coordinate)
    {
        if (builtRoads.Contains(coordinate))
        {
            return true;
        }

        GridTile tile = gridMap.GetTileAt(coordinate.x, coordinate.y);
        return IsSettlementTile(tile);
    }

    private static bool IsSettlementTile(GridTile tile)
    {
        if (tile == null)
        {
            return false;
        }

        return tile.tileType == TileType.City
            || tile.tileType == TileType.Village
            || HasExactTileName(tile, "City")
            || HasExactTileName(tile, "Village");
    }

    private static bool HasExactTileName(GridTile tile, string expectedName)
    {
        return tile != null
            && !string.IsNullOrWhiteSpace(expectedName)
            && string.Equals((tile.tileName ?? string.Empty).Trim(), expectedName.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetRoadCostByTileName(string tileName, out int woodCost, out int stoneCost)
    {
        woodCost = 0;
        stoneCost = 0;

        if (string.IsNullOrWhiteSpace(tileName))
        {
            return false;
        }

        string lower = tileName.Trim().ToLowerInvariant();

        if (lower.Contains("grass") || lower == "land" || lower.Contains("plain"))
        {
            woodCost = 0;
            stoneCost = 1;
            return true;
        }

        if (lower == "forest")
        {
            woodCost = 1;
            stoneCost = 1;
            return true;
        }

        if (lower == "valley")
        {
            woodCost = 1;
            stoneCost = 1;
            return true;
        }

        return false;
    }
    private bool TryGetRoadCostByTile(GridTile tile, out int[] cost)
    {
        int expectedLength = turns != null ? Mathf.Max(0, turns.resourceTypesCount) : 0;

        if (tileBuildContextPanel != null)
        {
            int[][] allCosts = tileBuildContextPanel.GetBuildingResourceCost();
            if (allCosts != null && allCosts.Length > 0 && allCosts[0] != null)
            {
                cost = NormalizeCostLength(allCosts[0], expectedLength);
            }
            else
            {
                cost = NormalizeCostLength(new[] { 0, 1, 1 }, expectedLength);
            }
        }
        else
        {
            cost = NormalizeCostLength(new[] { 0, 1, 1 }, expectedLength);
            return false;
        }

        if (tile == null)
        {
            return false;
        }

        string lower = (tile.tileName ?? string.Empty).Trim().ToLowerInvariant();
        if (lower == "obstacle1" || lower == "obstacle2")
        {
            return false;
        }

        ApplyDynamicRoadCostScaling(cost);

        if (villageCrisisSystem != null)
        {
            cost = villageCrisisSystem.GetAdjustedBuildCost(cost, Building.Road);
        }

        return true;
    }

    private void ApplyDynamicRoadCostScaling(int[] cost)
    {
        if (!useDynamicRoadCostScaling || cost == null || roadsPerCostStep <= 0)
        {
            return;
        }

        int effectiveFreeRoads = freeRoadsBeforeScaling;
        int effectiveRoadsPerStep = roadsPerCostStep;

        if (scaleRoadCostByMapSize && gridMap != null)
        {
            int mapArea = Mathf.Max(1, gridMap.Width * gridMap.Height);
            int mapFreeRoads = Mathf.Max(0, Mathf.RoundToInt(mapArea * Mathf.Max(0f, freeRoadsMapAreaRatio)));
            int mapRoadsPerStep = Mathf.Max(1, Mathf.RoundToInt(mapArea * Mathf.Max(0.001f, roadsPerStepMapAreaRatio)));

            // Strength=0 keeps manual values; Strength=1 fully derives from map size.
            effectiveFreeRoads = Mathf.Max(0, Mathf.RoundToInt(Mathf.Lerp(freeRoadsBeforeScaling, mapFreeRoads, mapSizeScalingStrength)));
            effectiveRoadsPerStep = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(roadsPerCostStep, mapRoadsPerStep, mapSizeScalingStrength)));
        }

        int roadsBuilt = builtRoads.Count;
        int scaledRoadCount = Mathf.Max(0, roadsBuilt - effectiveFreeRoads);
        int stepCount = scaledRoadCount / effectiveRoadsPerStep;
        if (stepCount <= 0)
        {
            return;
        }

        int woodIndex = (int)ResourceType.Wood;
        int stoneIndex = (int)ResourceType.Stone;

        if (woodIndex >= 0 && woodIndex < cost.Length)
        {
            cost[woodIndex] += stepCount * Mathf.Max(0, woodPerStep);
        }

        if (stoneIndex >= 0 && stoneIndex < cost.Length)
        {
            cost[stoneIndex] += stepCount * Mathf.Max(0, stonePerStep);
        }
    }

    private static int[] NormalizeCostLength(int[] source, int expectedLength)
    {
        if (expectedLength <= 0)
        {
            return source != null ? (int[])source.Clone() : Array.Empty<int>();
        }

        int[] normalized = new int[expectedLength];
        if (source != null && source.Length > 0)
        {
            Array.Copy(source, normalized, Mathf.Min(source.Length, expectedLength));
        }

        return normalized;
    }
    private void SetHoveredTile(GameObject tileObject, Vector2Int coordinate)
    {
        if (hoveredTile == tileObject)
        {
            return;
        }

        if (hoveredTile != null)
        {
            hoveredTile.transform.localPosition = hoveredBasePosition;
        }

        hoveredTile = tileObject;
        hoveredBasePosition = GetFlatBasePosition(hoveredTile.transform.localPosition);
        hoveredCoordinate = coordinate;

        if (CanBuildOnTile(coordinate, out int[] cost) && enableDebugLogs)
        {
            //Debug.Log($"Hover tile ({coordinate.x}, {coordinate.y}) | Build cost: W{woodCost} S{stoneCost}");
        }
    }

    private void ClearHoveredTile()
    {
        if (hoveredTile != null)
        {
            ResetTileLift(hoveredTile, hoveredBasePosition);
            hoveredTile = null;
        }

        hoveredCoordinate = new Vector2Int(-1, -1);
    }

    private void AnimateTiles()
    {
        if (hoveredTile != null)
        {
            AnimateTileLift(hoveredTile, hoveredBasePosition, hoverLiftHeight, hoverAnimationSpeed);
        }
    }

    public static void ResetTileLift(GameObject tileObject, Vector3 basePosition)
    {
        if (tileObject == null)
        {
            return;
        }

        tileObject.transform.localPosition = basePosition;
    }

    public static void AnimateTileLift(GameObject tileObject, Vector3 basePosition, float liftHeight, float animationSpeed)
    {
        if (tileObject == null)
        {
            return;
        }

        float t = Mathf.Clamp01(Time.deltaTime * animationSpeed);
        Vector3 upTarget = basePosition + Vector3.up * liftHeight;
        tileObject.transform.localPosition = Vector3.Lerp(tileObject.transform.localPosition, upTarget, t);
    }

    private static Vector3 GetFlatBasePosition(Vector3 localPosition)
    {
        return new Vector3(localPosition.x, 0f, localPosition.z);
    }

    private void SpawnBuildEffectAt(Vector2Int coordinate)
    {
        if (buildEffectPrefab == null)
        {
            return;
        }

        GameObject tileInstance = gridMap.GetTileInstanceAt(coordinate.x, coordinate.y);
        if (tileInstance == null)
        {
            return;
        }

        Vector3 spawnPosition = tileInstance.transform.position + Vector3.up * yEffectOffset;
        Instantiate(buildEffectPrefab, spawnPosition, Quaternion.identity);
    }
}
