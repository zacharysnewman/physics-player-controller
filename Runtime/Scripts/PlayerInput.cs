using UnityEngine;
using UnityEngine.InputSystem;

namespace ZacharysNewman.PPC
{
    public class PlayerInput : MonoBehaviour
    {
        [Header("Input Configuration")]
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

        // Debug properties — use the cached component instead of GetComponent every frame
        public string CurrentControlScheme => playerInputComponent?.currentControlScheme ?? "None";
        public int PlayerIndex => playerInputComponent?.playerIndex ?? -1;

        private UnityEngine.InputSystem.PlayerInput playerInputComponent;

        private void Awake()
        {
            playerInputComponent = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        }

        private void Start()
        {
            if (cameraController == null)
            {
                cameraController = GetComponent<CameraController>();
            }
        }

        private void Update()
        {
            if (playerInputComponent == null || playerInputComponent.actions == null) return;

            // Poll input actions every frame, matching the original continuous update
            // Sensitivity applies to look only; MoveInput must stay in [-1,1] so speed calculations are correct
            MoveInput = playerInputComponent.actions["Move"].ReadValue<Vector2>();
            LookInput = playerInputComponent.actions["Look"].ReadValue<Vector2>() * inputSensitivity;
            RunInput = playerInputComponent.actions["Run"].IsPressed();
            CrouchInput = playerInputComponent.actions["Crouch"].IsPressed();
            JumpInput = playerInputComponent.actions["Jump"].IsPressed();
            InteractInput = playerInputComponent.actions["Interact"].IsPressed();

            // Handle menu and use presses (triggered on press, like original performed)
            if (playerInputComponent.actions["Menu"].WasPressedThisFrame())
            {
                if (cameraController != null && cameraController.IsMouseLocked)
                {
                    cameraController.ToggleMouseLock();
                }
                OnMenuAction?.Invoke();
            }

            if (playerInputComponent.actions["Use"].WasPressedThisFrame())
            {
                if (cameraController != null && !cameraController.IsMouseLocked)
                {
                    cameraController.ToggleMouseLock();
                }
                OnUseAction?.Invoke();
            }
        }

        // Public methods for external control (now handled by PlayerInput component)
        public void EnableInput()
        {
            if (playerInputComponent != null)
            {
                playerInputComponent.enabled = true;
            }
        }

        public void DisableInput()
        {
            if (playerInputComponent != null)
            {
                playerInputComponent.enabled = false;
            }
        }
    }
}