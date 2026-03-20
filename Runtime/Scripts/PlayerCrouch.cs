using UnityEngine;

namespace ZacharysNewman.PPC
{
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(GroundChecker))]
    public class PlayerCrouch : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private PlayerCrouchConfig config;



        // Component references
        private CapsuleCollider capsule;
        private GroundChecker groundChecker;
        private CameraController cameraController;
        private PlayerMovement playerMovement;
        private VerticalVelocityLayer verticalLayer;

        // Crouch state
        private bool isCrouching;
        private bool wasCrouchPressed;
        private bool wasGrounded;
        private float originalHeight;
        private Vector3 originalCenter;
        private float currentHeight;
        private Vector3 currentCenter;

        // Public properties
        public bool IsCrouching => isCrouching;
        public Vector3 ColliderBottomPosition => transform.position + capsule.center - Vector3.up * (capsule.height / 2f);

        private void Awake()
        {
            capsule = GetComponent<CapsuleCollider>();
            groundChecker = GetComponent<GroundChecker>();
            cameraController = GetComponent<CameraController>();
            playerMovement = GetComponent<PlayerMovement>();
            verticalLayer = GetComponent<VerticalVelocityLayer>();

            // Store original capsule dimensions
            originalHeight = capsule.height;
            originalCenter = capsule.center;
            currentHeight = originalHeight;
            currentCenter = originalCenter;

            if (config == null)
            {
                Debug.LogError("PlayerCrouch: Config is required. Please assign a PlayerCrouchConfig.");
            }
        }

        private void Update()
        {
            bool isGrounded = groundChecker != null && groundChecker.IsGrounded;

            // When landing while crouching: the capsule was shifted upward (mid-air "lift legs" mode).
            // Switch to the grounded crouch position (shifted downward) so the collider sits correctly.
            // This preserves the intentional mid-air shift — it only corrects it on landing.
            if (isCrouching && !wasGrounded && isGrounded)
            {
                currentHeight = config.CrouchHeight;
                currentCenter = originalCenter + Vector3.down * (originalHeight - config.CrouchHeight) / 2f;
                if (cameraController != null)
                {
                    float heightAdjustment = (currentCenter.y - originalCenter.y) + (currentHeight - originalHeight) / 2f;
                    cameraController.AdjustHeight(heightAdjustment);
                }
            }

            wasGrounded = isGrounded;

            // Smoothly interpolate capsule dimensions
            capsule.height = Mathf.Lerp(capsule.height, currentHeight, Time.deltaTime * 10f);
            capsule.center = Vector3.Lerp(capsule.center, currentCenter, Time.deltaTime * 10f);
        }

        public void HandleCrouch(bool crouchInput)
        {
            if (groundChecker == null || config == null) return;

            bool canUncrouch = !IsCeilingBlocked();

            // Handle crouch state changes
            if (crouchInput && !wasCrouchPressed)
            {
                // Start crouching
                if (!isCrouching)
                {
                    StartCrouch();
                }
            }
            else if (!crouchInput && isCrouching && canUncrouch)
            {
                // Stop crouching if not blocked
                StopCrouch();
            }

            wasCrouchPressed = crouchInput;
        }

        private void StartCrouch()
        {
            isCrouching = true;

            if (groundChecker.IsGrounded)
            {
                // Grounded crouch: shrink downward
                currentHeight = config.CrouchHeight;
                currentCenter = originalCenter + Vector3.down * (originalHeight - currentHeight) / 2f;
            }
            else
            {
                // Midair crouch: shrink upward
                currentHeight = config.CrouchHeight;
                currentCenter = originalCenter + Vector3.up * (originalHeight - currentHeight) / 2f;

                // Optional velocity boost
                if (config.MidAirCrouchBoost > 0 && verticalLayer != null)
                {
                    verticalLayer.AddVerticalImpulse(config.MidAirCrouchBoost);
                }
            }

            // Adjust camera height to maintain offset from top of collider
            if (cameraController != null)
            {
                float heightAdjustment = (currentCenter.y - originalCenter.y) + (currentHeight - originalHeight) / 2f;
                cameraController.AdjustHeight(heightAdjustment);
            }

            // Reduce movement speed
            if (playerMovement != null)
            {
                playerMovement.SetCrouchSpeedMultiplier(config.CrouchSpeed / playerMovement.WalkSpeed);
            }
        }

        private void StopCrouch()
        {
            isCrouching = false;

            // Restore original dimensions
            currentHeight = originalHeight;
            currentCenter = originalCenter;

            // Restore camera
            if (cameraController != null)
            {
                cameraController.AdjustHeight(0);
            }

            // Restore movement speed
            if (playerMovement != null)
            {
                playerMovement.SetCrouchSpeedMultiplier(1f);
            }
        }

        private bool IsCeilingBlocked()
        {
            return groundChecker.IsCeilingBlocked;
        }
    }
}