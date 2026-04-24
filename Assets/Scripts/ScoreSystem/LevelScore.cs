using System.Diagnostics;
using UnityEngine;

namespace ScoreSystem
{
    public enum ScoreType
    {
        Default,
        Time,
        Buildings,
        Crisis,
        Quest
    }

    public class LevelScore : MonoBehaviour
    {
        [Min(0)][SerializeField] private int scoreValue;
        [SerializeField] private float[] scoreMultipliers;
        [SerializeField] private float difficultyMultiplier = 1f;
        [SerializeField] private float turnMultiplier = 1f;
        [SerializeField, Min(1)] private int perfectTurns = 10;
        [SerializeField] private Turns turns;

        private readonly Stopwatch _timePassed = new Stopwatch();
        private int _turnsUsed;
        private bool _isEndScoreCalculated;

        public int ScoreValue
        {
            get => scoreValue;
            set => scoreValue = value;
        }

        public float TurnMultiplier => turnMultiplier;
        public float DifficultyMultiplier => difficultyMultiplier;

        public int GetCurrentScore()
        {
            return Mathf.Max(0, scoreValue);
        }

        public int GetCurrentSpeedRatingPercent()
        {
            int usedTurns = Mathf.Max(1, _turnsUsed);
            int targetTurns = Mathf.Max(1, perfectTurns);
            float liveTurnMultiplier = (float)targetTurns / usedTurns;
            return Mathf.Max(0, Mathf.RoundToInt(liveTurnMultiplier * 100f));
        }

        public int GetCurrentDifficultyRatingPercent()
        {
            return Mathf.Max(0, Mathf.RoundToInt(difficultyMultiplier * 100f));
        }

        public void AddToScore(float value)
        {
            AddToScore(value, ScoreType.Default);
        }

        public void AddToScore(float value, ScoreType type)
        {
            var multiplier = scoreMultipliers[(int)type];
            var delta = Mathf.RoundToInt(value * multiplier);
            scoreValue = Mathf.Max(0, scoreValue + delta);
        }

        // UnityEvent-friendly wrappers.
        public void AddDefaultScore(float value) => AddToScore(value, ScoreType.Default);
        public void AddTimeScore(float value) => AddToScore(value, ScoreType.Time);
        public void AddBuildingScore(float value) => AddToScore(value, ScoreType.Buildings);
        public void AddCrisisScore(float value) => AddToScore(value, ScoreType.Crisis);
        public void AddQuestScore(float value) => AddToScore(value, ScoreType.Quest);

        public void CalculateEndScore()
        {
            if (_isEndScoreCalculated)
            {
                return;
            }

            UpdateTurnMultiplier();

            var timePenalty = _timePassed.Elapsed.TotalSeconds;
            AddToScore(-(float)timePenalty, ScoreType.Time);
            ScoreValue = Mathf.RoundToInt(ScoreValue * difficultyMultiplier * turnMultiplier);
            _isEndScoreCalculated = true;
        }

        private void Start()
        {
            if (turns == null)
            {
                turns = FindAnyObjectByType<Turns>();
            }

            if (turns != null)
            {
                turns.TurnStarted -= OnTurnStarted;
                turns.TurnStarted += OnTurnStarted;
            }

            _timePassed.Start();
        }

        private void OnDisable()
        {
            if (turns != null)
            {
                turns.TurnStarted -= OnTurnStarted;
            }
        }

        private void OnTurnStarted(Turns _)
        {
            _turnsUsed++;
        }

        private void UpdateTurnMultiplier()
        {
            int usedTurns = Mathf.Max(1, _turnsUsed);
            int targetTurns = Mathf.Max(1, perfectTurns);

            // Faster-than-perfect gives >1.0, slower gives <1.0.
            turnMultiplier = (float)targetTurns / usedTurns;
        }
    }
}
