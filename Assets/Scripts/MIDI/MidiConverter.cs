using System.Collections.Generic;
using UnityEngine;
using SoloBandStudio.Audio;
using SoloBandStudio.Core;

namespace SoloBandStudio.MIDI
{
    /// <summary>
    /// Converts between MidiFile and LoopTrackData formats.
    /// </summary>
    public static class MidiConverter
    {
        // MIDI channel to instrument type mapping
        private static readonly Dictionary<int, InstrumentType> ChannelToInstrument = new Dictionary<int, InstrumentType>
        {
            { 0, InstrumentType.Piano },
            { 1, InstrumentType.Piano },
            { 2, InstrumentType.Bass },
            { 3, InstrumentType.Guitar },
            { 9, InstrumentType.Drum },  // Channel 10 (0-indexed = 9) is drums in GM
            { 10, InstrumentType.Drum }
        };

        // Instrument type to MIDI channel mapping
        private static readonly Dictionary<InstrumentType, int> InstrumentToChannel = new Dictionary<InstrumentType, int>
        {
            { InstrumentType.Piano, 0 },
            { InstrumentType.Bass, 2 },
            { InstrumentType.Guitar, 3 },
            { InstrumentType.Drum, 9 }
        };

        // MIDI Program numbers for each instrument
        private static readonly Dictionary<InstrumentType, int> InstrumentToProgram = new Dictionary<InstrumentType, int>
        {
            { InstrumentType.Piano, 0 },    // Acoustic Grand Piano
            { InstrumentType.Bass, 33 },    // Electric Bass (finger)
            { InstrumentType.Guitar, 25 },  // Acoustic Guitar (steel)
            { InstrumentType.Drum, 0 }      // Drums don't use program change
        };

        #region MIDI to LoopTrackData

        /// <summary>
        /// Convert MidiFile to list of LoopTrackData.
        /// </summary>
        public static List<LoopTrackData> ToLoopTracks(MidiFile midi)
        {
            var result = new List<LoopTrackData>();
            int ticksPerBeat = midi.TicksPerBeat;

            foreach (var midiTrack in midi.Tracks)
            {
                // Skip empty tracks or tracks with only meta events
                if (!HasNoteEvents(midiTrack))
                {
                    continue;
                }

                var instrumentType = GetInstrumentType(midiTrack);
                string trackName = string.IsNullOrEmpty(midiTrack.Name)
                    ? $"Track {result.Count + 1}"
                    : midiTrack.Name;

                var loopTrack = new LoopTrackData(trackName, instrumentType);

                // Convert note events
                var pendingNotes = new Dictionary<int, (int absoluteTime, float velocity)>();

                foreach (var evt in midiTrack.Events)
                {
                    if (evt is NoteOnEvent noteOn)
                    {
                        float beatTime = evt.AbsoluteTime / (float)ticksPerBeat;
                        float velocity = noteOn.Velocity / 127f;

                        // Store pending note
                        pendingNotes[noteOn.Note] = (evt.AbsoluteTime, velocity);
                    }
                    else if (evt is NoteOffEvent noteOff)
                    {
                        if (pendingNotes.TryGetValue(noteOff.Note, out var pending))
                        {
                            float startBeat = pending.absoluteTime / (float)ticksPerBeat;
                            float endBeat = evt.AbsoluteTime / (float)ticksPerBeat;
                            float duration = endBeat - startBeat;

                            var noteEvent = new NoteEvent
                            {
                                beatTime = startBeat,
                                note = noteOff.Note,
                                velocity = pending.velocity,
                                duration = duration
                            };

                            loopTrack.AddEvent(noteEvent);
                            pendingNotes.Remove(noteOff.Note);
                        }
                    }
                }

                // Handle any remaining pending notes (no note-off)
                foreach (var kvp in pendingNotes)
                {
                    float startBeat = kvp.Value.absoluteTime / (float)ticksPerBeat;
                    var noteEvent = new NoteEvent
                    {
                        beatTime = startBeat,
                        note = kvp.Key,
                        velocity = kvp.Value.velocity,
                        duration = 1f // Default duration
                    };
                    loopTrack.AddEvent(noteEvent);
                }

                loopTrack.SortEvents();
                result.Add(loopTrack);
            }

            return result;
        }

        /// <summary>
        /// Get song metadata from MidiFile.
        /// Returns structured SongMetadata with all timing information.
        /// </summary>
        public static SongMetadata GetSongMetadata(MidiFile midi)
        {
            // Find the last event time (only from note events, not meta events)
            int maxTicks = 0;
            foreach (var track in midi.Tracks)
            {
                foreach (var evt in track.Events)
                {
                    // Only consider note events for song length calculation
                    if (evt is NoteOnEvent || evt is NoteOffEvent)
                    {
                        if (evt.AbsoluteTime > maxTicks)
                        {
                            maxTicks = evt.AbsoluteTime;
                        }
                    }
                }
            }

            return SongMetadata.FromMidiTicks(
                midi.BPM,
                midi.BeatsPerBar,
                maxTicks,
                midi.TicksPerBeat,
                midi.Name
            );
        }

        /// <summary>
        /// Get song metadata from MidiFile (legacy tuple format for compatibility).
        /// </summary>
        [System.Obsolete("Use GetSongMetadata() instead for better type safety")]
        public static (float bpm, int beatsPerBar, int totalBeats) GetMetadata(MidiFile midi)
        {
            var metadata = GetSongMetadata(midi);
            return (metadata.BPM, metadata.BeatsPerBar, metadata.TotalBeats);
        }

