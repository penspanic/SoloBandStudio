using UnityEngine;
using SoloBandStudio.Core;

namespace SoloBandStudio.Instruments.Keyboard
{
    /// <summary>
    /// Manages keyboard sound synthesis using multi-sampling.
    /// Uses the closest recorded sample for natural sound across all octaves.
    /// Used for Piano, Bass, and other keyboard-based instruments.
    /// </summary>
    public class KeyboardSynthesizer : MonoBehaviour, ISynthesizer
    {
        [Header("Sample Bank")]
        [Tooltip("Sample bank with multiple recorded notes for natural sound")]
        [SerializeField] private InstrumentSampleBank sampleBank;

        private void Awake()
        {
            if (sampleBank != null)
            {
                sampleBank.Initialize();
            }
        }

        /// <summary>
        /// Gets the audio clip and pitch for a given MIDI note.
        /// </summary>
        /// <param name="midiNote">The MIDI note to play (0-127)</param>
        /// <param name="clip">Output: The audio clip to use</param>
        /// <param name="pitch">Output: The pitch multiplier to apply</param>
        /// <returns>True if a sample was found</returns>
        public bool GetSampleForMidiNote(int midiNote, out AudioClip clip, out float pitch)
        {
            if (sampleBank != null && sampleBank.GetSampleForNote(midiNote, out clip, out pitch))
            {
                return true;
            }

            clip = null;
            pitch = 1f;
            return false;
        }

        /// <summary>
        /// Validates that samples are properly configured.
        /// </summary>
        public bool IsValid()
        {
            return sampleBank != null && sampleBank.IsValid();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (sampleBank == null)
            {
                Debug.LogWarning("[KeyboardSynthesizer] No sample bank assigned.");
            }
        }

        [ContextMenu("Log Sample Bank Info")]
        private void LogSampleBankInfo()
        {
            if (sampleBank != null && sampleBank.IsValid())
            {
                Debug.Log($"[KeyboardSynthesizer] Using {sampleBank.Samples.Count} samples ({sampleBank.name})");
            }
            else
            {
                Debug.LogWarning("[KeyboardSynthesizer] No samples configured!");
            }
        }
#endif
    }
}
