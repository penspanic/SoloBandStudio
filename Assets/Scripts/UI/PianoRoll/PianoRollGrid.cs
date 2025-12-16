using System;
using UnityEngine;
using UnityEngine.UIElements;
using SoloBandStudio.Audio;

namespace SoloBandStudio.UI.PianoRoll
{
    /// <summary>
    /// Handles grid background generation and scroll synchronization.
    /// This is the core scroll controller - Timeline and PianoKeys sync to this.
    /// </summary>
    public class PianoRollGrid
    {
        private readonly PianoRollLayout layout;
        private readonly PianoRollData data;
        private readonly BeatClock beatClock;

        private VisualElement gridLinesContainer;
        private bool scrollDirty;
        private Vector2 pendingScrollOffset;
        private Vector2 pendingWheelDelta;
        private bool hasWheelEvent;

        public event Action<Vector2> OnScrollChanged;

        public float GridWidth { get; private set; }
        public float GridHeight { get; private set; }
        public bool HasPendingScrollUpdate => scrollDirty;
        public Vector2 PendingScrollOffset => pendingScrollOffset;

        private const float SCROLL_SPEED = 20f;

        public PianoRollGrid(PianoRollLayout layout, PianoRollData data, BeatClock beatClock)
        {
            this.layout = layout;
            this.data = data;
            this.beatClock = beatClock;
        }

        /// <summary>
        /// Subscribe to ScrollView's scroller events for safe scroll synchronization.
        /// Call this after UI is initialized.
        /// </summary>
        public void SubscribeToScrollEvents()
        {
            if (layout.GridScroll == null) return;

            layout.GridScroll.verticalScroller.valueChanged += OnVerticalScrollChanged;
            layout.GridScroll.horizontalScroller.valueChanged += OnHorizontalScrollChanged;

            // Intercept wheel events to prevent render conflicts from XR UI system
            layout.GridScroll.RegisterCallback<WheelEvent>(OnWheelEvent, TrickleDown.TrickleDown);
        }

        public void UnsubscribeFromScrollEvents()
        {
            if (layout.GridScroll == null) return;

            layout.GridScroll.verticalScroller.valueChanged -= OnVerticalScrollChanged;
            layout.GridScroll.horizontalScroller.valueChanged -= OnHorizontalScrollChanged;
            layout.GridScroll.UnregisterCallback<WheelEvent>(OnWheelEvent, TrickleDown.TrickleDown);
        }

        private void OnWheelEvent(WheelEvent evt)
        {
            // Stop the event from propagating to ScrollView's default handler
            // which causes render conflicts when called from XR UI system
            evt.StopImmediatePropagation();

            // Accumulate wheel delta to be processed in LateUpdate
            // evt.delta is Vector3, convert to Vector2 (x = horizontal, y = vertical)
            pendingWheelDelta += new Vector2(evt.delta.x, evt.delta.y) * SCROLL_SPEED;
            hasWheelEvent = true;
        }

        private void OnVerticalScrollChanged(float value)
        {
            MarkScrollDirty();
        }

        private void OnHorizontalScrollChanged(float value)
        {
            MarkScrollDirty();
        }

        private void MarkScrollDirty()
        {
            if (layout.GridScroll == null) return;
            scrollDirty = true;
            pendingScrollOffset = layout.GridScroll.scrollOffset;
        }

        /// <summary>
        /// Call this in LateUpdate to process pending scroll changes safely.
        /// </summary>
        public void ProcessPendingScrollUpdate()
        {
            // Process deferred wheel events
            if (hasWheelEvent && layout.GridScroll != null)
            {
                hasWheelEvent = false;
                var currentOffset = layout.GridScroll.scrollOffset;
                var newOffset = new Vector2(
                    currentOffset.x + pendingWheelDelta.x,
                    currentOffset.y + pendingWheelDelta.y
                );
                pendingWheelDelta = Vector2.zero;
                layout.GridScroll.scrollOffset = newOffset;
                // This will trigger scroller valueChanged events, which will set scrollDirty
            }

            if (!scrollDirty) return;
            scrollDirty = false;
            OnScrollChanged?.Invoke(pendingScrollOffset);
        }

