using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using SoloBandStudio.UI;
using SoloBandStudio.Common.Rendering;

namespace SoloBandStudio.XR
{
    /// <summary>
    /// A portal that teleports the player to another location when entered.
    /// Supports specific destination, random destination, or sequential cycling.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TeleportPortal : MonoBehaviour
    {
        public enum DestinationMode
        {
            Specific,   // Teleport to a specific portal
            Random,     // Teleport to a random portal in the network
            Sequential  // Cycle through portals in order
        }

        [Header("Portal Identity")]
        [SerializeField] private string portalName;
        [SerializeField] private bool canBeDestination = true;

        [Header("Destination")]
        [SerializeField] private DestinationMode destinationMode = DestinationMode.Random;
        [SerializeField] private TeleportPortal specificDestination;
        [SerializeField] private TeleportPortal[] sequentialDestinations;

        [Header("Destination Point")]
        [Tooltip("Where the player arrives when teleporting TO this portal. If null, uses this transform.")]
        [SerializeField] private Transform destinationPoint;

        [Header("Trigger Settings")]
        [SerializeField] private float activationCooldown = 1f;
        [SerializeField] private bool requiresInteraction = false;

        [Header("Visual Effects")]
        [SerializeField] private GameObject portalActiveEffect;
        [SerializeField] private GameObject teleportEffect;
        [SerializeField] private Color portalColor = Color.cyan;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip enterSound;
        [SerializeField] private AudioClip exitSound;

        [Header("Events")]
        public UnityEvent OnPortalEnter;
        public UnityEvent OnPortalExit;
        public UnityEvent<TeleportPortal> OnTeleportTo;

        [Header("Confirmation")]
        [SerializeField] private bool requireConfirmation = true;
        [SerializeField] private string confirmationTitle = "Teleport";
        [SerializeField] private string confirmationMessage = "Do you want to teleport to {0}?";

        [Header("Fade")]
        [SerializeField] private bool useFade = true;
        [SerializeField] private float fadeDuration = 0.3f;

        [Header("Debug")]
        [SerializeField] private bool debugLog = false;

        private PortalNetwork network;
        private float lastActivationTime = -999f;
        private int sequentialIndex = 0;
        private Collider portalCollider;

        // Public properties
        public string PortalName => string.IsNullOrEmpty(portalName) ? gameObject.name : portalName;
        public bool CanBeDestination => canBeDestination;
        public Transform DestinationPoint => destinationPoint != null ? destinationPoint : transform;
        public Color PortalColor => portalColor;

        private void Awake()
        {
            portalCollider = GetComponent<Collider>();
            portalCollider.isTrigger = true;

            if (destinationPoint == null)
            {
                // Create a child transform for destination point
                var destObj = new GameObject("DestinationPoint");
                destObj.transform.SetParent(transform);
                destObj.transform.localPosition = Vector3.forward * 0.5f;
                destObj.transform.localRotation = Quaternion.identity;
                destinationPoint = destObj.transform;
            }
        }

        private void Start()
        {
            // Find or wait for PortalNetwork
            network = PortalNetwork.Instance;
            if (network == null)
            {
                network = FindFirstObjectByType<PortalNetwork>();
            }

            if (network != null)
            {
                network.RegisterPortal(this);
            }
            else
            {
                Debug.LogWarning($"[TeleportPortal] {PortalName}: No PortalNetwork found in scene!");
            }

            // Setup visuals
            if (portalActiveEffect != null)
            {
                portalActiveEffect.SetActive(true);
            }
        }

        private void OnDestroy()
        {
            if (network != null)
            {
                network.UnregisterPortal(this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (requiresInteraction) return;

            // Check if player
            if (!IsPlayer(other)) return;

            TryActivatePortal();
        }

        /// <summary>
        /// Manually activate the portal (for interaction-based activation).
        /// </summary>
        public void Activate()
        {
            TryActivatePortal();
        }

        private bool IsPlayer(Collider other)
        {
            // Check for common player identifiers
            if (other.CompareTag("Player")) return true;
            if (other.CompareTag("MainCamera")) return true;
            if (other.GetComponentInParent<Camera>() != null) return true;
            if (other.GetComponent<CharacterController>() != null) return true;

            return false;
        }

        private void TryActivatePortal()
        {
            // Cooldown check
            if (Time.time - lastActivationTime < activationCooldown)
            {
                if (debugLog) Debug.Log($"[TeleportPortal] {PortalName}: On cooldown");
                return;
            }

            // Network cooldown check
            if (network != null && network.IsOnCooldown)
            {
                if (debugLog) Debug.Log($"[TeleportPortal] {PortalName}: Network on cooldown");
                return;
            }

            // Get destination
            TeleportPortal destination = GetDestination();
            if (destination == null)
            {
                if (debugLog) Debug.Log($"[TeleportPortal] {PortalName}: No valid destination found");
                return;
            }

            // Show confirmation dialog if required
            if (requireConfirmation)
            {
                string message = string.Format(confirmationMessage, destination.PortalName);

                VRMessageBox.Instance?.Show(
                    confirmationTitle,
                    message,
                    onConfirm: () => ExecuteTeleport(destination),
                    onCancel: null
                );
            }
            else
            {
                // Execute teleport directly
                ExecuteTeleport(destination);
            }
        }

        private TeleportPortal GetDestination()
        {
            switch (destinationMode)
            {
                case DestinationMode.Specific:
                    return specificDestination;

                case DestinationMode.Random:
                    return network?.GetRandomPortal(this);

                case DestinationMode.Sequential:
                    if (sequentialDestinations == null || sequentialDestinations.Length == 0)
                    {
                        return network?.GetRandomPortal(this);
                    }
                    var dest = sequentialDestinations[sequentialIndex];
                    sequentialIndex = (sequentialIndex + 1) % sequentialDestinations.Length;
                    return dest;

                default:
                    return null;
            }
        }

        private void ExecuteTeleport(TeleportPortal destination)
        {
            lastActivationTime = Time.time;

            if (useFade)
            {
                StartCoroutine(ExecuteTeleportWithFade(destination));
            }
            else
            {
                ExecuteTeleportImmediate(destination);
            }
        }

        private IEnumerator ExecuteTeleportWithFade(TeleportPortal destination)
        {
            // Fade out to black
            yield return ScreenFader.FadeOut(fadeDuration, Color.black);

            // Execute actual teleport
            ExecuteTeleportImmediate(destination);

            // Small delay while faded
            yield return new WaitForSeconds(0.1f);

            // Fade in from black
            yield return ScreenFader.FadeIn(fadeDuration);
        }

        private void ExecuteTeleportImmediate(TeleportPortal destination)
        {
            // Play effects at source
            OnPortalEnter?.Invoke();
            PlaySound(enterSound);
            ShowTeleportEffect();

            // Notify destination
            destination.OnIncomingTeleport();

            // Execute teleport
            bool success = network?.TeleportToPortal(destination) ?? false;

            if (success)
            {
                if (debugLog)
                {
                    Debug.Log($"[TeleportPortal] {PortalName} -> {destination.PortalName}");
                }

                OnTeleportTo?.Invoke(destination);
            }
        }

        /// <summary>
        /// Called when a player is teleporting TO this portal.
        /// </summary>
        public void OnIncomingTeleport()
        {
            lastActivationTime = Time.time; // Prevent immediate re-trigger

            OnPortalExit?.Invoke();
            PlaySound(exitSound);
            ShowTeleportEffect();
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip == null) return;

            if (audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clip, transform.position, 0.8f);
            }
        }

        private void ShowTeleportEffect()
        {
            if (teleportEffect != null)
            {
                // If it's a particle system, play it
                var particles = teleportEffect.GetComponent<ParticleSystem>();
                if (particles != null)
                {
                    particles.Play();
                }
                else
                {
                    // Otherwise, briefly activate
                    teleportEffect.SetActive(true);
                    Invoke(nameof(HideTeleportEffect), 1f);
                }
            }
        }

        private void HideTeleportEffect()
        {
            if (teleportEffect != null)
            {
                teleportEffect.SetActive(false);
            }
        }

        /// <summary>
        /// Set destination mode at runtime.
        /// </summary>
        public void SetDestinationMode(DestinationMode mode)
        {
            destinationMode = mode;
        }

        /// <summary>
        /// Set specific destination at runtime.
        /// </summary>
        public void SetSpecificDestination(TeleportPortal destination)
        {
            specificDestination = destination;
            destinationMode = DestinationMode.Specific;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Portal area
            Gizmos.color = new Color(portalColor.r, portalColor.g, portalColor.b, 0.3f);
            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
            }
            else if (col is CapsuleCollider capsule)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireSphere(capsule.center, capsule.radius);
            }

            // Destination point
            Transform dest = destinationPoint != null ? destinationPoint : transform;
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(dest.position, 0.1f);
            Gizmos.DrawRay(dest.position, dest.forward * 0.5f);

            // Connection to specific destination
            if (destinationMode == DestinationMode.Specific && specificDestination != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, specificDestination.transform.position);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Show all sequential destinations when selected
            if (destinationMode == DestinationMode.Sequential && sequentialDestinations != null)
            {
                Gizmos.color = Color.magenta;
                for (int i = 0; i < sequentialDestinations.Length; i++)
                {
                    if (sequentialDestinations[i] != null)
                    {
                        Gizmos.DrawLine(transform.position, sequentialDestinations[i].transform.position);
                        UnityEditor.Handles.Label(
                            Vector3.Lerp(transform.position, sequentialDestinations[i].transform.position, 0.5f),
                            $"[{i}]"
                        );
                    }
                }
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(portalName))
            {
                portalName = gameObject.name;
            }
        }
#endif
    }
}
