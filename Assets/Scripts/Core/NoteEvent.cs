using System;

namespace SoloBandStudio.Core
{
    /// <summary>
    /// Musical note names (matches MIDI standard).
    /// </summary>
    public enum Note
    {
        C = 0,
        CSharp = 1,
        D = 2,
        DSharp = 3,
        E = 4,
        F = 5,
        FSharp = 6,
        G = 7,
        GSharp = 8,
        A = 9,
        ASharp = 10,
        B = 11
    }

    /// <summary>
    /// Instrument types available in the studio.
    /// </summary>
    public enum InstrumentType
    {
        Piano,
        Drum,
        Bass,
        Guitar
    }

    /// <summary>
    /// Represents a single note event for recording/playback.
    /// Immutable struct for efficient storage.
    /// </summary>
    [Serializable]
    public struct NoteEvent
    {
        /// <summary>
        /// Time in beats from loop start (0.0 to totalBeats).
        /// </summary>
        public float beatTime;

        /// <summary>
        /// MIDI note number (e.g., 60 = C4).
        /// For drums, this represents the pad index.
        /// </summary>
        public int note;

        /// <summary>
        /// Velocity/intensity (0.0 to 1.0).
        /// </summary>
        public float velocity;

        /// <summary>
        /// Duration in beats. 0 for percussive sounds (drums).
        /// </summary>
        public float duration;

        public NoteEvent(float beatTime, int note, float velocity, float duration = 0f)
        {
            this.beatTime = beatTime;
            this.note = note;
            this.velocity = velocity;
            this.duration = duration;
        }

        /// <summary>
        /// Create from Note enum and octave.
        /// MIDI standard: C-1=0, C0=12, C4=60
        /// </summary>
        public static NoteEvent FromNote(float beatTime, Note note, int octave, float velocity, float duration = 0f)
        {
            int midiNote = ((octave + 1) * 12) + (int)note;
            return new NoteEvent(beatTime, midiNote, velocity, duration);
        }

        /// <summary>
        /// Get the Note enum from the MIDI note number.
        /// </summary>
        public Note GetNote()
        {
            return (Note)(note % 12);
        }

        /// <summary>
        /// Get the octave from the MIDI note number.
        /// MIDI standard: C-1=0, C0=12, C4=60
        /// </summary>
        public int GetOctave()
        {
            return (note / 12) - 1;
        }

        public override string ToString()
        {
            return $"[{beatTime:F2}] {GetNote()}{GetOctave()} vel:{velocity:F2} dur:{duration:F2}";
        }
    }

    /// <summary>
    /// Event args for note on/off events.
    /// </summary>
    public class NoteEventArgs : EventArgs
    {
        public int MidiNote { get; }
        public Note Note { get; }
        public int Octave { get; }
        public float Velocity { get; }

        public NoteEventArgs(int midiNote, float velocity)
        {
            MidiNote = midiNote;
            Note = (Note)(midiNote % 12);
            Octave = midiNote / 12;
            Velocity = velocity;
        }
    }
}
