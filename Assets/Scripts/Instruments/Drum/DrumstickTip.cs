using UnityEngine;

namespace SoloBandStudio.Instruments.Drum
{
    /// <summary>
    /// Attach to the tip of a drumstick to detect collisions with DrumPads.
    /// Calculates hit velocity for dynamic volume control.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DrumstickTip : MonoBehaviour
    {
        [Header("Velocity Settings")]
        [SerializeField] private float minVelocityForHit = 0.5f;
        [SerializeField] private float maxVelocityForFullHit = 5f;

        [Header("Cooldown")]
        [SerializeField] private float hitCooldown = 0.05f;

        [Header("Debug")]
        [SerializeField] private bool debugLog = false;

        private Rigidbody parentRigidbody;
        private Vector3 lastPosition;
        private Vector3 velocity;
        private float lastHitTime;

        // Tag or layer to identify drum pads
        private const string DRUM_PAD_TAG = "DrumPad";

        private void Start()
        {
            // Find rigidbody in parent hierarchy
            parentRigidbody = GetComponentInParent<Rigidbody>();

            // Ensure collider is trigger
            var col = GetComponent<Collider>();
            col.isTrigger = true;

            lastPosition = transform.position;
        }

        private void FixedUpdate()
        {
            // Calculate velocity manually for more accurate hit detection
            // (Rigidbody velocity can be unreliable during XR grab)
            velocity = (transform.position - lastPosition) / Time.fixedDeltaTime;
            lastPosition = transform.position;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Cooldown check
            if (Time.time - lastHitTime < hitCooldown) return;

            // Try to find DrumPad component
            var drumPad = other.GetComponent<DrumPad>();
            if (drumPad == null)
            {
                drumPad = other.GetComponentInParent<DrumPad>();
            }

            if (drumPad == null) return;

            // Calculate hit velocity (use the faster of manual or rigidbody velocity)
            float hitSpeed = velocity.magnitude;
            if (parentRigidbody != null)
            {
                hitSpeed = Mathf.Max(hitSpeed, parentRigidbody.linearVelocity.magnitude);
            }

            if (debugLog)
            {
                Debug.Log($"[DrumstickTip] Hit {drumPad.PartType} with velocity {hitSpeed:F2}");
            }

            // Check minimum velocity
            if (hitSpeed < minVelocityForHit) return;

            // Calculate normalized velocity (0-1)
            float normalizedVelocity = Mathf.InverseLerp(minVelocityForHit, maxVelocityForFullHit, hitSpeed);
            normalizedVelocity = Mathf.Clamp01(normalizedVelocity);

            // Apply minimum velocity floor for audible hits
            float finalVelocity = Mathf.Lerp(0.3f, 1f, normalizedVelocity);

            // Hit the drum pad
            drumPad.HitFromCollision(finalVelocity);
            lastHitTime = Time.time;

            if (debugLog)
            {
                Debug.Log($"[DrumstickTip] Final hit velocity: {finalVelocity:F2}");
            }
        }

        /// <summary>
        /// Get the current tip velocity (useful for debugging).
        /// </summary>
        public Vector3 GetVelocity() => velocity;
    }
}
