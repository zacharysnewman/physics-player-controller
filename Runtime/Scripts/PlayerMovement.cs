using UnityEngine;

namespace ZacharysNewman.PPC
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(GroundChecker))]
    public class PlayerMovement : MonoBehaviour, IVelocityLayer
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
        private CapsuleCollider capsule;

        // Movement state
        private bool isGrounded;
        private Vector3 groundNormal;
        private float crouchSpeedMultiplier = 1f;
        private Vector3 currentHorizontalVelocity;

        // Moving platform support
        private Transform currentPlatform;
        private Vector3 previousPlatformPosition;
        private Quaternion previousPlatformRotation;
        private Vector3 platformVelocity;
        private Vector3 platformAngularVelocity;
        private Quaternion platformDeltaRotation;
        private Quaternion platformRotationAccum;

        public Vector3 BaseVelocity { get; private set; }
        public Vector3 TargetVelocity { get; private set; }
        public Vector3 CurrentVelocity { get; private set; }
        private Vector3 currentMoveDirection;
        private Vector3 debugFootPoint;
        private Vector3 debugSlopePoint;
        private float debugSlopeAngle;

        // Moving platform debug
        private Vector3 debugPlatformVelocity;

        // Step handling debug
        private Vector3 debugStepRayOrigin;
        private Vector3 debugStepHitPoint;
        private bool debugStepDetected;

        // IVelocityLayer
        public bool IsActive => true;
        public bool IsExclusive => false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            groundChecker = GetComponent<GroundChecker>();
            playerInput = GetComponent<PlayerInput>();
            playerCrouch = GetComponent<PlayerCrouch>();
            capsule = GetComponent<CapsuleCollider>();

            if (cameraController == null)
            {
                cameraController = GetComponent<CameraController>();
            }

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

            if (groundChecker == null)
            {
                Debug.LogError("PlayerMovement: GroundChecker component is required but not found!");
                enabled = false;
                return;
            }

            if (mainCamera == null)
            {
                Debug.LogError("PlayerMovement: mainCamera must be assigned in the inspector!");
                enabled = false;
                return;
            }
        }

        private void TrackPlatformMovement()
        {
            if (currentPlatform == null)
            {
                BaseVelocity = Vector3.zero;
                return;
            }

            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return;

            Vector3 currentPosition = currentPlatform.position;
            Quaternion currentRotation = currentPlatform.rotation;

            Rigidbody platformRb = currentPlatform.GetComponent<Rigidbody>();
            if (platformRb != null && !platformRb.isKinematic)
            {
                // Non-kinematic: read velocity directly from the physics body
                Vector3 playerRelativePos = rb.position - currentPlatform.position;
                Vector3 rotationalVelocity = Vector3.Cross(platformRb.angularVelocity, playerRelativePos);
                BaseVelocity = platformRb.linearVelocity + rotationalVelocity;
                platformAngularVelocity = platformRb.angularVelocity;
            }
            else
            {
                // Kinematic or static: compute from transform delta
                platformVelocity = (currentPosition - previousPlatformPosition) / dt;

                platformDeltaRotation = currentRotation * Quaternion.Inverse(previousPlatformRotation);
                platformDeltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
                platformAngularVelocity = axis * (angle * Mathf.Deg2Rad / dt);

                Vector3 playerRelativePos = rb.position - currentPlatform.position;
                Vector3 rotationalVelocity = Vector3.Cross(platformAngularVelocity, playerRelativePos);
                BaseVelocity = platformVelocity + rotationalVelocity;
                debugPlatformVelocity = BaseVelocity;
            }

            // Update camera yaw from platform rotation delta
            Quaternion rotDelta = currentRotation * Quaternion.Inverse(previousPlatformRotation);
            platformRotationAccum = platformRotationAccum * rotDelta;
            if (cameraController != null)
            {
                cameraController.PlatformYawOffset = platformRotationAccum.eulerAngles.y;
            }

            previousPlatformPosition = currentPosition;
            previousPlatformRotation = currentRotation;
        }

        public void UpdateGrounded(bool grounded, Vector3 normal, Transform groundObject)
        {
            isGrounded = grounded;
            groundNormal = normal;

            if (grounded && groundObject != null)
            {
                if (currentPlatform != groundObject)
                {
                    currentPlatform = groundObject;
                    previousPlatformPosition = currentPlatform.position;
                    previousPlatformRotation = currentPlatform.rotation;
                    platformVelocity = Vector3.zero;
                    platformAngularVelocity = Vector3.zero;
                    platformDeltaRotation = Quaternion.identity;
                    platformRotationAccum = Quaternion.identity;
                    if (cameraController != null) cameraController.PlatformYawOffset = 0f;
                }
            }
            else
            {
                currentPlatform = null;
                BaseVelocity = Vector3.zero;
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

        public Vector3 GetVelocityContribution(float deltaTime)
        {
            if (mainCamera == null || playerInput == null) return currentHorizontalVelocity;

            TrackPlatformMovement();

            Vector2 moveInput = playerInput.MoveInput;
            bool runInput = playerInput.RunInput;

            // Camera-relative direction
            Vector3 cameraForward = mainCamera.transform.forward;
            cameraForward.y = 0f;
            cameraForward.Normalize();

            Vector3 cameraRight = mainCamera.transform.right;
            cameraRight.y = 0f;
            cameraRight.Normalize();

            Vector3 moveDirection = cameraForward * moveInput.y + cameraRight * moveInput.x;
            currentMoveDirection = moveDirection;

            // Terrain navigation
            if (isGrounded && groundChecker != null)
            {
                moveDirection = AdjustForTerrain(moveDirection);
            }

            float speed = (runInput ? config.RunSpeed : config.WalkSpeed) * crouchSpeedMultiplier;
            Vector3 playerTargetVelocity = moveDirection * speed;

            // Relative-space acceleration loop (Unreal-inspired platform frame)
            Vector3 baseHorizontal = new Vector3(BaseVelocity.x, 0f, BaseVelocity.z);
            Vector3 relativeVelocity = currentHorizontalVelocity - baseHorizontal;
            Vector3 relativeTarget = playerTargetVelocity;
            Vector3 relativeDelta = relativeTarget - relativeVelocity;
            relativeDelta = Vector3.ClampMagnitude(relativeDelta, config.MaxVelocityChange);

            float dot = relativeVelocity.magnitude > 0.01f && relativeTarget.magnitude > 0.01f
                ? Vector3.Dot(relativeVelocity.normalized, relativeTarget.normalized)
                : 1f;

            float accelRate;
            if (moveInput.magnitude > 0.1f)
            {
                accelRate = (dot < -0.1f) ? config.ReverseDeceleration : config.Acceleration;
            }
            else
            {
                accelRate = config.Deceleration;
            }

            Vector3 relativeChange = Vector3.MoveTowards(Vector3.zero, relativeDelta, accelRate * deltaTime);
            currentHorizontalVelocity = relativeVelocity + relativeChange + baseHorizontal;

            TargetVelocity = playerTargetVelocity + baseHorizontal;
            CurrentVelocity = currentHorizontalVelocity;
            DebugMovementForce = relativeChange / deltaTime;

            return currentHorizontalVelocity;
        }

        public void ResetHorizontalVelocity()
        {
            currentHorizontalVelocity = Vector3.zero;
        }

        private Vector3 AdjustForTerrain(Vector3 moveDirection)
        {
            if (moveDirection.magnitude < 0.01f) return moveDirection;

            float playerHeight = (playerCrouch != null && playerCrouch.IsCrouching) ? config.CrouchingHeight : config.StandingHeight;

            Vector3 center = transform.position + capsule.center;
            LayerMask groundLayer = groundChecker.GetGroundLayerMask();

            Vector3 adjustedDirection = moveDirection;

            Vector3 offset = moveDirection.magnitude > 0.01f ? moveDirection.normalized * 0.5f : Vector3.zero;
            Vector3 rayOrigin = center + offset;

            RaycastHit groundHit;
            Vector3 groundPoint = rayOrigin + Vector3.down * config.SlopeDetectionRayDistance;
            if (Physics.Raycast(rayOrigin, Vector3.down, out groundHit, config.SlopeDetectionRayDistance, groundLayer))
            {
                groundPoint = groundHit.point;
                Vector3 groundNormal = groundHit.normal;

                float slopeAngle = Vector3.Angle(Vector3.up, groundNormal);
                debugSlopeAngle = slopeAngle;

                adjustedDirection = Vector3.ProjectOnPlane(moveDirection, groundNormal);
                adjustedDirection = Vector3.Lerp(moveDirection, adjustedDirection, config.SlopeAlignmentStrength);
            }

            debugFootPoint = groundPoint;
            debugSlopePoint = groundPoint;

            HandleStep(moveDirection);

            return adjustedDirection;
        }

        private void HandleStep(Vector3 moveDirection)
        {
            if (moveDirection.magnitude < 0.01f) return;

            Vector3 rayOrigin = transform.position + moveDirection.normalized * 0.51f;
            debugStepRayOrigin = rayOrigin;

            RaycastHit stepHit;
            if (Physics.Raycast(rayOrigin, Vector3.down, out stepHit, 2f, groundChecker.GetGroundLayerMask()))
            {
                debugStepHitPoint = stepHit.point;
                debugStepDetected = true;

                float playerBottomY = transform.position.y - 1f;
                float stepHeight = stepHit.point.y - playerBottomY;

                if (stepHeight > 0f && stepHeight <= config.MaxStepHeight)
                {
                    Vector3 newPosition = rb.position + Vector3.up * stepHeight;
                    rb.MovePosition(newPosition);
                }
            }
            else
            {
                debugStepDetected = false;
            }
        }

        public void SetCrouchSpeedMultiplier(float multiplier)
        {
            crouchSpeedMultiplier = multiplier;
        }

        public float WalkSpeed => config.WalkSpeed;

        public Vector3 DebugMovementForce;

        public void VisualizePlatform()
        {
            if (currentPlatform == null) return;

            Gizmos.color = Color.magenta;
            if (currentPlatform.TryGetComponent<Renderer>(out var renderer))
            {
                Gizmos.DrawWireCube(renderer.bounds.center, renderer.bounds.size);
            }
            else
            {
                Gizmos.DrawWireSphere(currentPlatform.position, 0.5f);
            }

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

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(rayOrigin, debugFootPoint);
            Gizmos.DrawSphere(debugFootPoint, 0.05f);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(debugFootPoint, debugFootPoint + Vector3.up * 0.5f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(rayOrigin, 0.03f);
        }

        public void VisualizeStepRays()
        {
            if (!isGrounded || currentMoveDirection.magnitude < 0.01f) return;

            Gizmos.color = debugStepDetected ? Color.red : Color.gray;
            Gizmos.DrawLine(debugStepRayOrigin, debugStepRayOrigin + Vector3.down * 2f);
            Gizmos.DrawSphere(debugStepRayOrigin, 0.03f);

            if (debugStepDetected)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(debugStepHitPoint, 0.05f);
            }
        }
    }
}
