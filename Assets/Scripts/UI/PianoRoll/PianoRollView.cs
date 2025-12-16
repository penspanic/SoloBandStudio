using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using SoloBandStudio.Audio;
using SoloBandStudio.Core;

namespace SoloBandStudio.UI.PianoRoll
{
    /// <summary>
    /// Main coordinator for the Piano Roll UI.
    /// Delegates responsibilities to specialized components.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PianoRollView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LoopStation loopStation;
        [SerializeField] private BeatClock beatClock;

        [Header("Settings")]
        [SerializeField] private int defaultMinNote = 48;  // C3
        [SerializeField] private int defaultMaxNote = 84;  // C6

        [Header("VR Input")]
        [SerializeField] private InputActionReference durationCycleAction;

        // Components
        private PianoRollLayout layout;
        private PianoRollData data;
        private PianoRollGrid grid;
        private PianoRollTimeline timeline;
        private PianoRollPianoKeys pianoKeys;
        private PianoRollNoteManager noteManager;

        // VR Input
        private InputAction durationInputAction;
        private bool isModifierHeld = false;  // B/Y button held

        // Grid drawing state
        private bool isDrawingNote = false;
        private int drawnNoteIndex = -1;
        private Vector3 drawStartPos;
        private float drawnNoteBeat;
        private int drawnNotepitch;

        private bool isInitialized;

        private void Start()
        {
            data = new PianoRollData();
            data.SetNoteRange(defaultMinNote, defaultMaxNote);

            if (loopStation == null)
                loopStation = FindFirstObjectByType<LoopStation>();

            if (beatClock == null && loopStation != null)
                beatClock = loopStation.BeatClock;

            StartCoroutine(InitializeDelayed());
        }

        private IEnumerator InitializeDelayed()
        {
            yield return null;

            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null || uiDocument.rootVisualElement == null)
            {
                Debug.LogError("[PianoRoll] UIDocument or root element not found!");
                yield break;
            }

            // Initialize layout
            layout = new PianoRollLayout(uiDocument.rootVisualElement);
            if (!layout.IsValid)
            {
                Debug.LogError("[PianoRoll] Layout initialization failed!");
                yield break;
            }

            // Initialize components
            grid = new PianoRollGrid(layout, data, beatClock);
            timeline = new PianoRollTimeline(layout, data, beatClock);
            pianoKeys = new PianoRollPianoKeys(layout, data);
            noteManager = new PianoRollNoteManager(layout, data);

            // Subscribe to scroll changes (event-based, safe timing)
            grid.OnScrollChanged += OnScrollChanged;
            grid.SubscribeToScrollEvents();

            // Subscribe to timeline click for seeking
            timeline.OnSeekRequested += OnTimelineSeekRequested;
            timeline.RegisterClickHandler();

            // Subscribe to selection changes for duration label
            noteManager.OnSelectionChanged += OnNoteSelectionChanged;

            // Setup UI
            SetupToolbar();
            SetupGridInteraction();
            GenerateAll();
            RefreshTrackDropdown();
            RefreshQuantizeDropdown();
            SubscribeToLoopStationEvents();

