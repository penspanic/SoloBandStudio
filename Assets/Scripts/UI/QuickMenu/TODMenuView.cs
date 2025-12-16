using UnityEngine;
using UnityEngine.UIElements;
using SoloBandStudio.Core;

namespace SoloBandStudio.UI.QuickMenu
{
    /// <summary>
    /// Time of Day control view using UI Toolkit.
    /// Controls time, speed, and play/pause via TODManager.
    /// </summary>
    public class TODMenuView : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float minSecondsPerHour = 1f;
        [SerializeField] private float maxSecondsPerHour = 120f;

        // UI Elements
        private VisualElement root;
        private Label timeLabel;
        private Label periodLabel;
        private Slider timeSlider;
        private Label timeValue;
        private Slider speedSlider;
        private Label speedValue;
        private Button playPauseBtn;
        private Label playPauseLabel;
        private Button dawnBtn;
        private Button noonBtn;
        private Button duskBtn;
        private Button midnightBtn;

        private TODManager todManager;
        private bool isUpdatingUI;
        private bool isInitialized;

        /// <summary>
        /// Initialize with the instantiated UXML content.
        /// Called by QuickMenuController when tab is loaded.
        /// </summary>
        public void Initialize(VisualElement content)
        {
            todManager = TODManager.Instance;
            root = content;

            QueryElements();
            SetupCallbacks();
            RefreshUI();

            todManager.OnTimeChanged += OnTimeChanged;
            isInitialized = true;
        }

        private void OnDestroy()
        {
            if (todManager != null)
            {
                todManager.OnTimeChanged -= OnTimeChanged;
            }
        }

        private void QueryElements()
        {
            timeLabel = root.Q<Label>("time-label");
            periodLabel = root.Q<Label>("period-label");
            timeSlider = root.Q<Slider>("time-slider");
            timeValue = root.Q<Label>("time-value");
            speedSlider = root.Q<Slider>("speed-slider");
            speedValue = root.Q<Label>("speed-value");
            playPauseBtn = root.Q<Button>("play-pause-btn");
            playPauseLabel = root.Q<Label>("play-pause-label");
            dawnBtn = root.Q<Button>("dawn-btn");
            noonBtn = root.Q<Button>("noon-btn");
            duskBtn = root.Q<Button>("dusk-btn");
            midnightBtn = root.Q<Button>("midnight-btn");
        }

        private void SetupCallbacks()
        {
            timeSlider.lowValue = 0f;
            timeSlider.highValue = 24f;
            timeSlider.RegisterValueChangedCallback(OnTimeSliderChanged);

            speedSlider.lowValue = minSecondsPerHour;
            speedSlider.highValue = maxSecondsPerHour;
            speedSlider.RegisterValueChangedCallback(OnSpeedSliderChanged);

            playPauseBtn.RegisterCallback<ClickEvent>(evt => OnPlayPauseClicked());

            dawnBtn.RegisterCallback<ClickEvent>(evt => SetTimePreset(TimePeriod.Dawn));
            noonBtn.RegisterCallback<ClickEvent>(evt => SetTimePreset(TimePeriod.Noon));
            duskBtn.RegisterCallback<ClickEvent>(evt => SetTimePreset(TimePeriod.Dusk));
            midnightBtn.RegisterCallback<ClickEvent>(evt => SetTimePreset(TimePeriod.Midnight));
        }

        private bool needsTimeDisplayUpdate;

        private void Update()
        {
            // Just mark as dirty - actual update happens in LateUpdate
            if (!isInitialized || isUpdatingUI) return;
            needsTimeDisplayUpdate = true;
        }

        private void LateUpdate()
        {
            if (!isInitialized || isUpdatingUI || !needsTimeDisplayUpdate) return;
            needsTimeDisplayUpdate = false;
            UpdateTimeDisplaySafe();
        }

        private void OnTimeChanged(float time)
        {
            if (!isInitialized) return;
            needsTimeDisplayUpdate = true;
        }

        private void UpdateTimeDisplaySafe()
        {
            // Only update labels, not the slider (to avoid render conflicts)
            string timeStr = todManager.GetTimeString();
            timeLabel.text = timeStr;
            periodLabel.text = todManager.CurrentPeriod.ToString();
            timeValue.text = timeStr;
        }

        private void RefreshUI()
        {
            isUpdatingUI = true;

            timeSlider.value = todManager.TimeOfDay;
            speedSlider.value = todManager.SecondsPerHour;

            UpdateTimeDisplay();
            UpdateSpeedDisplay();
            UpdatePlayPauseButton();

            isUpdatingUI = false;
        }

        private void UpdateTimeDisplay()
        {
            string timeStr = todManager.GetTimeString();

            timeLabel.text = timeStr;
            periodLabel.text = todManager.CurrentPeriod.ToString();
            timeValue.text = timeStr;

            if (!isUpdatingUI)
            {
                isUpdatingUI = true;
                timeSlider.value = todManager.TimeOfDay;
                isUpdatingUI = false;
            }
        }

        private void UpdateSpeedDisplay()
        {
            float speed = todManager.SecondsPerHour;
            speedValue.text = speed >= 60
                ? $"{speed / 60f:F1} min/hour"
                : $"{speed:F0} sec/hour";
        }

        private void UpdatePlayPauseButton()
        {
            bool isPlaying = todManager.AutoProgress;
            playPauseLabel.text = isPlaying ? "||" : ">";
            playPauseBtn.EnableInClassList("paused", !isPlaying);
        }

        #region UI Callbacks

        private void OnTimeSliderChanged(ChangeEvent<float> evt)
        {
            if (isUpdatingUI) return;
            todManager.SetTime(evt.newValue);
        }

        private void OnSpeedSliderChanged(ChangeEvent<float> evt)
        {
            if (isUpdatingUI) return;
            todManager.SecondsPerHour = evt.newValue;
            UpdateSpeedDisplay();
        }

        private void OnPlayPauseClicked()
        {
            todManager.AutoProgress = !todManager.AutoProgress;
            UpdatePlayPauseButton();
        }

        private void SetTimePreset(TimePeriod period)
        {
            todManager.SetTimePeriod(period);
            RefreshUI();
        }

        #endregion
    }
}
