using GridGeneration;
using UnityEngine;
using UnityEngine.InputSystem;

public class TileBuilding : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridMap gridMap;
    [SerializeField] private Turns turns;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private UnityEngine.Camera mainCamera;

    [Header("Hover Animation")]
    [SerializeField] private float hoverLiftHeight = 0.2f;
    [SerializeField] private float hoverAnimationSpeed = 8f;

    private GameObject hoveredTile;
    private Vector3 hoveredBasePosition;

    private GameObject returningTile;
    private Vector3 returningBasePosition;

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = UnityEngine.Camera.main;
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

        if (!IsAdjacentToPlayer(hoveredCoordinate))
        {
            ClearHoveredTile();
            return;
        }

        if (!CanBuildOnTile(hoveredCoordinate, out _, out _))
        {
            ClearHoveredTile();
            return;
        }

        SetHoveredTile(hit.collider.gameObject);
    }

    private void TryBuildOnClick()
    {
        if (hoveredTile == null || Mouse.current == null)
        {
            return;
        }

        if (!Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (!TryParseTileCoordinate(hoveredTile.name, out Vector2Int tileCoordinate))
        {
            return;
        }

        if (gridMap.FindFirstTileByType(TileType.Road) == null)
        {
            Debug.LogWarning("TileBuilding: Missing a tile of type Road in GridMap tiles.", this);
            return;
        }

        if (!CanBuildOnTile(tileCoordinate, out int woodCost, out int stoneCost))
        {
            return;
        }

        if (!turns.CanTakeAction)
        {
            return;
        }

        if (!turns.CanAffordResources(woodCost, stoneCost))
        {
            Debug.Log($"Not enough resources. Need Wood {woodCost}, Stone {stoneCost}.");
            return;
        }

        if (!turns.TrySpendAction(1))
        {
            return;
        }

        if (!turns.TrySpendResources(woodCost, stoneCost))
        {
            return;
        }

        if (!gridMap.TryBuildRoadAt(tileCoordinate.x, tileCoordinate.y))
        {
            return;
        }

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

    private void SetHoveredTile(GameObject tileObject)
    {
        if (hoveredTile == tileObject)
        {
            return;
        }

        if (hoveredTile != null)
        {
            returningTile = hoveredTile;
            returningBasePosition = hoveredBasePosition;
        }

        hoveredTile = tileObject;
        hoveredBasePosition = hoveredTile.transform.localPosition;

        if (TryParseTileCoordinate(hoveredTile.name, out Vector2Int coordinate)
            && CanBuildOnTile(coordinate, out int woodCost, out int stoneCost))
        {
            Debug.Log($"Hover tile ({coordinate.x}, {coordinate.y}) | Build cost: W{woodCost} S{stoneCost}");
        }
    }

    private void ClearHoveredTile()
    {
        if (hoveredTile != null)
        {
            returningTile = hoveredTile;
            returningBasePosition = hoveredBasePosition;
            hoveredTile = null;
        }
    }

    private void AnimateTiles()
    {
        float t = Mathf.Clamp01(Time.deltaTime * hoverAnimationSpeed);

        if (hoveredTile != null)
        {
            Vector3 upTarget = hoveredBasePosition + Vector3.up * hoverLiftHeight;
            hoveredTile.transform.localPosition = Vector3.Lerp(hoveredTile.transform.localPosition, upTarget, t);
        }

        if (returningTile != null)
        {
            returningTile.transform.localPosition = Vector3.Lerp(returningTile.transform.localPosition, returningBasePosition, t);
            if ((returningTile.transform.localPosition - returningBasePosition).sqrMagnitude < 0.0001f)
            {
                returningTile.transform.localPosition = returningBasePosition;
                returningTile = null;
            }
        }
    }
}
