using UnityEngine;

namespace SoloBandStudio.Core
{
    /// <summary>
    /// Controls day/night cycle by rotating the directional light.
    /// Works with BK_EnvironmentManager which handles color gradients based on light direction.
    /// Reads time from TODManager.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        [Header("Light Reference")]
        [SerializeField] private Light directionalLight;

        [Header("Sun Path")]
        [Tooltip("Rotation axis for the sun (usually X for east-west movement)")]
        [SerializeField] private Vector3 sunRotationAxis = Vector3.right;

        [Tooltip("Base Y rotation offset (compass direction of sunrise)")]
        [SerializeField] private float sunYRotation = 0f;

        private TODManager todManager;

        private void Start()
        {
            todManager = TODManager.Instance;
            if (todManager == null)
            {
                Debug.LogError("[DayNightCycle] No TODManager found! Add TODManager to the scene.");
                enabled = false;
                return;
            }

            if (directionalLight == null)
            {
                directionalLight = FindDirectionalLight();
            }

            if (directionalLight == null)
            {
                Debug.LogError("[DayNightCycle] No directional light found!");
                enabled = false;
                return;
            }

            // Subscribe to time changes
            todManager.OnTimeChanged += OnTimeChanged;
            UpdateSunRotation(todManager.TimeOfDay);
        }

        private void OnDestroy()
        {
            if (todManager != null)
            {
                todManager.OnTimeChanged -= OnTimeChanged;
            }
        }

        private void OnTimeChanged(float timeOfDay)
        {
            UpdateSunRotation(timeOfDay);
        }

        private void UpdateSunRotation(float timeOfDay)
        {
            if (directionalLight == null) return;

            // Convert time to angle (0h = -90deg, 6h = 0deg, 12h = 90deg, 18h = 180deg, 24h = 270deg)
            float sunAngle = (timeOfDay - 6f) / 24f * 360f;

            Quaternion rotation = Quaternion.AngleAxis(sunYRotation, Vector3.up) *
                                  Quaternion.AngleAxis(sunAngle, sunRotationAxis);

            directionalLight.transform.rotation = rotation;
        }

        private Light FindDirectionalLight()
        {
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    return light;
                }
            }

            return null;
        }
    }
}
