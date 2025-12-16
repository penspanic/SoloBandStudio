using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using SoloBandStudio.ChordQuiz;

namespace SoloBandStudio.UI.ChordQuiz
{
    /// <summary>
    /// Standalone UI Toolkit view controller for the Chord Quiz.
    /// Handles UI updates based on ChordQuizManager events.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ChordQuizView : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float feedbackDisplayTime = 1.2f;
        [SerializeField] private float timeLimit = 30f;

        // Root element
        private VisualElement root;

        // Header
        private Label scoreLabel;
        private Label progressLabel;
        private Label accuracyLabel;

        // Chord Display
        private VisualElement chordDisplay;
        private Label chordNameLabel;
        private Label instructionLabel;

        // Timer
        private VisualElement timerBar;
        private Label timerLabel;

        // Difficulty
        private Button easyBtn;
        private Button mediumBtn;
        private Button hardBtn;
        private Button[] difficultyBtns;

        // Controls
        private Button startBtn;
        private Button stopBtn;

        // Feedback
        private VisualElement feedbackPanel;
        private Label feedbackLabel;
        private Label pointsLabel;

        // Session Complete
        private VisualElement sessionComplete;
        private Label finalScoreLabel;
        private Label finalAccuracyLabel;
        private Button restartBtn;

        private ChordQuizManager quizManager;
        private Coroutine feedbackCoroutine;

        private void Start()
        {
            Debug.Log("[ChordQuizView] Start() called");
            StartCoroutine(InitializeDelayed());
        }

        private IEnumerator InitializeDelayed()
        {
            Debug.Log("[ChordQuizView] InitializeDelayed started");
            yield return null; // Wait one frame for UIDocument

            // Get quiz manager singleton
            quizManager = ChordQuizManager.Instance;
            Debug.Log($"[ChordQuizView] ChordQuizManager: {(quizManager != null ? "found" : "NOT FOUND")}");

            if (quizManager == null)
            {
                Debug.LogError("[ChordQuizView] ChordQuizManager.Instance not found! Make sure ChordQuizManager exists in scene.");
                yield break;
            }

            // Get UIDocument
            var uiDocument = GetComponent<UIDocument>();

            if (uiDocument == null)
            {
                Debug.LogError("[ChordQuizView] UIDocument component not found!");
                yield break;
            }

            root = uiDocument.rootVisualElement;

            if (root == null)
            {
                Debug.LogError("[ChordQuizView] rootVisualElement is null!");
                yield break;
            }

            QueryElements();
            SetupCallbacks();
            SubscribeToManager();
            SetInitialState();

            Debug.Log("[ChordQuizView] Initialized successfully");
        }

        private void OnDestroy()
        {
            UnsubscribeFromManager();
        }

        private void QueryElements()
        {
            // Header
            scoreLabel = root.Q<Label>("score-label");
            progressLabel = root.Q<Label>("progress-label");
            accuracyLabel = root.Q<Label>("accuracy-label");

            // Chord Display
            chordDisplay = root.Q<VisualElement>("chord-display");
            chordNameLabel = root.Q<Label>("chord-name");
            instructionLabel = root.Q<Label>("instruction");

            // Timer
            timerBar = root.Q<VisualElement>("timer-bar");
            timerLabel = root.Q<Label>("timer-label");

            // Difficulty
            easyBtn = root.Q<Button>("easy-btn");
            mediumBtn = root.Q<Button>("medium-btn");
            hardBtn = root.Q<Button>("hard-btn");
            difficultyBtns = new Button[] { easyBtn, mediumBtn, hardBtn };

            // Controls
            startBtn = root.Q<Button>("start-btn");
            stopBtn = root.Q<Button>("stop-btn");

            // Feedback
            feedbackPanel = root.Q<VisualElement>("feedback-panel");
            feedbackLabel = root.Q<Label>("feedback-label");
            pointsLabel = root.Q<Label>("points-label");

            // Session Complete
            sessionComplete = root.Q<VisualElement>("session-complete");
            finalScoreLabel = root.Q<Label>("final-score");
            finalAccuracyLabel = root.Q<Label>("final-accuracy");
            restartBtn = root.Q<Button>("restart-btn");

            Debug.Log($"[ChordQuizView] QueryElements - startBtn: {startBtn != null}, chordNameLabel: {chordNameLabel != null}");
        }

