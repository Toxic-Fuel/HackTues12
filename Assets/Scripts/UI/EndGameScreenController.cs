using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using ScoreSystem;
using GridGeneration;

namespace UI
{
    [RequireComponent(typeof(UIDocument))]
    public class EndGameScreenController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UIDocument endGameDocument;
        [SerializeField] private StyleSheet endGameStyleSheet;

        [Header("Scene Actions")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Input Actions")]
        [SerializeField] private InputActionReference closeSummaryAction;

        [Header("Reopen Button")]
        [SerializeField] private GameObject reopenEndScreenButton;

        [Header("Live Summary")]
        [SerializeField] private LevelScore levelScore;
        [SerializeField] private GridMap gridMap;
        [SerializeField, Min(0)] private int fallbackLiveScore;
        [SerializeField, Range(0, 1000)] private int fallbackLiveSpeedRatingPercent = 100;
        [SerializeField, Range(0, 100)] private int fallbackLiveDifficultyPercent = 100;

        [Header("Disable While End Screen Is Open")]
        [SerializeField] private List<Component> componentsToDisableWhenScreenEnabled = new List<Component>();

        private Label _resultTitle;
        private Label _scoreText;
        private Label _highscoreText;
        private Label _speedRatingText;
        private Label _difficultyText;
        private Label _seedText;
        private VisualElement _overlay;
        private VisualElement _panel;
        private Button _viewMapButton;
        private Button _playAgainButton;
        private Button _quitButton;
        private Button _closeSummaryButton;

        private readonly Dictionary<Component, bool> _previousEnabledStates = new Dictionary<Component, bool>();
        private bool _lastVisibleState;
        private Coroutine _hideCoroutine;
        private bool _isStyleSheetApplied;
        private bool _hasFinalSummary;
        private string _lastResultText = "Result";
        private int _lastScore;
        private int _lastHighscore;
        private int _lastSpeedRatingPercent;
        private int _lastDifficultyPercent;

        [Header("Animation")]
        [SerializeField, Min(0f)] private float hideAnimationDuration = 0.18f;

        private const string OverlayOpenClass = "endgame-overlay--open";
        private const string PanelOpenClass = "endgame-panel--open";

        public bool IsVisible => endGameDocument != null && endGameDocument.enabled;

        private void Awake()
        {
            if (endGameDocument == null)
            {
                endGameDocument = GetComponent<UIDocument>();
            }

            if (endGameDocument != null)
            {
                endGameDocument.enabled = false;
            }

            if (reopenEndScreenButton != null)
            {
                reopenEndScreenButton.SetActive(true);
            }
        }

        private void OnEnable()
        {
            RegisterInputActions();

            CacheControls();
            RegisterCallbacks();

            _lastVisibleState = IsVisible;
            ApplyScreenEnabledState(_lastVisibleState);
        }

        private void OnDisable()
        {
            UnregisterInputActions();

            if (_hideCoroutine != null)
            {
                StopCoroutine(_hideCoroutine);
                _hideCoroutine = null;
            }

            UnregisterCallbacks();
            ApplyScreenEnabledState(false);
        }

        private void Update()
        {
            bool isVisible = IsVisible;
            if (isVisible == _lastVisibleState)
            {
                return;
            }

            _lastVisibleState = isVisible;
            ApplyScreenEnabledState(isVisible);
        }

        public void Show(string resultText, int score, int speedRatingPercent, int difficultyPercent)
        {
            Show(resultText, score, score, speedRatingPercent, difficultyPercent);
        }

        public void Show(string resultText, int score, int highscore, int speedRatingPercent, int difficultyPercent)
        {
            if (endGameDocument == null)
            {
                return;
            }

            _lastResultText = string.IsNullOrEmpty(resultText) ? "Result" : resultText;
            _lastScore = Mathf.Max(0, score);
            _lastHighscore = Mathf.Max(0, highscore);
            _lastSpeedRatingPercent = Mathf.Max(0, speedRatingPercent);
            _lastDifficultyPercent = Mathf.Max(0, difficultyPercent);
            _hasFinalSummary = true;

            if (_hideCoroutine != null)
            {
                StopCoroutine(_hideCoroutine);
                _hideCoroutine = null;
            }

            if (!endGameDocument.enabled)
            {
                endGameDocument.enabled = true;
            }

            CacheControls();
            RegisterCallbacks();
            SetSummary(resultText, score, highscore, speedRatingPercent, difficultyPercent);

            SetAnimationOpenState(false);
            ScheduleOpenAnimation();

            _lastVisibleState = true;
            ApplyScreenEnabledState(true);
        }

        public void SetSummary(string resultText, int score, int speedRatingPercent, int difficultyPercent)
        {
            SetSummary(resultText, score, score, speedRatingPercent, difficultyPercent);
        }

        public void SetSummary(string resultText, int score, int highscore, int speedRatingPercent, int difficultyPercent)
        {
            if (_resultTitle != null)
            {
                _resultTitle.text = resultText;
            }

            if (_scoreText != null)
            {
                _scoreText.text = $"Score: {Mathf.Max(0, score)}";
            }

            if (_highscoreText != null)
            {
                _highscoreText.text = $"Highscore: {Mathf.Max(0, highscore)}";
            }

            if (_speedRatingText != null)
            {
                int safeSpeed = Mathf.Max(0, speedRatingPercent);
                _speedRatingText.text = $"Speed rating: {safeSpeed}%";
            }

            if (_difficultyText != null)
            {
                int safeDifficulty = Mathf.Max(0, difficultyPercent);
                _difficultyText.text = $"Difficulty rating: {safeDifficulty}%";
            }

            if (_seedText != null)
            {
                _seedText.text = BuildSeedSummaryText();
            }
        }

        public void Hide()
        {
            if (endGameDocument == null)
            {
                return;
            }

            if (_hideCoroutine != null)
            {
                StopCoroutine(_hideCoroutine);
                _hideCoroutine = null;
            }

            SetAnimationOpenState(false);

            if (endGameDocument.enabled)
            {
                _hideCoroutine = StartCoroutine(HideAfterAnimation());
            }

            _lastVisibleState = false;
            ApplyScreenEnabledState(false);
        }

        public void ShowVictory(int score, int speedRatingPercent, int difficultyPercent)
        {
            Show("Victory", score, score, speedRatingPercent, difficultyPercent);
        }

        public void ShowDefeat(int score, int speedRatingPercent, int difficultyPercent)
        {
            Show("Defeat", score, score, speedRatingPercent, difficultyPercent);
        }

        private void CacheControls()
        {
            if (endGameDocument == null)
            {
                return;
            }

            VisualElement root = endGameDocument.rootVisualElement;
            if (root == null)
            {
                return;
            }

            if (!_isStyleSheetApplied && endGameStyleSheet != null)
            {
                root.styleSheets.Add(endGameStyleSheet);
                _isStyleSheetApplied = true;
            }

            _resultTitle = root.Q<Label>("ResultTitle");
            _scoreText = root.Q<Label>("ScoreText");
            _highscoreText = root.Q<Label>("HighscoreText");
            _speedRatingText = root.Q<Label>("SpeedRatingText");
            _difficultyText = root.Q<Label>("DifficultyText");
            _seedText = root.Q<Label>("SeedText");
            _overlay = root.Q<VisualElement>("EndGameOverlay");
            _panel = root.Q<VisualElement>("EndGamePanel");
            _viewMapButton = root.Q<Button>("ViewMapButton");
            _playAgainButton = root.Q<Button>("PlayAgainButton");
            _quitButton = root.Q<Button>("QuitButton");
            _closeSummaryButton = root.Q<Button>("CloseSummaryButton");
        }

        private void SetAnimationOpenState(bool isOpen)
        {
            if (_overlay != null)
            {
                if (isOpen)
                {
                    _overlay.AddToClassList(OverlayOpenClass);
                }
                else
                {
                    _overlay.RemoveFromClassList(OverlayOpenClass);
                }
            }

            if (_panel != null)
            {
                if (isOpen)
                {
                    _panel.AddToClassList(PanelOpenClass);
                }
                else
                {
                    _panel.RemoveFromClassList(PanelOpenClass);
                }
            }
        }

        private void ScheduleOpenAnimation()
        {
            if (endGameDocument == null)
            {
                return;
            }

            VisualElement root = endGameDocument.rootVisualElement;
            if (root == null)
            {
                return;
            }

            // Delay one frame so the panel starts from the closed visual state first.
            root.schedule.Execute(() =>
            {
                if (IsVisible)
                {
                    SetAnimationOpenState(true);
                }
            }).StartingIn(0);
        }

        private IEnumerator HideAfterAnimation()
        {
            float delay = Mathf.Max(0f, hideAnimationDuration);
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            if (endGameDocument != null)
            {
                endGameDocument.enabled = false;
            }

            _hideCoroutine = null;
        }

        private void RegisterCallbacks()
        {
            UnregisterCallbacks();

            if (_viewMapButton != null)
            {
                _viewMapButton.clicked += OnViewMapClicked;
            }

            if (_playAgainButton != null)
            {
                _playAgainButton.clicked += OnPlayAgainClicked;
            }

            if (_quitButton != null)
            {
                _quitButton.clicked += OnQuitClicked;
            }

            if (_closeSummaryButton != null)
            {
                _closeSummaryButton.clicked += OnCloseSummaryClicked;
            }
        }

        private void UnregisterCallbacks()
        {
            if (_viewMapButton != null)
            {
                _viewMapButton.clicked -= OnViewMapClicked;
            }

            if (_playAgainButton != null)
            {
                _playAgainButton.clicked -= OnPlayAgainClicked;
            }

            if (_quitButton != null)
            {
                _quitButton.clicked -= OnQuitClicked;
            }

            if (_closeSummaryButton != null)
            {
                _closeSummaryButton.clicked -= OnCloseSummaryClicked;
            }
        }

        private void RegisterInputActions()
        {
            if (closeSummaryAction == null || closeSummaryAction.action == null)
            {
                return;
            }

            closeSummaryAction.action.performed -= OnCloseSummaryPerformed;
            closeSummaryAction.action.performed += OnCloseSummaryPerformed;
            closeSummaryAction.action.Enable();
        }

        private void UnregisterInputActions()
        {
            if (closeSummaryAction == null || closeSummaryAction.action == null)
            {
                return;
            }

            closeSummaryAction.action.performed -= OnCloseSummaryPerformed;
            closeSummaryAction.action.Disable();
        }

        private void OnCloseSummaryPerformed(InputAction.CallbackContext context)
        {
            if (!context.performed || !IsVisible)
            {
                return;
            }

            Hide();
        }

        private void OnViewMapClicked()
        {
            Hide();
        }

        private void OnCloseSummaryClicked()
        {
            Hide();
        }

        public void OnReopenEndScreenClicked()
        {
            if (_hasFinalSummary)
            {
                Show(_lastResultText, _lastScore, _lastHighscore, _lastSpeedRatingPercent, _lastDifficultyPercent);
                return;
            }

            ShowCurrentSummary();
        }

        public void ShowCurrentSummary()
        {
            if (levelScore == null)
            {
                levelScore = FindAnyObjectByType<LevelScore>();
            }

            int score = fallbackLiveScore;
            int speedRatingPercent = fallbackLiveSpeedRatingPercent;
            int difficultyPercent = fallbackLiveDifficultyPercent;

            if (levelScore != null)
            {
                score = levelScore.GetCurrentScore();
                speedRatingPercent = levelScore.GetCurrentSpeedRatingPercent();
                difficultyPercent = levelScore.GetCurrentDifficultyRatingPercent();
            }

            int highscore = HighscoreSystem.GetBestScore();
            Show("Current", score, highscore, speedRatingPercent, difficultyPercent);
            _hasFinalSummary = false;
        }

        private void OnPlayAgainClicked()
        {
            if (gridMap == null)
            {
                gridMap = FindAnyObjectByType<GridMap>();
            }

            if (gridMap != null)
            {
                RandomSeed.ForceSeedForNextLoad(gridMap.seed);
            }

            InGameGenerationMenu.QueueCurrentSettingsForNextGridMap();

            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.name);
        }

