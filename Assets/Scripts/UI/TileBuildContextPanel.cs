using GridGeneration;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class TileBuildContextPanel : MonoBehaviour
{
    private enum BuildOption
    {
        None,
        Road,
        Sawmill,
        StoneMine
    }

    [Header("References")]
    [SerializeField] private TileBuilding tileBuilding;
    [SerializeField] private SelectTile selectTile;
    [SerializeField] private GridMap gridMap;
    [SerializeField] private Canvas canvas;
    [SerializeField] private UnityEngine.Camera worldCamera;
    [SerializeField] private Turns turns;
    [SerializeField] private InputActionReference buildConfirmAction;

    [Header("UI")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private Button buildRoadButton;
    [SerializeField] private Button buildSawmillButton;
    [SerializeField] private Button buildStoneMineButton;
    [SerializeField] private Button confirmBuildButton;
    [SerializeField] private TMP_Text woodCostText;
    [SerializeField] private TMP_Text stoneCostText;

    [Header("Button Tint")]
    [SerializeField] private Color normalButtonColor = Color.white;
    [SerializeField] private Color selectedButtonColor = new Color(0.82f, 0.82f, 0.62f, 1f);

    [Header("Build Costs")]
    [SerializeField, Min(0)] private int sawmillWoodCost = 2;
    [SerializeField, Min(0)] private int sawmillStoneCost = 2;
    [SerializeField, Min(0)] private int stoneMineWoodCost = 5;
    [SerializeField, Min(0)] private int stoneMineStoneCost = 4;

    [Header("Building Prefabs")]
    [SerializeField] private GameObject sawmillForestPrefab;
    [SerializeField] private GameObject stoneMineMountainPrefab;
    [SerializeField] private GameObject stoneMineStoneValleyPrefab;

    [Header("Transitions")]
    [SerializeField, Min(0f)] private float tileSwitchHideDuration = 0.06f;

    [Header("Clamping")]
    [SerializeField, Min(0f)] private float screenEdgePadding = 10f;

    [Header("Spawn Offset")]
    [SerializeField] private float spawnOffsetY = 64f;
    [SerializeField, Min(0f)] private float selectedTileClearancePixels = 40f;

    private Vector2Int currentCoordinate = new Vector2Int(-1, -1);
    private Vector2Int lastCoordinate = new Vector2Int(-1, -1);
    private float hideUntilTime;
    private bool useCanvasGroupVisibility;
    private CanvasGroup panelCanvasGroup;
    private BuildOption selectedOption = BuildOption.None;
    private bool canBuildRoadOption;
    private bool canBuildSawmillOption;
    private bool canBuildStoneMineOption;
    private readonly HashSet<Vector2Int> placedCollectorBuildings = new HashSet<Vector2Int>();

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

        if (turns == null)
        {
            turns = FindAnyObjectByType<Turns>();
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
            buildRoadButton.onClick.AddListener(() => OnBuildOptionSelected(BuildOption.Road));
        }

        if (buildSawmillButton != null)
        {
            buildSawmillButton.onClick.AddListener(() => OnBuildOptionSelected(BuildOption.Sawmill));
        }

        if (buildStoneMineButton != null)
        {
            buildStoneMineButton.onClick.AddListener(() => OnBuildOptionSelected(BuildOption.StoneMine));
        }

        if (confirmBuildButton != null)
        {
            confirmBuildButton.onClick.AddListener(TryConfirmSelectedBuild);
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

    private void OnEnable()
    {
        if (buildConfirmAction != null && buildConfirmAction.action != null)
        {
            buildConfirmAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (buildConfirmAction != null && buildConfirmAction.action != null)
        {
            buildConfirmAction.action.Disable();
        }
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
            selectedOption = BuildOption.None;
            UpdateSelectionVisuals();
            UpdateCostTexts(0, 0);
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
        EnsureSelectedOptionIsValid(tile, coordinate, tileChanged);
        if (tileChanged)
        {
            UpdatePanelPositionAtSelection(tileInstance.transform.position);
        }

        bool confirmPressed = buildConfirmAction != null
            && buildConfirmAction.action != null
            && buildConfirmAction.action.WasPressedThisFrame();

        if (!confirmPressed && Keyboard.current != null)
        {
            confirmPressed = Keyboard.current.bKey.wasPressedThisFrame;
        }

        if (confirmPressed)
        {
            TryConfirmSelectedBuild();
        }

        SetPanelVisible(true);
    }

    private void UpdatePanelButtons(GridTile tile, Vector2Int coordinate)
    {
        bool isSettlement = IsSettlementTile(tile);
        bool isForestTile = IsForestTile(tile);
        bool isValleyTile = IsValleyTile(tile);
        bool isMountainMineTile = IsMountainMineTile(tile);

        bool hasCollectorBuilding = placedCollectorBuildings.Contains(coordinate);
        bool hasRoadBuilding = tileBuilding != null && tileBuilding.IsRoadAlreadyBuilt(coordinate);

        bool canBuildRoad = tileBuilding.CanBuildRoadAt(coordinate)
            && !isSettlement
            && !hasCollectorBuilding;
        bool canBuildSawmill = isForestTile && !hasCollectorBuilding && !hasRoadBuilding;
        bool canBuildStoneMine = (isValleyTile || isMountainMineTile)
            && !hasCollectorBuilding
            && !hasRoadBuilding;

        canBuildRoadOption = canBuildRoad;
        canBuildSawmillOption = canBuildSawmill;
        canBuildStoneMineOption = canBuildStoneMine;

        if (buildRoadButton != null)
        {
            buildRoadButton.interactable = canBuildRoad;
        }

        if (buildSawmillButton != null)
        {
            buildSawmillButton.interactable = canBuildSawmillOption;
        }

        if (buildStoneMineButton != null)
        {
            buildStoneMineButton.interactable = canBuildStoneMineOption;
        }

        if (confirmBuildButton != null)
        {
            confirmBuildButton.interactable = IsSelectedOptionCurrentlyBuildable();
        }
    }

    private void EnsureSelectedOptionIsValid(GridTile tile, Vector2Int coordinate, bool tileChanged)
    {
        if (tileChanged)
        {
            selectedOption = BuildOption.None;
        }

        bool selectedInvalid = selectedOption != BuildOption.None && !IsSelectedOptionCurrentlyBuildable();
        if (selectedInvalid)
        {
            selectedOption = BuildOption.None;
        }

        UpdateSelectionVisuals();
        UpdateCostPreviewForSelection(tile, coordinate);

        if (confirmBuildButton != null)
        {
            confirmBuildButton.interactable = IsSelectedOptionCurrentlyBuildable();
        }
    }

    private void OnBuildOptionSelected(BuildOption option)
    {
        if (!IsOptionBuildable(option))
        {
            return;
        }

        selectedOption = option;
        UpdateSelectionVisuals();

        if (gridMap == null || currentCoordinate.x < 0 || currentCoordinate.y < 0)
        {
            UpdateCostTexts(0, 0);
            return;
        }

        GridTile tile = gridMap.GetTileAt(currentCoordinate.x, currentCoordinate.y);
        UpdateCostPreviewForSelection(tile, currentCoordinate);

        if (confirmBuildButton != null)
        {
            confirmBuildButton.interactable = IsSelectedOptionCurrentlyBuildable();
        }
    }

    public void SelectBuildOptionByIndex(int optionIndex)
    {
        BuildOption option = optionIndex switch
        {
            0 => BuildOption.Road,
            1 => BuildOption.Sawmill,
            2 => BuildOption.StoneMine,
            _ => BuildOption.None
        };

        if (option == BuildOption.None)
        {
            return;
        }

        OnBuildOptionSelected(option);
    }

    public void ConfirmSelectedBuild()
    {
        TryConfirmSelectedBuild();
    }

    private void UpdateCostPreviewForSelection(GridTile tile, Vector2Int coordinate)
    {
        int woodCost = 0;
        int stoneCost = 0;

        switch (selectedOption)
        {
            case BuildOption.Road:
                TryGetRoadCost(tile, coordinate, out woodCost, out stoneCost);
                break;

            case BuildOption.Sawmill:
                woodCost = sawmillWoodCost;
                stoneCost = sawmillStoneCost;
                break;

            case BuildOption.StoneMine:
                woodCost = stoneMineWoodCost;
                stoneCost = stoneMineStoneCost;
                break;
        }

        UpdateCostTexts(woodCost, stoneCost);
    }

    private bool TryGetRoadCost(GridTile tile, Vector2Int coordinate, out int woodCost, out int stoneCost)
    {
        woodCost = 0;
        stoneCost = 0;

        if (tileBuilding == null || tile == null)
        {
            return false;
        }

        if (!tileBuilding.CanBuildRoadAt(coordinate))
        {
            return false;
        }

        return TryGetRoadCostByTileName(tile.tileName, out woodCost, out stoneCost);
    }

    private void UpdateCostTexts(int woodCost, int stoneCost)
    {
        if (woodCostText != null)
        {
            woodCostText.text = woodCost.ToString("00");
        }

        if (stoneCostText != null)
        {
            stoneCostText.text = stoneCost.ToString("00");
        }
    }

    private void UpdateSelectionVisuals()
    {
        ApplyButtonState(buildRoadButton, selectedOption == BuildOption.Road);
        ApplyButtonState(buildSawmillButton, selectedOption == BuildOption.Sawmill);
        ApplyButtonState(buildStoneMineButton, selectedOption == BuildOption.StoneMine);
    }

    private void ApplyButtonState(Button button, bool isSelected)
    {
        if (button == null)
        {
            return;
        }

        ColorBlock colors = button.colors;
        Color targetColor = isSelected ? selectedButtonColor : normalButtonColor;
        colors.normalColor = targetColor;
        colors.highlightedColor = targetColor;
        colors.selectedColor = targetColor;
        button.colors = colors;
    }

    private bool IsSelectedOptionCurrentlyBuildable()
    {
        return IsOptionBuildable(selectedOption);
    }

    private bool IsOptionBuildable(BuildOption option)
    {
        return option switch
        {
            BuildOption.Road => canBuildRoadOption,
            BuildOption.Sawmill => canBuildSawmillOption,
            BuildOption.StoneMine => canBuildStoneMineOption,
            _ => false
        };
    }

    private void TryConfirmSelectedBuild()
    {
        if (!selectTile.HasSelection)
        {
            return;
        }

        if (currentCoordinate.x < 0 || currentCoordinate.y < 0)
        {
            return;
        }

        if (!IsSelectedOptionCurrentlyBuildable())
        {
            return;
        }

        switch (selectedOption)
        {
            case BuildOption.Road:
                OnBuildRoadPressed();
                break;

            case BuildOption.Sawmill:
                OnBuildSawmillPressed();
                break;

            case BuildOption.StoneMine:
                OnBuildStoneMinePressed();
                break;
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

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(worldCamera, worldPosition);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCamera, out Vector2 localPoint))
        {
            Vector2 panelSize = GetPanelSizeInParentSpace();
            float offsetY = Mathf.Abs(spawnOffsetY);
            float topPart = panelSize.y * (1f - panelRoot.pivot.y);
            float bottomPart = panelSize.y * panelRoot.pivot.y;

            float aboveY = localPoint.y + selectedTileClearancePixels + bottomPart + offsetY;
            Vector2 spawnAbove = new Vector2(localPoint.x, aboveY);
            Vector2 clampedAbove = ClampToParent(spawnAbove, parentRect);
            bool aboveKeepsTileVisible = (clampedAbove.y - bottomPart) >= (localPoint.y + selectedTileClearancePixels);

            if (aboveKeepsTileVisible)
            {
                panelRoot.anchoredPosition = clampedAbove;
                return;
            }

            float belowY = localPoint.y - selectedTileClearancePixels - topPart - offsetY;
            Vector2 spawnBelow = new Vector2(localPoint.x, belowY);
            Vector2 clampedBelow = ClampToParent(spawnBelow, parentRect);
            bool belowKeepsTileVisible = (clampedBelow.y + topPart) <= (localPoint.y - selectedTileClearancePixels);

            if (belowKeepsTileVisible)
            {
                panelRoot.anchoredPosition = clampedBelow;
                return;
            }

            // If both sides are constrained by edges, keep the side with more visual clearance.
            float aboveGap = (clampedAbove.y - bottomPart) - localPoint.y;
            float belowGap = localPoint.y - (clampedBelow.y + topPart);
            panelRoot.anchoredPosition = aboveGap >= belowGap ? clampedAbove : clampedBelow;
        }
    }

    private Vector2 GetPanelSizeInParentSpace()
    {
        Vector2 panelSize = new Vector2(
            panelRoot.rect.width * Mathf.Abs(panelRoot.lossyScale.x),
            panelRoot.rect.height * Mathf.Abs(panelRoot.lossyScale.y));

        float canvasScale = canvas != null ? canvas.scaleFactor : 1f;
        if (canvasScale > 0.0001f)
        {
            panelSize /= canvasScale;
        }

        return panelSize;
    }

    private Vector2 ClampToParent(Vector2 targetPosition, RectTransform parentRect)
    {
        Vector2 panelSize = GetPanelSizeInParentSpace();

        Vector2 pivot = panelRoot.pivot;
        Rect parent = parentRect.rect;

        float minX = parent.xMin + panelSize.x * pivot.x + screenEdgePadding;
        float maxX = parent.xMax - panelSize.x * (1f - pivot.x) - screenEdgePadding;
        float minY = parent.yMin + panelSize.y * pivot.y + screenEdgePadding;
        float maxY = parent.yMax - panelSize.y * (1f - pivot.y) - screenEdgePadding;

        return new Vector2(
            Mathf.Clamp(targetPosition.x, minX, maxX),
            Mathf.Clamp(targetPosition.y, minY, maxY));
    }

    private static bool HasExactTileName(GridTile tile, string expectedName)
    {
        return tile != null
            && !string.IsNullOrWhiteSpace(expectedName)
            && string.Equals(tile.tileName, expectedName, System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSettlementTile(GridTile tile)
    {
        return HasExactTileName(tile, "City") || HasExactTileName(tile, "Village");
    }

    private static bool IsForestTile(GridTile tile)
    {
        return HasExactTileName(tile, "Forest");
    }

    private static bool IsValleyTile(GridTile tile)
    {
        return HasExactTileName(tile, "Valley");
    }

    private static bool IsMountainMineTile(GridTile tile)
    {
        return HasExactTileName(tile, "Obstacle1");
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

        GridTile tile = gridMap != null ? gridMap.GetTileAt(currentCoordinate.x, currentCoordinate.y) : null;
        if (tile == null || !IsForestTile(tile))
        {
            Debug.LogWarning($"Sawmill build blocked: tile must be 'Forest' but was '{tile?.tileName ?? "null"}'.", this);
            return;
        }

        if (placedCollectorBuildings.Contains(currentCoordinate)
            || (tileBuilding != null && tileBuilding.IsRoadAlreadyBuilt(currentCoordinate)))
        {
            Debug.LogWarning("Sawmill build blocked: tile already has a building.", this);
            return;
        }

        if (sawmillForestPrefab == null)
        {
            Debug.LogWarning("Sawmill prefab for Forest is not assigned.", this);
            return;
        }

        if (!TrySpendBuildActionAndResources(sawmillWoodCost, sawmillStoneCost, "sawmill"))
        {
            return;
        }

        if (!gridMap.TryReplaceTileVisualAt(currentCoordinate.x, currentCoordinate.y, sawmillForestPrefab, Quaternion.identity))
        {
            Debug.LogWarning("Sawmill build failed: could not replace tile visual.", this);
            return;
        }

        placedCollectorBuildings.Add(currentCoordinate);
        if (selectTile != null)
        {
            selectTile.ClearSelection();
        }
    }

    private void OnBuildStoneMinePressed()
    {
        if (currentCoordinate.x < 0 || currentCoordinate.y < 0)
        {
            return;
        }

        GridTile tile = gridMap != null ? gridMap.GetTileAt(currentCoordinate.x, currentCoordinate.y) : null;
        if (tile == null)
        {
            Debug.LogWarning("Stone mine build blocked: tile data is missing.", this);
            return;
        }

        if (placedCollectorBuildings.Contains(currentCoordinate)
            || (tileBuilding != null && tileBuilding.IsRoadAlreadyBuilt(currentCoordinate)))
        {
            Debug.LogWarning("Stone mine build blocked: tile already has a building.", this);
            return;
        }

        GameObject targetPrefab = null;
        if (IsValleyTile(tile))
        {
            targetPrefab = stoneMineStoneValleyPrefab;
        }
        else if (IsMountainMineTile(tile))
        {
            targetPrefab = stoneMineMountainPrefab;
        }
        else
        {
            Debug.LogWarning($"Stone mine build blocked: tile must be 'Valley' or 'Obstacle1' but was '{tile.tileName}'.", this);
            return;
        }

        if (targetPrefab == null)
        {
            Debug.LogWarning("Stone mine prefab is missing for this tile type.", this);
            return;
        }

        if (!TrySpendBuildActionAndResources(stoneMineWoodCost, stoneMineStoneCost, "stone mine"))
        {
            return;
        }

        if (!gridMap.TryReplaceTileVisualAt(currentCoordinate.x, currentCoordinate.y, targetPrefab, Quaternion.identity))
        {
            Debug.LogWarning("Stone mine build failed: could not replace tile visual.", this);
            return;
        }

        placedCollectorBuildings.Add(currentCoordinate);
        if (selectTile != null)
        {
            selectTile.ClearSelection();
        }
    }

    private bool TrySpendBuildActionAndResources(int woodCost, int stoneCost, string buildLabel)
    {
        if (turns == null)
        {
            return true;
        }

        if (!turns.CanTakeAction)
        {
            Debug.LogWarning($"Cannot build {buildLabel}: no actions left this turn.", this);
            return false;
        }

        if (!turns.CanAffordResources(woodCost, stoneCost))
        {
            Debug.LogWarning($"Not enough resources for {buildLabel}. Need W{woodCost} S{stoneCost}.", this);
            return false;
        }

        if (!turns.TrySpendAction(1))
        {
            Debug.LogWarning($"Cannot build {buildLabel}: failed to spend action.", this);
            return false;
        }

        if (!turns.TrySpendResources(woodCost, stoneCost))
        {
            Debug.LogWarning($"Cannot build {buildLabel}: failed to spend resources.", this);
            return false;
        }

        return true;
    }
}
