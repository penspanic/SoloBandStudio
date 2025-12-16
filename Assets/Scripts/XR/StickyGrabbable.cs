using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace SoloBandStudio.XR
{
    /// <summary>
    /// Grabbable that sticks to the controller once grabbed.
    /// - First grab: attaches to controller and stays attached (even after releasing grip)
    /// - Second grab (while attached): releases and returns home
    /// - Direct attachment: follows controller position in Update
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class StickyGrabbable : XRGrabInteractable
    {
        [Header("Sticky Settings")]
        [SerializeField] private bool stickyEnabled = true;
        [SerializeField] private Vector3 attachOffset = Vector3.zero;
        [SerializeField] private Vector3 attachRotationOffset = Vector3.zero;

        [Header("Return Settings")]
        [SerializeField] private float returnSpeed = 5f;
        [SerializeField] private float returnRotationSpeed = 360f;
        [SerializeField] private float snapDistance = 0.05f;

        [Header("Feedback")]
        [SerializeField] private float hapticIntensity = 0.3f;
        [SerializeField] private float hapticDuration = 0.1f;

        private Vector3 homePosition;
        private Quaternion homeRotation;
        private Transform homeParent;
        private bool isReturning;
        private Rigidbody rb;

        // Sticky state
        private bool isStuck;
        private Transform attachedController;

        protected override void Awake()
        {
            base.Awake();
            rb = GetComponent<Rigidbody>();
            SaveHomePosition();

            // Configure
            useDynamicAttach = false;
            throwOnDetach = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            selectEntered.AddListener(OnGrabbed);
            selectExited.AddListener(OnReleased);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            selectEntered.RemoveListener(OnGrabbed);
            selectExited.RemoveListener(OnReleased);

            if (isStuck)
            {
                ForceRelease();
            }
        }

        public void SaveHomePosition()
        {
            homePosition = transform.localPosition;
            homeRotation = transform.localRotation;
            homeParent = transform.parent;
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            isReturning = false;
            var controllerTransform = args.interactorObject.transform;

            if (stickyEnabled)
            {
                if (!isStuck)
                {
                    // First grab - become sticky
                    BecomeStuck(controllerTransform);
                    SendHapticFeedback(args.interactorObject, hapticIntensity, hapticDuration);
                }
                else
                {
                    // Already stuck and grabbed again - mark for release
                    isStuck = false;
                    attachedController = null;
                    SendHapticFeedback(args.interactorObject, hapticIntensity * 0.5f, hapticDuration);
                }
            }
            else
            {
                rb.isKinematic = false;
                rb.useGravity = false;
            }
        }

        private void BecomeStuck(Transform controller)
        {
            isStuck = true;
            attachedController = controller;

            // Make kinematic
            rb.isKinematic = true;
            rb.useGravity = false;

            // Unparent from home (if any)
            transform.SetParent(null);

            // Immediately snap to controller position
            UpdateStickyPosition();
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (stickyEnabled && isStuck)
            {
                // Still stuck - keep following controller, don't return
                return;
            }

            // Actually releasing - start return
            StartReturn();
        }

        private void StartReturn()
        {
            isStuck = false;
            attachedController = null;

            // Clear physics (only if not kinematic)
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;

            isReturning = true;
        }

        /// <summary>
        /// Force release from sticky state and return home.
        /// </summary>
        public void ForceRelease()
        {
            isStuck = false;
            attachedController = null;
            StartReturn();
        }

        private void Update()
        {
            // Follow controller while stuck
            if (isStuck && attachedController != null)
            {
                UpdateStickyPosition();
                return;
            }

            // Return home animation
            if (isReturning)
            {
                UpdateReturnAnimation();
            }
        }

        private void UpdateStickyPosition()
        {
            if (attachedController == null) return;

            // Calculate target position/rotation relative to controller
            Vector3 targetPos = attachedController.TransformPoint(attachOffset);
            Quaternion targetRot = attachedController.rotation * Quaternion.Euler(attachRotationOffset);

            transform.position = targetPos;
            transform.rotation = targetRot;
        }

        private void UpdateReturnAnimation()
        {
            Vector3 targetWorldPos = homeParent != null
                ? homeParent.TransformPoint(homePosition)
                : homePosition;
            Quaternion targetWorldRot = homeParent != null
                ? homeParent.rotation * homeRotation
                : homeRotation;

            float distance = Vector3.Distance(transform.position, targetWorldPos);

            if (distance < snapDistance)
            {
                // Snap to final position
                if (homeParent != null)
                {
                    transform.SetParent(homeParent);
                    transform.localPosition = homePosition;
                    transform.localRotation = homeRotation;
                }
                else
                {
                    transform.position = targetWorldPos;
                    transform.rotation = targetWorldRot;
                }

                isReturning = false;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, targetWorldPos, returnSpeed * Time.deltaTime);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetWorldRot, returnRotationSpeed * Time.deltaTime);
            }
        }

        private void SendHapticFeedback(IXRSelectInteractor interactor, float intensity, float duration)
        {
            if (interactor is XRBaseInputInteractor inputInteractor)
            {
                inputInteractor.SendHapticImpulse(intensity, duration);
            }
        }

        public void TeleportHome()
        {
            isStuck = false;
            attachedController = null;
            isReturning = false;

            if (homeParent != null)
            {
                transform.SetParent(homeParent);
                transform.localPosition = homePosition;
                transform.localRotation = homeRotation;
            }
            else
            {
                transform.position = homePosition;
                transform.rotation = homeRotation;
            }

            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        public bool IsStuck => isStuck;
        public bool IsStickyEnabled => stickyEnabled;
        public void SetStickyEnabled(bool enabled) => stickyEnabled = enabled;
    }
}
