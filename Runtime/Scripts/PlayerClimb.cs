using UnityEngine;

namespace ZacharysNewman.PPC
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerClimb : MonoBehaviour, IVelocityLayer
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
        private VerticalVelocityLayer verticalLayer;
        [SerializeField] private Camera mainCamera;

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

        // IVelocityLayer
        public bool IsActive => isClimbing;
        public bool IsExclusive => true;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            playerInput = GetComponent<PlayerInput>();
            playerMovement = GetComponent<PlayerMovement>();
            groundChecker = GetComponent<GroundChecker>();
            verticalLayer = GetComponent<VerticalVelocityLayer>();
            // mainCamera must be assigned in inspector

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
                wasGrounded = groundChecker.IsGrounded;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (config == null) return;

            if (((1 << other.gameObject.layer) & config.LadderLayerMask) != 0)
            {
                EnterClimb(other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (isClimbing && ((1 << other.gameObject.layer) & config.LadderLayerMask) != 0)
            {
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

            ladderAxis = Vector3.up;

            ladderBounds = ladderCollider.bounds;
            debugLadderBounds = ladderBounds;
            debugClimbAxis = ladderAxis;

            // Reset horizontal velocity so there's no burst when grabbing the ladder
            if (playerMovement != null)
            {
                playerMovement.ResetHorizontalVelocity();
            }
        }

        private void ExitClimb()
        {
            if (!isClimbing) return;

            isClimbing = false;

            // Zero accumulated Y so stale climb speed doesn't launch the player, then sync the
            // absorption baseline so the frozen lastTargetY isn't misread as an external impulse.
            if (verticalLayer != null)
            {
                verticalLayer.AddVerticalImpulse(-verticalLayer.AccumulatedY);
                verticalLayer.ResetAbsorptionBaseline();
            }

            // Seed horizontal baseline from current rb velocity so the delta between the frozen
            // lastHorizontalContribution and the actual velocity isn't absorbed as a lateral impulse.
            if (playerMovement != null)
            {
                playerMovement.SeedHorizontalBaseline();
            }
        }

        public Vector3 GetVelocityContribution(float deltaTime)
        {
            if (playerInput == null || config == null || mainCamera == null)
            {
                if (debugLogging) Debug.Log($"GetVelocityContribution early return - playerInput: {playerInput}, config: {config}, mainCamera: {mainCamera}");
                return Vector3.zero;
            }

            Vector2 moveInput = playerInput.MoveInput;

            Vector3 cameraForward = mainCamera.transform.forward;
            Vector3 cameraForwardHorizontal = cameraForward;
            cameraForwardHorizontal.y = 0f;
            cameraForwardHorizontal.Normalize();

            Vector3 cameraRight = mainCamera.transform.right;
            cameraRight.y = 0f;
            cameraRight.Normalize();

            float verticalInput = moveInput.y;
            float verticalDirection = 0f;

            if (Mathf.Abs(verticalInput) > 0.01f)
            {
                if (debugLogging) Debug.Log($"Camera forward y: {cameraForward.y}, verticalInput: {verticalInput}");

                bool isLookingUp = cameraForward.y > 0.3f;
                bool isLookingDown = cameraForward.y < -0.3f;

                if (isLookingDown)
                {
                    verticalDirection = -verticalInput;
                    if (debugLogging) Debug.Log("Looking down - inverting input");
                }
                else if (isLookingUp)
                {
                    verticalDirection = verticalInput;
                    if (debugLogging) Debug.Log("Looking up - normal input");
                }
                else
                {
                    verticalDirection = verticalInput;
                    if (debugLogging) Debug.Log("Camera level - direct input");
                }
            }

            float horizontalInput = moveInput.x;
            Vector3 horizontalVelocity = Vector3.zero;

            if (Mathf.Abs(horizontalInput) > 0.01f)
            {
                Vector3 horizontalDirection = cameraRight;
                horizontalDirection = Vector3.ProjectOnPlane(horizontalDirection, ladderAxis).normalized;
                horizontalVelocity = horizontalDirection * horizontalInput * config.LadderClimbSpeed;
            }

            Vector3 verticalVelocity = ladderAxis * verticalDirection * config.LadderClimbSpeed;
            Vector3 totalVelocity = verticalVelocity + horizontalVelocity;

            if (debugLogging) Debug.Log($"Climb velocity: {totalVelocity}, verticalDirection: {verticalDirection}");

            return totalVelocity;
        }

        private bool IsOverlappingLadder()
        {
            Collider[] colliders = Physics.OverlapBox(transform.position, Vector3.one * 0.5f, Quaternion.identity, config.LadderLayerMask);
            return colliders.Length > 0;
        }

        // Debug visualization
        public void VisualizeLadder()
        {
            if (!isClimbing || mainCamera == null) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(debugLadderBounds.center, debugLadderBounds.size);

            Gizmos.color = Color.green;
            Vector3 start = transform.position;
            Vector3 end = start + debugClimbAxis * 2f;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(end, 0.1f);

            Vector3 cameraForward = mainCamera.transform.forward;
            Vector3 cameraRight = mainCamera.transform.right;
            cameraRight.y = 0f;
            cameraRight.Normalize();

            Vector3 playerToLadder = debugLadderBounds.center - start;
            playerToLadder.y = 0f;
            if (playerToLadder != Vector3.zero)
            {
                playerToLadder.Normalize();
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(start, start + playerToLadder * 2f);
                Gizmos.DrawSphere(start + playerToLadder * 2f, 0.06f);
            }

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(start, start + cameraForward * 1.5f);
            Gizmos.DrawSphere(start + cameraForward * 1.5f, 0.05f);

            float cameraPitch = cameraForward.y;
            Gizmos.color = cameraPitch > 0.1f ? Color.green : (cameraPitch < -0.1f ? Color.red : Color.yellow);
            Vector3 pitchIndicator = start + Vector3.up * cameraPitch * 2f;
            Gizmos.DrawLine(start, pitchIndicator);
            Gizmos.DrawSphere(pitchIndicator, 0.08f);

            Vector3 horizontalDirection = Vector3.ProjectOnPlane(cameraRight, debugClimbAxis).normalized;
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(start, start + horizontalDirection * 1.5f);
            Gizmos.DrawSphere(start + horizontalDirection * 1.5f, 0.05f);
            Gizmos.DrawLine(start, start - horizontalDirection * 1.5f);
            Gizmos.DrawSphere(start - horizontalDirection * 1.5f, 0.05f);
        }
    }
}
