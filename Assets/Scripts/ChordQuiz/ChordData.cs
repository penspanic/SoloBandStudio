using System;
using System.Collections.Generic;
using UnityEngine;
using SoloBandStudio.Core;

namespace SoloBandStudio.ChordQuiz
{
    /// <summary>
    /// Represents the type of chord.
    /// </summary>
    public enum ChordType
    {
        Major,
        Minor,
        Diminished,
        Augmented,
        Major7,
        Minor7,
        Dominant7,
        MajorAdd9,
        MinorAdd9
    }

    /// <summary>
    /// Data structure representing a musical chord.
    /// Contains the root note, chord type, and intervals.
    /// </summary>
    [Serializable]
    public class ChordData
    {
        [Header("Chord Definition")]
        [SerializeField] private Note rootNote;
        [SerializeField] private ChordType chordType;
        [SerializeField] private int octave = 4;

        public Note RootNote => rootNote;
        public ChordType ChordType => chordType;
        public int Octave => octave;
        public string DisplayName => GetChordName();

        public ChordData(Note root, ChordType type, int oct = 4)
        {
            rootNote = root;
            chordType = type;
            octave = oct;
        }

        /// <summary>
        /// Gets the intervals for this chord type.
        /// Intervals are in semitones from the root note.
        /// </summary>
        public List<int> GetIntervals()
        {
            return chordType switch
            {
                ChordType.Major => new List<int> { 0, 4, 7 },
                ChordType.Minor => new List<int> { 0, 3, 7 },
                ChordType.Diminished => new List<int> { 0, 3, 6 },
                ChordType.Augmented => new List<int> { 0, 4, 8 },
                ChordType.Major7 => new List<int> { 0, 4, 7, 11 },
                ChordType.Minor7 => new List<int> { 0, 3, 7, 10 },
                ChordType.Dominant7 => new List<int> { 0, 4, 7, 10 },
                ChordType.MajorAdd9 => new List<int> { 0, 2, 4, 7 },
                ChordType.MinorAdd9 => new List<int> { 0, 2, 3, 7 },
                _ => new List<int> { 0, 4, 7 }
            };
        }

        /// <summary>
        /// Gets all the notes in this chord.
        /// </summary>
        public List<Note> GetNotes()
        {
            List<Note> notes = new List<Note>();
            List<int> intervals = GetIntervals();

            foreach (int interval in intervals)
            {
                int noteValue = ((int)rootNote + interval) % 12;
                notes.Add((Note)noteValue);
            }

            return notes;
        }

        /// <summary>
        /// Gets the absolute MIDI note indices for this chord (considering octave).
        /// </summary>
        public List<int> GetMidiNotes()
        {
            List<int> midiNotes = new List<int>();
            List<int> intervals = GetIntervals();

            int baseMidiNote = (octave + 1) * 12 + (int)rootNote;

            foreach (int interval in intervals)
            {
                midiNotes.Add(baseMidiNote + interval);
            }

            midiNotes.Sort();
            return midiNotes;
        }

        private string GetChordName()
        {
            string noteName = GetNoteName(rootNote);
            string typeSymbol = GetChordTypeSymbol(chordType);
            return $"{noteName}{typeSymbol}";
        }

        private string GetNoteName(Note note)
        {
            return note switch
            {
                Note.C => "C",
                Note.CSharp => "C#",
                Note.D => "D",
                Note.DSharp => "D#",
                Note.E => "E",
                Note.F => "F",
                Note.FSharp => "F#",
                Note.G => "G",
                Note.GSharp => "G#",
                Note.A => "A",
                Note.ASharp => "A#",
                Note.B => "B",
                _ => "?"
            };
        }

        private string GetChordTypeSymbol(ChordType type)
        {
            return type switch
            {
                ChordType.Major => "",
                ChordType.Minor => "m",
                ChordType.Diminished => "dim",
                ChordType.Augmented => "aug",
                ChordType.Major7 => "maj7",
                ChordType.Minor7 => "m7",
                ChordType.Dominant7 => "7",
                ChordType.MajorAdd9 => "add9",
                ChordType.MinorAdd9 => "m(add9)",
                _ => ""
            };
        }

        /// <summary>
        /// Checks if a set of notes matches this chord.
        /// Compares notes by pitch class (ignores octave).
        /// </summary>
        public bool MatchesNotes(List<Note> playedNotes)
        {
            List<Note> chordNotes = GetNotes();

            if (playedNotes.Count != chordNotes.Count)
                return false;

            List<Note> sortedPlayed = new List<Note>(playedNotes);
            List<Note> sortedChord = new List<Note>(chordNotes);
            sortedPlayed.Sort();
            sortedChord.Sort();

            for (int i = 0; i < sortedPlayed.Count; i++)
            {
                if (sortedPlayed[i] != sortedChord[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a set of MIDI notes matches this chord.
        /// Compares by pitch class (ignores octave).
        /// </summary>
        public bool MatchesMidiNotes(HashSet<int> midiNotes)
        {
            List<Note> playedNotes = new List<Note>();
            foreach (int midi in midiNotes)
            {
                playedNotes.Add((Note)(midi % 12));
            }
            return MatchesNotes(playedNotes);
        }
    }
}
