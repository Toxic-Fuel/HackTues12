using UnityEngine;

namespace QuestSystem
{
    public class Quest : MonoBehaviour
    {
        public string questName;
        public string description;
        [SerializeField] private Turns turns;
        [SerializeField] private int rewardWoodPerTurn, rewardStonePerTurn;
        [SerializeField] private int woodCost, stoneCost;

        private bool _isCompleted;

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

            if (!turns.TrySpendResources(woodCost, stoneCost))
            {
                return false;
            }

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
        }
    }
}
