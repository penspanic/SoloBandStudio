using UnityEngine;
using System.Collections.Generic;

namespace SoloBandStudio.Audio
{
    /// <summary>
    /// Visual and audio metronome feedback for BeatClock.
    /// Uses CustomAudioMixer for sample-accurate timing.
    /// </summary>
    public class Metronome : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BeatClock beatClock;

        [Header("Audio")]
        [SerializeField] private AudioClip tickSound;
        [SerializeField] private AudioClip accentSound;
        [SerializeField] private float tickVolume = 0.5f;
        [SerializeField] private float accentVolume = 0.7f;

        [Header("Visual Feedback")]
        [SerializeField] private Transform visualIndicator;
        [SerializeField] private float pulseScale = 1.3f;
        [SerializeField] private float pulseDuration = 0.1f;

        [Header("Options")]
        [SerializeField] private bool playAccentOnBarStart = true;
        [SerializeField] private bool muteMetronome = false;

        private Vector3 initialIndicatorScale;
        private float pulseTimer;
        private CustomAudioMixer audioMixer;

        // Scheduling state
        private const double LOOKAHEAD_BEATS = 2.0;
        private HashSet<int> scheduledBeats = new HashSet<int>();
        private int lastScheduledLoop = -1;
        private double lastScheduledBeat = -1;

        // Count-in scheduling
        private HashSet<int> scheduledCountInBeats = new HashSet<int>();

        // Track scheduled handle IDs (to cancel on stop)
        private List<int> scheduledHandleIds = new List<int>();

        // Visual scheduling
        private List<(int beat, double dspTime)> scheduledVisuals = new List<(int, double)>();

        public bool IsMuted
        {
            get => muteMetronome;
            set
            {
                if (muteMetronome != value)
                {
                    muteMetronome = value;
                    OnMuteChanged?.Invoke(value);
                }
            }
        }

        public event System.Action<bool> OnMuteChanged;

        private void Awake()
        {
            if (visualIndicator != null)
            {
                initialIndicatorScale = visualIndicator.localScale;
            }
        }

        private void Start()
        {
            audioMixer = CustomAudioMixer.Instance;
            PreloadSamples();
        }

        private void PreloadSamples()
        {
            var cache = SampleDataCache.Instance;
            if (tickSound != null)
                cache.Preload(tickSound);
            if (accentSound != null)
                cache.Preload(accentSound);
        }

        private void OnEnable()
        {
            if (beatClock != null)
            {
                beatClock.OnPlayStateChanged += HandlePlayStateChanged;
                beatClock.OnLoopComplete += HandleLoopComplete;
                beatClock.OnCountInStarted += HandleCountInStarted;
            }
        }

        private void OnDisable()
        {
            if (beatClock != null)
            {
                beatClock.OnPlayStateChanged -= HandlePlayStateChanged;
                beatClock.OnLoopComplete -= HandleLoopComplete;
                beatClock.OnCountInStarted -= HandleCountInStarted;
            }
        }

        private void Update()
        {
            // Animate pulse decay
            if (visualIndicator != null && pulseTimer > 0)
            {
                pulseTimer -= Time.deltaTime;
                float t = pulseTimer / pulseDuration;
                float scale = Mathf.Lerp(1f, pulseScale, t);
                visualIndicator.localScale = initialIndicatorScale * scale;
            }

            // Schedule count-in ticks
            if (beatClock != null && beatClock.IsCountingIn && !muteMetronome)
            {
                ScheduleCountInTicks();
            }
            // Schedule upcoming ticks during normal playback
            else if (beatClock != null && beatClock.IsPlaying && !muteMetronome)
            {
                ScheduleUpcomingTicks();
            }

            // Process visual updates
            ProcessScheduledVisuals();
        }

        private void HandlePlayStateChanged()
        {
            if (beatClock != null && !beatClock.IsPlaying && !beatClock.IsCountingIn)
            {
                // Stop all scheduled metronome sounds immediately
                StopAllScheduledSounds();
                ResetSchedulingState();
            }
        }

        private void StopAllScheduledSounds()
        {
            if (audioMixer == null) return;

            foreach (var handleId in scheduledHandleIds)
            {
                if (handleId > 0)
                {
                    audioMixer.StopNoteImmediate(handleId);
                }
            }
            scheduledHandleIds.Clear();
        }

        private void HandleCountInStarted()
        {
            scheduledCountInBeats.Clear();
            // Immediately schedule all count-in beats
            ScheduleCountInTicks();
        }

        private void HandleLoopComplete()
        {
            // Clean up old scheduled beats
            scheduledBeats.Clear();
            lastScheduledBeat = -1;
        }

        private void ResetSchedulingState()
        {
            scheduledBeats.Clear();
            scheduledCountInBeats.Clear();
            scheduledHandleIds.Clear();
            lastScheduledLoop = -1;
            lastScheduledBeat = -1;
            scheduledVisuals.Clear();
        }

