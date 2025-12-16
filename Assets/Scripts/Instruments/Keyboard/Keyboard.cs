using System;
using System.Collections.Generic;
using UnityEngine;
using SoloBandStudio.Core;
using SoloBandStudio.Audio;

namespace SoloBandStudio.Instruments.Keyboard
{
    /// <summary>
    /// Keyboard instrument implementing IInstrument with CustomAudioMixer.
    /// Uses sample-accurate DSP scheduling for precise timing.
    /// </summary>
    public class Keyboard : MonoBehaviour, IInstrument
    {
        [Header("Components")]
        [SerializeField] private KeyboardLayout keyboard;
        [SerializeField] private KeyboardSynthesizer synthesizer;

        [Header("Settings")]
        [SerializeField] private InstrumentType instrumentType = InstrumentType.Piano;
        [SerializeField] private string instrumentId;
        [SerializeField] private string instrumentName = "Piano";
        [SerializeField] private float volume = 0.8f;
        [SerializeField] private bool autoRegister = true;

        private bool isInitialized;
        private CustomAudioMixer audioMixer;

        // Track active notes (for user input)
        private Dictionary<int, int> activeUserNotes = new Dictionary<int, int>(); // midiNote -> handleId

        // Track scheduled notes
        private List<ScheduledNoteHandle> scheduledNotes = new List<ScheduledNoteHandle>();

        // Track pending visual update coroutines (to cancel on stop)
        private List<Coroutine> pendingVisualCoroutines = new List<Coroutine>();

        // IInstrument implementation
        public string InstrumentId => string.IsNullOrEmpty(instrumentId) ? gameObject.GetInstanceID().ToString() : instrumentId;
        public string InstrumentName => instrumentName;
        public InstrumentType Type => instrumentType;
        public float Volume => volume;

        public event Action<int, float> OnNoteOn;
        public event Action<int> OnNoteOff;

        // Additional properties
        public KeyboardLayout KeyboardLayout => keyboard;
        public KeyboardSynthesizer Synthesizer => synthesizer;
        public bool IsInitialized => isInitialized;

        private void Awake()
        {
            if (keyboard == null)
                keyboard = GetComponentInChildren<KeyboardLayout>();
            if (synthesizer == null)
                synthesizer = GetComponentInChildren<KeyboardSynthesizer>();
        }

        private void Start()
        {
            Initialize();
            if (autoRegister)
                RegisterWithLoopStation();
        }

        private void OnDestroy()
        {
            if (autoRegister)
                UnregisterFromLoopStation();
        }

        public void Initialize()
        {
            if (isInitialized) return;

            audioMixer = CustomAudioMixer.Instance;
            if (audioMixer == null)
            {
                Debug.LogError("[Keyboard] CustomAudioMixer not found! Make sure it's initialized before instruments.");
                return;
            }

            // Preload all samples into cache
            PreloadSamples();

            if (keyboard != null)
            {
                // Subscribe to keyboard input events
                keyboard.OnKeyPressed += HandleKeyPressed;
                keyboard.OnKeyReleased += HandleKeyReleased;
            }

            isInitialized = true;
            Debug.Log($"[Keyboard] {instrumentName} initialized with CustomAudioMixer");
        }

        /// <summary>
        /// Preload all samples into SampleDataCache for audio thread access.
        /// </summary>
        private void PreloadSamples()
        {
            if (synthesizer == null) return;

            var cache = SampleDataCache.Instance;

            // Get sample bank from synthesizer via reflection or direct access
            // For now, preload samples by querying common MIDI range
            for (int midiNote = 21; midiNote <= 108; midiNote++) // A0 to C8
            {
                if (synthesizer.GetSampleForMidiNote(midiNote, out AudioClip clip, out _))
                {
                    cache.Preload(clip);
                }
            }

            var (clipCount, bytes) = cache.GetStats();
            Debug.Log($"[Keyboard] Preloaded samples: {clipCount} clips, {bytes / 1024}KB");
        }

        #region User Input (from KeyboardLayout)

        private void HandleKeyPressed(int midiNote, float velocity)
        {
            if (!isInitialized || synthesizer == null || audioMixer == null) return;

            // Stop any existing note on this key
            if (activeUserNotes.TryGetValue(midiNote, out int existingHandle))
            {
                audioMixer.StopNoteImmediate(existingHandle);
                activeUserNotes.Remove(midiNote);
            }

            // Get sample and pitch
            if (!synthesizer.GetSampleForMidiNote(midiNote, out AudioClip clip, out float pitch))
                return;

            // Play immediately
            int handleId = audioMixer.PlayNoteNow(clip, volume * velocity, pitch, midiNote);
            if (handleId > 0)
            {
                activeUserNotes[midiNote] = handleId;
            }

            // Fire event for recording
            OnNoteOn?.Invoke(midiNote, velocity);

            // Update visual (skip in toggle mode - quiz handles this)
            if (keyboard != null && !keyboard.ToggleMode)
            {
                keyboard.SetKeyVisualState(midiNote, true);
            }
        }

