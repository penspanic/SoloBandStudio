using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using SoloBandStudio.Core;
using SoloBandStudio.Instruments.Keyboard;

namespace SoloBandStudio.ChordQuiz
{
    /// <summary>
    /// Manages the chord quiz system including question generation,
    /// answer checking, and score tracking.
    /// Designed to work with SoloBandStudio's KeyboardLayout.
    /// </summary>
    public class ChordQuizManager : MonoBehaviour
    {
        // Singleton
        public static ChordQuizManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private ChordQuizSettings settings;
        [SerializeField] private KeyboardLayout pianoKeyboard;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        // Events
        public event Action<ChordData> OnQuestionChanged;
        public event Action<bool, int> OnAnswerChecked; // (isCorrect, points)
        public event Action<int, int, int> OnSessionComplete; // (score, correct, total)
        public event Action<float> OnTimerUpdated; // (remainingTime)
        public event Action<int> OnDifficultyChanged; // (difficulty)

        // State
        private ChordLibrary chordLibrary;
        private ChordData currentChord;
        private HashSet<int> pressedMidiNotes = new HashSet<int>();
        private List<int> correctAnswerNotes = new List<int>(); // Store the actual keys user pressed for correct answer
        private HashSet<string> answeredChordsInSession = new HashSet<string>();

        private int currentQuestionIndex;
        private int correctAnswersCount;
        private int totalScore;
        private int currentDifficulty;
        private float questionStartTime;
        private bool isQuizActive;
        private bool hasAnswered;
        private bool hasTimedOut;
        private CancellationTokenSource questionCts;

        // Properties
        public ChordData CurrentChord => currentChord;
        public int CurrentScore => totalScore;
        public bool IsActive => isQuizActive;
        public int CurrentDifficulty => currentDifficulty;
        public int CurrentQuestionIndex => currentQuestionIndex;
        public int QuestionsPerSession => settings != null ? settings.questionsPerSession : 10;

        private void Awake()
        {
            // Singleton setup
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<ChordQuizSettings>();
                Log("Using default settings");
            }
        }

        private void Start()
        {
            chordLibrary = new ChordLibrary(settings.baseOctave);

            if (pianoKeyboard != null)
            {
                pianoKeyboard.OnKeyPressed += HandleKeyPressed;
                pianoKeyboard.OnKeyReleased += HandleKeyReleased;
                Log("Connected to KeyboardLayout");
            }
            else
            {
                Debug.LogWarning("[ChordQuizManager] KeyboardLayout not assigned!");
            }
        }

        private void OnDestroy()
        {
            if (pianoKeyboard != null)
            {
                pianoKeyboard.OnKeyPressed -= HandleKeyPressed;
                pianoKeyboard.OnKeyReleased -= HandleKeyReleased;
            }

            questionCts?.Cancel();
            questionCts?.Dispose();
        }

        private void Update()
        {
            if (!isQuizActive || currentChord == null || hasAnswered) return;

            float remaining = GetTimeRemaining();
            OnTimerUpdated?.Invoke(remaining);

            // Check for timeout
            if (!hasTimedOut && remaining <= 0)
            {
                hasTimedOut = true;
                hasAnswered = true;
                Log("Time's up!");
                OnAnswerChecked?.Invoke(false, 0);
                MoveToNextQuestionAsync(settings.answerDelayMs, questionCts.Token).Forget();
            }
        }

        #region Public API

        public void SetPianoKeyboard(KeyboardLayout keyboard)
        {
            if (pianoKeyboard != null)
            {
                pianoKeyboard.OnKeyPressed -= HandleKeyPressed;
                pianoKeyboard.OnKeyReleased -= HandleKeyReleased;
            }

            pianoKeyboard = keyboard;

            if (pianoKeyboard != null)
            {
                pianoKeyboard.OnKeyPressed += HandleKeyPressed;
                pianoKeyboard.OnKeyReleased += HandleKeyReleased;
                Log("KeyboardLayout updated");
            }
        }

        public void StartSession(int difficulty = -1)
        {
            if (difficulty >= 0)
            {
                currentDifficulty = Mathf.Clamp(difficulty, 0, 2);
            }

            currentQuestionIndex = 0;
            correctAnswersCount = 0;
            totalScore = 0;
            answeredChordsInSession.Clear();
            pressedMidiNotes.Clear();
            isQuizActive = true;

            // Enable toggle mode on piano
            if (pianoKeyboard != null)
            {
                pianoKeyboard.ToggleMode = true;
            }

            GenerateNextQuestion();
            Log($"Started new session (Difficulty: {currentDifficulty})");
        }

