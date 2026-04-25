using GridGeneration;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UnityEngine.Events;

public class InGameGenerationMenu : MonoBehaviour
{
    private struct PendingGridSettings
    {
        public float obstaclePercent;
        public bool limitMineSourceTiles;
        public int maxMineSourceTilesCap;
        public int minMineSourceTiles;
        public bool guaranteeStarterResources;
        public int starterMinDistance;
        public int starterRadius;
        public bool preserveNearestMine;
        public string seedText;
        public bool regenerateOnApply;
    }

    private enum DifficultyPreset
    {
        Easy,
        Medium,
        Hard,
        Extreme
    }

    [Header("References")]
    [SerializeField] private GridMap gridMap;
    [SerializeField] private UIDocument menuDocument;
    [SerializeField] private PanelSettings panelSettings;
    [SerializeField] private VisualTreeAsset menuLayoutAsset;
    [SerializeField] private StyleSheet menuStyleSheet;

    [Header("Menu")]
    [SerializeField] private InputActionReference toggleMenuAction;
    [SerializeField] private bool showOnStart;
    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool blockGameplayInputWhileOpen = true;
    [SerializeField] private bool blockSceneUiInputWhileMenuIsOpen = true;
    [SerializeField] private bool mainMenuOnlyUntilGridMapFound = true;
    [SerializeField] private bool showFloatingToggleButton = true;
    [SerializeField] private Vector2 floatingButtonSize = new Vector2(220f, 84f);
    [SerializeField, Range(0f, 0.5f)] private float floatingButtonToggleCooldown = 0.2f;
    [SerializeField] private bool autoFindGridMap = true;
    [SerializeField] private bool autoApplyPendingChangesWhenGridMapAppears = true;

    [Header("Button Events")]
    [SerializeField] private UnityEvent OnButtonClicked;

    [Header("UI Scale")]
    [SerializeField] private bool scaleWithScreenShortSide = true;
    [SerializeField, Min(320f)] private float referenceShortSidePixels = 1080f;
    [SerializeField, Range(0.5f, 2.2f)] private float minUiScale = 0.75f;
    [SerializeField, Range(0.7f, 2.2f)] private float maxUiScale = 1.35f;
    [SerializeField, Range(0.6f, 1.6f)] private float baseMenuScaleBoost = 0.9f;
    [SerializeField, Range(24f, 96f)] private float controlHeight = 58f;

    [Header("Regeneration")]
    [SerializeField] private bool regenerateImmediatelyOnApply = true;
    [SerializeField] private bool regenerateWhenApplyingPendingChanges = true;

    private static InGameGenerationMenu instance;
    private static bool hasPendingGridSettings;
    private static PendingGridSettings pendingGridSettings;
    private const string OverlayOpenClass = "gen-menu-overlay--open";
    private const string ButtonPressedClass = "gen-menu-button--pressed";

    public static bool IsAnyMenuOpen { get; private set; }
    public static bool IsMenuVisible { get; private set; }

    public static bool IsSceneActionBlocked()
    {
        return instance != null && instance.isOpen && instance.CanDisplayMenu();
    }

    private float obstaclePercent = 0.30f;
    private bool limitMineSourceTiles = true;
    private int maxMineSourceTilesCap = 20;
    private int minMineSourceTiles = 6;
    private bool guaranteeStarterResources = true;
    private int starterMinDistance = 2;
    private int starterRadius = 4;
    private bool preserveNearestMine = true;
    private string seedText = string.Empty;

    private bool isOpen;
    private bool pendingApplyWithoutGridMap;
    private float nextFloatingToggleAllowedAt;

    private bool uiInitialized;
    private bool uiCallbacksRegistered;
    private bool suppressUiCallbacks;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;

    private VisualElement root;
    private VisualElement menuRoot;
    private VisualElement overlay;
    private VisualElement blocker;
    private VisualElement window;
    private VisualElement mineSourceGroup;
    private VisualElement starterGroup;
    private ScrollView menuScroll;

    private Button openButton;
    private Button closeTopButton;
    private Button closeBottomButton;
    private Button easyButton;
    private Button mediumButton;
    private Button hardButton;
    private Button extremeButton;
    private Button applyButton;
    private Button applySeedButton;

    private TextField seedField;
    private Toggle limitMineSourceToggle;
    private Toggle guaranteeStarterToggle;
    private Toggle preserveNearestMineToggle;

    private Slider obstacleSlider;
    private SliderInt maxMineSourceSlider;
    private SliderInt minMineSourceSlider;
    private SliderInt starterMinDistanceSlider;
    private SliderInt starterRadiusSlider;

    private Label obstacleLabel;
    private Label maxMineSourceLabel;
    private Label minMineSourceLabel;
    private Label starterMinDistanceLabel;
    private Label starterRadiusLabel;

