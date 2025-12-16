using System;
using UnityEngine;

namespace SoloBandStudio.Core
{
    /// <summary>
    /// Central Time of Day Manager. Singleton that manages game time.
    /// Other components subscribe to this for time-based behavior.
    /// </summary>
    public class TODManager : MonoBehaviour
    {
        public static TODManager Instance { get; private set; }

        [Header("Time Settings")]
        [Tooltip("Current time of day (0-24 hours)")]
        [Range(0f, 24f)]
        [SerializeField] private float timeOfDay = 12f;

        [Tooltip("How many real seconds = 1 in-game hour")]
        [SerializeField] private float secondsPerHour = 60f;

        [Tooltip("Auto-advance time")]
        [SerializeField] private bool autoProgress = true;

        [Header("Non-linear Time")]
        [Tooltip("Enable faster nights and slower days")]
        [SerializeField] private bool useNonLinearTime = true;

        [Tooltip("Speed multiplier during daytime (6-18h). Lower = slower days")]
        [Range(0.1f, 2f)]
        [SerializeField] private float dayTimeMultiplier = 0.5f;

        [Tooltip("Speed multiplier during nighttime (18-6h). Higher = faster nights")]
        [Range(1f, 10f)]
        [SerializeField] private float nightTimeMultiplier = 4f;

        [Header("Debug")]
        [SerializeField] private bool debugLog = false;

        private float previousHour = -1f;

        // Events
        public event Action<float> OnTimeChanged;          // (timeOfDay)
        public event Action<int> OnHourChanged;            // (hour 0-23)
        public event Action<TimePeriod> OnPeriodChanged;   // (period)

        private TimePeriod currentPeriod;

        // Public properties
        public float TimeOfDay
        {
            get => timeOfDay;
            set
            {
                timeOfDay = Mathf.Repeat(value, 24f);
                OnTimeChanged?.Invoke(timeOfDay);
            }
        }

        public float NormalizedTime => timeOfDay / 24f;
        public bool IsDay => timeOfDay >= 6f && timeOfDay < 18f;
        public bool IsNight => !IsDay;
        public float SecondsPerHour
        {
            get => secondsPerHour;
            set => secondsPerHour = Mathf.Max(0.1f, value);
        }
        public bool AutoProgress
        {
            get => autoProgress;
            set => autoProgress = value;
        }
        public TimePeriod CurrentPeriod => currentPeriod;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[TODManager] Multiple instances detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            currentPeriod = CalculatePeriod();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (autoProgress)
            {
                AdvanceTime(Time.deltaTime);
            }

            CheckHourChange();
            CheckPeriodChange();
        }

        /// <summary>
        /// Advance time by deltaTime seconds.
        /// </summary>
        public void AdvanceTime(float deltaTime)
        {
            float hoursToAdd = deltaTime / secondsPerHour;

            if (useNonLinearTime)
            {
                hoursToAdd *= GetTimeMultiplier();
            }

            timeOfDay = Mathf.Repeat(timeOfDay + hoursToAdd, 24f);
            OnTimeChanged?.Invoke(timeOfDay);
        }

        /// <summary>
        /// Get the current time speed multiplier based on time of day.
        /// </summary>
        private float GetTimeMultiplier()
        {
            // Dawn transition (5-7): night speed -> day speed
            if (timeOfDay >= 5f && timeOfDay < 7f)
            {
                float t = (timeOfDay - 5f) / 2f;
                return Mathf.Lerp(nightTimeMultiplier, dayTimeMultiplier, t);
            }
            // Daytime (7-17): slow
            else if (timeOfDay >= 7f && timeOfDay < 17f)
            {
                return dayTimeMultiplier;
            }
            // Dusk transition (17-19): day speed -> night speed
            else if (timeOfDay >= 17f && timeOfDay < 19f)
            {
                float t = (timeOfDay - 17f) / 2f;
                return Mathf.Lerp(dayTimeMultiplier, nightTimeMultiplier, t);
            }
            // Nighttime (19-5): fast
            else
            {
                return nightTimeMultiplier;
            }
        }

