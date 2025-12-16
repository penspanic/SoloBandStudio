using System;
using UnityEngine;
using UnityEngine.UIElements;
using SoloBandStudio.Audio;

namespace SoloBandStudio.UI.PianoRoll
{
    /// <summary>
    /// Generates and manages the timeline ruler (bar/beat markers).
    /// Syncs horizontal position with grid scroll.
    /// </summary>
    public class PianoRollTimeline
    {
        private readonly PianoRollLayout layout;
        private readonly PianoRollData data;
        private readonly BeatClock beatClock;

        /// <summary>
        /// Event fired when user clicks on timeline to seek.
        /// Parameter is the beat position clicked.
        /// </summary>
        public event Action<float> OnSeekRequested;

        public PianoRollTimeline(PianoRollLayout layout, PianoRollData data, BeatClock beatClock)
        {
            this.layout = layout;
            this.data = data;
            this.beatClock = beatClock;
        }

        /// <summary>
        /// Register click handler for timeline seeking.
        /// Call after layout is initialized.
        /// </summary>
        public void RegisterClickHandler()
        {
            layout.TimelineViewport?.RegisterCallback<PointerDownEvent>(OnTimelineClick);
        }

        public void UnregisterClickHandler()
        {
            layout.TimelineViewport?.UnregisterCallback<PointerDownEvent>(OnTimelineClick);
        }

        private void OnTimelineClick(PointerDownEvent evt)
        {
            if (beatClock == null) return;

            // Get click position relative to viewport
            float clickX = evt.localPosition.x;

            // Account for current scroll offset (timeline content is translated)
            float scrollX = 0;
            if (layout.GridScroll != null)
            {
                scrollX = layout.GridScroll.scrollOffset.x;
            }

            // Convert to beat position
            float beat = (clickX + scrollX) / data.PixelsPerBeat;

            // Clamp to valid range
            beat = Mathf.Clamp(beat, 0, beatClock.TotalBeats - 0.01f);

            // Fire event for external handling
            OnSeekRequested?.Invoke(beat);

            Debug.Log($"[PianoRoll] Timeline clicked - seeking to beat {beat:F2}");
        }

        public void Generate()
        {
            if (layout.TimelineContent == null || beatClock == null) return;

            layout.TimelineContent.Clear();

            int totalBeats = beatClock.BeatsPerBar * beatClock.TotalBars;
            float totalWidth = totalBeats * data.PixelsPerBeat;

            // Set content width
            layout.TimelineContent.style.width = totalWidth;
            layout.TimelineContent.style.left = 0;

            // Generate markers
            for (int beat = 0; beat < totalBeats; beat++)
            {
                int bar = beat / beatClock.BeatsPerBar + 1;
                bool isBar = beat % beatClock.BeatsPerBar == 0;

                var marker = new VisualElement();
                marker.AddToClassList("timeline-marker");
                if (isBar) marker.AddToClassList("timeline-bar-marker");

                float x = beat * data.PixelsPerBeat;
                marker.style.left = x;

                // Label for bar starts only
                if (isBar)
                {
                    var label = new Label($"{bar}");
                    label.AddToClassList("timeline-marker-label");
                    marker.Add(label);
                }

                layout.TimelineContent.Add(marker);
            }
        }

        /// <summary>
        /// Sync timeline position with grid scroll.
        /// Called directly from Update - no scheduling.
        /// </summary>
        public void SyncScroll(Vector2 scrollOffset)
        {
            if (layout.TimelineContent == null) return;

            // Move content by negative scroll X (translate instead of left for performance)
            layout.TimelineContent.style.translate = new Translate(-scrollOffset.x, 0);
        }
    }
}
