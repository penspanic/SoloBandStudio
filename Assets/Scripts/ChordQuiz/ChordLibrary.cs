using System.Collections.Generic;
using UnityEngine;
using SoloBandStudio.Core;

namespace SoloBandStudio.ChordQuiz
{
    /// <summary>
    /// Library of chord definitions organized by difficulty level.
    /// Provides methods to retrieve random chords for quiz questions.
    /// </summary>
    public class ChordLibrary
    {
        private List<ChordData> easyChords = new List<ChordData>();
        private List<ChordData> mediumChords = new List<ChordData>();
        private List<ChordData> hardChords = new List<ChordData>();
        private int baseOctave;

        public ChordLibrary(int startOctave = 4)
        {
            baseOctave = startOctave;

            PopulateEasyChords();
            PopulateMediumChords();
            PopulateHardChords();

            Debug.Log($"[ChordLibrary] Initialized with {easyChords.Count} easy, {mediumChords.Count} medium, {hardChords.Count} hard chords (base octave: {baseOctave}).");
        }

        private void PopulateEasyChords()
        {
            // Major chords
            easyChords.Add(new ChordData(Note.C, ChordType.Major, baseOctave));
            easyChords.Add(new ChordData(Note.D, ChordType.Major, baseOctave));
            easyChords.Add(new ChordData(Note.E, ChordType.Major, baseOctave));
            easyChords.Add(new ChordData(Note.F, ChordType.Major, baseOctave));
            easyChords.Add(new ChordData(Note.G, ChordType.Major, baseOctave));
            easyChords.Add(new ChordData(Note.A, ChordType.Major, baseOctave));
            easyChords.Add(new ChordData(Note.B, ChordType.Major, baseOctave));

            // Minor chords
            easyChords.Add(new ChordData(Note.C, ChordType.Minor, baseOctave));
            easyChords.Add(new ChordData(Note.D, ChordType.Minor, baseOctave));
            easyChords.Add(new ChordData(Note.E, ChordType.Minor, baseOctave));
            easyChords.Add(new ChordData(Note.F, ChordType.Minor, baseOctave));
            easyChords.Add(new ChordData(Note.G, ChordType.Minor, baseOctave));
            easyChords.Add(new ChordData(Note.A, ChordType.Minor, baseOctave));
            easyChords.Add(new ChordData(Note.B, ChordType.Minor, baseOctave));
        }

        private void PopulateMediumChords()
        {
            // Dominant 7th chords
            mediumChords.Add(new ChordData(Note.C, ChordType.Dominant7, baseOctave));
            mediumChords.Add(new ChordData(Note.D, ChordType.Dominant7, baseOctave));
            mediumChords.Add(new ChordData(Note.E, ChordType.Dominant7, baseOctave));
            mediumChords.Add(new ChordData(Note.F, ChordType.Dominant7, baseOctave));
            mediumChords.Add(new ChordData(Note.G, ChordType.Dominant7, baseOctave));
            mediumChords.Add(new ChordData(Note.A, ChordType.Dominant7, baseOctave));
            mediumChords.Add(new ChordData(Note.B, ChordType.Dominant7, baseOctave));

            // Major 7th chords
            mediumChords.Add(new ChordData(Note.C, ChordType.Major7, baseOctave));
            mediumChords.Add(new ChordData(Note.F, ChordType.Major7, baseOctave));
            mediumChords.Add(new ChordData(Note.G, ChordType.Major7, baseOctave));

            // Minor 7th chords
            mediumChords.Add(new ChordData(Note.D, ChordType.Minor7, baseOctave));
            mediumChords.Add(new ChordData(Note.E, ChordType.Minor7, baseOctave));
            mediumChords.Add(new ChordData(Note.A, ChordType.Minor7, baseOctave));

            // Sharp/flat major chords
            mediumChords.Add(new ChordData(Note.CSharp, ChordType.Major, baseOctave));
            mediumChords.Add(new ChordData(Note.FSharp, ChordType.Major, baseOctave));
            mediumChords.Add(new ChordData(Note.GSharp, ChordType.Major, baseOctave));
        }

        private void PopulateHardChords()
        {
            // Diminished chords
            hardChords.Add(new ChordData(Note.C, ChordType.Diminished, baseOctave));
            hardChords.Add(new ChordData(Note.D, ChordType.Diminished, baseOctave));
            hardChords.Add(new ChordData(Note.E, ChordType.Diminished, baseOctave));
            hardChords.Add(new ChordData(Note.F, ChordType.Diminished, baseOctave));
            hardChords.Add(new ChordData(Note.G, ChordType.Diminished, baseOctave));

            // Augmented chords
            hardChords.Add(new ChordData(Note.C, ChordType.Augmented, baseOctave));
            hardChords.Add(new ChordData(Note.E, ChordType.Augmented, baseOctave));
            hardChords.Add(new ChordData(Note.G, ChordType.Augmented, baseOctave));

            // Add9 chords
            hardChords.Add(new ChordData(Note.C, ChordType.MajorAdd9, baseOctave));
            hardChords.Add(new ChordData(Note.D, ChordType.MinorAdd9, baseOctave));
            hardChords.Add(new ChordData(Note.E, ChordType.MinorAdd9, baseOctave));
            hardChords.Add(new ChordData(Note.F, ChordType.MajorAdd9, baseOctave));
            hardChords.Add(new ChordData(Note.G, ChordType.MajorAdd9, baseOctave));

            // Complex 7th chords with sharps
            hardChords.Add(new ChordData(Note.CSharp, ChordType.Minor7, baseOctave));
            hardChords.Add(new ChordData(Note.FSharp, ChordType.Minor7, baseOctave));
            hardChords.Add(new ChordData(Note.DSharp, ChordType.Major7, baseOctave));
        }

        public ChordData GetRandomEasyChord()
        {
            if (easyChords.Count == 0) return null;
            return easyChords[Random.Range(0, easyChords.Count)];
        }

        public ChordData GetRandomMediumChord()
        {
            if (mediumChords.Count == 0) return null;
            return mediumChords[Random.Range(0, mediumChords.Count)];
        }

        public ChordData GetRandomHardChord()
        {
            if (hardChords.Count == 0) return null;
            return hardChords[Random.Range(0, hardChords.Count)];
        }

        public ChordData GetRandomChordByDifficulty(int difficulty)
        {
            return difficulty switch
            {
                0 => GetRandomEasyChord(),
                1 => GetRandomMediumChord(),
                2 => GetRandomHardChord(),
                _ => GetRandomEasyChord()
            };
        }

        public ChordData GetRandomChord()
        {
            int difficulty = Random.Range(0, 3);
            return GetRandomChordByDifficulty(difficulty);
        }

        public List<ChordData> GetChordsByDifficulty(int difficulty)
        {
            return difficulty switch
            {
                0 => new List<ChordData>(easyChords),
                1 => new List<ChordData>(mediumChords),
                2 => new List<ChordData>(hardChords),
                _ => new List<ChordData>(easyChords)
            };
        }

        public int GetChordCount(int difficulty)
        {
            return difficulty switch
            {
                0 => easyChords.Count,
                1 => mediumChords.Count,
                2 => hardChords.Count,
                _ => easyChords.Count
            };
        }
    }
}
