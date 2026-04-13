using GridGeneration;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InGameGenerationMenu : MonoBehaviour
{
    private enum DifficultyPreset
    {
        Easy,
        Medium,
        Hard,
        Extreme
    }

    [Header("References")]
    [SerializeField] private GridMap gridMap;

    [Header("Menu")]
    [SerializeField] private InputActionReference toggleMenuAction;
    [SerializeField] private bool showOnStart;
    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool blockGameplayInputWhileOpen = true;
    [SerializeField] private bool mainMenuOnlyUntilGridMapFound = true;
    [SerializeField] private bool showFloatingToggleButton = true;
    [SerializeField] private Vector2 floatingButtonSize = new Vector2(140f, 58f);
    [SerializeField] private bool autoFindGridMap = true;
    [SerializeField] private bool autoApplyPendingChangesWhenGridMapAppears = true;

    [Header("Typography")]
    [SerializeField, Range(14, 48)] private int windowTitleFontSize = 28;
    [SerializeField, Range(12, 42)] private int contentFontSize = 24;
    [SerializeField, Range(12, 48)] private int buttonFontSize = 24;
    [SerializeField, Range(12, 42)] private int floatingButtonFontSize = 22;
    [SerializeField, Range(24f, 84f)] private float controlHeight = 40f;

    [Header("Regeneration")]
    [SerializeField] private bool regenerateImmediatelyOnApply = true;
    [SerializeField] private bool regenerateWhenApplyingPendingChanges = true;

    private const int WindowId = 92741;
    private Rect windowRect = new Rect(16f, 120f, 640f, 560f);
    private Vector2 scrollPosition;
    private bool isOpen;
    private bool pendingApplyWithoutGridMap;
    private bool centerWindowOnNextDraw = true;

    private static InGameGenerationMenu instance;

    public static bool IsAnyMenuOpen { get; private set; }

    private float obstaclePercent = 0.30f;
    private bool limitMineSourceTiles = true;
    private float maxMineSourcePercent = 0.05f;
    private int minMineSourceTiles = 6;
    private bool guaranteeStarterResources = true;
    private int starterMinDistance = 2;
    private int starterRadius = 4;
    private bool preserveNearestMine = true;
    private string seedText = "0";

    private void Awake()
    {
        if (persistAcrossScenes)
        {
            if (instance != null && instance != this)
            {
                bool hostsEventSystem = GetComponent<EventSystem>() != null || GetComponent<BaseInputModule>() != null;
                if (hostsEventSystem)
                {
                    Destroy(this);
                }
                else
                {
                    Destroy(gameObject);
                }

                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

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
            PullFromGridMap();
        }
    }

    private void OnEnable()
    {
        if (toggleMenuAction != null && toggleMenuAction.action != null)
        {
            toggleMenuAction.action.Enable();
        }
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
        TryResolveGridMap();

        if (!CanDisplayMenu())
        {
            SetMenuOpenState(false);
            return;
        }

        bool shouldToggle = false;

        if (toggleMenuAction != null && toggleMenuAction.action != null && toggleMenuAction.action.WasPressedThisFrame())
        {
            shouldToggle = true;
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
    }

    private void OnGUI()
    {
        if (!CanDisplayMenu())
        {
            return;
        }

        DrawFloatingToggleButton();

        if (!isOpen)
        {
            return;
        }

        float maxWidth = Mathf.Max(360f, Screen.width - 24f);
        float maxHeight = Mathf.Max(300f, Screen.height - 24f);
        windowRect.width = Mathf.Min(windowRect.width, maxWidth);
        windowRect.height = Mathf.Min(windowRect.height, maxHeight);

        if (centerWindowOnNextDraw)
        {
            windowRect.x = (Screen.width - windowRect.width) * 0.5f;
            windowRect.y = (Screen.height - windowRect.height) * 0.5f;
            centerWindowOnNextDraw = false;
        }

        windowRect.x = Mathf.Clamp(windowRect.x, 0f, Screen.width - windowRect.width);
        windowRect.y = Mathf.Clamp(windowRect.y, 0f, Screen.height - windowRect.height);

        GUIStyle windowStyle = new GUIStyle(GUI.skin.window)
        {
            fontSize = windowTitleFontSize
        };

        windowRect = GUI.Window(WindowId, windowRect, DrawWindow, "World Generation", windowStyle);
    }

    private void ToggleMenu()
    {
        if (!CanDisplayMenu())
        {
            SetMenuOpenState(false);
            return;
        }

        SetMenuOpenState(!isOpen);
        if (isOpen)
        {
            centerWindowOnNextDraw = true;
            if (gridMap != null)
            {
                PullFromGridMap();
            }
        }
    }

    private void SetMenuOpenState(bool open)
    {
        isOpen = open;
        IsAnyMenuOpen = blockGameplayInputWhileOpen && isOpen;
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

    private void DrawFloatingToggleButton()
    {
        if (!showFloatingToggleButton)
        {
            return;
        }

        float buttonWidth = Mathf.Clamp(floatingButtonSize.x, 100f, 280f);
        float buttonHeight = Mathf.Clamp(floatingButtonSize.y, 44f, 120f);
        Rect buttonRect = new Rect(
            0f,
            Screen.height - buttonHeight,
            buttonWidth,
            buttonHeight);

        GUIStyle floatingButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = floatingButtonFontSize
        };

        string buttonLabel = isOpen ? "Close Menu" : "Gen Menu";
        if (GUI.Button(buttonRect, buttonLabel, floatingButtonStyle))
        {
            ToggleMenu();
        }
    }

    private void DrawWindow(int id)
    {
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = contentFontSize
        };

        GUIStyle sectionStyle = new GUIStyle(labelStyle)
        {
            fontStyle = FontStyle.Bold,
            fontSize = contentFontSize + 2
        };

        GUIStyle toggleStyle = new GUIStyle(GUI.skin.toggle)
        {
            fontSize = contentFontSize
        };

        GUIStyle textFieldStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize = contentFontSize
        };

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = buttonFontSize
        };

        float clampedControlHeight = Mathf.Clamp(controlHeight, 24f, 100f);
        GUILayoutOption[] controlHeightOption = { GUILayout.Height(clampedControlHeight) };

        Rect contentRect = new Rect(8f, 26f, windowRect.width - 16f, windowRect.height - 34f);
        GUILayout.BeginArea(contentRect);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true, GUILayout.ExpandHeight(true));

        GUILayout.Label("Seed", sectionStyle);
        seedText = GUILayout.TextField(seedText ?? string.Empty, textFieldStyle, controlHeightOption);

        GUILayout.Space(6f);
        GUILayout.Label($"Obstacle Percent: {obstaclePercent:0.00}", labelStyle);
        obstaclePercent = GUILayout.HorizontalSlider(obstaclePercent, 0f, 0.85f);

        GUILayout.Space(6f);
        limitMineSourceTiles = GUILayout.Toggle(limitMineSourceTiles, "Limit Mine Sources", toggleStyle, controlHeightOption);
        if (limitMineSourceTiles)
        {
            GUILayout.Label($"Max Mine Source Percent: {maxMineSourcePercent:0.000}", labelStyle);
            maxMineSourcePercent = GUILayout.HorizontalSlider(maxMineSourcePercent, 0f, 0.15f);

            GUILayout.Label($"Min Mine Source Tiles: {minMineSourceTiles}", labelStyle);
            minMineSourceTiles = Mathf.RoundToInt(GUILayout.HorizontalSlider(minMineSourceTiles, 0f, 30f));
            preserveNearestMine = GUILayout.Toggle(preserveNearestMine, "Preserve nearest mine to city", toggleStyle, controlHeightOption);
        }

        GUILayout.Space(6f);
        guaranteeStarterResources = GUILayout.Toggle(guaranteeStarterResources, "Guarantee Starter Resource Nodes", toggleStyle, controlHeightOption);
        if (guaranteeStarterResources)
        {
            GUILayout.Label($"Starter Min Distance: {starterMinDistance}", labelStyle);
            starterMinDistance = Mathf.RoundToInt(GUILayout.HorizontalSlider(starterMinDistance, 1f, 8f));

            int minRadius = Mathf.Max(1, starterMinDistance);
            starterRadius = Mathf.Max(minRadius, starterRadius);
            GUILayout.Label($"Starter Radius: {starterRadius}", labelStyle);
            starterRadius = Mathf.RoundToInt(GUILayout.HorizontalSlider(starterRadius, minRadius, 12f));
        }

        GUILayout.Space(10f);
        GUILayout.Label("Difficulty Presets", sectionStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Easy", buttonStyle, controlHeightOption)) ApplyPreset(DifficultyPreset.Easy);
        if (GUILayout.Button("Medium", buttonStyle, controlHeightOption)) ApplyPreset(DifficultyPreset.Medium);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Hard", buttonStyle, controlHeightOption)) ApplyPreset(DifficultyPreset.Hard);
        if (GUILayout.Button("Extreme", buttonStyle, controlHeightOption)) ApplyPreset(DifficultyPreset.Extreme);
        GUILayout.EndHorizontal();

        GUILayout.Space(12f);
        if (GUILayout.Button("Apply", buttonStyle, controlHeightOption))
        {
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
                pendingApplyWithoutGridMap = true;
            }
        }

        if (GUILayout.Button("Apply + New Seed", buttonStyle, controlHeightOption))
        {
            seedText = Random.Range(int.MinValue, int.MaxValue).ToString();

            if (gridMap != null)
            {
                PushToGridMap();
                gridMap.GenerateLandMap();
            }
            else
            {
                pendingApplyWithoutGridMap = true;
            }
        }

        if (GUILayout.Button("Close", buttonStyle, controlHeightOption))
        {
            SetMenuOpenState(false);
        }

        GUILayout.Space(8f);
        GUILayout.Label("Toggle via action/F2 or bottom-left button", labelStyle);

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    private void PullFromGridMap()
    {
        if (gridMap == null)
        {
            return;
        }

        obstaclePercent = gridMap.ObstaclePercent;
        limitMineSourceTiles = gridMap.LimitMineSourceTiles;
        maxMineSourcePercent = gridMap.MaxMineSourcePercent;
        minMineSourceTiles = gridMap.MinMineSourceTiles;
        guaranteeStarterResources = gridMap.GuaranteeStarterResourceNodes;
        starterMinDistance = gridMap.StarterResourceMinDistanceFromCity;
        starterRadius = gridMap.StarterResourceRadius;
        preserveNearestMine = gridMap.PreserveNearestMineSourceToCity;
        seedText = gridMap.seed.ToString();
    }

    private void PushToGridMap()
    {
        if (gridMap == null)
        {
            return;
        }

        gridMap.ObstaclePercent = obstaclePercent;
        gridMap.LimitMineSourceTiles = limitMineSourceTiles;
        gridMap.MaxMineSourcePercent = maxMineSourcePercent;
        gridMap.MinMineSourceTiles = minMineSourceTiles;
        gridMap.GuaranteeStarterResourceNodes = guaranteeStarterResources;
        gridMap.StarterResourceMinDistanceFromCity = starterMinDistance;
        gridMap.StarterResourceRadius = starterRadius;
        gridMap.PreserveNearestMineSourceToCity = preserveNearestMine;

        if (int.TryParse(seedText, out int parsedSeed))
        {
            gridMap.seed = parsedSeed;
        }
    }

    private void ApplyPreset(DifficultyPreset preset)
    {
        switch (preset)
        {
            case DifficultyPreset.Easy:
                obstaclePercent = 0.22f;
                limitMineSourceTiles = true;
                maxMineSourcePercent = 0.07f;
                minMineSourceTiles = 8;
                guaranteeStarterResources = true;
                starterMinDistance = 1;
                starterRadius = 4;
                preserveNearestMine = true;
                break;

            case DifficultyPreset.Medium:
                obstaclePercent = 0.30f;
                limitMineSourceTiles = true;
                maxMineSourcePercent = 0.05f;
                minMineSourceTiles = 6;
                guaranteeStarterResources = true;
                starterMinDistance = 2;
                starterRadius = 4;
                preserveNearestMine = true;
                break;

            case DifficultyPreset.Hard:
                obstaclePercent = 0.38f;
                limitMineSourceTiles = true;
                maxMineSourcePercent = 0.035f;
                minMineSourceTiles = 4;
                guaranteeStarterResources = true;
                starterMinDistance = 2;
                starterRadius = 3;
                preserveNearestMine = true;
                break;

            case DifficultyPreset.Extreme:
                obstaclePercent = 0.48f;
                limitMineSourceTiles = true;
                maxMineSourcePercent = 0.02f;
                minMineSourceTiles = 2;
                guaranteeStarterResources = true;
                starterMinDistance = 3;
                starterRadius = 3;
                preserveNearestMine = true;
                break;
        }
    }
}
