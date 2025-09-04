using UnityEngine;

namespace ZacharysNewman.PPC
{
    public class GroundChecker
    {
        private Transform groundCheck;
        private Transform ceilingCheck;
        private float groundCheckRadius;
        private float groundCheckDistance;
        private LayerMask groundLayerMask;
        private float ceilingCheckRadius;
        private float ceilingCheckDistance;
        private LayerMask ceilingLayerMask;
        private bool debugInput;
        private CapsuleCollider capsule;

        public bool IsGrounded { get; private set; }
        public Vector3 GroundNormal { get; private set; }
        public float GroundSlopeAngle { get; private set; }

        // Wall detection
        public bool IsTouchingWall { get; private set; }
        public Vector3 WallNormal { get; private set; }

        private string GetLayerNames(LayerMask mask)
        {
            string names = "";
            for (int i = 0; i < 32; i++)
            {
                if ((mask.value & (1 << i)) != 0)
                {
                    names += UnityEngine.LayerMask.LayerToName(i) + ",";
                }
            }
            return names.TrimEnd(',');
        }

        public GroundChecker(Transform gCheck, Transform cCheck, float gRadius, float gDist, LayerMask gMask, float cRadius, float cDist, LayerMask cMask, bool debug, CapsuleCollider cap)
        {
            groundCheck = gCheck;
            ceilingCheck = cCheck;
            groundCheckRadius = gRadius;
            groundCheckDistance = gDist;
            groundLayerMask = gMask;
            ceilingCheckRadius = cRadius;
            ceilingCheckDistance = cDist;
            ceilingLayerMask = cMask;
            debugInput = debug;
            capsule = cap;
        }

        public void CheckGround()
        {
            RaycastHit hit;

            // Debug: Draw the actual sphere cast ray
            if (debugInput)
            {
                UnityEngine.Debug.DrawRay(groundCheck.position, Vector3.down * groundCheckDistance, Color.yellow, 0.1f);
                UnityEngine.Debug.DrawLine(groundCheck.position, groundCheck.position + Vector3.down * groundCheckDistance, Color.yellow, 0.1f);
            }

            // Try Raycast first (simpler)
            if (Physics.Raycast(groundCheck.position, Vector3.down, out hit, groundCheckDistance, groundLayerMask))
            {
                IsGrounded = true;
                GroundNormal = hit.normal;
                GroundSlopeAngle = Vector3.Angle(Vector3.up, GroundNormal);

                // Debug logging
                if (debugInput)
                {
                    UnityEngine.Debug.Log($"Ground detected with Raycast! Position: {groundCheck.position}, Distance: {hit.distance}, Hit point: {hit.point}, Normal: {GroundNormal}");
                }
            }
            // Fallback: Try SphereCast
            else if (Physics.SphereCast(groundCheck.position, groundCheckRadius, Vector3.down, out hit, groundCheckDistance, groundLayerMask))
            {
                IsGrounded = true;
                GroundNormal = hit.normal;
                GroundSlopeAngle = Vector3.Angle(Vector3.up, GroundNormal);

                // Debug logging
                if (debugInput)
                {
                    UnityEngine.Debug.Log($"Ground detected with SphereCast! Position: {groundCheck.position}, Distance: {hit.distance}, Hit point: {hit.point}, Normal: {GroundNormal}");
                }
            }
            else
            {
                // Fallback: Try OverlapSphere at the bottom of the cast
                Vector3 checkPosition = groundCheck.position + Vector3.down * groundCheckDistance;
                Collider[] colliders = Physics.OverlapSphere(checkPosition, groundCheckRadius, groundLayerMask);

                if (colliders.Length > 0)
                {
                    // Simple check: ensure at least one collider is not the player itself
                    bool hasValidCollider = false;
                    foreach (Collider col in colliders)
                    {
                        if (col != null && col.gameObject != groundCheck.parent.gameObject)
                        {
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

                        if (debugInput)
                        {
                            UnityEngine.Debug.Log($"Ground detected with OverlapSphere! Position: {checkPosition}, Colliders: {colliders.Length}");
                        }
                    }
                }
                else
                {
                    IsGrounded = false;
                    GroundNormal = Vector3.up;
                    GroundSlopeAngle = 0f;

                    // Debug logging (more frequent for troubleshooting)
                    if (debugInput)
                    {
                        UnityEngine.Debug.Log($"No ground detected. GroundCheck worldPos: {groundCheck.position}, OverlapSphere position: {checkPosition}, distance: {groundCheckDistance}, radius: {groundCheckRadius}, layerNames: {GetLayerNames(groundLayerMask)}");
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
            float wallCheckDistance = groundCheckRadius * 0.8f; // Slightly less than radius

            IsTouchingWall = false;
            WallNormal = Vector3.zero;

            foreach (Vector3 direction in directions)
            {
                RaycastHit hit;
                Vector3 checkPosition = groundCheck.position + direction * (capsule.radius * 0.9f);

                if (Physics.Raycast(checkPosition, direction, out hit, wallCheckDistance, groundLayerMask))
                {
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

            if (debugInput && IsTouchingWall)
            {
                UnityEngine.Debug.Log($"Wall detected! Normal: {WallNormal}");
            }
        }

        public void CheckCeiling()
        {
            // For ceiling check, we can use a similar spherecast upward
            RaycastHit hit;
            if (Physics.SphereCast(ceilingCheck.position, ceilingCheckRadius, Vector3.up, out hit, ceilingCheckDistance, ceilingLayerMask))
            {
                // Store if ceiling is hit, but for now just check
            }
        }
    }
}