using GridGeneration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TileBuildContextPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TileBuilding tileBuilding;
    [SerializeField] private SelectTile selectTile;
    [SerializeField] private GridMap gridMap;
    [SerializeField] private Canvas canvas;
    [SerializeField] private UnityEngine.Camera worldCamera;

    [Header("UI")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private Button buildRoadButton;
    [SerializeField] private Button buildSawmillButton;
    [SerializeField] private Button buildStoneMineButton;

    [Header("Tile Name Rules")]
    [SerializeField] private string[] woodValleyKeywords = { "wood valley", "wood" };
    [SerializeField] private string[] stoneValleyKeywords = { "stone valley", "stone" };

    [Header("Transitions")]
    [SerializeField, Min(0f)] private float tileSwitchHideDuration = 0.06f;

    private Vector2Int currentCoordinate = new Vector2Int(-1, -1);
    private Vector2Int lastCoordinate = new Vector2Int(-1, -1);
    private float hideUntilTime;
    private bool useCanvasGroupVisibility;
    private CanvasGroup panelCanvasGroup;

    private void Awake()
    {
        if (tileBuilding == null)
        {
            tileBuilding = FindAnyObjectByType<TileBuilding>();
        }

        if (gridMap == null)
        {
            gridMap = FindAnyObjectByType<GridMap>();
        }

        if (selectTile == null)
        {
            selectTile = FindAnyObjectByType<SelectTile>();
        }

        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (worldCamera == null)
        {
            worldCamera = UnityEngine.Camera.main;
        }

        if (buildRoadButton != null)
        {
            buildRoadButton.onClick.AddListener(OnBuildRoadPressed);
        }

        if (buildSawmillButton != null)
        {
            buildSawmillButton.onClick.AddListener(OnBuildSawmillPressed);
        }

        if (buildStoneMineButton != null)
        {
            buildStoneMineButton.onClick.AddListener(OnBuildStoneMinePressed);
        }

        if (panelRoot != null)
        {
            panelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            panelRoot.pivot = new Vector2(0.5f, 0.5f);
        }

        if (panelRoot != null && panelRoot.gameObject == gameObject)
        {
            useCanvasGroupVisibility = true;
            panelCanvasGroup = GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        SetPanelVisible(false);
    }

    private void Update()
    {
        if (tileBuilding == null || gridMap == null || panelRoot == null)
        {
            SetPanelVisible(false);
            return;
        }

        if (selectTile == null || !selectTile.HasSelection)
        {
            lastCoordinate = new Vector2Int(-1, -1);
            SetPanelVisible(false);
            return;
        }

        Vector2Int coordinate = selectTile.SelectedCoordinate;

        GridTile tile = gridMap.GetTileAt(coordinate.x, coordinate.y);
        GameObject tileInstance = gridMap.GetTileInstanceAt(coordinate.x, coordinate.y);
        if (tile == null || tileInstance == null)
        {
            lastCoordinate = new Vector2Int(-1, -1);
            SetPanelVisible(false);
            return;
        }

        if (coordinate != lastCoordinate)
        {
            lastCoordinate = coordinate;
            if (tileSwitchHideDuration > 0f)
            {
                hideUntilTime = Time.unscaledTime + tileSwitchHideDuration;
                SetPanelVisible(false);
                return;
            }
        }

        if (Time.unscaledTime < hideUntilTime)
        {
            SetPanelVisible(false);
            return;
        }

        bool tileChanged = coordinate != currentCoordinate;
        currentCoordinate = coordinate;
        UpdatePanelButtons(tile, coordinate);
        if (tileChanged)
        {
            UpdatePanelPositionAtSelection(tileInstance.transform.position);
        }
        SetPanelVisible(true);
    }

    private void UpdatePanelButtons(GridTile tile, Vector2Int coordinate)
    {
        bool isWoodValley = IsTileMatchingAnyKeyword(tile, woodValleyKeywords);
        bool isStoneValley = IsTileMatchingAnyKeyword(tile, stoneValleyKeywords);
        bool isSettlement = tile.tileType == TileType.City || tile.tileType == TileType.Village;

        bool canBuildRoad = tileBuilding.CanBuildRoadAt(coordinate)
            && !isSettlement
            && !isWoodValley
            && !isStoneValley;

        if (buildRoadButton != null)
        {
            buildRoadButton.interactable = canBuildRoad;
        }

        if (buildSawmillButton != null)
        {
            buildSawmillButton.interactable = isWoodValley;
        }

        if (buildStoneMineButton != null)
        {
            buildStoneMineButton.interactable = isStoneValley;
        }
    }

    private void UpdatePanelPositionAtSelection(Vector3 worldPosition)
    {
        if (canvas == null || worldCamera == null || panelRoot == null)
        {
            return;
        }

        UnityEngine.Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : worldCamera;
        RectTransform parentRect = panelRoot.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        Vector2 screenPoint = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : RectTransformUtility.WorldToScreenPoint(worldCamera, worldPosition);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCamera, out Vector2 localPoint))
        {
            panelRoot.anchoredPosition = localPoint;
        }
    }

    private bool IsTileMatchingAnyKeyword(GridTile tile, string[] keywords)
    {
        if (tile == null || string.IsNullOrEmpty(tile.tileName) || keywords == null)
        {
            return false;
        }

        string tileName = tile.tileName.ToLowerInvariant();
        for (int i = 0; i < keywords.Length; i++)
        {
            string keyword = keywords[i];
            if (string.IsNullOrEmpty(keyword))
            {
                continue;
            }

            if (tileName.Contains(keyword.ToLowerInvariant()))
            {
                return true;
            }
        }

        return false;
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot == null)
        {
            return;
        }

        if (useCanvasGroupVisibility)
        {
            panelCanvasGroup.alpha = visible ? 1f : 0f;
            panelCanvasGroup.interactable = visible;
            panelCanvasGroup.blocksRaycasts = visible;
        }
        else if (panelRoot.gameObject.activeSelf != visible)
        {
            panelRoot.gameObject.SetActive(visible);
        }
    }

    private void OnBuildRoadPressed()
    {
        if (currentCoordinate.x < 0 || currentCoordinate.y < 0)
        {
            return;
        }

        if (tileBuilding.TryBuildRoadAt(currentCoordinate) && selectTile != null)
        {
            selectTile.ClearSelection();
        }
    }

    private void OnBuildSawmillPressed()
    {
        if (currentCoordinate.x < 0 || currentCoordinate.y < 0)
        {
            return;
        }
    }

    private void OnBuildStoneMinePressed()
    {
        if (currentCoordinate.x < 0 || currentCoordinate.y < 0)
        {
            return;
        }
    }
}
