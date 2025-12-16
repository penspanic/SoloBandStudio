using System;
using UnityEngine;
using UnityEngine.UIElements;
using SoloBandStudio.Audio;

namespace SoloBandStudio.UI
{
    /// <summary>
    /// Reusable transport controls component (Play/Stop).
    /// Can be embedded in other UI or used standalone as floating controls.
    /// </summary>
    public class TransportControlsView
    {
        // UI Elements
        private VisualElement root;
        private Button playBtn;
        private Button stopBtn;

        // References
        private LoopStation loopStation;

        // Events for external handling
        public event Action OnPlayClicked;
        public event Action OnStopClicked;

        // Properties
        public VisualElement Root => root;
        public bool IsPlaying => loopStation?.IsPlaying ?? false;

        /// <summary>
        /// Create transport controls from a template.
        /// </summary>
        public static TransportControlsView CreateFromTemplate(VisualTreeAsset template, LoopStation loopStation)
        {
            var view = new TransportControlsView();
            view.root = template.Instantiate();
            view.loopStation = loopStation;
            view.QueryElements();
            view.SetupCallbacks();
            view.SubscribeToEvents();
            return view;
        }

        /// <summary>
        /// Create transport controls from existing UI elements.
        /// </summary>
        public static TransportControlsView CreateFromExisting(VisualElement container, LoopStation loopStation)
        {
            var view = new TransportControlsView();
            view.root = container;
            view.loopStation = loopStation;
            view.QueryElements();
            view.SetupCallbacks();
            view.SubscribeToEvents();
            return view;
        }

        /// <summary>
        /// Create transport controls programmatically.
        /// </summary>
        public static TransportControlsView CreateProgrammatic(LoopStation loopStation, bool compact = false)
        {
            var view = new TransportControlsView();
            view.loopStation = loopStation;
            view.CreateUI(compact);
            view.SetupCallbacks();
            view.SubscribeToEvents();
            return view;
        }

        private void CreateUI(bool compact)
        {
            root = new VisualElement();
            root.name = "transport-root";
            root.AddToClassList("transport-controls");
            if (compact) root.AddToClassList("compact");

            // Play button
            playBtn = new Button();
            playBtn.name = "play-btn";
            playBtn.AddToClassList("transport-btn");
            playBtn.AddToClassList("play-btn");
            var playLabel = new Label("PLAY");
            playLabel.AddToClassList("btn-label");
            playBtn.Add(playLabel);
            root.Add(playBtn);

            // Stop button
            stopBtn = new Button();
            stopBtn.name = "stop-btn";
            stopBtn.AddToClassList("transport-btn");
            stopBtn.AddToClassList("stop-btn");
            var stopLabel = new Label("STOP");
            stopLabel.AddToClassList("btn-label");
            stopBtn.Add(stopLabel);
            root.Add(stopBtn);
        }

        private void QueryElements()
        {
            playBtn = root.Q<Button>("play-btn");
            stopBtn = root.Q<Button>("stop-btn");
        }

        private void SetupCallbacks()
        {
            playBtn?.RegisterCallback<ClickEvent>(evt => HandlePlayClick());
            stopBtn?.RegisterCallback<ClickEvent>(evt => HandleStopClick());
        }

        private void SubscribeToEvents()
        {
            if (loopStation == null) return;
            loopStation.OnPlayStarted += UpdateButtonStates;
            loopStation.OnPlayStopped += UpdateButtonStates;
        }

        public void UnsubscribeFromEvents()
        {
            if (loopStation == null) return;
            loopStation.OnPlayStarted -= UpdateButtonStates;
            loopStation.OnPlayStopped -= UpdateButtonStates;
        }

        private void HandlePlayClick()
        {
            UIAudioManager.Instance?.PlayButtonClick();
            loopStation?.TogglePlay();
            OnPlayClicked?.Invoke();
        }

        private void HandleStopClick()
        {
            UIAudioManager.Instance?.PlayButtonClick();
            loopStation?.Stop();
            OnStopClicked?.Invoke();
        }

        public void UpdateButtonStates()
        {
            if (loopStation == null) return;
            playBtn?.EnableInClassList("playing", loopStation.IsPlaying);
        }

        /// <summary>
        /// Set the loop station reference (for late binding).
        /// </summary>
        public void SetLoopStation(LoopStation station)
        {
            UnsubscribeFromEvents();
            loopStation = station;
            SubscribeToEvents();
            UpdateButtonStates();
        }

        /// <summary>
        /// Add this control to a parent element.
        /// </summary>
        public void AddTo(VisualElement parent)
        {
            parent?.Add(root);
        }

        /// <summary>
        /// Remove from parent.
        /// </summary>
        public void RemoveFromParent()
        {
            root?.RemoveFromHierarchy();
        }

        /// <summary>
        /// Cleanup.
        /// </summary>
        public void Destroy()
        {
            UnsubscribeFromEvents();
            RemoveFromParent();
        }
    }
}
