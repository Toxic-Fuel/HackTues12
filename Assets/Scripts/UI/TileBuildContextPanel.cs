using GridGeneration;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using QuestSystem;

public class TileBuildContextPanel : MonoBehaviour
{
    [System.Serializable]
    private sealed class IntArrayRow
    {
        public int[] values;
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
    [SerializeField] private GameObject questPanelRoot;
    [SerializeField] private Button buildRoadButton;
    [SerializeField] private Button buildSawmillButton;
    [SerializeField] private Button buildStoneMineButton;
    [SerializeField] private Button confirmBuildButton;
    [SerializeField] private TMP_Text woodCostText;
    [SerializeField] private TMP_Text stoneCostText;

    [Header("Quest UI")]
    [SerializeField] private TMP_Text questUIText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button declineButton;

    [Header("Button Tint")]
    [SerializeField] private Color normalButtonColor = Color.white;
    [SerializeField] private Color selectedButtonColor = new Color(0.82f, 0.82f, 0.62f, 1f);

    [Header("Build Costs")]
    [SerializeField] private IntArrayRow[] buildingCostRows;

    [Header("Build Turn Costs")]
    [SerializeField] private int[] buildingTurnCost;

    [Header("Collector Income Per Turn")]
    [SerializeField] private IntArrayRow[] buildingResourcePerTurnRows;

    // Runtime arrays used by build logic.
    private int[][] buildingCost;
    private int[][] buildingResourcePerTurn;


    [Header("Building Prefabs")]
    [SerializeField] private GameObject[] buildingsPrefab;


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
    private Building selectedOption = Building.None;
    private bool canBuildRoadOption;
    private bool canBuildSawmillOption;
    private bool canBuildStoneMineOption;
    private Quest currentQuest;
    private readonly HashSet<Vector2Int> placedCollectorBuildings = new HashSet<Vector2Int>();

    private void Awake()
    {
        SyncBuildDataFromInspector();

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
            buildRoadButton.onClick.AddListener(() => SelectBuildOption(Building.Road));
        }

        if (buildSawmillButton != null)
        {
            buildSawmillButton.onClick.AddListener(() => SelectBuildOption(Building.Sawmill));
        }

        if (buildStoneMineButton != null)
        {
            buildStoneMineButton.onClick.AddListener(() => SelectBuildOption(Building.ValleyMine));
        }

        if (confirmBuildButton != null)
        {
            confirmBuildButton.onClick.AddListener(TryConfirmSelectedBuild);
        }

        if (acceptButton != null)
        {
            acceptButton.onClick.AddListener(OnAcceptQuestPressed);
        }

        if (declineButton != null)
        {
            declineButton.onClick.AddListener(OnDeclineQuestPressed);
        }

        ConfigurePanelRect(panelRoot);
        ConfigurePanelRect(GetQuestPanelRect());

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
        SetQuestPanelVisible(false);
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

    private void OnValidate()
    {
        SyncBuildDataFromInspector();
    }

    private void SyncBuildDataFromInspector()
    {
        buildingCost = ConvertRowsToJagged(buildingCostRows);
        buildingResourcePerTurn = ConvertRowsToJagged(buildingResourcePerTurnRows);
    }

    private static int[][] ConvertRowsToJagged(IntArrayRow[] rows)
    {
        if (rows == null)
        {
            return new int[0][];
        }

        int[][] result = new int[rows.Length][];
        for (int i = 0; i < rows.Length; i++)
        {
            int[] source = rows[i] != null && rows[i].values != null ? rows[i].values : new int[0];
            result[i] = new int[source.Length];
            System.Array.Copy(source, result[i], source.Length);
        }

        return result;
    }

