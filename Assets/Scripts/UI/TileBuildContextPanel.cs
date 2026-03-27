using GridGeneration;
using UnityEngine;
using UnityEngine.UI;

public class TileBuildContextPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TileBuilding tileBuilding;
    [SerializeField] private GridMap gridMap;
    [SerializeField] private Canvas canvas;
    [SerializeField] private UnityEngine.Camera worldCamera;

    [Header("UI")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private Button buildRoadButton;
    [SerializeField] private Button buildSawmillButton;
    [SerializeField] private Button buildStoneMineButton;

    [Header("Panel Position")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.15f, 0f);
    [SerializeField] private Vector2 canvasOffset = new Vector2(0f, 36f);
    [SerializeField] private float extraVerticalPixels = 120f;
    [SerializeField] private float screenEdgePadding = 8f;

    [Header("Tile Name Rules")]
    [SerializeField] private string[] woodValleyKeywords = { "wood valley", "wood" };
    [SerializeField] private string[] stoneValleyKeywords = { "stone valley", "stone" };

    private Vector2Int currentCoordinate = new Vector2Int(-1, -1);
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
            panelRoot.pivot = new Vector2(0.5f, 0f);
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

        if (!tileBuilding.TryGetHoveredCoordinate(out Vector2Int coordinate))
        {
            SetPanelVisible(false);
            return;
        }

        GridTile tile = gridMap.GetTileAt(coordinate.x, coordinate.y);
        GameObject tileInstance = gridMap.GetTileInstanceAt(coordinate.x, coordinate.y);
        if (tile == null || tileInstance == null)
        {
            SetPanelVisible(false);
            return;
        }

        currentCoordinate = coordinate;
        UpdatePanelButtons(tile, coordinate);
        UpdatePanelPosition(tileInstance.transform.position);
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

    private void UpdatePanelPosition(Vector3 worldPosition)
    {
        if (canvas == null || worldCamera == null)
        {
            return;
        }

        UnityEngine.Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : worldCamera;
        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        Vector3 cameraRelativeOffset =
            worldCamera.transform.right * worldOffset.x +
            worldCamera.transform.up * worldOffset.y +
            worldCamera.transform.forward * worldOffset.z;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(worldCamera, worldPosition + cameraRelativeOffset);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out Vector2 localPoint))
        {
            float verticalOffset = Mathf.Abs(canvasOffset.y) + Mathf.Max(0f, extraVerticalPixels);
            Vector2 target = localPoint + new Vector2(canvasOffset.x, verticalOffset);
            panelRoot.anchoredPosition = ClampPanelToCanvas(target, canvasRect);
        }
    }

    private Vector2 ClampPanelToCanvas(Vector2 targetPosition, RectTransform canvasRect)
    {
        Vector2 panelSize = new Vector2(
            panelRoot.rect.width * Mathf.Abs(panelRoot.localScale.x),
            panelRoot.rect.height * Mathf.Abs(panelRoot.localScale.y)
        );
        Vector2 pivot = panelRoot.pivot;
        Rect rect = canvasRect.rect;

        float minX = rect.xMin + panelSize.x * pivot.x + screenEdgePadding;
        float maxX = rect.xMax - panelSize.x * (1f - pivot.x) - screenEdgePadding;
        float minY = rect.yMin + panelSize.y * pivot.y + screenEdgePadding;
        float maxY = rect.yMax - panelSize.y * (1f - pivot.y) - screenEdgePadding;

        return new Vector2(
            Mathf.Clamp(targetPosition.x, minX, maxX),
            Mathf.Clamp(targetPosition.y, minY, maxY)
        );
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

        tileBuilding.TryBuildRoadAt(currentCoordinate);
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
