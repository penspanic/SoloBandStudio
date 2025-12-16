using UnityEngine;
using UnityEditor;
using SoloBandStudio.Audio;
using SoloBandStudio.Core;

namespace SoloBandStudio.Editor
{
    /// <summary>
    /// Editor utility for creating sample song preset assets.
    /// Menu: SoloBandStudio > Create Sample Presets
    /// </summary>
    public static class SamplePresetsEditor
    {
        private const string PresetFolder = "Assets/Resources/Presets";

        [MenuItem("SoloBandStudio/Create Sample Presets")]
        public static void CreateAllSamplePresets()
        {
            EnsureFolderExists();

            CreateCanonInD();
            CreateCMajorScale();
            CreatePopProgression();
            CreateBluesProgression();
            CreateTwinkleTwinkle();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SamplePresets] Created 5 sample presets in {PresetFolder}");
        }

        [MenuItem("SoloBandStudio/Create Presets/Canon in D")]
        public static void CreateCanonInD()
        {
            EnsureFolderExists();

            var preset = ScriptableObject.CreateInstance<SongPreset>();
            preset.SetMetadata(
                "Canon in D (Chords)",
                "Pachelbel",
                "Classic chord progression: D-A-Bm-F#m-G-D-G-A",
                70f, 4, 4
            );

            var chordTrack = new LoopTrackData("Chord Progression", InstrumentType.Piano);

            // D-A-Bm-F#m-G-D-G-A (each 2 beats)
            AddChord(chordTrack, 0f, 2f, 50, 54, 57);   // D Major
            AddChord(chordTrack, 2f, 2f, 49, 52, 57);   // A Major
            AddChord(chordTrack, 4f, 2f, 47, 50, 54);   // B minor
            AddChord(chordTrack, 6f, 2f, 54, 57, 61);   // F# minor
            AddChord(chordTrack, 8f, 2f, 55, 59, 62);   // G Major
            AddChord(chordTrack, 10f, 2f, 50, 54, 57);  // D Major
            AddChord(chordTrack, 12f, 2f, 55, 59, 62);  // G Major
            AddChord(chordTrack, 14f, 2f, 49, 52, 57);  // A Major

            chordTrack.SortEvents();
            preset.AddTrack(chordTrack);

            SavePreset(preset, "CanonInD.asset");
        }

        [MenuItem("SoloBandStudio/Create Presets/C Major Scale")]
        public static void CreateCMajorScale()
        {
            EnsureFolderExists();

            var preset = ScriptableObject.CreateInstance<SongPreset>();
            preset.SetMetadata(
                "C Major Scale",
                "Demo",
                "Ascending and descending C major scale",
                100f, 4, 4
            );

            var melodyTrack = new LoopTrackData("Melody", InstrumentType.Piano);

            // Ascending
            int[] ascending = { 60, 62, 64, 65, 67, 69, 71, 72 };
            for (int i = 0; i < ascending.Length; i++)
            {
                AddNote(melodyTrack, i * 0.5f, ascending[i], 0.4f);
            }

            // Descending
            int[] descending = { 72, 71, 69, 67, 65, 64, 62, 60 };
            for (int i = 0; i < descending.Length; i++)
            {
                AddNote(melodyTrack, 8f + i * 0.5f, descending[i], 0.4f);
            }

            melodyTrack.SortEvents();
            preset.AddTrack(melodyTrack);

            SavePreset(preset, "CMajorScale.asset");
        }