        private void SetupCallbacks()
        {
            startBtn?.RegisterCallback<ClickEvent>(evt => OnStartClicked());
            stopBtn?.RegisterCallback<ClickEvent>(evt => OnStopClicked());
            restartBtn?.RegisterCallback<ClickEvent>(evt => OnRestartClicked());

            easyBtn?.RegisterCallback<ClickEvent>(evt => OnDifficultySelected(0));
            mediumBtn?.RegisterCallback<ClickEvent>(evt => OnDifficultySelected(1));
            hardBtn?.RegisterCallback<ClickEvent>(evt => OnDifficultySelected(2));
        }

        private void SubscribeToManager()
        {
            if (quizManager == null) return;

            quizManager.OnQuestionChanged += OnQuestionChanged;
            quizManager.OnAnswerChecked += OnAnswerChecked;
            quizManager.OnSessionComplete += OnSessionComplete;
            quizManager.OnTimerUpdated += OnTimerUpdated;
            quizManager.OnDifficultyChanged += OnDifficultyChanged;
        }

        private void UnsubscribeFromManager()
        {
            if (quizManager == null) return;

            quizManager.OnQuestionChanged -= OnQuestionChanged;
            quizManager.OnAnswerChecked -= OnAnswerChecked;
            quizManager.OnSessionComplete -= OnSessionComplete;
            quizManager.OnTimerUpdated -= OnTimerUpdated;
            quizManager.OnDifficultyChanged -= OnDifficultyChanged;
        }

        private void SetInitialState()
        {
            // Hide overlays
            feedbackPanel?.AddToClassList("hidden");
            feedbackPanel?.RemoveFromClassList("visible");
            sessionComplete?.AddToClassList("hidden");

            // Show start button, hide stop
            startBtn?.RemoveFromClassList("hidden");
            stopBtn?.AddToClassList("hidden");

            // Set waiting state
            chordDisplay?.AddToClassList("waiting");
            if (chordNameLabel != null) chordNameLabel.text = "?";
            if (instructionLabel != null) instructionLabel.text = "Press START to begin";

            // Reset displays
            if (scoreLabel != null) scoreLabel.text = "0";
            if (progressLabel != null) progressLabel.text = "0/10";
            if (accuracyLabel != null) accuracyLabel.text = "0%";
            if (timerLabel != null) timerLabel.text = $"{timeLimit:F0}s";

            if (timerBar != null)
            {
                timerBar.style.width = Length.Percent(100);
            }

            // Set initial difficulty
            UpdateDifficultyButtons(0);
        }

        #region Button Handlers

        private void OnStartClicked()
        {
            if (quizManager == null) return;

            sessionComplete?.AddToClassList("hidden");
            startBtn?.AddToClassList("hidden");
            stopBtn?.RemoveFromClassList("hidden");
            chordDisplay?.RemoveFromClassList("waiting");

            quizManager.StartSession();
        }

        private void OnStopClicked()
        {
            if (quizManager == null) return;

            quizManager.StopSession();
            SetInitialState();
        }

        private void OnRestartClicked()
        {
            if (quizManager == null) return;

            sessionComplete?.AddToClassList("hidden");
            startBtn?.AddToClassList("hidden");
            stopBtn?.RemoveFromClassList("hidden");
            chordDisplay?.RemoveFromClassList("waiting");

            quizManager.StartSession(-1); // Keep current difficulty
        }

        private void OnDifficultySelected(int difficulty)
        {
            if (quizManager == null) return;

            quizManager.SetDifficulty(difficulty);
            UpdateDifficultyButtons(difficulty);
        }

        #endregion

        #region Event Handlers

