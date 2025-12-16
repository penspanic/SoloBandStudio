using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using SoloBandStudio.UI;

namespace SoloBandStudio.Common.SceneManagement
{
    /// <summary>
    /// Portal that triggers scene transition when interacted with or entered.
    /// Can be used with XR Interaction Toolkit or simple trigger collider.
    /// </summary>
    public class ScenePortal : MonoBehaviour
    {
        [Header("Target Scene")]
        [SerializeField] private string targetSceneName;
        [SerializeField] private int targetSceneBuildIndex = -1; // -1 = use name instead

        [Header("Arrival Point")]
        [Tooltip("Where the player spawns when arriving at this scene via portal")]
        [SerializeField] private Transform arrivalPoint;

        [Header("Trigger Settings")]
        [SerializeField] private bool useColliderTrigger = true;
        [SerializeField] private bool useXRInteraction = true;
        [SerializeField] private string playerTag = "Player";

        [Header("Cooldown")]
        [SerializeField] private float cooldownTime = 2f;
        [SerializeField] private float sceneLoadGracePeriod = 1.5f; // Ignore triggers after scene load

        [Header("Visual Feedback")]
        [SerializeField] private GameObject portalEffect;
        [SerializeField] private AudioSource portalSound;

        [Header("Confirmation")]
        [SerializeField] private bool requireConfirmation = true;
        [SerializeField] private string confirmationTitle = "Scene Transition";
        [SerializeField] private string confirmationMessage = "Do you want to move to {0}?";

        private XRSimpleInteractable interactable;
        private float lastTriggerTime = -999f;
        private float sceneLoadTime;

        public Vector3 ArrivalPosition => arrivalPoint != null ? arrivalPoint.position : transform.position;
        public Quaternion ArrivalRotation => arrivalPoint != null ? arrivalPoint.rotation : transform.rotation;

        private void Awake()
        {
            if (useXRInteraction)
            {
                interactable = GetComponent<XRSimpleInteractable>();
                if (interactable == null)
                {
                    interactable = gameObject.AddComponent<XRSimpleInteractable>();
                }
            }
        }

        private void Start()
        {
            // Record when this scene was loaded to prevent immediate re-triggering
            sceneLoadTime = Time.time;
        }

        private void OnEnable()
        {
            if (interactable != null)
            {
                interactable.selectEntered.AddListener(OnSelectEntered);
            }
        }

        private void OnDisable()
        {
            if (interactable != null)
            {
                interactable.selectEntered.RemoveListener(OnSelectEntered);
            }
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (!useXRInteraction) return;
            TriggerTransition();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!useColliderTrigger) return;

            // Check if it's the player (XR Origin typically has MainCamera or specific tag)
            if (other.CompareTag(playerTag) ||
                other.CompareTag("MainCamera") ||
                other.GetComponentInParent<Camera>() != null ||
                other.GetComponent<CharacterController>() != null)
            {
                TriggerTransition();
            }
        }

        private void TriggerTransition()
        {
            // Grace period after scene load - prevent immediate re-triggering
            if (Time.time - sceneLoadTime < sceneLoadGracePeriod)
            {
                Debug.Log("[ScenePortal] Ignoring trigger during grace period after scene load");
                return;
            }

            // Cooldown check
            if (Time.time - lastTriggerTime < cooldownTime)
            {
                return;
            }

            // Already transitioning check
            if (SceneTransitionManager.IsTransitioning)
            {
                return;
            }

            lastTriggerTime = Time.time;

            // Show confirmation dialog if required
            if (requireConfirmation)
            {
                string sceneName = !string.IsNullOrEmpty(targetSceneName) ? targetSceneName : $"Scene {targetSceneBuildIndex}";
                string message = string.Format(confirmationMessage, sceneName);

                VRMessageBox.Instance?.Show(
                    confirmationTitle,
                    message,
                    onConfirm: ExecuteTransition,
                    onCancel: null
                );
            }
            else
            {
                ExecuteTransition();
            }
        }

        private void ExecuteTransition()
        {
            // Visual/audio feedback
            if (portalEffect != null)
            {
                portalEffect.SetActive(true);
            }

            if (portalSound != null)
            {
                portalSound.Play();
            }

            // Trigger scene load
            if (targetSceneBuildIndex >= 0)
            {
                string scenePath = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(targetSceneBuildIndex);
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                SceneTransitionManager.LoadScene(sceneName);
            }
            else if (!string.IsNullOrEmpty(targetSceneName))
            {
                SceneTransitionManager.LoadScene(targetSceneName);
            }
            else
            {
                Debug.LogError("[ScenePortal] No target scene specified!");
            }

            Debug.Log($"[ScenePortal] Triggered transition to: {(targetSceneBuildIndex >= 0 ? targetSceneBuildIndex.ToString() : targetSceneName)}");
        }

        /// <summary>
        /// Public method to trigger transition (can be called from UI buttons, etc.)
        /// </summary>
        public void Activate()
        {
            TriggerTransition();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(targetSceneName) && targetSceneBuildIndex < 0)
            {
                Debug.LogWarning($"[ScenePortal] {gameObject.name}: No target scene configured!");
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);

            var collider = GetComponent<Collider>();
            if (collider is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (collider is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
            }
        }
#endif
    }
}
