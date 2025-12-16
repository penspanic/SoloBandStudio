using System;
using System.Collections.Generic;
using UnityEngine;
using SoloBandStudio.Core;

namespace SoloBandStudio.Audio
{
    /// <summary>
    /// Pure data container for a single loop track.
    /// NOT a MonoBehaviour - just holds recorded note events.
    /// </summary>
    [Serializable]
    public class LoopTrackData
    {
        [SerializeField] private string trackName;
        [SerializeField] private string instrumentId;
        [SerializeField] private InstrumentType instrumentType;
        [SerializeField] private List<NoteEvent> events = new List<NoteEvent>();
        [SerializeField] private bool isMuted;
        [SerializeField] private float volume = 1f;

        /// <summary>
        /// Display name of the track.
        /// </summary>
        public string TrackName
        {
            get => trackName;
            set => trackName = value;
        }

        /// <summary>
        /// Unique ID of the instrument this track was recorded with.
        /// </summary>
        public string InstrumentId
        {
            get => instrumentId;
            set => instrumentId = value;
        }

        /// <summary>
        /// Type of instrument this track was recorded with.
        /// </summary>
        public InstrumentType InstrumentType
        {
            get => instrumentType;
            set => instrumentType = value;
        }

        /// <summary>
        /// Whether this track is muted.
        /// </summary>
        public bool IsMuted
        {
            get => isMuted;
            set => isMuted = value;
        }

        /// <summary>
        /// Volume multiplier (0.0 - 1.0).
        /// </summary>
        public float Volume
        {
            get => volume;
            set => volume = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Number of recorded events.
        /// </summary>
        public int EventCount => events.Count;

        /// <summary>
        /// Read-only access to events.
        /// </summary>
        public IReadOnlyList<NoteEvent> Events => events.AsReadOnly();

        /// <summary>
        /// Create a new empty track.
        /// </summary>
        public LoopTrackData(string name, string instId, InstrumentType type)
        {
            trackName = name;
            instrumentId = instId;
            instrumentType = type;
            events = new List<NoteEvent>();
            isMuted = false;
            volume = 1f;
        }

        /// <summary>
        /// Create a new empty track (legacy, for MIDI import).
        /// </summary>
        public LoopTrackData(string name, InstrumentType type)
            : this(name, type.ToString(), type)
        {
        }

        /// <summary>
        /// Add a note event to the track.
        /// </summary>
        public void AddEvent(NoteEvent evt)
        {
            events.Add(evt);
        }

        /// <summary>
        /// Update the duration of the last event matching the given note.
        /// Used when NoteOff is received.
        /// </summary>
        public void SetNoteDuration(int midiNote, float duration)
        {
            for (int i = events.Count - 1; i >= 0; i--)
            {
                if (events[i].note == midiNote && events[i].duration == 0)
                {
                    var evt = events[i];
                    evt.duration = duration;
                    events[i] = evt;
                    return;
                }
            }
        }

        /// <summary>
        /// Sort events by beat time.
        /// </summary>
        public void SortEvents()
        {
            events.Sort((a, b) => a.beatTime.CompareTo(b.beatTime));
        }

        /// <summary>
        /// Clear all events.
        /// </summary>
        public void Clear()
        {
            events.Clear();
        }

        /// <summary>
        /// Remove event at specified index.
        /// </summary>
        public void RemoveEventAt(int index)
        {
            if (index >= 0 && index < events.Count)
            {
                events.RemoveAt(index);
            }
        }

        /// <summary>
        /// Update event at specified index.
        /// </summary>
        public void UpdateEvent(int index, NoteEvent newEvent)
        {
            if (index >= 0 && index < events.Count)
            {
                events[index] = newEvent;
            }
        }

        /// <summary>
        /// Get events within a beat range (fromBeat, toBeat].
        /// Handles loop wraparound.
        /// </summary>
        public List<NoteEvent> GetEventsInRange(float fromBeat, float toBeat, float totalBeats)
        {
            var result = new List<NoteEvent>();

            foreach (var evt in events)
            {
                bool inRange;

                if (fromBeat < toBeat)
                {
                    inRange = evt.beatTime > fromBeat && evt.beatTime <= toBeat;
                }
                else
                {
                    // Wrapped around loop end
                    inRange = evt.beatTime > fromBeat || evt.beatTime <= toBeat;
                }

                if (inRange)
                {
                    result.Add(evt);
                }
            }

            return result;
        }

        /// <summary>
        /// Create a deep copy of this track.
        /// </summary>
        public LoopTrackData Clone()
        {
            var clone = new LoopTrackData(trackName + " (Copy)", instrumentId, instrumentType);
            clone.isMuted = isMuted;
            clone.volume = volume;

            foreach (var evt in events)
            {
                clone.events.Add(evt);
            }

            return clone;
        }
    }
}
