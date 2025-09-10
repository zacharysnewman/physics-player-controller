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
        public Transform GroundObject { get; private set; }

        // Wall detection
        public bool IsTouchingWall { get; private set; }
        public Vector3 WallNormal { get; private set; }

        // Ceiling detection
        public bool IsCeilingBlocked { get; private set; }

        // Public access to config
        public LayerMask GetGroundLayerMask() => config.GroundLayerMask;

        private void Awake()
        {
            capsule = GetComponent<CapsuleCollider>();

            // Create child transforms if not assigned
            if (groundCheck == null)
            {
                groundCheck = new GameObject("GroundCheck").transform;
                groundCheck.parent = transform;
                groundCheck.localPosition = capsule.center;
            }
            if (ceilingCheck == null)
            {
                ceilingCheck = new GameObject("CeilingCheck").transform;
                ceilingCheck.parent = transform;
                ceilingCheck.localPosition = capsule.center;
            }

            // Ensure config is set
            if (config == null)
            {
                Debug.LogError("GroundChecker: Config is required. Please assign a GroundCheckerConfig.");
            }
        }

        private void Update()
        {
            // Update check positions based on current capsule
            if (groundCheck != null)
            {
                groundCheck.localPosition = capsule.center;
            }
            if (ceilingCheck != null)
            {
                ceilingCheck.localPosition = capsule.center;
            }

            CheckGround();
            CheckCeiling();
        }

        public void CheckGround()
        {
            float checkDistance = config.GroundCheckDistance;
            LayerMask layerMask = config.GroundLayerMask;
            float radius = capsule.radius;

            RaycastHit closestHit = new RaycastHit();
            bool foundGround = false;
            float minDistance = float.MaxValue;

            // Center raycast
            {
                Vector3 checkPosition = groundCheck.position;

                // Debug: Draw the raycast
                if (debugLogging)
                {
                    Debug.DrawRay(checkPosition, Vector3.down * checkDistance, Color.yellow, 0.1f);
                }

                RaycastHit hit;
                if (Physics.Raycast(checkPosition, Vector3.down, out hit, checkDistance, layerMask))
                {
                    // Check for player self-collision
                    CheckForPlayerCollision(hit.collider.gameObject, "ground");

                    // Track the closest hit
                    if (hit.distance < minDistance)
                    {
                        minDistance = hit.distance;
                        closestHit = hit;
                        foundGround = true;
                    }

                    // Debug logging for center hit
                    if (debugLogging)
                    {
                        Debug.Log($"Ground detected with Center Raycast! Position: {checkPosition}, Distance: {hit.distance}, Hit point: {hit.point}, Normal: {hit.normal}");
                    }
                }
            }

            // Perform 16 raycasts in a circle
            for (int i = 0; i < 16; i++)
            {
                float angle = i * 22.5f * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Vector3 checkPosition = groundCheck.position + offset;

                // Debug: Draw the raycast
                if (debugLogging)
                {
                    Debug.DrawRay(checkPosition, Vector3.down * checkDistance, Color.yellow, 0.1f);
                }

                RaycastHit hit;
                if (Physics.Raycast(checkPosition, Vector3.down, out hit, checkDistance, layerMask))
                {
                    // Check for player self-collision
                    CheckForPlayerCollision(hit.collider.gameObject, "ground");

                    // Track the closest hit
                    if (hit.distance < minDistance)
                    {
                        minDistance = hit.distance;
                        closestHit = hit;
                        foundGround = true;
                    }

                    // Debug logging for each hit
                    if (debugLogging)
                    {
                        Debug.Log($"Ground detected with Raycast {i}! Position: {checkPosition}, Distance: {hit.distance}, Hit point: {hit.point}, Normal: {hit.normal}");
                    }
                }
            }

            if (foundGround)
            {
                GroundNormal = closestHit.normal;
                GroundSlopeAngle = Vector3.Angle(Vector3.up, GroundNormal);
                GroundObject = closestHit.transform;

                // Check if slope is traversable
                IsGrounded = GroundSlopeAngle <= config.MaxSlopeAngle;

                // Debug logging for closest hit
                if (debugLogging)
                {
                    Debug.Log($"Ground detected! Closest hit - Position: {closestHit.point}, Distance: {closestHit.distance}, Normal: {GroundNormal}");
                }
            }
            else
            {
                IsGrounded = false;
                GroundNormal = Vector3.up;
                GroundSlopeAngle = 0f;
                GroundObject = null;

                // Debug logging
                if (debugLogging)
                {
                    Debug.Log($"No ground detected. GroundCheck worldPos: {groundCheck.position}, distance: {config.GroundCheckDistance}, layerNames: {GetLayerNames(config.GroundLayerMask)}");
                }
            }

            // Wall detection
            CheckWall();
        }

        private void CheckWall()
        {
            // Check for walls in all horizontal directions
            Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            float wallCheckDistance = 0.16f; // Fixed wall check distance

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
            float checkDistance = config.CeilingCheckDistance;
            LayerMask layerMask = config.CeilingLayerMask;
            float radius = capsule.radius;

            RaycastHit closestHit = new RaycastHit();
            bool foundCeiling = false;
            float minDistance = float.MaxValue;

            // Center raycast upward
            {
                Vector3 checkPosition = ceilingCheck.position;

                if (debugLogging)
                {
                    Debug.DrawRay(checkPosition, Vector3.up * checkDistance, Color.yellow, 0.1f);
                }

                RaycastHit hit;
                if (Physics.Raycast(checkPosition, Vector3.up, out hit, checkDistance, layerMask))
                {
                    CheckForPlayerCollision(hit.collider.gameObject, "ceiling");

                    if (hit.distance < minDistance)
                    {
                        minDistance = hit.distance;
                        closestHit = hit;
                        foundCeiling = true;
                    }
                }
            }

            // Perform 16 raycasts in a circle upward
            for (int i = 0; i < 16; i++)
            {
                float angle = i * 22.5f * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Vector3 checkPosition = ceilingCheck.position + offset;

                if (debugLogging)
                {
                    Debug.DrawRay(checkPosition, Vector3.up * checkDistance, Color.yellow, 0.1f);
                }

                RaycastHit hit;
                if (Physics.Raycast(checkPosition, Vector3.up, out hit, checkDistance, layerMask))
                {
                    CheckForPlayerCollision(hit.collider.gameObject, "ceiling");

                    if (hit.distance < minDistance)
                    {
                        minDistance = hit.distance;
                        closestHit = hit;
                        foundCeiling = true;
                    }
                }
            }

            IsCeilingBlocked = foundCeiling;
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