        private static bool HasNoteEvents(MidiTrack track)
        {
            foreach (var evt in track.Events)
            {
                if (evt is NoteOnEvent)
                {
                    return true;
                }
            }
            return false;
        }

        private static InstrumentType GetInstrumentType(MidiTrack track)
        {
            // Check channel
            if (ChannelToInstrument.TryGetValue(track.Channel, out var type))
            {
                return type;
            }

            // Check track name for hints
            string nameLower = track.Name.ToLower();
            if (nameLower.Contains("drum") || nameLower.Contains("percussion"))
            {
                return InstrumentType.Drum;
            }
            if (nameLower.Contains("bass"))
            {
                return InstrumentType.Bass;
            }
            if (nameLower.Contains("guitar"))
            {
                return InstrumentType.Guitar;
            }

            return InstrumentType.Piano; // Default
        }

        #endregion

        #region LoopTrackData to MIDI

        /// <summary>
        /// Convert LoopTrackData list to MidiFile.
        /// </summary>
        public static MidiFile FromLoopTracks(
            List<LoopTrackData> tracks,
            float bpm,
            int beatsPerBar,
            string songName = "Untitled")
        {
            var midi = new MidiFile
            {
                Format = 1,
                TicksPerBeat = 480,
                BPM = bpm,
                BeatsPerBar = beatsPerBar,
                Name = songName
            };

            // Create tempo track (track 0)
            var tempoTrack = CreateTempoTrack(bpm, beatsPerBar, songName);
            midi.Tracks.Add(tempoTrack);

            // Create note tracks
            foreach (var loopTrack in tracks)
            {
                if (loopTrack.EventCount == 0)
                {
                    continue;
                }

                var midiTrack = CreateNoteTrack(loopTrack, midi.TicksPerBeat);
                midi.Tracks.Add(midiTrack);
            }

            return midi;
        }

        private static MidiTrack CreateTempoTrack(float bpm, int beatsPerBar, string songName)
        {
            var track = new MidiTrack
            {
                Name = songName,
                Channel = 0
            };

            // Track name
            track.Events.Add(new TrackNameEvent
            {
                AbsoluteTime = 0,
                DeltaTime = 0,
                Name = songName
            });

            // Time signature
            track.Events.Add(new TimeSignatureEvent
            {
                AbsoluteTime = 0,
                DeltaTime = 0,
                Numerator = beatsPerBar,
                Denominator = 4,
                ClocksPerClick = 24,
                ThirtySecondNotesPerBeat = 8
            });

            // Tempo
            int microsecondsPerBeat = (int)(60_000_000f / bpm);
            track.Events.Add(new TempoEvent
            {
                AbsoluteTime = 0,
                DeltaTime = 0,
                MicrosecondsPerBeat = microsecondsPerBeat
            });

            // End of track
            track.Events.Add(new EndOfTrackEvent
            {
                AbsoluteTime = 0,
                DeltaTime = 0
            });

            return track;
        }

        private static MidiTrack CreateNoteTrack(LoopTrackData loopTrack, int ticksPerBeat)
        {
            int channel = InstrumentToChannel.TryGetValue(loopTrack.InstrumentType, out var ch) ? ch : 0;
            int program = InstrumentToProgram.TryGetValue(loopTrack.InstrumentType, out var prog) ? prog : 0;

            var track = new MidiTrack
            {
                Name = loopTrack.TrackName,
                Channel = channel
            };

            // Track name
            track.Events.Add(new TrackNameEvent
            {
                AbsoluteTime = 0,
                DeltaTime = 0,
                Name = loopTrack.TrackName
            });

            // Program change (except for drums)
            if (loopTrack.InstrumentType != InstrumentType.Drum)
            {
                track.Events.Add(new ProgramChangeEvent
                {
                    AbsoluteTime = 0,
                    DeltaTime = 0,
                    Channel = channel,
                    Program = program
                });
            }

            // Convert note events
            var midiEvents = new List<MidiEvent>();

            foreach (var noteEvent in loopTrack.Events)
            {
                int startTick = (int)(noteEvent.beatTime * ticksPerBeat);
                int endTick = (int)((noteEvent.beatTime + noteEvent.duration) * ticksPerBeat);
                int velocity = (int)(noteEvent.velocity * 127);

                // Note On
                midiEvents.Add(new NoteOnEvent
                {
                    AbsoluteTime = startTick,
                    Channel = channel,
                    Note = noteEvent.note,
                    Velocity = velocity
                });

                // Note Off
                midiEvents.Add(new NoteOffEvent
                {
                    AbsoluteTime = endTick,
                    Channel = channel,
                    Note = noteEvent.note,
                    Velocity = 0
                });
            }

            // Sort by absolute time
            midiEvents.Sort((a, b) => a.AbsoluteTime.CompareTo(b.AbsoluteTime));

            // Calculate delta times and add to track
            int lastTime = 0;
            foreach (var evt in midiEvents)
            {
                evt.DeltaTime = evt.AbsoluteTime - lastTime;
                lastTime = evt.AbsoluteTime;
                track.Events.Add(evt);
            }

            // Find last event time for end of track
            int lastEventTime = 0;
            foreach (var evt in track.Events)
            {
                if (evt.AbsoluteTime > lastEventTime)
                {
                    lastEventTime = evt.AbsoluteTime;
                }
            }

            // End of track
            track.Events.Add(new EndOfTrackEvent
            {
                AbsoluteTime = lastEventTime,
                DeltaTime = 0
            });

            return track;
        }

        #endregion
    }
}
