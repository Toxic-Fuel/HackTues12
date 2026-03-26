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
    [SerializeField] private int woodPerTurn = 1;
    [SerializeField] private int stonePerTurn = 1;

    [Header("Starting Resources")]
    [SerializeField] private int startingWood = 0;
    [SerializeField] private int startingStone = 0;

    [Header("Input")]
    [SerializeField] private InputActionReference endTurnAction;

    [Header("UI")]
    [SerializeField] private ResourceTurnsUI resourceTurnsUI;

    private int _currentTurn;
    private int _currentWood;
    private int _currentStone;

    private int CurrentTurn
    {
        get => _currentTurn;
        set
        {
            _currentTurn = value;
            resourceTurnsUI.UpdateTexts(CurrentWood, CurrentStone, RemainingTurns);
        }
    }

    public int RemainingTurns { get; private set; }
    public int ActionsRemaining { get; private set; }

    public int CurrentWood
    {
        get => _currentWood;
        private set => _currentWood = value;
    }

    public int CurrentStone
    {
        get => _currentStone;
        private set => _currentStone = value;
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
    }

    private void OnDisable()
    {
        if (endTurnAction == null || endTurnAction.action == null)
        {
            return;
        }

        endTurnAction.action.performed -= OnEndTurnPerformed;
        endTurnAction.action.Disable();
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
        RemainingTurns = Mathf.Max(0, RemainingTurns - 1);

        TurnEnded?.Invoke(this);

        if (RemainingTurns <= 0)
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
    }

    public void TriggerWin()
    {
        if (State == TurnState.Win || State == TurnState.Lose)
        {
            return;
        }

        State = TurnState.Win;
        EncounterWon?.Invoke(this);
    }

    private void BeginPlayerTurn()
    {
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
        CurrentWood += Mathf.Max(0, woodPerTurn);
        CurrentStone += Mathf.Max(0, stonePerTurn);
        ResourcesGained?.Invoke(this);
    }

    private void SetLoseState()
    {
        State = TurnState.Lose;
        EncounterLost?.Invoke(this);

    }
}