        [MenuItem("SoloBandStudio/Create Presets/Pop Progression")]
        public static void CreatePopProgression()
        {
            EnsureFolderExists();

            var preset = ScriptableObject.CreateInstance<SongPreset>();
            preset.SetMetadata(
                "Pop Progression",
                "Demo",
                "Classic I-V-vi-IV progression in C major (C-G-Am-F)",
                120f, 4, 4
            );

            var chordTrack = new LoopTrackData("Chords", InstrumentType.Piano);

            AddChord(chordTrack, 0f, 4f, 48, 52, 55);   // C Major
            AddChord(chordTrack, 4f, 4f, 55, 59, 62);   // G Major
            AddChord(chordTrack, 8f, 4f, 57, 60, 64);   // A minor
            AddChord(chordTrack, 12f, 4f, 53, 57, 60);  // F Major

            chordTrack.SortEvents();
            preset.AddTrack(chordTrack);

            SavePreset(preset, "PopProgression.asset");
        }

        [MenuItem("SoloBandStudio/Create Presets/12-Bar Blues")]
        public static void CreateBluesProgression()
        {
            EnsureFolderExists();

            var preset = ScriptableObject.CreateInstance<SongPreset>();
            preset.SetMetadata(
                "12-Bar Blues",
                "Traditional",
                "Classic 12-bar blues progression in C",
                100f, 4, 12
            );

            var chordTrack = new LoopTrackData("Blues Chords", InstrumentType.Piano);

            // C7, F7, G7 dominant 7th chords
            int[] C7 = { 48, 52, 55, 58 };
            int[] F7 = { 53, 57, 60, 63 };
            int[] G7 = { 55, 59, 62, 65 };

            // 12-bar structure
            int[][] progression = { C7, C7, C7, C7, F7, F7, C7, C7, G7, F7, C7, G7 };

            for (int bar = 0; bar < 12; bar++)
            {
                AddChord(chordTrack, bar * 4f, 4f, progression[bar]);
            }

            chordTrack.SortEvents();
            preset.AddTrack(chordTrack);

            SavePreset(preset, "BluesProgression.asset");
        }

        [MenuItem("SoloBandStudio/Create Presets/Twinkle Twinkle")]
        public static void CreateTwinkleTwinkle()
        {
            EnsureFolderExists();

            var preset = ScriptableObject.CreateInstance<SongPreset>();
            preset.SetMetadata(
                "Twinkle Twinkle",
                "Traditional",
                "Twinkle Twinkle Little Star melody",
                100f, 4, 4
            );

            var melodyTrack = new LoopTrackData("Melody", InstrumentType.Piano);

            // C C G G A A G - F F E E D D C
            int[] notes = { 60, 60, 67, 67, 69, 69, 67, 65, 65, 64, 64, 62, 62, 60 };
            float[] durations = { 1, 1, 1, 1, 1, 1, 2, 1, 1, 1, 1, 1, 1, 2 };

            float currentBeat = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                AddNote(melodyTrack, currentBeat, notes[i], durations[i]);
                currentBeat += durations[i];
            }

            melodyTrack.SortEvents();
            preset.AddTrack(melodyTrack);

            SavePreset(preset, "TwinkleTwinkle.asset");
        }

        #region Helpers

        private static void EnsureFolderExists()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            if (!AssetDatabase.IsValidFolder(PresetFolder))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Presets");
            }
        }

        private static void SavePreset(SongPreset preset, string filename)
        {
            string path = $"{PresetFolder}/{filename}";

            var existing = AssetDatabase.LoadAssetAtPath<SongPreset>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(preset, existing);
                EditorUtility.SetDirty(existing);
                Debug.Log($"[SamplePresets] Updated: {filename}");
            }
            else
            {
                AssetDatabase.CreateAsset(preset, path);
                Debug.Log($"[SamplePresets] Created: {filename}");
            }
        }

        private static void AddChord(LoopTrackData track, float beatTime, float duration, params int[] notes)
        {
            foreach (int note in notes)
            {
                track.AddEvent(new NoteEvent(beatTime, note, 0.7f, duration));
            }
        }

        private static void AddNote(LoopTrackData track, float beatTime, int note, float duration, float velocity = 0.8f)
        {
            track.AddEvent(new NoteEvent(beatTime, note, velocity, duration));
        }

        #endregion
    }
}
