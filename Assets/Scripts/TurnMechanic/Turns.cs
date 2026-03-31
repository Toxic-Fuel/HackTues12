using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class Turns : MonoBehaviour
{
    public enum TurnState
    {
        NotStarted,
        PlayerTurn,
        ResolvingTurn,
        Win,
        Lose
    }

    [Header("Turn Settings")]
    [SerializeField] private int actionsPerTurn = 1;
    [SerializeField] private bool autoStartOnAwake = true;
    [SerializeField] private bool autoEndWhenOutOfActions = true;

    [Header("Resource Income Per Turn")]
    [SerializeField, Min(0)] private int[] resourcesPerTurn;
    
    [Header("Starting Resources")]
    private int[] startingResources;
    [SerializeField, Min(0)] private int[] startingResourceCount;
    [Header("Input")]
    [SerializeField] private InputActionReference endTurnAction;

    [Header("UI")]
    [SerializeField] private ResourceTurnsUI resourceTurnsUI;
    [SerializeField] private TileBuilding tileBuilding;

    [Header("Win Lose Con")]
    [SerializeField] private WinOrLose winOrLose;

    [Header("Connected Village Bonus Per Turn")]
    [SerializeField, Min(0)] private int[] resourcePerConnectedVillage;

    [Header("Connected Village Reward")]
    [SerializeField, Min(0)] private int turnsPerConnectedVillage = 5;
    
    private int[] _currentResources;
    private int _lastRewardedConnectedVillageCount;
    public int RemainingTurns { get; private set; }
    public int ActionsRemaining { get; private set; }

    public int[] CurrentResources
    {
        get => _currentResources;
        set
        {
            _currentResources = value;
            RefreshUI();
        }
    }


    public TurnState State { get; private set; } = TurnState.NotStarted;

    public bool CanTakeAction => State == TurnState.PlayerTurn && ActionsRemaining > 0;

    public bool CanAffordResources(int[] cost)
    {
        foreach (var resource in cost) if(resource < 0) return false;

        bool[] hasResources = new bool[cost.Length];
        for (int resourceIndex = 0; resourceIndex < cost.Length; ++resourceIndex)
        {
            hasResources[resourceIndex] = _currentResources[resourceIndex] >= cost[resourceIndex];
            if(!hasResources[resourceIndex]) return false;
        }
        return true;
    }

    public bool CanAffordTurns(int turnCost)
    {
        if (turnCost < 0)
        {
            return false;
        }

        if (State == TurnState.Win || State == TurnState.Lose)
        {
            return false;
        }

        return CurrentResources[(int)ResourceType.Turn] >= turnCost;
    }

    public bool TrySpendResources(int[] cost)
    {
        if (!CanAffordResources(cost))
        {
            return false;
        }

        for (int resourceIndex = 0; resourceIndex < cost.Length; ++resourceIndex)
        {
            CurrentResources[resourceIndex] -= cost[resourceIndex];
        }
        ResourcesGained?.Invoke(this);
        return true;
    }

    public bool TrySpendTurns(int turnCost)
    {
        if (turnCost < 0)
        {
            return false;
        }

        if (!CanAffordTurns(turnCost))
        {
            return false;
        }

        RemainingTurns = Mathf.Max(0, RemainingTurns - turnCost);
        RefreshUI();

        if (RemainingTurns == 0)
        {
            SetLoseState();
        }

        return true;
    }

    public event Action<Turns> EncounterStarted;
    public event Action<Turns> TurnStarted;
    public event Action<Turns> ActionSpent;
    public event Action<Turns> TurnEnded;
    public event Action<Turns> EncounterWon;
    public event Action<Turns> EncounterLost;
    public event Action<Turns> ResourcesGained;

    private void OnEnable()
    {
        if (endTurnAction == null || endTurnAction.action == null)
        {
            return;
        }

        endTurnAction.action.Enable();
        endTurnAction.action.performed += OnEndTurnPerformed;

        EnsureTileBuildingReference();
        if (tileBuilding != null)
        {
            tileBuilding.RoadPlaced -= OnRoadPlaced;
            tileBuilding.RoadPlaced += OnRoadPlaced;
        }
    }

    private void OnDisable()
    {
        if (endTurnAction == null || endTurnAction.action == null)
        {
            return;
        }

        endTurnAction.action.performed -= OnEndTurnPerformed;
        endTurnAction.action.Disable();

        if (tileBuilding != null)
        {
            tileBuilding.RoadPlaced -= OnRoadPlaced;
        }
    }

    private void Awake()
    {
        if (autoStartOnAwake)
        {
            StartEncounter();
        }
    }

    private void OnEndTurnPerformed(InputAction.CallbackContext context)
    {
        if (State != TurnState.PlayerTurn)
        {
            return;
        }
        Debug.Log($"Current wood: {CurrentResources[(int)ResourceType.Wood]}, Current stone: {(int)ResourceType.Stone}, Actions remaining: {ActionsRemaining}");
        EndTurn();
    }

    public void StartEncounter()
    {
        if (actionsPerTurn <= 0)
        {
            Debug.LogError("Turns: Actions per turn must be greater than 0.", this);
            return;
        }

        if (startingResources[(int)ResourceType.Turn] <= 0)
        {
            Debug.LogError("Turns: Starting turns must be greater than 0.", this);
            return;
        }

        CurrentResources[(int)ResourceType.Turn] = 1;
        RemainingTurns = startingResources[(int)ResourceType.Turn];
        for(int resourceIndex = 0; resourceIndex < startingResourceCount.Length; ++resourceIndex)
        {
            CurrentResources[resourceIndex] = Math.Max(0, startingResourceCount[resourceIndex]);
        }
        State = TurnState.PlayerTurn;

        // Baseline existing connections so rewards are only for newly connected villages.
        _lastRewardedConnectedVillageCount = GetConnectedVillageCount();

        EncounterStarted?.Invoke(this);
        BeginPlayerTurn();
    }

    public bool TrySpendAction(int amount = 1)
    {
        if (State != TurnState.PlayerTurn)
        {
            return false;
        }

        if (amount <= 0 || amount > ActionsRemaining)
        {
            return false;
        }

        ActionsRemaining -= amount;
        ActionSpent?.Invoke(this);

        if (autoEndWhenOutOfActions && ActionsRemaining == 0)
        {
            EndTurn();
        }

        return true;
    }

    public bool EndTurn()
    {
        if (State != TurnState.PlayerTurn)
        {
            return false;
        }

        State = TurnState.ResolvingTurn;

        if (RemainingTurns > 0)
        {
            RemainingTurns--;
        }

        RemainingTurns = Mathf.Max(0, RemainingTurns);
        RefreshUI();

        TurnEnded?.Invoke(this);

        if (RemainingTurns == 0)
        {
            SetLoseState();
            return true;
        }

        CurrentResources[(int)ResourceType.Turn] += 1;
        BeginPlayerTurn();
        return true;
    }


    public void AddPerTurnResources(int woodBonus, int stoneBonus)
    {
        for (int resourceIndex = 0; resourceIndex < resourcesPerTurn.Length; ++resourceIndex)
        {
            resourcesPerTurn[resourceIndex] += Mathf.Max(0, woodBonus);
        }
        RefreshUI();
    }

    public void TriggerWin()
    {
        if (State == TurnState.Win || State == TurnState.Lose)
        {
            return;
        }

        Debug.Log("Encounter won!");
        State = TurnState.Win;
        EncounterWon?.Invoke(this);

        if (winOrLose != null)
        {
            winOrLose.CheckWinOrLose(State);
        }
    }

    private void BeginPlayerTurn()
    {
        if (State == TurnState.Win || State == TurnState.Lose)
        {
            return;
        }

        if (winOrLose != null)
        {
            winOrLose.CheckWinOrLose(State);
        }

        if (State == TurnState.Win || State == TurnState.Lose)
        {
            return;
        }

        ActionsRemaining = actionsPerTurn;
        GrantTurnResources();
        State = TurnState.PlayerTurn;
        TurnStarted?.Invoke(this);
    }

    private int[] GetVillageBonuses()
    {
        int connectedVillageCount = GetConnectedVillageCount();
        int[] villageBonuses =  new int[CurrentResources.Length];
        for (int resourceIndex = 0; resourceIndex < villageBonuses.Length; ++resourceIndex)
        {
            villageBonuses[resourceIndex] = connectedVillageCount * Mathf.Max(0, resourcePerConnectedVillage[resourceIndex]);
        }
        return villageBonuses;
    }
    private void GrantTurnResources()
    {
        var villageBonuses = GetVillageBonuses();
        for (int resourceIndex = 0; resourceIndex < CurrentResources.Length; ++resourceIndex)
        {
            CurrentResources[resourceIndex] += Mathf.Max(0, resourcesPerTurn[resourceIndex]) + villageBonuses[resourceIndex];
        }
        ResourcesGained?.Invoke(this);
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (resourceTurnsUI == null)
        {
            return;
        }

        var villageBonuses = GetVillageBonuses();
        var displayedResourcePerTurn = new int[villageBonuses.Length];
        for (int resourceIndex = 0; resourceIndex < CurrentResources.Length; ++resourceIndex)
        {
            displayedResourcePerTurn[resourceIndex] =  Mathf.Max(0, resourcesPerTurn[resourceIndex]) + villageBonuses[resourceIndex];
        }
        resourceTurnsUI.UpdateTexts(CurrentWood, CurrentStone, RemainingTurns, displayedWoodPerTurn, displayedStonePerTurn);
    }

    private int GetConnectedVillageCount()
    {
        EnsureTileBuildingReference();
        return tileBuilding != null ? Mathf.Max(0, tileBuilding.GetConnectedVillageCount()) : 0;
    }

    private void EnsureTileBuildingReference()
    {
        if (tileBuilding == null)
        {
            tileBuilding = FindAnyObjectByType<TileBuilding>();
        }
    }

    private void OnRoadPlaced()
    {
        int connectedVillageCount = GetConnectedVillageCount();
        int newlyConnectedVillages = Mathf.Max(0, connectedVillageCount - _lastRewardedConnectedVillageCount);

        if (newlyConnectedVillages > 0)
        {
            RemainingTurns += newlyConnectedVillages * Mathf.Max(0, turnsPerConnectedVillage);
            _lastRewardedConnectedVillageCount = connectedVillageCount;
        }

        RefreshUI();
    }

    public void SetLoseState()
    {
        if (State == TurnState.Win || State == TurnState.Lose)
        {
            return;
        }

        State = TurnState.Lose;
        EncounterLost?.Invoke(this);
        Debug.Log("Encounter lost!");

        if (winOrLose != null)
        {
            winOrLose.CheckWinOrLose(State);
        }
    }
}