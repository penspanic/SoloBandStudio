using UnityEngine;

namespace SoloBandStudio.UI.QuickMenu
{
    /// <summary>
    /// Makes a UI element follow the player's view (camera) with smooth movement.
    /// Positions below the player's view for easy access.
    /// </summary>
    public class FollowPlayerView : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform targetCamera;

        [Header("Position")]
        [Tooltip("Distance in front of the camera")]
        [SerializeField] private float forwardDistance = 0.5f;

        [Tooltip("Vertical offset from camera (negative = below)")]
        [SerializeField] private float verticalOffset = -0.3f;

        [Tooltip("Horizontal offset from camera center")]
        [SerializeField] private float horizontalOffset = 0f;

        [Header("Follow Behavior")]
        [Tooltip("How smoothly to follow position (higher = snappier)")]
        [SerializeField] private float positionSmoothSpeed = 5f;

        [Tooltip("How smoothly to follow rotation (higher = snappier)")]
        [SerializeField] private float rotationSmoothSpeed = 5f;

        [Tooltip("Only follow rotation on Y axis (horizontal)")]
        [SerializeField] private bool horizontalRotationOnly = false;

        [Tooltip("Angle threshold before starting to rotate (prevents jitter)")]
        [SerializeField] private float rotationDeadzone = 15f;

        [Header("Follow Mode")]
        [Tooltip("If false, only positions once when SnapToPosition() is called")]
        [SerializeField] private bool continuousFollow = false;

        [Header("Lazy Follow (only when continuousFollow is true)")]
        [Tooltip("Enable lazy following (only updates when player looks away)")]
        [SerializeField] private bool lazyFollow = true;

        [Tooltip("Distance from center of view to trigger reposition")]
        [SerializeField] private float lazyFollowThreshold = 0.4f;

        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private bool needsReposition;

        private void Start()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main?.transform;
            }

            if (targetCamera == null)
            {
                Debug.LogError("[FollowPlayerView] No camera found!");
                enabled = false;
                return;
            }

            // Initialize to current ideal position
            UpdateTargetTransform();
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }

        private void LateUpdate()
        {
            if (targetCamera == null) return;
            if (!continuousFollow) return;

            if (lazyFollow)
            {
                UpdateLazyFollow();
            }
            else
            {
                UpdateTargetTransform();
            }

            ApplySmoothing();
        }

        private void UpdateTargetTransform()
        {
            // Calculate position relative to camera
            Vector3 forward = targetCamera.forward;
            Vector3 right = targetCamera.right;

            if (horizontalRotationOnly)
            {
                forward.y = 0;
                forward.Normalize();
                if (forward.sqrMagnitude < 0.001f)
                {
                    forward = Vector3.forward;
                }
                right = Vector3.Cross(Vector3.up, forward).normalized;
            }

            // Use camera's local axes for proper plane alignment
            Vector3 up = horizontalRotationOnly ? Vector3.up : targetCamera.up;

            targetPosition = targetCamera.position
                + forward * forwardDistance
                + up * verticalOffset
                + right * horizontalOffset;

            // Rotation: face the camera, aligned to camera's plane
            if (horizontalRotationOnly)
            {
                Vector3 lookDirection = targetCamera.position - targetPosition;
                lookDirection.y = 0;
                if (lookDirection.sqrMagnitude > 0.001f)
                {
                    targetRotation = Quaternion.LookRotation(-lookDirection, Vector3.up);
                }
            }
            else
            {
                // Simply face opposite to camera direction (same plane as camera view)
                targetRotation = targetCamera.rotation;
            }
        }

        private void UpdateLazyFollow()
        {
            // Check if menu is too far from ideal position
            Vector3 idealPosition = CalculateIdealPosition();
            float distance = Vector3.Distance(transform.position, idealPosition);

            // Check angle from camera forward
            Vector3 toMenu = (transform.position - targetCamera.position).normalized;
            float angle = Vector3.Angle(targetCamera.forward, toMenu);

            // Reposition if too far off or player looking away
            if (distance > lazyFollowThreshold || angle > 90f - rotationDeadzone)
            {
                needsReposition = true;
            }

            if (needsReposition)
            {
                UpdateTargetTransform();

                // Stop repositioning when close enough
                float currentDistance = Vector3.Distance(transform.position, targetPosition);
                if (currentDistance < 0.05f)
                {
                    needsReposition = false;
                }
            }
        }

        private Vector3 CalculateIdealPosition()
        {
            Vector3 forward = targetCamera.forward;
            if (horizontalRotationOnly)
            {
                forward.y = 0;
                forward.Normalize();
            }

            return targetCamera.position
                + forward * forwardDistance
                + Vector3.up * verticalOffset
                + targetCamera.right * horizontalOffset;
        }

        private void ApplySmoothing()
        {
            // Smooth position
            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                positionSmoothSpeed * Time.deltaTime
            );

            // Smooth rotation with deadzone
            float angleDiff = Quaternion.Angle(transform.rotation, targetRotation);
            if (angleDiff > rotationDeadzone || needsReposition)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSmoothSpeed * Time.deltaTime
                );
            }
        }

        /// <summary>
        /// Immediately snap to ideal position (useful when opening menu).
        /// </summary>
        public void SnapToPosition()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main?.transform;
            }

            if (targetCamera == null)
            {
                Debug.LogWarning("[FollowPlayerView] No camera found for SnapToPosition");
                return;
            }

            UpdateTargetTransform();
            transform.position = targetPosition;
            transform.rotation = targetRotation;

            Debug.Log($"[FollowPlayerView] Snapped to position: {targetPosition}, rotation: {targetRotation.eulerAngles}");
        }

        /// <summary>
        /// Set the target camera at runtime.
        /// </summary>
        public void SetTargetCamera(Transform camera)
        {
            targetCamera = camera;
            SnapToPosition();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (targetCamera == null && Camera.main != null)
            {
                targetCamera = Camera.main.transform;
            }

            if (targetCamera == null) return;

            // Draw ideal position
            Vector3 idealPos = CalculateIdealPosition();
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(idealPos, 0.05f);
            Gizmos.DrawLine(targetCamera.position, idealPos);
        }
#endif
    }
}
