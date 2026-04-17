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

    private VisualElement marketRoot;
    private VisualElement marketPanel;
    private Button leftSlotButton;
    private Button rightSlotButton;
    private SliderInt amountSlider;
    private Label amountLabel;
    private Label tradeHelpLabel;
    private Button confirmTradeButton;

    private SlotMaterial leftSlotMaterial = SlotMaterial.None;
    private SlotMaterial rightSlotMaterial = SlotMaterial.None;

    private bool uiInitialized;

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
            return;
        }

        bool showMarket = IsVillageCurrentlySelected();
        SetMarketVisible(showMarket);

        if (!showMarket)
        {
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
        amountSlider = root.Q<SliderInt>("amount-slider");
        amountLabel = root.Q<Label>("amount-label");
        tradeHelpLabel = root.Q<Label>("trade-help-label");
        confirmTradeButton = root.Q<Button>("confirm-trade-button");

        if (leftSlotButton != null)
        {
            leftSlotButton.clicked += OnLeftSlotClicked;
        }

        if (rightSlotButton != null)
        {
            rightSlotButton.clicked += OnRightSlotClicked;
        }

        if (amountSlider != null)
        {
            amountSlider.RegisterValueChangedCallback(_ => UpdateAmountLabelAndConfirmState());
        }

        if (confirmTradeButton != null)
        {
            confirmTradeButton.clicked += ExecuteTrade;
        }

        UpdateSlotTexts();
        RefreshTradeState();
        uiInitialized = true;
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

    private void OnLeftSlotClicked()
    {
        SlotMaterial next = NextMaterial(leftSlotMaterial);
        if (next == SlotMaterial.None)
        {
            next = SlotMaterial.Wood;
        }

        SetSlotsFromLeft(next);
    }

    private void OnRightSlotClicked()
    {
        SlotMaterial next = NextMaterial(rightSlotMaterial);
        if (next == SlotMaterial.None)
        {
            next = SlotMaterial.Wood;
        }

        SetSlotsFromRight(next);
    }

    private static SlotMaterial NextMaterial(SlotMaterial material)
    {
        switch (material)
        {
            case SlotMaterial.None:
                return SlotMaterial.Wood;
            case SlotMaterial.Wood:
                return SlotMaterial.Stone;
            case SlotMaterial.Stone:
                return SlotMaterial.Wood;
            default:
                return SlotMaterial.Wood;
        }
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
        if (leftSlotButton != null)
        {
            leftSlotButton.text = MaterialToText(leftSlotMaterial);
        }

        if (rightSlotButton != null)
        {
            rightSlotButton.text = MaterialToText(rightSlotMaterial);
        }

        if (tradeHelpLabel != null)
        {
            if (leftSlotMaterial == SlotMaterial.None || rightSlotMaterial == SlotMaterial.None)
            {
                tradeHelpLabel.text = "Pick a material in either slot.";
            }
            else
            {
                tradeHelpLabel.text = "Left slot gives, right slot receives.";
            }
        }
    }

    private static string MaterialToText(SlotMaterial material)
    {
        switch (material)
        {
            case SlotMaterial.Wood:
                return "Wood";
            case SlotMaterial.Stone:
                return "Stone";
            default:
                return "Empty";
        }
    }

    private void RefreshTradeState()
    {
        int available = GetAvailableGivingResourceAmount();

        if (amountSlider != null)
        {
            amountSlider.lowValue = 0;
            amountSlider.highValue = Mathf.Max(0, available);

            int clampedValue = Mathf.Clamp(amountSlider.value, 0, Mathf.Max(0, available));
            amountSlider.SetValueWithoutNotify(clampedValue);
            amountSlider.SetEnabled(available > 0 && leftSlotMaterial != SlotMaterial.None);
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
        int tradeAmount = amountSlider != null ? Mathf.Max(0, amountSlider.value) : 0;
        int available = GetAvailableGivingResourceAmount();

        if (amountLabel != null)
        {
            amountLabel.text = $"Trade Amount: {tradeAmount} / {available}";
        }

        if (confirmTradeButton != null)
        {
            bool canTrade = leftSlotMaterial != SlotMaterial.None
                && rightSlotMaterial != SlotMaterial.None
                && tradeAmount > 0
                && available >= tradeAmount;

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
        int amount = Mathf.Max(0, amountSlider.value);

        if (giveIndex < 0 || receiveIndex < 0 || giveIndex == receiveIndex || amount <= 0)
        {
            return;
        }

        int[] resources = turns.CurrentResources;
        if (giveIndex >= resources.Length || receiveIndex >= resources.Length)
        {
            return;
        }

        if (resources[giveIndex] < amount)
        {
            return;
        }

        resources[giveIndex] -= amount;
        resources[receiveIndex] += amount;
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
}
