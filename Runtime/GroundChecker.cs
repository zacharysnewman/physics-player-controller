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

        public bool IsGrounded { get; private set; }
        public Vector3 GroundNormal { get; private set; }
        public float GroundSlopeAngle { get; private set; }

        public GroundChecker(Transform gCheck, Transform cCheck, float gRadius, float gDist, LayerMask gMask, float cRadius, float cDist, LayerMask cMask)
        {
            groundCheck = gCheck;
            ceilingCheck = cCheck;
            groundCheckRadius = gRadius;
            groundCheckDistance = gDist;
            groundLayerMask = gMask;
            ceilingCheckRadius = cRadius;
            ceilingCheckDistance = cDist;
            ceilingLayerMask = cMask;
        }

        public void CheckGround()
        {
            RaycastHit hit;
            if (Physics.SphereCast(groundCheck.position, groundCheckRadius, Vector3.down, out hit, groundCheckDistance, groundLayerMask))
            {
                IsGrounded = true;
                GroundNormal = hit.normal;
                GroundSlopeAngle = Vector3.Angle(Vector3.up, GroundNormal);
            }
            else
            {
                IsGrounded = false;
                GroundNormal = Vector3.up;
                GroundSlopeAngle = 0f;
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