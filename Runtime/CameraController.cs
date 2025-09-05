using UnityEngine;

namespace ZacharysNewman.PPC
{
    [RequireComponent(typeof(PlayerInput))]
    public class CameraController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private CameraControllerConfig config;

        [Header("Dependencies")]
        [SerializeField] private Camera mainCamera;
        // Component references (auto-assigned)
        private PlayerInput playerInput;

        [Header("Fallback Settings")]
        [SerializeField] private float mouseSensitivity = 12f;
        [SerializeField] private bool invertY = false;
        [SerializeField] private float minVerticalAngle = -80f;
        [SerializeField] private float maxVerticalAngle = 80f;

        // Camera state
        private float cameraYaw = 0f;
        private float cameraPitch = 0f;
        private bool isMouseLocked = true;

        public bool IsMouseLocked => isMouseLocked;

        private void Awake()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            playerInput = GetComponent<PlayerInput>();
            InitializeAngles();
        }

        public void InitializeAngles()
        {
            if (mainCamera != null)
            {
                cameraYaw = mainCamera.transform.eulerAngles.y;
                cameraPitch = mainCamera.transform.eulerAngles.x;
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (playerInput != null)
            {
                HandleLook(playerInput.LookInput);
            }
        }

        public void HandleLook(Vector2 lookInput)
        {
            if (mainCamera == null || !isMouseLocked) return;

            // Use config values if available
            float sensitivity = config != null ? config.MouseSensitivity : mouseSensitivity;
            bool invert = config != null ? config.InvertY : invertY;
            float minAngle = config != null ? config.MinVerticalAngle : minVerticalAngle;
            float maxAngle = config != null ? config.MaxVerticalAngle : maxVerticalAngle;

            // Update yaw and pitch
            cameraYaw += lookInput.x * sensitivity * Time.deltaTime;
            cameraPitch += (invert ? 1 : -1) * lookInput.y * sensitivity * Time.deltaTime;

            // Clamp pitch
            cameraPitch = Mathf.Clamp(cameraPitch, minAngle, maxAngle);

            // Apply rotation
            mainCamera.transform.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
        }

        public void ToggleMouseLock()
        {
            isMouseLocked = !isMouseLocked;
            Cursor.lockState = isMouseLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !isMouseLocked;
        }

        // Public methods for configuration
        public void SetSensitivity(float sensitivity)
        {
            mouseSensitivity = sensitivity;
        }

        public void SetVerticalLimits(float minAngle, float maxAngle)
        {
            minVerticalAngle = minAngle;
            maxVerticalAngle = maxAngle;
        }

        public void SetInvertY(bool invert)
        {
            invertY = invert;
        }
    }
}