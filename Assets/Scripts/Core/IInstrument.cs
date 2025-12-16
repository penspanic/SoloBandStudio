using System;

namespace SoloBandStudio.Core
{
    /// <summary>
    /// Interface that all instruments must implement.
    /// Provides unified note events for recording and DSP-scheduled playback.
    /// </summary>
    public interface IInstrument
    {
        /// <summary>
        /// Unique identifier for this instrument instance.
        /// Used for recording/playback to distinguish multiple instruments of the same type.
        /// </summary>
        string InstrumentId { get; }

        /// <summary>
        /// Display name of the instrument.
        /// </summary>
        string InstrumentName { get; }

        /// <summary>
        /// Type of this instrument.
        /// </summary>
        InstrumentType Type { get; }

        /// <summary>
        /// Fired when a note is played by user input (for recording).
        /// Parameters: (midiNote, velocity)
        /// </summary>
        event Action<int, float> OnNoteOn;

        /// <summary>
        /// Fired when a note is released by user input (for recording).
        /// Parameters: (midiNote)
        /// </summary>
        event Action<int> OnNoteOff;

        /// <summary>
        /// Schedule a note to play at a specific DSP time.
        /// Returns a handle that can be used to stop the note.
        /// </summary>
        /// <param name="midiNote">MIDI note number</param>
        /// <param name="velocity">Velocity 0.0-1.0</param>
        /// <param name="dspTime">DSP time to start playback</param>
        /// <returns>Handle to the scheduled note, or null if scheduling failed</returns>
        ScheduledNoteHandle ScheduleNote(int midiNote, float velocity, double dspTime);

        /// <summary>
        /// Schedule a note to stop at a specific DSP time.
        /// </summary>
        /// <param name="handle">Handle returned from ScheduleNote</param>
        /// <param name="dspTime">DSP time to stop playback</param>
        /// <param name="fadeOutDuration">Optional fade out duration in seconds</param>
        void ScheduleNoteOff(ScheduledNoteHandle handle, double dspTime, float fadeOutDuration = 0.05f);

        /// <summary>
        /// Stop all currently playing and scheduled notes immediately.
        /// </summary>
        void StopAllNotes();

        /// <summary>
        /// Cancel all scheduled notes that haven't started yet.
        /// </summary>
        void CancelAllScheduled();
    }

    /// <summary>
    /// Handle for a scheduled note, allowing it to be stopped later.
    /// Uses integer handle ID for CustomAudioMixer system.
    /// </summary>
    public class ScheduledNoteHandle
    {
        public int MidiNote { get; }
        public double StartDspTime { get; }
        public int HandleId { get; }

        public bool IsValid => HandleId > 0;

        public ScheduledNoteHandle(int midiNote, double startDspTime, int handleId)
        {
            MidiNote = midiNote;
            StartDspTime = startDspTime;
            HandleId = handleId;
        }
    }
}
