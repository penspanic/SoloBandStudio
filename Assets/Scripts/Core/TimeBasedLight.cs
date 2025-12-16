using UnityEngine;

namespace SoloBandStudio.Core
{
    /// <summary>
    /// Controls a light (and optionally emission material) based on time of day.
    /// Turns on during specified hours with smooth fade transitions.
    /// </summary>
    public class TimeBasedLight : MonoBehaviour
    {
        [Header("Active Hours")]
        [Tooltip("Hour when light turns on (0-24)")]
        [Range(0f, 24f)]
        [SerializeField] private float turnOnHour = 18f;

        [Tooltip("Hour when light turns off (0-24)")]
        [Range(0f, 24f)]
        [SerializeField] private float turnOffHour = 6f;

        [Header("Fade Settings")]
        [Tooltip("Time in hours to fade in/out")]
        [SerializeField] private float fadeDuration = 0.5f;

        [Header("Light Settings")]
        [Tooltip("Light component to control. Auto-finds if not set.")]
        [SerializeField] private Light targetLight;

        [Tooltip("Maximum light intensity when fully on")]
        [SerializeField] private float maxIntensity = 1f;

        [Header("Emission Settings")]
        [Tooltip("Renderer with emission material. Optional.")]
        [SerializeField] private Renderer emissionRenderer;

        [Tooltip("Emission color when on")]
        [SerializeField] private Color emissionColor = new Color(1f, 0.9f, 0.7f);

        [Tooltip("Maximum emission intensity")]
        [SerializeField] private float maxEmissionIntensity = 2f;

        [Header("GameObject Toggle")]
        [Tooltip("Optional: GameObject to enable/disable with light")]
        [SerializeField] private GameObject toggleObject;

        private TODManager todManager;
        private Material emissionMaterial;
        private float currentIntensity;
        private bool isInitialized;

        private void Start()
        {
            todManager = TODManager.Instance;
            if (todManager == null)
            {
                Debug.LogWarning($"[TimeBasedLight] {name}: No TODManager found!");
                enabled = false;
                return;
            }

            // Auto-find light if not assigned
            if (targetLight == null)
            {
                targetLight = GetComponentInChildren<Light>();
            }

            // Create material instance for emission
            if (emissionRenderer != null)
            {
                emissionMaterial = emissionRenderer.material; // Creates instance
            }

            isInitialized = true;

            // Initial state
            UpdateLight(todManager.TimeOfDay);
        }

        private void Update()
        {
            if (!isInitialized || todManager == null) return;

            UpdateLight(todManager.TimeOfDay);
        }

        private void UpdateLight(float timeOfDay)
        {
            float targetIntensity = CalculateIntensity(timeOfDay);

            // Smooth transition (fadeDuration is in game-hours, convert to real seconds)
            float fadeRealSeconds = fadeDuration * todManager.SecondsPerHour;
            float fadeSpeed = fadeRealSeconds > 0 ? Time.deltaTime / fadeRealSeconds : 1f;
            currentIntensity = Mathf.MoveTowards(currentIntensity, targetIntensity, fadeSpeed);

            // Apply to light
            if (targetLight != null)
            {
                targetLight.intensity = currentIntensity * maxIntensity;
                targetLight.enabled = currentIntensity > 0.01f;
            }

            // Apply to emission
            if (emissionMaterial != null)
            {
                if (currentIntensity > 0.01f)
                {
                    emissionMaterial.EnableKeyword("_EMISSION");
                    emissionMaterial.SetColor("_EmissionColor", emissionColor * (currentIntensity * maxEmissionIntensity));
                }
                else
                {
                    emissionMaterial.SetColor("_EmissionColor", Color.black);
                }
            }

            // Toggle object
            if (toggleObject != null)
            {
                toggleObject.SetActive(currentIntensity > 0.01f);
            }
        }

        private float CalculateIntensity(float timeOfDay)
        {
            bool isActiveTime = todManager.IsTimeInRange(turnOnHour, turnOffHour);

            if (!isActiveTime)
            {
                // Check if we're in fade-out zone (just after turnOffHour)
                float fadeOutEnd = turnOffHour + fadeDuration;
                if (todManager.IsTimeInRange(turnOffHour, Mathf.Repeat(fadeOutEnd, 24f)))
                {
                    float elapsed = timeOfDay >= turnOffHour ? timeOfDay - turnOffHour : timeOfDay + (24f - turnOffHour);
                    return 1f - Mathf.Clamp01(elapsed / fadeDuration);
                }
                return 0f;
            }
            else
            {
                // Check if we're in fade-in zone (just after turnOnHour)
                float fadeInEnd = turnOnHour + fadeDuration;
                if (todManager.IsTimeInRange(turnOnHour, Mathf.Repeat(fadeInEnd, 24f)))
                {
                    float elapsed = timeOfDay >= turnOnHour ? timeOfDay - turnOnHour : timeOfDay + (24f - turnOnHour);
                    return Mathf.Clamp01(elapsed / fadeDuration);
                }
                return 1f;
            }
        }

        private void OnDestroy()
        {
            // Clean up material instance
            if (emissionMaterial != null)
            {
                Destroy(emissionMaterial);
            }
        }

        /// <summary>
        /// Set active hours at runtime.
        /// </summary>
        public void SetActiveHours(float onHour, float offHour)
        {
            turnOnHour = Mathf.Repeat(onHour, 24f);
            turnOffHour = Mathf.Repeat(offHour, 24f);
        }

        /// <summary>
        /// Force light on/off (ignores time).
        /// </summary>
        public void SetOverride(bool? forceOn)
        {
            if (!forceOn.HasValue)
            {
                // Resume normal behavior
                enabled = true;
            }
            else
            {
                currentIntensity = forceOn.Value ? 1f : 0f;
                UpdateLight(todManager?.TimeOfDay ?? 12f);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Show preview in editor
            if (targetLight != null && !Application.isPlaying)
            {
                bool wouldBeOn = false;
                float previewTime = 12f; // Default to noon

                var todManagerInScene = FindFirstObjectByType<TODManager>();
                if (todManagerInScene != null)
                {
                    previewTime = todManagerInScene.TimeOfDay;
                }

                if (turnOnHour <= turnOffHour)
                {
                    wouldBeOn = previewTime >= turnOnHour && previewTime < turnOffHour;
                }
                else
                {
                    wouldBeOn = previewTime >= turnOnHour || previewTime < turnOffHour;
                }

                targetLight.enabled = wouldBeOn;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }
#endif
    }
}
