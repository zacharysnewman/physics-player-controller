using UnityEngine;

namespace ZacharysNewman.PPC
{
    [RequireComponent(typeof(BoxCollider))]
    public class GravityLauncher : MonoBehaviour
    {
        [Header("Launcher Settings")]
        [SerializeField] private float strength = 10f;
        [SerializeField] private Vector3 launchDirection = Vector3.up;
        [SerializeField] private LayerMask affectedLayers = -1; // All layers by default

        private BoxCollider boxCollider;

        private void Awake()
        {
            boxCollider = GetComponent<BoxCollider>();
            boxCollider.isTrigger = true;
        }

        private void OnTriggerStay(Collider other)
        {
            // Check if the object is on an affected layer
            if ((affectedLayers.value & (1 << other.gameObject.layer)) == 0)
                return;

            // Only affect objects with Rigidbody
            if (!other.TryGetComponent<Rigidbody>(out var rb))
                return;

            // Don't affect kinematic rigidbodies
            if (rb.isKinematic)
                return;

            // Apply force in the launch direction
            rb.AddForce(launchDirection.normalized * strength, ForceMode.Force);

            // Debug visualization: draw line showing launch direction
            Debug.DrawLine(other.transform.position, other.transform.position + launchDirection.normalized * 2f, Color.blue, 1.0f);

            // Debug visualization: draw force vector (scaled for visibility)
            Vector3 forceVector = launchDirection.normalized * strength * 0.01f;
            Debug.DrawLine(other.transform.position, other.transform.position + forceVector, Color.green, 1.0f);
        }

        private void OnDrawGizmosSelected()
        {
            if (boxCollider != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);

                // Draw launch direction arrow
                Gizmos.color = Color.blue;
                Vector3 start = transform.position + boxCollider.center;
                Vector3 end = start + launchDirection.normalized * 2f;
                Gizmos.DrawLine(start, end);

                // Draw arrowhead
                Vector3 right = Vector3.Cross(launchDirection.normalized, Vector3.up).normalized * 0.3f;
                if (right == Vector3.zero) right = Vector3.Cross(launchDirection.normalized, Vector3.right).normalized * 0.3f;
                Vector3 left = -right;

                Gizmos.DrawLine(end, end + right - launchDirection.normalized * 0.3f);
                Gizmos.DrawLine(end, end + left - launchDirection.normalized * 0.3f);
            }
        }
    }
}