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
        private float heightOffset = 0f;
        private float currentHeightOffset = 0f;
        private float baseHeight;
        public float PlatformYawOffset = 0f;

        public bool IsMouseLocked => isMouseLocked;

        private void Awake()
        {
            // mainCamera must be assigned in inspector

            playerInput = GetComponent<PlayerInput>();
            baseHeight = mainCamera.transform.localPosition.y;
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
                float rawPitch = mainCamera.transform.eulerAngles.x;
                // Unity returns eulerAngles.x in 0–360; downward pitches come back as 270–360.
                // Normalize to -180..180 so the clamp in HandleLook works correctly.
                cameraPitch = rawPitch > 180f ? rawPitch - 360f : rawPitch;
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

        private void LateUpdate()
        {
            // Smoothly interpolate height offset
            currentHeightOffset = Mathf.Lerp(currentHeightOffset, heightOffset, Time.deltaTime * 10f);

            if (mainCamera != null)
            {
                mainCamera.transform.localPosition = new Vector3(
                    mainCamera.transform.localPosition.x,
                    baseHeight + currentHeightOffset,
                    mainCamera.transform.localPosition.z
                );
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
            mainCamera.transform.localRotation = Quaternion.Euler(cameraPitch, cameraYaw + PlatformYawOffset, 0f);
        }

        public void ToggleMouseLock()
        {
            isMouseLocked = !isMouseLocked;
            Cursor.lockState = isMouseLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !isMouseLocked;
        }

        // Public methods for configuration
        public void AdjustHeight(float offset)
        {
            heightOffset = offset;
        }

        public void AdjustYaw(float delta)
        {
            cameraYaw += delta;
        }

        /// <summary>
        /// The player's facing direction in world space (horizontal only, derived from camera yaw).
        /// Use this instead of transform.forward when the Rigidbody freezes Y-rotation.
        /// </summary>
        public Vector3 FacingDirection
        {
            get
            {
                float radYaw = (cameraYaw + PlatformYawOffset) * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(radYaw), 0f, Mathf.Cos(radYaw));
            }
        }
    }
}