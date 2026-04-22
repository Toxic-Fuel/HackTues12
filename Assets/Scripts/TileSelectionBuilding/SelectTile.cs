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
    [SerializeField] private InputActionReference selectPositionAction;

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
    private int defaultLayer = -1;
    private int outlineLayer = -1;

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
        defaultLayer = LayerMask.NameToLayer("Default");
        outlineLayer = LayerMask.NameToLayer("Outline");

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

        if (selectPositionAction != null && selectPositionAction.action != null)
        {
            selectPositionAction.action.Enable();
        }
    }

    private void Update()
    {
        if (InGameGenerationMenu.IsAnyMenuOpen)
        {
            return;
        }

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

        if (selectPositionAction != null && selectPositionAction.action != null)
        {
            selectPositionAction.action.Disable();
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

        bool deselectPressed = deselectAction != null
            && deselectAction.action != null
            && deselectAction.action.WasPressedThisFrame();

        if (deselectPressed)
        {
            DeselectTile();
            return;
        }

        if (IsEscapePressedThisFrame())
        {
            DeselectTile();
            return;
        }

        if (IsSelectInputPressedThisFrame())
        {
            return;
        }

        if (IsPointerOverUI())
        {
            return;
        }
    }

    private bool IsSelectInputPressedThisFrame()
    {
        return selectAction != null
            && selectAction.action != null
            && selectAction.action.WasPressedThisFrame();
    }

    private static bool IsEscapePressedThisFrame()
    {
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    private void TryToggleSelectionOnClick()
    {
        if (selectAction == null || selectAction.action == null)
        {
            return;
        }

        bool wasPressed = selectAction.action.WasPressedThisFrame();
        bool wasReleased = selectAction.action.WasReleasedThisFrame();

        if (wasPressed)
        {
            pendingMouseSelection = true;
            if (!TryGetPointerScreenPosition(out mousePressPosition))
            {
                pendingMouseSelection = false;
            }
        }

        if (!wasReleased)
        {
            return;
        }

        if (!pendingMouseSelection)
        {
            return;
        }

        pendingMouseSelection = false;

        if (!TryGetPointerScreenPosition(out Vector2 releasePosition))
        {
            return;
        }

        float thresholdSq = clickSelectionThresholdPixels * clickSelectionThresholdPixels;
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

    private void TryToggleSelectionAtPointer()
    {
        if (!TryGetPointerGridCoordinate(out Vector2Int coordinate))
        {
            return;
        }

        GridTile tileData = gridMap.GetTileAt(coordinate.x, coordinate.y);
        if (tileData == null || !IsSelectableTileType(tileData.tileType))
        {
            return;
        }

        bool isVillageTile = tileData.tileType == TileType.Village;

        if (!isVillageTile && hoverAnimationSource != null && hoverAnimationSource.IsBlockedForHoverOrSelection(coordinate))
        {
            return;
        }

        GameObject clickedTile = gridMap.GetTileInstanceAt(coordinate.x, coordinate.y);
        if (clickedTile == null)
        {
            return;
        }

        if (!isVillageTile && hoverAnimationSource != null && !hoverAnimationSource.HasConnectedNeighbor(coordinate))
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
            && tileType != TileType.City;
    }

    private bool TryGetPointerGridCoordinate(out Vector2Int coordinate)
    {
        coordinate = new Vector2Int(-1, -1);
        if (mainCamera == null || gridMap == null)
        {
            return false;
        }

        if (!TryGetPointerScreenPosition(out Vector2 pointerPosition))
        {
            return false;
        }

        Ray ray = mainCamera.ScreenPointToRay(pointerPosition);
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, gridMap.transform.position.y, 0f));

        if (!groundPlane.Raycast(ray, out float enterDistance))
        {
            return false;
        }

        Vector3 worldPoint = ray.GetPoint(enterDistance);
        return gridMap.TryWorldToGridCoordinate(worldPoint, out coordinate);
    }

    private bool TryGetPointerScreenPosition(out Vector2 pointerPosition)
    {
        pointerPosition = Vector2.zero;

        if (selectPositionAction == null || selectPositionAction.action == null)
        {
            return false;
        }

        pointerPosition = selectPositionAction.action.ReadValue<Vector2>();
        return true;
    }

    private void SelectTileAt(GameObject tileObject, Vector2Int coordinate)
    {
        RestoreSelectedTilePosition();
        SetTileAndChildrenLayer(selectedTile, defaultLayer);

        selectedTile = tileObject;
        selectedCoordinate = coordinate;
        selectedBasePosition = GetFlatBasePosition(selectedTile.transform.localPosition);
        SetTileAndChildrenLayer(selectedTile, outlineLayer);
    }

    private void DeselectTile()
    {
        lastDeselectedCoordinate = selectedCoordinate;
        lastDeselectedFrame = Time.frameCount;
        RestoreSelectedTilePosition();
        SetTileAndChildrenLayer(selectedTile, defaultLayer);
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
            SetTileAndChildrenLayer(selectedTile, defaultLayer);
            selectedTile = currentTileAtCoordinate;
            selectedBasePosition = GetFlatBasePosition(selectedTile.transform.localPosition);
            SetTileAndChildrenLayer(selectedTile, outlineLayer);
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

    private static void SetTileAndChildrenLayer(GameObject tileObject, int layer)
    {
        if (tileObject == null || layer < 0)
        {
            return;
        }

        Transform[] transforms = tileObject.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            transforms[i].gameObject.layer = layer;
        }
    }
}