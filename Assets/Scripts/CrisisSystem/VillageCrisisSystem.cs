using System;
using System.Collections.Generic;
using GridGeneration;
using ScoreSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class VillageCrisisSystem : MonoBehaviour
{
    [Serializable]
    private sealed class VillageCrisisState
    {
        public Vector2Int coordinate;
        public VillageCrisisType type;
        public int severity;
        public int ageTurns;
        public int announcedSeverityStage;
        public bool isActive;
        public GameObject marker;
        public Vector3 markerBaseScale;
        public SpriteRenderer selectionOutline;
    }

    [Header("References")]
    [SerializeField] private GridMap gridMap;
    [SerializeField] private Turns turns;
    [SerializeField] private TileBuilding tileBuilding;
    [SerializeField] private LevelScore levelScore;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference previousCrisisAction;
    [SerializeField] private InputActionReference nextCrisisAction;
    [SerializeField] private InputActionReference respondCrisisAction;

    [Header("Crisis Flow")]
    [SerializeField, Min(1)] private int maxActiveCrises = 4;
    [SerializeField, Range(0f, 1f)] private float baseSpawnChance = 0.28f;
    [SerializeField, Min(1)] private int guaranteedSpawnIntervalTurns = 4;
    [SerializeField, Min(1)] private int severityGainPerTurnMin = 2;
    [SerializeField, Min(1)] private int severityGainPerTurnMax = 8;
    [SerializeField, Range(0f, 1f)] private float spreadChance = 0.08f;
    [SerializeField, Min(1)] private int criticalSeverityThreshold = 90;
    [SerializeField] private bool deterministicPerMapSeed = true;
    [SerializeField] private int crisisSeedOffset = 9173;
    [SerializeField, Min(0)] private int postResolveSpawnGraceTurns = 2;
    [SerializeField, Min(0)] private int postResolveSpreadGraceTurns = 1;

    [Header("Severity Milestones")]
    [SerializeField, Range(1, 99)] private int warningSeverityThreshold = 55;
    [SerializeField, Range(1, 99)] private int dangerSeverityThreshold = 75;
    [SerializeField] private bool showSeverityMilestoneAlerts = true;

    [Header("Player Response")]
    [SerializeField, Min(1)] private int responseActionCost = 1;
    [SerializeField, Min(1)] private int baseResponsePower = 50;
    [SerializeField, Min(0)] private int extraResponsePowerWhenConnected = 20;
    [SerializeField, Range(0.1f, 1f)] private float disconnectedResponsePowerMultiplier = 0.75f;
    [SerializeField, Min(0)] private int disconnectedResolveFloor = 24;
    [SerializeField, Min(0)] private int connectedResolveStabilityBonus = 4;

    [Header("Infrastructure Pressure")]
    [SerializeField] private bool increaseBuildCostsDuringInfrastructureCrisis = true;
    [SerializeField, Range(0f, 0.5f)] private float infrastructureCostStepPerActiveCrisis = 0.1f;
    [SerializeField, Range(0f, 0.5f)] private float infrastructureCostStepPerCriticalInfrastructureCrisis = 0.1f;
    [SerializeField, Range(1f, 3f)] private float maxInfrastructureBuildCostMultiplier = 1.7f;

    [Header("Stability")]
    [SerializeField, Range(0, 100)] private int startingStability = 100;
    [SerializeField, Min(0)] private int passiveStabilityDrain = 1;
    [SerializeField, Min(0)] private int criticalCrisisStabilityDrain = 4;
    [SerializeField, Min(1)] private int resolveStabilityReward = 8;
    [SerializeField, Min(0)] private int stabilityTurnPenaltyOnCritical;

    [Header("Victory Gate")]
    [SerializeField] private bool requireNoCriticalCrisesForVictory = true;

    [Header("Markers")]
    [SerializeField] private GameObject crisisMarkerPrefab;
    [SerializeField] private Texture2D crisisMarkerTexture;
    [SerializeField] private bool preferTextureMarkerOverPrefab = true;
    [SerializeField, Min(0.1f)] private float textureMarkerWorldSize = 1.2f;
    [SerializeField] private int textureMarkerSortingOrder = 25;
    [SerializeField] private Vector3 markerOffset = new Vector3(0f, 1.7f, 0f);
    [SerializeField, Min(0.01f)] private float prefabMarkerScaleMultiplier = 0.20f;
    [SerializeField] private bool pulseMarkers = true;
    [SerializeField, Min(0.1f)] private float markerPulseSpeed = 3f;
    [SerializeField, Range(0f, 0.4f)] private float markerPulseAmplitude = 0.12f;
    [SerializeField, Min(1f)] private float selectedMarkerScaleBoost = 1.15f;
    [SerializeField] private Color selectedMarkerOutlineColor = new Color(0.2f, 0.95f, 1f, 0.95f);
    [SerializeField, Range(1.01f, 2f)] private float selectedMarkerOutlineScale = 1.14f;
    [SerializeField] private int selectedMarkerOutlineOrderOffset = -1;

    [Header("Debug")]
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private bool logImportantEvents = true;
    [SerializeField] private bool showRuleHints = false;
    [SerializeField] private bool compactOverlay = true;
    [SerializeField] private bool startInMiniMode = true;

    [Header("Overlay UI Toolkit")]
    [SerializeField] private UIDocument overlayDocument;
    [SerializeField] private PanelSettings panelSettings;
    [SerializeField] private VisualTreeAsset overlayLayoutAsset;
    [SerializeField] private StyleSheet overlayStyleSheet;
    [SerializeField] private bool createOverlayDocumentIfMissing = true;

    [Header("Overlay Scaling")]
    [SerializeField] private bool scaleWithScreenShortSide = true;
    [SerializeField, Min(320f)] private float referenceShortSidePixels = 720f;
    [SerializeField, Range(0.65f, 1.25f)] private float compactScaleThreshold = 0.72f;
    [SerializeField, Range(1.0f, 1.8f)] private float largeScaleThreshold = 1.28f;
    [SerializeField, Range(0.8f, 1.5f)] private float baseScaleBoost = 1.05f;
    [SerializeField, Range(0.7f, 1.2f)] private float globalOverlayScaleMultiplier = 0.80f;
    [SerializeField, Min(0.5f)] private float minOverlayVisualScale = 1f;
    [SerializeField, Min(0.5f)] private float maxOverlayVisualScale = 1.6f;
    [SerializeField] private bool usePhonePortraitLayout = true;
    [SerializeField, Min(320)] private int phonePortraitMaxScreenWidth = 1300;
    [SerializeField, Range(0.4f, 1f)] private float phonePortraitScaleMultiplier = 0.88f;
    [SerializeField, Min(180f)] private float phonePortraitFixedPanelWidth = 340f;
    [SerializeField, Min(0f)] private float phonePortraitTopOffset = 118f;
    [SerializeField, Min(0f)] private float phonePortraitSafeAreaTopInset = 38f;
    [SerializeField, Min(0f)] private float phonePortraitUpwardShift = 72f;

    [Header("Overlay Stacking")]
    [SerializeField] private bool stackBelowResourceHudOnNarrowScreens = false;
    [SerializeField, Min(320)] private int stackBelowResourceHudMaxScreenWidth = 1500;
    [SerializeField, Min(0f)] private float stackedMenuGapPixels = 8f;
    [SerializeField, Min(0f)] private float fallbackStackTopOffset = 140f;

    [Header("Overlay Messages")]
    [SerializeField, Min(1f)] private float responseStatusDuration = 3f;

    [Header("Balance Safety")]
    [SerializeField] private bool forceDemoFriendlyBalance = true;

    private readonly List<Vector2Int> _allVillages = new List<Vector2Int>();
    private readonly List<VillageCrisisState> _activeCrises = new List<VillageCrisisState>();
    private readonly HashSet<Vector2Int> _reachableFromCity = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> _connectedRoadNetwork = new HashSet<Vector2Int>();

    private Vector2Int _cityCoordinate = new Vector2Int(-1, -1);
    private int _selectedCrisisIndex;
    private int _stability;
    private bool _initialized;
    private int _turnsUntilGuaranteedSpawn;
    private int _spawnGraceTurnsRemaining;
    private int _spreadGraceTurnsRemaining;
    private int _lastComputedStabilityDrain;
    private string _responseStatusMessage = "No action yet.";
    private float _responseStatusTimestamp;
    private System.Random _crisisRng;
    private int _crisisRngSeed;
    private bool _overlayUiInitialized;
    private int _lastScreenWidth = -1;
    private int _lastScreenHeight = -1;
    private string _activeScaleClass = string.Empty;
    private bool _isPhonePortraitLayout;
    private UIDocument _resourceHudDocument;
    private VisualElement _resourceHudRoot;
    private Sprite _cachedTextureMarkerSprite;

    private VisualElement _overlayRoot;
    private VisualElement _overlayContainer;
    private VisualElement _overlayPanel;
    private Label _titleLabel;
    private Label _summaryLabel;
    private Label _gateLabel;
    private Label _spawnRuleLabel;
    private Label _selectedLabel;
    private Label _typeEffectLabel;
    private Label _woodCostLabel;
    private Label _stoneCostLabel;
    private Label _actionCostLabel;
    private VisualElement _costRow;
    private Label _selectedHintLabel;
    private Label _resolveHintLabel;
    private Label _controlsLabel;
    private Label _pressureLabel;
    private Button _prevButton;
    private Button _respondButton;
    private Button _nextButton;
    private Button _collapseButton;
    private VisualElement _detailsTop;
    private VisualElement _detailsMiddle;
    private VisualElement _detailsBottom;
    private bool _isMiniMode;

    public int Stability => _stability;
    public int ActiveCrisisCount => _activeCrises.Count;
    public int CriticalCrisisCount => CountCriticalCrises();

    public event Action<VillageCrisisSystem> CrisisStateChanged;

    private void Awake()
    {
        if (forceDemoFriendlyBalance)
        {
            ApplyDemoFriendlyBalancePreset();
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

        if (levelScore == null)
        {
            levelScore = FindAnyObjectByType<LevelScore>();
        }

        _stability = Mathf.Clamp(startingStability, 0, 100);
    }

    private void OnEnable()
    {
        if (gridMap != null)
        {
            gridMap.MapGenerated -= OnMapGenerated;
            gridMap.MapGenerated += OnMapGenerated;
        }

        if (turns != null)
        {
            turns.TurnStarted -= OnTurnStarted;
            turns.TurnStarted += OnTurnStarted;
        }

        RegisterAction(previousCrisisAction, OnPreviousCrisisPerformed);
        RegisterAction(nextCrisisAction, OnNextCrisisPerformed);
        RegisterAction(respondCrisisAction, OnRespondPerformed);

        EnsureOverlayInitialized();
        SetOverlayVisible(showOverlay);
    }

    private void Start()
    {
        InitializeCrisisMap();
        RefreshOverlayText();
    }

    private void OnDisable()
    {
        if (gridMap != null)
        {
            gridMap.MapGenerated -= OnMapGenerated;
        }

        if (turns != null)
        {
            turns.TurnStarted -= OnTurnStarted;
        }

        UnregisterAction(previousCrisisAction, OnPreviousCrisisPerformed);
        UnregisterAction(nextCrisisAction, OnNextCrisisPerformed);
        UnregisterAction(respondCrisisAction, OnRespondPerformed);
    }

    private void Update()
    {
        EnsureOverlayInitialized();
        ApplyResponsiveOverlay(force: false);

        if (!pulseMarkers || _activeCrises.Count == 0)
        {
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.time * markerPulseSpeed) * markerPulseAmplitude;
        int selectedIndex = Mathf.Clamp(_selectedCrisisIndex, 0, _activeCrises.Count - 1);
        float selectedBoost = Mathf.Max(1f, selectedMarkerScaleBoost);
        for (int i = 0; i < _activeCrises.Count; i++)
        {
            VillageCrisisState crisis = _activeCrises[i];
            if (crisis.marker == null)
            {
                continue;
            }

            float scale = pulse;
            if (i == selectedIndex)
            {
                scale *= selectedBoost;
            }

            crisis.marker.transform.localScale = crisis.markerBaseScale * scale;
        }
    }

    public bool CanDeclareVictory()
    {
        if (!requireNoCriticalCrisesForVictory)
        {
            return true;
        }

        return CountCriticalCrises() == 0;
    }

    public float GetInfrastructureBuildCostMultiplier()
    {
        if (!increaseBuildCostsDuringInfrastructureCrisis)
        {
            return 1f;
        }

        int activeInfrastructureCount = 0;
        int criticalInfrastructureCount = 0;

        for (int i = 0; i < _activeCrises.Count; i++)
        {
            VillageCrisisState crisis = _activeCrises[i];
            if (crisis == null || !crisis.isActive || crisis.type != VillageCrisisType.Infrastructure)
            {
                continue;
            }

            activeInfrastructureCount++;
            if (crisis.severity >= criticalSeverityThreshold)
            {
                criticalInfrastructureCount++;
            }
        }

        if (activeInfrastructureCount <= 0)
        {
            return 1f;
        }

        float multiplier = 1f
            + activeInfrastructureCount * Mathf.Max(0f, infrastructureCostStepPerActiveCrisis)
            + criticalInfrastructureCount * Mathf.Max(0f, infrastructureCostStepPerCriticalInfrastructureCrisis);

        return Mathf.Clamp(multiplier, 1f, Mathf.Max(1f, maxInfrastructureBuildCostMultiplier));
    }

    public int[] GetAdjustedBuildCost(int[] baseCost, Building building)
    {
        int length = baseCost != null ? baseCost.Length : 0;
        int[] adjustedCost = new int[length];
        if (baseCost == null || length == 0)
        {
            return adjustedCost;
        }

        Array.Copy(baseCost, adjustedCost, length);

        if (building == Building.None)
        {
            return adjustedCost;
        }

        float multiplier = GetInfrastructureBuildCostMultiplier();
        if (multiplier <= 1f)
        {
            return adjustedCost;
        }

        int woodIndex = (int)ResourceType.Wood;
        int stoneIndex = (int)ResourceType.Stone;

        if (woodIndex >= 0 && woodIndex < adjustedCost.Length && adjustedCost[woodIndex] > 0)
        {
            adjustedCost[woodIndex] = Mathf.CeilToInt(adjustedCost[woodIndex] * multiplier);
        }

        if (stoneIndex >= 0 && stoneIndex < adjustedCost.Length && adjustedCost[stoneIndex] > 0)
        {
            adjustedCost[stoneIndex] = Mathf.CeilToInt(adjustedCost[stoneIndex] * multiplier);
        }

        return adjustedCost;
    }

    public bool TryRespondToSelectedCrisis()
    {
        if (!_initialized || turns == null || _activeCrises.Count == 0)
        {
            SetResponseStatus("No active crisis to respond to.");
            RefreshOverlayText();
            return false;
        }

        if (_selectedCrisisIndex < 0 || _selectedCrisisIndex >= _activeCrises.Count)
        {
            _selectedCrisisIndex = 0;
        }

        VillageCrisisState crisis = _activeCrises[_selectedCrisisIndex];
        if (crisis == null || !crisis.isActive)
        {
            SetResponseStatus("Selected crisis is not valid anymore.");
            RefreshOverlayText();
            return false;
        }

        if (turns.State != Turns.TurnState.PlayerTurn)
        {
            SetResponseStatus("You can respond only during your turn.");
            RefreshOverlayText();
            return false;
        }

        if (responseActionCost > 0 && (!turns.CanTakeAction || turns.ActionsRemaining < responseActionCost))
        {
            SetResponseStatus($"Not enough actions. Need {responseActionCost}.");
            RefreshOverlayText();
            return false;
        }

        int[] cost = BuildResponseResourceCost(crisis);
        if (!turns.CanAffordResources(cost))
        {
            SetResponseStatus($"Not enough resources. Need W:{cost[(int)ResourceType.Wood]} S:{cost[(int)ResourceType.Stone]}.");
            RefreshOverlayText();
            return false;
        }

        if (responseActionCost > 0 && !turns.TrySpendAction(responseActionCost))
        {
            SetResponseStatus("Could not spend action point.");
            RefreshOverlayText();
            return false;
        }

        if (!turns.TrySpendResources(cost))
        {
            SetResponseStatus("Could not spend resources.");
            RefreshOverlayText();
            return false;
        }

        bool connectedAtResponse = IsVillageConnectedToCity(crisis.coordinate);
        int responsePower = Mathf.Max(1, Mathf.RoundToInt(baseResponsePower * (connectedAtResponse ? 1f : disconnectedResponsePowerMultiplier)));
        if (connectedAtResponse)
        {
            responsePower += extraResponsePowerWhenConnected;
        }

        int severityBefore = crisis.severity;
        int severityAfterResponse = Mathf.Max(0, severityBefore - responsePower);
        if (!connectedAtResponse && disconnectedResolveFloor > 0)
        {
            int containmentFloor = Mathf.Clamp(disconnectedResolveFloor, 1, 99);
            severityAfterResponse = Mathf.Max(containmentFloor, severityAfterResponse);
        }

        crisis.severity = severityAfterResponse;
        if (crisis.severity <= 0)
        {
            ResolveCrisis(crisis);
            int resolveReward = Mathf.Max(0, resolveStabilityReward) + (connectedAtResponse ? Mathf.Max(0, connectedResolveStabilityBonus) : 0);
            SetResponseStatus($"Resolved crisis! ({severityBefore} -> 0, +{resolveReward} Health)");
        }
        else if (!connectedAtResponse && disconnectedResolveFloor > 0 && crisis.severity <= disconnectedResolveFloor)
        {
            SetResponseStatus($"Crisis contained at {crisis.severity}. Connect village to city to fully resolve.");
            UpdateMarkerVisual(crisis);
        }
        else
        {
            UpdateMarkerVisual(crisis);
            SetResponseStatus($"Response applied: {severityBefore} -> {crisis.severity}");
        }

        NotifyStateChanged();
        return true;
    }

    public void SelectNextCrisis()
    {
        if (_activeCrises.Count == 0)
        {
            return;
        }

        _selectedCrisisIndex = (_selectedCrisisIndex + 1) % _activeCrises.Count;
        if (logImportantEvents)
        {
            VillageCrisisState selected = _activeCrises[_selectedCrisisIndex];
            Debug.Log($"Crisis selected: {selected.type} at {selected.coordinate} (severity {selected.severity})", this);
        }

        RefreshAllMarkerVisuals();
        RefreshOverlayText();
    }

    public void SelectPreviousCrisis()
    {
        if (_activeCrises.Count == 0)
        {
            return;
        }

        _selectedCrisisIndex--;
        if (_selectedCrisisIndex < 0)
        {
            _selectedCrisisIndex = _activeCrises.Count - 1;
        }

        if (logImportantEvents)
        {
            VillageCrisisState selected = _activeCrises[_selectedCrisisIndex];
            Debug.Log($"Crisis selected: {selected.type} at {selected.coordinate} (severity {selected.severity})", this);
        }

        RefreshAllMarkerVisuals();
        RefreshOverlayText();
    }

    private void OnMapGenerated(GridMap _)
    {
        InitializeCrisisMap();
    }

    private void InitializeCrisisMap()
    {
        if (gridMap == null || gridMap.tileMap == null)
        {
            _initialized = false;
            return;
        }

        ClearAllCrisisMarkers();
        _activeCrises.Clear();
        _allVillages.Clear();
        _connectedRoadNetwork.Clear();
        _reachableFromCity.Clear();

        _cityCoordinate = new Vector2Int(-1, -1);
        for (int x = 0; x < gridMap.Width; x++)
        {
            for (int y = 0; y < gridMap.Height; y++)
            {
                GridTile tile = gridMap.GetTileAt(x, y);
                if (tile == null)
                {
                    continue;
                }

                if (IsCityTile(tile))
                {
                    _cityCoordinate = new Vector2Int(x, y);
                }

                if (IsVillageTile(tile))
                {
                    _allVillages.Add(new Vector2Int(x, y));
                }
            }
        }

        _selectedCrisisIndex = 0;
        _stability = Mathf.Clamp(startingStability, 0, 100);
        _turnsUntilGuaranteedSpawn = Mathf.Max(1, guaranteedSpawnIntervalTurns);
        _spawnGraceTurnsRemaining = 0;
        _spreadGraceTurnsRemaining = 0;
        _lastComputedStabilityDrain = 0;
        InitializeCrisisRng();
        SetResponseStatus("Crisis system initialized.");
        _initialized = _allVillages.Count > 0;

        if (_initialized)
        {
            RebuildConnectivityCache();
            TrySpawnCrisis(forceSpawn: true, guaranteedSpawnFromTimer: false);
        }

        NotifyStateChanged();
    }

    private void OnTurnStarted(Turns _)
    {
        if (!_initialized || turns == null)
        {
            return;
        }

        if (turns.State == Turns.TurnState.Win || turns.State == Turns.TurnState.Lose)
        {
            return;
        }

        RebuildConnectivityCache();
        bool allowSpread = ConsumeSpreadGraceTurn();
        bool allowSpawn = ConsumeSpawnGraceTurn();
        EscalateCrises();
        if (allowSpread)
        {
            TrySpreadCrises();
        }

        if (allowSpawn)
        {
            bool guaranteedSpawn = AdvanceGuaranteedSpawnTimer();
            TrySpawnCrisis(forceSpawn: false, guaranteedSpawnFromTimer: guaranteedSpawn);
        }

        ApplyStabilityPressure();

        NotifyStateChanged();
    }

    private void EscalateCrises()
    {
        string highestPriorityAlert = null;
        int highestAlertStage = -1;

        for (int i = 0; i < _activeCrises.Count; i++)
        {
            VillageCrisisState crisis = _activeCrises[i];
            if (crisis == null || !crisis.isActive)
            {
                continue;
            }

            int gain = NextRandomIntInclusive(severityGainPerTurnMin, severityGainPerTurnMax);
            if (IsVillageConnectedToCity(crisis.coordinate))
            {
                gain = Mathf.Max(1, Mathf.RoundToInt(gain * 0.7f));
            }

            crisis.severity = Mathf.Clamp(crisis.severity + gain, 0, 100);
            crisis.ageTurns++;
            UpdateMarkerVisual(crisis);

            int stageNow = GetSeverityStage(crisis.severity);
            if (showSeverityMilestoneAlerts && stageNow > crisis.announcedSeverityStage)
            {
                crisis.announcedSeverityStage = stageNow;
                if (stageNow > highestAlertStage)
                {
                    highestAlertStage = stageNow;
                    highestPriorityAlert = BuildSeverityMilestoneMessage(crisis, stageNow);
                }
            }

            if (crisis.severity >= criticalSeverityThreshold)
            {
                turns.TrySpendTurns(stabilityTurnPenaltyOnCritical);
            }
        }

        if (!string.IsNullOrWhiteSpace(highestPriorityAlert))
        {
            SetResponseStatus(highestPriorityAlert);
        }
    }

    private void TrySpreadCrises()
    {
        if (_activeCrises.Count == 0 || _allVillages.Count <= _activeCrises.Count)
        {
            return;
        }

        int crisisCountBeforeSpread = _activeCrises.Count;
        for (int i = 0; i < crisisCountBeforeSpread; i++)
        {
            if (_activeCrises.Count >= maxActiveCrises)
            {
                return;
            }

            VillageCrisisState source = _activeCrises[i];
            if (source == null || source.severity < criticalSeverityThreshold)
            {
                continue;
            }

            if (NextRandomValue01() > spreadChance)
            {
                continue;
            }

            Vector2Int target = PickSpreadTarget(source.coordinate);
            if (target.x < 0)
            {
                continue;
            }

            CreateCrisisAt(target, RandomCrisisType(), NextRandomIntInclusive(35, 60));
        }
    }

    private bool AdvanceGuaranteedSpawnTimer()
    {
        _turnsUntilGuaranteedSpawn = Mathf.Max(0, _turnsUntilGuaranteedSpawn - 1);
        if (_turnsUntilGuaranteedSpawn > 0)
        {
            return false;
        }

        _turnsUntilGuaranteedSpawn = Mathf.Max(1, guaranteedSpawnIntervalTurns);
        return true;
    }

    private void TrySpawnCrisis(bool forceSpawn, bool guaranteedSpawnFromTimer)
    {
        if (_activeCrises.Count >= maxActiveCrises || _allVillages.Count == 0)
        {
            return;
        }

        float chance = (forceSpawn || guaranteedSpawnFromTimer) ? 1f : baseSpawnChance;
        chance = Mathf.Clamp01(chance + _activeCrises.Count * 0.05f);

        if (!forceSpawn && !guaranteedSpawnFromTimer && NextRandomValue01() > chance)
        {
            return;
        }

        Vector2Int target = PickVillageWithoutCrisis();
        if (target.x < 0)
        {
            return;
        }

        CreateCrisisAt(target, RandomCrisisType(), NextRandomIntInclusive(28, 55));
    }

    private void ApplyStabilityPressure()
    {
        int criticalCount = CountCriticalCrises();
        int drain = passiveStabilityDrain + _activeCrises.Count + criticalCount * criticalCrisisStabilityDrain;
        _lastComputedStabilityDrain = Mathf.Max(0, drain);
        _stability = Mathf.Clamp(_stability - Mathf.Max(0, drain), 0, 100);

        if (_stability <= 0)
        {
            if (logImportantEvents)
            {
                Debug.LogWarning("CrisisSystem: Stability collapsed. Encounter lost.", this);
            }

            turns.SetLoseState();
        }
    }

    private void ResolveCrisis(VillageCrisisState crisis)
    {
        if (crisis == null)
        {
            return;
        }

        ClearSelectionOutline(crisis);
        if (crisis.marker != null)
        {
            Destroy(crisis.marker);
            crisis.marker = null;
        }

        if (logImportantEvents)
        {
            Debug.Log($"Crisis resolved at {crisis.coordinate}.", this);
        }

        _activeCrises.Remove(crisis);

        int stabilityReward = Mathf.Max(0, resolveStabilityReward);
        if (IsVillageConnectedToCity(crisis.coordinate))
        {
            stabilityReward += Mathf.Max(0, connectedResolveStabilityBonus);
        }

        _stability = Mathf.Clamp(_stability + stabilityReward, 0, 100);
        _spawnGraceTurnsRemaining = Mathf.Max(_spawnGraceTurnsRemaining, Mathf.Max(0, postResolveSpawnGraceTurns));
        _spreadGraceTurnsRemaining = Mathf.Max(_spreadGraceTurnsRemaining, Mathf.Max(0, postResolveSpreadGraceTurns));
        _turnsUntilGuaranteedSpawn = Mathf.Max(1, guaranteedSpawnIntervalTurns);

        if (levelScore != null)
        {
            levelScore.AddDefaultScore(65f);
        }

        if (_activeCrises.Count == 0)
        {
            _selectedCrisisIndex = 0;
        }
        else
        {
            _selectedCrisisIndex = Mathf.Clamp(_selectedCrisisIndex, 0, _activeCrises.Count - 1);
        }

        RefreshAllMarkerVisuals();
    }

    private void CreateCrisisAt(Vector2Int villageCoordinate, VillageCrisisType type, int initialSeverity)
    {
        if (HasActiveCrisisAt(villageCoordinate))
        {
            return;
        }

        GameObject markerInstance = CreateMarker(villageCoordinate);

        var crisis = new VillageCrisisState
        {
            coordinate = villageCoordinate,
            type = type,
            severity = Mathf.Clamp(initialSeverity, 1, 100),
            ageTurns = 0,
            announcedSeverityStage = GetSeverityStage(initialSeverity),
            isActive = true,
            marker = markerInstance,
            markerBaseScale = markerInstance != null ? markerInstance.transform.localScale : Vector3.one
        };

        _activeCrises.Add(crisis);
        _selectedCrisisIndex = Mathf.Clamp(_selectedCrisisIndex, 0, _activeCrises.Count - 1);
        UpdateMarkerVisual(crisis);

        if (logImportantEvents)
        {
            Debug.Log($"New crisis: {type} at {villageCoordinate}, severity {crisis.severity}", this);
        }
    }

    private bool ConsumeSpawnGraceTurn()
    {
        if (_spawnGraceTurnsRemaining <= 0)
        {
            return true;
        }

        _spawnGraceTurnsRemaining = Mathf.Max(0, _spawnGraceTurnsRemaining - 1);
        return false;
    }

    private bool ConsumeSpreadGraceTurn()
    {
        if (_spreadGraceTurnsRemaining <= 0)
        {
            return true;
        }

        _spreadGraceTurnsRemaining = Mathf.Max(0, _spreadGraceTurnsRemaining - 1);
        return false;
    }

    private GameObject CreateMarker(Vector2Int coordinate)
    {
        if (gridMap == null)
        {
            return null;
        }

        GameObject villageTile = gridMap.GetTileInstanceAt(coordinate.x, coordinate.y);
        if (villageTile == null)
        {
            return null;
        }

        Vector3 markerPosition = villageTile.transform.position + markerOffset;

        if (preferTextureMarkerOverPrefab)
        {
            GameObject textureMarker = CreateTextureMarker(markerPosition, coordinate);
            if (textureMarker != null)
            {
                return textureMarker;
            }
        }

        GameObject marker = null;
        if (crisisMarkerPrefab != null)
        {
            marker = Instantiate(crisisMarkerPrefab, markerPosition, Quaternion.identity, transform);
            float safeScaleMultiplier = Mathf.Max(0.01f, prefabMarkerScaleMultiplier);
            marker.transform.localScale *= safeScaleMultiplier;
        }
        else
        {
            marker = CreateTextureMarker(markerPosition, coordinate);
            if (marker != null)
            {
                return marker;
            }
            return null;
        }

        marker.name = $"CrisisMarker_{coordinate.x}_{coordinate.y}";
        return marker;
    }

    private GameObject CreateTextureMarker(Vector3 markerPosition, Vector2Int coordinate)
    {
        Sprite markerSprite = GetOrCreateTextureMarkerSprite();
        if (markerSprite == null)
        {
            return null;
        }

        var markerObject = new GameObject($"CrisisMarker_{coordinate.x}_{coordinate.y}");
        markerObject.transform.SetParent(transform);
        markerObject.transform.position = markerPosition;

        SpriteRenderer spriteRenderer = markerObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = markerSprite;
        spriteRenderer.sortingOrder = textureMarkerSortingOrder;
        spriteRenderer.color = Color.white;

        return markerObject;
    }

    private Sprite GetOrCreateTextureMarkerSprite()
    {
        if (crisisMarkerTexture == null)
        {
            return null;
        }

        if (_cachedTextureMarkerSprite != null && _cachedTextureMarkerSprite.texture == crisisMarkerTexture)
        {
            return _cachedTextureMarkerSprite;
        }

        float worldSize = Mathf.Max(0.1f, textureMarkerWorldSize);
        float pixelsPerUnit = Mathf.Max(1f, crisisMarkerTexture.width / worldSize);

        _cachedTextureMarkerSprite = Sprite.Create(
            crisisMarkerTexture,
            new Rect(0f, 0f, crisisMarkerTexture.width, crisisMarkerTexture.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit
        );

        return _cachedTextureMarkerSprite;
    }

    private void UpdateMarkerVisual(VillageCrisisState crisis)
    {
        if (crisis == null || crisis.marker == null)
        {
            return;
        }

        Color color = Color.green;
        if (crisis.severity >= criticalSeverityThreshold)
        {
            color = new Color(0.95f, 0.12f, 0.12f);
        }
        else if (crisis.severity >= criticalSeverityThreshold * 0.65f)
        {
            color = new Color(0.98f, 0.52f, 0.06f);
        }
        else
        {
            color = new Color(0.98f, 0.86f, 0.1f);
        }

        SpriteRenderer spriteRenderer = crisis.marker.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
            UpdateSelectionOutline(crisis, spriteRenderer);
            return;
        }

        ClearSelectionOutline(crisis);

        Renderer[] renderers = crisis.marker.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererComponent = renderers[i];
            if (rendererComponent == null || rendererComponent.material == null)
            {
                continue;
            }

            rendererComponent.material.color = color;
        }
    }

    private bool IsSelectedCrisis(VillageCrisisState crisis)
    {
        if (crisis == null || _activeCrises.Count == 0)
        {
            return false;
        }

        int selectedIndex = Mathf.Clamp(_selectedCrisisIndex, 0, _activeCrises.Count - 1);
        return ReferenceEquals(_activeCrises[selectedIndex], crisis);
    }

    private void RefreshAllMarkerVisuals()
    {
        for (int i = 0; i < _activeCrises.Count; i++)
        {
            UpdateMarkerVisual(_activeCrises[i]);
        }
    }

    private void UpdateSelectionOutline(VillageCrisisState crisis, SpriteRenderer markerRenderer)
    {
        if (crisis == null)
        {
            return;
        }

        bool shouldShowOutline = IsSelectedCrisis(crisis)
            && markerRenderer != null
            && markerRenderer.sprite != null;

        if (!shouldShowOutline)
        {
            ClearSelectionOutline(crisis);
            return;
        }

        if (crisis.selectionOutline == null)
        {
            GameObject outlineObject = new GameObject("SelectionOutline");
            outlineObject.transform.SetParent(markerRenderer.transform, false);
            outlineObject.transform.localPosition = Vector3.zero;
            outlineObject.transform.localRotation = Quaternion.identity;
            crisis.selectionOutline = outlineObject.AddComponent<SpriteRenderer>();
        }

        SpriteRenderer outlineRenderer = crisis.selectionOutline;
        outlineRenderer.sprite = markerRenderer.sprite;
        outlineRenderer.color = selectedMarkerOutlineColor;
        outlineRenderer.sortingLayerID = markerRenderer.sortingLayerID;
        outlineRenderer.sortingOrder = markerRenderer.sortingOrder + selectedMarkerOutlineOrderOffset;

        float outlineScale = Mathf.Max(1.01f, selectedMarkerOutlineScale);
        outlineRenderer.transform.localScale = new Vector3(outlineScale, outlineScale, 1f);
    }

    private void ClearSelectionOutline(VillageCrisisState crisis)
    {
        if (crisis == null || crisis.selectionOutline == null)
        {
            return;
        }

        Destroy(crisis.selectionOutline.gameObject);
        crisis.selectionOutline = null;
    }

    private void OnDestroy()
    {
        if (_cachedTextureMarkerSprite != null)
        {
            Destroy(_cachedTextureMarkerSprite);
            _cachedTextureMarkerSprite = null;
        }
    }

    private int[] BuildResponseResourceCost(VillageCrisisState crisis)
    {
        int resourceCount = turns != null ? Mathf.Max(3, turns.resourceTypesCount) : 3;
        int[] cost = new int[resourceCount];

        int severityBand = Mathf.Clamp(Mathf.CeilToInt(crisis.severity / 40f), 1, 3);
        bool connected = IsVillageConnectedToCity(crisis.coordinate);
        int connectivityPenalty = (!connected && crisis.severity >= criticalSeverityThreshold) ? 1 : 0;

        int woodCost = 1;
        int stoneCost = 1;

        switch (crisis.type)
        {
            case VillageCrisisType.Infrastructure:
                stoneCost += Mathf.Max(0, severityBand - 1);
                break;
            case VillageCrisisType.Nature:
                woodCost += Mathf.Max(0, severityBand - 1);
                break;
            case VillageCrisisType.Cultural:
                // Cultural crises are intentionally cheaper so players can stabilize quickly.
                break;
            case VillageCrisisType.Social:
                woodCost += severityBand >= 3 ? 1 : 0;
                break;
            case VillageCrisisType.Health:
                if (severityBand >= 2)
                {
                    woodCost += 1;
                    stoneCost += 1;
                }
                break;
        }

        if (severityBand >= 3)
        {
            woodCost += 1;
            stoneCost += 1;
        }

        cost[(int)ResourceType.Wood] = Mathf.Max(1, woodCost + connectivityPenalty);
        cost[(int)ResourceType.Stone] = Mathf.Max(1, stoneCost + connectivityPenalty);
        return cost;
    }

    private void RebuildConnectivityCache()
    {
        _connectedRoadNetwork.Clear();
        _reachableFromCity.Clear();

        if (gridMap == null || tileBuilding == null || _cityCoordinate.x < 0)
        {
            return;
        }

        var queue = new Queue<Vector2Int>();
        queue.Enqueue(_cityCoordinate);
        _reachableFromCity.Add(_cityCoordinate);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            TryVisitNeighbor(current + Vector2Int.up, queue);
            TryVisitNeighbor(current + Vector2Int.right, queue);
            TryVisitNeighbor(current + Vector2Int.down, queue);
            TryVisitNeighbor(current + Vector2Int.left, queue);
        }

        foreach (Vector2Int coordinate in _reachableFromCity)
        {
            if (tileBuilding.IsRoadAlreadyBuilt(coordinate))
            {
                _connectedRoadNetwork.Add(coordinate);
            }
        }
    }

    private void TryVisitNeighbor(Vector2Int neighbor, Queue<Vector2Int> queue)
    {
        if (!gridMap.IsInsideGrid(neighbor) || _reachableFromCity.Contains(neighbor))
        {
            return;
        }

        GridTile tile = gridMap.GetTileAt(neighbor.x, neighbor.y);
        if (tile == null)
        {
            return;
        }

        bool traversable = tileBuilding.IsRoadAlreadyBuilt(neighbor) || IsSettlementTile(tile);
        if (!traversable)
        {
            return;
        }

        _reachableFromCity.Add(neighbor);
        queue.Enqueue(neighbor);
    }

    private bool IsVillageConnectedToCity(Vector2Int village)
    {
        if (_cityCoordinate.x < 0)
        {
            return false;
        }

        return _reachableFromCity.Contains(village);
    }

    private Vector2Int PickSpreadTarget(Vector2Int sourceVillage)
    {
        Vector2Int bestTarget = new Vector2Int(-1, -1);
        int bestDistance = int.MaxValue;

        for (int i = 0; i < _allVillages.Count; i++)
        {
            Vector2Int candidate = _allVillages[i];
            if (candidate == sourceVillage || HasActiveCrisisAt(candidate))
            {
                continue;
            }

            int distance = Mathf.Abs(candidate.x - sourceVillage.x) + Mathf.Abs(candidate.y - sourceVillage.y);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    private Vector2Int PickVillageWithoutCrisis()
    {
        var candidates = new List<Vector2Int>();
        for (int i = 0; i < _allVillages.Count; i++)
        {
            Vector2Int village = _allVillages[i];
            if (!HasActiveCrisisAt(village))
            {
                candidates.Add(village);
            }
        }

        if (candidates.Count == 0)
        {
            return new Vector2Int(-1, -1);
        }

        int randomIndex = NextRandomIntInclusive(0, candidates.Count - 1);
        return candidates[randomIndex];
    }

    private bool HasActiveCrisisAt(Vector2Int coordinate)
    {
        for (int i = 0; i < _activeCrises.Count; i++)
        {
            VillageCrisisState crisis = _activeCrises[i];
            if (crisis != null && crisis.isActive && crisis.coordinate == coordinate)
            {
                return true;
            }
        }

        return false;
    }

    private VillageCrisisType RandomCrisisType()
    {
        int value = NextRandomIntInclusive(1, 5);
        return (VillageCrisisType)value;
    }

    private void InitializeCrisisRng()
    {
        int mapSeed = gridMap != null ? gridMap.seed : 0;
        unchecked
        {
            int widthHash = gridMap != null ? gridMap.Width * 73856093 : 0;
            int heightHash = gridMap != null ? gridMap.Height * 19349663 : 0;
            int baseSeed = mapSeed ^ widthHash ^ heightHash ^ crisisSeedOffset;
            _crisisRngSeed = deterministicPerMapSeed ? baseSeed : (Environment.TickCount ^ baseSeed);
        }

        if (_crisisRngSeed == int.MinValue)
        {
            _crisisRngSeed = int.MaxValue;
        }

        if (_crisisRngSeed < 0)
        {
            _crisisRngSeed = -_crisisRngSeed;
        }

        if (_crisisRngSeed == 0)
        {
            _crisisRngSeed = 1;
        }

        _crisisRng = new System.Random(_crisisRngSeed);
    }

    private int NextRandomIntInclusive(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive)
        {
            (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        }

        if (_crisisRng == null)
        {
            InitializeCrisisRng();
        }

        if (minInclusive == maxInclusive)
        {
            return minInclusive;
        }

        return _crisisRng.Next(minInclusive, maxInclusive + 1);
    }

    private float NextRandomValue01()
    {
        if (_crisisRng == null)
        {
            InitializeCrisisRng();
        }

        return (float)_crisisRng.NextDouble();
    }

    private int CountCriticalCrises()
    {
        int count = 0;
        for (int i = 0; i < _activeCrises.Count; i++)
        {
            VillageCrisisState crisis = _activeCrises[i];
            if (crisis != null && crisis.severity >= criticalSeverityThreshold)
            {
                count++;
            }
        }

        return count;
    }

    private int CountDisconnectedVillagesFromCity()
    {
        if (!_initialized || _allVillages.Count == 0)
        {
            return 0;
        }

        int disconnected = 0;
        for (int i = 0; i < _allVillages.Count; i++)
        {
            if (!IsVillageConnectedToCity(_allVillages[i]))
            {
                disconnected++;
            }
        }

        return disconnected;
    }

    private void ClearAllCrisisMarkers()
    {
        for (int i = 0; i < _activeCrises.Count; i++)
        {
            VillageCrisisState crisis = _activeCrises[i];
            if (crisis != null && crisis.marker != null)
            {
                ClearSelectionOutline(crisis);
                Destroy(crisis.marker);
                crisis.marker = null;
            }
        }
    }

    private static bool IsSettlementTile(GridTile tile)
    {
        return IsCityTile(tile) || IsVillageTile(tile);
    }

    private static bool IsCityTile(GridTile tile)
    {
        if (tile == null)
        {
            return false;
        }

        if (tile.tileType == TileType.City)
        {
            return true;
        }

        string tileName = (tile.tileName ?? string.Empty).Trim();
        return string.Equals(tileName, "City", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVillageTile(GridTile tile)
    {
        if (tile == null)
        {
            return false;
        }

        if (tile.tileType == TileType.Village)
        {
            return true;
        }

        string tileName = (tile.tileName ?? string.Empty).Trim();
        return string.Equals(tileName, "Village", StringComparison.OrdinalIgnoreCase);
    }

    private void OnPreviousCrisisPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed || InGameGenerationMenu.IsAnyMenuOpen)
        {
            return;
        }

        SelectPreviousCrisis();
    }

    private void OnNextCrisisPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed || InGameGenerationMenu.IsAnyMenuOpen)
        {
            return;
        }

        SelectNextCrisis();
    }

    private void OnRespondPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed || InGameGenerationMenu.IsAnyMenuOpen)
        {
            return;
        }

        TryRespondToSelectedCrisis();
    }

    private static void RegisterAction(InputActionReference actionReference, Action<InputAction.CallbackContext> callback)
    {
        if (actionReference == null || actionReference.action == null)
        {
            return;
        }

        actionReference.action.Enable();
        actionReference.action.performed -= callback;
        actionReference.action.performed += callback;
    }

    private static void UnregisterAction(InputActionReference actionReference, Action<InputAction.CallbackContext> callback)
    {
        if (actionReference == null || actionReference.action == null)
        {
            return;
        }

        actionReference.action.performed -= callback;
        actionReference.action.Disable();
    }

    private void NotifyStateChanged()
    {
        CrisisStateChanged?.Invoke(this);
        RefreshOverlayText();
    }

    private void SetResponseStatus(string message)
    {
        _responseStatusMessage = string.IsNullOrWhiteSpace(message) ? "-" : message;
        _responseStatusTimestamp = Time.time;
    }

    private void EnsureOverlayInitialized()
    {
        if (_overlayUiInitialized && _overlayRoot != null)
        {
            return;
        }

        if (overlayDocument == null)
        {
            overlayDocument = GetComponent<UIDocument>();
        }

        if (overlayDocument == null && createOverlayDocumentIfMissing)
        {
            overlayDocument = gameObject.AddComponent<UIDocument>();
        }

        if (overlayDocument == null)
        {
            return;
        }

        ResolvePanelSettings();
        if (overlayDocument.panelSettings == null && panelSettings != null)
        {
            overlayDocument.panelSettings = panelSettings;
        }

        if (overlayLayoutAsset == null)
        {
            overlayLayoutAsset = Resources.Load<VisualTreeAsset>("UI/CrisisOverlay");
        }

        if (overlayStyleSheet == null)
        {
            overlayStyleSheet = Resources.Load<StyleSheet>("UI/CrisisOverlay");
        }

        if (overlayLayoutAsset == null)
        {
            Debug.LogError("VillageCrisisSystem: Missing UI layout Resources/UI/CrisisOverlay.uxml", this);
            return;
        }

        overlayDocument.enabled = true;
        _overlayRoot = overlayDocument.rootVisualElement;
        if (_overlayRoot == null)
        {
            return;
        }

        _overlayRoot.Clear();
        overlayLayoutAsset.CloneTree(_overlayRoot);
        if (overlayStyleSheet != null)
        {
            _overlayRoot.styleSheets.Add(overlayStyleSheet);
        }

        _overlayContainer = _overlayRoot.Q<VisualElement>("crisis-ui-root");
        _overlayPanel = _overlayRoot.Q<VisualElement>("crisis-panel");
        _titleLabel = _overlayRoot.Q<Label>("crisis-title");
        _summaryLabel = _overlayRoot.Q<Label>("crisis-summary");
        _gateLabel = _overlayRoot.Q<Label>("crisis-gate");
        _spawnRuleLabel = _overlayRoot.Q<Label>("crisis-spawn-rule");
        _selectedLabel = _overlayRoot.Q<Label>("crisis-selected");
        _typeEffectLabel = _overlayRoot.Q<Label>("crisis-type-effect");
        _woodCostLabel = _overlayRoot.Q<Label>("crisis-wood-cost");
        _stoneCostLabel = _overlayRoot.Q<Label>("crisis-stone-cost");
        _actionCostLabel = _overlayRoot.Q<Label>("crisis-action-cost");
        _costRow = _overlayRoot.Q<VisualElement>("crisis-cost-row");
        _selectedHintLabel = _overlayRoot.Q<Label>("crisis-selected-hint");
        _resolveHintLabel = _overlayRoot.Q<Label>("crisis-resolve-hint");
        _controlsLabel = _overlayRoot.Q<Label>("crisis-controls");
        _pressureLabel = _overlayRoot.Q<Label>("crisis-pressure");
        _detailsTop = _overlayRoot.Q<VisualElement>("crisis-details");
        _detailsMiddle = _overlayRoot.Q<VisualElement>("crisis-details-lower");
        _detailsBottom = _overlayRoot.Q<VisualElement>("crisis-details-bottom");
        _prevButton = _overlayRoot.Q<Button>("crisis-prev-button");
        _respondButton = _overlayRoot.Q<Button>("crisis-respond-button");
        _nextButton = _overlayRoot.Q<Button>("crisis-next-button");
        _collapseButton = _overlayRoot.Q<Button>("crisis-collapse-button");

        ConfigurePickingModes();

        if (_prevButton != null)
        {
            _prevButton.clicked -= OnPreviousButtonClicked;
            _prevButton.clicked += OnPreviousButtonClicked;
        }

        if (_respondButton != null)
        {
            _respondButton.clicked -= OnRespondButtonClicked;
            _respondButton.clicked += OnRespondButtonClicked;
        }

        if (_nextButton != null)
        {
            _nextButton.clicked -= OnNextButtonClicked;
            _nextButton.clicked += OnNextButtonClicked;
        }

        if (_collapseButton != null)
        {
            _collapseButton.clicked -= OnCollapseButtonClicked;
            _collapseButton.clicked += OnCollapseButtonClicked;
        }

        _isMiniMode = startInMiniMode;

        _overlayUiInitialized = true;
        ApplyMiniModeVisualState();
        ApplyResponsiveOverlay(force: true);
        RefreshOverlayText();
        SetOverlayVisible(showOverlay);
    }

    private void ConfigurePickingModes()
    {
        if (_overlayRoot != null)
        {
            _overlayRoot.pickingMode = PickingMode.Ignore;
        }

        if (_overlayContainer != null)
        {
            _overlayContainer.pickingMode = PickingMode.Ignore;
        }

        if (_overlayPanel != null)
        {
            _overlayPanel.pickingMode = PickingMode.Ignore;
        }

        if (_titleLabel != null)
        {
            _titleLabel.pickingMode = PickingMode.Ignore;
        }

        if (_summaryLabel != null)
        {
            _summaryLabel.pickingMode = PickingMode.Ignore;
        }

        if (_gateLabel != null)
        {
            _gateLabel.pickingMode = PickingMode.Ignore;
        }

        if (_spawnRuleLabel != null)
        {
            _spawnRuleLabel.pickingMode = PickingMode.Ignore;
        }

        if (_selectedLabel != null)
        {
            _selectedLabel.pickingMode = PickingMode.Ignore;
        }

        if (_woodCostLabel != null)
        {
            _woodCostLabel.pickingMode = PickingMode.Ignore;
        }

        if (_stoneCostLabel != null)
        {
            _stoneCostLabel.pickingMode = PickingMode.Ignore;
        }

        if (_actionCostLabel != null)
        {
            _actionCostLabel.pickingMode = PickingMode.Ignore;
        }

        if (_costRow != null)
        {
            _costRow.pickingMode = PickingMode.Ignore;
        }

        if (_typeEffectLabel != null)
        {
            _typeEffectLabel.pickingMode = PickingMode.Ignore;
        }

        if (_selectedHintLabel != null)
        {
            _selectedHintLabel.pickingMode = PickingMode.Ignore;
        }

        if (_resolveHintLabel != null)
        {
            _resolveHintLabel.pickingMode = PickingMode.Ignore;
        }

        if (_controlsLabel != null)
        {
            _controlsLabel.pickingMode = PickingMode.Ignore;
        }

        if (_pressureLabel != null)
        {
            _pressureLabel.pickingMode = PickingMode.Ignore;
        }

        if (_detailsTop != null)
        {
            _detailsTop.pickingMode = PickingMode.Ignore;
        }

        if (_detailsMiddle != null)
        {
            _detailsMiddle.pickingMode = PickingMode.Ignore;
        }

        if (_detailsBottom != null)
        {
            _detailsBottom.pickingMode = PickingMode.Ignore;
        }

        if (_prevButton != null)
        {
            _prevButton.pickingMode = PickingMode.Position;
        }

        if (_respondButton != null)
        {
            _respondButton.pickingMode = PickingMode.Position;
        }

        if (_nextButton != null)
        {
            _nextButton.pickingMode = PickingMode.Position;
        }

        if (_collapseButton != null)
        {
            _collapseButton.pickingMode = PickingMode.Position;
        }
    }

    private void ResolvePanelSettings()
    {
        if (panelSettings != null)
        {
            return;
        }

        if (overlayDocument != null && overlayDocument.panelSettings != null)
        {
            panelSettings = overlayDocument.panelSettings;
            return;
        }

        UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsInactive.Include);
        for (int i = 0; i < documents.Length; i++)
        {
            UIDocument document = documents[i];
            if (document != null && document != overlayDocument && document.panelSettings != null)
            {
                panelSettings = document.panelSettings;
                return;
            }
        }

        panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings");
    }

    private void SetOverlayVisible(bool visible)
    {
        if (_overlayRoot == null)
        {
            return;
        }

        _overlayRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void ApplyResponsiveOverlay(bool force)
    {
        if (_overlayPanel == null)
        {
            return;
        }

        bool canStackByWidth = stackBelowResourceHudOnNarrowScreens
            && Screen.width <= Mathf.Max(320, stackBelowResourceHudMaxScreenWidth);

        if (!force
            && !canStackByWidth
            && Screen.width == _lastScreenWidth
            && Screen.height == _lastScreenHeight)
        {
            return;
        }

        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        bool isPhonePortraitLayout = usePhonePortraitLayout
            && Screen.height > Screen.width
            && Screen.width <= Mathf.Max(320, phonePortraitMaxScreenWidth);

        ApplyPhonePortraitClass(isPhonePortraitLayout);

        Rect resourceHudBounds = default;
        bool hasResourceHudBounds = canStackByWidth && TryGetResourceHudBounds(out resourceHudBounds);
        bool applyStackedLayout = canStackByWidth && hasResourceHudBounds;

        if (applyStackedLayout)
        {
            ApplyStackedPanelLayout(resourceHudBounds);
        }
        else
        {
            ClearStackedPanelLayout();
        }

        if (!applyStackedLayout)
        {
            if (isPhonePortraitLayout)
            {
                float requestedPhoneWidth = Mathf.Clamp(phonePortraitFixedPanelWidth, 300f, 360f);
                float fixedWidth = Mathf.Clamp(
                    requestedPhoneWidth,
                    180f,
                    Mathf.Max(180f, Screen.width - 12f)
                );

                _overlayPanel.style.width = fixedWidth;
                _overlayPanel.style.minWidth = fixedWidth;
                _overlayPanel.style.maxWidth = fixedWidth;
            }

            float resolvedTopMargin = ResolveCrisisPanelTopMargin(isPhonePortraitLayout, canStackByWidth, hasResourceHudBounds ? resourceHudBounds : default);
            if (isPhonePortraitLayout)
            {
                resolvedTopMargin += Mathf.Max(0f, phonePortraitSafeAreaTopInset * 0.05f);
                resolvedTopMargin = Mathf.Min(resolvedTopMargin, 8f);
            }

            if (resolvedTopMargin > 0.01f)
            {
                _overlayPanel.style.marginTop = resolvedTopMargin;
            }
            else
            {
                _overlayPanel.style.marginTop = StyleKeyword.Null;
            }

            if (isPhonePortraitLayout)
            {
                _overlayPanel.style.marginRight = 6f;
            }
            else
            {
                _overlayPanel.style.marginRight = StyleKeyword.Null;
            }
        }

        float resolvedMaxVisualScale = Mathf.Max(minOverlayVisualScale, maxOverlayVisualScale);
        float resolvedMinVisualScale = Mathf.Max(0.5f, minOverlayVisualScale);
        float safeGlobalScaleMultiplier = Mathf.Clamp(globalOverlayScaleMultiplier, 0.7f, 1.2f);
        float effectiveGlobalScaleMultiplier = isPhonePortraitLayout ? 1f : safeGlobalScaleMultiplier;

        if (isPhonePortraitLayout)
        {
            resolvedMinVisualScale = Mathf.Max(1f, resolvedMinVisualScale);
            resolvedMaxVisualScale = Mathf.Min(resolvedMaxVisualScale, 1.2f);
        }

        if (!scaleWithScreenShortSide)
        {
            SetScaleClass(isPhonePortraitLayout ? "crisis-panel--compact" : "crisis-panel--normal");
            _overlayPanel.style.transformOrigin = new TransformOrigin(
                new Length(applyStackedLayout ? 0f : 100f, LengthUnit.Percent),
                new Length(0f, LengthUnit.Percent),
                0f
            );
            float noAutoScale = isPhonePortraitLayout
                ? Mathf.Clamp(phonePortraitScaleMultiplier, resolvedMinVisualScale, resolvedMaxVisualScale)
                : 1f;
            noAutoScale *= effectiveGlobalScaleMultiplier;
            _overlayPanel.style.scale = new Scale(new Vector3(noAutoScale, noAutoScale, 1f));
            return;
        }

        float shortSide = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height));
        float rawScale = Mathf.Clamp(shortSide / Mathf.Max(1f, referenceShortSidePixels), 0.4f, 2.5f) * baseScaleBoost;
        if (isPhonePortraitLayout)
        {
            rawScale *= Mathf.Clamp(phonePortraitScaleMultiplier, 0.4f, 1f);
        }

        float visualScale = Mathf.Clamp(rawScale, resolvedMinVisualScale, resolvedMaxVisualScale);
        visualScale *= effectiveGlobalScaleMultiplier;
        float scaledRawScaleForClass = rawScale * effectiveGlobalScaleMultiplier;

        _overlayPanel.style.transformOrigin = new TransformOrigin(
            new Length(applyStackedLayout ? 0f : 100f, LengthUnit.Percent),
            new Length(0f, LengthUnit.Percent),
            0f
        );
        _overlayPanel.style.scale = new Scale(new Vector3(visualScale, visualScale, 1f));

        // Keep desktop / free-aspect preview readable by default.
        if (!isPhonePortraitLayout && shortSide >= 700f)
        {
            SetScaleClass(shortSide >= 1300f ? "crisis-panel--large" : "crisis-panel--normal");
            return;
        }

        if (isPhonePortraitLayout)
        {
            SetScaleClass("crisis-panel--compact");
        }
        else if (scaledRawScaleForClass <= compactScaleThreshold)
        {
            SetScaleClass("crisis-panel--compact");
        }
        else if (scaledRawScaleForClass >= largeScaleThreshold)
        {
            SetScaleClass("crisis-panel--large");
        }
        else
        {
            SetScaleClass("crisis-panel--normal");
        }
    }

    private void ApplyStackedPanelLayout(Rect resourceHudBounds)
    {
        if (_overlayPanel == null)
        {
            return;
        }

        if (_overlayContainer != null)
        {
            _overlayContainer.style.position = Position.Absolute;
            _overlayContainer.style.left = 0f;
            _overlayContainer.style.top = 0f;
            _overlayContainer.style.right = 0f;
            _overlayContainer.style.bottom = 0f;
            _overlayContainer.style.width = new Length(100f, LengthUnit.Percent);
            _overlayContainer.style.height = new Length(100f, LengthUnit.Percent);
        }

        float availableMaxWidth = Mathf.Max(220f, Screen.width - 12f);
        float targetWidth = Mathf.Clamp(resourceHudBounds.width, 220f, availableMaxWidth);
        float left = Mathf.Clamp(resourceHudBounds.xMin, 0f, Mathf.Max(0f, Screen.width - targetWidth));
        float top = Mathf.Max(0f, resourceHudBounds.yMax + Mathf.Max(0f, stackedMenuGapPixels));

        _overlayPanel.style.position = Position.Absolute;
        _overlayPanel.style.left = left;
        _overlayPanel.style.top = top;
        _overlayPanel.style.width = targetWidth;
        _overlayPanel.style.minWidth = targetWidth;
        _overlayPanel.style.maxWidth = targetWidth;
        _overlayPanel.style.marginLeft = 0f;
        _overlayPanel.style.marginTop = 0f;
        _overlayPanel.style.marginRight = 0f;
        _overlayPanel.style.marginBottom = 0f;
    }

    private void ClearStackedPanelLayout()
    {
        if (_overlayPanel == null)
        {
            return;
        }

        if (_overlayContainer != null)
        {
            _overlayContainer.style.position = StyleKeyword.Null;
            _overlayContainer.style.left = StyleKeyword.Null;
            _overlayContainer.style.top = StyleKeyword.Null;
            _overlayContainer.style.right = StyleKeyword.Null;
            _overlayContainer.style.bottom = StyleKeyword.Null;
            _overlayContainer.style.width = StyleKeyword.Null;
            _overlayContainer.style.height = StyleKeyword.Null;
        }

        _overlayPanel.style.position = Position.Relative;
        _overlayPanel.style.left = StyleKeyword.Null;
        _overlayPanel.style.top = StyleKeyword.Null;
        _overlayPanel.style.width = StyleKeyword.Null;
        _overlayPanel.style.minWidth = StyleKeyword.Null;
        _overlayPanel.style.maxWidth = StyleKeyword.Null;
        _overlayPanel.style.marginLeft = StyleKeyword.Null;
        _overlayPanel.style.marginBottom = StyleKeyword.Null;
    }

    private void ApplyPhonePortraitClass(bool enabled)
    {
        if (_overlayPanel == null || _isPhonePortraitLayout == enabled)
        {
            return;
        }

        _isPhonePortraitLayout = enabled;
        if (enabled)
        {
            _overlayPanel.AddToClassList("crisis-panel--phone-portrait");
        }
        else
        {
            _overlayPanel.RemoveFromClassList("crisis-panel--phone-portrait");
        }
    }

    private float ResolveCrisisPanelTopMargin(bool isPhonePortraitLayout, bool canStackByWidth, Rect resourceHudBounds)
    {
        float topMargin = 0f;
        if (isPhonePortraitLayout)
        {
            topMargin = Mathf.Max(0f, phonePortraitTopOffset - Mathf.Max(0f, phonePortraitUpwardShift));
            topMargin = Mathf.Min(topMargin, 8f);
        }

        if (!canStackByWidth)
        {
            return topMargin;
        }

        if (resourceHudBounds.height > 1f)
        {
            return Mathf.Max(topMargin, resourceHudBounds.yMax + Mathf.Max(0f, stackedMenuGapPixels));
        }

        return Mathf.Max(topMargin, fallbackStackTopOffset);
    }

    private bool TryGetResourceHudBounds(out Rect bounds)
    {
        bounds = default;
        EnsureResourceHudReference();
        if (_resourceHudRoot == null)
        {
            return false;
        }

        Rect hudBounds = _resourceHudRoot.worldBound;
        if (hudBounds.height <= 1f)
        {
            return false;
        }

        bounds = hudBounds;
        return true;
    }

    public bool TryGetOverlayPanelScreenBounds(out Rect bounds)
    {
        bounds = default;
        if (!_overlayUiInitialized || !showOverlay || _overlayRoot == null || _overlayPanel == null)
        {
            return false;
        }

        if (_overlayRoot.resolvedStyle.display == DisplayStyle.None)
        {
            return false;
        }

        Rect panelBounds = _overlayPanel.worldBound;
        if (panelBounds.width <= 1f || panelBounds.height <= 1f)
        {
            return false;
        }

        bounds = panelBounds;
        return true;
    }

    private void EnsureResourceHudReference()
    {
        if (_resourceHudDocument != null)
        {
            VisualElement existingRoot = _resourceHudDocument.rootVisualElement;
            if (existingRoot != null)
            {
                _resourceHudRoot = existingRoot.Q<VisualElement>("hud-root") ?? existingRoot;
                return;
            }

            _resourceHudDocument = null;
            _resourceHudRoot = null;
        }

        UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsInactive.Include);
        for (int i = 0; i < documents.Length; i++)
        {
            UIDocument document = documents[i];
            if (document == null || document == overlayDocument)
            {
                continue;
            }

            VisualElement root = document.rootVisualElement;
            if (root == null)
            {
                continue;
            }

            VisualElement hudRoot = root.Q<VisualElement>("hud-root");
            if (hudRoot == null)
            {
                continue;
            }

            _resourceHudDocument = document;
            _resourceHudRoot = hudRoot;
            return;
        }
    }

    private void SetScaleClass(string className)
    {
        if (_overlayPanel == null || string.Equals(_activeScaleClass, className, StringComparison.Ordinal))
        {
            return;
        }

        if (!string.IsNullOrEmpty(_activeScaleClass))
        {
            _overlayPanel.RemoveFromClassList(_activeScaleClass);
        }

        _activeScaleClass = className;
        _overlayPanel.AddToClassList(className);
    }

    private void RefreshOverlayText()
    {
        if (!_overlayUiInitialized || _overlayRoot == null)
        {
            return;
        }

        SetOverlayVisible(showOverlay);
        if (!showOverlay)
        {
            return;
        }

        int criticalCount = CountCriticalCrises();
        int disconnectedVillages = CountDisconnectedVillagesFromCity();
        bool gateOpen = CanDeclareVictory();
        bool allowLongHints = showRuleHints && !_isMiniMode && !string.Equals(_activeScaleClass, "crisis-panel--compact", StringComparison.Ordinal);
        string transientStatus = GetTimedStatusMessage();
        int selectedSeverity = -1;

        if (_activeCrises.Count > 0)
        {
            int selectedIndexForSummary = Mathf.Clamp(_selectedCrisisIndex, 0, _activeCrises.Count - 1);
            VillageCrisisState selectedForSummary = _activeCrises[selectedIndexForSummary];
            if (selectedForSummary != null)
            {
                selectedSeverity = Mathf.Clamp(selectedForSummary.severity, 0, 100);
            }
        }

        if (_titleLabel != null)
        {
            if (_activeCrises.Count > 0)
            {
                int titleIndex = Mathf.Clamp(_selectedCrisisIndex, 0, _activeCrises.Count - 1);
                VillageCrisisState selectedForTitle = _activeCrises[titleIndex];
                bool criticalForTitle = selectedForTitle != null && selectedForTitle.severity >= criticalSeverityThreshold;
                _titleLabel.text = criticalForTitle
                    ? $"Crisis - {selectedForTitle.type} (CRITICAL)"
                    : $"Crisis - {selectedForTitle.type}";

                if (criticalForTitle)
                {
                    _titleLabel.AddToClassList("crisis-title--critical");
                }
                else
                {
                    _titleLabel.RemoveFromClassList("crisis-title--critical");
                }
            }
            else
            {
                _titleLabel.text = "Crisis";
                _titleLabel.RemoveFromClassList("crisis-title--critical");
            }
        }

        if (_summaryLabel != null)
        {
            string severityText = selectedSeverity >= 0 ? $"{selectedSeverity}/100" : "-";
            _summaryLabel.text = $"Health {_stability}/100 | Problems {_activeCrises.Count} | Critical {criticalCount} | Severity {severityText}";
        }

        if (_gateLabel != null)
        {
            if (gateOpen)
            {
                _gateLabel.text = disconnectedVillages > 0
                    ? $"To win: connect {disconnectedVillages} village(s) to the city."
                    : "Crisis goal complete. Finish remaining win goals.";
            }
            else
            {
                _gateLabel.text = disconnectedVillages > 0
                    ? $"To win: resolve CRITICAL crises and connect {disconnectedVillages} village(s)."
                    : "To win: resolve all CRITICAL crises.";
            }

            if (gateOpen)
            {
                _gateLabel.RemoveFromClassList("crisis-text--critical");
            }
            else
            {
                _gateLabel.AddToClassList("crisis-text--critical");
            }
        }

        if (_spawnRuleLabel != null)
        {
            string rule = allowLongHints
                ? $"Health loss now: -{_lastComputedStabilityDrain}/turn from crises. If Health reaches 0, you lose. CRITICAL can spread ({(spreadChance * 100f):0}%/turn). Next guaranteed crisis in {_turnsUntilGuaranteedSpawn} turn(s)."
                : $"Health loss: -{_lastComputedStabilityDrain}/turn (lose at 0). Next guaranteed crisis in {_turnsUntilGuaranteedSpawn} turn(s).";

            _spawnRuleLabel.text = rule;
        }

        if (_activeCrises.Count == 0)
        {
            if (_selectedLabel != null)
            {
                _selectedLabel.text = string.Empty;
                _selectedLabel.RemoveFromClassList("crisis-text--critical");
                _selectedLabel.style.display = DisplayStyle.None;
            }

            if (_woodCostLabel != null)
            {
                _woodCostLabel.text = "x-";
            }

            if (_stoneCostLabel != null)
            {
                _stoneCostLabel.text = "x-";
            }

            if (_actionCostLabel != null)
            {
                _actionCostLabel.text = responseActionCost == 1 ? "1 turn" : $"{responseActionCost} turns";
            }

            if (_typeEffectLabel != null)
            {
                _typeEffectLabel.text = "Select a crisis and press Respond.";
            }

            if (_selectedHintLabel != null)
            {
                _selectedHintLabel.text = string.IsNullOrWhiteSpace(transientStatus)
                    ? "No active problem right now."
                    : transientStatus;
            }

            if (_resolveHintLabel != null)
            {
                _resolveHintLabel.text = "Connect villages to the city for bonus response power.";
            }
        }
        else
        {
            int selectedIndex = Mathf.Clamp(_selectedCrisisIndex, 0, _activeCrises.Count - 1);
            VillageCrisisState selected = _activeCrises[selectedIndex];
            int[] selectedCost = BuildResponseResourceCost(selected);
            bool selectedCritical = selected.severity >= criticalSeverityThreshold;
            bool selectedConnected = IsVillageConnectedToCity(selected.coordinate);
            int responsePower = Mathf.Max(1, Mathf.RoundToInt(baseResponsePower * (selectedConnected ? 1f : disconnectedResponsePowerMultiplier)))
                + (selectedConnected ? extraResponsePowerWhenConnected : 0);
            int severityStage = GetSeverityStage(selected.severity);

            if (_selectedLabel != null)
            {
                _selectedLabel.text = string.Empty;
                _selectedLabel.style.display = DisplayStyle.None;
                if (selectedCritical)
                {
                    _selectedLabel.AddToClassList("crisis-text--critical");
                }
                else
                {
                    _selectedLabel.RemoveFromClassList("crisis-text--critical");
                }
            }

            if (_typeEffectLabel != null)
            {
                _typeEffectLabel.text = GetCrisisTypeEffectText(selected);
            }

            if (_woodCostLabel != null)
            {
                _woodCostLabel.text = $"x{selectedCost[(int)ResourceType.Wood]}";
            }

            if (_stoneCostLabel != null)
            {
                _stoneCostLabel.text = $"x{selectedCost[(int)ResourceType.Stone]}";
            }

            if (_actionCostLabel != null)
            {
                _actionCostLabel.text = responseActionCost == 1 ? "1 turn" : $"{responseActionCost} turns";
            }

            if (_selectedHintLabel != null)
            {
                string stageText = severityStage switch
                {
                    3 => "CRITICAL",
                    2 => "DANGER",
                    1 => "WARNING",
                    _ => "STABLE"
                };

                string baseHint = allowLongHints
                    ? $"Location: highlighted village. Severity stage: {stageText}."
                    : $"Severity stage: {stageText}.";

                _selectedHintLabel.text = string.IsNullOrWhiteSpace(transientStatus)
                    ? baseHint
                    : $"{baseHint} {transientStatus}";
            }

            if (_resolveHintLabel != null)
            {
                _resolveHintLabel.text =
                    selectedConnected
                        ? $"Connected: +{extraResponsePowerWhenConnected} response power and +{connectedResolveStabilityBonus} Health on resolve. Respond now lowers severity by {responsePower}."
                        : disconnectedResolveFloor > 0
                            ? $"Disconnected: response lowers severity by {responsePower}, but cannot resolve below {disconnectedResolveFloor}. Connect village to finish crisis."
                            : $"Connect this village to the city for +{extraResponsePowerWhenConnected} response power.";
            }
        }

        if (_controlsLabel != null)
        {
            _controlsLabel.text = "Controls: Prev/Next switch crisis, Respond handles selected crisis.";
        }

        bool hasActiveCrises = _activeCrises.Count > 0;
        if (_prevButton != null)
        {
            _prevButton.SetEnabled(hasActiveCrises);
            _prevButton.text = _isMiniMode ? "<" : "Prev";
        }

        if (_nextButton != null)
        {
            _nextButton.SetEnabled(hasActiveCrises);
            _nextButton.text = _isMiniMode ? ">" : "Next";
        }

        if (_respondButton != null)
        {
            _respondButton.SetEnabled(hasActiveCrises && turns != null && turns.State == Turns.TurnState.PlayerTurn);
            _respondButton.text = _isMiniMode ? "Help" : "Respond";
        }

        if (_collapseButton != null)
        {
            _collapseButton.text = _isMiniMode ? "Show" : "Hide";
        }

        if (_pressureLabel != null)
        {
            _pressureLabel.text = $"Health loss this turn: -{_lastComputedStabilityDrain} (you lose at 0 Health)";
        }

        if (compactOverlay)
        {
            if (_selectedHintLabel != null)
            {
                _selectedHintLabel.style.display = allowLongHints ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_controlsLabel != null)
            {
                _controlsLabel.style.display = DisplayStyle.None;
            }

            if (_pressureLabel != null)
            {
                _pressureLabel.style.display = DisplayStyle.None;
            }
        }
        else
        {
            if (_selectedHintLabel != null)
            {
                _selectedHintLabel.style.display = allowLongHints ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_controlsLabel != null)
            {
                _controlsLabel.style.display = DisplayStyle.None;
            }

            if (_pressureLabel != null)
            {
                _pressureLabel.style.display = DisplayStyle.None;
            }
        }

        ApplyMiniModeVisualState();
    }

    private void OnPreviousButtonClicked()
    {
        if (InGameGenerationMenu.IsAnyMenuOpen)
        {
            return;
        }

        SelectPreviousCrisis();
    }

    private void OnNextButtonClicked()
    {
        if (InGameGenerationMenu.IsAnyMenuOpen)
        {
            return;
        }

        SelectNextCrisis();
    }

    private void OnRespondButtonClicked()
    {
        if (InGameGenerationMenu.IsAnyMenuOpen)
        {
            return;
        }

        TryRespondToSelectedCrisis();
    }

    private void OnCollapseButtonClicked()
    {
        if (InGameGenerationMenu.IsAnyMenuOpen)
        {
            return;
        }

        _isMiniMode = !_isMiniMode;
        ApplyMiniModeVisualState();
        ApplyResponsiveOverlay(force: true);
        RefreshOverlayText();
    }

    private void ApplyMiniModeVisualState()
    {
        if (!_overlayUiInitialized)
        {
            return;
        }

        if (_overlayPanel != null)
        {
            if (_isMiniMode && _isPhonePortraitLayout)
            {
                _overlayPanel.style.minHeight = 0f;
                _overlayPanel.style.height = StyleKeyword.Null;
                _overlayPanel.style.maxHeight = StyleKeyword.Null;
            }
            else
            {
                _overlayPanel.style.minHeight = StyleKeyword.Null;
                _overlayPanel.style.height = StyleKeyword.Null;
                _overlayPanel.style.maxHeight = StyleKeyword.Null;
            }
        }

        bool showDetails = !_isMiniMode;

        if (_detailsTop != null)
        {
            _detailsTop.style.display = showDetails ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (_detailsMiddle != null)
        {
            _detailsMiddle.style.display = showDetails ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (_detailsBottom != null)
        {
            _detailsBottom.style.display = showDetails ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (_costRow != null)
        {
            _costRow.style.display = DisplayStyle.Flex;
        }
    }

    private string GetCrisisTypeEffectText(VillageCrisisState crisis)
    {
        if (crisis == null)
        {
            return "Any active crisis drains health each turn.";
        }

        int stage = GetSeverityStage(crisis.severity);
        switch (crisis.type)
        {
            case VillageCrisisType.Health:
                return stage >= 2
                    ? "Health: response costs are elevated at this severity and Health loss pressure is high."
                    : "Health: response cost rises as severity climbs.";
            case VillageCrisisType.Infrastructure:
                int infrastructureIncreasePercent = Mathf.RoundToInt((GetInfrastructureBuildCostMultiplier() - 1f) * 100f);
                return infrastructureIncreasePercent > 0
                    ? $"Infrastructure: roads/buildings cost +{infrastructureIncreasePercent}% now."
                    : "Infrastructure: no extra road/building cost right now.";
            case VillageCrisisType.Cultural:
                return stage >= 2
                    ? "Cultural: still cheaper than others, but prolonged neglect adds sustained map pressure."
                    : "Cultural: usually the cheapest to respond to.";
            case VillageCrisisType.Nature:
                return stage >= 2
                    ? "Nature: wood-heavy responses are now expensive."
                    : "Nature: tends to require more wood as severity rises.";
            case VillageCrisisType.Social:
                return stage >= 2
                    ? "Social: high severity adds stronger wood pressure and can snowball quickly."
                    : "Social: high severity can add extra wood cost.";
            default:
                return "Any active crisis drains health each turn.";
        }
    }

    private int GetSeverityStage(int severity)
    {
        int clampedSeverity = Mathf.Clamp(severity, 0, 100);
        int warningThreshold = Mathf.Clamp(Mathf.Min(warningSeverityThreshold, dangerSeverityThreshold - 1), 1, 99);
        int dangerThreshold = Mathf.Clamp(Mathf.Max(dangerSeverityThreshold, warningThreshold + 1), 1, Mathf.Max(1, criticalSeverityThreshold - 1));

        if (clampedSeverity >= criticalSeverityThreshold)
        {
            return 3;
        }

        if (clampedSeverity >= dangerThreshold)
        {
            return 2;
        }

        if (clampedSeverity >= warningThreshold)
        {
            return 1;
        }

        return 0;
    }

    private string BuildSeverityMilestoneMessage(VillageCrisisState crisis, int stage)
    {
        if (crisis == null)
        {
            return string.Empty;
        }

        return crisis.type switch
        {
            VillageCrisisType.Infrastructure => stage switch
            {
                3 => "CRITICAL Infrastructure: building costs are heavily increased.",
                2 => "Danger Infrastructure: construction costs are rising fast.",
                _ => "Warning Infrastructure: build costs are starting to climb."
            },
            VillageCrisisType.Health => stage switch
            {
                3 => "CRITICAL Health: response costs and Health loss pressure are severe.",
                2 => "Danger Health: response costs increased.",
                _ => "Warning Health: prepare additional response resources."
            },
            VillageCrisisType.Nature => stage switch
            {
                3 => "CRITICAL Nature: wood-heavy containment required.",
                2 => "Danger Nature: wood cost pressure is now high.",
                _ => "Warning Nature: response wood demand increased."
            },
            VillageCrisisType.Social => stage switch
            {
                3 => "CRITICAL Social: crisis can destabilize quickly.",
                2 => "Danger Social: resolve soon to avoid snowballing.",
                _ => "Warning Social: pressure is increasing."
            },
            VillageCrisisType.Cultural => stage switch
            {
                3 => "CRITICAL Cultural: prolonged neglect is now dangerous.",
                2 => "Danger Cultural: Health loss pressure is mounting.",
                _ => "Warning Cultural: address before it escalates."
            },
            _ => "Crisis escalated to a new severity tier."
        };
    }

    private string GetTimedStatusMessage()
    {
        if (string.IsNullOrWhiteSpace(_responseStatusMessage))
        {
            return string.Empty;
        }

        if (responseStatusDuration <= 0f)
        {
            return _responseStatusMessage;
        }

        return Time.time - _responseStatusTimestamp <= responseStatusDuration
            ? _responseStatusMessage
            : string.Empty;
    }

    private void ApplyDemoFriendlyBalancePreset()
    {
        maxActiveCrises = Mathf.Clamp(maxActiveCrises, 1, 2);
        baseSpawnChance = 0.14f;
        guaranteedSpawnIntervalTurns = Mathf.Max(6, guaranteedSpawnIntervalTurns);
        severityGainPerTurnMin = 2;
        severityGainPerTurnMax = 6;
        spreadChance = 0.025f;
        criticalSeverityThreshold = Mathf.Max(90, criticalSeverityThreshold);
        postResolveSpawnGraceTurns = Mathf.Max(2, postResolveSpawnGraceTurns);
        postResolveSpreadGraceTurns = Mathf.Max(1, postResolveSpreadGraceTurns);
        warningSeverityThreshold = Mathf.Clamp(warningSeverityThreshold, 45, 65);
        dangerSeverityThreshold = Mathf.Clamp(dangerSeverityThreshold, 70, 85);
        deterministicPerMapSeed = true;

        responseActionCost = Mathf.Max(1, responseActionCost);
        baseResponsePower = Mathf.Max(55, baseResponsePower);
        extraResponsePowerWhenConnected = Mathf.Max(25, extraResponsePowerWhenConnected);
        disconnectedResponsePowerMultiplier = Mathf.Clamp(disconnectedResponsePowerMultiplier, 0.65f, 0.85f);
        disconnectedResolveFloor = Mathf.Clamp(disconnectedResolveFloor, 16, 30);
        connectedResolveStabilityBonus = Mathf.Max(4, connectedResolveStabilityBonus);

        passiveStabilityDrain = Mathf.Clamp(passiveStabilityDrain, 0, 1);
        criticalCrisisStabilityDrain = Mathf.Clamp(criticalCrisisStabilityDrain, 1, 3);
        resolveStabilityReward = Mathf.Max(10, resolveStabilityReward);
        stabilityTurnPenaltyOnCritical = 0;
    }
}