    private void Update()
    {
        if (InGameGenerationMenu.IsAnyMenuOpen)
        {
            SetPanelVisible(false);
            SetQuestPanelVisible(false);
            return;
        }

        if (tileBuilding == null || gridMap == null || panelRoot == null)
        {
            SetPanelVisible(false);
            SetQuestPanelVisible(false);
            return;
        }

        if (selectTile == null || !selectTile.HasSelection)
        {
            lastCoordinate = new Vector2Int(-1, -1);
            selectedOption = Building.None;
            currentQuest = null;
            UpdateSelectionVisuals();
            //UpdateCostTexts(0, 0);
            SetPanelVisible(false);
            SetQuestPanelVisible(false);
            return;
        }

        Vector2Int coordinate = selectTile.SelectedCoordinate;

        GridTile tile = gridMap.GetTileAt(coordinate.x, coordinate.y);
        GameObject tileInstance = gridMap.GetTileInstanceAt(coordinate.x, coordinate.y);
        if (tile == null || tileInstance == null)
        {
            lastCoordinate = new Vector2Int(-1, -1);
            currentQuest = null;
            SetPanelVisible(false);
            SetQuestPanelVisible(false);
            return;
        }

        if (tile.tileType == TileType.Quest)
        {
            SetPanelVisible(false);
            SetQuestPanelVisible(true);

            RectTransform questRect = GetQuestPanelRect();
            if (questRect != null)
            {
                ConfigurePanelRect(questRect);
                Canvas.ForceUpdateCanvases();
                UpdatePanelPositionAtSelection(tileInstance.transform.position, questRect);
            }

            UpdateQuestPanelContent(tileInstance);
            return;
        }

        currentQuest = null;
        SetQuestPanelVisible(false);

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
            selectedOption = Building.None;
        }

        bool selectedInvalid = selectedOption != Building.None && !IsSelectedOptionCurrentlyBuildable();
        if (selectedInvalid)
        {
            selectedOption = Building.None;
        }

        UpdateSelectionVisuals();
        UpdateCostPreviewForSelection(tile, coordinate);

        if (confirmBuildButton != null)
        {
            confirmBuildButton.interactable = IsSelectedOptionCurrentlyBuildable();
        }
    }