        private void OnQuestionChanged(ChordData chord)
        {
            if (chord == null) return;

            if (chordNameLabel != null) chordNameLabel.text = chord.DisplayName;
            if (instructionLabel != null) instructionLabel.text = "Play this chord on the piano";

            int current = quizManager.CurrentQuestionIndex;
            int total = quizManager.QuestionsPerSession;
            if (progressLabel != null) progressLabel.text = $"{current}/{total}";
        }

        private void OnAnswerChecked(bool isCorrect, int points)
        {
            // Update score
            if (scoreLabel != null) scoreLabel.text = quizManager.CurrentScore.ToString();

            // Update accuracy
            float accuracy = quizManager.GetAccuracy();
            if (accuracyLabel != null) accuracyLabel.text = $"{accuracy:F0}%";

            // Show feedback
            ShowFeedback(isCorrect, points);
        }

        private void OnSessionComplete(int score, int correct, int total)
        {
            // Stop button -> Start button
            stopBtn?.AddToClassList("hidden");
            startBtn?.RemoveFromClassList("hidden");

            // Show session complete overlay
            if (finalScoreLabel != null) finalScoreLabel.text = $"Score: {score}";
            float accuracy = (float)correct / total * 100f;
            if (finalAccuracyLabel != null) finalAccuracyLabel.text = $"Accuracy: {accuracy:F0}% ({correct}/{total})";

            sessionComplete?.RemoveFromClassList("hidden");
        }

        private void OnTimerUpdated(float remaining)
        {
            // Update timer label
            if (timerLabel != null) timerLabel.text = $"{remaining:F0}s";

            // Update timer bar width
            if (timerBar != null)
            {
                float percent = (remaining / timeLimit) * 100f;
                timerBar.style.width = Length.Percent(percent);

                // Update color based on time
                timerBar.RemoveFromClassList("warning");
                timerBar.RemoveFromClassList("danger");

                if (remaining <= 5f)
                {
                    timerBar.AddToClassList("danger");
                }
                else if (remaining <= 10f)
                {
                    timerBar.AddToClassList("warning");
                }
            }
        }

        private void OnDifficultyChanged(int difficulty)
        {
            UpdateDifficultyButtons(difficulty);
        }

        #endregion

        #region UI Helpers

        private void UpdateDifficultyButtons(int activeDifficulty)
        {
            for (int i = 0; i < difficultyBtns.Length; i++)
            {
                if (difficultyBtns[i] == null) continue;

                if (i == activeDifficulty)
                {
                    difficultyBtns[i].AddToClassList("active");
                }
                else
                {
                    difficultyBtns[i].RemoveFromClassList("active");
                }
            }
        }

        private void ShowFeedback(bool isCorrect, int points)
        {
            if (feedbackPanel == null) return;

            // Stop any existing feedback
            if (feedbackCoroutine != null)
            {
                StopCoroutine(feedbackCoroutine);
            }

            // Set feedback content
            if (feedbackLabel != null) feedbackLabel.text = isCorrect ? "Correct!" : "Wrong!";
            if (pointsLabel != null) pointsLabel.text = points >= 0 ? $"+{points}" : $"{points}";

            // Set feedback style
            feedbackPanel.RemoveFromClassList("correct");
            feedbackPanel.RemoveFromClassList("wrong");
            feedbackPanel.AddToClassList(isCorrect ? "correct" : "wrong");

            // Show feedback
            feedbackPanel.RemoveFromClassList("hidden");
            feedbackPanel.AddToClassList("visible");

            // Hide after delay
            feedbackCoroutine = StartCoroutine(HideFeedbackAfterDelay());
        }

        private IEnumerator HideFeedbackAfterDelay()
        {
            yield return new WaitForSeconds(feedbackDisplayTime);

            feedbackPanel?.RemoveFromClassList("visible");
            feedbackPanel?.AddToClassList("hidden");
            feedbackPanel?.RemoveFromClassList("correct");
            feedbackPanel?.RemoveFromClassList("wrong");
        }

        #endregion
    }
}
