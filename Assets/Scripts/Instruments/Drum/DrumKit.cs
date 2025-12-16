using System.Collections.Generic;
using UnityEngine;

namespace SoloBandStudio.Instruments.Drum
{
    /// <summary>
    /// Manages the drum kit visuals and finds drum pads.
    /// Reflects the state of VirtualDrumEngine visually.
    /// </summary>
    public class DrumKit : MonoBehaviour
    {
        [Header("Drum Pads")]
        [Tooltip("If empty, will auto-find objects named 'DrumPart_*' in children")]
        [SerializeField] private List<DrumPad> drumPads = new List<DrumPad>();

        private VirtualDrumEngine engine;
        private Dictionary<DrumPartType, List<DrumPad>> padsByType = new Dictionary<DrumPartType, List<DrumPad>>();

        /// <summary>
        /// Initialize and find drum pads.
        /// </summary>
        public void Initialize(VirtualDrumEngine drumEngine)
        {
            engine = drumEngine;

            if (engine != null)
            {
                engine.OnHitStateChanged += HandleHitStateChanged;
            }

            FindAndInitializePads();
            Debug.Log($"[DrumKit] Initialized with {drumPads.Count} drum pads");
        }

        private void FindAndInitializePads()
        {
            padsByType.Clear();

            // If no pads assigned, find them by name pattern
            if (drumPads.Count == 0)
            {
                FindPadsByName();
            }

            // Initialize all pads and organize by type
            foreach (var pad in drumPads)
            {
                if (pad == null) continue;

                pad.Initialize(engine);

                if (!padsByType.ContainsKey(pad.PartType))
                {
                    padsByType[pad.PartType] = new List<DrumPad>();
                }
                padsByType[pad.PartType].Add(pad);
            }
        }

        private void FindPadsByName()
        {
            // Find all transforms with "DrumPart_" prefix
            var allTransforms = GetComponentsInChildren<Transform>(true);

            foreach (var t in allTransforms)
            {
                if (!t.name.StartsWith("DrumPart_")) continue;

                // Parse the part type from name
                string partName = t.name.Substring("DrumPart_".Length);
                DrumPartType partType;

                switch (partName)
                {
                    case "Kick":
                        partType = DrumPartType.Kick;
                        break;
                    case "Snare":
                        partType = DrumPartType.Snare;
                        break;
                    case "HiHat":
                        partType = DrumPartType.HiHatClosed;  // Default to closed
                        break;
                    case "TomHigh":
                        partType = DrumPartType.TomHigh;
                        break;
                    case "TomMid":
                        partType = DrumPartType.TomMid;
                        break;
                    case "TomLow":
                        partType = DrumPartType.TomLow;
                        break;
                    case "Crash":
                        partType = DrumPartType.Crash;
                        break;
                    case "Ride":
                        partType = DrumPartType.Ride;
                        break;
                    default:
                        Debug.LogWarning($"[DrumKit] Unknown drum part: {partName}");
                        continue;
                }

                // Add DrumPad component if not present
                DrumPad pad = t.GetComponent<DrumPad>();
                if (pad == null)
                {
                    pad = t.gameObject.AddComponent<DrumPad>();
                    // Set part type via reflection or serialization workaround
                    SetDrumPadPartType(pad, partType);
                }

                drumPads.Add(pad);
                Debug.Log($"[DrumKit] Found drum part: {partName} -> {partType}");
            }
        }

        private void SetDrumPadPartType(DrumPad pad, DrumPartType partType)
        {
            // Use reflection to set the private field
            var field = typeof(DrumPad).GetField("partType",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(pad, partType);
            }
        }

        /// <summary>
        /// Handle hit state change from engine.
        /// </summary>
        private void HandleHitStateChanged(DrumPartType partType, bool isHit)
        {
            if (padsByType.TryGetValue(partType, out var pads))
            {
                foreach (var pad in pads)
                {
                    pad.SetHitState(isHit);
                }
            }
        }

        /// <summary>
        /// Get drum pads by type.
        /// </summary>
        public List<DrumPad> GetPads(DrumPartType partType)
        {
            if (padsByType.TryGetValue(partType, out var pads))
            {
                return pads;
            }
            return new List<DrumPad>();
        }

        private void OnDestroy()
        {
            if (engine != null)
            {
                engine.OnHitStateChanged -= HandleHitStateChanged;
            }
        }
    }
}
