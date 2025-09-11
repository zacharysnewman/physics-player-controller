using UnityEngine;
using UnityEngine.Events;

namespace ZacharysNewman.PPC
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(GroundChecker))]
    public class PlayerJump : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private PlayerJumpConfig config;

        [Header("Debug")]
        [SerializeField] private bool debugLogging = false;

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

        // Configuration

        // Public properties for other components
        public float JumpBufferTimer => jumpBufferTimer;
        public float CoyoteTimer => coyoteTimer;
        public float JumpApexHeight => jumpApexHeight;
        public bool IsJumping => isJumping;

        // Events
        public UnityEvent OnJump = new UnityEvent();

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            groundChecker = GetComponent<GroundChecker>();

            // Ensure config is set
            if (config == null)
            {
                Debug.LogError("PlayerJump: Config is required. Please assign a PlayerJumpConfig.");
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
                coyoteTimer = config.CoyoteTime;
                if (debugLogging)
                {
                    if (debugLogging) Debug.Log($"PlayerJump: Coyote time started. Timer: {coyoteTimer}");
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
                jumpBufferTimer = config.JumpBufferTime;
                if (debugLogging)
                {
                    if (debugLogging) Debug.Log($"PlayerJump: Jump buffer set. Timer: {jumpBufferTimer}");
                }
            }

            // Perform jump
            bool canJump = (groundChecker.IsGrounded || coyoteTimer > 0) && jumpBufferTimer > 0;
            if (debugLogging && jumpInput && !wasJumpPressed)
            {
                if (debugLogging) Debug.Log($"PlayerJump: Jump input detected. Can jump: {canJump}, IsGrounded: {groundChecker.IsGrounded}, CoyoteTimer: {coyoteTimer}, JumpBufferTimer: {jumpBufferTimer}");
            }

            if (canJump)
            {
                PerformJump();
            }

            wasJumpPressed = jumpInput;
        }

        private void PerformJump()
        {
            float force = config.JumpForce;

            // Reset vertical velocity
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            // Apply jump force
            rb.AddForce(Vector3.up * force, ForceMode.Impulse);
            // Reset timers
            jumpBufferTimer = 0;
            coyoteTimer = 0;
            isJumping = true;
            jumpApexHeight = transform.position.y + (force * force) / (2 * Physics.gravity.magnitude); // Approximate apex

            // Trigger event
            OnJump.Invoke();

            if (debugLogging)
            {
                Debug.Log($"PlayerJump: Jump performed, isJumping = true. Force: {force}, Velocity: {rb.linearVelocity}");
            }
        }

        // Public methods for configuration

        public void SetDebugLogging(bool enabled)
        {
            debugLogging = enabled;
        }
    }
}