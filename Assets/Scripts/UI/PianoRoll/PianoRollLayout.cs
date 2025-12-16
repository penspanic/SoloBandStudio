using UnityEngine;
using UnityEngine.UIElements;

namespace SoloBandStudio.UI.PianoRoll
{
    /// <summary>
    /// Holds references to all UI elements in the Piano Roll.
    /// Queries elements from the visual tree and provides access to them.
    /// </summary>
    public class PianoRollLayout
    {
        // Toolbar
        public DropdownField TrackDropdown { get; private set; }
        public DropdownField QuantizeDropdown { get; private set; }
        public Button ZoomInBtn { get; private set; }
        public Button ZoomOutBtn { get; private set; }
        public Label ZoomLabel { get; private set; }
        public Button EditModeBtn { get; private set; }
        public Button EraseModeBtn { get; private set; }
        public Label DurationLabel { get; private set; }

        // Timeline
        public VisualElement TimelineViewport { get; private set; }
        public VisualElement TimelineContent { get; private set; }

        // Piano Keys
        public VisualElement PianoViewport { get; private set; }
        public VisualElement PianoContent { get; private set; }

        // Grid
        public ScrollView GridScroll { get; private set; }
        public VisualElement GridContainer { get; private set; }
        public VisualElement GridBackground { get; private set; }
        public VisualElement NotesContainer { get; private set; }
        public VisualElement Playhead { get; private set; }

        // Status Bar
        public Label StatusLabel { get; private set; }
        public Label PositionLabel { get; private set; }

        public VisualElement Root { get; private set; }
        public bool IsValid { get; private set; }

        public PianoRollLayout(VisualElement root)
        {
            Root = root;
            QueryElements();
        }

        private void QueryElements()
        {
            if (Root == null)
            {
                Debug.LogError("[PianoRollLayout] Root is null!");
                IsValid = false;
                return;
            }

            // Toolbar
            TrackDropdown = Root.Q<DropdownField>("track-dropdown");
            QuantizeDropdown = Root.Q<DropdownField>("quantize-dropdown");
            ZoomInBtn = Root.Q<Button>("zoom-in-btn");
            ZoomOutBtn = Root.Q<Button>("zoom-out-btn");
            ZoomLabel = Root.Q<Label>("zoom-label");
            EditModeBtn = Root.Q<Button>("edit-mode-btn");
            EraseModeBtn = Root.Q<Button>("erase-mode-btn");
            DurationLabel = Root.Q<Label>("duration-label");

            // Timeline
            TimelineViewport = Root.Q<VisualElement>("timeline-viewport");
            TimelineContent = Root.Q<VisualElement>("timeline-content");

            // Piano Keys
            PianoViewport = Root.Q<VisualElement>("piano-viewport");
            PianoContent = Root.Q<VisualElement>("piano-content");

            // Grid
            GridScroll = Root.Q<ScrollView>("grid-scroll");
            GridContainer = Root.Q<VisualElement>("grid-container");
            GridBackground = Root.Q<VisualElement>("grid-background");
            NotesContainer = Root.Q<VisualElement>("notes-container");
            Playhead = Root.Q<VisualElement>("playhead");

            // Status Bar
            StatusLabel = Root.Q<Label>("status-label");
            PositionLabel = Root.Q<Label>("position-label");

            // Validate critical elements
            IsValid = GridScroll != null && GridContainer != null && GridBackground != null;

            if (!IsValid)
            {
                Debug.LogError("[PianoRollLayout] Failed to query critical elements!");
            }
        }
    }
}
