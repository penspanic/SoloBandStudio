using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SoloBandStudio.Audio;
using SoloBandStudio.Core;

namespace SoloBandStudio.UI.QuickMenu
{
    /// <summary>
    /// LoopStation control view for QuickMenu.
    /// Controls play/stop, record, tempo, and time signature.
    /// </summary>
    public class LoopStationMenuView : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float minBPM = 60f;
        [SerializeField] private float maxBPM = 180f;

        // References (auto-found)
        private LoopStation loopStation;
        private BeatClock beatClock;
        private Metronome metronome;

        // UI Elements
        private VisualElement root;
        private VisualElement playIndicator;
        private VisualElement recordIndicator;
        private Label playStatusLabel;
        private Label recordStatusLabel;
        private Button playBtn;
        private Button stopBtn;
        private Button recordBtn;
        private Label playLabel;
        private Label recordLabel;
        private DropdownField instrumentDropdown;
        private Slider bpmSlider;
        private Label bpmValue;
        private Button beatsUpBtn;
        private Button beatsDownBtn;
        private Label beatsValue;
        private Button barsUpBtn;
        private Button barsDownBtn;
        private Label barsValue;
        private Label loopInfoLabel;
        private Button quantizeCycleBtn;
        private Label quantizeCycleLabel;
        private Button countInBtn;
        private Label countInLabel;
        private Button metronomeBtn;
        private Label metronomeLabel;

        // Quantize options: (label, value) - 0 means Off
        private static readonly (string label, float value)[] quantizeOptions = {
            ("1/32", 0.125f),
            ("1/16", 0.25f),
            ("1/8", 0.5f),
            ("1/4", 1.0f),
            ("Off", 0f)
        };
        private int currentQuantizeIndex = 1; // Default: 1/16

        // State
        private List<IInstrument> registeredInstruments = new List<IInstrument>();
        private int selectedInstrumentIndex = 0;
        private bool isInitialized;
        private bool isUpdatingUI;

