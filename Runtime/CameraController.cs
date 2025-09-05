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

            // Ensure config is set
            if (config == null)
            {
                Debug.LogError("CameraController: Config is required. Please assign a CameraControllerConfig.");
            }
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

            // Use config values
            float sensitivity = config.MouseSensitivity;
            bool invert = config.InvertY;
            float minAngle = config.MinVerticalAngle;
            float maxAngle = config.MaxVerticalAngle;

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
    }
}