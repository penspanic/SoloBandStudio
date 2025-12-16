using UnityEngine;

namespace SoloBandStudio.ChordQuiz
{
    /// <summary>
    /// Settings for the Chord Quiz system.
    /// </summary>
    [CreateAssetMenu(fileName = "ChordQuizSettings", menuName = "SoloBandStudio/ChordQuiz/Settings")]
    public class ChordQuizSettings : ScriptableObject
    {
        [Header("Quiz Settings")]
        [Tooltip("Time limit per question in seconds")]
        public float timeLimit = 30f;

        [Tooltip("Number of questions per session")]
        public int questionsPerSession = 10;

        [Tooltip("Base octave for chord generation")]
        public int baseOctave = 4;

        [Header("Scoring")]
        [Tooltip("Points for a correct answer")]
        public int pointsPerCorrectAnswer = 100;

        [Tooltip("Points deducted for a wrong answer (negative value)")]
        public int pointsPerWrongAnswer = -20;

        [Tooltip("Bonus points for fast answers")]
        public int speedBonusPoints = 50;

        [Tooltip("Time threshold for speed bonus (seconds)")]
        public float speedBonusThreshold = 10f;

        [Header("Timing")]
        [Tooltip("Delay after answer before next question (ms)")]
        public int answerDelayMs = 1800;

        [Tooltip("Delay for auto-check after chord completion (ms)")]
        public int autoCheckDelayMs = 300;

        [Header("Visual Feedback")]
        public Color correctColor = new Color(0.3f, 0.69f, 0.31f); // Green
        public Color wrongColor = new Color(0.96f, 0.26f, 0.21f);  // Red
        public float feedbackDuration = 1.5f;
    }
}
