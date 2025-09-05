using UnityEngine;

namespace ZacharysNewman.PPC
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(GroundChecker))]
    public class PlayerJump : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private PlayerJumpConfig config;

        // Component references (auto-assigned)
        private GroundChecker groundChecker;

        // Component references
        private Rigidbody rb;

        // Jump state
        private float jumpBufferTimer;
        private float coyoteTimer;
        private float jumpApexHeight;
        private bool isJumping;
        private bool wasGrounded;
        private bool wasJumpPressed;

        // Configuration fallbacks
        private float jumpForce;
        private float jumpBufferTime;
        private float coyoteTime;
        private bool debugLogging;

        // Public properties for other components
        public float JumpBufferTimer => jumpBufferTimer;
        public float CoyoteTimer => coyoteTimer;
        public float JumpApexHeight => jumpApexHeight;
        public bool IsJumping => isJumping;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            groundChecker = GetComponent<GroundChecker>();

            // Apply config values
            if (config != null)
            {
                // Config values are used in jump calculations
            }
        }

        private void Update()
        {
            if (groundChecker == null) return;

            // Update timers
            if (jumpBufferTimer > 0)
            {
                jumpBufferTimer -= Time.deltaTime;
            }
            if (coyoteTimer > 0)
            {
                coyoteTimer -= Time.deltaTime;
            }

            // Coyote time starts when leaving ground
            if (!groundChecker.IsGrounded && wasGrounded)
            {
                coyoteTimer = coyoteTime;
                if (debugLogging)
                {
                    Debug.Log($"PlayerJump: Coyote time started. Timer: {coyoteTimer}");
                }
            }

            // Check if landed
            if (groundChecker.IsGrounded && isJumping && rb.linearVelocity.y <= 0)
            {
                isJumping = false;
            }

            wasGrounded = groundChecker.IsGrounded;
        }

        public void HandleJump(bool jumpInput)
        {
            if (groundChecker == null) return;

            // Set jump buffer only on initial press (not while held)
            if (jumpInput && !wasJumpPressed && jumpBufferTimer <= 0)
            {
                jumpBufferTimer = config != null ? config.JumpBufferTime : jumpBufferTime;
                if ((config != null ? config.DebugLogging : debugLogging))
                {
                    Debug.Log($"PlayerJump: Jump buffer set. Timer: {jumpBufferTimer}");
                }
            }

            // Coyote time starts when leaving ground
            if (!groundChecker.IsGrounded && wasGrounded)
            {
                coyoteTimer = config != null ? config.CoyoteTime : coyoteTime;
                if ((config != null ? config.DebugLogging : debugLogging))
                {
                    Debug.Log($"PlayerJump: Coyote time started. Timer: {coyoteTimer}");
                }
            }

            // Perform jump
            bool canJump = (groundChecker.IsGrounded || coyoteTimer > 0) && jumpBufferTimer > 0;
            if (debugLogging && jumpInput && !wasJumpPressed)
            {
                Debug.Log($"PlayerJump: Jump input detected. Can jump: {canJump}, IsGrounded: {groundChecker.IsGrounded}, CoyoteTimer: {coyoteTimer}, JumpBufferTimer: {jumpBufferTimer}");
            }

            if (canJump)
            {
                PerformJump();
            }

            bool debugEnabled = config != null ? config.DebugLogging : debugLogging;
            if (debugEnabled && jumpInput && !wasJumpPressed)
            {
                Debug.Log($"PlayerJump: Jump input detected. Can jump: {canJump}, IsGrounded: {groundChecker.IsGrounded}, CoyoteTimer: {coyoteTimer}, JumpBufferTimer: {jumpBufferTimer}");
            }

            wasJumpPressed = jumpInput;
        }

        private void PerformJump()
        {
            float force = config != null ? config.JumpForce : jumpForce;
            bool debugEnabled = config != null ? config.DebugLogging : debugLogging;

            // Reset vertical velocity
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            // Apply jump force
            rb.AddForce(Vector3.up * force, ForceMode.Impulse);
            // Reset timers
            jumpBufferTimer = 0;
            coyoteTimer = 0;
            isJumping = true;
            jumpApexHeight = transform.position.y + (force * force) / (2 * Physics.gravity.magnitude); // Approximate apex

            if (debugEnabled)
            {
                Debug.Log($"PlayerJump: Jump performed with force {force}. New velocity: {rb.linearVelocity}");
            }
        }

        // Public methods for configuration
        public void SetJumpParameters(float force, float bufferTime, float coyote)
        {
            jumpForce = force;
            jumpBufferTime = bufferTime;
            coyoteTime = coyote;
        }

        public void SetDebugLogging(bool enabled)
        {
            debugLogging = enabled;
        }
    }
}