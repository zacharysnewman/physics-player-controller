using UnityEngine;

namespace ZacharysNewman.PPC
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerJump))]
    [RequireComponent(typeof(PlayerCrouch))]
    [RequireComponent(typeof(PlayerClimb))]
    [RequireComponent(typeof(GroundChecker))]
    [RequireComponent(typeof(CameraController))]
    [RequireComponent(typeof(DebugVisualizer))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private PlayerControllerConfig config;

        [Header("Debug")]
        [SerializeField] private bool showStateDebug = true;

        // Component references (auto-assigned, private so hidden from inspector)
        private PlayerInput playerInput;
        private PlayerMovement playerMovement;
        private PlayerJump playerJump;
        private PlayerCrouch playerCrouch;
        private PlayerClimb playerClimb;
        private GroundChecker groundChecker;
        private CameraController cameraController;
        private DebugVisualizer debugVisualizer;

        // State machine
        public enum PlayerState { Idle, Walking, Running, Crouching, Sliding, Jumping, Falling, Climbing }
        public PlayerState CurrentState { get; private set; }
        public bool IsFalling => CurrentState == PlayerState.Falling;

        // Dynamic collider position for animator sync
        public Vector3 ColliderBottomPosition => playerCrouch != null ? playerCrouch.ColliderBottomPosition : transform.position;

        [Header("Settings")]
        [SerializeField] private bool freezeYRotation = true;

        private Rigidbody rb;
        private bool previousFreezeYRotation;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();

            // Auto-assign required components
            playerInput = GetComponent<PlayerInput>();
            playerMovement = GetComponent<PlayerMovement>();
            playerJump = GetComponent<PlayerJump>();
            playerCrouch = GetComponent<PlayerCrouch>();
            playerClimb = GetComponent<PlayerClimb>();
            groundChecker = GetComponent<GroundChecker>();
            cameraController = GetComponent<CameraController>();
            debugVisualizer = GetComponent<DebugVisualizer>();

            // Setup Rigidbody constraints
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | (freezeYRotation ? RigidbodyConstraints.FreezeRotationY : 0);
            previousFreezeYRotation = freezeYRotation;
        }

        private void Start()
        {
            // Apply configuration if available
            if (config != null)
            {
                ApplyConfiguration();
            }

            // Validate required components (should always be found due to RequireComponent)
            ValidateRequiredComponents();
        }

        private void ApplyConfiguration()
        {
            // Apply component configurations
            if (playerMovement != null && config.MovementConfig != null)
            {
                // Note: Components would need to be updated to accept config references
                // For now, this is a placeholder for future implementation
            }

            if (playerJump != null && config.JumpConfig != null)
            {
                // Apply jump config
            }

            if (cameraController != null && config.CameraConfig != null)
            {
                // Apply camera config
            }

            if (groundChecker != null && config.GroundCheckerConfig != null)
            {
                // Apply ground checker config
            }

            if (playerClimb != null && config.ClimbConfig != null)
            {
                // Apply climb config
            }



            if (playerInput != null)
            {
                // Apply input sensitivity
                // Note: PlayerInput would need to be updated to accept sensitivity
            }
        }

        private void UpdateStateMachine()
        {
            PlayerState newState = PlayerState.Idle;

            if (playerClimb != null && playerClimb.IsClimbing)
            {
                newState = PlayerState.Climbing;
            }
            else if (playerCrouch != null && playerCrouch.IsCrouching)
            {
                newState = PlayerState.Crouching;
            }
            else if (groundChecker != null && !groundChecker.IsGrounded)
            {
                if (rb.linearVelocity.y > 0 || (playerJump != null && playerJump.IsJumping))
                {
                    newState = PlayerState.Jumping;
                }
                else
                {
                    newState = PlayerState.Falling;
                }
            }
            else if (groundChecker != null && groundChecker.IsGrounded)
            {
                Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                if (horizontalVelocity.magnitude > 0.1f)
                {
                    if (playerInput != null && playerInput.RunInput)
                    {
                        newState = PlayerState.Running;
                    }
                    else
                    {
                        newState = PlayerState.Walking;
                    }
                }
                else
                {
                    newState = PlayerState.Idle;
                }
            }

            // Sliding: Skipping Phase 4, so not implemented yet
            // If slope angle > limit, set to Sliding, but since not exposed, leave as is

            CurrentState = newState;
        }

        private void ValidateRequiredComponents()
        {
            // Components should always be found due to RequireComponent attributes
            // This validation is mainly for debugging if something goes wrong
            if (playerInput == null) Debug.LogError("PlayerController: PlayerInput component not found! This should not happen.");
            if (playerMovement == null) Debug.LogError("PlayerController: PlayerMovement component not found! This should not happen.");
            if (playerJump == null) Debug.LogError("PlayerController: PlayerJump component not found! This should not happen.");
            if (playerCrouch == null) Debug.LogError("PlayerController: PlayerCrouch component not found! This should not happen.");
            if (playerClimb == null) Debug.LogError("PlayerController: PlayerClimb component not found! This should not happen.");
            if (groundChecker == null) Debug.LogError("PlayerController: GroundChecker component not found! This should not happen.");
            if (cameraController == null) Debug.LogError("PlayerController: CameraController component not found! This should not happen.");
            if (debugVisualizer == null) Debug.LogError("PlayerController: DebugVisualizer component not found! This should not happen.");
        }

        private void Update()
        {
            // Update Y rotation constraints if changed
            if (previousFreezeYRotation != freezeYRotation)
            {
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | (freezeYRotation ? RigidbodyConstraints.FreezeRotationY : 0);
                previousFreezeYRotation = freezeYRotation;
            }

            // Update movement with ground info
            if (playerMovement != null && groundChecker != null)
            {
                playerMovement.UpdateGrounded(groundChecker.IsGrounded, groundChecker.GroundNormal, groundChecker.GroundObject);
            }

            // Handle jump input
            if (playerJump != null && playerInput != null)
            {
                playerJump.HandleJump(playerInput.JumpInput);
            }

            // Handle crouch input
            if (playerCrouch != null && playerInput != null)
            {
                playerCrouch.HandleCrouch(playerInput.CrouchInput);
            }

            // Update state machine
            UpdateStateMachine();
        }

        private void FixedUpdate()
        {
            // Handle movement
            if (playerMovement != null)
            {
                playerMovement.HandleMovement();
            }
        }



        private void OnGUI()
        {
            if (showStateDebug)
            {
                GUI.Label(new Rect(10, 10, 200, 20), $"State: {CurrentState}");
            }
        }

        // Public methods for external control
        public void SetFreezeYRotation(bool freeze)
        {
            freezeYRotation = freeze;
        }

        // Component access methods for external systems
        public PlayerInput GetPlayerInput() => playerInput;
        public PlayerMovement GetPlayerMovement() => playerMovement;
        public PlayerJump GetPlayerJump() => playerJump;
        public PlayerCrouch GetPlayerCrouch() => playerCrouch;
        public PlayerClimb GetPlayerClimb() => playerClimb;
        public GroundChecker GetGroundChecker() => groundChecker;
        public CameraController GetCameraController() => cameraController;
        public DebugVisualizer GetDebugVisualizer() => debugVisualizer;
    }
}