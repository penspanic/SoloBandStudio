using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SoloBandStudio.Audio;
using SoloBandStudio.Core;
using SoloBandStudio.MIDI;
using SoloBandStudio.UI.Dialogs;

namespace SoloBandStudio.UI
{
    /// <summary>
    /// World Space UI for controlling LoopStation using UI Toolkit.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LoopStationUIView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LoopStation loopStation;
        [SerializeField] private BeatClock beatClock;
        [SerializeField] private Metronome metronome;

        [Header("BPM Settings")]
        [SerializeField] private float minBPM = 60f;
        [SerializeField] private float maxBPM = 180f;

        [Header("Auto Load")]
        [SerializeField] private string autoLoadPresetName = "CanonInD";

        // UI Elements
        private VisualElement root;
        private Button playBtn;
        private Button stopBtn;
        private Button recordBtn;
        private Slider bpmSlider;
        private Label bpmLabel;
        private Label barLabel;
        private List<VisualElement> beatIndicatorList = new List<VisualElement>();
        private DropdownField instrumentDropdown;
        private Button presetBtn;
        private ScrollView trackList;

        // Song Info UI
        private Label songNameLabel;
        private Button newBtn;
        private string currentSongName = "New Session";

        // Time Signature & Bars UI
        private Button beatsUpBtn;
        private Button beatsDownBtn;
        private Label timeSigLabel;
        private Button barsUpBtn;
        private Button barsDownBtn;
        private Label barsLabel;
        private Label loopLengthLabel;

        // Metronome UI
        private Button metronomeBtn;

        // Count-In UI
        private Button countInBtn;

        // File Operations UI
        private Button saveBtn;
        private Button loadBtn;
        private Button folderBtn;

        // Dialogs
        private SaveDialog saveDialog;
        private SelectionDialog loadDialog;
        private SelectionDialog presetDialog;

        // State
        private SongPreset[] availablePresets;
        private string[] streamingAssetsSongs; // MIDI files from StreamingAssets
        private List<IInstrument> registeredInstruments = new List<IInstrument>();
        private int selectedInstrumentIndex = 0;

        private void Start()
        {
            Debug.Log("[LoopStationUI] Start() called");

            if (loopStation == null)
                loopStation = FindFirstObjectByType<LoopStation>();

            Debug.Log($"[LoopStationUI] LoopStation: {(loopStation != null ? "found" : "NOT FOUND")}");

            if (beatClock == null && loopStation != null)
                beatClock = loopStation.BeatClock;

            Debug.Log($"[LoopStationUI] BeatClock: {(beatClock != null ? "found" : "NOT FOUND")}");

            if (metronome == null)
                metronome = FindFirstObjectByType<Metronome>();

            Debug.Log($"[LoopStationUI] Metronome: {(metronome != null ? "found" : "NOT FOUND")}");

            LoadPresetsFromResources();

            // Delay UI init to ensure UIDocument is ready
            StartCoroutine(InitializeUIDelayed());
        }

        private System.Collections.IEnumerator InitializeUIDelayed()
        {
            Debug.Log("[LoopStationUI] InitializeUIDelayed started");
            yield return null; // Wait one frame

            var uiDocument = GetComponent<UIDocument>();

            if (uiDocument == null)
            {
                Debug.LogError("[LoopStationUI] UIDocument component not found!");
                yield break;
            }

            root = uiDocument.rootVisualElement;

            if (root == null)
            {
                Debug.LogError("[LoopStationUI] rootVisualElement is null!");
                yield break;
            }

            QueryUIElements();

            SetupUI();
            SubscribeToEvents();
            RefreshUI();
            AutoLoadPreset();

            Debug.Log($"[LoopStationUI] Initialized - Instruments: {registeredInstruments.Count}, Presets: {availablePresets?.Length ?? 0}");
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();

            // Cleanup dialogs
            if (saveDialog != null)
            {
                saveDialog.OnSaveConfirmed -= OnSaveConfirmed;
                saveDialog.Destroy();
            }

            if (loadDialog != null)
            {
                loadDialog.OnItemSelected -= OnSavedSongSelected;
                loadDialog.OnItemDeleted -= OnSavedSongDeleted;
                loadDialog.Destroy();
            }

            if (presetDialog != null)
            {
                presetDialog.OnItemSelected -= OnPresetItemSelected;
                presetDialog.Destroy();
            }
        }

