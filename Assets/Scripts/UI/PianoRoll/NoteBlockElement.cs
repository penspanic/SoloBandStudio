using UnityEngine;
using UnityEngine.UIElements;
using SoloBandStudio.Core;

namespace SoloBandStudio.UI.PianoRoll
{
    /// <summary>
    /// Visual element representing a single note in the piano roll.
    /// Handles drag to move and resize interactions.
    /// </summary>
    public class NoteBlockElement : VisualElement
    {
        // Note data
        public int NoteIndex { get; private set; }
        public NoteEvent NoteData { get; private set; }

        // References
        private PianoRollData viewData;
        private VisualElement resizeHandle;

        // Drag state
        private bool isDragging = false;
        private bool isResizing = false;
        private Vector3 dragStartPos;
        private float originalBeatTime;
        private int originalNote;
        private float originalDuration;
        private int capturedPointerId = -1;
        private int lastPreviewedNote = -1;

        // Hold-to-activate state
        private bool isHolding = false;
        private bool isActivated = false;
        private float holdStartTime;
        private IVisualElementScheduledItem holdSchedule;
        private const float HOLD_DURATION = 0.5f;  // 0.5 second hold to activate

        // Events
        public System.Action<int, float, int> OnNoteMoved;      // (index, newBeatTime, newNote)
        public System.Action<int, float> OnNoteResized;          // (index, newDuration)
        public System.Action<int> OnNoteSelected;
        public System.Action<int> OnNoteDeleted;
        public System.Action<int> OnNotePreview;                 // (midiNote) - play preview sound

        // Constants
        private const float RESIZE_HANDLE_WIDTH = 8f;
        private const float MIN_DURATION = 0.125f;  // 1/32 note minimum

        public NoteBlockElement(int index, NoteEvent noteData, PianoRollData data, InstrumentType instrumentType)
        {
            NoteIndex = index;
            NoteData = noteData;
            viewData = data;

            // Base setup
            AddToClassList("note-block");

            // Enable pointer events
            pickingMode = PickingMode.Position;
            style.position = Position.Absolute;

            // Style based on instrument type
            switch (instrumentType)
            {
                case InstrumentType.Drum:
                    AddToClassList("note-block-drum");
                    break;
                case InstrumentType.Bass:
                    AddToClassList("note-block-bass");
                    break;
            }

            // Position and size
            UpdatePositionAndSize();

            // Opacity based on velocity
            style.opacity = 0.6f + (noteData.velocity * 0.4f);

            // Create resize handle (only for non-drum instruments)
            if (instrumentType != InstrumentType.Drum && noteData.duration > 0)
            {
                CreateResizeHandle();
            }

            // Register event handlers
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        private void CreateResizeHandle()
        {
            resizeHandle = new VisualElement();
            resizeHandle.AddToClassList("note-resize-handle");
            resizeHandle.style.position = Position.Absolute;
            resizeHandle.style.right = 0;
            resizeHandle.style.top = 0;
            resizeHandle.style.bottom = 0;
            resizeHandle.style.width = RESIZE_HANDLE_WIDTH;
            Add(resizeHandle);
        }

        public void UpdatePositionAndSize()
        {
            float x = viewData.BeatToPixelX(NoteData.beatTime);
            float y = viewData.NoteToPixelY(NoteData.note);
            float width = Mathf.Max(viewData.DurationToPixelWidth(NoteData.duration), 10f);

            style.left = x;
            style.top = y;
            style.width = width;
        }

        public void SetSelected(bool selected)
        {
            EnableInClassList("note-block-selected", selected);
        }

        public void UpdateNoteData(NoteEvent newData)
        {
            NoteData = newData;
            UpdatePositionAndSize();
            style.opacity = 0.6f + (newData.velocity * 0.4f);
        }

        /// <summary>
        /// Update only the duration (width) without changing position.
        /// Used when cycling duration during drag.
        /// </summary>
        public void UpdateDurationOnly(float newDuration)
        {
            var updated = NoteData;
            updated.duration = newDuration;
            NoteData = updated;

            // Only update width, keep current position
            float width = Mathf.Max(viewData.DurationToPixelWidth(newDuration), 10f);
            style.width = width;
        }

        #region Pointer Events

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return; // Left click only

            evt.StopPropagation();

            // Start hold timer
            isHolding = true;
            isActivated = false;
            holdStartTime = Time.realtimeSinceStartup;
            dragStartPos = evt.position;
            capturedPointerId = evt.pointerId;

            // Store original values for potential drag/resize
            originalBeatTime = NoteData.beatTime;
            originalNote = NoteData.note;
            originalDuration = NoteData.duration;
            lastPreviewedNote = NoteData.note;

            // Determine if this will be resize or drag (for Edit mode)
            Vector2 localPos = evt.localPosition;
            float noteWidth = resolvedStyle.width;
            bool willResize = resizeHandle != null && localPos.x > noteWidth - RESIZE_HANDLE_WIDTH;

            this.CapturePointer(evt.pointerId);

            // Register move/up on panel
            panel?.visualTree.RegisterCallback<PointerMoveEvent>(OnPanelPointerMove);
            panel?.visualTree.RegisterCallback<PointerUpEvent>(OnPanelPointerUp);

            // Schedule activation after hold duration
            holdSchedule?.Pause();
            holdSchedule = schedule.Execute(() => {
                if (!isHolding) return;

                isActivated = true;

                if (viewData.CurrentEditMode == EditMode.Erase)
                {
                    // Erase mode: delete after hold
                    OnNoteDeleted?.Invoke(NoteIndex);
                    CancelHold();
                }
                else
                {
                    // Edit mode: enable drag/resize after hold
                    if (willResize)
                    {
                        isResizing = true;
                        Debug.Log($"[NoteBlock] Hold complete - resizing note {NoteIndex}");
                    }
                    else
                    {
                        isDragging = true;
                        Debug.Log($"[NoteBlock] Hold complete - dragging note {NoteIndex}");
                    }

                    OnNoteSelected?.Invoke(NoteIndex);
                    AddToClassList("note-block-dragging");
                }
            }).StartingIn((long)(HOLD_DURATION * 1000));

            // Add holding visual feedback
            AddToClassList("note-block-holding");
        }

