using UnityEngine;

namespace ZacharysNewman.PPC
{
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(GroundChecker))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerJump))]
    [RequireComponent(typeof(PlayerInput))]
    public class DebugVisualizer : MonoBehaviour
    {
        [Header("Visualization Toggles")]
        [SerializeField] private bool visualizeBounds = false;
        [SerializeField] private bool debugLogging = true;
        [SerializeField] private bool visualizeGroundCeilingChecks = true;
        [SerializeField] private bool visualizeVelocity = false;
        [SerializeField] private bool visualizeJump = true;

        [Header("Ground Check Settings")]
        [SerializeField] private float groundCheckRadius = 0.2f;

        // Component references (auto-assigned)
        private GroundChecker groundChecker;
        private PlayerMovement playerMovement;
        private PlayerJump playerJump;
        private PlayerInput playerInput;

        // Component references
        private CapsuleCollider capsule;
        private Transform groundCheck;
        private Transform ceilingCheck;

        private void Awake()
        {
            capsule = GetComponent<CapsuleCollider>();

            // Auto-assign required components
            groundChecker = GetComponent<GroundChecker>();
            playerMovement = GetComponent<PlayerMovement>();
            playerJump = GetComponent<PlayerJump>();
            playerInput = GetComponent<PlayerInput>();

            // Create debug check transforms
            groundCheck = new GameObject("DebugGroundCheck").transform;
            groundCheck.parent = transform;
            groundCheck.localPosition = capsule.center + Vector3.down * (capsule.height / 2f - capsule.radius * 0.1f);

            ceilingCheck = new GameObject("DebugCeilingCheck").transform;
            ceilingCheck.parent = transform;
            ceilingCheck.localPosition = Vector3.up * (capsule.height / 2f - capsule.radius);
        }

        private void Update()
        {
            if (playerInput != null && debugLogging)
            {
                LogInput(playerInput.MoveInput, playerInput.RunInput, playerInput.CrouchInput, playerInput.JumpInput, playerInput.InteractInput);
            }
        }

        public void LogInput(Vector2 moveInput, bool runInput, bool crouchInput, bool jumpInput, bool interactInput)
        {
            if (debugLogging)
            {
                Debug.Log($"Move Input: {moveInput}, Run: {runInput}, Crouch: {crouchInput}, Jump: {jumpInput}, Interact: {interactInput}");
            }
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            DrawGizmos();
        }

        public void DrawGizmos()
        {
            if (groundChecker == null || playerMovement == null || playerJump == null) return;

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
                Gizmos.color = groundChecker.IsGrounded ? Color.blue : Color.red;
                Gizmos.DrawWireSphere(groundCheck.position + Vector3.down * 0.1f, groundCheckRadius);

                // Ceiling check
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(ceilingCheck.position + Vector3.up * 0.1f, 0.4f);

                // Ground normal
                if (groundChecker.IsGrounded)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(groundCheck.position, groundCheck.position + groundChecker.GroundNormal);
                }
            }

            if (visualizeVelocity)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, transform.position + playerMovement.CurrentVelocity);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, transform.position + playerMovement.TargetVelocity);
            }

            if (visualizeJump)
            {
                // Draw jump apex line
                if (playerJump.IsJumping)
                {
                    Gizmos.color = Color.green;
                    Vector3 apexPosition = new Vector3(transform.position.x, playerJump.JumpApexHeight, transform.position.z);
                    Gizmos.DrawLine(transform.position, apexPosition);
                    Gizmos.DrawSphere(apexPosition, 0.1f);
                }

                // Draw jump buffer and coyote time indicators
                if (playerJump.JumpBufferTimer > 0)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(transform.position + Vector3.up * 2f, playerJump.JumpBufferTimer * 0.5f);
                }
                if (playerJump.CoyoteTimer > 0)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawSphere(transform.position + Vector3.up * 2.5f, playerJump.CoyoteTimer * 0.5f);
                }
            }
        }

        // Public methods for configuration
        public void SetVisualizationToggles(bool bounds, bool logging, bool groundChecks, bool velocity, bool jump)
        {
            visualizeBounds = bounds;
            debugLogging = logging;
            visualizeGroundCeilingChecks = groundChecks;
            visualizeVelocity = velocity;
            visualizeJump = jump;
        }
    }
}