        /// <summary>
        /// Set time instantly (0-24).
        /// </summary>
        public void SetTime(float hour)
        {
            timeOfDay = Mathf.Repeat(hour, 24f);
            OnTimeChanged?.Invoke(timeOfDay);
            CheckHourChange();
            CheckPeriodChange();
        }

        /// <summary>
        /// Set time by period name.
        /// </summary>
        public void SetTimePeriod(TimePeriod period)
        {
            switch (period)
            {
                case TimePeriod.Dawn: timeOfDay = 6f; break;
                case TimePeriod.Morning: timeOfDay = 9f; break;
                case TimePeriod.Noon: timeOfDay = 12f; break;
                case TimePeriod.Afternoon: timeOfDay = 15f; break;
                case TimePeriod.Dusk: timeOfDay = 18f; break;
                case TimePeriod.Evening: timeOfDay = 20f; break;
                case TimePeriod.Midnight: timeOfDay = 0f; break;
            }
            OnTimeChanged?.Invoke(timeOfDay);
            CheckPeriodChange();
        }

        /// <summary>
        /// Check if current time is within a range (handles overnight ranges like 22-6).
        /// </summary>
        public bool IsTimeInRange(float startHour, float endHour)
        {
            if (startHour <= endHour)
            {
                // Normal range (e.g., 8-18)
                return timeOfDay >= startHour && timeOfDay < endHour;
            }
            else
            {
                // Overnight range (e.g., 22-6)
                return timeOfDay >= startHour || timeOfDay < endHour;
            }
        }

        /// <summary>
        /// Get how far into a time range we are (0-1). Useful for fading.
        /// </summary>
        public float GetRangeProgress(float startHour, float endHour)
        {
            if (!IsTimeInRange(startHour, endHour)) return 0f;

            float duration;
            float elapsed;

            if (startHour <= endHour)
            {
                duration = endHour - startHour;
                elapsed = timeOfDay - startHour;
            }
            else
            {
                // Overnight
                duration = (24f - startHour) + endHour;
                elapsed = timeOfDay >= startHour ? timeOfDay - startHour : (24f - startHour) + timeOfDay;
            }

            return Mathf.Clamp01(elapsed / duration);
        }

        private TimePeriod CalculatePeriod()
        {
            if (timeOfDay >= 5f && timeOfDay < 7f) return TimePeriod.Dawn;
            if (timeOfDay >= 7f && timeOfDay < 11f) return TimePeriod.Morning;
            if (timeOfDay >= 11f && timeOfDay < 14f) return TimePeriod.Noon;
            if (timeOfDay >= 14f && timeOfDay < 17f) return TimePeriod.Afternoon;
            if (timeOfDay >= 17f && timeOfDay < 19f) return TimePeriod.Dusk;
            if (timeOfDay >= 19f && timeOfDay < 22f) return TimePeriod.Evening;
            return TimePeriod.Midnight;
        }

        private void CheckHourChange()
        {
            int currentHour = Mathf.FloorToInt(timeOfDay);
            if (currentHour != previousHour)
            {
                if (debugLog)
                {
                    Debug.Log($"[TODManager] Hour: {currentHour}:00 ({CalculatePeriod()})");
                }
                previousHour = currentHour;
                OnHourChanged?.Invoke(currentHour);
            }
        }

        private void CheckPeriodChange()
        {
            var newPeriod = CalculatePeriod();
            if (newPeriod != currentPeriod)
            {
                currentPeriod = newPeriod;
                OnPeriodChanged?.Invoke(currentPeriod);

                if (debugLog)
                {
                    Debug.Log($"[TODManager] Period changed to: {currentPeriod}");
                }
            }
        }

        /// <summary>
        /// Get formatted time string (HH:MM).
        /// </summary>
        public string GetTimeString()
        {
            int hours = Mathf.FloorToInt(timeOfDay);
            int minutes = Mathf.FloorToInt((timeOfDay - hours) * 60f);
            return $"{hours:D2}:{minutes:D2}";
        }
    }

    public enum TimePeriod
    {
        Dawn,       // 5-7
        Morning,    // 7-11
        Noon,       // 11-14
        Afternoon,  // 14-17
        Dusk,       // 17-19
        Evening,    // 19-22
        Midnight    // 22-5
    }
}
