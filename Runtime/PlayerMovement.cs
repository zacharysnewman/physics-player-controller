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
        [SerializeField] private CameraController cameraController;

        // Component references
        private Rigidbody rb;
        private GroundChecker groundChecker;
        private PlayerInput playerInput;
        private PlayerCrouch playerCrouch;
        private PlayerClimb playerClimb;
        private CapsuleCollider capsule;

        // Movement state
        private bool isGrounded;
        private Vector3 groundNormal;
        private float crouchSpeedMultiplier = 1f;

        // Moving platform support
        private Transform currentPlatform;
        private Vector3 previousPlatformPosition;
        private Quaternion previousPlatformRotation;
        private Vector3 platformVelocity;
        private Vector3 platformAngularVelocity;
        private Quaternion platformDeltaRotation;
        private Quaternion platformRotationAccum;

        public Vector3 TargetVelocity { get; private set; }
        public Vector3 CurrentVelocity { get; private set; }
        private Vector3 currentMoveDirection;
        private Vector3 debugFootPoint;
        private Vector3 debugSlopePoint;
        private float debugSlopeAngle;

        // Moving platform debug
        private Vector3 debugPlatformVelocity;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            groundChecker = GetComponent<GroundChecker>();
            playerInput = GetComponent<PlayerInput>();
            playerCrouch = GetComponent<PlayerCrouch>();
            playerClimb = GetComponent<PlayerClimb>();
            capsule = GetComponent<CapsuleCollider>();

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (cameraController == null)
            {
                cameraController = GetComponent<CameraController>();
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

        private void Update()
        {
            // Track platform movement
            TrackPlatformMovement();
        }

        private void TrackPlatformMovement()
        {
            if (currentPlatform == null) return;

            // Calculate platform velocity
            Vector3 currentPosition = currentPlatform.position;
            Quaternion currentRotation = currentPlatform.rotation;

            platformVelocity = (currentPosition - previousPlatformPosition) / Time.deltaTime;

            // Calculate rotation delta
            platformDeltaRotation = currentRotation * Quaternion.Inverse(previousPlatformRotation);

            // Calculate angular velocity
            platformDeltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
            platformAngularVelocity = axis * (angle * Mathf.Deg2Rad / Time.deltaTime);

            // Accumulate platform rotation
            platformRotationAccum = platformRotationAccum * platformDeltaRotation;

            // Update camera yaw offset
            if (cameraController != null)
            {
                cameraController.PlatformYawOffset = platformRotationAccum.eulerAngles.y;
            }

            // Update previous values
            previousPlatformPosition = currentPosition;
            previousPlatformRotation = currentRotation;

            // Debug
            debugPlatformVelocity = platformVelocity;
        }

        public void UpdateGrounded(bool grounded, Vector3 normal, Transform groundObject)
        {
            isGrounded = grounded;
            groundNormal = normal;

            // Moving platform detection
            if (grounded && groundObject != null)
            {
                // Check if this is a new platform or different from current
                if (currentPlatform != groundObject)
                {
                    // New platform detected
                    currentPlatform = groundObject;
                    previousPlatformPosition = currentPlatform.position;
                    previousPlatformRotation = currentPlatform.rotation;
                    platformVelocity = Vector3.zero;
                    platformAngularVelocity = Vector3.zero;
                    platformDeltaRotation = Quaternion.identity;
                    platformRotationAccum = Quaternion.identity;
                    if (cameraController != null) cameraController.PlatformYawOffset = 0f;
            platformRotationAccum = Quaternion.identity;
                }
            }
            else
            {
                // No longer grounded or no ground object
                currentPlatform = null;
                platformVelocity = Vector3.zero;
                platformAngularVelocity = Vector3.zero;
                platformDeltaRotation = Quaternion.identity;
                platformRotationAccum = Quaternion.identity;
                if (cameraController != null)
                {
                    cameraController.AdjustYaw(cameraController.PlatformYawOffset);
                    cameraController.PlatformYawOffset = 0f;
                }
            }
        }

        public void HandleMovement()
        {
            if (mainCamera == null || playerInput == null) return;

            // Don't handle normal movement if climbing
            if (playerClimb != null && playerClimb.IsClimbing) return;

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
            currentMoveDirection = moveDirection;

            // Terrain navigation: adjust movement to follow terrain contours
            if (isGrounded && groundChecker != null)
            {
                moveDirection = AdjustForTerrain(moveDirection);
            }

            // Compute target velocity
            float speed = (runInput ? config.RunSpeed : config.WalkSpeed) * crouchSpeedMultiplier;

            // Toggle gravity based on grounded state
            rb.useGravity = !isGrounded;

            TargetVelocity = moveDirection * speed;

            // Apply platform velocity to target if on moving platform
            if (isGrounded && currentPlatform != null)
            {
                // Calculate platform velocity at player's position (translational + rotational)
                Vector3 playerRelativePos = rb.position - currentPlatform.position;
                Vector3 rotationalVelocity = Vector3.Cross(platformAngularVelocity, playerRelativePos);
                Vector3 totalPlatformVelocity = platformVelocity + rotationalVelocity;
                TargetVelocity += totalPlatformVelocity;
            }

            // Smooth velocity
            Vector3 velocityChange = TargetVelocity - rb.linearVelocity;

            // When airborne, preserve vertical velocity for natural gravity
            if (!isGrounded)
            {
                velocityChange.y = 0f;
            }

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
            DebugMovementForce = TargetVelocity;
        }

        private Vector3 AdjustForTerrain(Vector3 moveDirection)
        {
            if (moveDirection.magnitude < 0.01f) return moveDirection;

            // Get current player height
            float playerHeight = (playerCrouch != null && playerCrouch.IsCrouching) ? config.CrouchingHeight : config.StandingHeight;

            Vector3 center = transform.position + capsule.center;
            LayerMask groundLayer = groundChecker.GetGroundLayerMask();

            Vector3 adjustedDirection = moveDirection;

            // Offset raycast position in movement direction when moving
            Vector3 offset = moveDirection.magnitude > 0.01f ? moveDirection.normalized * 0.5f : Vector3.zero;
            Vector3 rayOrigin = center + offset;

            // Single raycast down from offset position
            RaycastHit groundHit;
            Vector3 groundPoint = rayOrigin + Vector3.down * config.SlopeDetectionRayDistance; // default
            if (Physics.Raycast(rayOrigin, Vector3.down, out groundHit, config.SlopeDetectionRayDistance, groundLayer))
            {
                groundPoint = groundHit.point;
                Vector3 groundNormal = groundHit.normal;

                // Calculate slope angle
                float slopeAngle = Vector3.Angle(Vector3.up, groundNormal);
                debugSlopeAngle = slopeAngle;

                // Align movement with ground normal
                adjustedDirection = Vector3.ProjectOnPlane(moveDirection, groundNormal);

                // Blend with original direction
                adjustedDirection = Vector3.Lerp(moveDirection, adjustedDirection, config.SlopeAlignmentStrength);
            }

            debugFootPoint = groundPoint;
            debugSlopePoint = groundPoint; // Same point for single ray

            return adjustedDirection;
        }

        // Public methods for configuration
        public void SetCrouchSpeedMultiplier(float multiplier)
        {
            crouchSpeedMultiplier = multiplier;
        }

        public float WalkSpeed => config.WalkSpeed;

        // Debug live values
        public Vector3 DebugMovementForce;

        // Debug visualization
        public void VisualizePlatform()
        {
            if (currentPlatform == null) return;

            // Highlight the platform
            Gizmos.color = Color.magenta;
            if (currentPlatform.TryGetComponent<Renderer>(out var renderer))
            {
                Gizmos.DrawWireCube(renderer.bounds.center, renderer.bounds.size);
            }
            else
            {
                Gizmos.DrawWireSphere(currentPlatform.position, 0.5f);
            }

            // Visualize platform velocity
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(currentPlatform.position, currentPlatform.position + debugPlatformVelocity);
            Gizmos.DrawSphere(currentPlatform.position + debugPlatformVelocity, 0.1f);
        }

        public void VisualizeTerrainRays()
        {
            if (!isGrounded) return;

            Vector3 center = transform.position + capsule.center;
            Vector3 offset = currentMoveDirection.magnitude > 0.01f ? currentMoveDirection.normalized * 0.5f : Vector3.zero;
            Vector3 rayOrigin = center + offset;

            // Visualize ray from offset position
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(rayOrigin, debugFootPoint);
            Gizmos.DrawSphere(debugFootPoint, 0.05f);

            // Visualize ground normal
            Gizmos.color = Color.green;
            Gizmos.DrawLine(debugFootPoint, debugFootPoint + Vector3.up * 0.5f); // Approximate normal direction

            // Show offset position
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(rayOrigin, 0.03f);
        }
    }
}