        private void Update()
        {
            UpdateBeatDisplay();
        }

        #region UI Setup

        private void QueryUIElements()
        {
            // Song Info
            songNameLabel = root.Q<Label>("song-name-label");
            newBtn = root.Q<Button>("new-btn");

            // Transport
            playBtn = root.Q<Button>("play-btn");
            stopBtn = root.Q<Button>("stop-btn");
            recordBtn = root.Q<Button>("record-btn");
            bpmSlider = root.Q<Slider>("bpm-slider");
            bpmLabel = root.Q<Label>("bpm-label");
            barLabel = root.Q<Label>("bar-label");
            instrumentDropdown = root.Q<DropdownField>("instrument-dropdown");
            presetBtn = root.Q<Button>("preset-btn");
            trackList = root.Q<ScrollView>("track-list");

            // Collect beat indicators
            beatIndicatorList.Clear();
            for (int i = 1; i <= 4; i++)
            {
                var indicator = root.Q<VisualElement>($"beat-{i}");
                if (indicator != null)
                    beatIndicatorList.Add(indicator);
            }

            // Time Signature & Bars controls
            beatsUpBtn = root.Q<Button>("beats-up-btn");
            beatsDownBtn = root.Q<Button>("beats-down-btn");
            timeSigLabel = root.Q<Label>("time-sig-label");
            barsUpBtn = root.Q<Button>("bars-up-btn");
            barsDownBtn = root.Q<Button>("bars-down-btn");
            barsLabel = root.Q<Label>("bars-label");
            loopLengthLabel = root.Q<Label>("loop-length-label");

            // File Operations
            saveBtn = root.Q<Button>("save-btn");
            loadBtn = root.Q<Button>("load-btn");
            folderBtn = root.Q<Button>("folder-btn");

            // Metronome
            metronomeBtn = root.Q<Button>("metronome-btn");

            // Count-In
            countInBtn = root.Q<Button>("countin-btn");
        }

