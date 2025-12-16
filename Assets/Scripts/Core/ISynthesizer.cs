using UnityEngine;

namespace SoloBandStudio.Core
{
    /// <summary>
    /// Interface for synthesizers that can provide audio samples for MIDI notes.
    /// Allows different instruments (Piano, Bass, etc.) to share the same keyboard engine.
    /// </summary>
    public interface ISynthesizer
    {
        /// <summary>
        /// Gets the audio clip and pitch for a given MIDI note.
        /// </summary>
        /// <param name="midiNote">The MIDI note to play (0-127)</param>
        /// <param name="clip">Output: The audio clip to use</param>
        /// <param name="pitch">Output: The pitch multiplier to apply</param>
        /// <returns>True if a sample was found</returns>
        bool GetSampleForMidiNote(int midiNote, out AudioClip clip, out float pitch);

        /// <summary>
        /// Validates that samples are properly configured.
        /// </summary>
        bool IsValid();
    }
}
