using UnityEngine;
using UnityEngine.Events;
using Sounds;
using ScoreSystem;

namespace QuestSystem
{
    public class Quest : MonoBehaviour
    {
        public string questName;
        public string description;
        [SerializeField] private Turns turns;
        [SerializeField] private UnityEvent onQuestCompleted;
        [SerializeField] private SFXManager sfxManager;
        [SerializeField] private string questCompletedSfxName = "quest_completed_sfx";
        [SerializeField] private int[] rewardResources;
        [SerializeField] private int[] resourceCosts;
        [SerializeField] private LevelScore levelScore;
        [SerializeField] private int questScore = 100;

        private bool _isCompleted;
        public bool IsCompleted => _isCompleted;

        private void Awake()
        {
            if (sfxManager == null)
            {
                sfxManager = FindAnyObjectByType<SFXManager>();
            }

            if (levelScore == null)
            {
                levelScore = FindAnyObjectByType<LevelScore>();
            }

            onQuestCompleted ??= new UnityEvent();
            onQuestCompleted.RemoveListener(HandleQuestCompletedScore);
            onQuestCompleted.AddListener(HandleQuestCompletedScore);

            // Ensure completion always routes through the UnityEvent to the shared SFX manager.
            // onQuestCompleted ??= new UnityEvent();
            // onQuestCompleted.RemoveListener(PlayQuestCompletedSfx);
            // onQuestCompleted.AddListener(PlayQuestCompletedSfx);
        }

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

            if (!turns.CanAffordResources(resourceCosts) || !turns.CanAffordTurns(resourceCosts[(int)ResourceType.Turn]))
            {
                return false;
            }

            turns.TrySpendResources(resourceCosts);
            turns.TrySpendTurns(resourceCosts[0]);

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

            turns.AddPerTurnResources(rewardResources);
            _isCompleted = true;

            onQuestCompleted?.Invoke();
        }

        private void HandleQuestCompletedScore()
        {
            if (levelScore == null)
            {
                levelScore = FindAnyObjectByType<LevelScore>();
            }

            if (levelScore == null)
            {
                Debug.LogWarning("Quest: No LevelScore found in scene.", this);
                return;
            }

            levelScore.AddQuestScore(questScore);
        }

        public void PlayQuestCompletedSfx()
        {
            if (sfxManager == null)
            {
                sfxManager = FindAnyObjectByType<SFXManager>();
            }

            if (sfxManager == null)
            {
                Debug.LogWarning("Quest: No SFXManager found in scene.", this);
                return;
            }

            sfxManager.PlaySfx(questCompletedSfxName);
        }
    }
}
