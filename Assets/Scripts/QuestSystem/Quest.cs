using UnityEngine;

namespace QuestSystem
{
    public class Quest : MonoBehaviour
    {
        public string questName;
        public string description;
        [SerializeField] private Turns turns;
        [SerializeField] private AudioSource completionAudioSource;
        [SerializeField] private int rewardWoodPerTurn, rewardStonePerTurn;
        [SerializeField] private int woodCost, stoneCost, turnCost;

        private bool _isCompleted;
        public bool IsCompleted => _isCompleted;

        public void SetTurns(Turns turnsReference)
        {
            if (turnsReference == null)
            {
                return;
            }

            turns = turnsReference;
        }

        public bool PayResources()
        {
            if (_isCompleted)
            {
                return false;
            }

            if (turns == null)
            {
                Debug.LogError("Quest: Turns reference is missing.", this);
                return false;
            }

            if (!turns.CanAffordResources(woodCost, stoneCost) || !turns.CanAffordTurns(turnCost))
            {
                return false;
            }

            turns.TrySpendResources(woodCost, stoneCost);
            turns.TrySpendTurns(turnCost);

            OnQuestComplete();
            return true;
        }

        private void OnQuestComplete()
        {
            if (_isCompleted)
            {
                return;
            }

            if (turns == null)
            {
                Debug.LogError("Quest: Turns reference is missing.", this);
                return;
            }

            turns.AddPerTurnResources(rewardWoodPerTurn, rewardStonePerTurn);
            _isCompleted = true;

            if (completionAudioSource != null)
            {
                completionAudioSource.Play();
            }
        }
    }
}
