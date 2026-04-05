using System.Diagnostics;
using UnityEngine;

namespace ScoreSystem
{
    public enum ScoreType
    {
        Default,
        Time,
        Buildings
    }
    
    public class LevelScore : MonoBehaviour
    {
        [Min(0)] [SerializeField] private int scoreValue;
        [SerializeField] private float[] scoreMultipliers;
        private readonly Stopwatch _timePassed = new Stopwatch();
        [SerializeField] private float difficultyMultiplier = 1f;
        
        public int ScoreValue
        {
            get => scoreValue;
            set => scoreValue = value;
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
        
        public void CalculateEndScore()
        {
            var timePenalty = _timePassed.Elapsed.TotalSeconds;
            AddToScore(-(float)timePenalty, ScoreType.Time);
            ScoreValue = (int)(ScoreValue*difficultyMultiplier);
        }

        private void Start()
        {
            _timePassed.Start();
        }
    }
}
