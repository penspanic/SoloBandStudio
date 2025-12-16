using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using SoloBandStudio.Core;

namespace SoloBandStudio.Instruments.Keyboard
{
    /// <summary>
    /// Represents a single keyboard key with VR interaction support.
    /// Handles user input and notifies KeyboardLayout of press/release events.
    /// </summary>
    public class KeyboardKey : MonoBehaviour
    {
        [Header("Key Properties")]
        [SerializeField] private Note note;
        [SerializeField] private int octave;
        [SerializeField] private bool isBlackKey;

        [Header("Visual")]
        [SerializeField] private MeshRenderer keyRenderer;
        [SerializeField] private Material defaultMaterial;
        [SerializeField] private Material pressedMaterial;
        [SerializeField] private Material highlightMaterial;

        [Header("Haptic Feedback")]
        [SerializeField] private float keyPressHapticAmplitude = 0.15f;
        [SerializeField] private float keyPressHapticDuration = 0.05f;
        [SerializeField] private float keyHoverHapticAmplitude = 0.02f;
        [SerializeField] private float keyHoverHapticDuration = 0.02f;

        [Header("Spring Animation")]
        [SerializeField] private float springStiffness = 800f;
        [SerializeField] private float springDamping = 25f;
        [SerializeField] private float pressVelocityBoost = 2f;

        private Vector3 initialPosition;
        private float pressDepth = 0.005f;
        private bool isVisuallyPressed = false;
        private bool isUserPressed = false;

        // Spring animation state
        private float currentDepth = 0f;
        private float targetDepth = 0f;
        private float velocity = 0f;

        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable;
        private KeyboardLayout keyboard;

        // Properties
        public Note Note => note;
        public int Octave => octave;
        public bool IsBlackKey => isBlackKey;
        // MIDI standard: C-1 = 0, C0 = 12, C4 = 60, A0 = 21
        public int MidiNote => ((octave + 1) * 12) + (int)note;

        /// <summary>
        /// Initializes the keyboard key.
        /// </summary>
        public void Initialize(Note keyNote, int keyOctave, bool blackKey, float depth, KeyboardLayout parentKeyboard)
        {
            note = keyNote;
            octave = keyOctave;
            isBlackKey = blackKey;
            pressDepth = depth;
            keyboard = parentKeyboard;

            initialPosition = transform.localPosition;

            SetupCollider();
            SetupInteraction();
            SetupVisuals();
        }

        private void SetupCollider()
        {
            BoxCollider collider = GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<BoxCollider>();
            }
            collider.isTrigger = false;

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
            interactable = gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            interactable.interactionLayers = InteractionLayerMask.GetMask("Default");
            interactable.selectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Single;
            interactable.hoverEntered.AddListener(OnKeyHoverEntered);
            interactable.selectEntered.AddListener(OnKeySelectEntered);
            interactable.selectExited.AddListener(OnKeySelectExited);
        }

        private void SetupVisuals()
        {
            if (keyRenderer == null)
            {
                keyRenderer = GetComponent<MeshRenderer>();
            }

            if (keyRenderer != null && defaultMaterial == null)
            {
                defaultMaterial = keyRenderer.material;
            }
        }

        private void OnKeyHoverEntered(HoverEnterEventArgs args)
        {
            // Light haptic feedback when finger approaches the key
            SendHapticFeedback(args.interactorObject, keyHoverHapticAmplitude, keyHoverHapticDuration);
        }

        private void OnKeySelectEntered(SelectEnterEventArgs args)
        {
            if (isUserPressed) return;
            isUserPressed = true;

            // Notify keyboard of user press
            keyboard?.NotifyKeyPressed(MidiNote, 1f);

            // Update visual state (skip in toggle mode - quiz handles this)
            if (keyboard == null || !keyboard.ToggleMode)
            {
                SetVisualState(true);
            }

            // Strong haptic feedback when key is pressed
            SendHapticFeedback(args.interactorObject, keyPressHapticAmplitude, keyPressHapticDuration);
        }

        private void OnKeySelectExited(SelectExitEventArgs args)
        {
            if (!isUserPressed) return;
            isUserPressed = false;

            // Notify keyboard of user release
            keyboard?.NotifyKeyReleased(MidiNote);

            // In toggle mode (quiz), don't auto-release visual state
            bool toggleMode = keyboard != null && keyboard.ToggleMode;
            if (toggleMode) return;

            // Update visual state
            SetVisualState(false);
        }

        private void Update()
        {
            UpdateSpringAnimation();
        }

        private void UpdateSpringAnimation()
        {
            // Clamp deltaTime to prevent instability on frame drops
            float dt = Mathf.Min(Time.deltaTime, 0.033f); // Cap at ~30fps

            // Spring physics: F = -kx - cv
            float displacement = targetDepth - currentDepth;
            float springForce = displacement * springStiffness;
            float dampingForce = velocity * springDamping;
            float acceleration = springForce - dampingForce;

            velocity += acceleration * dt;
            currentDepth += velocity * dt;

            // Reset if values become invalid (NaN/Infinity)
            if (float.IsNaN(currentDepth) || float.IsInfinity(currentDepth) ||
                float.IsNaN(velocity) || float.IsInfinity(velocity))
            {
                currentDepth = targetDepth;
                velocity = 0f;
            }

            // Apply position
            transform.localPosition = initialPosition - new Vector3(0, currentDepth, 0);
        }

        /// <summary>
        /// Set the visual state of the key (for both user input and scheduled playback).
        /// </summary>
        public void SetVisualState(bool pressed)
        {
            // Don't override user press with playback release (except in toggle mode)
            bool inToggleMode = keyboard != null && keyboard.ToggleMode;
            if (!pressed && isUserPressed && !inToggleMode) return;

            isVisuallyPressed = pressed;

            // Set target for spring animation
            targetDepth = pressed ? pressDepth : 0f;

            // Add velocity boost when pressing for snappier response
            if (pressed)
            {
                velocity += pressVelocityBoost;
            }

            UpdateMaterial();
        }

        private void UpdateMaterial()
        {
            if (keyRenderer == null) return;

            if (isVisuallyPressed && pressedMaterial != null)
            {
                keyRenderer.material = pressedMaterial;
            }
            else if (defaultMaterial != null)
            {
                keyRenderer.material = defaultMaterial;
            }
        }

        public void SetHighlightMaterial(Material material)
        {
            highlightMaterial = material;
        }

        public void SetPressedMaterial(Material material)
        {
            pressedMaterial = material;
        }

        private void SendHapticFeedback(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor interactor, float amplitude, float duration)
        {
#if UNITY_EDITOR
            // Skip haptics in Editor to avoid XRSimulatedController warnings
            return;
#else
            if (interactor is MonoBehaviour interactorBehaviour)
            {
                var controller = interactorBehaviour.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>();
                controller?.SendHapticImpulse(amplitude, duration);
            }
#endif
        }

        private void OnDestroy()
        {
            if (interactable != null)
            {
                interactable.hoverEntered.RemoveListener(OnKeyHoverEntered);
                interactable.selectEntered.RemoveListener(OnKeySelectEntered);
                interactable.selectExited.RemoveListener(OnKeySelectExited);
            }
        }
    }
}
