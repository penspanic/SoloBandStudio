using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoloBandStudio.Core
{
    /// <summary>
    /// A single instrument sample with its MIDI note information.
    /// </summary>
    [Serializable]
    public class InstrumentSample
    {
        [Tooltip("The audio clip for this sample")]
        public AudioClip clip;

        [Tooltip("The MIDI note this sample was recorded at")]
        public int midiNote;

        [Tooltip("Optional: note name for reference (e.g., C4, E1)")]
        public string noteName;
    }

    /// <summary>
    /// A bank of instrument samples for multi-sampling playback.
    /// Works with any pitched instrument (Piano, Bass, Synth, etc.)
    /// Uses MIDI note numbers for universal compatibility.
    /// </summary>
    [CreateAssetMenu(fileName = "SampleBank", menuName = "SoloBandStudio/Instrument Sample Bank")]
    public class InstrumentSampleBank : ScriptableObject
    {
        [Header("Samples")]
        [Tooltip("List of samples, each mapped to a specific MIDI note")]
        [SerializeField] private List<InstrumentSample> samples = new List<InstrumentSample>();

        // Cached sorted samples for quick lookup
        private InstrumentSample[] sortedSamples;
        private bool isInitialized;

        /// <summary>
        /// Gets the list of samples.
        /// </summary>
        public IReadOnlyList<InstrumentSample> Samples => samples;

        /// <summary>
        /// Initialize the sample bank (sorts samples by MIDI note).
        /// </summary>
        public void Initialize()
        {
            if (samples == null || samples.Count == 0)
            {
                Debug.LogWarning($"[InstrumentSampleBank] {name}: No samples configured!");
                return;
            }

            // Sort samples by MIDI note for binary search
            sortedSamples = samples.ToArray();
            Array.Sort(sortedSamples, (a, b) => a.midiNote.CompareTo(b.midiNote));

            isInitialized = true;
            Debug.Log($"[InstrumentSampleBank] {name}: Initialized with {sortedSamples.Length} samples");
        }

        /// <summary>
        /// Gets the best sample and pitch multiplier for a given MIDI note.
        /// </summary>
        public bool GetSampleForNote(int midiNote, out AudioClip clip, out float pitch)
        {
            clip = null;
            pitch = 1f;

            if (!isInitialized || sortedSamples == null || sortedSamples.Length == 0)
            {
                Initialize();
                if (!isInitialized) return false;
            }

            InstrumentSample closestSample = FindClosestSample(midiNote);

            if (closestSample == null || closestSample.clip == null)
            {
                return false;
            }

            clip = closestSample.clip;

            // Calculate pitch shift: pitch = 2^(semitones/12)
            int semitoneDifference = midiNote - closestSample.midiNote;
            pitch = Mathf.Pow(2f, semitoneDifference / 12f);

            return true;
        }

        private InstrumentSample FindClosestSample(int midiNote)
        {
            if (sortedSamples.Length == 0) return null;
            if (sortedSamples.Length == 1) return sortedSamples[0];

            int left = 0;
            int right = sortedSamples.Length - 1;

            if (midiNote <= sortedSamples[left].midiNote) return sortedSamples[left];
            if (midiNote >= sortedSamples[right].midiNote) return sortedSamples[right];

            while (left < right - 1)
            {
                int mid = (left + right) / 2;
                int midNote = sortedSamples[mid].midiNote;

                if (midNote == midiNote)
                    return sortedSamples[mid];
                else if (midNote < midiNote)
                    left = mid;
                else
                    right = mid;
            }

            int leftDiff = Mathf.Abs(midiNote - sortedSamples[left].midiNote);
            int rightDiff = Mathf.Abs(midiNote - sortedSamples[right].midiNote);

            return leftDiff <= rightDiff ? sortedSamples[left] : sortedSamples[right];
        }

        /// <summary>
        /// Validates the sample bank configuration.
        /// </summary>
        public bool IsValid()
        {
            if (samples == null || samples.Count == 0) return false;

            foreach (var sample in samples)
            {
                if (sample.clip == null) return false;
            }

            return true;
        }

        /// <summary>
        /// Helper to convert Note enum + octave to MIDI note.
        /// MIDI standard: C-1=0, C0=12, C4=60, A0=21
        /// </summary>
        public static int ToMidiNote(Note note, int octave)
        {
            return ((octave + 1) * 12) + (int)note;
        }

        /// <summary>
        /// Helper to get note name from MIDI note.
        /// </summary>
        public static string GetNoteName(int midiNote)
        {
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int octave = (midiNote / 12) - 1;
            int noteIndex = midiNote % 12;
            return $"{noteNames[noteIndex]}{octave}";
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            isInitialized = false;

            // Auto-fill note names
            foreach (var sample in samples)
            {
                if (string.IsNullOrEmpty(sample.noteName))
                {
                    sample.noteName = GetNoteName(sample.midiNote);
                }
            }
        }

        [ContextMenu("Sort Samples by Note")]
        private void SortSamples()
        {
            samples.Sort((a, b) => a.midiNote.CompareTo(b.midiNote));
        }

        [ContextMenu("Auto-fill Note Names")]
        private void AutoFillNoteNames()
        {
            foreach (var sample in samples)
            {
                sample.noteName = GetNoteName(sample.midiNote);
            }
        }

        [ContextMenu("Log Sample Info")]
        private void LogSampleInfo()
        {
            Debug.Log($"=== Sample Bank: {name} ===");
            foreach (var sample in samples)
            {
                string clipName = sample.clip != null ? sample.clip.name : "(none)";
                Debug.Log($"  MIDI {sample.midiNote} ({sample.noteName}): {clipName}");
            }
        }
#endif
    }
}