        public void StopSession()
        {
            if (!isQuizActive) return;

            isQuizActive = false;
            questionCts?.Cancel();
            questionCts?.Dispose();
            questionCts = null;

            pressedMidiNotes.Clear();

            // Disable toggle mode on piano
            if (pianoKeyboard != null)
            {
                pianoKeyboard.ToggleMode = false;
                pianoKeyboard.ResetAllKeyVisuals();
            }

            Log("Session stopped");
        }

        public void SetDifficulty(int difficulty)
        {
            currentDifficulty = Mathf.Clamp(difficulty, 0, 2);
            OnDifficultyChanged?.Invoke(currentDifficulty);
            Log($"Difficulty set to: {currentDifficulty}");
        }

        public float GetTimeRemaining()
        {
            if (!isQuizActive) return settings.timeLimit;
            float elapsed = Time.time - questionStartTime;
            return Mathf.Max(0, settings.timeLimit - elapsed);
        }

        public float GetProgress()
        {
            if (settings.questionsPerSession == 0) return 1f;
            return (float)currentQuestionIndex / settings.questionsPerSession;
        }

        public float GetAccuracy()
        {
            if (currentQuestionIndex == 0) return 0f;
            return (float)correctAnswersCount / currentQuestionIndex * 100f;
        }

        #endregion

        #region Private Methods

        private void HandleKeyPressed(int midiNote, float velocity)
        {
            if (!isQuizActive || hasAnswered) return;

            // Toggle mode: 이미 눌려있으면 해제, 아니면 추가
            if (!pressedMidiNotes.Add(midiNote))
            {
                pressedMidiNotes.Remove(midiNote);
                pianoKeyboard?.SetKeyVisualState(midiNote, false);
                Log($"Key toggled OFF. Pressed notes count: {pressedMidiNotes.Count}");
            }
            else
            {
                pianoKeyboard?.SetKeyVisualState(midiNote, true);
                Log($"Key toggled ON. Pressed notes count: {pressedMidiNotes.Count}, Required: {currentChord?.GetNotes().Count ?? 0}");
                TryAutoCheck();
            }
        }

        private void HandleKeyReleased(int midiNote)
        {
            // 퀴즈 모드에서는 release를 무시 (토글 방식)
            if (isQuizActive) return;

            pressedMidiNotes.Remove(midiNote);
        }

        private void TryAutoCheck()
        {
            if (currentChord == null || hasAnswered) return;

            List<Note> chordNotes = currentChord.GetNotes();
            if (pressedMidiNotes.Count == chordNotes.Count)
            {
                DelayedCheckAnswerAsync(settings.autoCheckDelayMs, questionCts.Token).Forget();
            }
        }

        private async UniTaskVoid DelayedCheckAnswerAsync(int delayMs, CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(delayMs, cancellationToken: ct);
                if (!hasAnswered)
                {
                    CheckAnswer();
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelled
            }
        }

        private void CheckAnswer()
        {
            if (!isQuizActive || currentChord == null || hasAnswered) return;

            if (pressedMidiNotes.Count == 0)
            {
                Log("No notes pressed");
                return;
            }

            bool isCorrect = currentChord.MatchesMidiNotes(pressedMidiNotes);
            int pointsAwarded;

            if (isCorrect)
            {
                hasAnswered = true;

                // Save the actual keys the user pressed for playback
                correctAnswerNotes.Clear();
                correctAnswerNotes.AddRange(pressedMidiNotes);

                string chordKey = $"{currentChord.RootNote}_{currentChord.ChordType}";
                answeredChordsInSession.Add(chordKey);

                pointsAwarded = settings.pointsPerCorrectAnswer;

                float timeElapsed = Time.time - questionStartTime;
                if (timeElapsed < settings.speedBonusThreshold)
                {
                    pointsAwarded += settings.speedBonusPoints;
                    Log($"Speed bonus! ({timeElapsed:F1}s)");
                }

                totalScore += pointsAwarded;
                correctAnswersCount++;

                Log($"Correct! +{pointsAwarded} points (Total: {totalScore})");
                OnAnswerChecked?.Invoke(true, pointsAwarded);

                ShowAnswerAndContinueAsync(settings.answerDelayMs, questionCts.Token).Forget();
            }
            else
            {
                pointsAwarded = settings.pointsPerWrongAnswer;
                totalScore = Mathf.Max(0, totalScore + pointsAwarded);

                Log($"Wrong! {pointsAwarded} points (Total: {totalScore})");
                OnAnswerChecked?.Invoke(false, pointsAwarded);

                // Reset pressed notes for retry
                pressedMidiNotes.Clear();
                pianoKeyboard?.ResetAllKeyVisuals();
            }
        }

