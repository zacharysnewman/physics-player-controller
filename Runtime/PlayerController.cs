using UnityEngine;
using UnityEngine.InputSystem;

namespace ZacharysNewman.PPC
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class PlayerController : MonoBehaviour
    {
        // Rigidbody and CapsuleCollider settings are configured directly on the components

        [Header("Child Transforms")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private Transform ceilingCheck;
        [SerializeField] private Transform frontCheck;

        [Header("Ground Check Settings")]
        [SerializeField] private float groundCheckRadius = 0.4f;
        [SerializeField] private float groundCheckDistance = 0.1f;
        [SerializeField] private LayerMask groundLayerMask = -1;

        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float runSpeed = 10f;
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float deceleration = 10f;
        [SerializeField] private float reverseDeceleration = 20f;
        [SerializeField] private float maxVelocityChange = 10f;

        [Header("Ceiling Check Settings")]
        [SerializeField] private float ceilingCheckRadius = 0.4f;
        [SerializeField] private float ceilingCheckDistance = 0.1f;
        [SerializeField] private LayerMask ceilingLayerMask = -1;

        [Header("Rotation Settings")]
        [SerializeField] private bool freezeYRotation = true;

        [Header("Camera Settings")]
        [SerializeField] private float mouseSensitivity = 12f;
        [SerializeField] private bool invertY = false;
        [SerializeField] private float minVerticalAngle = -80f;
        [SerializeField] private float maxVerticalAngle = 80f;

        [Header("Debug")]
        [SerializeField] private bool visualizeBounds = false;
        [SerializeField] private bool debugInput = false;
        [SerializeField] private bool visualizeGroundCeilingChecks = false;
        [SerializeField] private bool visualizeVelocity = false;

        private Rigidbody rb;
        private CapsuleCollider capsule;
        private PlayerControls playerControls;

        // Utility classes
        private PlayerInputHandler inputHandler;
        private PlayerMovement movement;
        private GroundChecker groundChecker;
        private CameraController cameraController;
        private DebugVisualizer debugVisualizer;

        // Rotation state
        private bool previousFreezeYRotation;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();

            // Setup Rigidbody constraints (other settings configured on component)
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | (freezeYRotation ? RigidbodyConstraints.FreezeRotationY : 0);
            previousFreezeYRotation = freezeYRotation;

            // CapsuleCollider settings configured on component

            // Create child transforms if not assigned
            if (groundCheck == null)
            {
                groundCheck = new GameObject("GroundCheck").transform;
                groundCheck.parent = transform;
                groundCheck.localPosition = Vector3.down * (capsule.height / 2f - capsule.radius);
            }
            if (ceilingCheck == null)
            {
                ceilingCheck = new GameObject("CeilingCheck").transform;
                ceilingCheck.parent = transform;
                ceilingCheck.localPosition = Vector3.up * (capsule.height / 2f - capsule.radius);
            }
            if (frontCheck == null)
            {
                frontCheck = new GameObject("FrontCheck").transform;
                frontCheck.parent = transform;
                frontCheck.localPosition = Vector3.forward * capsule.radius;
            }

            // Initialize utility classes
            playerControls = new PlayerControls();
            cameraController = new CameraController(mouseSensitivity, invertY, minVerticalAngle, maxVerticalAngle, freezeYRotation);
            inputHandler = new PlayerInputHandler(playerControls,
                () => { if (cameraController.IsMouseLocked) cameraController.ToggleMouseLock(); }, // Menu: unlock if locked
                () => { if (!cameraController.IsMouseLocked) cameraController.ToggleMouseLock(); }); // Use: lock if unlocked
            movement = new PlayerMovement(rb, transform, walkSpeed, runSpeed, acceleration, deceleration, reverseDeceleration, maxVelocityChange);
            groundChecker = new GroundChecker(groundCheck, ceilingCheck, groundCheckRadius, groundCheckDistance, groundLayerMask, ceilingCheckRadius, ceilingCheckDistance, ceilingLayerMask);
            debugVisualizer = new DebugVisualizer(transform, capsule, groundCheck, ceilingCheck, visualizeBounds, debugInput, visualizeGroundCeilingChecks, visualizeVelocity);

            cameraController.InitializeAngles();
        }

        private void OnEnable()
        {
            inputHandler.Enable();
        }

        private void OnDisable()
        {
            inputHandler.Disable();
        }

        private void OnDestroy()
        {
            inputHandler.Dispose();
        }





        public void SetFreezeYRotation(bool freeze)
        {
            freezeYRotation = freeze;
        }

        private void Update()
        {
            // Update Y rotation constraints if changed
            if (previousFreezeYRotation != freezeYRotation)
            {
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | (freezeYRotation ? RigidbodyConstraints.FreezeRotationY : 0);
                previousFreezeYRotation = freezeYRotation;
            }

            // Handle camera look
            cameraController.HandleLook(inputHandler.LookInput);

            // Update ground checker
            groundChecker.CheckGround();
            groundChecker.CheckCeiling();

            // Update movement with ground info
            movement.UpdateGrounded(groundChecker.IsGrounded, groundChecker.GroundNormal);

            // Debug logging
            debugVisualizer.LogInput(inputHandler.MoveInput, inputHandler.RunInput, inputHandler.CrouchInput, inputHandler.JumpInput, inputHandler.InteractInput);

            // Update debug visualizer
            debugVisualizer.UpdateDebugInfo(groundChecker.IsGrounded, groundChecker.GroundNormal, movement.CurrentVelocity, movement.TargetVelocity);
        }

        private void FixedUpdate()
        {
            // Handle movement
            movement.HandleMovement(inputHandler.MoveInput, inputHandler.RunInput, Camera.main);
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            debugVisualizer.DrawGizmos();
        }


    }
}