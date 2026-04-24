using GridGeneration;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

public class Market : MonoBehaviour
{
    private enum SlotMaterial
    {
        None,
        Wood,
        Stone
    }

    [Header("References")]
    [SerializeField] private SelectTile selectTile;
    [SerializeField] private GridMap gridMap;
    [SerializeField] private Turns turns;
    [SerializeField] private TileBuilding tileBuilding;

    [Header("UI Toolkit")]
    [SerializeField] private UIDocument marketDocument;
    [SerializeField] private PanelSettings panelSettings;
    [SerializeField] private VisualTreeAsset marketLayoutAsset;
    [SerializeField] private StyleSheet marketStyleSheet;
    [SerializeField] private int sortingOrder = 400;

    [Header("Button Events")]
    [SerializeField] private UnityEvent OnButtonClicked;

    private const string SlotWoodClass = "material-slot-button--wood";
    private const string SlotStoneClass = "material-slot-button--stone";

    private VisualElement marketRoot;
    private VisualElement marketPanel;
    private Button leftSlotButton;
    private Button rightSlotButton;
    private VisualElement leftSlotMenu;
    private VisualElement rightSlotMenu;
    private Button leftOptionWoodButton;
    private Button leftOptionStoneButton;
    private Button rightOptionWoodButton;
    private Button rightOptionStoneButton;
    private SliderInt amountSlider;
    private Label amountLabel;
    private Label tradeHelpLabel;
    private Button confirmTradeButton;
    private Button closeMarketButton;

    private SlotMaterial leftSlotMaterial;
    private SlotMaterial rightSlotMaterial;

    private bool uiInitialized;
    private bool wasMarketVisible;
    private const int GivePerReceiveRatio = 2;

    private Vector2Int marketSelectionCoordinate = new Vector2Int(-1, -1);
    [SerializeField] private Camera worldCamera;
    [SerializeField, Min(0f)] private float panelVerticalOffsetPixels = 36f;
    [SerializeField, Min(0f)] private float panelScreenEdgePadding = 12f;
    private void Awake()
    {
        OnButtonClicked ??= new UnityEvent();
        if (selectTile == null)
        {
            selectTile = FindAnyObjectByType<SelectTile>();
        }

        if (gridMap == null)
        {
            gridMap = FindAnyObjectByType<GridMap>();
        }

        if (turns == null)
        {
            turns = FindAnyObjectByType<Turns>();
        }

        if (tileBuilding == null)
        {
            tileBuilding = FindAnyObjectByType<TileBuilding>();
        }

        EnsureUiInitialized();
        SetMarketVisible(false);
    }

    private void OnEnable()
    {
        EnsureUiInitialized();
    }

    private void Update()
    {
        if (!uiInitialized)
        {
            EnsureUiInitialized();
        }

        if (InGameGenerationMenu.IsAnyMenuOpen)
        {
            SetMarketVisible(false);
            wasMarketVisible = false;
            marketSelectionCoordinate = new Vector2Int(-1, -1);
            return;
        }

        Vector2Int selectedCoordinate = selectTile != null
            ? selectTile.SelectedCoordinate
            : new Vector2Int(-1, -1);

        bool showMarket = IsVillageCurrentlySelected();

        // If market was open and player clicked a different tile (including another village),
        // close market and clear selection so it stays closed.
        if (showMarket
            && wasMarketVisible
            && marketSelectionCoordinate.x >= 0
            && selectedCoordinate != marketSelectionCoordinate)
        {
            CloseSlotMenus();
            SetMarketVisible(false);
            wasMarketVisible = false;
            marketSelectionCoordinate = new Vector2Int(-1, -1);

            if (selectTile != null)
            {
                selectTile.ClearSelection();
            }

            return;
        }

        SetMarketVisible(showMarket);

        if (showMarket && !wasMarketVisible)
        {
            marketSelectionCoordinate = selectedCoordinate;
            ResetSlotsToEmpty();
        }
        else if (!showMarket)
        {
            marketSelectionCoordinate = new Vector2Int(-1, -1);
        }

        wasMarketVisible = showMarket;

        if (!showMarket)
        {
            CloseSlotMenus();
            return;
        }

        RefreshTradeState();
        UpdateMarketPanelPosition(selectedCoordinate);
    }

