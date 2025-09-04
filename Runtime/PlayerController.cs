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

        // Input values
        private Vector2 moveInput;
        private Vector2 lookInput;
        private bool runInput;
        private bool crouchInput;
        private bool jumpInput;
        private bool interactInput;

        // Ground/Ceiling state
        private bool isGrounded;
        private Vector3 groundNormal;
        private float groundSlopeAngle;

        // Movement
        private Vector3 targetVelocity;
        private Vector3 currentVelocity;

        // Rotation state
        private bool previousFreezeYRotation;

        // Camera look state
        private float cameraYaw = 0f;
        private float cameraPitch = 0f;
        private bool isMouseLocked = true;

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

            // Setup new input system
            playerControls = new PlayerControls();

            // Initialize camera angles and mouse lock
            if (Camera.main != null)
            {
                cameraYaw = Camera.main.transform.eulerAngles.y;
                cameraPitch = Camera.main.transform.eulerAngles.x;
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Set up input callbacks
            playerControls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
            playerControls.Player.Move.canceled += ctx => moveInput = Vector2.zero;

            playerControls.Player.Look.performed += ctx => { if (isMouseLocked) lookInput = ctx.ReadValue<Vector2>(); };
            playerControls.Player.Look.canceled += ctx => { if (isMouseLocked) lookInput = Vector2.zero; };

            playerControls.Player.Run.performed += ctx => runInput = true;
            playerControls.Player.Run.canceled += ctx => runInput = false;

            playerControls.Player.Crouch.performed += ctx => crouchInput = true;
            playerControls.Player.Crouch.canceled += ctx => crouchInput = false;

            playerControls.Player.Jump.performed += ctx => jumpInput = true;
            playerControls.Player.Jump.canceled += ctx => jumpInput = false;

            playerControls.Player.Interact.performed += ctx => interactInput = true;
            playerControls.Player.Interact.canceled += ctx => interactInput = false;

            // Menu action unlocks mouse
            playerControls.Player.Menu.performed += ctx => { if (isMouseLocked) ToggleMouseLock(); };

            // Use action locks mouse when unlocked
            playerControls.Player.Use.performed += ctx => { if (!isMouseLocked) ToggleMouseLock(); };
        }

        private void OnEnable()
        {
            playerControls.Player.Enable();
        }

        private void OnDisable()
        {
            playerControls.Player.Disable();
        }

        private void OnDestroy()
        {
            playerControls.Dispose();
        }





        private void Update()
        {
            if (debugInput)
            {
                Debug.Log($"Move Input: {moveInput}, Run: {runInput}, Crouch: {crouchInput}, Jump: {jumpInput}, Interact: {interactInput}");
            }

            // Update Y rotation constraints if changed
            if (previousFreezeYRotation != freezeYRotation)
            {
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | (freezeYRotation ? RigidbodyConstraints.FreezeRotationY : 0);
                previousFreezeYRotation = freezeYRotation;
            }

            HandleCameraLook();
            CheckGrounded();
            CheckCeiling();
        }

        private void FixedUpdate()
        {
            HandleMovement();
        }

        private void HandleMovement()
        {
            // Get camera forward and right for relative movement
            Vector3 cameraForward = Camera.main.transform.forward;
            cameraForward.y = 0f;
            cameraForward.Normalize();

            Vector3 cameraRight = Camera.main.transform.right;
            cameraRight.y = 0f;
            cameraRight.Normalize();

            // Convert input to world space
            Vector3 moveDirection = cameraForward * moveInput.y + cameraRight * moveInput.x;

            // Project onto ground plane
            if (isGrounded)
            {
                moveDirection = moveDirection - Vector3.Project(moveDirection, groundNormal);
            }

            // Compute target velocity
            float speed = runInput ? runSpeed : walkSpeed;
            targetVelocity = moveDirection * speed;

            // Smooth velocity
            Vector3 velocityChange = targetVelocity - rb.linearVelocity;
            velocityChange.y = 0f; // Don't affect vertical velocity

            // Limit velocity change
            if (velocityChange.magnitude > maxVelocityChange)
            {
                velocityChange = velocityChange.normalized * maxVelocityChange;
            }

            // Apply acceleration/deceleration with faster reverse deceleration
            Vector3 currentHorizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            Vector3 targetHorizontalVelocity = new Vector3(targetVelocity.x, 0, targetVelocity.z);
            float dot = Vector3.Dot(currentHorizontalVelocity.normalized, targetHorizontalVelocity.normalized);
            float accel;
            if (moveInput.magnitude > 0.1f)
            {
                accel = (dot < -0.1f) ? reverseDeceleration : acceleration;
            }
            else
            {
                accel = deceleration;
            }
            velocityChange = Vector3.MoveTowards(Vector3.zero, velocityChange, accel * Time.fixedDeltaTime);

            // Apply to rigidbody
            rb.AddForce(velocityChange, ForceMode.VelocityChange);

            currentVelocity = rb.linearVelocity;
        }

        private void HandleCameraLook()
        {
            if (Camera.main == null || !isMouseLocked) return;

            // Update yaw and pitch
            cameraYaw += lookInput.x * mouseSensitivity * Time.deltaTime;
            cameraPitch += (invertY ? 1 : -1) * lookInput.y * mouseSensitivity * Time.deltaTime;

            // Clamp pitch
            cameraPitch = Mathf.Clamp(cameraPitch, minVerticalAngle, maxVerticalAngle);

            // Apply rotation
            Camera.main.transform.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
        }

        private void ToggleMouseLock()
        {
            isMouseLocked = !isMouseLocked;
            Cursor.lockState = isMouseLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !isMouseLocked;
        }

        private void CheckGrounded()
        {
            RaycastHit hit;
            if (Physics.SphereCast(groundCheck.position, groundCheckRadius, Vector3.down, out hit, groundCheckDistance, groundLayerMask))
            {
                isGrounded = true;
                groundNormal = hit.normal;
                groundSlopeAngle = Vector3.Angle(Vector3.up, groundNormal);
            }
            else
            {
                isGrounded = false;
                groundNormal = Vector3.up;
                groundSlopeAngle = 0f;
            }
        }

        private void CheckCeiling()
        {
            // For ceiling check, we can use a similar spherecast upward
            RaycastHit hit;
            if (Physics.SphereCast(ceilingCheck.position, ceilingCheckRadius, Vector3.up, out hit, ceilingCheckDistance, ceilingLayerMask))
            {
                // Store if ceiling is hit, but for now just check
            }
        }

        public void SetFreezeYRotation(bool freeze)
        {
            freezeYRotation = freeze;
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

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
                Gizmos.color = isGrounded ? Color.blue : Color.red;
                Gizmos.DrawWireSphere(groundCheck.position + Vector3.down * groundCheckDistance, groundCheckRadius);

                // Ceiling check
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(ceilingCheck.position + Vector3.up * ceilingCheckDistance, ceilingCheckRadius);

                // Ground normal
                if (isGrounded)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(groundCheck.position, groundCheck.position + groundNormal);
                }
            }

            if (visualizeVelocity)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, transform.position + currentVelocity);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, transform.position + targetVelocity);
            }
        }


    }
}