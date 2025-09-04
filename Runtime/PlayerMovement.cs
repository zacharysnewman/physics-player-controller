using UnityEngine;

namespace ZacharysNewman.PPC
{
    public class PlayerMovement
    {
        private Rigidbody rb;
        private Transform transform;
        private bool isGrounded;
        private Vector3 groundNormal;

        // Settings
        private float walkSpeed;
        private float runSpeed;
        private float acceleration;
        private float deceleration;
        private float reverseDeceleration;
        private float maxVelocityChange;

        public Vector3 TargetVelocity { get; private set; }
        public Vector3 CurrentVelocity { get; private set; }

        // Jump settings
        private float jumpForce;
        private float jumpBufferTime;
        private float coyoteTime;
        private bool debugInput;

        // Jump state
        private float jumpBufferTimer;
        private float coyoteTimer;
        private float jumpApexHeight;
        private bool isJumping;
        private bool wasGrounded;
        private bool wasJumpPressed;

        public float JumpBufferTimer => jumpBufferTimer;
        public float CoyoteTimer => coyoteTimer;
        public float JumpApexHeight => jumpApexHeight;
        public bool IsJumping => isJumping;

        public PlayerMovement(Rigidbody rigidbody, Transform playerTransform, float walkSpd, float runSpd, float accel, float decel, float revDecel, float maxVelChange, float jumpF, float jumpBufTime, float coyTime, bool debug)
        {
            rb = rigidbody;
            transform = playerTransform;
            walkSpeed = walkSpd;
            runSpeed = runSpd;
            acceleration = accel;
            deceleration = decel;
            reverseDeceleration = revDecel;
            maxVelocityChange = maxVelChange;
            jumpForce = jumpF;
            jumpBufferTime = jumpBufTime;
            coyoteTime = coyTime;
            debugInput = debug;
        }

        public void UpdateGrounded(bool grounded, Vector3 normal)
        {
            isGrounded = grounded;
            groundNormal = normal;
        }

        public void HandleMovement(Vector2 moveInput, bool runInput, Camera camera)
        {
            // Get camera forward and right for relative movement
            Vector3 cameraForward = camera.transform.forward;
            cameraForward.y = 0f;
            cameraForward.Normalize();

            Vector3 cameraRight = camera.transform.right;
            cameraRight.y = 0f;
            cameraRight.Normalize();

            // Convert input to world space
            Vector3 moveDirection = cameraForward * moveInput.y + cameraRight * moveInput.x;

            // Project onto ground plane
            if (isGrounded)
            {
                moveDirection = moveDirection - Vector3.Project(moveDirection, groundNormal);
            }

            // Compute target velocity
            float speed = runInput ? runSpeed : walkSpeed;
            TargetVelocity = moveDirection * speed;

            // Smooth velocity
            Vector3 velocityChange = TargetVelocity - rb.linearVelocity;
            velocityChange.y = 0f; // Don't affect vertical velocity

            // Limit velocity change
            if (velocityChange.magnitude > maxVelocityChange)
            {
                velocityChange = velocityChange.normalized * maxVelocityChange;
            }

            // Apply acceleration/deceleration with faster reverse deceleration
            Vector3 currentHorizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            Vector3 targetHorizontalVelocity = new Vector3(TargetVelocity.x, 0, TargetVelocity.z);
            float dot = Vector3.Dot(currentHorizontalVelocity.normalized, targetHorizontalVelocity.normalized);
            float accel;
            if (moveInput.magnitude > 0.1f)
            {
                accel = (dot < -0.1f) ? reverseDeceleration : acceleration;
            }
            else
            {
                accel = deceleration;
            }
            velocityChange = Vector3.MoveTowards(Vector3.zero, velocityChange, accel * Time.fixedDeltaTime);

            // Apply to rigidbody
            rb.AddForce(velocityChange, ForceMode.VelocityChange);

            CurrentVelocity = rb.linearVelocity;
        }

        public void HandleJump(bool jumpInput)
        {
            // Update timers
            if (jumpBufferTimer > 0)
            {
                jumpBufferTimer -= Time.deltaTime;
            }
            if (coyoteTimer > 0)
            {
                coyoteTimer -= Time.deltaTime;
            }

            // Set jump buffer only on initial press (not while held)
            if (jumpInput && !wasJumpPressed && jumpBufferTimer <= 0)
            {
                jumpBufferTimer = jumpBufferTime;
                if (debugInput)
                {
                    Debug.Log($"PlayerMovement: Jump buffer set. Timer: {jumpBufferTimer}");
                }
            }

            // Coyote time starts when leaving ground
            if (!isGrounded && wasGrounded)
            {
                coyoteTimer = coyoteTime;
                if (debugInput)
                {
                    Debug.Log($"PlayerMovement: Coyote time started. Timer: {coyoteTimer}");
                }
            }

            // Perform jump
            bool canJump = (isGrounded || coyoteTimer > 0) && jumpBufferTimer > 0;
            if (debugInput && jumpInput && !wasJumpPressed)
            {
                Debug.Log($"PlayerMovement: Jump input detected. Can jump: {canJump}, IsGrounded: {isGrounded}, CoyoteTimer: {coyoteTimer}, JumpBufferTimer: {jumpBufferTimer}");
            }

            if (canJump)
            {
                // Reset vertical velocity
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                // Apply jump force
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                // Reset timers
                jumpBufferTimer = 0;
                coyoteTimer = 0;
                isJumping = true;
                jumpApexHeight = transform.position.y + (jumpForce * jumpForce) / (2 * Physics.gravity.magnitude); // Approximate apex

                if (debugInput)
                {
                    Debug.Log($"PlayerMovement: Jump performed with force {jumpForce}. New velocity: {rb.linearVelocity}");
                }
            }

            // Check if landed
            if (isGrounded && isJumping && rb.linearVelocity.y <= 0)
            {
                isJumping = false;
            }

            wasGrounded = isGrounded;
            wasJumpPressed = jumpInput;
        }
    }
}