    private void Awake()
    {
        OnButtonClicked ??= new UnityEvent();
        if (persistAcrossScenes)
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        EnsureUiInitialized();

        if (gridMap == null)
        {
            TryResolveGridMap();
        }

        isOpen = showOnStart;
        SetMenuOpenState(isOpen);

        if (!CanDisplayMenu())
        {
            SetMenuOpenState(false);
        }

        if (gridMap != null)
        {
            TryApplyPendingSettingsToGridMap();
            PullFromGridMap();
        }

        SyncControlsFromState();
        ApplyResponsiveLayout(force: true);
    }

    private void OnEnable()
    {
        if (toggleMenuAction != null && toggleMenuAction.action != null)
        {
            toggleMenuAction.action.Enable();
        }

        EnsureUiInitialized();
    }

    private void OnDisable()
    {
        if (toggleMenuAction != null && toggleMenuAction.action != null)
        {
            toggleMenuAction.action.Disable();
        }

        SetMenuOpenState(false);

        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        EnsureUiInitialized();
        TryResolveGridMap();

        if (!CanDisplayMenu())
        {
            SetMenuOpenState(false);
            if (menuRoot != null)
            {
                menuRoot.style.display = DisplayStyle.None;
            }

            return;
        }

        if (menuRoot != null)
        {
            menuRoot.style.display = DisplayStyle.Flex;
        }

        bool shouldToggle = false;

        if (toggleMenuAction != null && toggleMenuAction.action != null && toggleMenuAction.action.WasPressedThisFrame())
        {
            bool actionFromTouchscreen = toggleMenuAction.action.activeControl != null
                && toggleMenuAction.action.activeControl.device is Touchscreen;

            if (!actionFromTouchscreen)
            {
                shouldToggle = true;
            }
            else if (!isOpen && !showFloatingToggleButton)
            {
                shouldToggle = true;
            }
        }

        if (!shouldToggle && Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
        {
            shouldToggle = true;
        }

        if (!shouldToggle && isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            shouldToggle = true;
        }

        if (shouldToggle)
        {
            ToggleMenu();
        }

        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            ApplyResponsiveLayout(force: true);
        }
    }

    private void EnsureUiInitialized()
    {
        if (uiInitialized && root != null)
        {
            return;
        }

        if (menuDocument == null)
        {
            menuDocument = GetComponent<UIDocument>();
        }

        if (menuDocument == null)
        {
            menuDocument = gameObject.AddComponent<UIDocument>();
        }

        ResolvePanelSettings();
        if (menuDocument.panelSettings == null && panelSettings != null)
        {
            menuDocument.panelSettings = panelSettings;
        }

        if (menuLayoutAsset == null)
        {
            menuLayoutAsset = Resources.Load<VisualTreeAsset>("UI/InGameGenerationMenu");
        }

        if (menuStyleSheet == null)
        {
            menuStyleSheet = Resources.Load<StyleSheet>("UI/InGameGenerationMenu");
        }

        if (menuLayoutAsset == null)
        {
            Debug.LogError("InGameGenerationMenu: Missing UI layout. Expected Resources/UI/InGameGenerationMenu.uxml", this);
            return;
        }

        menuDocument.enabled = true;
        root = menuDocument.rootVisualElement;
        if (root == null)
        {
            return;
        }

        root.Clear();
        menuLayoutAsset.CloneTree(root);
        if (menuStyleSheet != null)
        {
            root.styleSheets.Add(menuStyleSheet);
        }

        CacheUiReferences();
        RegisterUiCallbacks();

        uiInitialized = true;
    }

    private void ResolvePanelSettings()
    {
        if (panelSettings != null)
        {
            return;
        }

        if (menuDocument != null && menuDocument.panelSettings != null)
        {
            panelSettings = menuDocument.panelSettings;
            return;
        }

        UIDocument[] docs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include);
        for (int i = 0; i < docs.Length; i++)
        {
            if (docs[i] != null && docs[i] != menuDocument && docs[i].panelSettings != null)
            {
                panelSettings = docs[i].panelSettings;
                return;
            }
        }

        panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings");
    }

    private void CacheUiReferences()
    {
        menuRoot = root.Q<VisualElement>("gen-menu-root");
        overlay = root.Q<VisualElement>("gen-menu-overlay");
        blocker = root.Q<VisualElement>("gen-menu-blocker");
        window = root.Q<VisualElement>("gen-menu-window");
        mineSourceGroup = root.Q<VisualElement>("mine-source-group");
        starterGroup = root.Q<VisualElement>("starter-group");
        menuScroll = root.Q<ScrollView>("gen-menu-scroll");

        openButton = root.Q<Button>("gen-menu-open-button");
        closeTopButton = root.Q<Button>("gen-menu-close-button");
        closeBottomButton = root.Q<Button>("close-bottom-button");
        easyButton = root.Q<Button>("preset-easy-button");
        mediumButton = root.Q<Button>("preset-medium-button");
        hardButton = root.Q<Button>("preset-hard-button");
        extremeButton = root.Q<Button>("preset-extreme-button");
        applyButton = root.Q<Button>("apply-button");
        applySeedButton = root.Q<Button>("apply-seed-button");

        seedField = root.Q<TextField>("seed-input");
        limitMineSourceToggle = root.Q<Toggle>("limit-mine-sources-toggle");
        guaranteeStarterToggle = root.Q<Toggle>("guarantee-starter-toggle");
        preserveNearestMineToggle = root.Q<Toggle>("preserve-nearest-mine-toggle");

        obstacleSlider = root.Q<Slider>("obstacle-slider");
        maxMineSourceSlider = root.Q<SliderInt>("max-mine-source-slider");
        minMineSourceSlider = root.Q<SliderInt>("min-mine-source-slider");
        starterMinDistanceSlider = root.Q<SliderInt>("starter-min-distance-slider");
        starterRadiusSlider = root.Q<SliderInt>("starter-radius-slider");

        obstacleLabel = root.Q<Label>("obstacle-percent-label");
        maxMineSourceLabel = root.Q<Label>("max-mine-source-percent-label");
        minMineSourceLabel = root.Q<Label>("min-mine-source-tiles-label");
        starterMinDistanceLabel = root.Q<Label>("starter-min-distance-label");
        starterRadiusLabel = root.Q<Label>("starter-radius-label");

        if (overlay != null)
        {
            overlay.pickingMode = PickingMode.Ignore;
        }

        if (root != null)
        {
            root.pickingMode = PickingMode.Ignore;
        }

        if (menuRoot != null)
        {
            menuRoot.pickingMode = PickingMode.Ignore;
        }

        if (openButton != null)
        {
            openButton.pickingMode = PickingMode.Position;
        }

        if (window != null)
        {
            window.pickingMode = PickingMode.Position;
        }

        if (blocker != null)
        {
            blocker.pickingMode = PickingMode.Position;
        }

        if (menuScroll != null)
        {
            menuScroll.mode = ScrollViewMode.Vertical;
            menuScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            menuScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            menuScroll.pickingMode = PickingMode.Position;
        }
    }

    private void RegisterUiCallbacks()
    {
        if (uiCallbacksRegistered)
        {
            return;
        }

        if (openButton != null)
        {
            openButton.clicked += HandleOpenButtonClicked;
        }

        if (closeTopButton != null)
        {
            closeTopButton.clicked += HandleCloseButtonClicked;
        }

        if (closeBottomButton != null)
        {
            closeBottomButton.clicked += HandleCloseButtonClicked;
        }

        if (easyButton != null)
        {
            easyButton.clicked += () => HandlePresetClicked(DifficultyPreset.Easy);
        }

        if (mediumButton != null)
        {
            mediumButton.clicked += () => HandlePresetClicked(DifficultyPreset.Medium);
        }

        if (hardButton != null)
        {
            hardButton.clicked += () => HandlePresetClicked(DifficultyPreset.Hard);
        }

        if (extremeButton != null)
        {
            extremeButton.clicked += () => HandlePresetClicked(DifficultyPreset.Extreme);
        }

        if (applyButton != null)
        {
            applyButton.clicked += HandleApplyClicked;
        }

        if (applySeedButton != null)
        {
            applySeedButton.clicked += HandleApplySeedClicked;
        }

        if (seedField != null)
        {
            seedField.RegisterValueChangedCallback(evt =>
            {
                if (suppressUiCallbacks)
                {
                    return;
                }

                string sanitized = SanitizeSeedInput(evt.newValue);
                if (!string.Equals(sanitized, evt.newValue))
                {
                    suppressUiCallbacks = true;
                    seedField.SetValueWithoutNotify(sanitized);
                    suppressUiCallbacks = false;
                }

                seedText = sanitized;
            });
        }

        if (obstacleSlider != null)
        {
            obstacleSlider.RegisterValueChangedCallback(evt =>
            {
                if (suppressUiCallbacks)
                {
                    return;
                }

                obstaclePercent = Mathf.Clamp(evt.newValue, 0f, 0.85f);
                UpdateObstacleLabel();
            });
        }

        if (limitMineSourceToggle != null)
        {
            limitMineSourceToggle.RegisterValueChangedCallback(evt =>
            {
                if (suppressUiCallbacks)
                {
                    return;
                }

                limitMineSourceTiles = evt.newValue;
                UpdateSectionVisibility();
            });
        }

        if (maxMineSourceSlider != null)
        {
            maxMineSourceSlider.RegisterValueChangedCallback(evt =>
            {
                if (suppressUiCallbacks)
                {
                    return;
                }

                maxMineSourceTilesCap = Mathf.Clamp(evt.newValue, 0, GetMineSourceCapMax());
                UpdateMineSourceLabels();
            });
        }

        if (minMineSourceSlider != null)
        {
            minMineSourceSlider.RegisterValueChangedCallback(evt =>
            {
                if (suppressUiCallbacks)
                {
                    return;
                }

                minMineSourceTiles = Mathf.Clamp(evt.newValue, 0, 30);
                UpdateMineSourceLabels();
            });
        }

        if (preserveNearestMineToggle != null)
        {
            preserveNearestMineToggle.RegisterValueChangedCallback(evt =>
            {
                if (suppressUiCallbacks)
                {
                    return;
                }

                preserveNearestMine = evt.newValue;
            });
        }

        if (guaranteeStarterToggle != null)
        {
            guaranteeStarterToggle.RegisterValueChangedCallback(evt =>
            {
                if (suppressUiCallbacks)
                {
                    return;
                }

                guaranteeStarterResources = evt.newValue;
                UpdateSectionVisibility();
            });
        }

        if (starterMinDistanceSlider != null)
        {
            starterMinDistanceSlider.RegisterValueChangedCallback(evt =>
            {
                if (suppressUiCallbacks)
                {
                    return;
                }

                starterMinDistance = Mathf.Clamp(evt.newValue, 1, 8);
                if (starterRadius < starterMinDistance)
                {
                    starterRadius = starterMinDistance;
                    suppressUiCallbacks = true;
                    if (starterRadiusSlider != null)
                    {
                        starterRadiusSlider.SetValueWithoutNotify(starterRadius);
                    }

                    suppressUiCallbacks = false;
                }

                UpdateStarterLabels();
            });
        }

        if (starterRadiusSlider != null)
        {
            starterRadiusSlider.RegisterValueChangedCallback(evt =>
            {
                if (suppressUiCallbacks)
                {
                    return;
                }

                starterRadius = Mathf.Clamp(evt.newValue, 1, 12);
                if (starterRadius < starterMinDistance)
                {
                    starterRadius = starterMinDistance;
                    suppressUiCallbacks = true;
                    starterRadiusSlider.SetValueWithoutNotify(starterRadius);
                    suppressUiCallbacks = false;
                }

                UpdateStarterLabels();
            });
        }

        uiCallbacksRegistered = true;
    }

    private void HandleOpenButtonClicked()
    {
        AnimateButtonPress(openButton);

        if (Time.unscaledTime < nextFloatingToggleAllowedAt)
        {
            return;
        }

        nextFloatingToggleAllowedAt = Time.unscaledTime + Mathf.Max(0f, floatingButtonToggleCooldown);

        if (!isOpen)
        {
            SetMenuOpenState(true);
            if (gridMap != null)
            {
                PullFromGridMap();
                SyncControlsFromState();
            }
        }

        OnButtonClicked?.Invoke();
    }

    private void HandleCloseButtonClicked()
    {
        AnimateButtonPress(closeTopButton);
        AnimateButtonPress(closeBottomButton);
        SetMenuOpenState(false);
        OnButtonClicked?.Invoke();
    }

    private void HandlePresetClicked(DifficultyPreset preset)
    {
        switch (preset)
        {
            case DifficultyPreset.Easy:
                AnimateButtonPress(easyButton);
                break;
            case DifficultyPreset.Medium:
                AnimateButtonPress(mediumButton);
                break;
            case DifficultyPreset.Hard:
                AnimateButtonPress(hardButton);
                break;
            case DifficultyPreset.Extreme:
                AnimateButtonPress(extremeButton);
                break;
        }

        ApplyPreset(preset);
        SyncControlsFromState();
        OnButtonClicked?.Invoke();
    }

    private void HandleApplyClicked()
    {
        AnimateButtonPress(applyButton);
        ApplyStateFromControls();

        if (gridMap != null)
        {
            PushToGridMap();
            if (regenerateImmediatelyOnApply)
            {
                gridMap.GenerateLandMap();
            }
        }
        else
        {
            CacheCurrentSettingsForPendingApply(regenerateWhenApplyingPendingChanges);
            pendingApplyWithoutGridMap = true;
        }

        OnButtonClicked?.Invoke();
    }

    private void HandleApplySeedClicked()
    {
        AnimateButtonPress(applySeedButton);
        seedText = Random.Range(int.MinValue, int.MaxValue).ToString();
        if (seedField != null)
        {
            suppressUiCallbacks = true;
            seedField.SetValueWithoutNotify(seedText);
            suppressUiCallbacks = false;
        }

        ApplyStateFromControls();

        if (gridMap != null)
        {
            PushToGridMap();
            gridMap.GenerateLandMap();
        }
        else
        {
            CacheCurrentSettingsForPendingApply(true);
            pendingApplyWithoutGridMap = true;
        }

        OnButtonClicked?.Invoke();
    }

    private void ToggleMenu()
    {
        SetMenuOpenState(!isOpen);
        if (isOpen && gridMap != null)
        {
            PullFromGridMap();
            SyncControlsFromState();
        }
    }

    private void SetMenuOpenState(bool open)
    {
        isOpen = open;
        IsMenuVisible = isOpen;
        IsAnyMenuOpen = blockGameplayInputWhileOpen && isOpen;
        UpdateUiVisibility();
    }

    private void UpdateUiVisibility()
    {
        if (overlay != null)
        {
            overlay.style.display = isOpen ? DisplayStyle.Flex : DisplayStyle.None;
            overlay.EnableInClassList(OverlayOpenClass, isOpen);
        }

        if (openButton != null)
        {
            openButton.style.display = showFloatingToggleButton && !isOpen ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (blocker != null)
        {
            bool shouldBlock = blockSceneUiInputWhileMenuIsOpen && isOpen;
            blocker.style.display = shouldBlock ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void UpdateSectionVisibility()
    {
        if (mineSourceGroup != null)
        {
            mineSourceGroup.style.display = limitMineSourceTiles ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (starterGroup != null)
        {
            starterGroup.style.display = guaranteeStarterResources ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void ApplyResponsiveLayout(bool force)
    {
        if (!force && Screen.width == lastScreenWidth && Screen.height == lastScreenHeight)
        {
            return;
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        float uiScale = ComputeUiScale();
        float widthScale = Mathf.Max(0.1f, Screen.width / 1920f);
        float heightScale = Mathf.Max(0.1f, Screen.height / 1080f);
        float targetHeight = Mathf.Clamp(controlHeight * uiScale, 48f, 112f);

        if (openButton != null)
        {
            openButton.style.width = Mathf.Clamp(floatingButtonSize.x * uiScale, 140f, Screen.width * 0.55f);
            openButton.style.height = Mathf.Clamp(floatingButtonSize.y * uiScale, 52f, Screen.height * 0.16f);
            openButton.style.left = 12f * uiScale;
            openButton.style.bottom = 12f * uiScale;
            openButton.style.fontSize = Mathf.Clamp(21f * uiScale, 16f, 30f);
        }

        if (window != null)
        {
            float windowWidth = Mathf.Clamp(Screen.width * 0.92f, 320f, 980f * Mathf.Clamp(widthScale, 0.85f, 1.2f));
            float windowHeight = Mathf.Clamp(Screen.height * 0.82f, 460f, Screen.height * 0.88f);
            window.style.width = windowWidth;
            window.style.maxWidth = windowWidth;
            window.style.height = windowHeight;
            window.style.minHeight = Mathf.Min(windowHeight, Screen.height * 0.70f);
            window.style.maxHeight = Screen.height * 0.90f;
            window.style.fontSize = Mathf.Clamp(18f * Mathf.Clamp(heightScale, 0.85f, 1.2f), 16f, 28f);
        }

        SetControlMinHeight(seedField, targetHeight);
        SetControlMinHeight(limitMineSourceToggle, targetHeight);
        SetControlMinHeight(guaranteeStarterToggle, targetHeight);
        SetControlMinHeight(preserveNearestMineToggle, targetHeight);
        SetControlMinHeight(obstacleSlider, targetHeight);
        SetControlMinHeight(maxMineSourceSlider, targetHeight);
        SetControlMinHeight(minMineSourceSlider, targetHeight);
        SetControlMinHeight(starterMinDistanceSlider, targetHeight);
        SetControlMinHeight(starterRadiusSlider, targetHeight);
        SetControlMinHeight(applyButton, targetHeight);
        SetControlMinHeight(applySeedButton, targetHeight);
        SetControlMinHeight(closeTopButton, targetHeight * 0.9f);
        SetControlMinHeight(closeBottomButton, targetHeight);
        SetControlMinHeight(easyButton, targetHeight);
        SetControlMinHeight(mediumButton, targetHeight);
        SetControlMinHeight(hardButton, targetHeight);
        SetControlMinHeight(extremeButton, targetHeight);
    }

    private static void SetControlMinHeight(VisualElement element, float height)
    {
        if (element == null)
        {
            return;
        }

        element.style.minHeight = height;
    }

    private float ComputeUiScale()
    {
        if (!scaleWithScreenShortSide)
        {
            return 1f;
        }

        float widthScale = Mathf.Max(0.1f, Screen.width / 1920f);
        float heightScale = Mathf.Max(0.1f, Screen.height / 1080f);
        float shortSide = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height));
        float referenceSide = Mathf.Max(320f, referenceShortSidePixels);
        float shortSideScale = shortSide / referenceSide;
        float narrowSideScale = Mathf.Min(widthScale, heightScale);

        // Bias toward the narrow side so tall phones do not get oversized UI.
        float rawScale = Mathf.Lerp(shortSideScale, narrowSideScale, 0.45f);
        rawScale *= Mathf.Max(0.6f, baseMenuScaleBoost);

        // Touch devices need slightly larger controls for reliable finger interaction.
        bool touchDevice = Input.touchSupported || Touchscreen.current != null;
        if (touchDevice)
        {
            rawScale *= 1.03f;
        }

        float minScale = Mathf.Min(minUiScale, maxUiScale);
        float maxScale = Mathf.Max(minUiScale, maxUiScale);
        return Mathf.Clamp(rawScale, minScale, maxScale);
    }

    private void AnimateButtonPress(Button button)
    {
        if (button == null)
        {
            return;
        }

        button.RemoveFromClassList(ButtonPressedClass);
        button.AddToClassList(ButtonPressedClass);
        button.schedule.Execute(() => button.RemoveFromClassList(ButtonPressedClass)).StartingIn(120);
    }

    private void TryResolveGridMap()
    {
        if (gridMap != null || !autoFindGridMap)
        {
            return;
        }

        gridMap = FindAnyObjectByType<GridMap>();
        if (gridMap == null)
        {
            return;
        }

        if (TryApplyPendingSettingsToGridMap())
        {
            pendingApplyWithoutGridMap = false;
        }

        if (pendingApplyWithoutGridMap && autoApplyPendingChangesWhenGridMapAppears)
        {
            PushToGridMap();
            if (regenerateWhenApplyingPendingChanges)
            {
                gridMap.GenerateLandMap();
            }

            pendingApplyWithoutGridMap = false;
        }

        if (CanDisplayMenu())
        {
            PullFromGridMap();
            SyncControlsFromState();
        }
        else
        {
            SetMenuOpenState(false);
        }
    }

    private bool CanDisplayMenu()
    {
        return !mainMenuOnlyUntilGridMapFound || gridMap == null;
    }

    private int GetGridCellCountOrDefault()
    {
        int width = gridMap != null ? Mathf.Max(1, gridMap.Width) : 20;
        int height = gridMap != null ? Mathf.Max(1, gridMap.Height) : 20;
        return width * height;
    }

    private int GetMineSourceCapMax()
    {
        return Mathf.Max(30, GetGridCellCountOrDefault());
    }

    private int ConvertPercentToMineSourceCap(float percent)
    {
        int cap = Mathf.RoundToInt(Mathf.Clamp01(percent) * GetGridCellCountOrDefault());
        return Mathf.Clamp(cap, 0, GetMineSourceCapMax());
    }

    private float ConvertMineSourceCapToPercent(int cap)
    {
        int cellCount = Mathf.Max(1, GetGridCellCountOrDefault());
        return Mathf.Clamp01(cap / (float)cellCount);
    }

    private void UpdateMineSourceSliderRange()
    {
        if (maxMineSourceSlider == null)
        {
            return;
        }

        int maxCap = GetMineSourceCapMax();
        maxMineSourceSlider.lowValue = 0;
        maxMineSourceSlider.highValue = maxCap;
        maxMineSourceTilesCap = Mathf.Clamp(maxMineSourceTilesCap, 0, maxCap);
    }

    private void PullFromGridMap()
    {
        if (gridMap == null)
        {
            return;
        }

        if (gridMap.seed == 0)
        {
            gridMap.seed = GenerateRandomSeed();
        }

        obstaclePercent = gridMap.ObstaclePercent;
        limitMineSourceTiles = gridMap.LimitMineSourceTiles;
        maxMineSourceTilesCap = ConvertPercentToMineSourceCap(gridMap.MaxMineSourcePercent);
        minMineSourceTiles = gridMap.MinMineSourceTiles;
        guaranteeStarterResources = gridMap.GuaranteeStarterResourceNodes;
        starterMinDistance = gridMap.StarterResourceMinDistanceFromCity;
        starterRadius = gridMap.StarterResourceRadius;
        preserveNearestMine = gridMap.PreserveNearestMineSourceToCity;
        seedText = NormalizeSeedText(gridMap.seed.ToString());
    }

    private static int GenerateRandomSeed()
    {
        int generatedSeed;
        do
        {
            generatedSeed = Random.Range(int.MinValue, int.MaxValue);
        }
        while (generatedSeed == 0);

        return generatedSeed;
    }

    private static string NormalizeSeedText(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "0")
        {
            return GenerateRandomSeed().ToString();
        }

        return value;
    }

    private void ApplyStateFromControls()
    {
        if (seedField != null)
        {
            seedText = SanitizeSeedInput(seedField.value);
            seedField.SetValueWithoutNotify(seedText);
        }

        if (obstacleSlider != null)
        {
            obstaclePercent = Mathf.Clamp(obstacleSlider.value, 0f, 0.85f);
        }

        if (limitMineSourceToggle != null)
        {
            limitMineSourceTiles = limitMineSourceToggle.value;
        }

        if (maxMineSourceSlider != null)
        {
            maxMineSourceTilesCap = Mathf.Clamp(maxMineSourceSlider.value, 0, GetMineSourceCapMax());
        }

        if (minMineSourceSlider != null)
        {
            minMineSourceTiles = Mathf.Clamp(minMineSourceSlider.value, 0, 30);
        }

        if (preserveNearestMineToggle != null)
        {
            preserveNearestMine = preserveNearestMineToggle.value;
        }

        if (guaranteeStarterToggle != null)
        {
            guaranteeStarterResources = guaranteeStarterToggle.value;
        }

        if (starterMinDistanceSlider != null)
        {
            starterMinDistance = Mathf.Clamp(starterMinDistanceSlider.value, 1, 8);
        }

        if (starterRadiusSlider != null)
        {
            starterRadius = Mathf.Clamp(starterRadiusSlider.value, 1, 12);
        }

        if (starterRadius < starterMinDistance)
        {
            starterRadius = starterMinDistance;
            if (starterRadiusSlider != null)
            {
                starterRadiusSlider.SetValueWithoutNotify(starterRadius);
            }
        }

        UpdateAllValueLabels();
        UpdateSectionVisibility();
    }

    private void SyncControlsFromState()
    {
        suppressUiCallbacks = true;
        seedText = NormalizeSeedText(seedText);

        if (seedField != null)
        {
            seedField.SetValueWithoutNotify(seedText ?? string.Empty);
        }

        if (obstacleSlider != null)
        {
            obstacleSlider.SetValueWithoutNotify(obstaclePercent);
        }

        if (limitMineSourceToggle != null)
        {
            limitMineSourceToggle.SetValueWithoutNotify(limitMineSourceTiles);
        }

        if (maxMineSourceSlider != null)
        {
            UpdateMineSourceSliderRange();
            maxMineSourceSlider.SetValueWithoutNotify(maxMineSourceTilesCap);
        }

        if (minMineSourceSlider != null)
        {
            minMineSourceSlider.SetValueWithoutNotify(minMineSourceTiles);
        }

        if (preserveNearestMineToggle != null)
        {
            preserveNearestMineToggle.SetValueWithoutNotify(preserveNearestMine);
        }

        if (guaranteeStarterToggle != null)
        {
            guaranteeStarterToggle.SetValueWithoutNotify(guaranteeStarterResources);
        }

        if (starterMinDistanceSlider != null)
        {
            starterMinDistanceSlider.SetValueWithoutNotify(starterMinDistance);
        }

        if (starterRadiusSlider != null)
        {
            starterRadiusSlider.SetValueWithoutNotify(starterRadius);
        }

        suppressUiCallbacks = false;

        UpdateAllValueLabels();
        UpdateSectionVisibility();
    }

    private void UpdateAllValueLabels()
    {
        UpdateObstacleLabel();
        UpdateMineSourceLabels();
        UpdateStarterLabels();
    }

    private void UpdateObstacleLabel()
    {
        if (obstacleLabel != null)
        {
            obstacleLabel.text = $"Obstacle Percent: {obstaclePercent:0.00}";
        }
    }

    private void UpdateMineSourceLabels()
    {
        if (maxMineSourceLabel != null)
        {
            maxMineSourceLabel.text = $"Max Mine Source Tiles: {maxMineSourceTilesCap}";
        }

        if (minMineSourceLabel != null)
        {
            minMineSourceLabel.text = $"Min Mine Source Tiles: {minMineSourceTiles}";
        }
    }

    private void UpdateStarterLabels()
    {
        if (starterMinDistanceLabel != null)
        {
            starterMinDistanceLabel.text = $"Starter Min Distance: {starterMinDistance}";
        }

        if (starterRadiusLabel != null)
        {
            int minRadius = Mathf.Max(1, starterMinDistance);
            starterRadiusLabel.text = $"Starter Radius: {starterRadius} (min {minRadius})";
        }
    }

    public static void QueueCurrentSettingsForNextGridMap()
    {
        if (instance == null)
        {
            return;
        }

        instance.CacheCurrentSettingsForPendingApply(instance.regenerateWhenApplyingPendingChanges);
        instance.pendingApplyWithoutGridMap = true;
    }

    public static void QueueCurrentSettingsForNextGridMapWithSeed(int seed)
    {
        if (instance == null)
        {
            return;
        }

        instance.CacheCurrentSettingsForPendingApply(instance.regenerateWhenApplyingPendingChanges);
        pendingGridSettings.seedText = seed.ToString();
        hasPendingGridSettings = true;
        instance.pendingApplyWithoutGridMap = true;
    }

    private void CacheCurrentSettingsForPendingApply(bool regenerateOnApply)
    {
        pendingGridSettings = new PendingGridSettings
        {
            obstaclePercent = obstaclePercent,
            limitMineSourceTiles = limitMineSourceTiles,
            maxMineSourceTilesCap = maxMineSourceTilesCap,
            minMineSourceTiles = minMineSourceTiles,
            guaranteeStarterResources = guaranteeStarterResources,
            starterMinDistance = starterMinDistance,
            starterRadius = starterRadius,
            preserveNearestMine = preserveNearestMine,
            seedText = seedText,
            regenerateOnApply = regenerateOnApply
        };

        hasPendingGridSettings = true;
    }

    private bool TryApplyPendingSettingsToGridMap()
    {
        if (!hasPendingGridSettings || gridMap == null || !autoApplyPendingChangesWhenGridMapAppears)
        {
            return false;
        }

        obstaclePercent = pendingGridSettings.obstaclePercent;
        limitMineSourceTiles = pendingGridSettings.limitMineSourceTiles;
        maxMineSourceTilesCap = pendingGridSettings.maxMineSourceTilesCap;
        minMineSourceTiles = pendingGridSettings.minMineSourceTiles;
        guaranteeStarterResources = pendingGridSettings.guaranteeStarterResources;
        starterMinDistance = pendingGridSettings.starterMinDistance;
        starterRadius = pendingGridSettings.starterRadius;
        preserveNearestMine = pendingGridSettings.preserveNearestMine;
        seedText = pendingGridSettings.seedText;

        PushToGridMap();
        if (pendingGridSettings.regenerateOnApply)
        {
            gridMap.GenerateLandMap();
        }

        hasPendingGridSettings = false;
        SyncControlsFromState();
        return true;
    }

    private void PushToGridMap()
    {
        if (gridMap == null)
        {
            return;
        }

        gridMap.ObstaclePercent = obstaclePercent;
        gridMap.LimitMineSourceTiles = limitMineSourceTiles;
        gridMap.MaxMineSourcePercent = ConvertMineSourceCapToPercent(maxMineSourceTilesCap);
        gridMap.MinMineSourceTiles = minMineSourceTiles;
        gridMap.GuaranteeStarterResourceNodes = guaranteeStarterResources;
        gridMap.StarterResourceMinDistanceFromCity = starterMinDistance;
        gridMap.StarterResourceRadius = starterRadius;
        gridMap.PreserveNearestMineSourceToCity = preserveNearestMine;

        if (TryParseSeed(seedText, out int parsedSeed))
        {
            gridMap.seed = parsedSeed;
            seedText = parsedSeed.ToString();
        }
        else
        {
            gridMap.seed = 0;
            seedText = "0";
        }
    }

    private static string SanitizeSeedInput(string rawInput)
    {
        if (string.IsNullOrEmpty(rawInput))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(rawInput.Length);
        bool canAddSign = true;
        for (int i = 0; i < rawInput.Length; i++)
        {
            char c = rawInput[i];
            if (c == '-' && canAddSign && builder.Length == 0)
            {
                builder.Append(c);
                canAddSign = false;
                continue;
            }

            if (char.IsDigit(c))
            {
                builder.Append(c);
                canAddSign = false;
            }
        }

        return builder.ToString();
    }

    private static bool TryParseSeed(string input, out int parsedSeed)
    {
        parsedSeed = 0;
        if (string.IsNullOrWhiteSpace(input) || input == "-")
        {
            return false;
        }

        if (!long.TryParse(input, out long longSeed))
        {
            return false;
        }

        if (longSeed > int.MaxValue)
        {
            parsedSeed = int.MaxValue;
            return true;
        }

        if (longSeed < int.MinValue)
        {
            parsedSeed = int.MinValue;
            return true;
        }

        parsedSeed = (int)longSeed;
        return true;
    }

    private void ApplyPreset(DifficultyPreset preset)
    {
        switch (preset)
        {
            case DifficultyPreset.Easy:
                obstaclePercent = 0.22f;
                limitMineSourceTiles = true;
                maxMineSourceTilesCap = 28;
                minMineSourceTiles = 8;
                guaranteeStarterResources = true;
                starterMinDistance = 1;
                starterRadius = 4;
                preserveNearestMine = true;
                break;

            case DifficultyPreset.Medium:
                obstaclePercent = 0.30f;
                limitMineSourceTiles = true;
                maxMineSourceTilesCap = 20;
                minMineSourceTiles = 6;
                guaranteeStarterResources = true;
                starterMinDistance = 2;
                starterRadius = 4;
                preserveNearestMine = true;
                break;

            case DifficultyPreset.Hard:
                obstaclePercent = 0.38f;
                limitMineSourceTiles = true;
                maxMineSourceTilesCap = 14;
                minMineSourceTiles = 4;
                guaranteeStarterResources = true;
                starterMinDistance = 2;
                starterRadius = 3;
                preserveNearestMine = true;
                break;

            case DifficultyPreset.Extreme:
                obstaclePercent = 0.48f;
                limitMineSourceTiles = true;
                maxMineSourceTilesCap = 8;
                minMineSourceTiles = 2;
                guaranteeStarterResources = true;
                starterMinDistance = 3;
                starterRadius = 3;
                preserveNearestMine = true;
                break;
        }
    }
}
