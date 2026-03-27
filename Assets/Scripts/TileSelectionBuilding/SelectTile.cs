using GridGeneration;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class SelectTile : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridMap gridMap;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private TileBuilding hoverAnimationSource;
    [SerializeField] private InputActionReference selectAction;
    [SerializeField] private InputActionReference deselectAction;

    [Header("Selection Animation")]
    [SerializeField] private float selectedLiftHeight = 0.2f;
    [SerializeField] private float selectedAnimationSpeed = 8f;
    [SerializeField] private float clickSelectionThresholdPixels = 6f;

    private GameObject selectedTile;
    private Vector3 selectedBasePosition;
    private Vector2Int selectedCoordinate = new Vector2Int(-1, -1);
    private Vector2Int lastDeselectedCoordinate = new Vector2Int(-1, -1);
    private int lastDeselectedFrame = -1000;
    private bool pendingMouseSelection;
    private Vector2 mousePressPosition;

    public GameObject SelectedTile => selectedTile;
    public Vector2Int SelectedCoordinate => selectedCoordinate;
    public bool HasSelection => selectedCoordinate.x >= 0 && selectedCoordinate.y >= 0;

    public bool IsSelectedCoordinate(Vector2Int coordinate)
    {
        return selectedCoordinate == coordinate;
    }

    public bool IsRecentlyDeselectedCoordinate(Vector2Int coordinate)
    {
        return coordinate == lastDeselectedCoordinate && Time.frameCount - lastDeselectedFrame <= 1;
    }

    public void ClearSelection()
    {
        DeselectTile();
    }

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (hoverAnimationSource == null)
        {
            hoverAnimationSource = FindAnyObjectByType<TileBuilding>();
        }
    }

    private void OnEnable()
    {
        if (selectAction != null && selectAction.action != null)
        {
            selectAction.action.Enable();
        }

        if (deselectAction != null && deselectAction.action != null)
        {
            deselectAction.action.Enable();
        }
    }

    private void Update()
    {
        if (gridMap == null || mainCamera == null)
        {
            return;
        }

        TryDeselectOnInput();
        TryToggleSelectionOnClick();
        AnimateSelectedTile();
    }

    private void OnDisable()
    {
        if (selectAction != null && selectAction.action != null)
        {
            selectAction.action.Disable();
        }

        if (deselectAction != null && deselectAction.action != null)
        {
            deselectAction.action.Disable();
        }

        pendingMouseSelection = false;

        DeselectTile();
    }

    private void TryDeselectOnInput()
    {
        if (!HasSelection)
        {
            return;
        }

        if (IsSelectInputPressedThisFrame())
        {
            return;
        }

        if (IsPointerOverUI())
        {
            if (!IsEscapePressedThisFrame())
            {
                return;
            }
        }

        if (deselectAction != null && deselectAction.action != null && deselectAction.action.WasPressedThisFrame())
        {
            DeselectTile();
        }
    }

    private bool IsSelectInputPressedThisFrame()
    {
        if (selectAction != null && selectAction.action != null)
        {
            return selectAction.action.WasPressedThisFrame();
        }

        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    private static bool IsEscapePressedThisFrame()
    {
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    private void TryToggleSelectionOnClick()
    {
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                pendingMouseSelection = true;
                mousePressPosition = Mouse.current.position.ReadValue();
                return;
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                if (!pendingMouseSelection)
                {
                    return;
                }

                pendingMouseSelection = false;

                float thresholdSq = clickSelectionThresholdPixels * clickSelectionThresholdPixels;
                Vector2 releasePosition = Mouse.current.position.ReadValue();
                if ((releasePosition - mousePressPosition).sqrMagnitude > thresholdSq)
                {
                    // Treat as drag gesture; do not select.
                    return;
                }

                if (IsPointerOverUI())
                {
                    return;
                }

                TryToggleSelectionAtPointer();
            }

            return;
        }

        if (selectAction != null && selectAction.action != null && selectAction.action.WasPressedThisFrame())
        {
            if (IsPointerOverUI())
            {
                return;
            }

            TryToggleSelectionAtPointer();
        }
    }

    private void TryToggleSelectionAtPointer()
    {
        if (!TryGetMouseGridCoordinate(out Vector2Int coordinate))
        {
            return;
        }

        GameObject clickedTile = gridMap.GetTileInstanceAt(coordinate.x, coordinate.y);
        if (clickedTile == null)
        {
            return;
        }

        GridTile tileData = gridMap.GetTileAt(coordinate.x, coordinate.y);
        if (tileData == null || !IsSelectableTileType(tileData.tileType))
        {
            return;
        }

        if (coordinate == selectedCoordinate)
        {
            DeselectTile();
            return;
        }

        SelectTileAt(clickedTile, coordinate);
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private static bool IsSelectableTileType(TileType tileType)
    {
        return tileType != TileType.Road
            && tileType != TileType.City
            && tileType != TileType.Village;
    }

    private bool TryGetMouseGridCoordinate(out Vector2Int coordinate)
    {
        coordinate = new Vector2Int(-1, -1);
        if (Mouse.current == null || mainCamera == null || gridMap == null)
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

    private void SelectTileAt(GameObject tileObject, Vector2Int coordinate)
    {
        RestoreSelectedTilePosition();

        selectedTile = tileObject;
        selectedCoordinate = coordinate;
        selectedBasePosition = GetFlatBasePosition(selectedTile.transform.localPosition);
    }

    private void DeselectTile()
    {
        lastDeselectedCoordinate = selectedCoordinate;
        lastDeselectedFrame = Time.frameCount;
        RestoreSelectedTilePosition();
        selectedTile = null;
        selectedCoordinate = new Vector2Int(-1, -1);
    }

    private void RestoreSelectedTilePosition()
    {
        if (selectedTile == null)
        {
            return;
        }

        TileBuilding.ResetTileLift(selectedTile, selectedBasePosition);
    }

    private void AnimateSelectedTile()
    {
        if (selectedCoordinate.x < 0 || selectedCoordinate.y < 0)
        {
            return;
        }

        GameObject currentTileAtCoordinate = gridMap.GetTileInstanceAt(selectedCoordinate.x, selectedCoordinate.y);
        if (currentTileAtCoordinate == null)
        {
            DeselectTile();
            return;
        }

        if (selectedTile != currentTileAtCoordinate)
        {
            selectedTile = currentTileAtCoordinate;
            selectedBasePosition = GetFlatBasePosition(selectedTile.transform.localPosition);
        }

        if (selectedTile == null)
        {
            return;
        }

        float liftHeight = hoverAnimationSource != null ? hoverAnimationSource.HoverLiftHeight : selectedLiftHeight;
        float animationSpeed = hoverAnimationSource != null ? hoverAnimationSource.HoverAnimationSpeed : selectedAnimationSpeed;
        TileBuilding.AnimateTileLift(selectedTile, selectedBasePosition, liftHeight, animationSpeed);
    }

    private static Vector3 GetFlatBasePosition(Vector3 localPosition)
    {
        return new Vector3(localPosition.x, 0f, localPosition.z);
    }
}