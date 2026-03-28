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
    [SerializeField] private int startingTurns = 20;
    [SerializeField] private int actionsPerTurn = 1;
    [SerializeField] private bool autoStartOnAwake = true;
    [SerializeField] private bool autoEndWhenOutOfActions = true;

    [Header("Resource Income Per Turn")]
    [SerializeField, Min(0)] private int woodPerTurn = 1;
    [SerializeField, Min(0)] private int stonePerTurn = 1;

    [Header("Starting Resources")]
    [SerializeField, Min(0)] private int startingWood = 0;
    [SerializeField, Min(0)] private int startingStone = 0;

    [Header("Input")]
    [SerializeField] private InputActionReference endTurnAction;

    [Header("UI")]
    [SerializeField] private ResourceTurnsUI resourceTurnsUI;
    [SerializeField] private TileBuilding tileBuilding;

    [Header("Win Lose Con")]
    [SerializeField] private WinOrLose winOrLose;

    [Header("Connected Village Bonus Per Turn")]
    [SerializeField, Min(0)] private int woodPerConnectedVillage = 1;
    [SerializeField, Min(0)] private int stonePerConnectedVillage = 1;

    [Header("Connected Village Reward")]
    [SerializeField, Min(0)] private int turnsPerConnectedVillage = 5;

    private int _currentTurn;
    private int _currentWood;
    private int _currentStone;
    private int _lastRewardedConnectedVillageCount;

    private int CurrentTurn
    {
        get => _currentTurn;
        set
        {
            _currentTurn = value;
            RefreshUI();
        }
    }

    public int RemainingTurns { get; private set; }
    public int ActionsRemaining { get; private set; }

    public int CurrentWood
    {
        get => _currentWood;
        set
        {
            _currentWood = value;
            RefreshUI();
        }
    }

    public int CurrentStone
    {
        get => _currentStone;
        set
        {
            _currentStone = value;
            RefreshUI();
        }
    }

    public TurnState State { get; private set; } = TurnState.NotStarted;

    public bool CanTakeAction => State == TurnState.PlayerTurn && ActionsRemaining > 0;

    public bool CanAffordResources(int woodCost, int stoneCost)
    {
        if (woodCost < 0 || stoneCost < 0)
        {
            return false;
        }

        return CurrentWood >= woodCost && CurrentStone >= stoneCost;
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

        return RemainingTurns >= turnCost;
    }

    public bool TrySpendResources(int woodCost, int stoneCost)
    {
        if (!CanAffordResources(woodCost, stoneCost))
        {
            return false;
        }

        CurrentWood -= woodCost;
        CurrentStone -= stoneCost;
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
        Debug.Log($"Current wood: {CurrentWood}, Current stone: {CurrentStone}, Actions remaining: {ActionsRemaining}");
        EndTurn();
    }

    public void StartEncounter()
    {
        if (actionsPerTurn <= 0)
        {
            Debug.LogError("Turns: Actions per turn must be greater than 0.", this);
            return;
        }

        if (startingTurns <= 0)
        {
            Debug.LogError("Turns: Starting turns must be greater than 0.", this);
            return;
        }

        CurrentTurn = 1;
        RemainingTurns = startingTurns;
        CurrentWood = Mathf.Max(0, startingWood);
        CurrentStone = Mathf.Max(0, startingStone);
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

        CurrentTurn += 1;
        BeginPlayerTurn();
        return true;
    }


    public void AddPerTurnResources(int woodBonus, int stoneBonus)
    {
        woodPerTurn += Mathf.Max(0, woodBonus);
        stonePerTurn += Mathf.Max(0, stoneBonus);
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

    private void GrantTurnResources()
    {
        int connectedVillageCount = GetConnectedVillageCount();
        int villageWoodBonus = connectedVillageCount * Mathf.Max(0, woodPerConnectedVillage);
        int villageStoneBonus = connectedVillageCount * Mathf.Max(0, stonePerConnectedVillage);

        CurrentWood += Mathf.Max(0, woodPerTurn) + villageWoodBonus;
        CurrentStone += Mathf.Max(0, stonePerTurn) + villageStoneBonus;
        ResourcesGained?.Invoke(this);
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (resourceTurnsUI == null)
        {
            return;
        }

        int connectedVillageCount = GetConnectedVillageCount();
        int villageWoodBonus = connectedVillageCount * Mathf.Max(0, woodPerConnectedVillage);
        int villageStoneBonus = connectedVillageCount * Mathf.Max(0, stonePerConnectedVillage);

        int displayedWoodPerTurn = Mathf.Max(0, woodPerTurn) + villageWoodBonus;
        int displayedStonePerTurn = Mathf.Max(0, stonePerTurn) + villageStoneBonus;
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