        private void HandleKeyReleased(int midiNote)
        {
            if (!isInitialized) return;

            // Stop the note with fade
            if (activeUserNotes.TryGetValue(midiNote, out int handleId))
            {
                audioMixer?.ScheduleStop(handleId, AudioSettings.dspTime + 0.01, 0.1f);
                activeUserNotes.Remove(midiNote);
            }

            // Fire event for recording
            OnNoteOff?.Invoke(midiNote);

            // Update visual (skip in toggle mode - quiz handles this)
            if (keyboard != null && !keyboard.ToggleMode)
            {
                keyboard.SetKeyVisualState(midiNote, false);
            }
        }

        #endregion

        #region IInstrument - DSP Scheduled Playback

        /// <summary>
        /// Schedule a note to play at a specific DSP time.
        /// </summary>
        public ScheduledNoteHandle ScheduleNote(int midiNote, float velocity, double dspTime)
        {
            if (!isInitialized || synthesizer == null || audioMixer == null)
                return null;

            if (!synthesizer.GetSampleForMidiNote(midiNote, out AudioClip clip, out float pitch))
                return null;

            int handleId = audioMixer.ScheduleNote(clip, dspTime, volume * velocity, pitch, midiNote);
            if (handleId <= 0)
                return null;

            var handle = new ScheduledNoteHandle(midiNote, dspTime, handleId);
            scheduledNotes.Add(handle);

            // Schedule visual update (approximate, for visual feedback)
            ScheduleVisualUpdate(midiNote, true, dspTime);

            return handle;
        }

        /// <summary>
        /// Schedule a note to stop at a specific DSP time.
        /// </summary>
        public void ScheduleNoteOff(ScheduledNoteHandle handle, double dspTime, float fadeOutDuration = 0.05f)
        {
            if (handle == null || !handle.IsValid || audioMixer == null) return;

            audioMixer.ScheduleStop(handle.HandleId, dspTime, fadeOutDuration);
            scheduledNotes.Remove(handle);

            // Schedule visual update
            ScheduleVisualUpdate(handle.MidiNote, false, dspTime);
        }

        /// <summary>
        /// Stop all playing and scheduled notes immediately.
        /// </summary>
        public void StopAllNotes()
        {
            // Cancel all pending visual update coroutines FIRST
            foreach (var coroutine in pendingVisualCoroutines)
            {
                if (coroutine != null)
                    StopCoroutine(coroutine);
            }
            pendingVisualCoroutines.Clear();

            // Stop user notes
            foreach (var kvp in activeUserNotes)
            {
                audioMixer?.StopNoteImmediate(kvp.Value);
            }
            activeUserNotes.Clear();

            // Stop scheduled notes
            foreach (var handle in scheduledNotes)
            {
                if (handle.IsValid)
                {
                    audioMixer?.StopNoteImmediate(handle.HandleId);
                }
            }
            scheduledNotes.Clear();

            // Reset all key visuals
            keyboard?.ResetAllKeyVisuals();
        }

        /// <summary>
        /// Cancel all scheduled notes that haven't started yet.
        /// </summary>
        public void CancelAllScheduled()
        {
            double currentDsp = AudioSettings.dspTime;

            for (int i = scheduledNotes.Count - 1; i >= 0; i--)
            {
                var handle = scheduledNotes[i];
                if (handle.StartDspTime > currentDsp && handle.IsValid)
                {
                    audioMixer?.StopNoteImmediate(handle.HandleId);
                    scheduledNotes.RemoveAt(i);
                }
            }
        }

        #endregion

        #region Visual Updates

        private void ScheduleVisualUpdate(int midiNote, bool pressed, double dspTime)
        {
            // Calculate delay from now
            float delay = (float)(dspTime - AudioSettings.dspTime);
            if (delay < 0.01f)
            {
                // Immediate
                keyboard?.SetKeyVisualState(midiNote, pressed);
            }
            else
            {
                // Delayed (approximate - visual doesn't need to be sample-accurate)
                var coroutine = StartCoroutine(DelayedVisualUpdate(midiNote, pressed, delay));
                pendingVisualCoroutines.Add(coroutine);
            }
        }

        private System.Collections.IEnumerator DelayedVisualUpdate(int midiNote, bool pressed, float delay)
        {
            yield return new WaitForSeconds(delay);
            keyboard?.SetKeyVisualState(midiNote, pressed);
        }

        #endregion

        #region Registration

        public void RegisterWithLoopStation()
        {
            var loopStation = FindFirstObjectByType<LoopStation>();
            loopStation?.RegisterInstrument(this);
        }

        public void UnregisterFromLoopStation()
        {
            var loopStation = FindFirstObjectByType<LoopStation>();
            loopStation?.UnregisterInstrument(this);
        }

        #endregion

        private void Update()
        {
            // Clean up finished scheduled notes
            double currentDsp = AudioSettings.dspTime;
            for (int i = scheduledNotes.Count - 1; i >= 0; i--)
            {
                var handle = scheduledNotes[i];

                // Remove invalid handles
                if (!handle.IsValid)
                {
                    scheduledNotes.RemoveAt(i);
                    continue;
                }

                // Remove handles that are old enough to have finished
                // (Assume max note duration of 10 seconds)
                if (handle.StartDspTime < currentDsp - 10.0)
                {
                    scheduledNotes.RemoveAt(i);
                }
            }

            // Clean up finished visual coroutines
            pendingVisualCoroutines.RemoveAll(c => c == null);
        }
    }
}
