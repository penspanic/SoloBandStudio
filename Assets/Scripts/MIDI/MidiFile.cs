using System;
using System.Collections.Generic;

namespace SoloBandStudio.MIDI
{
    /// <summary>
    /// Represents a complete MIDI file structure.
    /// </summary>
    public class MidiFile
    {
        /// <summary>
        /// MIDI format (0 = single track, 1 = multi-track synchronous).
        /// </summary>
        public ushort Format { get; set; } = 1;

        /// <summary>
        /// Ticks per quarter note (beat). Standard is 480.
        /// </summary>
        public ushort TicksPerBeat { get; set; } = 480;

        /// <summary>
        /// List of tracks in this MIDI file.
        /// </summary>
        public List<MidiTrack> Tracks { get; set; } = new List<MidiTrack>();

        // Extracted metadata
        public string Name { get; set; } = "Untitled";
        public float BPM { get; set; } = 120f;
        public int BeatsPerBar { get; set; } = 4;
        public int BeatUnit { get; set; } = 4; // denominator (4 = quarter note)
    }

    /// <summary>
    /// Represents a single MIDI track.
    /// </summary>
    public class MidiTrack
    {
        public string Name { get; set; } = "";
        public int Channel { get; set; } = 0;
        public List<MidiEvent> Events { get; set; } = new List<MidiEvent>();
    }

    /// <summary>
    /// Base class for all MIDI events.
    /// </summary>
    public abstract class MidiEvent
    {
        /// <summary>
        /// Delta time in ticks from the previous event.
        /// </summary>
        public int DeltaTime { get; set; }

        /// <summary>
        /// Absolute time in ticks from track start.
        /// </summary>
        public int AbsoluteTime { get; set; }
    }

    /// <summary>
    /// Note On event.
    /// </summary>
    public class NoteOnEvent : MidiEvent
    {
        public int Channel { get; set; }
        public int Note { get; set; }
        public int Velocity { get; set; }
    }

    /// <summary>
    /// Note Off event.
    /// </summary>
    public class NoteOffEvent : MidiEvent
    {
        public int Channel { get; set; }
        public int Note { get; set; }
        public int Velocity { get; set; }
    }

    /// <summary>
    /// Tempo change event (meta event).
    /// </summary>
    public class TempoEvent : MidiEvent
    {
        /// <summary>
        /// Microseconds per quarter note.
        /// </summary>
        public int MicrosecondsPerBeat { get; set; }

        /// <summary>
        /// Calculated BPM.
        /// </summary>
        public float BPM => 60_000_000f / MicrosecondsPerBeat;
    }

    /// <summary>
    /// Time signature event (meta event).
    /// </summary>
    public class TimeSignatureEvent : MidiEvent
    {
        public int Numerator { get; set; } = 4;
        public int Denominator { get; set; } = 4;
        public int ClocksPerClick { get; set; } = 24;
        public int ThirtySecondNotesPerBeat { get; set; } = 8;
    }

    /// <summary>
    /// Track name event (meta event).
    /// </summary>
    public class TrackNameEvent : MidiEvent
    {
        public string Name { get; set; } = "";
    }

    /// <summary>
    /// End of track event (meta event).
    /// </summary>
    public class EndOfTrackEvent : MidiEvent
    {
    }

    /// <summary>
    /// Program change event (instrument selection).
    /// </summary>
    public class ProgramChangeEvent : MidiEvent
    {
        public int Channel { get; set; }
        public int Program { get; set; }
    }

    /// <summary>
    /// Control change event (CC messages).
    /// </summary>
    public class ControlChangeEvent : MidiEvent
    {
        public int Channel { get; set; }
        public int Controller { get; set; }
        public int Value { get; set; }
    }

    /// <summary>
    /// Unknown/unsupported event (preserved for round-trip).
    /// </summary>
    public class UnknownEvent : MidiEvent
    {
        public byte[] Data { get; set; }
    }

    /// <summary>
    /// MIDI event type constants.
    /// </summary>
    public static class MidiEventType
    {
        // Channel messages (high nibble)
        public const byte NoteOff = 0x80;
        public const byte NoteOn = 0x90;
        public const byte PolyPressure = 0xA0;
        public const byte ControlChange = 0xB0;
        public const byte ProgramChange = 0xC0;
        public const byte ChannelPressure = 0xD0;
        public const byte PitchBend = 0xE0;

        // System messages
        public const byte SysEx = 0xF0;
        public const byte Meta = 0xFF;

        // Meta event types
        public const byte MetaSequenceNumber = 0x00;
        public const byte MetaText = 0x01;
        public const byte MetaCopyright = 0x02;
        public const byte MetaTrackName = 0x03;
        public const byte MetaInstrumentName = 0x04;
        public const byte MetaLyric = 0x05;
        public const byte MetaMarker = 0x06;
        public const byte MetaCuePoint = 0x07;
        public const byte MetaChannelPrefix = 0x20;
        public const byte MetaEndOfTrack = 0x2F;
        public const byte MetaTempo = 0x51;
        public const byte MetaSMPTEOffset = 0x54;
        public const byte MetaTimeSignature = 0x58;
        public const byte MetaKeySignature = 0x59;
        public const byte MetaSequencerSpecific = 0x7F;
    }
}
