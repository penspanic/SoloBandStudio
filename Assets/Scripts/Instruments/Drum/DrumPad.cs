using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace SoloBandStudio.Instruments.Drum
{
    /// <summary>
    /// Represents a single drum pad/surface with VR interaction support.
    /// Attach this to each hittable drum part (kick, snare, etc.)
    /// Supports both Ray interaction (select) and physical collision (drumstick).
    /// </summary>
    public class DrumPad : MonoBehaviour
    {
        [Header("Drum Part")]
        [SerializeField] private DrumPartType partType;

        [Header("Visual Feedback")]
        [SerializeField] private MeshRenderer[] renderers;
        [SerializeField] private Color hitColor = new Color(1f, 0.5f, 0f, 1f);  // Orange glow
        [SerializeField] private float emissionIntensity = 2f;

        [Header("Haptic Feedback")]
        [SerializeField] private float hapticAmplitude = 0.5f;
        [SerializeField] private float hapticDuration = 0.05f;

        [Header("Collision Settings")]
        [SerializeField] private bool enableCollisionHits = true;

        [Header("Hit Cooldown")]
        [SerializeField] private float hitCooldown = 0.3f;

        private VirtualDrumEngine engine;
        private float lastHitTime = -1f;
        private Color[] originalColors;
        private Material[] materials;
        private bool isHit = false;

        public DrumPartType PartType => partType;

        /// <summary>
        /// Initialize the drum pad with engine reference.
        /// </summary>
        public void Initialize(VirtualDrumEngine drumEngine)
        {
            engine = drumEngine;

            // Cache renderers if not set
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<MeshRenderer>();
            }

            // Cache original colors and materials
            if (renderers.Length > 0)
            {
                materials = new Material[renderers.Length];
                originalColors = new Color[renderers.Length];

                for (int i = 0; i < renderers.Length; i++)
                {
                    materials[i] = renderers[i].material;  // Create instance
                    if (materials[i].HasProperty("_Color"))
                    {
                        originalColors[i] = materials[i].color;
                    }
                    else if (materials[i].HasProperty("_BaseColor"))
                    {
                        originalColors[i] = materials[i].GetColor("_BaseColor");
                    }
                }
            }

            SetupCollider();
            SetupInteraction();
        }

        private void SetupCollider()
        {
            // Ensure we have a collider
            Collider col = GetComponent<Collider>();
            if (col == null)
            {
                // Add a mesh collider if no collider exists
                MeshCollider meshCol = gameObject.AddComponent<MeshCollider>();
                meshCol.convex = true;
                col = meshCol;
            }

            // Set collider as trigger for drumstick collision detection
            col.isTrigger = true;

            // Add rigidbody if needed (required for trigger events)
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        private void SetupInteraction()
        {
            // Use XRSimpleInteractable for tap detection
            UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (interactable == null)
            {
                interactable = gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            }

            interactable.interactionLayers = InteractionLayerMask.GetMask("Default");
            interactable.selectEntered.AddListener(OnPadHit);
        }

        private void OnPadHit(SelectEnterEventArgs args)
        {
            if (!CanHit()) return;

            // Hit the drum
            engine?.HitDrum(partType, 1f);
            lastHitTime = Time.time;

            // Haptic feedback
            SendHapticFeedback(args.interactorObject);
        }

        private bool CanHit()
        {
            return Time.time - lastHitTime >= hitCooldown;
        }

        /// <summary>
        /// Set the visual hit state (called by DrumKit from engine events).
        /// </summary>
        public void SetHitState(bool hit)
        {
            if (isHit == hit) return;
            isHit = hit;

            if (materials == null) return;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;

                if (hit)
                {
                    // Set emission for hit effect
                    materials[i].EnableKeyword("_EMISSION");
                    materials[i].SetColor("_EmissionColor", hitColor * emissionIntensity);
                }
                else
                {
                    // Reset to original
                    materials[i].SetColor("_EmissionColor", Color.black);
                }
            }
        }

        private void SendHapticFeedback(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor interactor)
        {
            if (interactor is MonoBehaviour interactorBehaviour)
            {
                var controller = interactorBehaviour.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>();
                controller?.SendHapticImpulse(hapticAmplitude, hapticDuration);
            }
        }

        /// <summary>
        /// Called by DrumstickTip when a physical collision occurs.
        /// </summary>
        public void HitFromCollision(float velocity)
        {
            if (!enableCollisionHits) return;
            if (!CanHit()) return;

            engine?.HitDrum(partType, velocity);
            lastHitTime = Time.time;
        }

        private void OnDestroy()
        {
            // Clean up instanced materials
            if (materials != null)
            {
                foreach (var mat in materials)
                {
                    if (mat != null) Destroy(mat);
                }
            }
        }
    }
}
