using UnityEngine;
using System;

namespace SoloBandStudio.Audio
{
    /// <summary>
    /// Central timing system for the loop station.
    /// Provides precise beat/bar tracking using AudioSettings.dspTime.
    /// All timing calculations use double precision for accuracy.
    /// </summary>
    public class BeatClock : MonoBehaviour
    {
        [Header("Tempo Settings")]
        [SerializeField] private float bpm = 120f;
        [SerializeField] private int beatsPerBar = 4;
        [SerializeField] private int totalBars = 4;

        [Header("Scheduling")]
        [SerializeField] private double startDelaySeconds = 0.1; // Buffer before playback starts

        [Header("Count-In")]
        [SerializeField] private bool enableCountIn = true;
        [SerializeField] private int countInBars = 1;

        [Header("State")]
        [SerializeField] private bool isPlaying = false;
        [SerializeField] private bool isCountingIn = false;

        // Timing state (all in double for precision)
        private double startDspTime;
        private double loopDurationSeconds;
        private double secondsPerBeat;
        private int totalBeatsInLoop;

        // Count-in timing
        private double countInStartDspTime;
        private double countInDurationSeconds;
        private int countInTotalBeats;

        // Events
        public event Action OnPlayStateChanged;
        public event Action OnLoopComplete;
        public event Action OnCountInStarted;
        public event Action OnCountInComplete;

        // Properties
        public float BPM
        {
            get => bpm;
            set
            {
                bpm = Mathf.Clamp(value, 30f, 300f);
                RecalculateTiming();
            }
        }

        public int BeatsPerBar
        {
            get => beatsPerBar;
            set
            {
                beatsPerBar = Mathf.Clamp(value, 2, 8);
                RecalculateTiming();
            }
        }

        public int TotalBars
        {
            get => totalBars;
            set
            {
                totalBars = Mathf.Max(1, value);  // No upper limit - determined by song
                RecalculateTiming();
            }
        }

        public bool IsPlaying => isPlaying;
        public bool IsCountingIn => isCountingIn;
        public bool EnableCountIn
        {
            get => enableCountIn;
            set
            {
                if (enableCountIn != value)
                {
                    enableCountIn = value;
                    OnCountInEnabledChanged?.Invoke(value);
                }
            }
        }

        public event Action<bool> OnCountInEnabledChanged;
        public int CountInBars
        {
            get => countInBars;
            set => countInBars = Mathf.Max(1, value);
        }
        public int TotalBeats => totalBeatsInLoop;
        public double SecondsPerBeat => secondsPerBeat;
        public double LoopDuration => loopDurationSeconds;
        public double StartDspTime => startDspTime;

        /// <summary>
        /// Current position in beats from loop start (0 to TotalBeats).
        /// Returns high-precision value for accurate scheduling.
        /// During count-in, returns 0.
        /// </summary>
        public double CurrentBeatPositionPrecise
        {
            get
            {
                if (!isPlaying || isCountingIn) return 0;
                double elapsed = AudioSettings.dspTime - startDspTime;
                if (elapsed < 0) return 0; // Not started yet
                return (elapsed / secondsPerBeat) % totalBeatsInLoop;
            }
        }

        /// <summary>
        /// Current position as float (for UI and less critical uses).
        /// </summary>
        public float CurrentBeatPosition => (float)CurrentBeatPositionPrecise;

        /// <summary>
        /// Current beat position during count-in (0 to CountInBeats).
        /// Returns -1 if not counting in.
        /// </summary>
        public double CountInBeatPositionPrecise
        {
            get
            {
                if (!isCountingIn) return -1;
                double elapsed = AudioSettings.dspTime - countInStartDspTime;
                if (elapsed < 0) return 0;
                return elapsed / secondsPerBeat;
            }
        }

        /// <summary>
        /// Total beats in count-in.
        /// </summary>
        public int CountInBeats => countInTotalBeats;

        /// <summary>
        /// Current loop number (0-based).
        /// </summary>
        public int CurrentLoop
        {
            get
            {
                if (!isPlaying) return 0;
                double elapsed = AudioSettings.dspTime - startDspTime;
                if (elapsed < 0) return 0;
                return (int)(elapsed / loopDurationSeconds);
            }
        }

        private int lastLoop = -1;

        private void Awake()
        {
            RecalculateTiming();
        }