        private void ScheduleCountInTicks()
        {
            if (tickSound == null && accentSound == null) return;
            if (audioMixer == null) return;

            int countInBeats = beatClock.CountInBeats;
            int beatsPerBar = beatClock.BeatsPerBar;

            for (int beat = 0; beat < countInBeats; beat++)
            {
                if (scheduledCountInBeats.Contains(beat)) continue;

                scheduledCountInBeats.Add(beat);

                // Accent on bar start (beat 0 of each bar)
                bool isAccent = playAccentOnBarStart && (beat % beatsPerBar == 0);
                AudioClip clip = isAccent && accentSound != null ? accentSound : tickSound;
                float volume = isAccent ? accentVolume : tickVolume;

                if (clip != null)
                {
                    double dspTime = beatClock.GetDspTimeForCountInBeat(beat);

                    if (dspTime > AudioSettings.dspTime)
                    {
                        int handleId = audioMixer.ScheduleNote(clip, dspTime, volume, 1f, -1);
                        if (handleId > 0)
                            scheduledHandleIds.Add(handleId);
                        scheduledVisuals.Add((beat, dspTime));
                    }
                }
            }
        }

        private void ScheduleUpcomingTicks()
        {
            if (tickSound == null && accentSound == null) return;
            if (audioMixer == null) return;

            int currentLoop = beatClock.CurrentLoop;
            double currentBeat = beatClock.CurrentBeatPositionPrecise;
            int totalBeats = beatClock.TotalBeats;
            int beatsPerBar = beatClock.BeatsPerBar;

            // Reset on new loop
            if (currentLoop > lastScheduledLoop)
            {
                scheduledBeats.Clear();
                lastScheduledLoop = currentLoop;
                lastScheduledBeat = -0.001;
            }

            // Calculate scheduling window
            double scheduleFrom = lastScheduledBeat;
            double scheduleUpTo = currentBeat + LOOKAHEAD_BEATS;
            if (scheduleUpTo > totalBeats) scheduleUpTo = totalBeats;

            // Schedule beats in window
            for (int beat = 0; beat < totalBeats; beat++)
            {
                // Check if in scheduling window
                if (beat <= scheduleFrom || beat > scheduleUpTo) continue;
                if (scheduledBeats.Contains(beat)) continue;

                scheduledBeats.Add(beat);

                // Determine which sound to play
                bool isAccent = playAccentOnBarStart && (beat % beatsPerBar == 0);
                AudioClip clip = isAccent && accentSound != null ? accentSound : tickSound;
                float volume = isAccent ? accentVolume : tickVolume;

                if (clip != null)
                {
                    double dspTime = beatClock.GetDspTimeForBeatInLoop(beat, currentLoop);

                    if (dspTime > AudioSettings.dspTime)
                    {
                        int handleId = audioMixer.ScheduleNote(clip, dspTime, volume, 1f, -1);
                        if (handleId > 0)
                            scheduledHandleIds.Add(handleId);

                        // Schedule visual pulse
                        scheduledVisuals.Add((beat, dspTime));
                    }
                }
            }

            lastScheduledBeat = scheduleUpTo;

            // Schedule next loop start if needed
            if (scheduleUpTo >= totalBeats - 0.001)
            {
                ScheduleNextLoopStart(currentLoop + 1, beatsPerBar);
            }
        }

        private void ScheduleNextLoopStart(int nextLoop, int beatsPerBar)
        {
            double currentBeat = beatClock.CurrentBeatPositionPrecise;
            double scheduleUpTo = (currentBeat + LOOKAHEAD_BEATS) - beatClock.TotalBeats;
            if (scheduleUpTo < 0) return;

            for (int beat = 0; beat <= (int)scheduleUpTo; beat++)
            {
                // Create unique key for next loop
                int nextLoopBeatKey = beat + 1000; // Offset to distinguish from current loop
                if (scheduledBeats.Contains(nextLoopBeatKey)) continue;

                scheduledBeats.Add(nextLoopBeatKey);

                bool isAccent = playAccentOnBarStart && (beat % beatsPerBar == 0);
                AudioClip clip = isAccent && accentSound != null ? accentSound : tickSound;
                float volume = isAccent ? accentVolume : tickVolume;

                if (clip != null)
                {
                    double dspTime = beatClock.GetDspTimeForBeatInLoop(beat, nextLoop);

                    if (dspTime > AudioSettings.dspTime)
                    {
                        int handleId = audioMixer.ScheduleNote(clip, dspTime, volume, 1f, -1);
                        if (handleId > 0)
                            scheduledHandleIds.Add(handleId);
                        scheduledVisuals.Add((beat, dspTime));
                    }
                }
            }
        }

        private void ProcessScheduledVisuals()
        {
            double currentDsp = AudioSettings.dspTime;

            for (int i = scheduledVisuals.Count - 1; i >= 0; i--)
            {
                var (beat, dspTime) = scheduledVisuals[i];

                // Trigger visual when time arrives
                if (currentDsp >= dspTime - 0.01)
                {
                    TriggerVisualPulse();
                    scheduledVisuals.RemoveAt(i);
                }
            }
        }

        private void TriggerVisualPulse()
        {
            pulseTimer = pulseDuration;
        }

        public void SetBeatClock(BeatClock clock)
        {
            if (beatClock != null)
            {
                beatClock.OnPlayStateChanged -= HandlePlayStateChanged;
                beatClock.OnLoopComplete -= HandleLoopComplete;
                beatClock.OnCountInStarted -= HandleCountInStarted;
            }

            beatClock = clock;

            if (beatClock != null)
            {
                beatClock.OnPlayStateChanged += HandlePlayStateChanged;
                beatClock.OnLoopComplete += HandleLoopComplete;
                beatClock.OnCountInStarted += HandleCountInStarted;
            }

            ResetSchedulingState();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            tickVolume = Mathf.Clamp01(tickVolume);
            accentVolume = Mathf.Clamp01(accentVolume);
        }
#endif
    }
}