        /// <summary>
        /// Initialize with the instantiated UXML content.
        /// Called by QuickMenuController when tab is loaded.
        /// </summary>
        public void Initialize(VisualElement content)
        {
            root = content;

            // Find references
            if (loopStation == null)
                loopStation = FindFirstObjectByType<LoopStation>();

            if (beatClock == null && loopStation != null)
                beatClock = loopStation.BeatClock;

            if (metronome == null)
                metronome = FindFirstObjectByType<Metronome>();

            QueryElements();
            SetupCallbacks();
            RefreshInstrumentDropdown();
            SubscribeToEvents();
            RefreshUI();

            isInitialized = true;
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void Update()
        {
            if (!isInitialized) return;
            UpdateStatusDisplay();
        }

        #region Setup

        private void QueryElements()
        {
            // Status
            playIndicator = root.Q<VisualElement>("play-indicator");
            recordIndicator = root.Q<VisualElement>("record-indicator");
            playStatusLabel = root.Q<Label>("play-status-label");
            recordStatusLabel = root.Q<Label>("record-status-label");

            // Transport
            playBtn = root.Q<Button>("play-btn");
            stopBtn = root.Q<Button>("stop-btn");
            recordBtn = root.Q<Button>("record-btn");
            playLabel = root.Q<Label>("play-label");
            recordLabel = root.Q<Label>("record-label");

            // Instrument
            instrumentDropdown = root.Q<DropdownField>("instrument-dropdown");

            // BPM
            bpmSlider = root.Q<Slider>("bpm-slider");
            bpmValue = root.Q<Label>("bpm-value");

            // Time Signature
            beatsUpBtn = root.Q<Button>("beats-up-btn");
            beatsDownBtn = root.Q<Button>("beats-down-btn");
            beatsValue = root.Q<Label>("beats-value");
            barsUpBtn = root.Q<Button>("bars-up-btn");
            barsDownBtn = root.Q<Button>("bars-down-btn");
            barsValue = root.Q<Label>("bars-value");

            // Loop Info
            loopInfoLabel = root.Q<Label>("loop-info-label");

            // Quantize
            quantizeCycleBtn = root.Q<Button>("quantize-cycle-btn");
            quantizeCycleLabel = root.Q<Label>("quantize-cycle-label");

            // Count-In
            countInBtn = root.Q<Button>("countin-btn");
            countInLabel = root.Q<Label>("countin-label");

            // Metronome
            metronomeBtn = root.Q<Button>("metronome-btn");
            metronomeLabel = root.Q<Label>("metronome-label");
        }

        private void SetupCallbacks()
        {
            // Transport buttons
            playBtn?.RegisterCallback<ClickEvent>(evt => OnPlayClicked());
            stopBtn?.RegisterCallback<ClickEvent>(evt => OnStopClicked());
            recordBtn?.RegisterCallback<ClickEvent>(evt => OnRecordClicked());

            // Fix picking mode for button labels
            SetButtonLabelsPickingMode(playBtn);
            SetButtonLabelsPickingMode(stopBtn);
            SetButtonLabelsPickingMode(recordBtn);

            // Instrument dropdown
            if (instrumentDropdown != null)
            {
                instrumentDropdown.RegisterValueChangedCallback(evt => {
                    selectedInstrumentIndex = instrumentDropdown.index;
                });
            }

            // BPM slider
            if (bpmSlider != null)
            {
                bpmSlider.lowValue = minBPM;
                bpmSlider.highValue = maxBPM;
                bpmSlider.RegisterValueChangedCallback(evt => OnBPMChanged(evt.newValue));
            }

            // Time signature spinners
            beatsUpBtn?.RegisterCallback<ClickEvent>(evt => OnBeatsPerBarChanged(1));
            beatsDownBtn?.RegisterCallback<ClickEvent>(evt => OnBeatsPerBarChanged(-1));
            barsUpBtn?.RegisterCallback<ClickEvent>(evt => OnTotalBarsChanged(1));
            barsDownBtn?.RegisterCallback<ClickEvent>(evt => OnTotalBarsChanged(-1));

            SetButtonLabelsPickingMode(beatsUpBtn);
            SetButtonLabelsPickingMode(beatsDownBtn);
            SetButtonLabelsPickingMode(barsUpBtn);
            SetButtonLabelsPickingMode(barsDownBtn);

            // Quantize cycle
            quantizeCycleBtn?.RegisterCallback<ClickEvent>(evt => OnQuantizeCycleClicked());
            SetButtonLabelsPickingMode(quantizeCycleBtn);

            // Count-In toggle
            countInBtn?.RegisterCallback<ClickEvent>(evt => OnCountInToggleClicked());
            SetButtonLabelsPickingMode(countInBtn);

            // Metronome toggle
            metronomeBtn?.RegisterCallback<ClickEvent>(evt => OnMetronomeToggleClicked());
            SetButtonLabelsPickingMode(metronomeBtn);
        }

        private void SetButtonLabelsPickingMode(Button button)
        {
            if (button == null) return;
            var label = button.Q<Label>();
            if (label != null)
            {
                label.pickingMode = PickingMode.Ignore;
            }
        }

        private void SubscribeToEvents()
        {
            if (loopStation != null)
            {
                loopStation.OnPlayStarted += OnPlayStarted;
                loopStation.OnPlayStopped += OnPlayStopped;
                loopStation.OnRecordingStarted += OnRecordingStarted;
                loopStation.OnRecordingStopped += OnRecordingStopped;
            }

            if (beatClock != null)
                beatClock.OnCountInEnabledChanged += OnCountInEnabledChanged;

            if (metronome != null)
                metronome.OnMuteChanged += OnMetronomeMuteChanged;
        }

        private void UnsubscribeFromEvents()
        {
            if (loopStation != null)
            {
                loopStation.OnPlayStarted -= OnPlayStarted;
                loopStation.OnPlayStopped -= OnPlayStopped;
                loopStation.OnRecordingStarted -= OnRecordingStarted;
                loopStation.OnRecordingStopped -= OnRecordingStopped;
            }

            if (beatClock != null)
                beatClock.OnCountInEnabledChanged -= OnCountInEnabledChanged;

            if (metronome != null)
                metronome.OnMuteChanged -= OnMetronomeMuteChanged;
        }

        private void OnCountInEnabledChanged(bool enabled) => UpdateCountInDisplay();
        private void OnMetronomeMuteChanged(bool muted) => UpdateMetronomeDisplay();

        #endregion

        #region Button Handlers

        private void OnPlayClicked()
        {
            UIAudioManager.Instance?.PlayButtonClick();
            loopStation?.TogglePlay();
        }

        private void OnStopClicked()
        {
            UIAudioManager.Instance?.PlayButtonClick();
            loopStation?.Stop();
        }

        private void OnRecordClicked()
        {
            UIAudioManager.Instance?.PlayButtonClick();
            if (loopStation == null) return;

            if (loopStation.IsRecording)
            {
                loopStation.StopRecording();
            }
            else
            {
                var instrument = GetSelectedInstrument();
                if (instrument != null)
                {
                    loopStation.StartRecording(instrument);
                }
                else
                {
                    UIAudioManager.Instance?.PlayError();
                    Debug.LogWarning("[LoopStationMenuView] No instrument selected");
                }
            }
        }

        private void OnBPMChanged(float value)
        {
            if (isUpdatingUI || beatClock == null) return;
            beatClock.BPM = value;
            UpdateBPMDisplay();
        }

        private void OnBeatsPerBarChanged(int delta)
        {
            UIAudioManager.Instance?.PlayButtonClick();
            if (beatClock == null) return;
            beatClock.BeatsPerBar = Mathf.Clamp(beatClock.BeatsPerBar + delta, 2, 8);
            UpdateTimeSignatureDisplay();
        }

        private void OnTotalBarsChanged(int delta)
        {
            UIAudioManager.Instance?.PlayButtonClick();
            if (beatClock == null) return;
            beatClock.TotalBars = Mathf.Clamp(beatClock.TotalBars + delta, 1, 16);
            UpdateTimeSignatureDisplay();
        }

        private void OnQuantizeCycleClicked()
        {
            UIAudioManager.Instance?.PlayButtonClick();
            if (loopStation == null) return;

            // Cycle to next option
            currentQuantizeIndex = (currentQuantizeIndex + 1) % quantizeOptions.Length;
            ApplyQuantizeOption();
            UpdateQuantizeDisplay();
        }

        private void ApplyQuantizeOption()
        {
            if (loopStation == null) return;

            var option = quantizeOptions[currentQuantizeIndex];
            if (option.value <= 0f)
            {
                loopStation.AutoQuantize = false;
            }
            else
            {
                loopStation.AutoQuantize = true;
                loopStation.QuantizeValue = option.value;
            }
        }

        private void OnCountInToggleClicked()
        {
            UIAudioManager.Instance?.PlayButtonClick();
            if (beatClock == null) return;

            beatClock.EnableCountIn = !beatClock.EnableCountIn;
            UpdateCountInDisplay();
            Debug.Log($"[LoopStationMenuView] Count-In: {(beatClock.EnableCountIn ? "ON" : "OFF")}");
        }

        private void OnMetronomeToggleClicked()
        {
            UIAudioManager.Instance?.PlayButtonClick();
            if (metronome == null) return;

            metronome.IsMuted = !metronome.IsMuted;
            UpdateMetronomeDisplay();
            Debug.Log($"[LoopStationMenuView] Metronome: {(metronome.IsMuted ? "OFF" : "ON")}");
        }

        #endregion

        #region LoopStation Events

        private void OnPlayStarted() => UpdateTransportButtons();
        private void OnPlayStopped() => UpdateTransportButtons();
        private void OnRecordingStarted(LoopTrackData track) => UpdateTransportButtons();
        private void OnRecordingStopped(LoopTrackData track) => UpdateTransportButtons();

        #endregion

        #region UI Updates

        private void RefreshUI()
        {
            isUpdatingUI = true;

            UpdateTransportButtons();
            UpdateBPMDisplay();
            UpdateTimeSignatureDisplay();
            UpdateStatusDisplay();
            UpdateQuantizeDisplay();
            UpdateCountInDisplay();
            UpdateMetronomeDisplay();

            isUpdatingUI = false;
        }

        private void UpdateStatusDisplay()
        {
            if (loopStation == null) return;

            // Play status
            bool isPlaying = loopStation.IsPlaying;
            playIndicator?.EnableInClassList("active", isPlaying);
            if (playStatusLabel != null)
                playStatusLabel.text = isPlaying ? "Playing" : "Stopped";

            // Record status
            bool isRecording = loopStation.IsRecording;
            recordIndicator?.EnableInClassList("active", isRecording);
            recordIndicator?.EnableInClassList("recording", isRecording);
            if (recordStatusLabel != null)
                recordStatusLabel.text = isRecording ? "Recording" : "Idle";
        }

        private void UpdateTransportButtons()
        {
            if (loopStation == null) return;

            bool isPlaying = loopStation.IsPlaying;
            bool isRecording = loopStation.IsRecording;

            // Play button
            playBtn?.EnableInClassList("active", isPlaying);
            if (playLabel != null)
                playLabel.text = isPlaying ? "||" : "Play";

            // Record button
            recordBtn?.EnableInClassList("active", isRecording);
            if (recordLabel != null)
                recordLabel.text = isRecording ? "Stop" : "Rec";
        }

        private void UpdateBPMDisplay()
        {
            if (beatClock == null) return;

            if (bpmValue != null)
                bpmValue.text = $"{beatClock.BPM:F0} BPM";

            if (bpmSlider != null && !isUpdatingUI)
            {
                isUpdatingUI = true;
                bpmSlider.SetValueWithoutNotify(beatClock.BPM);
                isUpdatingUI = false;
            }
        }

        private void UpdateTimeSignatureDisplay()
        {
            if (beatClock == null) return;

            if (beatsValue != null)
                beatsValue.text = beatClock.BeatsPerBar.ToString();

            if (barsValue != null)
                barsValue.text = beatClock.TotalBars.ToString();

            if (loopInfoLabel != null)
            {
                int totalBeats = beatClock.BeatsPerBar * beatClock.TotalBars;
                loopInfoLabel.text = $"{totalBeats} beats / loop";
            }
        }

        private void UpdateQuantizeDisplay()
        {
            if (loopStation == null) return;

            // Sync index with current loopStation state
            SyncQuantizeIndex();

            var option = quantizeOptions[currentQuantizeIndex];
            bool isActive = option.value > 0f;

            quantizeCycleBtn?.EnableInClassList("active", isActive);
            if (quantizeCycleLabel != null)
                quantizeCycleLabel.text = option.label;
        }

        private void UpdateCountInDisplay()
        {
            if (beatClock == null) return;

            bool isOn = beatClock.EnableCountIn;
            countInBtn?.EnableInClassList("active", isOn);
            if (countInLabel != null)
                countInLabel.text = isOn ? "ON" : "OFF";
        }

        private void UpdateMetronomeDisplay()
        {
            bool isOn = metronome == null || !metronome.IsMuted;
            metronomeBtn?.EnableInClassList("active", isOn);
            if (metronomeLabel != null)
                metronomeLabel.text = isOn ? "ON" : "OFF";
        }

        private void SyncQuantizeIndex()
        {
            if (loopStation == null) return;

            if (!loopStation.AutoQuantize)
            {
                // Find "Off" option
                for (int i = 0; i < quantizeOptions.Length; i++)
                {
                    if (quantizeOptions[i].value <= 0f)
                    {
                        currentQuantizeIndex = i;
                        return;
                    }
                }
            }
            else
            {
                // Find matching quantize value
                float targetValue = loopStation.QuantizeValue;
                for (int i = 0; i < quantizeOptions.Length; i++)
                {
                    if (Mathf.Approximately(quantizeOptions[i].value, targetValue))
                    {
                        currentQuantizeIndex = i;
                        return;
                    }
                }
                // Default to first option if no match
                currentQuantizeIndex = 0;
            }
        }

        #endregion

        #region Instrument Selection

        private void RefreshInstrumentDropdown()
        {
            if (instrumentDropdown == null) return;

            registeredInstruments.Clear();
            var options = new List<string>();

            // Find all instruments
            var pianos = FindObjectsByType<Instruments.Keyboard.Keyboard>(FindObjectsSortMode.None);
            foreach (var piano in pianos)
            {
                registeredInstruments.Add(piano);
                options.Add(piano.InstrumentName);
            }

            var drums = FindObjectsByType<Instruments.Drum.Drum>(FindObjectsSortMode.None);
            foreach (var drum in drums)
            {
                registeredInstruments.Add(drum);
                options.Add(drum.InstrumentName);
            }

            if (options.Count == 0)
            {
                options.Add("No instruments");
            }

            instrumentDropdown.choices = options;
            if (options.Count > 0)
                instrumentDropdown.index = 0;
        }

        private IInstrument GetSelectedInstrument()
        {
            if (registeredInstruments.Count == 0) return null;
            if (selectedInstrumentIndex >= registeredInstruments.Count) return null;
            return registeredInstruments[selectedInstrumentIndex];
        }

        #endregion
    }
}
