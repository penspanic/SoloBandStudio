using System;
using System.Collections.Generic;
using UnityEngine;
using SoloBandStudio.Audio;
using SoloBandStudio.Core;

namespace SoloBandStudio.UI.PianoRoll
{
    /// <summary>
    /// View state and settings for the Piano Roll UI.
    /// </summary>
    [Serializable]
    public class PianoRollData
    {
        // Current track being displayed
        public LoopTrackData CurrentTrack { get; set; }
        public int CurrentTrackIndex { get; set; } = -1;

        // Selection state
        public int SelectedNoteIndex { get; set; } = -1;
        public List<int> SelectedNotes { get; } = new List<int>();

        // View settings
        [SerializeField] private float pixelsPerBeat = 80f;
        [SerializeField] private float pixelsPerNote = 16f;
        [SerializeField] private float scrollX = 0f;
        [SerializeField] private float scrollY = 0f;

        // Visible note range (MIDI numbers)
        [SerializeField] private int minVisibleNote = 48;  // C3
        [SerializeField] private int maxVisibleNote = 84;  // C6

        // Quantize settings
        [SerializeField] private float quantizeValue = 0.25f;  // 1/16 note

        // Zoom
        [SerializeField] private float zoomLevel = 1.0f;
        private const float MinZoom = 0.25f;
        private const float MaxZoom = 4.0f;

        // Edit mode
        public EditMode CurrentEditMode { get; set; } = EditMode.Edit;

        // Properties
        public float PixelsPerBeat => pixelsPerBeat * zoomLevel;
        public float PixelsPerNote => pixelsPerNote;
        public float ScrollX => scrollX;
        public float ScrollY => scrollY;
        public int MinVisibleNote => minVisibleNote;
        public int MaxVisibleNote => maxVisibleNote;
        public float QuantizeValue => quantizeValue;
        public float ZoomLevel => zoomLevel;

        public int VisibleNoteRange => maxVisibleNote - minVisibleNote + 1;
        public float GridHeight => VisibleNoteRange * pixelsPerNote;

        /// <summary>
        /// Convert beat time to pixel X position
        /// </summary>
        public float BeatToPixelX(float beatTime)
        {
            return (beatTime - scrollX) * PixelsPerBeat;
        }

        /// <summary>
        /// Convert pixel X to beat time
        /// </summary>
        public float PixelToBeat(float pixelX)
        {
            return (pixelX / PixelsPerBeat) + scrollX;
        }

        /// <summary>
        /// Convert MIDI note to pixel Y position (relative to grid content, not scroll)
        /// </summary>
        public float NoteToPixelY(int midiNote)
        {
            // Higher notes at top (smaller Y)
            // Note: Do NOT subtract scrollY here - scrolling is handled by ScrollView
            return (maxVisibleNote - midiNote) * pixelsPerNote;
        }

        /// <summary>
        /// Convert pixel Y to MIDI note (relative to grid content, not scroll)
        /// </summary>
        public int PixelToNote(float pixelY)
        {
            // Note: Do NOT add scrollY here - scrolling is handled by ScrollView
            return maxVisibleNote - (int)(pixelY / pixelsPerNote);
        }

        /// <summary>
        /// Convert duration to pixel width
        /// </summary>
        public float DurationToPixelWidth(float duration)
        {
            return duration * PixelsPerBeat;
        }

        /// <summary>
        /// Quantize a beat value to the current grid
        /// </summary>
        public float Quantize(float beatValue)
        {
            return Mathf.Round(beatValue / quantizeValue) * quantizeValue;
        }

        /// <summary>
        /// Set scroll position
        /// </summary>
        public void SetScroll(float x, float y)
        {
            scrollX = Mathf.Max(0, x);
            scrollY = Mathf.Max(0, y);
        }

        /// <summary>
        /// Zoom in
        /// </summary>
        public void ZoomIn()
        {
            zoomLevel = Mathf.Min(zoomLevel * 1.25f, MaxZoom);
        }

