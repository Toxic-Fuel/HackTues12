using UnityEngine;
using UnityEngine.UIElements;

public class ResourceTurnsUIDocument : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private Turns turns;

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
    [SerializeField, Min(0f)] private float portraitTopInsetPixels = 20f;

    [Header("Label Names")]
    [SerializeField] private string turnsLabelName = "turns-value";
    [SerializeField] private string[] currentResourceLabelNames;
    [SerializeField] private string[] perTurnResourceLabelNames;

    [Header("Control Names")]
    [SerializeField] private string skipTurnButtonName = "skip-turn-button";

    private Label turnsLabel;
    private Label[] currentResourceLabels;
    private Label[] perTurnResourceLabels;
    private Button skipTurnButton;
    private VisualElement hudRoot;
    private int cachedScreenWidth = -1;
    private int cachedScreenHeight = -1;

    private void Awake()
    {
        CacheElements();
    }

    private void OnEnable()
    {
        CacheElements();
        ApplyPanelScale(force: true);
        BindControls();
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
        hudRoot = string.IsNullOrWhiteSpace(hudRootName) ? root : root.Q<VisualElement>(hudRootName) ?? root;
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

        hudRoot.style.position = Position.Absolute;
        hudRoot.style.left = 0f;
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
