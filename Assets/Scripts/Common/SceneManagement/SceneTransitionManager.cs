using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;
using SoloBandStudio.Common.Rendering;

namespace SoloBandStudio.Common.SceneManagement
{
    /// <summary>
    /// Manages scene transitions with VR-friendly fade effects.
    /// Uses ScreenFader (URP Render Pass based) for fading.
    /// No scene setup required - automatically creates singleton.
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        [Header("Fade Settings")]
        [SerializeField] private float fadeDuration = 1.2f;
        [SerializeField] private Color fadeColor = Color.black;

        private static SceneTransitionManager instance;
        private bool isTransitioning;

        public static SceneTransitionManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<SceneTransitionManager>();

                    if (instance == null)
                    {
                        var go = new GameObject("SceneTransitionManager");
                        instance = go.AddComponent<SceneTransitionManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        public static bool IsTransitioning => instance != null && instance.isTransitioning;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            // Set initial fade color
            ScreenFader.SetFadeColor(fadeColor);
        }

        /// <summary>
        /// Load a scene with fade transition.
        /// </summary>
        public static void LoadScene(string sceneName)
        {
            // Ensure instance exists
            var _ = Instance;

            if (instance.isTransitioning)
            {
                Debug.LogWarning("[SceneTransition] Already transitioning");
                return;
            }

            instance.StartCoroutine(instance.TransitionToScene(sceneName));
        }

        /// <summary>
        /// Load a scene by build index with fade transition.
        /// </summary>
        public static void LoadScene(int buildIndex)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(buildIndex);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            LoadScene(sceneName);
        }

        private IEnumerator TransitionToScene(string sceneName)
        {
            isTransitioning = true;
            Debug.Log($"[SceneTransition] Starting transition to: {sceneName}");

            // Stop any playing audio systems gracefully
            StopAudioSystems();

            // Fade out (to black)
            yield return ScreenFader.FadeOut(fadeDuration, fadeColor);

            // Small delay while fully black
            yield return new WaitForSecondsRealtime(0.1f);

            // Load scene
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            asyncLoad.allowSceneActivation = true;

            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            // Small delay after scene load for initialization
            yield return new WaitForSecondsRealtime(0.1f);

            // Recenter XR Origin after scene load
            RecenterXROrigin();

            // Small delay after recenter
            yield return new WaitForSecondsRealtime(0.1f);

            // Fade in (from black)
            yield return ScreenFader.FadeIn(fadeDuration);

            isTransitioning = false;
            Debug.Log($"[SceneTransition] Completed transition to: {sceneName}");
        }

        private void StopAudioSystems()
        {
            // Stop LoopStation if present
            var loopStation = FindFirstObjectByType<SoloBandStudio.Audio.LoopStation>();
            if (loopStation != null && loopStation.IsPlaying)
            {
                loopStation.Stop();
            }
        }

        private void RecenterXROrigin()
        {
            var xrOrigin = FindFirstObjectByType<XROrigin>();
            if (xrOrigin == null)
            {
                Debug.LogWarning("[SceneTransition] No XROrigin found in scene");
                return;
            }

            Camera camera = xrOrigin.Camera;
            if (camera == null)
            {
                Debug.LogWarning("[SceneTransition] No Camera found in XROrigin");
                return;
            }

            // Find ScenePortal in the arrival scene to get spawn position
            var scenePortal = FindFirstObjectByType<ScenePortal>();

            Vector3 targetPosition;
            Quaternion targetRotation;

            if (scenePortal != null)
            {
                targetPosition = scenePortal.ArrivalPosition;
                targetRotation = scenePortal.ArrivalRotation;
                Debug.Log($"[SceneTransition] Found ScenePortal, spawning at {targetPosition}");
            }
            else
            {
                targetPosition = xrOrigin.transform.position;
                targetRotation = xrOrigin.transform.rotation;
                Debug.Log($"[SceneTransition] No ScenePortal found, using XR Origin position {targetPosition}");
            }

            // Get current camera forward direction (horizontal only)
            Vector3 cameraForward = camera.transform.forward;
            cameraForward.y = 0;
            cameraForward.Normalize();

            // Get target forward direction
            Vector3 targetForward = targetRotation * Vector3.forward;
            targetForward.y = 0;
            targetForward.Normalize();

            // Calculate rotation offset between camera and target
            float rotationOffset = Vector3.SignedAngle(cameraForward, targetForward, Vector3.up);

            // Apply rotation to XR Origin
            xrOrigin.transform.rotation = Quaternion.Euler(0, xrOrigin.transform.eulerAngles.y + rotationOffset, 0);

            // Get camera's local offset from XR Origin (HMD tracking offset) after rotation
            Vector3 cameraLocalOffset = xrOrigin.transform.InverseTransformPoint(camera.transform.position);
            cameraLocalOffset.y = 0;

            // Calculate new XR Origin position so camera ends up at target
            Vector3 newOriginPosition = targetPosition - xrOrigin.transform.TransformVector(cameraLocalOffset);
            newOriginPosition.y = targetPosition.y;

            xrOrigin.transform.position = newOriginPosition;

            Debug.Log($"[SceneTransition] XR Origin recentered to {newOriginPosition}, rotation {xrOrigin.transform.eulerAngles.y}, Camera at {camera.transform.position}");
        }

        /// <summary>
        /// Manually trigger fade out (useful for custom transitions).
        /// </summary>
        public static Coroutine FadeOut()
        {
            return ScreenFader.FadeOut(Instance.fadeDuration);
        }

        /// <summary>
        /// Manually trigger fade in (useful for custom transitions).
        /// </summary>
        public static Coroutine FadeIn()
        {
            return ScreenFader.FadeIn(Instance.fadeDuration);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