    private void EnsureUiInitialized()
    {
        if (uiInitialized)
        {
            return;
        }

        if (marketDocument == null)
        {
            marketDocument = GetComponent<UIDocument>();
        }

        if (marketDocument == null)
        {
            marketDocument = gameObject.AddComponent<UIDocument>();
        }

        ResolvePanelSettings();
        if (marketDocument.panelSettings == null && panelSettings != null)
        {
            marketDocument.panelSettings = panelSettings;
        }

        marketDocument.sortingOrder = sortingOrder;
        marketDocument.enabled = true;

        if (marketLayoutAsset == null)
        {
            marketLayoutAsset = Resources.Load<VisualTreeAsset>("UI/VillageMarketPanel");
        }

        if (marketStyleSheet == null)
        {
            marketStyleSheet = Resources.Load<StyleSheet>("UI/VillageMarketPanel");
        }

        if (marketLayoutAsset == null)
        {
            Debug.LogError("Market: Missing UXML at Resources/UI/VillageMarketPanel.uxml", this);
            return;
        }

        VisualElement root = marketDocument.rootVisualElement;
        if (root == null)
        {
            return;
        }

        root.Clear();
        marketLayoutAsset.CloneTree(root);
        if (marketStyleSheet != null)
        {
            root.styleSheets.Add(marketStyleSheet);
        }

        marketRoot = root.Q<VisualElement>("market-root") ?? root;
        marketPanel = root.Q<VisualElement>("market-panel");
        leftSlotButton = root.Q<Button>("left-slot-button");
        rightSlotButton = root.Q<Button>("right-slot-button");
        leftSlotMenu = root.Q<VisualElement>("left-slot-menu");
        rightSlotMenu = root.Q<VisualElement>("right-slot-menu");
        leftOptionWoodButton = root.Q<Button>("left-option-wood");
        leftOptionStoneButton = root.Q<Button>("left-option-stone");
        rightOptionWoodButton = root.Q<Button>("right-option-wood");
        rightOptionStoneButton = root.Q<Button>("right-option-stone");
        amountSlider = root.Q<SliderInt>("amount-slider");
        amountLabel = root.Q<Label>("amount-label");
        tradeHelpLabel = root.Q<Label>("trade-help-label");
        confirmTradeButton = root.Q<Button>("confirm-trade-button");
        closeMarketButton = root.Q<Button>("market-close-button");

        if (leftSlotButton != null)
        {
            leftSlotButton.clicked += ToggleLeftSlotMenu;
        }

        if (rightSlotButton != null)
        {
            rightSlotButton.clicked += ToggleRightSlotMenu;
        }

        if (leftOptionWoodButton != null)
        {
            leftOptionWoodButton.clicked += () =>
            {
                SetSlotsFromLeft(SlotMaterial.Wood);
                CloseSlotMenus();
            };
        }

        if (leftOptionStoneButton != null)
        {
            leftOptionStoneButton.clicked += () =>
            {
                SetSlotsFromLeft(SlotMaterial.Stone);
                CloseSlotMenus();
            };
        }

        if (rightOptionWoodButton != null)
        {
            rightOptionWoodButton.clicked += () =>
            {
                SetSlotsFromRight(SlotMaterial.Wood);
                CloseSlotMenus();
            };
        }

        if (rightOptionStoneButton != null)
        {
            rightOptionStoneButton.clicked += () =>
            {
                SetSlotsFromRight(SlotMaterial.Stone);
                CloseSlotMenus();
            };
        }

        if (amountSlider != null)
        {
            amountSlider.RegisterValueChangedCallback(OnAmountSliderChanged);
        }

        if (confirmTradeButton != null)
        {
            confirmTradeButton.clicked += ExecuteTrade;
        }

        if (closeMarketButton != null)
        {
            closeMarketButton.clicked += CloseMarketFromButton;
        }

        CloseSlotMenus();
        UpdateSlotTexts();
        RefreshTradeState();
        uiInitialized = true;
    }

    private void ToggleLeftSlotMenu()
    {
        bool open = leftSlotMenu != null && leftSlotMenu.style.display != DisplayStyle.Flex;
        CloseSlotMenus();
        if (open && leftSlotMenu != null)
        {
            leftSlotMenu.style.display = DisplayStyle.Flex;
        }

        OnButtonClicked?.Invoke();
    }