        private void Update()
        {
            if (!isPlaying && !isCountingIn) return;

            // Check for count-in completion
            if (isCountingIn)
            {
                double elapsed = AudioSettings.dspTime - countInStartDspTime;
                if (elapsed >= countInDurationSeconds)
                {
                    CompleteCountIn();
                }
                return;
            }

            // Check for loop completion
            int currentLoop = CurrentLoop;
            if (currentLoop > lastLoop)
            {
                lastLoop = currentLoop;
                OnLoopComplete?.Invoke();
            }
        }

        private void CompleteCountIn()
        {
            isCountingIn = false;
            isPlaying = true;
            lastLoop = 0;

            // startDspTime was already set to right after count-in
            Debug.Log($"[BeatClock] Count-in complete, playback started");
            OnCountInComplete?.Invoke();
            OnPlayStateChanged?.Invoke();
        }

        /// <summary>
        /// Start playback from the beginning (or from pending seek position).
        /// If count-in is enabled, plays count-in first.
        /// </summary>
        public void Play()
        {
            if (isPlaying || isCountingIn) return;

            if (enableCountIn && !hasPendingSeek)
            {
                StartCountIn();
            }
            else
            {
                StartPlaybackImmediately();
            }
        }

        private void StartCountIn()
        {
            isCountingIn = true;
            countInTotalBeats = beatsPerBar * countInBars;
            countInDurationSeconds = countInTotalBeats * secondsPerBeat;
            countInStartDspTime = AudioSettings.dspTime + startDelaySeconds;

            // Pre-calculate when actual playback will start (right after count-in)
            startDspTime = countInStartDspTime + countInDurationSeconds;

            Debug.Log($"[BeatClock] Count-in started - {countInTotalBeats} beats ({countInBars} bar(s)) at {bpm} BPM");
            OnCountInStarted?.Invoke();
        }

        private void StartPlaybackImmediately()
        {
            isPlaying = true;
            lastLoop = 0;

            if (hasPendingSeek)
            {
                // Start from pending seek position
                double currentDsp = AudioSettings.dspTime + startDelaySeconds;
                startDspTime = currentDsp - (pendingSeekPosition * secondsPerBeat);
                hasPendingSeek = false;
                Debug.Log($"[BeatClock] Started from beat {pendingSeekPosition:F2} - {bpm} BPM, {beatsPerBar}/4, {totalBars} bars");
            }
            else
            {
                startDspTime = AudioSettings.dspTime + startDelaySeconds;
                Debug.Log($"[BeatClock] Started - {bpm} BPM, {beatsPerBar}/4, {totalBars} bars, startDsp={startDspTime:F3}");
            }

            OnPlayStateChanged?.Invoke();
        }

        /// <summary>
        /// Stop playback (also cancels count-in if in progress).
        /// </summary>
        public void Stop()
        {
            if (!isPlaying && !isCountingIn) return;

            bool wasCountingIn = isCountingIn;
            isPlaying = false;
            isCountingIn = false;
            lastLoop = -1;

            OnPlayStateChanged?.Invoke();
            Debug.Log(wasCountingIn ? "[BeatClock] Count-in cancelled" : "[BeatClock] Stopped");
        }

        /// <summary>
        /// Toggle play/stop.
        /// </summary>
        public void Toggle()
        {
            if (isPlaying) Stop();
            else Play();
        }

        /// <summary>
        /// Seek to a specific beat position.
        /// If playing, adjusts playback position. If stopped, position is used on next Play().
        /// </summary>
        public void SeekTo(double beatPosition)
        {
            // Normalize beat position
            beatPosition = beatPosition % totalBeatsInLoop;
            if (beatPosition < 0) beatPosition += totalBeatsInLoop;

            if (isPlaying)
            {
                // Adjust startDspTime so current position becomes beatPosition
                double currentDsp = AudioSettings.dspTime;
                double beatsElapsed = beatPosition;
                startDspTime = currentDsp - (beatsElapsed * secondsPerBeat);

                // Reset loop tracking
                lastLoop = CurrentLoop;

                Debug.Log($"[BeatClock] Seeked to beat {beatPosition:F2} (playing)");
            }
            else
            {
                // Store position for next Play - adjust startDspTime when Play is called
                pendingSeekPosition = beatPosition;
                hasPendingSeek = true;

                Debug.Log($"[BeatClock] Seek position set to beat {beatPosition:F2} (stopped)");
            }
        }

