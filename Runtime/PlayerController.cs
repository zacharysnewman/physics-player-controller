using UnityEngine;

namespace ZacharysNewman.PPC
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerJump))]
    [RequireComponent(typeof(GroundChecker))]
    [RequireComponent(typeof(CameraController))]
    [RequireComponent(typeof(DebugVisualizer))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private PlayerControllerConfig config;

        // Component references (auto-assigned, private so hidden from inspector)
        private PlayerInput playerInput;
        private PlayerMovement playerMovement;
        private PlayerJump playerJump;
        private GroundChecker groundChecker;
        private CameraController cameraController;
        private DebugVisualizer debugVisualizer;

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



            if (playerInput != null)
            {
                // Apply input sensitivity
                // Note: PlayerInput would need to be updated to accept sensitivity
            }
        }

        private void ValidateRequiredComponents()
        {
            // Components should always be found due to RequireComponent attributes
            // This validation is mainly for debugging if something goes wrong
            if (playerInput == null) Debug.LogError("PlayerController: PlayerInput component not found! This should not happen.");
            if (playerMovement == null) Debug.LogError("PlayerController: PlayerMovement component not found! This should not happen.");
            if (playerJump == null) Debug.LogError("PlayerController: PlayerJump component not found! This should not happen.");
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
                playerMovement.UpdateGrounded(groundChecker.IsGrounded, groundChecker.GroundNormal);
            }

            // Handle jump input
            if (playerJump != null && playerInput != null)
            {
                playerJump.HandleJump(playerInput.JumpInput);
            }
        }

        private void FixedUpdate()
        {
            // Handle movement
            if (playerMovement != null)
            {
                playerMovement.HandleMovement();
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
        public GroundChecker GetGroundChecker() => groundChecker;
        public CameraController GetCameraController() => cameraController;
        public DebugVisualizer GetDebugVisualizer() => debugVisualizer;
    }
}