        private void OnQuitClicked()
        {
            if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                Debug.LogError($"EndGameScreenController: Scene '{mainMenuSceneName}' is not in Build Profiles / shared scene list.", this);
                return;
            }

            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void ApplyScreenEnabledState(bool isVisible)
        {
            if (isVisible)
            {
                for (int i = 0; i < componentsToDisableWhenScreenEnabled.Count; i++)
                {
                    Component component = componentsToDisableWhenScreenEnabled[i];
                    if (component == null || component == this || component == endGameDocument)
                    {
                        continue;
                    }

                    if (TryGetComponentEnabled(component, out bool currentEnabled))
                    {
                        if (!_previousEnabledStates.ContainsKey(component))
                        {
                            _previousEnabledStates[component] = currentEnabled;
                        }

                        SetComponentEnabled(component, false);
                    }
                }

                return;
            }

            if (_previousEnabledStates.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<Component, bool> pair in _previousEnabledStates)
            {
                if (pair.Key != null)
                {
                    SetComponentEnabled(pair.Key, pair.Value);
                }
            }

            _previousEnabledStates.Clear();
        }

        private string BuildSeedSummaryText()
        {
            if (gridMap == null)
            {
                gridMap = FindAnyObjectByType<GridMap>();
            }

            if (gridMap == null)
            {
                return "Seed: N/A";
            }

            return $"Seed: {gridMap.seed}";
        }

        private static bool TryGetComponentEnabled(Component component, out bool enabled)
        {
            enabled = false;
            if (component == null)
            {
                return false;
            }

            var property = component.GetType().GetProperty("enabled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (property == null || property.PropertyType != typeof(bool) || !property.CanRead)
            {
                return false;
            }

            enabled = (bool)property.GetValue(component);
            return true;
        }

        private static void SetComponentEnabled(Component component, bool enabled)
        {
            if (component == null)
            {
                return;
            }

            var property = component.GetType().GetProperty("enabled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (property != null && property.PropertyType == typeof(bool) && property.CanWrite)
            {
                property.SetValue(component, enabled);
            }
        }
    }
}



