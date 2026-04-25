using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

public class ResourceTurnsUIDocument : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private Turns turns;
    [SerializeField] private VillageCrisisSystem crisisSystem;

    [Header("Button Events")]
    [SerializeField] private UnityEvent OnButtonClicked;

    [Header("Panel Scale")]
    [SerializeField] private string hudRootName = "hud-root";
    [SerializeField, Min(0.1f)] private float panelScale = 0.72f;
    [SerializeField, Min(0.1f)] private float manualScaleMultiplier = 0.8f;
    [SerializeField] private bool autoScaleByShortSide = true;
    [SerializeField, Min(320f)] private float referenceShortSidePixels = 1080f;
    [SerializeField, Min(0.1f)] private float minAutoScale = 0.55f;
    [SerializeField, Min(0.1f)] private float maxAutoScale = 1.2f;
    [SerializeField] private bool reduceScaleOnPortraitScreens = true;
    [SerializeField, Min(320f)] private float portraitMaxScreenWidthPixels = 1300f;
    [SerializeField, Range(0.4f, 1f)] private float portraitScaleMultiplier = 0.7f;
    [SerializeField, Range(0.3f, 1f)] private float portraitMinEffectiveScale = 0.55f;
    [SerializeField, Range(0.3f, 1f)] private float portraitMaxEffectiveScale = 0.7f;
    [SerializeField, Min(0f)] private float portraitTopInsetPixels = 30f;
    [SerializeField, Min(0f)] private float portraitLeftInsetPixels = 24f;
    [SerializeField] private bool includeSafeAreaLeftInsetOnPortrait = true;

    [Header("Label Names")]
    [SerializeField] private string turnsLabelName = "turns-value";
    [SerializeField] private string[] currentResourceLabelNames;
    [SerializeField] private string[] perTurnResourceLabelNames;

    [Header("Health Bar")]
    [SerializeField] private string healthBarName = "health-bar";
    [SerializeField] private string healthBarFillName = "health-bar-fill";
    [SerializeField] private string healthCountLabelName = "health-count";

    [Header("Control Names")]
    [SerializeField] private string skipTurnButtonName = "skip-turn-button";

    private Label turnsLabel;
    private Label[] currentResourceLabels;
    private Label[] perTurnResourceLabels;
    private Button skipTurnButton;
    private VisualElement healthBar;
    private VisualElement healthBarFill;
    private Label healthCountLabel;
    private VisualElement hudRoot;
    private int cachedScreenWidth = -1;
    private int cachedScreenHeight = -1;

    private void Awake()
    {
        OnButtonClicked ??= new UnityEvent();
        CacheElements();
    }

    private void OnEnable()
    {
        CacheElements();
        EnsureCrisisSystemReference();
        SubscribeToCrisisSystem();
        ApplyPanelScale(force: true);
        BindControls();
        UpdateHealthBar();
    }

    private void Update()
    {
        if (!autoScaleByShortSide)
        {
            return;
        }

        if (Screen.width == cachedScreenWidth && Screen.height == cachedScreenHeight)
        {
            return;
        }

        ApplyPanelScale(force: false);
    }

    private void OnDisable()
    {
        UnbindControls();
        UnsubscribeFromCrisisSystem();
    }

    public void UpdateTexts(int[] currentResources, int[] resourcesPerTurn, int remainingTurns)
    {
        if (uiDocument == null)
        {
            return;
        }

        if (turnsLabel == null)
        {
            CacheElements();
        }

        if (turnsLabel != null)
        {
            turnsLabel.text = remainingTurns.ToString();
        }

        if (currentResources != null && currentResourceLabels != null)
        {
            for (int i = 0; i < currentResourceLabels.Length; i++)
            {
                if (currentResourceLabels[i] == null || i >= currentResources.Length)
                {
                    continue;
                }

                currentResourceLabels[i].text = currentResources[i].ToString();
            }
        }

        if (resourcesPerTurn != null && perTurnResourceLabels != null)
        {
            for (int i = 0; i < perTurnResourceLabels.Length; i++)
            {
                if (perTurnResourceLabels[i] == null || i >= resourcesPerTurn.Length)
                {
                    continue;
                }

                perTurnResourceLabels[i].text = $"+{resourcesPerTurn[i]}/t";
            }
        }

        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (healthBar == null || healthBarFill == null)
        {
            CacheElements();
        }

        if (healthBar == null || healthBarFill == null)
        {
            return;
        }

        int currentHealth = crisisSystem != null ? Mathf.Clamp(crisisSystem.Stability, 0, 100) : 0;
        if (healthCountLabel != null)
        {
            healthCountLabel.text = $"{currentHealth}/100";
        }

        healthBarFill.style.width = Length.Percent(currentHealth);
    }

    private void CacheElements()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                return;
            }
        }

        VisualElement root = uiDocument.rootVisualElement;
        if (root == null)
        {
            return;
        }

        turnsLabel = string.IsNullOrWhiteSpace(turnsLabelName) ? null : root.Q<Label>(turnsLabelName);

        currentResourceLabels = ResolveLabels(root, currentResourceLabelNames);
        perTurnResourceLabels = ResolveLabels(root, perTurnResourceLabelNames);
        skipTurnButton = string.IsNullOrWhiteSpace(skipTurnButtonName) ? null : root.Q<Button>(skipTurnButtonName);
        healthBar = string.IsNullOrWhiteSpace(healthBarName) ? null : root.Q<VisualElement>(healthBarName);
        healthBarFill = string.IsNullOrWhiteSpace(healthBarFillName) ? null : root.Q<VisualElement>(healthBarFillName);
        healthCountLabel = string.IsNullOrWhiteSpace(healthCountLabelName) ? null : root.Q<Label>(healthCountLabelName);
        hudRoot = string.IsNullOrWhiteSpace(hudRootName) ? root : root.Q<VisualElement>(hudRootName) ?? root;
    }

    private void EnsureCrisisSystemReference()
    {
        if (crisisSystem == null)
        {
            crisisSystem = FindAnyObjectByType<VillageCrisisSystem>();
        }
    }

    private void SubscribeToCrisisSystem()
    {
        if (crisisSystem == null)
        {
            return;
        }

        crisisSystem.CrisisStateChanged -= OnCrisisStateChanged;
        crisisSystem.CrisisStateChanged += OnCrisisStateChanged;
    }

    private void UnsubscribeFromCrisisSystem()
    {
        if (crisisSystem == null)
        {
            return;
        }

        crisisSystem.CrisisStateChanged -= OnCrisisStateChanged;
    }

    private void OnCrisisStateChanged(VillageCrisisSystem _)
    {
        UpdateHealthBar();
    }

    private void OnValidate()
    {
        panelScale = Mathf.Max(0.1f, panelScale);
        manualScaleMultiplier = Mathf.Max(0.1f, manualScaleMultiplier);
        referenceShortSidePixels = Mathf.Max(320f, referenceShortSidePixels);
        minAutoScale = Mathf.Max(0.1f, minAutoScale);
        maxAutoScale = Mathf.Max(minAutoScale, maxAutoScale);
        portraitMaxScreenWidthPixels = Mathf.Max(320f, portraitMaxScreenWidthPixels);
        portraitScaleMultiplier = Mathf.Clamp(portraitScaleMultiplier, 0.4f, 1f);
        portraitMinEffectiveScale = Mathf.Clamp(portraitMinEffectiveScale, 0.3f, 1f);
        portraitMaxEffectiveScale = Mathf.Clamp(portraitMaxEffectiveScale, 0.3f, 1f);
        if (portraitMaxEffectiveScale < portraitMinEffectiveScale)
        {
            portraitMaxEffectiveScale = portraitMinEffectiveScale;
        }
        portraitTopInsetPixels = Mathf.Max(0f, portraitTopInsetPixels);
        portraitLeftInsetPixels = Mathf.Max(0f, portraitLeftInsetPixels);

        if (!Application.isPlaying)
        {
            return;
        }

        CacheElements();
        ApplyPanelScale(force: true);
    }

    private void ApplyPanelScale(bool force)
    {
        if (hudRoot == null)
        {
            return;
        }

        if (!force && Screen.width == cachedScreenWidth && Screen.height == cachedScreenHeight)
        {
            return;
        }

        cachedScreenWidth = Screen.width;
        cachedScreenHeight = Screen.height;

        float autoScale = 1f;
        if (autoScaleByShortSide)
        {
            float shortSide = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height));
            float reference = Mathf.Max(1f, referenceShortSidePixels);
            autoScale = Mathf.Clamp(shortSide / reference, minAutoScale, maxAutoScale);
        }

        float effectiveScale = panelScale * manualScaleMultiplier * autoScale;
        bool isPortraitPhoneScreen = Screen.height > Screen.width
            && Screen.width <= portraitMaxScreenWidthPixels;

        if (reduceScaleOnPortraitScreens
            && isPortraitPhoneScreen)
        {
            effectiveScale *= portraitScaleMultiplier;
            float minScale = Mathf.Min(portraitMinEffectiveScale, portraitMaxEffectiveScale);
            float maxScale = Mathf.Max(portraitMinEffectiveScale, portraitMaxEffectiveScale);
            effectiveScale = Mathf.Clamp(effectiveScale, minScale, maxScale);
        }

        float leftInset = 0f;
        if (isPortraitPhoneScreen)
        {
            leftInset = portraitLeftInsetPixels;
            if (includeSafeAreaLeftInsetOnPortrait)
            {
                leftInset += Mathf.Max(0f, Screen.safeArea.xMin);
            }
        }

        hudRoot.style.position = Position.Absolute;
        hudRoot.style.left = leftInset;
        hudRoot.style.top = isPortraitPhoneScreen ? portraitTopInsetPixels : 0f;
        hudRoot.style.transformOrigin = new TransformOrigin(
            new Length(0f, LengthUnit.Percent),
            new Length(0f, LengthUnit.Percent),
            0f
        );
        hudRoot.style.scale = new Scale(new Vector3(effectiveScale, effectiveScale, 1f));
    }

    private void BindControls()
    {
        if (skipTurnButton == null)
        {
            return;
        }

        skipTurnButton.clicked -= OnSkipTurnClicked;
        skipTurnButton.clicked += OnSkipTurnClicked;
    }

    private void UnbindControls()
    {
        if (skipTurnButton == null)
        {
            return;
        }

        skipTurnButton.clicked -= OnSkipTurnClicked;
    }

    private void OnSkipTurnClicked()
    {
        EnsureTurnsReference();
        if (turns == null)
        {
            return;
        }

        turns.EndTurn();
        OnButtonClicked?.Invoke();
    }

    private void EnsureTurnsReference()
    {
        if (turns == null)
        {
            turns = FindAnyObjectByType<Turns>();
        }
    }

    private static Label[] ResolveLabels(VisualElement root, string[] names)
    {
        if (names == null)
        {
            return new Label[0];
        }

        Label[] labels = new Label[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(names[i]))
            {
                labels[i] = null;
                continue;
            }

            labels[i] = root.Q<Label>(names[i]);
        }

        return labels;
    }
}
