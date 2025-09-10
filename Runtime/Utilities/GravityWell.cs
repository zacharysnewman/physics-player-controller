using UnityEngine;

namespace ZacharysNewman.PPC
{
    [RequireComponent(typeof(SphereCollider))]
    public class GravityWell : MonoBehaviour
    {
        [Header("Gravity Settings")]
        [SerializeField] private float strength = 10f;
        [SerializeField] private float maxDistance = 5f;
        [SerializeField] private bool useInverseSquare = true;
        [SerializeField] private LayerMask affectedLayers = -1; // All layers by default

        private SphereCollider sphereCollider;

        private void Awake()
        {
            sphereCollider = GetComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = maxDistance;
        }

        private void OnTriggerStay(Collider other)
        {
            // Debug log to see if trigger is being called
            Debug.Log($"GravityWell affecting: {other.gameObject.name}");

            // Check if the object is on an affected layer
            if ((affectedLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                Debug.Log($"Object {other.gameObject.name} not on affected layer");
                return;
            }

            // Only affect objects with Rigidbody
            if (!other.TryGetComponent<Rigidbody>(out var rb))
            {
                Debug.Log($"Object {other.gameObject.name} has no Rigidbody");
                return;
            }

            // Don't affect kinematic rigidbodies
            if (rb.isKinematic)
            {
                Debug.Log($"Object {other.gameObject.name} has kinematic Rigidbody");
                return;
            }

            // Calculate direction and distance
            Vector3 direction = (transform.position - other.transform.position).normalized;
            float distance = Vector3.Distance(transform.position, other.transform.position);

            // Calculate force magnitude
            float forceMagnitude = strength;
            if (useInverseSquare && distance > 0.1f)
            {
                forceMagnitude = strength / (distance * distance);
            }

            // Apply force towards the center
            rb.AddForce(direction * forceMagnitude, ForceMode.Force);

            // Debug visualization: draw line to affected object (longer duration)
            Debug.DrawLine(transform.position, other.transform.position, Color.red, 1.0f);

            // Debug visualization: draw force vector (scaled for better visibility)
            Vector3 forceVector = direction * Mathf.Min(forceMagnitude * 0.1f, 2.0f); // Scale and clamp for visibility
            Debug.DrawLine(other.transform.position, other.transform.position + forceVector, Color.green, 1.0f);

            Debug.Log($"Applied force {forceMagnitude} to {other.gameObject.name} at distance {distance}");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, maxDistance);
        }
    }
}