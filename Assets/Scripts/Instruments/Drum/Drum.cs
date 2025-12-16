using System;
using System.Collections.Generic;
using UnityEngine;
using SoloBandStudio.Core;
using SoloBandStudio.Audio;

namespace SoloBandStudio.Instruments.Drum
{
    /// <summary>
    /// Drum instrument implementing IInstrument with CustomAudioMixer.
    /// Drums are percussive - notes don't have duration/release.
    /// </summary>
    public class Drum : MonoBehaviour, IInstrument
    {
        [Header("Components")]
        [SerializeField] private DrumKit drumKit;
        [SerializeField] private DrumSoundBank soundBank;

        [Header("Settings")]
        [SerializeField] private string instrumentId;
        [SerializeField] private string instrumentName = "Drum Kit";
        [SerializeField] private float volume = 0.8f;
        [SerializeField] private bool autoInitialize = true;
        [SerializeField] private bool autoRegister = true;

        [Header("Debug")]
        [SerializeField] private bool debugLog = false;

        private VirtualDrumEngine engine;
        private bool isInitialized;
        private CustomAudioMixer audioMixer;

        // Track scheduled notes
        private List<ScheduledNoteHandle> scheduledNotes = new List<ScheduledNoteHandle>();

        // IInstrument implementation
        public string InstrumentId => string.IsNullOrEmpty(instrumentId) ? gameObject.GetInstanceID().ToString() : instrumentId;
        public string InstrumentName => instrumentName;
        public InstrumentType Type => InstrumentType.Drum;

        public event Action<int, float> OnNoteOn;
        public event Action<int> OnNoteOff;

        // Additional properties
        public DrumKit Kit => drumKit;
        public DrumSoundBank SoundBank => soundBank;
        public VirtualDrumEngine Engine => engine;
        public bool IsInitialized => isInitialized;

        private void Awake()
        {
            if (drumKit == null)
                drumKit = GetComponentInChildren<DrumKit>();
        }

        private void Start()
        {
            if (autoInitialize)
                Initialize();
            if (autoRegister)
                RegisterWithLoopStation();
        }

        private void OnDestroy()
        {
            if (autoRegister)
                UnregisterFromLoopStation();

            if (engine != null)
            {
                engine.OnNoteOn -= HandleEngineNoteOn;
                engine.OnNoteOff -= HandleEngineNoteOff;
            }
        }

        public void Initialize()
        {
            if (isInitialized) return;

            audioMixer = CustomAudioMixer.Instance;
            if (audioMixer == null)
            {
                Debug.LogError("[Drum] CustomAudioMixer not found! Make sure it's initialized before instruments.");
                return;
            }

            if (drumKit == null)
            {
                Debug.LogError("[Drum] DrumKit not found!");
                return;
            }

            if (soundBank == null)
            {
                Debug.LogWarning("[Drum] DrumSoundBank not assigned - no sounds will play");
            }

            // Preload samples
            PreloadSamples();

            // Create engine for user input handling
            engine = gameObject.AddComponent<VirtualDrumEngine>();
            engine.SetSoundBank(soundBank);
            engine.SetDebugLog(debugLog);

            // Subscribe to engine events for recording
            engine.OnNoteOn += HandleEngineNoteOn;
            engine.OnNoteOff += HandleEngineNoteOff;

            // Initialize kit with engine reference
            drumKit.Initialize(engine);

            isInitialized = true;
            Debug.Log($"[Drum] {instrumentName} initialized with CustomAudioMixer");
        }

        /// <summary>
        /// Preload all drum samples into cache.
        /// </summary>
        private void PreloadSamples()
        {
            if (soundBank == null) return;

            var cache = SampleDataCache.Instance;
            foreach (DrumPartType partType in Enum.GetValues(typeof(DrumPartType)))
            {
                var samples = soundBank.GetSamples(partType);
                foreach (var clip in samples)
                {
                    if (clip != null)
                        cache.Preload(clip);
                }
            }

            var (clipCount, bytes) = cache.GetStats();
            Debug.Log($"[Drum] Preloaded samples: {clipCount} clips, {bytes / 1024}KB");
        }

        #region Engine Event Handlers

        private void HandleEngineNoteOn(int midiNote, float velocity)
        {
            OnNoteOn?.Invoke(midiNote, velocity);
        }

        private void HandleEngineNoteOff(int midiNote)
        {
            OnNoteOff?.Invoke(midiNote);
        }

        #endregion

        #region IInstrument - DSP Scheduled Playback

        public ScheduledNoteHandle ScheduleNote(int midiNote, float velocity, double dspTime)
        {
            if (!isInitialized || soundBank == null || audioMixer == null)
                return null;

            DrumPartType partType = (DrumPartType)midiNote;
            var clip = soundBank.GetSample(partType);
            if (clip == null)
                return null;

            int handleId = audioMixer.ScheduleNote(clip, dspTime, volume * velocity, 1f, midiNote);
            if (handleId <= 0)
                return null;

            var handle = new ScheduledNoteHandle(midiNote, dspTime, handleId);
            scheduledNotes.Add(handle);

            return handle;
        }

        public void ScheduleNoteOff(ScheduledNoteHandle handle, double dspTime, float fadeOutDuration = 0.05f)
        {
            // Drums are percussive - no note-off needed
            // Just remove from tracking
            if (handle != null)
            {
                scheduledNotes.Remove(handle);
            }
        }

        public void StopAllNotes()
        {
            // Stop all scheduled notes
            foreach (var handle in scheduledNotes)
            {
                if (handle.IsValid)
                {
                    audioMixer?.StopNoteImmediate(handle.HandleId);
                }
            }
            scheduledNotes.Clear();

            // Also reset engine visual state
            engine?.ResetPlaybackState();
        }

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
                // (Drum samples are typically short, 2 seconds max)
                if (handle.StartDspTime < currentDsp - 2.0)
                {
                    scheduledNotes.RemoveAt(i);
                }
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Initialize")]
        private void EditorInitialize()
        {
            Initialize();
        }

        [ContextMenu("Register with LoopStation")]
        private void EditorRegister()
        {
            RegisterWithLoopStation();
        }
#endif
    }
}