        /// <summary>
        /// Zoom out
        /// </summary>
        public void ZoomOut()
        {
            zoomLevel = Mathf.Max(zoomLevel / 1.25f, MinZoom);
        }

        /// <summary>
        /// Set zoom level
        /// </summary>
        public void SetZoom(float zoom)
        {
            zoomLevel = Mathf.Clamp(zoom, MinZoom, MaxZoom);
        }

        /// <summary>
        /// Set quantize value
        /// </summary>
        public void SetQuantize(float value)
        {
            quantizeValue = value;
        }

        /// <summary>
        /// Set visible note range
        /// </summary>
        public void SetNoteRange(int min, int max)
        {
            minVisibleNote = Mathf.Clamp(min, 0, 127);
            maxVisibleNote = Mathf.Clamp(max, minVisibleNote + 1, 127);
        }

        /// <summary>
        /// Clear selection
        /// </summary>
        public void ClearSelection()
        {
            SelectedNoteIndex = -1;
            SelectedNotes.Clear();
        }

        /// <summary>
        /// Check if a note index is selected
        /// </summary>
        public bool IsNoteSelected(int noteIndex)
        {
            return SelectedNoteIndex == noteIndex || SelectedNotes.Contains(noteIndex);
        }

        /// <summary>
        /// Get note name from MIDI number
        /// </summary>
        public static string GetNoteName(int midiNote)
        {
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int octave = (midiNote / 12) - 1;
            int noteIndex = midiNote % 12;
            return $"{noteNames[noteIndex]}{octave}";
        }

        /// <summary>
        /// Check if a MIDI note is a black key
        /// </summary>
        public static bool IsBlackKey(int midiNote)
        {
            int noteInOctave = midiNote % 12;
            // C#, D#, F#, G#, A# are black keys
            return noteInOctave == 1 || noteInOctave == 3 || noteInOctave == 6 || noteInOctave == 8 || noteInOctave == 10;
        }

        /// <summary>
        /// Check if a MIDI note is C
        /// </summary>
        public static bool IsC(int midiNote)
        {
            return midiNote % 12 == 0;
        }
    }

    /// <summary>
    /// Edit mode for the piano roll
    /// </summary>
    public enum EditMode
    {
        Edit,     // Click empty = add note, click note = select/move
        Erase     // Click note = delete
    }

    /// <summary>
    /// Note duration presets for cycling
    /// </summary>
    public static class NoteDurationPresets
    {
        public static readonly float[] Values = { 0.125f, 0.25f, 0.5f, 1.0f, 2.0f, 4.0f }; // 1/32, 1/16, 1/8, 1/4, 1/2, 1
        public static readonly string[] Names = { "1/32", "1/16", "1/8", "1/4", "1/2", "1" };

        public static int GetIndex(float duration)
        {
            for (int i = 0; i < Values.Length; i++)
            {
                if (Mathf.Approximately(Values[i], duration))
                    return i;
            }
            // Find closest
            float minDiff = float.MaxValue;
            int closest = 0;
            for (int i = 0; i < Values.Length; i++)
            {
                float diff = Mathf.Abs(Values[i] - duration);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closest = i;
                }
            }
            return closest;
        }

        public static float GetNext(float currentDuration)
        {
            int index = GetIndex(currentDuration);
            int nextIndex = (index + 1) % Values.Length;
            return Values[nextIndex];
        }

        public static float GetPrevious(float currentDuration)
        {
            int index = GetIndex(currentDuration);
            int prevIndex = (index - 1 + Values.Length) % Values.Length;
            return Values[prevIndex];
        }
    }

    /// <summary>
    /// Quantize presets
    /// </summary>
    public static class QuantizePresets
    {
        public static readonly (string name, float value)[] Values = {
            ("1/1", 4.0f),
            ("1/2", 2.0f),
            ("1/4", 1.0f),
            ("1/8", 0.5f),
            ("1/16", 0.25f),
            ("1/32", 0.125f)
        };

        public static string GetName(float value)
        {
            foreach (var preset in Values)
            {
                if (Mathf.Approximately(preset.value, value))
                    return preset.name;
            }
            return "Custom";
        }
    }
}
