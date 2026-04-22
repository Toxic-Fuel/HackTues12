using GridGeneration;
using UnityEngine;
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

    [Header("UI Toolkit")]
    [SerializeField] private UIDocument marketDocument;
    [SerializeField] private PanelSettings panelSettings;
    [SerializeField] private VisualTreeAsset marketLayoutAsset;
    [SerializeField] private StyleSheet marketStyleSheet;
    [SerializeField] private int sortingOrder = 400;

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

    private SlotMaterial leftSlotMaterial;
    private SlotMaterial rightSlotMaterial;

    private bool uiInitialized;
    private bool wasMarketVisible;
    private const int GivePerReceiveRatio = 2;
    
    private void Awake()
    {
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
            return;
        }

        bool showMarket = IsVillageCurrentlySelected();
        SetMarketVisible(showMarket);

        if (showMarket && !wasMarketVisible)
        {
            ResetSlotsToEmpty();
        }

        wasMarketVisible = showMarket;

        if (!showMarket)
        {
            CloseSlotMenus();
            return;
        }

        RefreshTradeState();
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
    }

    private void ToggleRightSlotMenu()
    {
        bool open = rightSlotMenu != null && rightSlotMenu.style.display != DisplayStyle.Flex;
        CloseSlotMenus();
        if (open && rightSlotMenu != null)
        {
            rightSlotMenu.style.display = DisplayStyle.Flex;
        }
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
        return tile != null && tile.tileType == TileType.Village;
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
    }

    private void SetSlotsFromRight(SlotMaterial rightMaterial)
    {
        rightSlotMaterial = rightMaterial;
        leftSlotMaterial = GetOppositeMaterial(rightMaterial);
        UpdateSlotTexts();
        RefreshTradeState();
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
            tradeHelpLabel.text = (leftSlotMaterial == SlotMaterial.None || rightSlotMaterial == SlotMaterial.None)
                ? "Pick a material in either slot."
                : "Left slot gives, right slot receives.";
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
        int available = GetAvailableGivingResourceAmount();
        int maxTradableGive = available - (available % GivePerReceiveRatio);

        if (amountSlider != null)
        {
            amountSlider.lowValue = 0;
            amountSlider.highValue = Mathf.Max(0, maxTradableGive);

            int clampedValue = Mathf.Clamp(amountSlider.value, 0, Mathf.Max(0, maxTradableGive));
            if (clampedValue % GivePerReceiveRatio != 0)
            {
                clampedValue -= clampedValue % GivePerReceiveRatio;
            }

            amountSlider.SetValueWithoutNotify(clampedValue);
            amountSlider.SetEnabled(maxTradableGive >= GivePerReceiveRatio && leftSlotMaterial != SlotMaterial.None);
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
        int giveAmount = amountSlider != null ? Mathf.Max(0, amountSlider.value) : 0;
        int receiveAmount = giveAmount / GivePerReceiveRatio;
        int available = GetAvailableGivingResourceAmount();

        if (amountLabel != null)
        {
            amountLabel.text = $"Trade Amount: Give {giveAmount} -> Receive {receiveAmount}";
        }

        if (confirmTradeButton != null)
        {
            bool canTrade = leftSlotMaterial != SlotMaterial.None
                && rightSlotMaterial != SlotMaterial.None
                && giveAmount >= GivePerReceiveRatio
                && giveAmount % GivePerReceiveRatio == 0
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

        int giveIndex = MaterialToResourceIndex(leftSlotMaterial);
        int receiveIndex = MaterialToResourceIndex(rightSlotMaterial);
        int giveAmount = Mathf.Max(0, amountSlider.value);

        if (giveAmount % GivePerReceiveRatio != 0)
        {
            giveAmount -= giveAmount % GivePerReceiveRatio;
        }

        int receiveAmount = giveAmount / GivePerReceiveRatio;

        if (giveIndex < 0 || receiveIndex < 0 || giveIndex == receiveIndex || giveAmount < GivePerReceiveRatio || receiveAmount <= 0)
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

        int value = Mathf.Max(0, evt.newValue);
        if (value % GivePerReceiveRatio != 0)
        {
            value -= value % GivePerReceiveRatio;
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
}
