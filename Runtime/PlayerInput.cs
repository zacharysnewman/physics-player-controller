using UnityEngine;
using UnityEngine.InputSystem;

namespace ZacharysNewman.PPC
{
    public class PlayerInput : MonoBehaviour
    {
        [Header("Input Configuration")]
        [SerializeField] private PlayerControls playerControls;
        [SerializeField] private float inputSensitivity = 1f;

        // Input state
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool RunInput { get; private set; }
        public bool CrouchInput { get; private set; }
        public bool JumpInput { get; private set; }
        public bool InteractInput { get; private set; }

        // Events for menu and use actions
        public System.Action OnMenuAction { get; set; }
        public System.Action OnUseAction { get; set; }

        // Reference to CameraController for menu/use actions
        [SerializeField] private CameraController cameraController;

        private void Awake()
        {
            if (playerControls == null)
            {
                playerControls = new PlayerControls();
            }

            // Set up input callbacks
            playerControls.Player.Move.performed += ctx => MoveInput = ctx.ReadValue<Vector2>() * inputSensitivity;
            playerControls.Player.Move.canceled += ctx => MoveInput = Vector2.zero;

            playerControls.Player.Look.performed += ctx => LookInput = ctx.ReadValue<Vector2>() * inputSensitivity;
            playerControls.Player.Look.canceled += ctx => LookInput = Vector2.zero;

            playerControls.Player.Run.performed += ctx => RunInput = true;
            playerControls.Player.Run.canceled += ctx => RunInput = false;

            playerControls.Player.Crouch.performed += ctx => CrouchInput = true;
            playerControls.Player.Crouch.canceled += ctx => CrouchInput = false;

            playerControls.Player.Jump.performed += ctx => JumpInput = true;
            playerControls.Player.Jump.canceled += ctx => JumpInput = false;

            playerControls.Player.Interact.performed += ctx => InteractInput = true;
            playerControls.Player.Interact.canceled += ctx => InteractInput = false;

            // Set up menu and use actions with CameraController
            playerControls.Player.Menu.performed += ctx =>
            {
                if (cameraController != null && cameraController.IsMouseLocked)
                {
                    cameraController.ToggleMouseLock();
                }
                OnMenuAction?.Invoke();
            };
            playerControls.Player.Use.performed += ctx =>
            {
                if (cameraController != null && !cameraController.IsMouseLocked)
                {
                    cameraController.ToggleMouseLock();
                }
                OnUseAction?.Invoke();
            };
        }

        private void Start()
        {
            if (cameraController == null)
            {
                cameraController = GetComponent<CameraController>();
            }
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

        // Public methods for external control
        public void EnableInput()
        {
            playerControls.Player.Enable();
        }

        public void DisableInput()
        {
            playerControls.Player.Disable();
        }
    }
}