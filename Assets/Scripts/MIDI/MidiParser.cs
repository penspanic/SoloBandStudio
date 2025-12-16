using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace SoloBandStudio.MIDI
{
    /// <summary>
    /// Parses MIDI binary data into MidiFile structure.
    /// </summary>
    public static class MidiParser
    {
        /// <summary>
        /// Parse a MIDI file from byte array.
        /// </summary>
        public static MidiFile Parse(byte[] data)
        {
            if (data == null || data.Length < 14)
            {
                throw new ArgumentException("Invalid MIDI data");
            }

            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                return Parse(reader);
            }
        }

        /// <summary>
        /// Parse a MIDI file from file path.
        /// </summary>
        public static MidiFile ParseFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"MIDI file not found: {filePath}");
            }

            byte[] data = File.ReadAllBytes(filePath);
            return Parse(data);
        }

        private static MidiFile Parse(BinaryReader reader)
        {
            var midi = new MidiFile();

            // Read header chunk
            string headerChunk = ReadChunkType(reader);
            if (headerChunk != "MThd")
            {
                throw new FormatException($"Invalid MIDI header: expected 'MThd', got '{headerChunk}'");
            }

            int headerLength = ReadInt32BigEndian(reader);
            if (headerLength < 6)
            {
                throw new FormatException($"Invalid header length: {headerLength}");
            }

            midi.Format = ReadUInt16BigEndian(reader);
            ushort numTracks = ReadUInt16BigEndian(reader);
            midi.TicksPerBeat = ReadUInt16BigEndian(reader);

            // Skip any extra header bytes
            if (headerLength > 6)
            {
                reader.ReadBytes(headerLength - 6);
            }

            // Read track chunks
            for (int i = 0; i < numTracks; i++)
            {
                var track = ReadTrack(reader, midi.TicksPerBeat);
                if (track != null)
                {
                    midi.Tracks.Add(track);
                }
            }

            // Extract metadata from first track (usually tempo track)
            ExtractMetadata(midi);

            return midi;
        }

        private static MidiTrack ReadTrack(BinaryReader reader, int ticksPerBeat)
        {
            string chunkType = ReadChunkType(reader);
            if (chunkType != "MTrk")
            {
                Debug.LogWarning($"Expected 'MTrk', got '{chunkType}', skipping");
                int skipLength = ReadInt32BigEndian(reader);
                reader.ReadBytes(skipLength);
                return null;
            }

            int trackLength = ReadInt32BigEndian(reader);
            long trackEnd = reader.BaseStream.Position + trackLength;

            var track = new MidiTrack();
            int absoluteTime = 0;
            byte runningStatus = 0;

            while (reader.BaseStream.Position < trackEnd)
            {
                int deltaTime = ReadVariableLength(reader);
                absoluteTime += deltaTime;

                byte statusByte = reader.ReadByte();

                // Running status
                if (statusByte < 0x80)
                {
                    reader.BaseStream.Position--;
                    statusByte = runningStatus;
                }
                else if (statusByte < 0xF0)
                {
                    runningStatus = statusByte;
                }

                var evt = ParseEvent(reader, statusByte, deltaTime, absoluteTime);
                if (evt != null)
                {
                    track.Events.Add(evt);

                    // Extract track name
                    if (evt is TrackNameEvent nameEvt)
                    {
                        track.Name = nameEvt.Name;
                    }

                    // Track channel from note events
                    if (evt is NoteOnEvent noteOn)
                    {
                        track.Channel = noteOn.Channel;
                    }
                }
            }

            return track;
        }

        private static MidiEvent ParseEvent(BinaryReader reader, byte statusByte, int deltaTime, int absoluteTime)
        {
            int eventType = statusByte & 0xF0;
            int channel = statusByte & 0x0F;

            MidiEvent evt = null;

            switch (eventType)
            {
                case MidiEventType.NoteOff:
                    evt = new NoteOffEvent
                    {
                        Channel = channel,
                        Note = reader.ReadByte(),
                        Velocity = reader.ReadByte()
                    };
                    break;

                case MidiEventType.NoteOn:
                    int note = reader.ReadByte();
                    int velocity = reader.ReadByte();
                    // Velocity 0 is actually Note Off
                    if (velocity == 0)
                    {
                        evt = new NoteOffEvent
                        {
                            Channel = channel,
                            Note = note,
                            Velocity = 0
                        };
                    }
                    else
                    {
                        evt = new NoteOnEvent
                        {
                            Channel = channel,
                            Note = note,
                            Velocity = velocity
                        };
                    }
                    break;

                case MidiEventType.PolyPressure:
                    reader.ReadBytes(2); // Skip
                    break;

                case MidiEventType.ControlChange:
                    evt = new ControlChangeEvent
                    {
                        Channel = channel,
                        Controller = reader.ReadByte(),
                        Value = reader.ReadByte()
                    };
                    break;

                case MidiEventType.ProgramChange:
                    evt = new ProgramChangeEvent
                    {
                        Channel = channel,
                        Program = reader.ReadByte()
                    };
                    break;

                case MidiEventType.ChannelPressure:
                    reader.ReadByte(); // Skip
                    break;

                case MidiEventType.PitchBend:
                    reader.ReadBytes(2); // Skip
                    break;

                default:
                    if (statusByte == MidiEventType.Meta)
                    {
                        evt = ParseMetaEvent(reader);
                    }
                    else if (statusByte == MidiEventType.SysEx || statusByte == 0xF7)
                    {
                        int length = ReadVariableLength(reader);
                        reader.ReadBytes(length); // Skip SysEx
                    }
                    break;
            }

            if (evt != null)
            {
                evt.DeltaTime = deltaTime;
                evt.AbsoluteTime = absoluteTime;
            }

            return evt;
        }

        private static MidiEvent ParseMetaEvent(BinaryReader reader)
        {
            byte metaType = reader.ReadByte();
            int length = ReadVariableLength(reader);

            switch (metaType)
            {
                case MidiEventType.MetaTrackName:
                    return new TrackNameEvent
                    {
                        Name = Encoding.UTF8.GetString(reader.ReadBytes(length))
                    };

                case MidiEventType.MetaTempo:
                    if (length >= 3)
                    {
                        int microseconds = (reader.ReadByte() << 16) |
                                          (reader.ReadByte() << 8) |
                                          reader.ReadByte();
                        if (length > 3) reader.ReadBytes(length - 3);
                        return new TempoEvent { MicrosecondsPerBeat = microseconds };
                    }
                    reader.ReadBytes(length);
                    break;

                case MidiEventType.MetaTimeSignature:
                    if (length >= 4)
                    {
                        int numerator = reader.ReadByte();
                        int denomPower = reader.ReadByte();
                        int clocks = reader.ReadByte();
                        int thirtySeconds = reader.ReadByte();
                        if (length > 4) reader.ReadBytes(length - 4);
                        return new TimeSignatureEvent
                        {
                            Numerator = numerator,
                            Denominator = (int)Math.Pow(2, denomPower),
                            ClocksPerClick = clocks,
                            ThirtySecondNotesPerBeat = thirtySeconds
                        };
                    }
                    reader.ReadBytes(length);
                    break;

                case MidiEventType.MetaEndOfTrack:
                    return new EndOfTrackEvent();

                default:
                    reader.ReadBytes(length); // Skip unknown meta events
                    break;
            }

            return null;
        }

        private static void ExtractMetadata(MidiFile midi)
        {
            // Look through all tracks for tempo and time signature
            foreach (var track in midi.Tracks)
            {
                foreach (var evt in track.Events)
                {
                    if (evt is TempoEvent tempo)
                    {
                        midi.BPM = tempo.BPM;
                    }
                    else if (evt is TimeSignatureEvent timeSig)
                    {
                        midi.BeatsPerBar = timeSig.Numerator;
                        midi.BeatUnit = timeSig.Denominator;
                    }
                    else if (evt is TrackNameEvent nameEvt && midi.Name == "Untitled")
                    {
                        midi.Name = nameEvt.Name;
                    }
                }
            }
        }

        #region Binary Reading Helpers

        private static string ReadChunkType(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            return Encoding.ASCII.GetString(bytes);
        }

        private static int ReadInt32BigEndian(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        }

        private static ushort ReadUInt16BigEndian(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(2);
            return (ushort)((bytes[0] << 8) | bytes[1]);
        }

        private static int ReadVariableLength(BinaryReader reader)
        {
            int value = 0;
            byte b;
            do
            {
                b = reader.ReadByte();
                value = (value << 7) | (b & 0x7F);
            } while ((b & 0x80) != 0);
            return value;
        }

        #endregion
    }
}
