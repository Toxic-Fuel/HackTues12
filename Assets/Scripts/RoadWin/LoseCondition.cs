using UnityEngine;

public class LoseCondition : MonoBehaviour
{
    [SerializeField] private Turns turns;

    private void Awake()
    {
        ResolveActiveTurns();
    }

    private void Update()
    {
        if (!ResolveActiveTurns())
        {
            return;
        }

        if (turns.State != Turns.TurnState.PlayerTurn && turns.State != Turns.TurnState.ResolvingTurn)
        {
            return;
        }

        if (turns.RemainingTurns <= 0)
        {
            turns.SetLoseState();
        }
    }

    private bool ResolveActiveTurns()
    {
        if (turns != null && (turns.State == Turns.TurnState.PlayerTurn || turns.State == Turns.TurnState.ResolvingTurn))
        {
            return true;
        }

        Turns[] turnSystems = FindObjectsByType<Turns>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < turnSystems.Length; i++)
        {
            Turns candidate = turnSystems[i];
            if (candidate == null)
            {
                continue;
            }

            if (candidate.State == Turns.TurnState.PlayerTurn || candidate.State == Turns.TurnState.ResolvingTurn)
            {
                turns = candidate;
                return true;
            }
        }

        return turns != null;
    }
}