        private void CancelHold()
        {
            holdSchedule?.Pause();

            // Release pointer before resetting capturedPointerId
            if (capturedPointerId >= 0 && this.HasPointerCapture(capturedPointerId))
            {
                this.ReleasePointer(capturedPointerId);
            }

            isHolding = false;
            isActivated = false;
            isDragging = false;
            isResizing = false;
            capturedPointerId = -1;

            RemoveFromClassList("note-block-holding");
            RemoveFromClassList("note-block-dragging");

            // Unregister panel events
            panel?.visualTree.UnregisterCallback<PointerMoveEvent>(OnPanelPointerMove);
            panel?.visualTree.UnregisterCallback<PointerUpEvent>(OnPanelPointerUp);
        }

        private void OnPanelPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != capturedPointerId) return;
            OnPointerMove(evt);
        }

        private void OnPanelPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != capturedPointerId) return;

            // Unregister panel events
            panel?.visualTree.UnregisterCallback<PointerMoveEvent>(OnPanelPointerMove);
            panel?.visualTree.UnregisterCallback<PointerUpEvent>(OnPanelPointerUp);

            OnPointerUp(evt);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            // Only allow movement after activation (hold complete)
            if (!isActivated || (!isDragging && !isResizing))
            {
                return;
            }

            Vector3 delta = evt.position - dragStartPos;

            if (isDragging)
            {
                // Apply multiplier for World Space UI coordinate scaling
                float multiplier = 100f;

                // Calculate beat delta and snap to quantize grid
                float beatDelta = (delta.x * multiplier) / viewData.PixelsPerBeat;
                float newBeatTime = originalBeatTime + beatDelta;
                newBeatTime = viewData.Quantize(newBeatTime);  // Snap to grid (e.g., 1/16 note)
                newBeatTime = Mathf.Max(0, newBeatTime);

                // Calculate note delta and snap to whole notes
                float noteDelta = (delta.y * multiplier) / viewData.PixelsPerNote;
                int newNote = originalNote + Mathf.RoundToInt(noteDelta);  // Positive for World Space UI (Y increases upward)
                newNote = Mathf.Clamp(newNote, viewData.MinVisibleNote, viewData.MaxVisibleNote);

                // Convert back to pixel position
                float newX = viewData.BeatToPixelX(newBeatTime);
                float newY = viewData.NoteToPixelY(newNote);

                style.left = newX;
                style.top = newY;

                // Preview sound when note pitch changes
                if (newNote != lastPreviewedNote)
                {
                    lastPreviewedNote = newNote;
                    OnNotePreview?.Invoke(newNote);
                }

                evt.StopPropagation();
            }
            else if (isResizing)
            {
                // Apply multiplier for World Space UI
                float multiplier = 100f;

                // Calculate new duration and snap to grid
                float durationDelta = (delta.x * multiplier) / viewData.PixelsPerBeat;
                float newDuration = originalDuration + durationDelta;
                newDuration = viewData.Quantize(Mathf.Max(newDuration, MIN_DURATION));

                // Update visual width
                style.width = viewData.DurationToPixelWidth(newDuration);

                evt.StopPropagation();
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            // Cancel hold schedule
            holdSchedule?.Pause();

            // If not activated yet, just cancel (released before hold complete)
            if (!isActivated)
            {
                CancelHold();
                this.ReleasePointer(evt.pointerId);
                return;
            }

            if (!isDragging && !isResizing)
            {
                CancelHold();
                this.ReleasePointer(evt.pointerId);
                return;
            }

            Vector3 delta = evt.position - dragStartPos;
            float multiplier = 100f;

            if (isDragging)
            {
                // Finalize move with same calculation as OnPointerMove
                float beatDelta = (delta.x * multiplier) / viewData.PixelsPerBeat;
                float newBeatTime = originalBeatTime + beatDelta;
                newBeatTime = viewData.Quantize(newBeatTime);
                newBeatTime = Mathf.Max(0, newBeatTime);

                float noteDelta = (delta.y * multiplier) / viewData.PixelsPerNote;
                int newNote = originalNote + Mathf.RoundToInt(noteDelta);
                newNote = Mathf.Clamp(newNote, viewData.MinVisibleNote, viewData.MaxVisibleNote);

                if (!Mathf.Approximately(newBeatTime, NoteData.beatTime) || newNote != NoteData.note)
                {
                    OnNoteMoved?.Invoke(NoteIndex, newBeatTime, newNote);
                }
            }
            else if (isResizing)
            {
                // Finalize resize with same calculation as OnPointerMove
                float durationDelta = (delta.x * multiplier) / viewData.PixelsPerBeat;
                float newDuration = originalDuration + durationDelta;
                newDuration = viewData.Quantize(Mathf.Max(newDuration, MIN_DURATION));

                if (!Mathf.Approximately(newDuration, NoteData.duration))
                {
                    OnNoteResized?.Invoke(NoteIndex, newDuration);
                }
            }

            // Clean up
            isHolding = false;
            isActivated = false;
            isDragging = false;
            isResizing = false;
            capturedPointerId = -1;
            this.ReleasePointer(evt.pointerId);
            RemoveFromClassList("note-block-holding");
            RemoveFromClassList("note-block-dragging");
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            // Cancel operation if pointer capture is lost
            if (isHolding || isDragging || isResizing)
            {
                holdSchedule?.Pause();

                // Unregister panel events
                panel?.visualTree.UnregisterCallback<PointerMoveEvent>(OnPanelPointerMove);
                panel?.visualTree.UnregisterCallback<PointerUpEvent>(OnPanelPointerUp);

                UpdatePositionAndSize(); // Reset to original
                isHolding = false;
                isActivated = false;
                isDragging = false;
                isResizing = false;
                capturedPointerId = -1;
                RemoveFromClassList("note-block-holding");
                RemoveFromClassList("note-block-dragging");
            }
        }

        #endregion
    }
}
