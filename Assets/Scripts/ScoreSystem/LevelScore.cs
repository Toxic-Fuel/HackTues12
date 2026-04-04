using System.Diagnostics;
using Unity.VisualScripting;
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

        public void AddToScore(float value, ScoreType type = ScoreType.Default)
        {
            var multiplier = scoreMultipliers[(int)type];
            var delta = Mathf.RoundToInt(value * multiplier);
            scoreValue = Mathf.Max(0, scoreValue + delta);
        }
        
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
