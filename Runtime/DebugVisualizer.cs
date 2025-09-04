using UnityEngine;

namespace ZacharysNewman.PPC
{
    public class DebugVisualizer
    {
        private Transform transform;
        private CapsuleCollider capsule;
        private Transform groundCheck;
        private Transform ceilingCheck;
        private bool isGrounded;
        private Vector3 groundNormal;
        private Vector3 currentVelocity;
        private Vector3 targetVelocity;

        private bool visualizeBounds;
        private bool debugInput;
        private bool visualizeGroundCeilingChecks;
        private bool visualizeVelocity;
        private bool visualizeJump;

        // Jump debug info
        private float jumpBufferTimer;
        private float coyoteTimer;
        private float jumpApexHeight;
        private bool isJumping;

        public DebugVisualizer(Transform playerTransform, CapsuleCollider cap, Transform gCheck, Transform cCheck, bool visBounds, bool dbgInput, bool visChecks, bool visVel, bool visJump, float groundCheckRadius)
        {
            transform = playerTransform;
            capsule = cap;
            groundCheck = gCheck;
            ceilingCheck = cCheck;
            visualizeBounds = visBounds;
            debugInput = dbgInput;
            visualizeGroundCeilingChecks = visChecks;
            visualizeVelocity = visVel;
            visualizeJump = visJump;
        }

        public void UpdateDebugInfo(bool grounded, Vector3 gNormal, Vector3 currVel, Vector3 targVel, float jumpBufTimer, float coyTimer, float jumpApex, bool jumping)
        {
            isGrounded = grounded;
            groundNormal = gNormal;
            currentVelocity = currVel;
            targetVelocity = targVel;
            jumpBufferTimer = jumpBufTimer;
            coyoteTimer = coyTimer;
            jumpApexHeight = jumpApex;
            isJumping = jumping;
        }

        public void LogInput(Vector2 moveInput, bool runInput, bool crouchInput, bool jumpInput, bool interactInput)
        {
            if (debugInput)
            {
                Debug.Log($"Move Input: {moveInput}, Run: {runInput}, Crouch: {crouchInput}, Jump: {jumpInput}, Interact: {interactInput}");
            }
        }

        public void DrawGizmos()
        {
            if (!Application.isPlaying) return;

            if (visualizeBounds)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position + capsule.center, capsule.radius);
                Gizmos.DrawWireSphere(transform.position + capsule.center + Vector3.up * (capsule.height / 2f - capsule.radius), capsule.radius);
                Gizmos.DrawWireSphere(transform.position + capsule.center + Vector3.down * (capsule.height / 2f - capsule.radius), capsule.radius);
            }

            if (visualizeGroundCeilingChecks)
            {
                // Ground check
                Gizmos.color = isGrounded ? Color.blue : Color.red;
                Gizmos.DrawWireSphere(groundCheck.position + Vector3.down * 0.1f, 0.4f); // Using default values, could pass settings

                // Ceiling check
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(ceilingCheck.position + Vector3.up * 0.1f, 0.4f);

                // Ground normal
                if (isGrounded)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(groundCheck.position, groundCheck.position + groundNormal);
                }
            }

            if (visualizeVelocity)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, transform.position + currentVelocity);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, transform.position + targetVelocity);
            }

            if (visualizeJump)
            {
                // Draw jump apex line
                if (isJumping)
                {
                    Gizmos.color = Color.green;
                    Vector3 apexPosition = new Vector3(transform.position.x, jumpApexHeight, transform.position.z);
                    Gizmos.DrawLine(transform.position, apexPosition);
                    Gizmos.DrawSphere(apexPosition, 0.1f);
                }

                // Draw jump buffer and coyote time indicators
                if (jumpBufferTimer > 0)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(transform.position + Vector3.up * 2f, jumpBufferTimer * 0.5f);
                }
                if (coyoteTimer > 0)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawSphere(transform.position + Vector3.up * 2.5f, coyoteTimer * 0.5f);
                }
            }
        }
    }
}