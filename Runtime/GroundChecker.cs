using UnityEngine;

namespace ZacharysNewman.PPC
{
    [RequireComponent(typeof(CapsuleCollider))]
    public class GroundChecker : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GroundCheckerConfig config;

        [Header("Check Transforms")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private Transform ceilingCheck;


        [SerializeField] private bool debugLogging = false;

        // Component references
        private CapsuleCollider capsule;

        // Ground detection state
        public bool IsGrounded { get; private set; }
        public Vector3 GroundNormal { get; private set; }
        public float GroundSlopeAngle { get; private set; }

        // Wall detection
        public bool IsTouchingWall { get; private set; }
        public Vector3 WallNormal { get; private set; }

        private void Awake()
        {
            capsule = GetComponent<CapsuleCollider>();

            // Create child transforms if not assigned
            if (groundCheck == null)
            {
                groundCheck = new GameObject("GroundCheck").transform;
                groundCheck.parent = transform;
                groundCheck.localPosition = capsule.center + Vector3.down * (capsule.height / 2f - capsule.radius * 0.1f);
            }
            if (ceilingCheck == null)
            {
                ceilingCheck = new GameObject("CeilingCheck").transform;
                ceilingCheck.parent = transform;
                ceilingCheck.localPosition = Vector3.up * (capsule.height / 2f - capsule.radius);
            }

            // Ensure config is set
            if (config == null)
            {
                Debug.LogError("GroundChecker: Config is required. Please assign a GroundCheckerConfig.");
            }
        }

        private void Update()
        {
            CheckGround();
            CheckCeiling();
        }

        public void CheckGround()
        {
            RaycastHit hit;

            // Debug: Draw the actual sphere cast ray
            if (debugLogging)
            {
                float distance = config.GroundCheckDistance;
                Debug.DrawRay(groundCheck.position, Vector3.down * distance, Color.yellow, 0.1f);
                Debug.DrawLine(groundCheck.position, groundCheck.position + Vector3.down * distance, Color.yellow, 0.1f);
            }

            // Try Raycast first (simpler)
            float checkDistance = config.GroundCheckDistance;
            LayerMask layerMask = config.GroundLayerMask;
            if (Physics.Raycast(groundCheck.position, Vector3.down, out hit, checkDistance, layerMask))
            {
                IsGrounded = true;
                GroundNormal = hit.normal;
                GroundSlopeAngle = Vector3.Angle(Vector3.up, GroundNormal);

                // Check for player self-collision
                CheckForPlayerCollision(hit.collider.gameObject, "ground");

                // Debug logging
                if (debugLogging)
                {
                    Debug.Log($"Ground detected with Raycast! Position: {groundCheck.position}, Distance: {hit.distance}, Hit point: {hit.point}, Normal: {GroundNormal}");
                }
            }
            // Fallback: Try SphereCast
            else if (Physics.SphereCast(groundCheck.position, config.GroundCheckRadius, Vector3.down, out hit, checkDistance, layerMask))
            {
                IsGrounded = true;
                GroundNormal = hit.normal;
                GroundSlopeAngle = Vector3.Angle(Vector3.up, GroundNormal);

                // Check for player self-collision
                CheckForPlayerCollision(hit.collider.gameObject, "ground");

                // Debug logging
                if (debugLogging)
                {
                    // Debug.Log($"Ground detected with SphereCast! Position: {groundCheck.position}, Distance: {hit.distance}, Hit point: {hit.point}, Normal: {GroundNormal}");
                }
            }
            else
            {
                // Fallback: Try OverlapSphere at the bottom of the cast
                Vector3 checkPosition = groundCheck.position + Vector3.down * checkDistance;
                Collider[] colliders = Physics.OverlapSphere(checkPosition, config.GroundCheckRadius, layerMask);

                if (colliders.Length > 0)
                {
                    // Simple check: ensure at least one collider is not the player itself
                    bool hasValidCollider = false;
                    foreach (Collider col in colliders)
                    {
                        if (col != null && col.gameObject != gameObject)
                        {
                            // Check for player collision warning
                            CheckForPlayerCollision(col.gameObject, "ground");
                            hasValidCollider = true;
                            break;
                        }
                    }

                    if (hasValidCollider)
                    {
                        IsGrounded = true;
                        // For overlap, we can't easily get the normal, so use up
                        GroundNormal = Vector3.up;
                        GroundSlopeAngle = 0f;

                        if (debugLogging)
                        {
                            Debug.Log($"Ground detected with OverlapSphere! Position: {checkPosition}, Colliders: {colliders.Length}");
                        }
                    }
                }
                else
                {
                    IsGrounded = false;
                    GroundNormal = Vector3.up;
                    GroundSlopeAngle = 0f;

                    // Debug logging (more frequent for troubleshooting)
                    if (debugLogging)
                    {
                        Debug.Log($"No ground detected. GroundCheck worldPos: {groundCheck.position}, OverlapSphere position: {checkPosition}, distance: {config.GroundCheckDistance}, radius: {config.GroundCheckRadius}, layerNames: {GetLayerNames(config.GroundLayerMask)}");
                    }
                }
            }

            // Wall detection
            CheckWall();
        }

        private void CheckWall()
        {
            // Check for walls in all horizontal directions
            Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            float wallCheckDistance = config.GroundCheckRadius * 0.8f; // Slightly less than radius

            IsTouchingWall = false;
            WallNormal = Vector3.zero;

            foreach (Vector3 direction in directions)
            {
                RaycastHit hit;
                Vector3 checkPosition = groundCheck.position + direction * (capsule.radius * 0.9f);

                if (Physics.Raycast(checkPosition, direction, out hit, wallCheckDistance, config.GroundLayerMask))
                {
                    // Check for player self-collision
                    CheckForPlayerCollision(hit.collider.gameObject, "wall");

                    // Make sure it's not the ground (check angle)
                    float angle = Vector3.Angle(Vector3.up, hit.normal);
                    if (angle > 45f) // Not ground, must be wall
                    {
                        IsTouchingWall = true;
                        WallNormal = hit.normal;
                        break;
                    }
                }
            }

            if (debugLogging && IsTouchingWall)
            {
                Debug.Log($"Wall detected! Normal: {WallNormal}");
            }
        }

        public void CheckCeiling()
        {
            // For ceiling check, we can use a similar spherecast upward
            RaycastHit hit;
            if (Physics.SphereCast(ceilingCheck.position, config.CeilingCheckRadius, Vector3.up, out hit, config.CeilingCheckDistance, config.CeilingLayerMask))
            {
                // Check for player self-collision
                CheckForPlayerCollision(hit.collider.gameObject, "ceiling");
            }
        }

        private string GetLayerNames(LayerMask mask)
        {
            string names = "";
            for (int i = 0; i < 32; i++)
            {
                if ((mask.value & (1 << i)) != 0)
                {
                    names += LayerMask.LayerToName(i) + ",";
                }
            }
            return names.TrimEnd(',');
        }

        // Public methods for configuration

        public void SetDebugLogging(bool enabled)
        {
            debugLogging = enabled;
        }

        private void CheckForPlayerCollision(GameObject hitObject, string checkType)
        {
            // Debug: Log what we hit (only if debug logging is enabled)
            if (debugLogging)
            {
                Debug.Log($"GroundChecker: {checkType} check hit object '{hitObject.name}' with tag '{hitObject.tag}' on layer {hitObject.layer}");
            }

            if (hitObject.CompareTag("Player"))
            {
                Debug.LogWarning($"GroundChecker: {checkType} check hit object tagged 'Player' ({hitObject.name}). " +
                    "Consider putting the player object on a separate layer excluded from ground/ceiling checks to prevent self-collision. " +
                    "Current layer mask may include the player's layer.");
            }
        }
    }
}