        private async UniTaskVoid ShowAnswerAndContinueAsync(int delayMs, CancellationToken ct)
        {
            try
            {
                Log($"Correct answer shown: {currentChord?.DisplayName}");

                // Brief pause to show "Correct!" feedback
                await UniTask.Delay(300, cancellationToken: ct);

                // Reset keys first (this releases the sounds)
                pianoKeyboard?.ResetAllKeyVisuals();
                pressedMidiNotes.Clear();

                await UniTask.Delay(200, cancellationToken: ct);

                // Now play the answer chord using the exact keys user pressed
                if (pianoKeyboard != null && correctAnswerNotes.Count > 0)
                {
                    foreach (int midi in correctAnswerNotes)
                    {
                        // This triggers Piano.HandleKeyPressed -> plays sound
                        pianoKeyboard.NotifyKeyPressed(midi, 0.8f);
                        pianoKeyboard.SetKeyVisualState(midi, true);
                    }
                }

                // Hold the answer display
                await UniTask.Delay(delayMs, cancellationToken: ct);

                // Release the keys (triggers sound fade out)
                if (pianoKeyboard != null && correctAnswerNotes.Count > 0)
                {
                    foreach (int midi in correctAnswerNotes)
                    {
                        pianoKeyboard.NotifyKeyReleased(midi);
                    }
                }

                pianoKeyboard?.ResetAllKeyVisuals();
                correctAnswerNotes.Clear();

                // Small delay before next question
                await UniTask.Delay(300, cancellationToken: ct);

                GenerateNextQuestion();
            }
            catch (OperationCanceledException)
            {
                pianoKeyboard?.ResetAllKeyVisuals();
                pressedMidiNotes.Clear();
                correctAnswerNotes.Clear();
            }
        }

        private async UniTaskVoid MoveToNextQuestionAsync(int delayMs, CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(delayMs, cancellationToken: ct);
                GenerateNextQuestion();
            }
            catch (OperationCanceledException)
            {
                // Cancelled
            }
        }

        private void GenerateNextQuestion()
        {
            if (currentQuestionIndex >= settings.questionsPerSession)
            {
                EndSession();
                return;
            }

            questionCts?.Cancel();
            questionCts?.Dispose();
            questionCts = new CancellationTokenSource();

            pressedMidiNotes.Clear();
            pianoKeyboard?.ResetAllKeyVisuals();

            // Find unique chord
            ChordData newChord = null;
            int maxAttempts = 50;
            int attempts = 0;

            while (attempts < maxAttempts)
            {
                newChord = chordLibrary.GetRandomChordByDifficulty(currentDifficulty);

                if (newChord == null)
                {
                    Debug.LogError("[ChordQuizManager] Failed to generate chord!");
                    return;
                }

                string chordKey = $"{newChord.RootNote}_{newChord.ChordType}";
                if (!answeredChordsInSession.Contains(chordKey))
                {
                    currentChord = newChord;
                    break;
                }

                attempts++;
            }

            if (attempts >= maxAttempts)
            {
                currentChord = newChord;
                Log($"Could not find unique chord after {maxAttempts} attempts. Using duplicate.");
            }

            questionStartTime = Time.time;
            currentQuestionIndex++;
            hasTimedOut = false;
            hasAnswered = false;

            OnQuestionChanged?.Invoke(currentChord);
            Log($"Question {currentQuestionIndex}/{settings.questionsPerSession}: {currentChord.DisplayName}");
        }

        private void EndSession()
        {
            isQuizActive = false;

            // Disable toggle mode on piano
            if (pianoKeyboard != null)
            {
                pianoKeyboard.ToggleMode = false;
                pianoKeyboard.ResetAllKeyVisuals();
            }

            float accuracy = (float)correctAnswersCount / settings.questionsPerSession;
            Log($"Session complete! Score: {totalScore}, Accuracy: {accuracy * 100:F0}%");

            OnSessionComplete?.Invoke(totalScore, correctAnswersCount, settings.questionsPerSession);

            // Auto-adjust difficulty
            if (accuracy >= 0.8f)
            {
                currentDifficulty = Mathf.Min(currentDifficulty + 1, 2);
                Log($"Great performance! Next difficulty: {currentDifficulty}");
            }
            else if (accuracy < 0.5f)
            {
                currentDifficulty = Mathf.Max(currentDifficulty - 1, 0);
                Log($"Needs practice. Next difficulty: {currentDifficulty}");
            }

            OnDifficultyChanged?.Invoke(currentDifficulty);
        }

        private void Log(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[ChordQuizManager] {message}");
            }
        }

        #endregion
    }
}