        private void SetupUI()
        {
            // Song Info
            newBtn?.RegisterCallback<ClickEvent>(evt => OnNewClicked());
            SetButtonLabelsPickingMode(newBtn);

            // Transport buttons
            playBtn?.RegisterCallback<ClickEvent>(evt => OnPlayClicked());
            stopBtn?.RegisterCallback<ClickEvent>(evt => OnStopClicked());
            recordBtn?.RegisterCallback<ClickEvent>(evt => OnRecordClicked());

            // BPM slider
            if (bpmSlider != null)
            {
                bpmSlider.lowValue = minBPM;
                bpmSlider.highValue = maxBPM;
                bpmSlider.value = beatClock != null ? beatClock.BPM : 120f;
                bpmSlider.RegisterValueChangedCallback(evt => OnBPMChanged(evt.newValue));
            }

            // Instrument dropdown
            if (instrumentDropdown != null)
            {
                instrumentDropdown.RegisterValueChangedCallback(evt => {
                    selectedInstrumentIndex = instrumentDropdown.index;
                    Debug.Log($"[LoopStationUI] Instrument selected: {evt.newValue} (index: {selectedInstrumentIndex})");
                });
            }

            // Preset button
            presetBtn?.RegisterCallback<ClickEvent>(evt => presetDialog?.Show());
            SetButtonLabelsPickingMode(presetBtn);

            // Time Signature buttons
            beatsUpBtn?.RegisterCallback<ClickEvent>(evt => OnBeatsPerBarChanged(1));
            beatsDownBtn?.RegisterCallback<ClickEvent>(evt => OnBeatsPerBarChanged(-1));

            // Bars buttons
            barsUpBtn?.RegisterCallback<ClickEvent>(evt => OnTotalBarsChanged(1));
            barsDownBtn?.RegisterCallback<ClickEvent>(evt => OnTotalBarsChanged(-1));

            // Metronome toggle
            metronomeBtn?.RegisterCallback<ClickEvent>(evt => OnMetronomeToggleClicked());
            SetButtonLabelsPickingMode(metronomeBtn);

            // Count-In toggle
            countInBtn?.RegisterCallback<ClickEvent>(evt => OnCountInToggleClicked());
            SetButtonLabelsPickingMode(countInBtn);

            // File Operations buttons
            saveBtn?.RegisterCallback<ClickEvent>(evt => saveDialog?.Show());
            loadBtn?.RegisterCallback<ClickEvent>(evt => loadDialog?.Show());
            folderBtn?.RegisterCallback<ClickEvent>(evt => loopStation?.OpenMidiSaveFolder());

            // Fix picking mode for button labels
            SetButtonLabelsPickingMode(saveBtn);
            SetButtonLabelsPickingMode(loadBtn);
            SetButtonLabelsPickingMode(folderBtn);

            // Create dialogs
            CreateDialogs();
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

        private void CreateDialogs()
        {
            // Save Dialog
            saveDialog = new SaveDialog();
            saveDialog.Create(root);
            saveDialog.OnSaveConfirmed += OnSaveConfirmed;

            // Load Dialog
            loadDialog = new SelectionDialog();
            loadDialog.Title = "Load Song";
            loadDialog.EmptyText = "No saved songs";
            loadDialog.ShowDeleteButton = true;
            loadDialog.GetItemsFunc = GetSavedSongItems;
            loadDialog.Create(root);
            loadDialog.OnItemSelected += OnSavedSongSelected;
            loadDialog.OnItemDeleted += OnSavedSongDeleted;

            // Preset Dialog
            presetDialog = new SelectionDialog();
            presetDialog.Title = "Select Preset";
            presetDialog.EmptyText = "No presets available";
            presetDialog.ShowDeleteButton = false;
            presetDialog.GetItemsFunc = GetPresetItems;
            presetDialog.Create(root);
            presetDialog.OnItemSelected += OnPresetItemSelected;
        }

        private List<(string displayName, string value)> GetPresetItems()
        {
            var items = new List<(string, string)>();

            // Add ScriptableObject presets
            if (availablePresets != null)
            {
                foreach (var preset in availablePresets)
                {
                    if (preset != null)
                    {
                        items.Add((preset.SongName, $"preset:{preset.name}"));
                    }
                }
            }

            // Add StreamingAssets MIDI files
            if (streamingAssetsSongs != null)
            {
                foreach (var song in streamingAssetsSongs)
                {
                    items.Add((song, $"midi:{song}"));
                }
            }

            return items;
        }

        private void OnPresetItemSelected(int index, string value)
        {
            if (loopStation == null || string.IsNullOrEmpty(value)) return;

            if (value.StartsWith("preset:"))
            {
                string presetName = value.Substring(7);
                foreach (var preset in availablePresets)
                {
                    if (preset != null && preset.name == presetName)
                    {
                        loopStation.LoadPreset(preset);
                        SetSongName(preset.SongName);
                        RefreshTrackList();
                        UpdateBPMDisplay();
                        UpdateTimeSignatureDisplay();
                        Debug.Log($"[LoopStationUI] Loaded preset: {preset.SongName}");
                        return;
                    }
                }
            }
            else if (value.StartsWith("midi:"))
            {
                string songName = value.Substring(5);
                if (loopStation.LoadFromStreamingAssets(songName, autoPlay: false))
                {
                    SetSongName(songName);
                    RefreshTrackList();
                    UpdateBPMDisplay();
                    UpdateTimeSignatureDisplay();
                    Debug.Log($"[LoopStationUI] Loaded MIDI: {songName}");
                }
            }
        }

        private void LoadPresetsFromResources()
        {
            availablePresets = Resources.LoadAll<SongPreset>("Presets");
            streamingAssetsSongs = MidiFileManager.Instance.GetStreamingAssetsSongs();
            Debug.Log($"[LoopStationUI] Loaded {availablePresets.Length} presets, {streamingAssetsSongs.Length} StreamingAssets songs");
        }

        private void AutoLoadPreset()
        {
            if (string.IsNullOrEmpty(autoLoadPresetName)) return;
            if (loopStation == null) return;

            // Check ScriptableObject presets first
            if (availablePresets != null)
            {
                for (int i = 0; i < availablePresets.Length; i++)
                {
                    if (availablePresets[i] != null && availablePresets[i].name == autoLoadPresetName)
                    {
                        loopStation.LoadPreset(availablePresets[i]);
                        SetSongName(availablePresets[i].SongName);
                        RefreshTrackList();
                        UpdateBPMDisplay();
                        UpdateTimeSignatureDisplay();
                        Debug.Log($"[LoopStationUI] Auto-loaded preset: {autoLoadPresetName}");
                        return;
                    }
                }
            }

            // Check StreamingAssets MIDI files
            if (streamingAssetsSongs != null)
            {
                for (int i = 0; i < streamingAssetsSongs.Length; i++)
                {
                    if (streamingAssetsSongs[i] == autoLoadPresetName)
                    {
                        if (loopStation.LoadFromStreamingAssets(autoLoadPresetName, autoPlay: false))
                        {
                            SetSongName(autoLoadPresetName);
                            RefreshTrackList();
                            UpdateBPMDisplay();
                            UpdateTimeSignatureDisplay();
                            Debug.Log($"[LoopStationUI] Auto-loaded from StreamingAssets: {autoLoadPresetName}");
                            return;
                        }
                    }
                }
            }

            Debug.LogWarning($"[LoopStationUI] Auto-load preset not found: {autoLoadPresetName}");
        }

        private void SubscribeToEvents()
        {
            if (loopStation != null)
            {
                loopStation.OnPlayStarted += OnPlayStarted;
                loopStation.OnPlayStopped += OnPlayStopped;
                loopStation.OnRecordingStarted += OnRecordingStarted;
                loopStation.OnRecordingStopped += OnRecordingStopped;
                loopStation.OnTrackCreated += OnTrackCreated;
                loopStation.OnTrackRemoved += OnTrackRemoved;
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
                loopStation.OnTrackCreated -= OnTrackCreated;
                loopStation.OnTrackRemoved -= OnTrackRemoved;
            }

            if (beatClock != null)
                beatClock.OnCountInEnabledChanged -= OnCountInEnabledChanged;

            if (metronome != null)
                metronome.OnMuteChanged -= OnMetronomeMuteChanged;
        }

        private void OnCountInEnabledChanged(bool enabled) => UpdateCountInButton();
        private void OnMetronomeMuteChanged(bool muted) => UpdateMetronomeButton();

        #endregion

        #region Button Handlers

        private void OnNewClicked()
        {
            UIAudioManager.Instance?.PlayButtonClick();
            if (loopStation == null) return;

            loopStation.Stop();
            loopStation.ClearAllTracks();

            // Reset to defaults
            if (beatClock != null)
            {
                beatClock.BPM = 120f;
                beatClock.BeatsPerBar = 4;
                beatClock.TotalBars = 4;
            }

            SetSongName("New Session");
            RefreshUI();
            Debug.Log("[LoopStationUI] New session started");
        }

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
                    Debug.LogWarning("[LoopStationUI] No instrument selected");
                }
            }
        }

        private void OnBPMChanged(float value)
        {
            if (beatClock == null) return;
            beatClock.BPM = value;
            UpdateBPMDisplay();
        }

        private void OnBeatsPerBarChanged(int delta)
        {
            UIAudioManager.Instance?.PlayButtonClick();
            if (beatClock == null) return;
            beatClock.BeatsPerBar = Mathf.Clamp(beatClock.BeatsPerBar + delta, 2, 8);
            UpdateTimeSignatureDisplay();
            Debug.Log($"[LoopStationUI] Time signature changed to {beatClock.BeatsPerBar}/4");
        }

        private void OnTotalBarsChanged(int delta)
        {
            UIAudioManager.Instance?.PlayButtonClick();
            if (beatClock == null) return;
            beatClock.TotalBars = Mathf.Clamp(beatClock.TotalBars + delta, 1, 16);
            UpdateTimeSignatureDisplay();
            Debug.Log($"[LoopStationUI] Total bars changed to {beatClock.TotalBars}");
        }

        private void OnMetronomeToggleClicked()
        {
            UIAudioManager.Instance?.PlayButtonClick();
            if (metronome == null) return;

            metronome.IsMuted = !metronome.IsMuted;
            UpdateMetronomeButton();
            Debug.Log($"[LoopStationUI] Metronome: {(metronome.IsMuted ? "OFF" : "ON")}");
        }

        private void OnCountInToggleClicked()
        {
            UIAudioManager.Instance?.PlayButtonClick();
            if (beatClock == null) return;

            beatClock.EnableCountIn = !beatClock.EnableCountIn;
            UpdateCountInButton();
            Debug.Log($"[LoopStationUI] Count-In: {(beatClock.EnableCountIn ? "ON" : "OFF")}");
        }

        #endregion

        #region LoopStation Events

        private void OnPlayStarted() => UpdateTransportButtons();
        private void OnPlayStopped()
        {
            UpdateTransportButtons();
            ResetBeatIndicators();
        }
        private void OnRecordingStarted(LoopTrackData track) => UpdateTransportButtons();
        private void OnRecordingStopped(LoopTrackData track) => UpdateTransportButtons();
        private void OnTrackCreated(LoopTrackData track) => RefreshTrackList();
        private void OnTrackRemoved(LoopTrackData track) => RefreshTrackList();

        #endregion

        #region UI Updates

        private void RefreshUI()
        {
            UpdateSongNameDisplay();
            UpdateTransportButtons();
            UpdateBPMDisplay();
            UpdateTimeSignatureDisplay();
            UpdateMetronomeButton();
            UpdateCountInButton();
            RefreshInstrumentDropdown();
            RefreshTrackList();
        }

        private void SetSongName(string name)
        {
            currentSongName = name;
            UpdateSongNameDisplay();
        }

        private void UpdateSongNameDisplay()
        {
            if (songNameLabel != null)
            {
                songNameLabel.text = currentSongName;
            }
        }

        private void UpdateTransportButtons()
        {
            if (loopStation == null) return;

            // Play button
            if (playBtn != null)
            {
                playBtn.EnableInClassList("playing", loopStation.IsPlaying);
            }

            // Record button
            if (recordBtn != null)
            {
                recordBtn.EnableInClassList("recording", loopStation.IsRecording);
            }
        }

        private void UpdateBPMDisplay()
        {
            if (beatClock == null) return;

            if (bpmLabel != null)
                bpmLabel.text = $"{beatClock.BPM:F0} BPM";

            if (bpmSlider != null)
                bpmSlider.SetValueWithoutNotify(beatClock.BPM);
        }

        private void UpdateTimeSignatureDisplay()
        {
            if (beatClock == null) return;

            if (timeSigLabel != null)
                timeSigLabel.text = $"{beatClock.BeatsPerBar}/4";

            if (barsLabel != null)
                barsLabel.text = $"{beatClock.TotalBars}";

            if (loopLengthLabel != null)
            {
                int totalBeats = beatClock.BeatsPerBar * beatClock.TotalBars;
                loopLengthLabel.text = $"{totalBeats} beats";
            }

            // Update bar label
            if (barLabel != null)
            {
                int currentBar = loopStation != null && loopStation.IsPlaying
                    ? Mathf.FloorToInt(beatClock.CurrentBeatPosition / beatClock.BeatsPerBar) + 1
                    : 1;
                barLabel.text = $"Bar {currentBar}/{beatClock.TotalBars}";
            }

            // Update beat indicators dynamically
            UpdateBeatIndicators();
        }

        private void UpdateBeatIndicators()
        {
            if (beatClock == null || root == null) return;

            var beatIndicatorsContainer = root.Q<VisualElement>(className: "beat-indicators");
            if (beatIndicatorsContainer == null) return;

            // Clear existing indicators
            beatIndicatorsContainer.Clear();
            beatIndicatorList.Clear();

            // Create new indicators based on BeatsPerBar
            for (int i = 0; i < beatClock.BeatsPerBar; i++)
            {
                var indicator = new VisualElement();
                indicator.name = $"beat-{i + 1}";
                indicator.AddToClassList("beat-indicator");
                beatIndicatorsContainer.Add(indicator);
                beatIndicatorList.Add(indicator);
            }
        }

        private void UpdateBeatDisplay()
        {
            if (beatClock == null || loopStation == null || !loopStation.IsPlaying) return;

            int currentBeat = Mathf.FloorToInt(beatClock.CurrentBeatPosition) + 1;
            int currentBar = (currentBeat - 1) / beatClock.BeatsPerBar + 1;
            int beatInBar = (currentBeat - 1) % beatClock.BeatsPerBar + 1;

            if (barLabel != null)
                barLabel.text = $"Bar {currentBar}/{beatClock.TotalBars}";

            // Beat indicators
            for (int i = 0; i < beatIndicatorList.Count; i++)
            {
                beatIndicatorList[i].EnableInClassList("active", i == beatInBar - 1);
            }
        }

        private void ResetBeatIndicators()
        {
            foreach (var indicator in beatIndicatorList)
            {
                indicator.EnableInClassList("active", false);
            }

            if (barLabel != null && beatClock != null)
                barLabel.text = $"Bar 1/{beatClock.TotalBars}";
        }

        private void UpdateMetronomeButton()
        {
            if (metronomeBtn == null) return;

            bool isOn = metronome == null || !metronome.IsMuted;
            var label = metronomeBtn.Q<Label>();
            if (label != null)
            {
                label.text = isOn ? "ON" : "OFF";
            }
            metronomeBtn.EnableInClassList("muted", !isOn);
        }

        private void UpdateCountInButton()
        {
            if (countInBtn == null) return;

            bool isOn = beatClock != null && beatClock.EnableCountIn;
            var label = countInBtn.Q<Label>();
            if (label != null)
            {
                label.text = isOn ? "ON" : "OFF";
            }
            countInBtn.EnableInClassList("muted", !isOn);
        }

        #endregion

        #region Track List

        private void RefreshTrackList()
        {
            if (trackList == null || loopStation == null) return;

            trackList.Clear();

            if (loopStation.TrackCount == 0)
            {
                var emptyState = new VisualElement();
                emptyState.AddToClassList("empty-state");
                var label = new Label("No tracks recorded");
                label.AddToClassList("empty-state-text");
                emptyState.Add(label);
                trackList.Add(emptyState);
                return;
            }

            for (int i = 0; i < loopStation.TrackCount; i++)
            {
                var track = loopStation.GetTrack(i);
                if (track != null)
                {
                    var trackItem = CreateTrackItem(track, i);
                    trackList.Add(trackItem);
                }
            }
        }

        private VisualElement CreateTrackItem(LoopTrackData track, int index)
        {
            var item = new VisualElement();
            item.AddToClassList("track-item");

            // Track info
            var info = new VisualElement();
            info.AddToClassList("track-info");

            var nameLabel = new Label(track.TrackName);
            nameLabel.AddToClassList("track-name");
            info.Add(nameLabel);

            // Get instrument name from registered instruments, fallback to type
            string instrumentDisplay = track.InstrumentType.ToString();
            var instrument = loopStation?.GetInstrument(track.InstrumentId);
            if (instrument != null)
            {
                instrumentDisplay = instrument.InstrumentName;
            }

            var metaLabel = new Label($"{instrumentDisplay} - {track.EventCount} events");
            metaLabel.AddToClassList("track-meta");
            info.Add(metaLabel);

            item.Add(info);

            // Controls
            var controls = new VisualElement();
            controls.AddToClassList("track-controls");

            // Volume slider
            var volumeSlider = new Slider(0f, 1f);
            volumeSlider.AddToClassList("volume-slider");
            volumeSlider.value = track.Volume;
            volumeSlider.RegisterValueChangedCallback(evt => track.Volume = evt.newValue);
            controls.Add(volumeSlider);

            // Mute button
            var muteBtn = new Button(() => {
                loopStation.ToggleMute(index);
                RefreshTrackList();
            });
            muteBtn.AddToClassList("track-btn");
            muteBtn.AddToClassList("mute-btn");
            muteBtn.Add(new Label("M"));
            if (track.IsMuted) muteBtn.AddToClassList("active");
            controls.Add(muteBtn);

            // Solo button
            var soloBtn = new Button(() => {
                loopStation.ToggleSolo(index);
                RefreshTrackList();
            });
            soloBtn.AddToClassList("track-btn");
            soloBtn.AddToClassList("solo-btn");
            soloBtn.Add(new Label("S"));
            if (loopStation.SoloTrackIndex == index) soloBtn.AddToClassList("active");
            controls.Add(soloBtn);

            // Delete button
            var deleteBtn = new Button(() => {
                loopStation.RemoveTrack(index);
            });
            deleteBtn.AddToClassList("track-btn");
            deleteBtn.AddToClassList("delete-btn");
            deleteBtn.Add(new Label("X"));
            controls.Add(deleteBtn);

            item.Add(controls);

            // State classes
            if (track.IsMuted) item.AddToClassList("muted");
            if (loopStation.SoloTrackIndex == index) item.AddToClassList("solo");

            return item;
        }

        #endregion

        #region Instrument Selection

        private void RefreshInstrumentDropdown()
        {
            if (instrumentDropdown == null) return;

            registeredInstruments.Clear();
            var options = new List<string>();

            // Find instruments
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

        #region Public API

        public void ExternalTogglePlay() => OnPlayClicked();
        public void ExternalStop() => OnStopClicked();
        public void ExternalToggleRecord() => OnRecordClicked();
        public void ExternalSetBPM(float bpm)
        {
            if (beatClock != null)
            {
                beatClock.BPM = Mathf.Clamp(bpm, minBPM, maxBPM);
                UpdateBPMDisplay();
            }
        }

        #endregion

        #region MIDI File Operations

        private void OnSaveConfirmed(string filename)
        {
            if (loopStation == null) return;

            loopStation.SaveToMidi(filename);
            Debug.Log($"[LoopStationUI] Saved: {filename}");
        }

        private List<(string displayName, string value)> GetSavedSongItems()
        {
            var items = new List<(string, string)>();
            var files = loopStation?.GetSavedMidiFiles() ?? new string[0];

            foreach (var file in files)
            {
                items.Add((file, file));
            }

            return items;
        }

        private void OnSavedSongSelected(int index, string filename)
        {
            if (loopStation == null) return;

            bool success = loopStation.LoadFromMidi(filename, autoPlay: false);

            if (success)
            {
                SetSongName(filename);
                RefreshTrackList();
                UpdateBPMDisplay();
                UpdateTimeSignatureDisplay();
                Debug.Log($"[LoopStationUI] Loaded: {filename}");
            }
            else
            {
                Debug.LogError($"[LoopStationUI] Failed to load: {filename}");
            }
        }

        private void OnSavedSongDeleted(int index, string filename)
        {
            if (loopStation == null) return;

            loopStation.DeleteMidiFile(filename);
            Debug.Log($"[LoopStationUI] Deleted: {filename}");
        }

        #endregion
    }
}
