using System;
using System.Collections.Generic;
using UnityEngine;
using SoloBandStudio.Core;

namespace SoloBandStudio.Audio
{
    /// <summary>
    /// ScriptableObject that stores a complete song preset.
    /// Can be created in Unity Editor and loaded at runtime.
    /// Uses the same LoopTrackData structure as live recording.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSongPreset", menuName = "SoloBandStudio/Song Preset")]
    public class SongPreset : ScriptableObject
    {
        [Header("Song Info")]
        [SerializeField] private string songName = "New Song";
        [SerializeField] private string artist = "Unknown";
        [SerializeField] private string description;

        [Header("Timing")]
        [SerializeField] private float bpm = 120f;
        [SerializeField] private int beatsPerBar = 4;
        [SerializeField] private int totalBars = 4;

        [Header("Tracks")]
        [SerializeField] private List<LoopTrackData> tracks = new List<LoopTrackData>();

        // Properties
        public string SongName => songName;
        public string Artist => artist;
        public string Description => description;
        public float BPM => bpm;
        public int BeatsPerBar => beatsPerBar;
        public int TotalBars => totalBars;
        public int TotalBeats => beatsPerBar * totalBars;
        public IReadOnlyList<LoopTrackData> Tracks => tracks.AsReadOnly();
        public int TrackCount => tracks.Count;

        /// <summary>
        /// Get a deep copy of all tracks (so original preset isn't modified).
        /// </summary>
        public List<LoopTrackData> CloneTracks()
        {
            var clones = new List<LoopTrackData>();
            foreach (var track in tracks)
            {
                clones.Add(track.Clone());
            }
            return clones;
        }

        /// <summary>
        /// Add a track to the preset.
        /// </summary>
        public void AddTrack(LoopTrackData track)
        {
            tracks.Add(track);
        }

        /// <summary>
        /// Clear all tracks.
        /// </summary>
        public void ClearTracks()
        {
            tracks.Clear();
        }

        /// <summary>
        /// Set song metadata.
        /// </summary>
        public void SetMetadata(string name, string artistName, string desc, float songBpm, int beats, int bars)
        {
            songName = name;
            artist = artistName;
            description = desc;
            bpm = songBpm;
            beatsPerBar = beats;
            totalBars = bars;
        }

#if UNITY_EDITOR
        [ContextMenu("Print Track Info")]
        private void PrintTrackInfo()
        {
            Debug.Log($"=== {songName} by {artist} ===");
            Debug.Log($"BPM: {bpm}, Time: {beatsPerBar}/{4}, Bars: {totalBars}");
            Debug.Log($"Tracks: {tracks.Count}");

            for (int i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i];
                Debug.Log($"  [{i}] {track.TrackName} ({track.InstrumentType}): {track.EventCount} events");
            }
        }
#endif
    }
}
