using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace SoloBandStudio.Audio
{
    /// <summary>
    /// Custom audio mixer using OnAudioFilterRead for sample-accurate timing.
    /// All audio is mixed directly in the audio thread, independent of frame rate.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CustomAudioMixer : MonoBehaviour
    {
        private static CustomAudioMixer instance;
        public static CustomAudioMixer Instance => instance;

        [Header("Settings")]
        [SerializeField] private float masterVolume = 0.7f;
        [SerializeField] private int maxVoices = 64;

        [Header("Debug")]
        [SerializeField] private int activeVoiceCount;
        [SerializeField] private int pendingNoteCount;
        [SerializeField] private bool enableDebugLog = false;

        // Thread-safe queue for scheduling notes from main thread
        private ConcurrentQueue<ScheduledNote> pendingNotes = new ConcurrentQueue<ScheduledNote>();

        // Active voices (manipulated only in audio thread)
        private List<Voice> activeVoices = new List<Voice>();
        private List<Voice> voicePool = new List<Voice>();

        // Stop commands (thread-safe)
        private ConcurrentQueue<StopCommand> stopCommands = new ConcurrentQueue<StopCommand>();

        // Pending stop commands (for notes not yet activated)
        private Dictionary<int, StopCommand> pendingStops = new Dictionary<int, StopCommand>();

        // DSP state
        private int sampleRate;
        private int channels;
        private double dspTime;
        private long totalSamplesWritten;

        // Events (invoked on main thread via Update)
        private ConcurrentQueue<NoteEvent> noteEvents = new ConcurrentQueue<NoteEvent>();
        public event Action<int, int> OnVoiceStarted; // handleId, midiNote
        public event Action<int> OnVoiceStopped; // handleId

        // Handle tracking
        private int nextHandleId = 1;

        /// <summary>
        /// Represents a note scheduled to play.
        /// </summary>
        private struct ScheduledNote
        {
            public int HandleId;
            public int ClipId;
            public float Pitch;
            public float Volume;
            public double StartDspTime;
            public double StopDspTime; // 0 = play until sample ends
            public float FadeOutDuration;
            public int MidiNote;
        }

        /// <summary>
        /// Represents a stop command for a voice.
        /// </summary>
        private struct StopCommand
        {
            public int HandleId;
            public double StopDspTime;
            public float FadeOutDuration;
            public bool Immediate;
        }

        /// <summary>
        /// Active voice playing audio.
        /// </summary>
        private class Voice
        {
            public int HandleId;
            public int MidiNote;
            public SampleDataCache.CachedSample Sample;
            public float Pitch;
            public float Volume;
            public double StartDspTime;
            public double StopDspTime; // 0 = no stop scheduled
            public float FadeOutDuration;

            // Playback state
            public double SamplePosition; // Fractional sample position
            public bool IsActive;
            public bool IsFadingOut;
            public float FadeOutProgress;

            public void Reset()
            {
                HandleId = 0;
                MidiNote = 0;
                Sample = default;
                Pitch = 1f;
                Volume = 1f;
                StartDspTime = 0;
                StopDspTime = 0;
                FadeOutDuration = 0;
                SamplePosition = 0;
                IsActive = false;
                IsFadingOut = false;
                FadeOutProgress = 0;
            }
        }

        private struct NoteEvent
        {
            public int HandleId;
            public int MidiNote;
            public bool IsStart;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize voice pool
            for (int i = 0; i < maxVoices; i++)
            {
                voicePool.Add(new Voice());
            }

            // Get audio settings
            sampleRate = AudioSettings.outputSampleRate;
            var config = AudioSettings.GetConfiguration();
            channels = config.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;

            Debug.Log($"[CustomAudioMixer] Initialized: {sampleRate}Hz, {channels}ch, {maxVoices} max voices");
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Update()
        {
            // Update debug info
            activeVoiceCount = activeVoices.Count;
            pendingNoteCount = pendingNotes.Count;

            // Process events on main thread
            while (noteEvents.TryDequeue(out var evt))
            {
                if (evt.IsStart)
                    OnVoiceStarted?.Invoke(evt.HandleId, evt.MidiNote);
                else
                    OnVoiceStopped?.Invoke(evt.HandleId);
            }
        }

        #region Public API (Main Thread)

        /// <summary>
        /// Schedule a note to play at a specific DSP time with optional stop time.
        /// Thread-safe, can be called from main thread.
        /// </summary>
        /// <param name="clip">AudioClip to play (must be preloaded in SampleDataCache)</param>
        /// <param name="startDspTime">DSP time to start playback</param>
        /// <param name="stopDspTime">DSP time to stop playback (0 = play until sample ends)</param>
        /// <param name="volume">Volume (0-1)</param>
        /// <param name="pitch">Pitch multiplier</param>
        /// <param name="midiNote">MIDI note for tracking</param>
        /// <param name="fadeOutDuration">Fade out duration in seconds</param>
        /// <returns>Handle ID for the scheduled note, or -1 if failed</returns>
        public int ScheduleNote(AudioClip clip, double startDspTime, double stopDspTime, float volume, float pitch, int midiNote, float fadeOutDuration = 0.05f)
        {
            if (clip == null) return -1;

            // Ensure clip is cached
            if (!SampleDataCache.Instance.IsCached(clip))
            {
                Debug.LogWarning($"[CustomAudioMixer] Clip not cached: {clip.name}. Caching now (may cause frame drop)");
                SampleDataCache.Instance.Preload(clip);
            }

            int handleId = nextHandleId++;
            var note = new ScheduledNote
            {
                HandleId = handleId,
                ClipId = clip.GetInstanceID(),
                Pitch = pitch,
                Volume = volume,
                StartDspTime = startDspTime,
                StopDspTime = stopDspTime,
                FadeOutDuration = fadeOutDuration,
                MidiNote = midiNote
            };

            pendingNotes.Enqueue(note);

            if (enableDebugLog)
                Debug.Log($"[CustomAudioMixer] Scheduled note {handleId}: midi={midiNote}, start={startDspTime:F4}, stop={stopDspTime:F4}");

            return handleId;
        }

        /// <summary>
        /// Schedule a note to play (legacy overload without stop time).
        /// </summary>
        public int ScheduleNote(AudioClip clip, double dspTime, float volume, float pitch, int midiNote)
        {
            return ScheduleNote(clip, dspTime, 0, volume, pitch, midiNote, 0.05f);
        }

        /// <summary>
        /// Schedule a note to play immediately (plays until sample ends).
        /// </summary>
        public int PlayNoteNow(AudioClip clip, float volume, float pitch, int midiNote)
        {
            return ScheduleNote(clip, AudioSettings.dspTime + 0.01, 0, volume, pitch, midiNote, 0.1f);
        }

        /// <summary>
        /// Schedule a note to stop at a specific DSP time.
        /// </summary>
        public void ScheduleStop(int handleId, double dspTime, float fadeOutDuration = 0.05f)
        {
            if (handleId <= 0) return;

            stopCommands.Enqueue(new StopCommand
            {
                HandleId = handleId,
                StopDspTime = dspTime,
                FadeOutDuration = fadeOutDuration,
                Immediate = false
            });
        }

        /// <summary>
        /// Stop a note immediately.
        /// </summary>
        public void StopNoteImmediate(int handleId)
        {
            if (handleId <= 0) return;

            stopCommands.Enqueue(new StopCommand
            {
                HandleId = handleId,
                Immediate = true
            });
        }

        /// <summary>
        /// Stop all notes immediately.
        /// </summary>
        public void StopAllNotes()
        {
            // Clear pending notes
            while (pendingNotes.TryDequeue(out _)) { }

            // Stop all active voices
            foreach (var voice in activeVoices)
            {
                if (voice.IsActive)
                {
                    stopCommands.Enqueue(new StopCommand
                    {
                        HandleId = voice.HandleId,
                        Immediate = true
                    });
                }
            }
        }

        /// <summary>
        /// Set master volume.
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
        }

        /// <summary>
        /// Get current voice count.
        /// </summary>
        public int ActiveVoiceCount => activeVoices.Count;

        #endregion

        #region Audio Thread (OnAudioFilterRead)

        /// <summary>
        /// Called by Unity's audio system on the audio thread.
        /// This is where all mixing happens.
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            int samplesPerChannel = data.Length / channels;

            // Use actual DSP time from Unity, not our tracked time
            double bufferStartDsp = AudioSettings.dspTime;
            double bufferEndDsp = bufferStartDsp + (double)samplesPerChannel / sampleRate;

            // Process stop commands
            ProcessStopCommands(bufferStartDsp);

            // Activate pending notes
            ActivatePendingNotes(bufferEndDsp);

            // Clear output buffer
            Array.Clear(data, 0, data.Length);

            // Mix all active voices
            for (int i = activeVoices.Count - 1; i >= 0; i--)
            {
                var voice = activeVoices[i];
                if (!voice.IsActive)
                {
                    ReturnVoiceToPool(voice, i);
                    continue;
                }

                MixVoice(voice, data, channels, samplesPerChannel, bufferStartDsp);

                // Check if voice finished
                if (!voice.IsActive)
                {
                    noteEvents.Enqueue(new NoteEvent { HandleId = voice.HandleId, MidiNote = voice.MidiNote, IsStart = false });
                    ReturnVoiceToPool(voice, i);
                }
            }

            // Update DSP time
            dspTime = bufferEndDsp;
            totalSamplesWritten += samplesPerChannel;
        }

        private void ProcessStopCommands(double currentDsp)
        {
            while (stopCommands.TryDequeue(out var cmd))
            {
                bool found = false;
                foreach (var voice in activeVoices)
                {
                    if (voice.HandleId == cmd.HandleId && voice.IsActive)
                    {
                        if (cmd.Immediate)
                        {
                            voice.IsActive = false;
                        }
                        else
                        {
                            voice.StopDspTime = cmd.StopDspTime;
                            voice.FadeOutDuration = cmd.FadeOutDuration;
                        }
                        found = true;
                        break;
                    }
                }

                // If voice not found yet, save for later (note might not be activated yet)
                if (!found && !cmd.Immediate)
                {
                    pendingStops[cmd.HandleId] = cmd;
                }
            }
        }

        private void ActivatePendingNotes(double bufferEndDsp)
        {
            // Process ALL notes in queue that should start within this buffer or earlier
            // ConcurrentQueue is NOT sorted by time, so we need to check all
            int count = pendingNotes.Count;
            for (int i = 0; i < count; i++)
            {
                if (!pendingNotes.TryDequeue(out var note))
                    break;

                if (note.StartDspTime <= bufferEndDsp)
                {
                    // This note should play now
                    if (!SampleDataCache.Instance.TryGetSample(note.ClipId, out var sample))
                    {
                        Debug.LogWarning($"[CustomAudioMixer] Sample not found for clip ID: {note.ClipId}");
                        continue;
                    }

                    var voice = GetVoiceFromPool();
                    if (voice == null)
                    {
                        Debug.LogWarning("[CustomAudioMixer] Voice pool exhausted!");
                        continue;
                    }

                    voice.HandleId = note.HandleId;
                    voice.MidiNote = note.MidiNote;
                    voice.Sample = sample;
                    voice.Pitch = note.Pitch;
                    voice.Volume = note.Volume;
                    voice.StartDspTime = note.StartDspTime;
                    voice.StopDspTime = note.StopDspTime; // Use stop time from scheduled note
                    voice.FadeOutDuration = note.FadeOutDuration;
                    voice.SamplePosition = 0;
                    voice.IsActive = true;
                    voice.IsFadingOut = false;
                    voice.FadeOutProgress = 0;

                    // Check if there's a pending stop command for this note
                    if (pendingStops.TryGetValue(note.HandleId, out var stopCmd))
                    {
                        voice.StopDspTime = stopCmd.StopDspTime;
                        voice.FadeOutDuration = stopCmd.FadeOutDuration;
                        pendingStops.Remove(note.HandleId);
                    }

                    activeVoices.Add(voice);

                    noteEvents.Enqueue(new NoteEvent { HandleId = note.HandleId, MidiNote = note.MidiNote, IsStart = true });
                }
                else
                {
                    // Not yet time - put back in queue
                    pendingNotes.Enqueue(note);
                }
            }
        }

        private void MixVoice(Voice voice, float[] data, int channels, int samplesPerChannel, double bufferStartDsp)
        {
            var sample = voice.Sample;
            if (sample.Data == null || sample.Data.Length == 0)
            {
                voice.IsActive = false;
                return;
            }

            double pitchRatio = (double)sample.SampleRate / sampleRate * voice.Pitch;
            float baseVolume = voice.Volume * masterVolume;

            for (int i = 0; i < samplesPerChannel; i++)
            {
                double sampleDspTime = bufferStartDsp + (double)i / sampleRate;

                // Check if note should start
                if (sampleDspTime < voice.StartDspTime)
                    continue;

                // Check if note should stop
                if (voice.StopDspTime > 0 && sampleDspTime >= voice.StopDspTime)
                {
                    if (!voice.IsFadingOut && voice.FadeOutDuration > 0)
                    {
                        voice.IsFadingOut = true;
                        voice.FadeOutProgress = 0;
                    }
                    else if (voice.FadeOutDuration <= 0)
                    {
                        voice.IsActive = false;
                        return;
                    }
                }

                // Calculate volume with fade
                float volume = baseVolume;
                if (voice.IsFadingOut)
                {
                    voice.FadeOutProgress += 1f / (voice.FadeOutDuration * sampleRate);
                    if (voice.FadeOutProgress >= 1f)
                    {
                        voice.IsActive = false;
                        return;
                    }
                    volume *= 1f - voice.FadeOutProgress;
                }

                // Get sample position
                int samplePos = (int)voice.SamplePosition;
                if (samplePos >= sample.SampleCount)
                {
                    // Sample finished
                    voice.IsActive = false;
                    return;
                }

                // Linear interpolation for better quality
                double frac = voice.SamplePosition - samplePos;
                int nextPos = Math.Min(samplePos + 1, sample.SampleCount - 1);

                // Mix into output buffer
                for (int ch = 0; ch < channels; ch++)
                {
                    int srcCh = ch % sample.Channels;
                    int srcIndex = samplePos * sample.Channels + srcCh;
                    int nextSrcIndex = nextPos * sample.Channels + srcCh;

                    if (srcIndex < sample.Data.Length && nextSrcIndex < sample.Data.Length)
                    {
                        // Linear interpolation
                        float s1 = sample.Data[srcIndex];
                        float s2 = sample.Data[nextSrcIndex];
                        float interpolated = (float)(s1 + (s2 - s1) * frac);

                        int outIndex = i * channels + ch;
                        data[outIndex] += interpolated * volume;
                    }
                }

                // Advance sample position
                voice.SamplePosition += pitchRatio;
            }
        }

        private Voice GetVoiceFromPool()
        {
            if (voicePool.Count > 0)
            {
                var voice = voicePool[voicePool.Count - 1];
                voicePool.RemoveAt(voicePool.Count - 1);
                voice.Reset();
                return voice;
            }

            // Pool exhausted - create new if under limit
            if (activeVoices.Count < maxVoices)
            {
                return new Voice();
            }

            return null;
        }

        private void ReturnVoiceToPool(Voice voice, int index)
        {
            activeVoices.RemoveAt(index);
            voice.Reset();
            voicePool.Add(voice);
        }

        #endregion

        #region Debug

        public void LogStatus()
        {
            var (clipCount, bytes) = SampleDataCache.Instance.GetStats();
            Debug.Log($"[CustomAudioMixer] Active: {activeVoices.Count}, Pool: {voicePool.Count}, " +
                     $"Pending: {pendingNotes.Count}, Cached: {clipCount} clips ({bytes / 1024}KB)");
        }

        #endregion
    }
}
