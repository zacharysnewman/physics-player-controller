using UnityEngine;

namespace ZacharysNewman.PPC
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerClimb : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private PlayerClimbConfig config;

        [Header("Debug")]
        [SerializeField] private bool debugLogging = false;

        // Component references
        private Rigidbody rb;
        private PlayerInput playerInput;
        private PlayerMovement playerMovement;
        private GroundChecker groundChecker;
        private Camera mainCamera;

        // Climbing state
        private bool isClimbing;
        private Vector3 ladderAxis;
        private Vector3 ladderPosition;
        private Bounds ladderBounds;
        private bool wasGrounded;

        // Debug
        private Vector3 debugClimbAxis;
        private Bounds debugLadderBounds;

        // Public properties
        public bool IsClimbing => isClimbing;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            playerInput = GetComponent<PlayerInput>();
            playerMovement = GetComponent<PlayerMovement>();
            groundChecker = GetComponent<GroundChecker>();
            mainCamera = Camera.main;

            if (config == null)
            {
                Debug.LogError("PlayerClimb: Config is required. Please assign a PlayerClimbConfig.");
            }

            if (mainCamera == null)
            {
                Debug.LogError("PlayerClimb: No main camera found!");
            }
        }

        private void Update()
        {
            if (isClimbing && playerInput != null)
            {
                // Exit climbing on jump
                if (playerInput.JumpInput)
                {
                    ExitClimb();
                }
            }

            // Auto dismount when becoming grounded while climbing
            if (isClimbing && groundChecker != null)
            {
                bool currentlyGrounded = groundChecker.IsGrounded;
                if (!wasGrounded && currentlyGrounded)
                {
                    if (debugLogging) Debug.Log("Auto dismounting from ladder - became grounded while climbing");
                    ExitClimb();
                }
                wasGrounded = currentlyGrounded;
            }
            else if (!isClimbing && groundChecker != null)
            {
                // Update wasGrounded even when not climbing to maintain state
                wasGrounded = groundChecker.IsGrounded;
            }
        }

        private void FixedUpdate()
        {
            if (isClimbing && playerInput != null)
            {
                HandleClimbMovement();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (config == null) return;

            // Check if collider is on ladder layer
            if (((1 << other.gameObject.layer) & config.LadderLayerMask) != 0)
            {
                EnterClimb(other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (isClimbing && ((1 << other.gameObject.layer) & config.LadderLayerMask) != 0)
            {
                // Check if we're still overlapping with any ladder triggers
                if (!IsOverlappingLadder())
                {
                    ExitClimb();
                }
            }
        }

        private void EnterClimb(Collider ladderCollider)
        {
            if (isClimbing) return;

            if (debugLogging) Debug.Log("Entering climb mode");
            isClimbing = true;

            // Determine ladder axis (assume vertical for now)
            ladderAxis = Vector3.up;

            // Store ladder bounds for debug
            ladderBounds = ladderCollider.bounds;
            debugLadderBounds = ladderBounds;
            debugClimbAxis = ladderAxis;

            // Disable gravity
            rb.useGravity = false;

            // Stop current velocity
            rb.linearVelocity = Vector3.zero;

            // Disable normal movement
            if (playerMovement != null)
            {
                // We'll need to modify PlayerMovement to respect climbing state
            }
        }

        private void ExitClimb()
        {
            if (!isClimbing) return;

            isClimbing = false;

            // Re-enable gravity
            rb.useGravity = true;

            // Re-enable normal movement
            if (playerMovement != null)
            {
                // Reset movement state
            }
        }

        private void HandleClimbMovement()
        {
            if (playerInput == null || config == null || mainCamera == null)
            {
                if (debugLogging) Debug.Log($"HandleClimbMovement early return - playerInput: {playerInput}, config: {config}, mainCamera: {mainCamera}");
                return;
            }

            Vector2 moveInput = playerInput.MoveInput;

            // Get camera forward and right directions
            Vector3 cameraForward = mainCamera.transform.forward;
            Vector3 cameraForwardHorizontal = cameraForward;
            cameraForwardHorizontal.y = 0f;
            cameraForwardHorizontal.Normalize();

            Vector3 cameraRight = mainCamera.transform.right;
            cameraRight.y = 0f;
            cameraRight.Normalize();

            // Calculate direction from player to ladder center (projected to horizontal)
            Vector3 playerToLadder = debugLadderBounds.center - transform.position;
            playerToLadder.y = 0f;
            playerToLadder.Normalize();

            // Calculate vertical movement based on camera pitch (looking up/down)
            float verticalInput = moveInput.y;
            float verticalDirection = 0f;

            if (Mathf.Abs(verticalInput) > 0.01f)
            {
                // Debug: Log camera forward y component
                if (debugLogging) Debug.Log($"Camera forward y: {cameraForward.y}, verticalInput: {verticalInput}");

                // Check camera pitch: positive y means looking up, negative means looking down
                bool isLookingUp = cameraForward.y > 0.0f;
                bool isLookingDown = cameraForward.y < 0.0f;

                // Invert input when looking down, normal when looking up
                if (isLookingDown)
                {
                    verticalDirection = -verticalInput; // Invert when looking down
                    if (debugLogging) Debug.Log("Looking down - inverting input");
                }
                else if (isLookingUp)
                {
                    verticalDirection = verticalInput; // Normal when looking up
                    if (debugLogging) Debug.Log("Looking up - normal input");
                }
                else
                {
                    // Camera is level, use input directly
                    verticalDirection = verticalInput;
                    if (debugLogging) Debug.Log("Camera level - direct input");
                }
            }

            // Calculate horizontal movement (relative to ladder)
            float horizontalInput = moveInput.x;
            Vector3 horizontalVelocity = Vector3.zero;

            if (Mathf.Abs(horizontalInput) > 0.01f)
            {
                // Move horizontally relative to camera and ladder
                // Use camera right direction, but keep it perpendicular to ladder axis
                Vector3 horizontalDirection = cameraRight;

                // Ensure horizontal movement is perpendicular to ladder axis
                horizontalDirection = Vector3.ProjectOnPlane(horizontalDirection, ladderAxis).normalized;

                horizontalVelocity = horizontalDirection * horizontalInput * config.LadderClimbSpeed;
            }

            // Combine vertical and horizontal movement
            Vector3 verticalVelocity = ladderAxis * verticalDirection * config.LadderClimbSpeed;
            Vector3 totalVelocity = verticalVelocity + horizontalVelocity;

            // Debug: Log final velocity
            if (debugLogging) Debug.Log($"Final velocity: {totalVelocity}, verticalDirection: {verticalDirection}, verticalVelocity: {verticalVelocity}");

            // Apply velocity
            rb.linearVelocity = totalVelocity;
        }

        private bool IsOverlappingLadder()
        {
            // Check for overlapping ladder colliders
            Collider[] colliders = Physics.OverlapBox(transform.position, Vector3.one * 0.5f, Quaternion.identity, config.LadderLayerMask);
            return colliders.Length > 0;
        }

        // Debug visualization
        public void VisualizeLadder()
        {
            if (!isClimbing || mainCamera == null) return;

            // Draw ladder bounds
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(debugLadderBounds.center, debugLadderBounds.size);

            // Draw climbing axis
            Gizmos.color = Color.green;
            Vector3 start = transform.position;
            Vector3 end = start + debugClimbAxis * 2f;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(end, 0.1f);

            // Draw camera-relative movement directions
            Vector3 cameraForward = mainCamera.transform.forward;
            Vector3 cameraRight = mainCamera.transform.right;
            cameraRight.y = 0f;
            cameraRight.Normalize();

            // Show direction from player to ladder center
            Vector3 playerToLadder = debugLadderBounds.center - start;
            playerToLadder.y = 0f;
            if (playerToLadder != Vector3.zero)
            {
                playerToLadder.Normalize();
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(start, start + playerToLadder * 2f);
                Gizmos.DrawSphere(start + playerToLadder * 2f, 0.06f);
            }

            // Show camera forward direction (with vertical component)
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(start, start + cameraForward * 1.5f);
            Gizmos.DrawSphere(start + cameraForward * 1.5f, 0.05f);

            // Show camera pitch indicator (for input inversion)
            float cameraPitch = cameraForward.y;
            Gizmos.color = cameraPitch > 0.1f ? Color.green : (cameraPitch < -0.1f ? Color.red : Color.yellow);
            Vector3 pitchIndicator = start + Vector3.up * cameraPitch * 2f;
            Gizmos.DrawLine(start, pitchIndicator);
            Gizmos.DrawSphere(pitchIndicator, 0.08f);

            // Show horizontal movement direction (camera right)
            Vector3 horizontalDirection = Vector3.ProjectOnPlane(cameraRight, debugClimbAxis).normalized;
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(start, start + horizontalDirection * 1.5f);
            Gizmos.DrawSphere(start + horizontalDirection * 1.5f, 0.05f);
            Gizmos.DrawLine(start, start - horizontalDirection * 1.5f);
            Gizmos.DrawSphere(start - horizontalDirection * 1.5f, 0.05f);
        }
    }
}