        private double pendingSeekPosition = 0;
        private bool hasPendingSeek = false;

        /// <summary>
        /// Get the DSP time for a specific beat position in the current or next loop.
        /// This is the core method for scheduling - always returns a future time.
        /// </summary>
        /// <param name="beatPosition">Beat position (0 to TotalBeats)</param>
        /// <returns>DSP time when that beat will occur</returns>
        public double GetDspTimeForBeat(double beatPosition)
        {
            if (!isPlaying) return AudioSettings.dspTime;

            // Normalize beat position to [0, totalBeats)
            beatPosition = beatPosition % totalBeatsInLoop;
            if (beatPosition < 0) beatPosition += totalBeatsInLoop;

            double currentDsp = AudioSettings.dspTime;
            double elapsed = currentDsp - startDspTime;

            // If we haven't started yet, calculate from start time
            if (elapsed < 0)
            {
                return startDspTime + (beatPosition * secondsPerBeat);
            }

            // Calculate which loop we're in and current beat
            int currentLoop = (int)(elapsed / loopDurationSeconds);
            double currentBeatInLoop = (elapsed / secondsPerBeat) % totalBeatsInLoop;

            // If target beat is behind current position, it's in the next loop
            int targetLoop = currentLoop;
            if (beatPosition <= currentBeatInLoop + 0.001) // Small epsilon for floating point
            {
                targetLoop++;
            }

            // Calculate absolute DSP time
            double absoluteBeatTime = (targetLoop * totalBeatsInLoop + beatPosition) * secondsPerBeat;
            return startDspTime + absoluteBeatTime;
        }

        /// <summary>
        /// Get DSP time for a beat position, with explicit loop specification.
        /// Useful for scheduling notes that span loop boundaries.
        /// </summary>
        public double GetDspTimeForBeatInLoop(double beatPosition, int loopNumber)
        {
            if (!isPlaying && !isCountingIn) return AudioSettings.dspTime;

            beatPosition = beatPosition % totalBeatsInLoop;
            if (beatPosition < 0) beatPosition += totalBeatsInLoop;

            double absoluteBeatTime = (loopNumber * totalBeatsInLoop + beatPosition) * secondsPerBeat;
            return startDspTime + absoluteBeatTime;
        }

        /// <summary>
        /// Get DSP time for a beat during count-in.
        /// </summary>
        /// <param name="countInBeat">Beat position in count-in (0 to CountInBeats-1)</param>
        /// <returns>DSP time when that count-in beat will occur</returns>
        public double GetDspTimeForCountInBeat(int countInBeat)
        {
            if (!isCountingIn) return AudioSettings.dspTime;
            return countInStartDspTime + (countInBeat * secondsPerBeat);
        }

        /// <summary>
        /// Convert a duration in beats to seconds.
        /// </summary>
        public double BeatsToSeconds(double beats)
        {
            return beats * secondsPerBeat;
        }

        /// <summary>
        /// Convert a duration in seconds to beats.
        /// </summary>
        public double SecondsToBeats(double seconds)
        {
            return seconds / secondsPerBeat;
        }

        /// <summary>
        /// Apply song metadata to configure the beat clock.
        /// This is the preferred way to set up timing for a loaded song.
        /// </summary>
        public void ApplyMetadata(SongMetadata metadata)
        {
            bpm = metadata.BPM;
            beatsPerBar = metadata.BeatsPerBar;
            totalBars = metadata.TotalBars;
            RecalculateTiming();
            Debug.Log($"[BeatClock] Applied metadata: {metadata}");
        }

        /// <summary>
        /// Get current timing as SongMetadata.
        /// </summary>
        public SongMetadata GetMetadata(string name = "")
        {
            return new SongMetadata(bpm, beatsPerBar, totalBars, name);
        }

        private void RecalculateTiming()
        {
            secondsPerBeat = 60.0 / bpm;
            totalBeatsInLoop = beatsPerBar * totalBars;
            loopDurationSeconds = secondsPerBeat * totalBeatsInLoop;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            bpm = Mathf.Clamp(bpm, 30f, 300f);
            beatsPerBar = Mathf.Clamp(beatsPerBar, 2, 8);
            totalBars = Mathf.Max(1, totalBars);
            RecalculateTiming();
        }
#endif
    }
}
