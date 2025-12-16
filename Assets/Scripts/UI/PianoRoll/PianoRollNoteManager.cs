using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SoloBandStudio.Core;
using SoloBandStudio.Instruments.Keyboard;
using SoloBandStudio.Instruments.Drum;

namespace SoloBandStudio.UI.PianoRoll
{
    /// <summary>
    /// Manages note display, creation, selection, and editing.
    /// </summary>
    public class PianoRollNoteManager
    {
        private readonly PianoRollLayout layout;
        private readonly PianoRollData data;
        private readonly List<NoteBlockElement> noteElements = new List<NoteBlockElement>();

        public event Action OnNotesChanged;
        public event Action<int> OnSelectionChanged;  // (selectedIndex) - -1 for no selection

        public PianoRollNoteManager(PianoRollLayout layout, PianoRollData data)
        {
            this.layout = layout;
            this.data = data;
        }

        public void RefreshNotes()
        {
            if (layout.NotesContainer == null) return;

            layout.NotesContainer.Clear();
            noteElements.Clear();

            if (data.CurrentTrack == null) return;

            var events = data.CurrentTrack.Events;
            for (int i = 0; i < events.Count; i++)
            {
                var noteEvent = events[i];
                var noteElement = CreateNoteElement(noteEvent, i);
                layout.NotesContainer.Add(noteElement);
                noteElements.Add(noteElement);
            }
        }

        private NoteBlockElement CreateNoteElement(NoteEvent noteEvent, int index)
        {
            var instrumentType = data.CurrentTrack?.InstrumentType ?? InstrumentType.Piano;
            var element = new NoteBlockElement(index, noteEvent, data, instrumentType);

            element.SetSelected(data.IsNoteSelected(index));

            element.OnNoteSelected = OnNoteSelected;
            element.OnNoteMoved = OnNoteMoved;
            element.OnNoteResized = OnNoteResized;
            element.OnNoteDeleted = DeleteNote;
            element.OnNotePreview = PreviewNote;

            return element;
        }

        private void OnNoteSelected(int index)
        {
            data.ClearSelection();
            data.SelectedNoteIndex = index;

            for (int i = 0; i < noteElements.Count; i++)
            {
                noteElements[i].SetSelected(i == index);
            }

            var note = data.CurrentTrack.Events[index];
            Debug.Log($"[PianoRoll] Note selected: {PianoRollData.GetNoteName(note.note)} at beat {note.beatTime:F2}");

            OnSelectionChanged?.Invoke(index);
        }

        private void OnNoteMoved(int index, float newBeatTime, int newNote)
        {
            if (data.CurrentTrack == null) return;
            if (index < 0 || index >= data.CurrentTrack.EventCount) return;

            var oldEvent = data.CurrentTrack.Events[index];
            var newEvent = new NoteEvent
            {
                beatTime = newBeatTime,
                note = newNote,
                velocity = oldEvent.velocity,
                duration = oldEvent.duration
            };

            data.CurrentTrack.UpdateEvent(index, newEvent);
            noteElements[index].UpdateNoteData(newEvent);
            OnNotesChanged?.Invoke();
        }

        private void OnNoteResized(int index, float newDuration)
        {
            if (data.CurrentTrack == null) return;
            if (index < 0 || index >= data.CurrentTrack.EventCount) return;

            var oldEvent = data.CurrentTrack.Events[index];
            var newEvent = new NoteEvent
            {
                beatTime = oldEvent.beatTime,
                note = oldEvent.note,
                velocity = oldEvent.velocity,
                duration = newDuration
            };

            data.CurrentTrack.UpdateEvent(index, newEvent);
            noteElements[index].UpdateNoteData(newEvent);
            OnNotesChanged?.Invoke();
        }

        public void AddNote(float beatTime, int midiNote, float duration, float velocity)
        {
            if (data.CurrentTrack == null) return;

            var newNote = new NoteEvent
            {
                beatTime = beatTime,
                note = midiNote,
                duration = duration,
                velocity = velocity
            };

            data.CurrentTrack.AddEvent(newNote);
            RefreshNotes();
            PreviewNote(midiNote);
            OnNotesChanged?.Invoke();

            Debug.Log($"[PianoRoll] Note added: {PianoRollData.GetNoteName(midiNote)} at beat {beatTime:F2}");
        }

        public void DeleteNote(int index)
        {
            if (data.CurrentTrack == null) return;
            if (index < 0 || index >= data.CurrentTrack.EventCount) return;

            data.CurrentTrack.RemoveEventAt(index);
            data.ClearSelection();
            RefreshNotes();
            OnNotesChanged?.Invoke();

            Debug.Log($"[PianoRoll] Note deleted at index {index}");
        }

        public void ClearSelection()
        {
            data.ClearSelection();
            foreach (var element in noteElements)
            {
                element.SetSelected(false);
            }
            OnSelectionChanged?.Invoke(-1);
        }

        /// <summary>
        /// Update a specific note's visual without refreshing all notes.
        /// Used when changing duration while dragging.
        /// </summary>
        public void UpdateNoteVisual(int index)
        {
            if (index < 0 || index >= noteElements.Count) return;
            if (data.CurrentTrack == null || index >= data.CurrentTrack.EventCount) return;

            var noteEvent = data.CurrentTrack.Events[index];
            noteElements[index].UpdateNoteData(noteEvent);
        }

        /// <summary>
        /// Update only the duration of a note without changing its visual position.
        /// Used when cycling duration while the note is being dragged.
        /// </summary>
        public void UpdateNoteDurationOnly(int index, float newDuration)
        {
            if (index < 0 || index >= noteElements.Count) return;
            noteElements[index].UpdateDurationOnly(newDuration);
        }

        private void PreviewNote(int midiNote)
        {
            if (data.CurrentTrack == null) return;

            var instrumentType = data.CurrentTrack.InstrumentType;
            IInstrument instrument = instrumentType switch
            {
                InstrumentType.Piano => UnityEngine.Object.FindFirstObjectByType<Keyboard>(),
                InstrumentType.Drum => UnityEngine.Object.FindFirstObjectByType<Drum>(),
                _ => null
            };

            if (instrument != null)
            {
                double now = AudioSettings.dspTime + 0.01;
                var handle = instrument.ScheduleNote(midiNote, 0.8f, now);

                if (instrumentType != InstrumentType.Drum && handle != null)
                {
                    instrument.ScheduleNoteOff(handle, now + 0.2, 0.05f);
                }
            }
        }
    }
}
