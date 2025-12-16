using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace SoloBandStudio.XR
{
    /// <summary>
    /// Manages switching between Continuous Turn and Snap Turn modes.
    /// Toggle with B button (right controller) or Y button (left controller).
    /// </summary>
    public class TurnModeManager : MonoBehaviour
    {
        public enum TurnMode
        {
            Continuous,
            Snap
        }

        [Header("Turn Providers")]
        [SerializeField] private ContinuousTurnProvider continuousTurnProvider;
        [SerializeField] private SnapTurnProvider snapTurnProvider;

        [Header("Settings")]
        [SerializeField] private TurnMode defaultMode = TurnMode.Continuous;

        [Header("Continuous Turn Settings")]
        [SerializeField] private float continuousTurnSpeed = 60f;

        [Header("Snap Turn Settings")]
        [SerializeField] private float snapTurnAmount = 45f;
        [SerializeField] private float snapTurnDebounceTime = 0.5f;

        [Header("Input")]
        [Tooltip("Input action for toggling turn mode. If not set, uses B/Y button fallback")]
        [SerializeField] private InputActionReference toggleAction;

        [Tooltip("Use B button (right) / Y button (left) as fallback")]
        [SerializeField] private bool useButtonFallback = true;

        [Header("Haptic Feedback")]
        [SerializeField] private bool enableHaptics = true;
        [SerializeField] private float hapticAmplitude = 0.3f;
        [SerializeField] private float hapticDuration = 0.1f;
        [SerializeField] private ActionBasedController leftController;
        [SerializeField] private ActionBasedController rightController;

        [Header("Debug")]
        [SerializeField] private bool debugLog = true;

        private TurnMode currentMode;
        private InputAction buttonToggleAction;

        public TurnMode CurrentMode => currentMode;

        public event System.Action<TurnMode> OnTurnModeChanged;

        private void Start()
        {
            // Auto-find turn providers if not assigned
            if (continuousTurnProvider == null)
            {
                continuousTurnProvider = GetComponentInChildren<ContinuousTurnProvider>(true);
                if (continuousTurnProvider == null)
                {
                    continuousTurnProvider = FindFirstObjectByType<ContinuousTurnProvider>(FindObjectsInactive.Include);
                }
            }

            if (snapTurnProvider == null)
            {
                snapTurnProvider = GetComponentInChildren<SnapTurnProvider>(true);
                if (snapTurnProvider == null)
                {
                    snapTurnProvider = FindFirstObjectByType<SnapTurnProvider>(FindObjectsInactive.Include);
                }
            }

            // Validate
            if (continuousTurnProvider == null)
            {
                Debug.LogWarning("[TurnModeManager] ContinuousTurnProvider not found!");
            }

            if (snapTurnProvider == null)
            {
                Debug.LogWarning("[TurnModeManager] SnapTurnProvider not found!");
            }

            // Apply settings
            ApplyTurnSettings();

            // Set initial mode
            SetTurnMode(defaultMode);

            // Create fallback button action
            if (useButtonFallback && toggleAction == null)
            {
                buttonToggleAction = new InputAction("TurnModeToggle", InputActionType.Button);
                // Meta Quest 3 button mapping:
                // A = primaryButton (right), B = secondaryButton (right)
                // X = primaryButton (left), Y = secondaryButton (left)
                // Using B button (right) and Y button (left) for toggle
                buttonToggleAction.AddBinding("<XRController>{RightHand}/secondaryButton"); // B button
                buttonToggleAction.AddBinding("<XRController>{LeftHand}/secondaryButton");  // Y button
                buttonToggleAction.Enable();

                if (debugLog)
                {
                    Debug.Log("[TurnModeManager] Using B/Y button fallback for toggle");
                }
            }

            // Auto-find controllers for haptics
            if (enableHaptics)
            {
                if (leftController == null)
                {
                    var leftHand = GameObject.Find("Left Controller");
                    if (leftHand != null)
                    {
                        leftController = leftHand.GetComponentInParent<ActionBasedController>();
                    }
                }

                if (rightController == null)
                {
                    var rightHand = GameObject.Find("Right Controller");
                    if (rightHand != null)
                    {
                        rightController = rightHand.GetComponentInParent<ActionBasedController>();
                    }
                }
            }
        }

        private void OnEnable()
        {
            if (toggleAction != null && toggleAction.action != null)
            {
                toggleAction.action.Enable();
            }

            buttonToggleAction?.Enable();
        }

        private void OnDisable()
        {
            if (toggleAction != null && toggleAction.action != null)
            {
                toggleAction.action.Disable();
            }

            buttonToggleAction?.Disable();
        }

        private void OnDestroy()
        {
            buttonToggleAction?.Dispose();
        }

        private void Update()
        {
            // Check for toggle input
            if (CheckToggleInput())
            {
                ToggleTurnMode();
            }
        }

        private bool CheckToggleInput()
        {
            // Configured action (priority)
            if (toggleAction != null && toggleAction.action != null && toggleAction.action.WasPressedThisFrame())
            {
                return true;
            }

            // Button fallback
            if (buttonToggleAction != null && buttonToggleAction.WasPressedThisFrame())
            {
                return true;
            }

            // Editor: T key for testing
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }

        private void ApplyTurnSettings()
        {
            if (continuousTurnProvider != null)
            {
                continuousTurnProvider.turnSpeed = continuousTurnSpeed;
            }

            if (snapTurnProvider != null)
            {
                snapTurnProvider.turnAmount = snapTurnAmount;
                snapTurnProvider.debounceTime = snapTurnDebounceTime;
            }
        }

        /// <summary>
        /// Toggle between Continuous and Snap turn modes.
        /// </summary>
        public void ToggleTurnMode()
        {
            TurnMode newMode = currentMode == TurnMode.Continuous ? TurnMode.Snap : TurnMode.Continuous;
            SetTurnMode(newMode);
        }

        /// <summary>
        /// Set the turn mode explicitly.
        /// </summary>
        public void SetTurnMode(TurnMode mode)
        {
            currentMode = mode;

            bool useContinuous = mode == TurnMode.Continuous;

            if (continuousTurnProvider != null)
            {
                continuousTurnProvider.enabled = useContinuous;
                if (debugLog)
                {
                    Debug.Log($"[TurnModeManager] ContinuousTurnProvider.enabled = {useContinuous}");
                }
            }
            else if (debugLog)
            {
                Debug.LogWarning("[TurnModeManager] ContinuousTurnProvider is NULL!");
            }

            if (snapTurnProvider != null)
            {
                snapTurnProvider.enabled = !useContinuous;
                if (debugLog)
                {
                    Debug.Log($"[TurnModeManager] SnapTurnProvider.enabled = {!useContinuous}");
                }
            }
            else if (debugLog)
            {
                Debug.LogWarning("[TurnModeManager] SnapTurnProvider is NULL!");
            }

            // Haptic feedback
            if (enableHaptics)
            {
                SendHapticFeedback();
            }

            // Event
            OnTurnModeChanged?.Invoke(mode);

            if (debugLog)
            {
                Debug.Log($"[TurnModeManager] Turn mode changed to: {mode}");
            }
        }

        private void SendHapticFeedback()
        {
            if (rightController != null)
            {
                rightController.SendHapticImpulse(hapticAmplitude, hapticDuration);
            }

            if (leftController != null)
            {
                leftController.SendHapticImpulse(hapticAmplitude, hapticDuration);
            }
        }

        /// <summary>
        /// Set continuous turn speed at runtime.
        /// </summary>
        public void SetContinuousTurnSpeed(float speed)
        {
            continuousTurnSpeed = speed;
            if (continuousTurnProvider != null)
            {
                continuousTurnProvider.turnSpeed = speed;
            }
        }

        /// <summary>
        /// Set snap turn amount at runtime.
        /// </summary>
        public void SetSnapTurnAmount(float degrees)
        {
            snapTurnAmount = degrees;
            if (snapTurnProvider != null)
            {
                snapTurnProvider.turnAmount = degrees;
            }
        }
    }
}