        // Maximum grid width to prevent UI Toolkit rendering issues with very long songs
        private const float MAX_GRID_WIDTH = 16000f;

        public void Generate()
        {
            if (layout.GridBackground == null || beatClock == null) return;

            layout.GridBackground.Clear();

            int totalBeats = beatClock.BeatsPerBar * beatClock.TotalBars;
            float rawGridWidth = totalBeats * data.PixelsPerBeat;

            // Auto-adjust zoom if grid would be too wide
            if (rawGridWidth > MAX_GRID_WIDTH)
            {
                float requiredZoom = MAX_GRID_WIDTH / (totalBeats * 80f); // 80 = base pixels per beat
                if (data.ZoomLevel > requiredZoom)
                {
                    data.SetZoom(requiredZoom);
                    Debug.LogWarning($"[PianoRoll] Long song detected ({totalBeats} beats). Auto-zoom to {data.ZoomLevel * 100:F0}%");
                }
            }

            GridWidth = totalBeats * data.PixelsPerBeat;
            GridHeight = data.GridHeight;

            // Set container sizes
            layout.GridContainer.style.width = GridWidth;
            layout.GridContainer.style.height = GridHeight;
            layout.GridBackground.style.width = GridWidth;
            layout.GridBackground.style.height = GridHeight;
            layout.GridBackground.pickingMode = PickingMode.Ignore;
            layout.NotesContainer.style.width = GridWidth;
            layout.NotesContainer.style.height = GridHeight;
            layout.NotesContainer.pickingMode = PickingMode.Ignore;

            // Generate row backgrounds using flex layout
            for (int note = data.MaxVisibleNote; note >= data.MinVisibleNote; note--)
            {
                var row = new VisualElement();
                row.AddToClassList("grid-row");
                row.AddToClassList(PianoRollData.IsBlackKey(note) ? "grid-row-black" : "grid-row-white");
                layout.GridBackground.Add(row);
            }

            // Create grid lines container
            gridLinesContainer = new VisualElement();
            gridLinesContainer.AddToClassList("grid-lines");
            gridLinesContainer.style.width = GridWidth;
            gridLinesContainer.style.height = GridHeight;
            gridLinesContainer.pickingMode = PickingMode.Ignore;

            // Generate vertical lines
            GenerateGridLines(totalBeats);

            layout.GridBackground.Add(gridLinesContainer);
        }

        private void GenerateGridLines(int totalBeats)
        {
            for (int beat = 0; beat <= totalBeats; beat++)
            {
                var line = new VisualElement();
                line.AddToClassList("grid-line-beat");

                bool isBar = beat % beatClock.BeatsPerBar == 0;
                if (isBar) line.AddToClassList("grid-line-bar");

                float x = beat * data.PixelsPerBeat;
                line.style.left = x;

                gridLinesContainer.Add(line);

                // Subdivision lines
                if (data.QuantizeValue < 1.0f && beat < totalBeats)
                {
                    float subdivisions = 1.0f / data.QuantizeValue;
                    for (int sub = 1; sub < subdivisions; sub++)
                    {
                        var subLine = new VisualElement();
                        subLine.AddToClassList("grid-line-beat");
                        subLine.AddToClassList("grid-line-subdivision");

                        float subX = x + (sub * data.QuantizeValue * data.PixelsPerBeat);
                        subLine.style.left = subX;

                        gridLinesContainer.Add(subLine);
                    }
                }
            }
        }

        public void UpdatePlayhead(float currentBeat, bool isPlaying)
        {
            if (layout.Playhead == null) return;

            float x = isPlaying ? data.BeatToPixelX(currentBeat) : 0;
            layout.Playhead.style.left = x;
            layout.Playhead.style.display = DisplayStyle.Flex;
        }
    }
}
