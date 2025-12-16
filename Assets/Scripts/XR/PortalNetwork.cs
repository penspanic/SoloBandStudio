using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace SoloBandStudio.XR
{
    /// <summary>
    /// Manages a network of teleport portals in the scene.
    /// Handles portal registration and random destination selection.
    /// </summary>
    public class PortalNetwork : MonoBehaviour
    {
        [Header("Teleportation")]
        [SerializeField] private TeleportationProvider teleportationProvider;

        [Header("Settings")]
        [SerializeField] private bool autoFindProvider = true;
        [SerializeField] private float globalCooldown = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool debugLog = false;

        private List<TeleportPortal> registeredPortals = new List<TeleportPortal>();
        private float lastTeleportTime = -999f;

        public static PortalNetwork Instance { get; private set; }

        public TeleportationProvider Provider => teleportationProvider;
        public IReadOnlyList<TeleportPortal> Portals => registeredPortals;
        public bool IsOnCooldown => Time.time - lastTeleportTime < globalCooldown;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PortalNetwork] Multiple instances detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (teleportationProvider == null && autoFindProvider)
            {
                teleportationProvider = FindFirstObjectByType<TeleportationProvider>();
                if (teleportationProvider == null)
                {
                    Debug.LogError("[PortalNetwork] No TeleportationProvider found! Portals won't work.");
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Register a portal to the network.
        /// </summary>
        public void RegisterPortal(TeleportPortal portal)
        {
            if (portal == null || registeredPortals.Contains(portal)) return;

            registeredPortals.Add(portal);

            if (debugLog)
            {
                Debug.Log($"[PortalNetwork] Registered portal: {portal.PortalName} (Total: {registeredPortals.Count})");
            }
        }

        /// <summary>
        /// Unregister a portal from the network.
        /// </summary>
        public void UnregisterPortal(TeleportPortal portal)
        {
            if (portal == null) return;

            registeredPortals.Remove(portal);

            if (debugLog)
            {
                Debug.Log($"[PortalNetwork] Unregistered portal: {portal.PortalName} (Total: {registeredPortals.Count})");
            }
        }

        /// <summary>
        /// Get a random portal excluding the specified one.
        /// </summary>
        public TeleportPortal GetRandomPortal(TeleportPortal exclude = null)
        {
            if (registeredPortals.Count == 0) return null;
            if (registeredPortals.Count == 1 && registeredPortals[0] == exclude) return null;

            List<TeleportPortal> candidates = new List<TeleportPortal>();
            foreach (var portal in registeredPortals)
            {
                if (portal != exclude && portal.CanBeDestination)
                {
                    candidates.Add(portal);
                }
            }

            if (candidates.Count == 0) return null;

            return candidates[Random.Range(0, candidates.Count)];
        }

        /// <summary>
        /// Get portal by name.
        /// </summary>
        public TeleportPortal GetPortalByName(string name)
        {
            foreach (var portal in registeredPortals)
            {
                if (portal.PortalName == name)
                {
                    return portal;
                }
            }
            return null;
        }

        /// <summary>
        /// Execute teleportation to a destination.
        /// </summary>
        public bool TeleportTo(Vector3 position, Quaternion rotation)
        {
            if (teleportationProvider == null)
            {
                Debug.LogError("[PortalNetwork] Cannot teleport - no TeleportationProvider!");
                return false;
            }

            if (IsOnCooldown)
            {
                if (debugLog) Debug.Log("[PortalNetwork] Teleport blocked by cooldown");
                return false;
            }

            TeleportRequest request = new TeleportRequest
            {
                requestTime = Time.time,
                matchOrientation = MatchOrientation.TargetUpAndForward,
                destinationPosition = position,
                destinationRotation = rotation
            };

            teleportationProvider.QueueTeleportRequest(request);
            lastTeleportTime = Time.time;

            if (debugLog)
            {
                Debug.Log($"[PortalNetwork] Teleported to {position}");
            }

            return true;
        }

        /// <summary>
        /// Execute teleportation to a portal's destination point.
        /// </summary>
        public bool TeleportToPortal(TeleportPortal portal)
        {
            if (portal == null) return false;

            return TeleportTo(portal.DestinationPoint.position, portal.DestinationPoint.rotation);
        }
    }
}