    private void ToggleRightSlotMenu()
    {
        bool open = rightSlotMenu != null && rightSlotMenu.style.display != DisplayStyle.Flex;
        CloseSlotMenus();
        if (open && rightSlotMenu != null)
        {
            rightSlotMenu.style.display = DisplayStyle.Flex;
        }

        OnButtonClicked?.Invoke();
    }

    private void CloseSlotMenus()
    {
        if (leftSlotMenu != null)
        {
            leftSlotMenu.style.display = DisplayStyle.None;
        }

        if (rightSlotMenu != null)
        {
            rightSlotMenu.style.display = DisplayStyle.None;
        }
    }

    private void ResolvePanelSettings()
    {
        if (panelSettings != null)
        {
            return;
        }

        if (marketDocument != null && marketDocument.panelSettings != null)
        {
            panelSettings = marketDocument.panelSettings;
            return;
        }

        UIDocument[] docs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < docs.Length; i++)
        {
            if (docs[i] != null && docs[i] != marketDocument && docs[i].panelSettings != null)
            {
                panelSettings = docs[i].panelSettings;
                return;
            }
        }

        panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings");
    }

    private bool IsVillageCurrentlySelected()
    {
        if (selectTile == null || gridMap == null || !selectTile.HasSelection)
        {
            return false;
        }

        Vector2Int coordinate = selectTile.SelectedCoordinate;
        GridTile tile = gridMap.GetTileAt(coordinate.x, coordinate.y);
        if (!IsVillageTile(tile))
        {
            return false;
        }

        return IsVillageConnectedToRoadNetwork(coordinate);
    }

    private void SetMarketVisible(bool visible)
    {
        if (marketRoot == null)
        {
            return;
        }

        marketRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void SetSlotsFromLeft(SlotMaterial leftMaterial)
    {
        leftSlotMaterial = leftMaterial;
        rightSlotMaterial = GetOppositeMaterial(leftMaterial);
        UpdateSlotTexts();
        RefreshTradeState();
        OnButtonClicked?.Invoke();
    }

    private void SetSlotsFromRight(SlotMaterial rightMaterial)
    {
        rightSlotMaterial = rightMaterial;
        leftSlotMaterial = GetOppositeMaterial(rightMaterial);
        UpdateSlotTexts();
        RefreshTradeState();
        OnButtonClicked?.Invoke();
    }

    private static SlotMaterial GetOppositeMaterial(SlotMaterial material)
    {
        switch (material)
        {
            case SlotMaterial.Wood:
                return SlotMaterial.Stone;
            case SlotMaterial.Stone:
                return SlotMaterial.Wood;
            default:
                return SlotMaterial.None;
        }
    }

    private void UpdateSlotTexts()
    {
        ApplySlotMaterialClass(leftSlotButton, leftSlotMaterial);
        ApplySlotMaterialClass(rightSlotButton, rightSlotMaterial);

        if (tradeHelpLabel != null)
        {
            tradeHelpLabel.style.display = DisplayStyle.None;
        }
    }

    private static void ApplySlotMaterialClass(Button button, SlotMaterial material)
    {
        if (button == null)
        {
            return;
        }

        button.RemoveFromClassList(SlotWoodClass);
        button.RemoveFromClassList(SlotStoneClass);

        switch (material)
        {
            case SlotMaterial.Wood:
                button.AddToClassList(SlotWoodClass);
                break;
            case SlotMaterial.Stone:
                button.AddToClassList(SlotStoneClass);
                break;
        }
    }

    private void RefreshTradeState()
    {
        int ratio = GetCurrentGivePerReceiveRatio();
        int available = GetAvailableGivingResourceAmount();
        int maxTradableGive = available - (available % ratio);

        if (amountSlider != null)
        {
            amountSlider.lowValue = 0;
            amountSlider.highValue = Mathf.Max(0, maxTradableGive);

            int clampedValue = Mathf.Clamp(amountSlider.value, 0, Mathf.Max(0, maxTradableGive));
            if (clampedValue % ratio != 0)
            {
                clampedValue -= clampedValue % ratio;
            }

            amountSlider.SetValueWithoutNotify(clampedValue);
            amountSlider.SetEnabled(maxTradableGive >= ratio && leftSlotMaterial != SlotMaterial.None);
        }

        UpdateAmountLabelAndConfirmState();
    }

    private int GetAvailableGivingResourceAmount()
    {
        if (turns == null || turns.CurrentResources == null)
        {
            return 0;
        }

        int index = MaterialToResourceIndex(leftSlotMaterial);
        if (index < 0 || index >= turns.CurrentResources.Length)
        {
            return 0;
        }

        return Mathf.Max(0, turns.CurrentResources[index]);
    }

    private void UpdateAmountLabelAndConfirmState()
    {
        int ratio = GetCurrentGivePerReceiveRatio();
        int giveAmount = amountSlider != null ? Mathf.Max(0, amountSlider.value) : 0;
        int receiveAmount = ratio > 0 ? giveAmount / ratio : 0;
        int available = GetAvailableGivingResourceAmount();

        if (amountLabel != null)
        {
            amountLabel.text = $"Trade Amount: Give {giveAmount} -> Receive {receiveAmount} ({ratio}:1)";
        }

        if (confirmTradeButton != null)
        {
            bool canTrade = leftSlotMaterial != SlotMaterial.None
                && rightSlotMaterial != SlotMaterial.None
                && ratio > 0
                && giveAmount >= ratio
                && giveAmount % ratio == 0
                && available >= giveAmount;

            confirmTradeButton.SetEnabled(canTrade);
        }
    }

    private void ExecuteTrade()
    {
        if (turns == null || turns.CurrentResources == null || amountSlider == null)
        {
            return;
        }

        int ratio = GetCurrentGivePerReceiveRatio();
        int giveIndex = MaterialToResourceIndex(leftSlotMaterial);
        int receiveIndex = MaterialToResourceIndex(rightSlotMaterial);
        int giveAmount = Mathf.Max(0, amountSlider.value);

        if (ratio > 0 && giveAmount % ratio != 0)
        {
            giveAmount -= giveAmount % ratio;
        }

        int receiveAmount = ratio > 0 ? giveAmount / ratio : 0;

        if (giveIndex < 0 || receiveIndex < 0 || giveIndex == receiveIndex || ratio <= 0 || giveAmount < ratio || receiveAmount <= 0)
        {
            return;
        }

        int[] resources = turns.CurrentResources;
        if (giveIndex >= resources.Length || receiveIndex >= resources.Length)
        {
            return;
        }

        if (resources[giveIndex] < giveAmount)
        {
            return;
        }

        resources[giveIndex] -= giveAmount;
        resources[receiveIndex] += receiveAmount;
        turns.CurrentResources = resources;

        RefreshTradeState();
        OnButtonClicked?.Invoke();
    }

    private static int MaterialToResourceIndex(SlotMaterial material)
    {
        switch (material)
        {
            case SlotMaterial.Wood:
                return (int)ResourceType.Wood;
            case SlotMaterial.Stone:
                return (int)ResourceType.Stone;
            default:
                return -1;
        }
    }

    private void OnAmountSliderChanged(ChangeEvent<int> evt)
    {
        if (amountSlider == null)
        {
            return;
        }

        int ratio = GetCurrentGivePerReceiveRatio();
        int value = Mathf.Max(0, evt.newValue);

        if (ratio > 0 && value % ratio != 0)
        {
            value -= value % ratio;
            amountSlider.SetValueWithoutNotify(value);
        }

        UpdateAmountLabelAndConfirmState();
    }

    private void ResetSlotsToEmpty()
    {
        leftSlotMaterial = SlotMaterial.None;
        rightSlotMaterial = SlotMaterial.None;
        CloseSlotMenus();
        UpdateSlotTexts();
        RefreshTradeState();
    }

    private void CloseMarketFromButton()
    {
        CloseSlotMenus();

        if (selectTile != null)
        {
            selectTile.ClearSelection();
        }

        SetMarketVisible(false);
        wasMarketVisible = false;
        marketSelectionCoordinate = new Vector2Int(-1, -1);
        OnButtonClicked?.Invoke();
    }

    private bool IsVillageConnectedToRoadNetwork(Vector2Int coordinate)
    {
        return tileBuilding != null && tileBuilding.IsCoordinateConnectedToCity(coordinate);
    }

    private static bool IsVillageTile(GridTile tile)
    {
        if (tile == null)
        {
            return false;
        }

        return tile.tileType == TileType.Village
            || string.Equals((tile.tileName ?? string.Empty).Trim(), "Village", System.StringComparison.OrdinalIgnoreCase);
    }

    private int GetConnectedVillageCountSafe()
    {
        return tileBuilding != null ? Mathf.Max(0, tileBuilding.GetConnectedVillageCount()) : 0;
    }

    private int GetTotalVillageCount()
    {
        if (gridMap == null)
        {
            return 0;
        }

        int count = 0;
        for (int x = 0; x < gridMap.Width; x++)
        {
            for (int y = 0; y < gridMap.Height; y++)
            {
                if (IsVillageTile(gridMap.GetTileAt(x, y)))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private int GetCurrentGivePerReceiveRatio()
    {
        int totalVillages = GetTotalVillageCount();
        if (totalVillages <= 0)
        {
            return 3;
        }

        int connectedVillages = Mathf.Clamp(GetConnectedVillageCountSafe(), 0, totalVillages);
        if (connectedVillages <= 0)
        {
            return 3;
        }

        if (connectedVillages * 3 <= totalVillages)
        {
            return 3;
        }

        if (connectedVillages * 3 <= totalVillages * 2)
        {
            return 2;
        }

        return 1;
    }

    private bool TryResolveWorldCamera()
    {
        if (worldCamera != null && worldCamera.isActiveAndEnabled)
        {
            return true;
        }

        if (Camera.main != null && Camera.main.isActiveAndEnabled)
        {
            worldCamera = Camera.main;
            return true;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].isActiveAndEnabled)
            {
                worldCamera = cameras[i];
                return true;
            }
        }

        return false;
    }

    private void UpdateMarketPanelPosition(Vector2Int coordinate)
    {
        if (marketPanel == null || marketRoot == null || gridMap == null)
        {
            return;
        }

        if (coordinate.x < 0 || coordinate.y < 0)
        {
            return;
        }

        if (!TryResolveWorldCamera())
        {
            return;
        }

        GameObject tileInstance = gridMap.GetTileInstanceAt(coordinate.x, coordinate.y);
        if (tileInstance == null)
        {
            return;
        }

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(tileInstance.transform.position);
        if (screenPoint.z <= 0f || marketPanel.panel == null)
        {
            return;
        }

        // Convert world/screen position to UI Toolkit panel coordinates.
        Vector2 screenPos = new Vector2(screenPoint.x, screenPoint.y);
        screenPos.y = Screen.height - screenPos.y;
        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(marketPanel.panel, screenPos);

        float panelWidth = marketPanel.resolvedStyle.width > 1f ? marketPanel.resolvedStyle.width : 460f;
        float panelHeight = marketPanel.resolvedStyle.height > 1f ? marketPanel.resolvedStyle.height : 280f;

        float rootWidth = marketRoot.resolvedStyle.width > 1f ? marketRoot.resolvedStyle.width : Screen.width;
        float rootHeight = marketRoot.resolvedStyle.height > 1f ? marketRoot.resolvedStyle.height : Screen.height;

        float targetLeft = panelPos.x - panelWidth * 0.5f;
        float targetTop = panelPos.y - panelVerticalOffsetPixels - panelHeight;

        float minLeft = panelScreenEdgePadding;
        float maxLeft = Mathf.Max(minLeft, rootWidth - panelWidth - panelScreenEdgePadding);
        float minTop = panelScreenEdgePadding;
        float maxTop = Mathf.Max(minTop, rootHeight - panelHeight - panelScreenEdgePadding);

        targetLeft = Mathf.Clamp(targetLeft, minLeft, maxLeft);
        targetTop = Mathf.Clamp(targetTop, minTop, maxTop);

        marketPanel.style.position = Position.Absolute;
        marketPanel.style.right = StyleKeyword.Null;
        marketPanel.style.bottom = StyleKeyword.Null;
        marketPanel.style.left = targetLeft;
        marketPanel.style.top = targetTop;
    }
}
