using System;
using UnityEngine;

namespace SoloBandStudio.Audio
{
    /// <summary>
    /// Immutable metadata for a song.
    /// Calculated from MIDI file or set manually.
    /// </summary>
    [Serializable]
    public struct SongMetadata
    {
        [SerializeField] private float bpm;
        [SerializeField] private int beatsPerBar;
        [SerializeField] private int totalBars;
        [SerializeField] private string name;

        public float BPM => bpm;
        public int BeatsPerBar => beatsPerBar;
        public int TotalBars => totalBars;
        public int TotalBeats => beatsPerBar * totalBars;
        public string Name => name ?? "";

        /// <summary>
        /// Duration in seconds at the given BPM.
        /// </summary>
        public double DurationSeconds => (60.0 / bpm) * TotalBeats;

        public SongMetadata(float bpm, int beatsPerBar, int totalBars, string name = "")
        {
            this.bpm = Mathf.Clamp(bpm, 30f, 300f);
            this.beatsPerBar = Mathf.Clamp(beatsPerBar, 2, 8);
            this.totalBars = Mathf.Max(1, totalBars);
            this.name = name ?? "";
        }

        /// <summary>
        /// Create metadata from total beats (rounds up to complete bars).
        /// </summary>
        public static SongMetadata FromTotalBeats(float bpm, int beatsPerBar, int totalBeats, string name = "")
        {
            int totalBars = (totalBeats + beatsPerBar - 1) / beatsPerBar;
            return new SongMetadata(bpm, beatsPerBar, totalBars, name);
        }

        /// <summary>
        /// Create metadata from MIDI tick information.
        /// </summary>
        public static SongMetadata FromMidiTicks(float bpm, int beatsPerBar, int maxTicks, int ticksPerBeat, string name = "")
        {
            int totalBeats = (maxTicks / ticksPerBeat) + 1;
            return FromTotalBeats(bpm, beatsPerBar, totalBeats, name);
        }

        /// <summary>
        /// Default metadata for new empty projects.
        /// </summary>
        public static SongMetadata Default => new SongMetadata(120f, 4, 4, "Untitled");

        public override string ToString()
        {
            return $"{Name} ({TotalBars} bars, {bpm} BPM, {beatsPerBar}/4)";
        }
    }
}
