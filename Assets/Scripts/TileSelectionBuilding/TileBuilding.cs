using GridGeneration;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TileBuilding : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridMap gridMap;
    [SerializeField] private Turns turns;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private UnityEngine.Camera mainCamera;
    [SerializeField] private InputActionReference buildAction;

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

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Testing")]
    [SerializeField] private bool allowBuildOnAnyTileForTesting = true;
    [SerializeField] private bool bypassTurnAndResourceChecksForTesting = true;

    private GameObject hoveredTile;
    private Vector3 hoveredBasePosition;
    private Vector2Int hoveredCoordinate = new Vector2Int(-1, -1);
    private readonly HashSet<Vector2Int> builtRoads = new HashSet<Vector2Int>();

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = UnityEngine.Camera.main;
        }
    }

    private void OnEnable()
    {
        if (buildAction != null && buildAction.action != null)
        {
            buildAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (buildAction != null && buildAction.action != null)
        {
            buildAction.action.Disable();
        }
    }

    private void Update()
    {
        if (gridMap == null || turns == null || playerTransform == null || mainCamera == null)
        {
            return;
        }

        UpdateHoveredTile();
        AnimateTiles();
        TryBuildOnClick();
    }

    private void UpdateHoveredTile()
    {
        if (Mouse.current == null)
        {
            ClearHoveredTile();
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            ClearHoveredTile();
            return;
        }

        if (!TryGetTileCoordinateFromHit(hit.collider.transform, out Vector2Int hoveredCoordinate))
        {
            ClearHoveredTile();
            return;
        }

        if (!allowBuildOnAnyTileForTesting && !IsAdjacentToPlayer(hoveredCoordinate))
        {
            ClearHoveredTile();
            return;
        }

        if (!CanBuildOnTile(hoveredCoordinate, out _, out _))
        {
            ClearHoveredTile();
            return;
        }

        SetHoveredTile(hit.collider.gameObject, hoveredCoordinate);
    }

    private void TryBuildOnClick()
    {
        if (hoveredTile == null)
        {
            return;
        }

        bool buildPressed = false;
        if (buildAction != null && buildAction.action != null)
        {
            buildPressed = buildAction.action.WasPressedThisFrame();
        }
        else if (Mouse.current != null)
        {
            buildPressed = Mouse.current.leftButton.wasPressedThisFrame;
        }

        if (!buildPressed)
        {
            return;
        }

        if (hoveredCoordinate.x < 0 || hoveredCoordinate.y < 0)
        {
            if (enableDebugLogs)
            {
                Debug.Log("Build blocked: no valid hovered coordinate resolved.", this);
            }
            return;
        }

        Vector2Int tileCoordinate = hoveredCoordinate;

        if (enableDebugLogs)
        {
            Debug.Log($"Build input pressed on ({tileCoordinate.x}, {tileCoordinate.y}).", this);
        }

        if (!CanBuildOnTile(tileCoordinate, out int woodCost, out int stoneCost))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"Build blocked: tile ({tileCoordinate.x}, {tileCoordinate.y}) is not buildable.", this);
            }
            return;
        }

        if (!bypassTurnAndResourceChecksForTesting)
        {
            if (!turns.CanTakeAction)
            {
                if (enableDebugLogs)
                {
                    Debug.Log("Build blocked: no action available this turn.", this);
                }
                return;
            }

            if (!turns.CanAffordResources(woodCost, stoneCost))
            {
                Debug.Log($"Not enough resources. Need Wood {woodCost}, Stone {stoneCost}.");
                return;
            }

            if (!turns.TrySpendAction(1))
            {
                if (enableDebugLogs)
                {
                    Debug.Log("Build blocked: TrySpendAction failed.", this);
                }
                return;
            }

            if (!turns.TrySpendResources(woodCost, stoneCost))
            {
                if (enableDebugLogs)
                {
                    Debug.Log("Build blocked: TrySpendResources failed.", this);
                }
                return;
            }
        }

        if (!builtRoads.Add(tileCoordinate))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"Build blocked: road already exists at ({tileCoordinate.x}, {tileCoordinate.y}).", this);
            }
            return;
        }

        bool mainTileBuilt = RebuildRoadVisualAt(tileCoordinate);
        if (!mainTileBuilt)
        {
            if (enableDebugLogs)
            {
                Debug.LogError($"Build failed: could not place road visual at ({tileCoordinate.x}, {tileCoordinate.y}).", this);
            }

            builtRoads.Remove(tileCoordinate);
            return;
        }

        RebuildRoadVisualAt(tileCoordinate + Vector2Int.up);
        RebuildRoadVisualAt(tileCoordinate + Vector2Int.right);
        RebuildRoadVisualAt(tileCoordinate + Vector2Int.down);
        RebuildRoadVisualAt(tileCoordinate + Vector2Int.left);

        Debug.Log($"Built road at ({tileCoordinate.x}, {tileCoordinate.y}) | Cost: W{woodCost} S{stoneCost}");

        ClearHoveredTile();
    }

    private bool IsAdjacentToPlayer(Vector2Int targetCoordinate)
    {
        if (!TryGetPlayerCoordinate(out Vector2Int playerCoordinate))
        {
            return false;
        }

        int dx = Mathf.Abs(targetCoordinate.x - playerCoordinate.x);
        int dy = Mathf.Abs(targetCoordinate.y - playerCoordinate.y);

        return (dx <= 1 && dy <= 1) && (dx != 0 || dy != 0);
    }

    private bool TryGetPlayerCoordinate(out Vector2Int playerCoordinate)
    {
        playerCoordinate = new Vector2Int(-1, -1);

        Vector3 rayStart = playerTransform.position + Vector3.up * 2f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 8f))
        {
            if (TryGetTileCoordinateFromHit(hit.collider.transform, out playerCoordinate))
            {
                return true;
            }
        }

        return gridMap.TryWorldToGridCoordinate(playerTransform.position, out playerCoordinate);
    }

    private bool TryGetTileCoordinateFromHit(Transform hitTransform, out Vector2Int coordinate)
    {
        coordinate = new Vector2Int(-1, -1);
        Transform current = hitTransform;

        while (current != null)
        {
            if (TryParseTileCoordinate(current.name, out coordinate))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private bool CanBuildOnTile(Vector2Int coordinate, out int woodCost, out int stoneCost)
    {
        woodCost = 0;
        stoneCost = 0;

        if (builtRoads.Contains(coordinate))
        {
            return false;
        }

        GridTile tile = gridMap.GetTileAt(coordinate.x, coordinate.y);
        if (tile == null)
        {
            return false;
        }

        switch (tile.tileType)
        {
            case TileType.Land:
                woodCost = 0;
                stoneCost = 1;
                return true;

            case TileType.River:
                woodCost = 1;
                stoneCost = 0;
                return true;

            case TileType.Mountain:
                woodCost = 2;
                stoneCost = 2;
                return true;

            case TileType.Obstacle:
                woodCost = 2;
                stoneCost = 2;
                return true;

            default:
                return false;
        }
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
        GameObject prefabToUse = ResolveRoadPrefab(north, east, south, west, connections, coordinate, out yRotation);

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
                hoveredBasePosition = hoveredTile.transform.localPosition;
            }
        }

        return true;
    }

    private GameObject ResolveRoadPrefab(bool north, bool east, bool south, bool west, int connections, Vector2Int coordinate, out float yRotation)
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

        if (!TryGetPlayerCoordinate(out Vector2Int playerCoordinate))
        {
            yRotation = 0f;
            return roadVerticalPrefab;
        }

        int dx = coordinate.x - playerCoordinate.x;
        int dy = coordinate.y - playerCoordinate.y;
        bool isLeftOrRight = Mathf.Abs(dx) > Mathf.Abs(dy);
        yRotation = 0f;
        return isLeftOrRight ? roadHorizontalPrefab : roadVerticalPrefab;
    }

    private bool IsRoadConnectionNode(Vector2Int coordinate)
    {
        if (builtRoads.Contains(coordinate))
        {
            return true;
        }

        if (IsPlayerTileCoordinate(coordinate))
        {
            return true;
        }

        if (!gridMap.IsInsideGrid(coordinate))
        {
            return false;
        }

        GridTile tile = gridMap.GetTileAt(coordinate.x, coordinate.y);
        return tile != null && tile.tileType == TileType.City;
    }

    private bool IsPlayerTileCoordinate(Vector2Int coordinate)
    {
        if (!TryGetPlayerCoordinate(out Vector2Int playerCoordinate))
        {
            return false;
        }

        return playerCoordinate == coordinate;
    }

    private static bool TryParseTileCoordinate(string tileName, out Vector2Int coordinate)
    {
        coordinate = new Vector2Int(-1, -1);
        if (string.IsNullOrEmpty(tileName))
        {
            return false;
        }

        string[] parts = tileName.Split('_');
        if (parts.Length != 3 || parts[0] != "Tile")
        {
            return false;
        }

        if (!int.TryParse(parts[1], out int x) || !int.TryParse(parts[2], out int y))
        {
            return false;
        }

        coordinate = new Vector2Int(x, y);
        return true;
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
        hoveredBasePosition = hoveredTile.transform.localPosition;
        hoveredCoordinate = coordinate;

        if (CanBuildOnTile(coordinate, out int woodCost, out int stoneCost) && enableDebugLogs)
        {
            Debug.Log($"Hover tile ({coordinate.x}, {coordinate.y}) | Build cost: W{woodCost} S{stoneCost}");
        }
    }

    private void ClearHoveredTile()
    {
        if (hoveredTile != null)
        {
            hoveredTile.transform.localPosition = hoveredBasePosition;
            hoveredTile = null;
        }

        hoveredCoordinate = new Vector2Int(-1, -1);
    }

    private void AnimateTiles()
    {
        float t = Mathf.Clamp01(Time.deltaTime * hoverAnimationSpeed);

        if (hoveredTile != null)
        {
            Vector3 upTarget = hoveredBasePosition + Vector3.up * hoverLiftHeight;
            hoveredTile.transform.localPosition = Vector3.Lerp(hoveredTile.transform.localPosition, upTarget, t);
        }
    }
}