            isInitialized = true;
            Debug.Log("[PianoRoll] Initialized with new architecture");
        }

        private void OnEnable()
        {
            // Setup VR input for duration cycling
            if (durationCycleAction != null && durationCycleAction.action != null)
            {
                durationCycleAction.action.performed += OnDurationCyclePerformed;
                durationCycleAction.action.Enable();
            }
            else
            {
                // Fallback: create input action for primaryButton (A/X on controllers)
                durationInputAction = new InputAction("DurationCycle", InputActionType.Button);
                durationInputAction.AddBinding("<XRController>{RightHand}/primaryButton");
                durationInputAction.AddBinding("<XRController>{LeftHand}/primaryButton");
                durationInputAction.performed += OnModifierPressed;
                durationInputAction.canceled += OnModifierReleased;
                durationInputAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (durationCycleAction != null && durationCycleAction.action != null)
            {
                durationCycleAction.action.performed -= OnDurationCyclePerformed;
            }

            if (durationInputAction != null)
            {
                durationInputAction.performed -= OnModifierPressed;
                durationInputAction.canceled -= OnModifierReleased;
                durationInputAction.Disable();
            }
        }

        private void OnModifierPressed(InputAction.CallbackContext ctx)
        {
            isModifierHeld = true;

            // Also cycle duration if a note is selected
            if (isInitialized && data.SelectedNoteIndex >= 0 && data.CurrentTrack != null)
            {
                var currentNote = data.CurrentTrack.Events[data.SelectedNoteIndex];
                float newDuration = NoteDurationPresets.GetNext(currentNote.duration);

                var newNoteData = new NoteEvent
                {
                    beatTime = currentNote.beatTime,
                    note = currentNote.note,
                    velocity = currentNote.velocity,
                    duration = newDuration
                };

                data.CurrentTrack.UpdateEvent(data.SelectedNoteIndex, newNoteData);
                // Only update duration visual, keep position (prevents reset during drag)
                noteManager.UpdateNoteDurationOnly(data.SelectedNoteIndex, newDuration);
                UpdateDurationLabel();

                Debug.Log($"[PianoRoll] Duration cycled to {NoteDurationPresets.Names[NoteDurationPresets.GetIndex(newDuration)]}");
            }
        }

        private void OnModifierReleased(InputAction.CallbackContext ctx)
        {
            isModifierHeld = false;

            // If drawing a note, finalize it
            if (isDrawingNote)
            {
                FinalizeDrawnNote();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromLoopStationEvents();
            durationInputAction?.Dispose();

            if (grid != null)
            {
                grid.OnScrollChanged -= OnScrollChanged;
                grid.UnsubscribeFromScrollEvents();
            }
            if (timeline != null)
            {
                timeline.OnSeekRequested -= OnTimelineSeekRequested;
                timeline.UnregisterClickHandler();
            }
            if (noteManager != null)
            {
                noteManager.OnSelectionChanged -= OnNoteSelectionChanged;
            }
        }

        private void OnNoteSelectionChanged(int selectedIndex)
        {
            UpdateDurationLabel();
        }

        private void Update()
        {
            if (!isInitialized) return;

            // Update position label (safe - just text)
            UpdatePositionLabel();
        }

        private void LateUpdate()
        {
            if (!isInitialized) return;

            // Process pending scroll updates safely after rendering decisions
            grid.ProcessPendingScrollUpdate();

            // Update playhead
            bool isPlaying = loopStation != null && loopStation.IsPlaying;
            float currentBeat = beatClock?.CurrentBeatPosition ?? 0;
            grid.UpdatePlayhead(currentBeat, isPlaying);
        }

        #region Scroll Sync

        private void OnScrollChanged(Vector2 scrollOffset)
        {
            // Safe to call from LateUpdate since wheel events are now deferred
            timeline.SyncScroll(scrollOffset);
            pianoKeys.SyncScroll(scrollOffset);
        }

        #endregion

        #region Timeline Seek

        private void OnTimelineSeekRequested(float beat)
        {
            if (beatClock == null) return;
            beatClock.SeekTo(beat);
        }

        #endregion

        #region UI Setup

        private void SetupToolbar()
        {
            // Track dropdown
            layout.TrackDropdown?.RegisterValueChangedCallback(evt => {
                OnTrackSelected(layout.TrackDropdown.index);
            });

            // Quantize dropdown
            layout.QuantizeDropdown?.RegisterValueChangedCallback(evt => {
                int index = layout.QuantizeDropdown.index;
                if (index >= 0 && index < QuantizePresets.Values.Length)
                {
                    data.SetQuantize(QuantizePresets.Values[index].value);
                    grid.Generate();
                    timeline.Generate();
                }
            });

            // Zoom buttons
            layout.ZoomInBtn?.RegisterCallback<ClickEvent>(evt => {
                data.ZoomIn();
                UpdateZoomLabel();
                RegenerateAll();
            });

            layout.ZoomOutBtn?.RegisterCallback<ClickEvent>(evt => {
                data.ZoomOut();
                UpdateZoomLabel();
                RegenerateAll();
            });

            // Edit mode buttons
            layout.EditModeBtn?.RegisterCallback<ClickEvent>(_ => SetEditMode(EditMode.Edit));
            layout.EraseModeBtn?.RegisterCallback<ClickEvent>(_ => SetEditMode(EditMode.Erase));

            UpdateZoomLabel();
            UpdateEditModeButtons();
            UpdateDurationLabel();
        }

        private void SetupGridInteraction()
        {
            layout.GridContainer?.RegisterCallback<PointerDownEvent>(OnGridPointerDown);
            layout.GridContainer?.RegisterCallback<PointerMoveEvent>(OnGridPointerMove);
            layout.GridContainer?.RegisterCallback<PointerUpEvent>(OnGridPointerUp);
        }

        #endregion

        #region Generation

        private void GenerateAll()
        {
            grid.Generate();
            timeline.Generate();
            pianoKeys.Generate();
            noteManager.RefreshNotes();
            UpdateStatusLabel();
        }

        private void RegenerateAll()
        {
            GenerateAll();
        }

        #endregion

        #region Track Management

        private void OnTrackSelected(int index)
        {
            if (loopStation == null) return;

            if (index >= 0 && index < loopStation.TrackCount)
            {
                data.CurrentTrackIndex = index;
                data.CurrentTrack = loopStation.GetTrack(index);
                data.ClearSelection();

                UpdateNoteRangeFromTrack();
                GenerateAll();

                Debug.Log($"[PianoRoll] Track selected: {data.CurrentTrack?.TrackName}");
            }
            else
            {
                data.CurrentTrackIndex = -1;
                data.CurrentTrack = null;
                data.SetNoteRange(defaultMinNote, defaultMaxNote);
                GenerateAll();
            }
        }

        private void UpdateNoteRangeFromTrack()
        {
            if (data.CurrentTrack == null || data.CurrentTrack.EventCount == 0)
            {
                data.SetNoteRange(defaultMinNote, defaultMaxNote);
                return;
            }

            int minNote = 127;
            int maxNote = 0;

            foreach (var noteEvent in data.CurrentTrack.Events)
            {
                if (noteEvent.note < minNote) minNote = noteEvent.note;
                if (noteEvent.note > maxNote) maxNote = noteEvent.note;
            }

            // Add padding
            int padding = 12;
            minNote = Mathf.Max(0, minNote - padding);
            maxNote = Mathf.Min(127, maxNote + padding);

            // Ensure minimum range
            int minRange = 36;
            if (maxNote - minNote < minRange)
            {
                int centerNote = (minNote + maxNote) / 2;
                minNote = Mathf.Max(0, centerNote - minRange / 2);
                maxNote = Mathf.Min(127, centerNote + minRange / 2);
            }

            // Snap to C notes
            minNote = (minNote / 12) * 12;
            maxNote = ((maxNote / 12) + 1) * 12;

            data.SetNoteRange(minNote, maxNote);
        }

        #endregion

        #region Interaction

        private void OnGridPointerDown(PointerDownEvent evt)
        {
            if (data.CurrentTrack == null) return;
            if (data.CurrentEditMode != EditMode.Edit) return;

            // Only create note when modifier (B/Y) is held
            if (!isModifierHeld) return;

            Vector2 localPos = evt.localPosition;
            float beat = data.PixelToBeat(localPos.x);
            int note = data.PixelToNote(localPos.y);

            beat = data.Quantize(beat);
            drawnNoteBeat = beat;
            drawnNotepitch = note;
            drawStartPos = evt.position;

            // Create note and start dragging immediately
            noteManager.AddNote(beat, note, data.QuantizeValue, 1.0f);
            drawnNoteIndex = data.CurrentTrack.EventCount - 1;
            isDrawingNote = true;

            // Select the new note
            data.SelectedNoteIndex = drawnNoteIndex;
            noteManager.RefreshNotes();
            UpdateDurationLabel();

            evt.StopPropagation();
            Debug.Log($"[PianoRoll] Started drawing note at beat {beat}, note {note}");
        }

        private void OnGridPointerMove(PointerMoveEvent evt)
        {
            if (!isDrawingNote) return;
            if (drawnNoteIndex < 0 || data.CurrentTrack == null) return;

            Vector3 delta = evt.position - drawStartPos;
            float multiplier = 100f;

            // Calculate new position
            float beatDelta = (delta.x * multiplier) / data.PixelsPerBeat;
            float newBeatTime = drawnNoteBeat + beatDelta;
            newBeatTime = data.Quantize(newBeatTime);
            newBeatTime = Mathf.Max(0, newBeatTime);

            float noteDelta = (delta.y * multiplier) / data.PixelsPerNote;
            int newNote = drawnNotepitch + Mathf.RoundToInt(noteDelta);
            newNote = Mathf.Clamp(newNote, data.MinVisibleNote, data.MaxVisibleNote);

            // Update the note
            var oldEvent = data.CurrentTrack.Events[drawnNoteIndex];
            var newEvent = new NoteEvent
            {
                beatTime = newBeatTime,
                note = newNote,
                velocity = oldEvent.velocity,
                duration = oldEvent.duration
            };

            data.CurrentTrack.UpdateEvent(drawnNoteIndex, newEvent);
            noteManager.RefreshNotes();
        }

        private void OnGridPointerUp(PointerUpEvent evt)
        {
            if (isDrawingNote)
            {
                FinalizeDrawnNote();
            }
        }

        private void FinalizeDrawnNote()
        {
            if (drawnNoteIndex >= 0)
            {
                Debug.Log($"[PianoRoll] Finalized drawn note at index {drawnNoteIndex}");
            }

            isDrawingNote = false;
            drawnNoteIndex = -1;
        }

        #endregion

        #region UI Updates

        private void SetEditMode(EditMode mode)
        {
            data.CurrentEditMode = mode;
            UpdateEditModeButtons();
            Debug.Log($"[PianoRoll] Edit mode: {mode}");
        }

        private void UpdateEditModeButtons()
        {
            layout.EditModeBtn?.EnableInClassList("edit-mode-active", data.CurrentEditMode == EditMode.Edit);
            layout.EraseModeBtn?.EnableInClassList("edit-mode-active", data.CurrentEditMode == EditMode.Erase);
        }

        private void OnDurationCyclePerformed(InputAction.CallbackContext ctx)
        {
            if (!isInitialized) return;
            if (data.SelectedNoteIndex < 0) return;
            if (data.CurrentTrack == null) return;

            // Cycle to next duration
            var currentNote = data.CurrentTrack.Events[data.SelectedNoteIndex];
            float newDuration = NoteDurationPresets.GetNext(currentNote.duration);

            // Update the note
            var newNoteData = new NoteEvent
            {
                beatTime = currentNote.beatTime,
                note = currentNote.note,
                velocity = currentNote.velocity,
                duration = newDuration
            };

            data.CurrentTrack.UpdateEvent(data.SelectedNoteIndex, newNoteData);
            noteManager.RefreshNotes();
            UpdateDurationLabel();

            Debug.Log($"[PianoRoll] Duration cycled to {NoteDurationPresets.Names[NoteDurationPresets.GetIndex(newDuration)]}");
        }

        private void UpdateDurationLabel()
        {
            if (layout.DurationLabel == null) return;

            if (data.SelectedNoteIndex >= 0 && data.CurrentTrack != null)
            {
                var note = data.CurrentTrack.Events[data.SelectedNoteIndex];
                int index = NoteDurationPresets.GetIndex(note.duration);
                layout.DurationLabel.text = NoteDurationPresets.Names[index];
            }
            else
            {
                layout.DurationLabel.text = "-";
            }
        }

        private void UpdateZoomLabel()
        {
            if (layout.ZoomLabel != null)
            {
                layout.ZoomLabel.text = $"{Mathf.RoundToInt(data.ZoomLevel * 100)}%";
            }
        }

        private void UpdateStatusLabel()
        {
            if (layout.StatusLabel == null) return;

            if (data.CurrentTrack == null)
            {
                layout.StatusLabel.text = "No track selected";
            }
            else
            {
                int noteCount = data.CurrentTrack.EventCount;
                layout.StatusLabel.text = $"{data.CurrentTrack.TrackName} - {noteCount} notes";
            }
        }

        private void UpdatePositionLabel()
        {
            if (layout.PositionLabel == null || beatClock == null) return;

            float currentBeat = beatClock.CurrentBeatPosition;
            int bar = (int)(currentBeat / beatClock.BeatsPerBar) + 1;
            int beat = (int)(currentBeat % beatClock.BeatsPerBar) + 1;

            layout.PositionLabel.text = $"Bar {bar} | Beat {beat}";
        }

        private void RefreshTrackDropdown()
        {
            if (layout.TrackDropdown == null || loopStation == null) return;

            var options = new List<string>();

            if (loopStation.TrackCount == 0)
            {
                options.Add("No tracks");
            }
            else
            {
                for (int i = 0; i < loopStation.TrackCount; i++)
                {
                    var track = loopStation.GetTrack(i);
                    options.Add(track?.TrackName ?? $"Track {i + 1}");
                }
            }

            layout.TrackDropdown.choices = options;

            if (data.CurrentTrackIndex >= 0 && data.CurrentTrackIndex < options.Count)
            {
                layout.TrackDropdown.index = data.CurrentTrackIndex;
            }
            else if (options.Count > 0)
            {
                layout.TrackDropdown.index = 0;
            }
        }

        private void RefreshQuantizeDropdown()
        {
            if (layout.QuantizeDropdown == null) return;

            var options = new List<string>();
            int selectedIndex = 4; // Default to 1/16

            for (int i = 0; i < QuantizePresets.Values.Length; i++)
            {
                options.Add(QuantizePresets.Values[i].name);
                if (Mathf.Approximately(QuantizePresets.Values[i].value, data.QuantizeValue))
                {
                    selectedIndex = i;
                }
            }

            layout.QuantizeDropdown.choices = options;
            layout.QuantizeDropdown.index = selectedIndex;
        }

        #endregion

        #region LoopStation Events

        private void SubscribeToLoopStationEvents()
        {
            if (loopStation != null)
            {
                loopStation.OnTrackCreated += OnTrackCreated;
                loopStation.OnTrackRemoved += OnTrackRemoved;
                loopStation.OnRecordingStarted += OnRecordingStarted;
                loopStation.OnRecordingStopped += OnRecordingStopped;
            }
        }

        private void UnsubscribeFromLoopStationEvents()
        {
            if (loopStation != null)
            {
                loopStation.OnTrackCreated -= OnTrackCreated;
                loopStation.OnTrackRemoved -= OnTrackRemoved;
                loopStation.OnRecordingStarted -= OnRecordingStarted;
                loopStation.OnRecordingStopped -= OnRecordingStopped;
            }
        }

        private void OnTrackCreated(LoopTrackData track)
        {
            RefreshTrackDropdown();
            int index = loopStation.TrackCount - 1;
            OnTrackSelected(index);
            layout.TrackDropdown.index = index;
        }

        private void OnTrackRemoved(LoopTrackData track)
        {
            RefreshTrackDropdown();
            if (data.CurrentTrack == track)
            {
                data.CurrentTrack = null;
                data.CurrentTrackIndex = -1;
                noteManager.RefreshNotes();
            }
        }

        private void OnRecordingStarted(LoopTrackData track)
        {
            data.CurrentTrack = track;
            data.CurrentTrackIndex = loopStation.TrackCount - 1;
            RefreshTrackDropdown();
            noteManager.RefreshNotes();
        }

        private void OnRecordingStopped(LoopTrackData track)
        {
            noteManager.RefreshNotes();
            RefreshTrackDropdown();
        }

        #endregion

        #region Public API

        public void SelectTrack(int index)
        {
            OnTrackSelected(index);
            if (layout.TrackDropdown != null && index >= 0 && index < layout.TrackDropdown.choices.Count)
            {
                layout.TrackDropdown.index = index;
            }
        }

        public void Refresh()
        {
            RefreshTrackDropdown();
            GenerateAll();
        }

        #endregion
    }
}
