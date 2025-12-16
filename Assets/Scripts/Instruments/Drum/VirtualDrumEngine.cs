using System;
using System.Collections.Generic;
using UnityEngine;
using SoloBandStudio.Audio;

namespace SoloBandStudio.Instruments.Drum
{
    /// <summary>
    /// Virtual drum engine that manages drum state, audio playback, and events.
    /// Uses CustomAudioMixer for sample-accurate playback.
    /// </summary>
    public class VirtualDrumEngine : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private DrumSoundBank soundBank;
        [SerializeField] private float volume = 0.8f;

        [Header("Visual Feedback")]
        [SerializeField] private float hitDuration = 0.1f;  // How long the visual "hit" effect lasts

        [Header("Debug")]
        [SerializeField] private bool debugLog = false;

        private CustomAudioMixer audioMixer;
        private Dictionary<DrumPartType, float> lastHitTime = new Dictionary<DrumPartType, float>();

        // Events for recording (fired only on user input)
        public event Action<int, float> OnNoteOn;   // (midiNote, velocity)
        public event Action<int> OnNoteOff;         // (midiNote) - for drums, fired immediately after hit

        // Events for visual state changes
        public event Action<DrumPartType, bool> OnHitStateChanged; // (partType, isHit)

        public bool IsValid => soundBank != null;
        public float HitDuration => hitDuration;

        private void Awake()
        {
            audioMixer = CustomAudioMixer.Instance;
        }

        public void SetSoundBank(DrumSoundBank bank)
        {
            soundBank = bank;
            PreloadSamples();
        }

        public void SetDebugLog(bool enabled)
        {
            debugLog = enabled;
        }

        /// <summary>
        /// Preload all drum samples into cache.
        /// </summary>
        public void PreloadSamples()
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
            if (debugLog)
                Debug.Log($"[VirtualDrumEngine] Preloaded drum samples: {clipCount} clips, {bytes / 1024}KB");
        }

        #region User Input (called by DrumPad)

        /// <summary>
        /// Called when user hits a drum pad.
        /// </summary>
        public void HitDrum(DrumPartType partType, float velocity = 1f)
        {
            int midiNote = (int)partType;

            if (debugLog) Debug.Log($"[VirtualDrumEngine] HitDrum {partType} ({midiNote}) vel={velocity:F2}");

            PlaySound(partType, velocity);
            lastHitTime[partType] = Time.time;

            // Fire events for recording
            OnNoteOn?.Invoke(midiNote, velocity);
            OnNoteOff?.Invoke(midiNote);  // Drums are instant - note off immediately

            // Fire visual event
            OnHitStateChanged?.Invoke(partType, true);
        }

        #endregion

        #region Playback (called by LoopStation via Drum)

        /// <summary>
        /// Play a drum hit for playback (doesn't fire recording events).
        /// </summary>
        public void PlayNote(int midiNote, float velocity = 1f)
        {
            DrumPartType partType = (DrumPartType)midiNote;

            if (debugLog) Debug.Log($"[VirtualDrumEngine] PlayNote {partType} ({midiNote}) vel={velocity:F2}");

            PlaySound(partType, velocity);
            lastHitTime[partType] = Time.time;

            // Fire visual event
            OnHitStateChanged?.Invoke(partType, true);
        }

        /// <summary>
        /// Stop is a no-op for drums (they don't sustain).
        /// </summary>
        public void StopNote(int midiNote)
        {
            // Drums don't sustain, nothing to stop
        }

        /// <summary>
        /// Reset playback state - for drums, just clear visual states.
        /// </summary>
        public void ResetPlaybackState()
        {
            if (debugLog) Debug.Log($"[VirtualDrumEngine] ResetPlaybackState");

            foreach (DrumPartType partType in Enum.GetValues(typeof(DrumPartType)))
            {
                OnHitStateChanged?.Invoke(partType, false);
            }
            lastHitTime.Clear();
        }

        /// <summary>
        /// Stop all - for drums, just reset visual state.
        /// </summary>
        public void StopAllNotes()
        {
            ResetPlaybackState();
        }

        #endregion

        #region Audio

        private void PlaySound(DrumPartType partType, float velocity)
        {
            if (soundBank == null)
            {
                if (debugLog) Debug.LogWarning($"[VirtualDrumEngine] Cannot play sound - soundbank not ready");
                return;
            }

            if (audioMixer == null)
            {
                audioMixer = CustomAudioMixer.Instance;
                if (audioMixer == null)
                {
                    if (debugLog) Debug.LogWarning($"[VirtualDrumEngine] CustomAudioMixer not available");
                    return;
                }
            }

            AudioClip clip = soundBank.GetSample(partType);
            if (clip == null)
            {
                if (debugLog) Debug.LogWarning($"[VirtualDrumEngine] No sample for {partType}");
                return;
            }

            audioMixer.PlayNoteNow(clip, volume * velocity, 1f, (int)partType);
        }

        #endregion

        #region Visual State

        /// <summary>
        /// Check if a drum part is currently in "hit" state (for visual feedback).
        /// </summary>
        public bool IsHit(DrumPartType partType)
        {
            if (lastHitTime.TryGetValue(partType, out float hitTime))
            {
                return Time.time - hitTime < hitDuration;
            }
            return false;
        }

        private void Update()
        {
            // Auto-reset visual state after hit duration
            foreach (var kvp in lastHitTime)
            {
                if (Time.time - kvp.Value >= hitDuration)
                {
                    OnHitStateChanged?.Invoke(kvp.Key, false);
                }
            }

            // Clean up old entries
            var keysToRemove = new List<DrumPartType>();
            foreach (var kvp in lastHitTime)
            {
                if (Time.time - kvp.Value >= hitDuration * 2)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in keysToRemove)
            {
                lastHitTime.Remove(key);
            }
        }

        #endregion
    }
}
