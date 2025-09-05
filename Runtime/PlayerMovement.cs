using UnityEngine;

namespace ZacharysNewman.PPC
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(GroundChecker))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private PlayerMovementConfig config;

        [Header("Dependencies")]
        [SerializeField] private Camera mainCamera;



        // Component references
        private Rigidbody rb;
        private GroundChecker groundChecker;
        private PlayerInput playerInput;

        // Movement state
        private bool isGrounded;
        private Vector3 groundNormal;
        private float crouchSpeedMultiplier = 1f;

        public Vector3 TargetVelocity { get; private set; }
        public Vector3 CurrentVelocity { get; private set; }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            groundChecker = GetComponent<GroundChecker>();
            playerInput = GetComponent<PlayerInput>();

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            // Ensure config is set
            if (config == null)
            {
                Debug.LogError("PlayerMovement: Config is required. Please assign a PlayerMovementConfig.");
            }
        }

        private void Start()
        {
            if (groundChecker == null)
            {
                groundChecker = GetComponent<GroundChecker>();
            }

            // Validate required components
            if (groundChecker == null)
            {
                Debug.LogError("PlayerMovement: GroundChecker component is required but not found!");
                enabled = false;
                return;
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    Debug.LogError("PlayerMovement: No main camera found!");
                    enabled = false;
                    return;
                }
            }
            }

        public void UpdateGrounded(bool grounded, Vector3 normal)
        {
            isGrounded = grounded;
            groundNormal = normal;
        }

        public void HandleMovement()
        {
            if (mainCamera == null || playerInput == null) return;

            Vector2 moveInput = playerInput.MoveInput;
            bool runInput = playerInput.RunInput;

            // Get camera forward and right for relative movement
            Vector3 cameraForward = mainCamera.transform.forward;
            cameraForward.y = 0f;
            cameraForward.Normalize();

            Vector3 cameraRight = mainCamera.transform.right;
            cameraRight.y = 0f;
            cameraRight.Normalize();

            // Convert input to world space
            Vector3 moveDirection = cameraForward * moveInput.y + cameraRight * moveInput.x;

            // Project onto ground plane
            if (isGrounded && groundChecker != null)
            {
                moveDirection = moveDirection - Vector3.Project(moveDirection, groundNormal);
            }

            // Compute target velocity
            float speed = (runInput ? config.RunSpeed : config.WalkSpeed) * crouchSpeedMultiplier;
            TargetVelocity = moveDirection * speed;

            // Smooth velocity
            Vector3 velocityChange = TargetVelocity - rb.linearVelocity;
            velocityChange.y = 0f; // Don't affect vertical velocity

            // Limit velocity change
            float maxChange = config.MaxVelocityChange;
            if (velocityChange.magnitude > maxChange)
            {
                velocityChange = velocityChange.normalized * maxChange;
            }

            // Apply acceleration/deceleration with faster reverse deceleration
            Vector3 currentHorizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            Vector3 targetHorizontalVelocity = new Vector3(TargetVelocity.x, 0, TargetVelocity.z);
            float dot = Vector3.Dot(currentHorizontalVelocity.normalized, targetHorizontalVelocity.normalized);
            float accel;
            if (moveInput.magnitude > 0.1f)
            {
                accel = (dot < -0.1f) ? config.ReverseDeceleration : config.Acceleration;
            }
            else
            {
                accel = config.Deceleration;
            }
            velocityChange = Vector3.MoveTowards(Vector3.zero, velocityChange, accel * Time.fixedDeltaTime);

            // Apply to rigidbody
            rb.AddForce(velocityChange, ForceMode.VelocityChange);

            CurrentVelocity = rb.linearVelocity;
        }

        // Public methods for configuration
        public void SetCrouchSpeedMultiplier(float multiplier)
        {
            crouchSpeedMultiplier = multiplier;
        }

        public float WalkSpeed => config.WalkSpeed;
    }
}