    public void SelectBuildOption(Building option)
    {
        if (!IsOptionBuildable(option))
        {
            return;
        }

        selectedOption = option;
        UpdateSelectionVisuals();

        if (gridMap == null || currentCoordinate.x < 0 || currentCoordinate.y < 0)
        {
            //UpdateCostTexts({0, 0});
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
        Building option = optionIndex switch
        {
            0 => Building.Road,
            1 => Building.Sawmill,
            2 => Building.ValleyMine,
            _ => Building.None
        };

        if (option == Building.None)
        {
            return;
        }

        SelectBuildOption(option);
    }

    public void ConfirmSelectedBuild()
    {
        TryConfirmSelectedBuild();
    }

    public int[][] GetBuildingResourceCost() => buildingCost;

    public bool HasCollectorBuildingAt(Vector2Int coordinate) => placedCollectorBuildings.Contains(coordinate);

    public void GetBuildingActionAndTurnCost(Building building, out int actionCost, out int turnCost)
    {
        actionCost = 1;
        turnCost = 1;

        if (buildingTurnCost == null)
        {
            return;
        }

        int index = (int)building;
        if (index >= 0 && index < buildingTurnCost.Length)
        {
            actionCost = Mathf.Max(0, buildingTurnCost[index]);
        }

        // Old turn-system behavior: build action cost also counts as turn cost.
        turnCost = actionCost;
    }

    private void UpdateCostPreviewForSelection(GridTile tile, Vector2Int coordinate)
    {
        int[] cost = new int[turns != null ? turns.resourceTypesCount : 0];

        switch (selectedOption)
        {
            case Building.Road:
                if (!TryGetRoadCost(tile, coordinate, out cost))
                {
                    cost = new int[turns != null ? turns.resourceTypesCount : 0];
                }
                break;
            case Building.Sawmill:
                cost = GetBuildingCost(Building.Sawmill);
                break;
            case Building.MountainMine:
            case Building.ValleyMine:
                if (TryResolveMineBuildingForCoordinate(coordinate, out Building mineBuilding))
                {
                    cost = GetBuildingCost(mineBuilding);
                }
                break;
            default:
                break;
        }

        UpdateCostTexts(cost);
    }

    private bool TryGetRoadCost(GridTile tile, Vector2Int coordinate, out int[] cost)
    {
        cost = new int[turns != null ? turns.resourceTypesCount : 0];

        if (tileBuilding == null || tile == null)
        {
            return false;
        }

        if (!tileBuilding.CanBuildRoadAt(coordinate))
        {
            return false;
        }

        return tileBuilding.TryGetRoadBuildCost(coordinate, out cost);
    }

    private void UpdateCostTexts(int[] cost)
    {
        int woodIndex = (int)ResourceType.Wood;
        int stoneIndex = (int)ResourceType.Stone;

        int woodCost = (cost != null && woodIndex >= 0 && woodIndex < cost.Length) ? cost[woodIndex] : 0;
        int stoneCost = (cost != null && stoneIndex >= 0 && stoneIndex < cost.Length) ? cost[stoneIndex] : 0;

        if (woodCostText != null)
        {
            woodCostText.text = woodCost.ToString();
        }

        if (stoneCostText != null)
        {
            stoneCostText.text = stoneCost.ToString();
        }
    }

    private void UpdateSelectionVisuals()
    {
        ApplyButtonState(buildRoadButton, selectedOption == Building.Road);
        ApplyButtonState(buildSawmillButton, selectedOption == Building.Sawmill);
        ApplyButtonState(buildStoneMineButton, selectedOption == Building.MountainMine || selectedOption == Building.ValleyMine);
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

    private bool IsOptionBuildable(Building option)
    {
        return option switch
        {
            Building.Road => canBuildRoadOption,
            Building.Sawmill => canBuildSawmillOption,
            Building.MountainMine => canBuildStoneMineOption,
            Building.ValleyMine => canBuildStoneMineOption,
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

        if (TryResolveSelectedBuilding(out Building building))
        {
            OnBuildingBuilt((int)building);
        }
    }

    public void OnBuildingBuilt(int buildingId)
    {
        if (currentCoordinate.x < 0 || currentCoordinate.y < 0)
        {
            return;
        }

        if (!System.Enum.IsDefined(typeof(Building), buildingId))
        {
            Debug.LogWarning($"Unknown building id '{buildingId}'.", this);
            return;
        }

        Building building = (Building)buildingId;
        switch (building)
        {
            case Building.Road:
                BuildRoad();
                break;
            case Building.Sawmill:
            case Building.MountainMine:
            case Building.ValleyMine:
                TryBuildCollectorBuilding(building);
                break;
        }
    }

    private bool TryResolveSelectedBuilding(out Building building)
    {
        building = Building.Road;

        switch (selectedOption)
        {
            case Building.Road:
                building = Building.Road;
                return true;
            case Building.Sawmill:
                building = Building.Sawmill;
                return true;
            case Building.MountainMine:
            case Building.ValleyMine:
                return TryResolveMineBuildingForCoordinate(currentCoordinate, out building);
            default:
                return false;
        }
    }

    private bool TryResolveMineBuildingForCoordinate(Vector2Int coordinate, out Building building)
    {
        building = Building.ValleyMine;
        GridTile tile = gridMap != null ? gridMap.GetTileAt(coordinate.x, coordinate.y) : null;
        if (tile == null)
        {
            return false;
        }

        if (IsMountainMineTile(tile))
        {
            building = Building.MountainMine;
            return true;
        }

        if (IsValleyTile(tile))
        {
            building = Building.ValleyMine;
            return true;
        }

        return false;
    }

    private void UpdatePanelPositionAtSelection(Vector3 worldPosition, RectTransform targetPanel)
    {
        if (canvas == null || worldCamera == null || targetPanel == null)
        {
            return;
        }

        UnityEngine.Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : worldCamera;
        RectTransform parentRect = targetPanel.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(worldCamera, worldPosition);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCamera, out Vector2 localPoint))
        {
            Vector2 panelSize = GetPanelSizeInParentSpace(targetPanel);
            float offsetY = Mathf.Abs(spawnOffsetY);
            float topPart = panelSize.y * (1f - targetPanel.pivot.y);
            float bottomPart = panelSize.y * targetPanel.pivot.y;

            float aboveY = localPoint.y + selectedTileClearancePixels + bottomPart + offsetY;
            Vector2 spawnAbove = new Vector2(localPoint.x, aboveY);
            Vector2 clampedAbove = ClampToParent(spawnAbove, parentRect, targetPanel);
            bool aboveKeepsTileVisible = (clampedAbove.y - bottomPart) >= (localPoint.y + selectedTileClearancePixels);

            if (aboveKeepsTileVisible)
            {
                targetPanel.anchoredPosition = clampedAbove;
                return;
            }

            float belowY = localPoint.y - selectedTileClearancePixels - topPart - offsetY;
            Vector2 spawnBelow = new Vector2(localPoint.x, belowY);
            Vector2 clampedBelow = ClampToParent(spawnBelow, parentRect, targetPanel);
            bool belowKeepsTileVisible = (clampedBelow.y + topPart) <= (localPoint.y - selectedTileClearancePixels);

            if (belowKeepsTileVisible)
            {
                targetPanel.anchoredPosition = clampedBelow;
                return;
            }

            // If both sides are constrained by edges, keep the side with more visual clearance.
            float aboveGap = (clampedAbove.y - bottomPart) - localPoint.y;
            float belowGap = localPoint.y - (clampedBelow.y + topPart);
            targetPanel.anchoredPosition = aboveGap >= belowGap ? clampedAbove : clampedBelow;
        }
    }

    private void UpdatePanelPositionAtSelection(Vector3 worldPosition)
    {
        UpdatePanelPositionAtSelection(worldPosition, panelRoot);
    }

    private void ConfigurePanelRect(RectTransform targetPanel)
    {
        if (targetPanel == null)
        {
            return;
        }

        targetPanel.anchorMin = new Vector2(0.5f, 0.5f);
        targetPanel.anchorMax = new Vector2(0.5f, 0.5f);
        targetPanel.pivot = new Vector2(0.5f, 0.5f);
    }

    private Vector2 GetPanelSizeInParentSpace(RectTransform targetPanel)
    {
        Vector2 panelSize = new Vector2(
            targetPanel.rect.width * Mathf.Abs(targetPanel.lossyScale.x),
            targetPanel.rect.height * Mathf.Abs(targetPanel.lossyScale.y));

        float canvasScale = canvas != null ? canvas.scaleFactor : 1f;
        if (canvasScale > 0.0001f)
        {
            panelSize /= canvasScale;
        }

        return panelSize;
    }

    private Vector2 ClampToParent(Vector2 targetPosition, RectTransform parentRect, RectTransform targetPanel)
    {
        Vector2 panelSize = GetPanelSizeInParentSpace(targetPanel);

        Vector2 pivot = targetPanel.pivot;
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

    private RectTransform GetQuestPanelRect()
    {
        return questPanelRoot != null ? questPanelRoot.GetComponent<RectTransform>() : null;
    }

    private void UpdateQuestPanelContent(GameObject tileInstance)
    {
        currentQuest = tileInstance != null ? tileInstance.GetComponent<Quest>() : null;
        if (currentQuest != null)
        {
            currentQuest.SetTurns(turns);
        }

        if (questUIText != null)
        {
            questUIText.text = currentQuest == null
                ? string.Empty
                : (currentQuest.IsCompleted ? "Thanks for your help!" : currentQuest.description);
        }

        if (acceptButton != null)
        {
            acceptButton.interactable = currentQuest != null && turns != null && !currentQuest.IsCompleted;
        }
    }

    private void OnAcceptQuestPressed()
    {
        if (currentQuest == null)
        {
            return;
        }

        bool completed = currentQuest.PayResources();
        if (completed && selectTile != null)
        {
            selectTile.ClearSelection();
        }
    }

    private void OnDeclineQuestPressed()
    {
        if (selectTile != null)
        {
            selectTile.ClearSelection();
        }
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
    private bool TryGetRoadCostByTile(GridTile tile, out int[] cost)
    {
        // Kept for compatibility with older call paths, but road preview now uses
        // TileBuilding.TryGetRoadBuildCost so dynamic scaling and spending match.
        cost = GetBuildingCost(Building.Road);
        return tile != null;
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

    private void SetQuestPanelVisible(bool visible)
    {
        if (questPanelRoot == null)
        {
            return;
        }

        if (questPanelRoot.activeSelf != visible)
        {
            questPanelRoot.SetActive(visible);
        }
    }

    private void BuildRoad()
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

    private bool TryBuildCollectorBuilding(Building building)
    {
        if (currentCoordinate.x < 0 || currentCoordinate.y < 0)
        {
            return false;
        }

        GridTile tile = gridMap != null ? gridMap.GetTileAt(currentCoordinate.x, currentCoordinate.y) : null;
        if (tile == null)
        {
            Debug.LogWarning($"{building} build blocked: tile data is missing.", this);
            return false;
        }

        if (!IsTileValidForBuilding(building, tile))
        {
            Debug.LogWarning($"{building} build blocked: tile '{tile.tileName}' is invalid for this building.", this);
            return false;
        }

        if (placedCollectorBuildings.Contains(currentCoordinate)
            || (tileBuilding != null && tileBuilding.IsRoadAlreadyBuilt(currentCoordinate)))
        {
            Debug.LogWarning($"{building} build blocked: tile already has a building.", this);
            return false;
        }

        GameObject targetPrefab = GetBuildingPrefab(building);
        if (targetPrefab == null)
        {
            Debug.LogWarning($"{building} prefab is missing.", this);
            return false;
        }

        if (!TrySpendBuildActionAndResources(building, GetBuildingCost(building), building.ToString()))
        {
            return false;
        }

        if (!gridMap.TryReplaceTileVisualAt(currentCoordinate.x, currentCoordinate.y, targetPrefab, Quaternion.identity))
        {
            Debug.LogWarning($"{building} build failed: could not replace tile visual.", this);
            return false;
        }

        if (turns != null)
        {
            turns.AddPerTurnResources(GetBuildingPerTurnIncome(building));
        }

        placedCollectorBuildings.Add(currentCoordinate);
        if (tileBuilding != null)
        {
            tileBuilding.NotifyStructureBuiltAt(currentCoordinate);
            tileBuilding.NotifyRoadPlacementChanged();
        }

        if (selectTile != null)
        {
            selectTile.ClearSelection();
        }

        return true;
    }

    private bool IsTileValidForBuilding(Building building, GridTile tile)
    {
        switch (building)
        {
            case Building.Sawmill:
                return IsForestTile(tile);
            case Building.MountainMine:
                return IsMountainMineTile(tile);
            case Building.ValleyMine:
                return IsValleyTile(tile);
            default:
                return false;
        }
    }

    private GameObject GetBuildingPrefab(Building building)
    {
        int index = (int)building;
        if (buildingsPrefab != null && index >= 0 && index < buildingsPrefab.Length)
        {
            return buildingsPrefab[index];
        }

        // Backward compatibility for older setups where both mines shared index 2.
        if ((building == Building.MountainMine || building == Building.ValleyMine)
            && buildingsPrefab != null
            && buildingsPrefab.Length > 2)
        {
            return buildingsPrefab[2];
        }

        return null;
    }

    private int[] GetBuildingCost(Building building)
    {
        return GetBuildingDataRow(buildingCost, building);
    }

    private int[] GetBuildingPerTurnIncome(Building building)
    {
        return GetBuildingDataRow(buildingResourcePerTurn, building);
    }

    private int[] GetBuildingDataRow(int[][] source, Building building)
    {
        int fallbackLength = turns != null ? turns.resourceTypesCount : 0;
        if (source == null)
        {
            return new int[fallbackLength];
        }

        int index = (int)building;
        if (index >= 0 && index < source.Length && source[index] != null)
        {
            return source[index];
        }

        // Backward compatibility for older setups where both mines shared index 2.
        if ((building == Building.MountainMine || building == Building.ValleyMine)
            && source.Length > 2
            && source[2] != null)
        {
            return source[2];
        }

        return new int[fallbackLength];
    }

    private bool TrySpendBuildActionAndResources(Building building, int[] cost, string buildLabel)
    {
        if (turns == null)
        {
            return true;
        }

        GetBuildingActionAndTurnCost(building, out int actionCost, out int turnCost);

        if (!turns.CanAffordResources(cost))
        {
            Debug.LogWarning($"Cannot build {buildLabel}: not enough resources.", this);
            return false;
        }

        if (actionCost > 0 && (!turns.CanTakeAction || turns.ActionsRemaining < actionCost))
        {
            Debug.LogWarning($"Cannot build {buildLabel}: no actions left this turn.", this);
            return false;
        }

        if (turnCost > 0 && !turns.CanAffordTurns(turnCost))
        {
            Debug.LogWarning($"Cannot build {buildLabel}: not enough turns available.", this);
            return false;
        }

        int turnsBeforeActionSpend = turns.RemainingTurns;

        if (actionCost > 0 && !turns.TrySpendAction(actionCost))
        {
            Debug.LogWarning($"Cannot build {buildLabel}: failed to spend {actionCost} action(s).", this);
            return false;
        }

        int turnsSpentByAction = Mathf.Max(0, turnsBeforeActionSpend - turns.RemainingTurns);
        int remainingTurnCost = Mathf.Max(0, turnCost - turnsSpentByAction);

        if (remainingTurnCost > 0 && !turns.TrySpendTurns(remainingTurnCost))
        {
            Debug.LogWarning($"Cannot build {buildLabel}: failed to spend {remainingTurnCost} turn(s).", this);
            return false;
        }

        if (!turns.TrySpendResources(cost))
        {
            Debug.LogWarning($"Cannot build {buildLabel}: failed to spend resources.", this);
            return false;
        }

        return true;
    }
}
