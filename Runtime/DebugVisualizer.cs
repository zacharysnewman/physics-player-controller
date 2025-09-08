using UnityEngine;

namespace ZacharysNewman.PPC
{
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(GroundChecker))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerJump))]
    [RequireComponent(typeof(PlayerCrouch))]
    [RequireComponent(typeof(PlayerClimb))]
    [RequireComponent(typeof(PlayerInput))]
    public class DebugVisualizer : MonoBehaviour
    {
        [Header("Visualization Toggles")]
        [SerializeField] private bool visualizeBounds = false;
        [SerializeField] private bool debugLogging = true;
        [SerializeField] private bool visualizeGroundCeilingChecks = true;
        [SerializeField] private bool visualizeVelocity = false;
        [SerializeField] private bool visualizeJump = true;
        [SerializeField] private bool visualizeCrouch = true;
        [SerializeField] private bool visualizeTerrainNavigation = false;
        [SerializeField] private bool visualizeLadderClimb = true;



        // Component references (auto-assigned)
        private GroundChecker groundChecker;
        private PlayerMovement playerMovement;
        private PlayerJump playerJump;
        private PlayerCrouch playerCrouch;
        private PlayerClimb playerClimb;
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
            playerCrouch = GetComponent<PlayerCrouch>();
            playerClimb = GetComponent<PlayerClimb>();
            playerInput = GetComponent<PlayerInput>();

            // Create debug check transforms
            groundCheck = new GameObject("DebugGroundCheck").transform;
            groundCheck.parent = transform;
            groundCheck.localPosition = capsule.center;

            ceilingCheck = new GameObject("DebugCeilingCheck").transform;
            ceilingCheck.parent = transform;
            ceilingCheck.localPosition = capsule.center;
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
                // Debug.Log($"Move Input: {moveInput}, Run: {runInput}, Crouch: {crouchInput}, Jump: {jumpInput}, Interact: {interactInput}");
            }
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            DrawGizmos();
        }

        public void DrawGizmos()
        {
            if (groundChecker == null || playerMovement == null || playerJump == null || playerCrouch == null || playerClimb == null) return;

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
                Gizmos.DrawLine(groundCheck.position, groundCheck.position + Vector3.down * 0.15f);

                // Ceiling check
                Gizmos.color = groundChecker.IsCeilingBlocked ? Color.red : Color.green;
                Gizmos.DrawLine(ceilingCheck.position, ceilingCheck.position + Vector3.up * 0.15f);

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

            if (visualizeCrouch)
            {
                // Draw current capsule dimensions
                Gizmos.color = playerCrouch.IsCrouching ? Color.blue : Color.gray;
                Gizmos.DrawWireCube(transform.position + capsule.center, new Vector3(capsule.radius * 2, capsule.height, capsule.radius * 2));

                // Show crouch state
                Gizmos.color = playerCrouch.IsCrouching ? Color.green : Color.red;
                Gizmos.DrawSphere(transform.position + Vector3.up * 3f, 0.1f);
            }

            if (visualizeTerrainNavigation)
            {
                playerMovement.VisualizeTerrainRays();
            }

            if (visualizeLadderClimb)
            {
                playerClimb.VisualizeLadder();
            }
        }

        // Public methods for configuration
        public void SetVisualizationToggles(bool bounds, bool logging, bool groundChecks, bool velocity, bool jump, bool crouch, bool terrain, bool ladder)
        {
            visualizeBounds = bounds;
            debugLogging = logging;
            visualizeGroundCeilingChecks = groundChecks;
            visualizeVelocity = velocity;
            visualizeJump = jump;
            visualizeCrouch = crouch;
            visualizeTerrainNavigation = terrain;
            visualizeLadderClimb = ladder;
        }
    }
}