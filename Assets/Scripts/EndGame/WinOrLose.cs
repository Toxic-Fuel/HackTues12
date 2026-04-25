using UnityEngine;
using UnityEngine.SceneManagement;
using ScoreSystem;
using UI;

namespace EndGame
{
    public class WinOrLose : MonoBehaviour
    {
        [SerializeField] private string victorySceneName = "VictoryScene";
        [SerializeField] private string defeatSceneName = "DefeatScene";
        [SerializeField] private EndGameScreenController endGameScreenController;
        [SerializeField] private LevelScore levelScore;
        [SerializeField] private Turns turns;
        [SerializeField, Min(0)] private int fallbackScore;
        [SerializeField, Range(0, 100)] private int fallbackSpeedRatingPercent;
        [SerializeField, Range(0, 100)] private int fallbackDifficultyPercent = 60;

        public void CheckWinOrLose(Turns.TurnState state)
        {
            Debug.Log("Checking win or lose condition: " + state);

            if (state == Turns.TurnState.Win)
            {
                if (TryShowEndScreen("Victory"))
                {
                    return;
                }

                LoadSceneSafe(victorySceneName);
            }
            else if (state == Turns.TurnState.Lose)
            {
                if (TryShowEndScreen("Defeat"))
                {
                    return;
                }

                LoadSceneSafe(defeatSceneName);
            }
        }

        private bool TryShowEndScreen(string resultText)
        {
            if (endGameScreenController == null)
            {
                endGameScreenController = FindAnyObjectByType<EndGameScreenController>();
            }

            if (endGameScreenController == null)
            {
                return false;
            }

            if (levelScore == null)
            {
                levelScore = FindAnyObjectByType<LevelScore>();
            }

            if (turns == null)
            {
                turns = FindAnyObjectByType<Turns>();
            }

            int score = fallbackScore;
            int speedRatingPercent = fallbackSpeedRatingPercent;
            int difficultyRatingPercent = fallbackDifficultyPercent;

            if (levelScore != null)
            {
                levelScore.CalculateEndScore();
                score = Mathf.Max(0, levelScore.ScoreValue);
                speedRatingPercent = Mathf.Max(0, Mathf.RoundToInt(levelScore.TurnMultiplier * 100f));
                difficultyRatingPercent = Mathf.Max(0, Mathf.RoundToInt(levelScore.DifficultyMultiplier * 100f));
            }

            if (resultText == "Defeat")
            {
                speedRatingPercent = 0;
            }

            bool isNewHighscore = HighscoreSystem.TrySubmitScore(score, out int bestScore);
            if (isNewHighscore)
            {
                Debug.Log($"New highscore saved: {bestScore}");
            }
            else
            {
                Debug.Log($"Highscore remains: {bestScore}");
            }

            if (resultText == "Defeat")
            {
                bool lostByTurns = turns != null && turns.RemainingTurns <= 0;
                if (lostByTurns)
                {
                    endGameScreenController.ShowDefeatOnTurns(score, speedRatingPercent, difficultyRatingPercent);
                }
                else
                {
                    endGameScreenController.ShowDefeatOnHealth(score, speedRatingPercent, difficultyRatingPercent);
                }

                return true;
            }

            endGameScreenController.ShowVictory(score, speedRatingPercent, difficultyRatingPercent);
            return true;
        }

        private void LoadSceneSafe(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"WinOrLose: Scene '{sceneName}' is not in Build Profiles / shared scene list.");
                return;
            }

            SceneManager.LoadScene(sceneName);
        }
    }
}
