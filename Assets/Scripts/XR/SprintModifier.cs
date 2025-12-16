using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace SoloBandStudio.XR
{
    /// <summary>
    /// Modifies movement speed based on sprint input.
    /// Editor: Hold Shift to sprint
    /// VR: Left Thumbstick Click (L3) or configured action
    /// </summary>
    public class SprintModifier : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ContinuousMoveProvider moveProvider;

        [Header("Speed Settings")]
        [SerializeField] private float normalSpeed = 2f;
        [SerializeField] private float sprintSpeed = 5f;
        [SerializeField] private float speedTransitionRate = 10f;

        [Header("VR Input")]
        [Tooltip("Input action for sprinting. If not set, uses Left Thumbstick Click")]
        [SerializeField] private InputActionReference sprintAction;

        [Tooltip("Use Left Thumbstick Click as fallback when no action is assigned")]
        [SerializeField] private bool useThumbstickClickFallback = true;

        [Header("Debug")]
        [SerializeField] private bool debugLog = false;

        private float currentSpeed;
        private bool wasSprinting;

        // Fallback input action for thumbstick click
        private InputAction thumbstickClickAction;

        private void Start()
        {
            if (moveProvider == null)
            {
                moveProvider = FindFirstObjectByType<ContinuousMoveProvider>();
            }

            if (moveProvider != null)
            {
                currentSpeed = normalSpeed;
                moveProvider.moveSpeed = normalSpeed;
            }
            else
            {
                Debug.LogWarning("[SprintModifier] ContinuousMoveProvider not found!");
            }

            // Create fallback thumbstick click action
            if (useThumbstickClickFallback && sprintAction == null)
            {
                thumbstickClickAction = new InputAction("ThumbstickClick", InputActionType.Button);
                thumbstickClickAction.AddBinding("<XRController>{LeftHand}/{Primary2DAxisClick}");
                thumbstickClickAction.AddBinding("<XRController>{RightHand}/{Primary2DAxisClick}");
                thumbstickClickAction.Enable();

                if (debugLog)
                {
                    Debug.Log("[SprintModifier] Using Thumbstick Click fallback for sprint");
                }
            }
        }

        private void OnEnable()
        {
            if (sprintAction != null && sprintAction.action != null)
            {
                sprintAction.action.Enable();
            }

            thumbstickClickAction?.Enable();
        }

        private void OnDisable()
        {
            if (sprintAction != null && sprintAction.action != null)
            {
                sprintAction.action.Disable();
            }

            thumbstickClickAction?.Disable();
        }

        private void OnDestroy()
        {
            thumbstickClickAction?.Dispose();
        }

        private void Update()
        {
            if (moveProvider == null) return;

            bool isSprinting = CheckSprintInput();

            // Log state change
            if (debugLog && isSprinting != wasSprinting)
            {
                Debug.Log($"[SprintModifier] Sprint: {isSprinting}");
                wasSprinting = isSprinting;
            }

            // Target speed
            float targetSpeed = isSprinting ? sprintSpeed : normalSpeed;

            // Smooth transition
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, speedTransitionRate * Time.deltaTime);
            moveProvider.moveSpeed = currentSpeed;
        }

        private bool CheckSprintInput()
        {
            // Editor: Shift key
            if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
            {
                return true;
            }

            // VR: Configured action (priority)
            if (sprintAction != null && sprintAction.action != null && sprintAction.action.IsPressed())
            {
                return true;
            }

            // VR: Thumbstick click fallback
            if (thumbstickClickAction != null && thumbstickClickAction.IsPressed())
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Set sprint speed multiplier at runtime.
        /// </summary>
        public void SetSprintMultiplier(float multiplier)
        {
            sprintSpeed = normalSpeed * multiplier;
        }

        /// <summary>
        /// Set normal speed at runtime.
        /// </summary>
        public void SetNormalSpeed(float speed)
        {
            normalSpeed = speed;
            if (!CheckSprintInput())
            {
                currentSpeed = speed;
            }
        }
    }
}
