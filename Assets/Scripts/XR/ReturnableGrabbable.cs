using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SoloBandStudio.XR
{
    /// <summary>
    /// XRGrabInteractable that returns to its original position when released.
    /// Useful for tools like drumsticks that should return to a holder.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ReturnableGrabbable : XRGrabInteractable
    {
        [Header("Return Settings")]
        [SerializeField] private float returnSpeed = 5f;
        [SerializeField] private float returnRotationSpeed = 360f;
        [SerializeField] private float snapDistance = 0.05f;
        [SerializeField] private bool useSmoothing = true;

        private Vector3 homePosition;
        private Quaternion homeRotation;
        private Transform homeParent;
        private bool isReturning;
        private Rigidbody rb;

        protected override void Awake()
        {
            base.Awake();
            rb = GetComponent<Rigidbody>();
            SaveHomePosition();
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
        }

        /// <summary>
        /// Save the current position as the home position.
        /// Call this if you want to update where the object returns to.
        /// </summary>
        public void SaveHomePosition()
        {
            homePosition = transform.localPosition;
            homeRotation = transform.localRotation;
            homeParent = transform.parent;
        }

        /// <summary>
        /// Set a new home position explicitly.
        /// </summary>
        public void SetHomePosition(Vector3 worldPosition, Quaternion worldRotation, Transform parent = null)
        {
            homeParent = parent;
            if (parent != null)
            {
                homePosition = parent.InverseTransformPoint(worldPosition);
                homeRotation = Quaternion.Inverse(parent.rotation) * worldRotation;
            }
            else
            {
                homePosition = worldPosition;
                homeRotation = worldRotation;
            }
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            isReturning = false;

            // Enable physics while grabbed
            rb.isKinematic = false;
            rb.useGravity = false;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            // Clear velocity before going kinematic
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Start return animation
            isReturning = true;

            // Disable physics during return
            rb.isKinematic = true;
        }

        private void Update()
        {
            if (!isReturning) return;

            // Calculate target position in world space
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
                transform.position = targetWorldPos;
                transform.rotation = targetWorldRot;

                // Re-parent if needed
                if (homeParent != null && transform.parent != homeParent)
                {
                    transform.SetParent(homeParent);
                    transform.localPosition = homePosition;
                    transform.localRotation = homeRotation;
                }

                isReturning = false;
            }
            else
            {
                // Smooth movement back to home
                if (useSmoothing)
                {
                    transform.position = Vector3.Lerp(transform.position, targetWorldPos, returnSpeed * Time.deltaTime);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetWorldRot, returnRotationSpeed * Time.deltaTime);
                }
                else
                {
                    transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, returnSpeed * Time.deltaTime);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetWorldRot, returnRotationSpeed * Time.deltaTime);
                }
            }
        }

        /// <summary>
        /// Immediately teleport back to home position.
        /// </summary>
        public void TeleportHome()
        {
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

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}