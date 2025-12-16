using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SoloBandStudio.MIDI
{
    /// <summary>
    /// Writes MidiFile structure to MIDI binary data.
    /// </summary>
    public static class MidiWriter
    {
        /// <summary>
        /// Write MidiFile to byte array.
        /// </summary>
        public static byte[] Write(MidiFile midi)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                WriteFile(writer, midi);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Write MidiFile to file.
        /// </summary>
        public static void WriteFile(MidiFile midi, string filePath)
        {
            byte[] data = Write(midi);

            // Ensure directory exists
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(filePath, data);
        }

        private static void WriteFile(BinaryWriter writer, MidiFile midi)
        {
            // Write header chunk
            WriteChunkType(writer, "MThd");
            WriteInt32BigEndian(writer, 6); // Header length always 6
            WriteUInt16BigEndian(writer, midi.Format);
            WriteUInt16BigEndian(writer, (ushort)midi.Tracks.Count);
            WriteUInt16BigEndian(writer, midi.TicksPerBeat);

            // Write track chunks
            foreach (var track in midi.Tracks)
            {
                WriteTrack(writer, track, midi);
            }
        }

        private static void WriteTrack(BinaryWriter writer, MidiTrack track, MidiFile midi)
        {
            // Build track data first
            using (var trackStream = new MemoryStream())
            using (var trackWriter = new BinaryWriter(trackStream))
            {
                int lastAbsoluteTime = 0;

                foreach (var evt in track.Events)
                {
                    int deltaTime = evt.AbsoluteTime - lastAbsoluteTime;
                    lastAbsoluteTime = evt.AbsoluteTime;

                    WriteEvent(trackWriter, evt, deltaTime);
                }

                // Ensure End of Track
                bool hasEndOfTrack = false;
                foreach (var evt in track.Events)
                {
                    if (evt is EndOfTrackEvent)
                    {
                        hasEndOfTrack = true;
                        break;
                    }
                }
                if (!hasEndOfTrack)
                {
                    WriteVariableLength(trackWriter, 0);
                    trackWriter.Write((byte)0xFF);
                    trackWriter.Write((byte)0x2F);
                    trackWriter.Write((byte)0x00);
                }

                byte[] trackData = trackStream.ToArray();

                // Write track chunk
                WriteChunkType(writer, "MTrk");
                WriteInt32BigEndian(writer, trackData.Length);
                writer.Write(trackData);
            }
        }

        private static void WriteEvent(BinaryWriter writer, MidiEvent evt, int deltaTime)
        {
            WriteVariableLength(writer, deltaTime);

            switch (evt)
            {
                case NoteOnEvent noteOn:
                    writer.Write((byte)(MidiEventType.NoteOn | (noteOn.Channel & 0x0F)));
                    writer.Write((byte)noteOn.Note);
                    writer.Write((byte)noteOn.Velocity);
                    break;

                case NoteOffEvent noteOff:
                    writer.Write((byte)(MidiEventType.NoteOff | (noteOff.Channel & 0x0F)));
                    writer.Write((byte)noteOff.Note);
                    writer.Write((byte)noteOff.Velocity);
                    break;

                case TempoEvent tempo:
                    writer.Write((byte)0xFF);
                    writer.Write((byte)MidiEventType.MetaTempo);
                    writer.Write((byte)0x03); // Length
                    int microseconds = tempo.MicrosecondsPerBeat;
                    writer.Write((byte)((microseconds >> 16) & 0xFF));
                    writer.Write((byte)((microseconds >> 8) & 0xFF));
                    writer.Write((byte)(microseconds & 0xFF));
                    break;

                case TimeSignatureEvent timeSig:
                    writer.Write((byte)0xFF);
                    writer.Write((byte)MidiEventType.MetaTimeSignature);
                    writer.Write((byte)0x04); // Length
                    writer.Write((byte)timeSig.Numerator);
                    writer.Write((byte)Log2(timeSig.Denominator));
                    writer.Write((byte)timeSig.ClocksPerClick);
                    writer.Write((byte)timeSig.ThirtySecondNotesPerBeat);
                    break;

                case TrackNameEvent trackName:
                    writer.Write((byte)0xFF);
                    writer.Write((byte)MidiEventType.MetaTrackName);
                    byte[] nameBytes = Encoding.UTF8.GetBytes(trackName.Name);
                    WriteVariableLength(writer, nameBytes.Length);
                    writer.Write(nameBytes);
                    break;

                case EndOfTrackEvent _:
                    writer.Write((byte)0xFF);
                    writer.Write((byte)MidiEventType.MetaEndOfTrack);
                    writer.Write((byte)0x00);
                    break;

                case ProgramChangeEvent program:
                    writer.Write((byte)(MidiEventType.ProgramChange | (program.Channel & 0x0F)));
                    writer.Write((byte)program.Program);
                    break;

                case ControlChangeEvent cc:
                    writer.Write((byte)(MidiEventType.ControlChange | (cc.Channel & 0x0F)));
                    writer.Write((byte)cc.Controller);
                    writer.Write((byte)cc.Value);
                    break;

                case UnknownEvent unknown:
                    if (unknown.Data != null)
                    {
                        writer.Write(unknown.Data);
                    }
                    break;
            }
        }

        #region Binary Writing Helpers

        private static void WriteChunkType(BinaryWriter writer, string type)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(type);
            writer.Write(bytes);
        }

        private static void WriteInt32BigEndian(BinaryWriter writer, int value)
        {
            writer.Write((byte)((value >> 24) & 0xFF));
            writer.Write((byte)((value >> 16) & 0xFF));
            writer.Write((byte)((value >> 8) & 0xFF));
            writer.Write((byte)(value & 0xFF));
        }

        private static void WriteUInt16BigEndian(BinaryWriter writer, ushort value)
        {
            writer.Write((byte)((value >> 8) & 0xFF));
            writer.Write((byte)(value & 0xFF));
        }

        private static void WriteVariableLength(BinaryWriter writer, int value)
        {
            if (value < 0)
            {
                value = 0;
            }

            // Build bytes in reverse order
            var bytes = new List<byte>();
            bytes.Add((byte)(value & 0x7F));
            value >>= 7;

            while (value > 0)
            {
                bytes.Add((byte)((value & 0x7F) | 0x80));
                value >>= 7;
            }

            // Write in reverse (highest byte first)
            for (int i = bytes.Count - 1; i >= 0; i--)
            {
                writer.Write(bytes[i]);
            }
        }

        private static int Log2(int value)
        {
            int result = 0;
            while (value > 1)
            {
                value >>= 1;
                result++;
            }
            return result;
        }

        #endregion
    }
}
