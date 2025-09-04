using UnityEngine;

namespace ZacharysNewman.PPC
{
    public class CameraController
    {
        private float mouseSensitivity;
        private bool invertY;
        private float minVerticalAngle;
        private float maxVerticalAngle;
        private bool freezeYRotation;

        private float cameraYaw = 0f;
        private float cameraPitch = 0f;
        private bool isMouseLocked = true;

        public CameraController(float sensitivity, bool invert, float minAngle, float maxAngle, bool freezeY)
        {
            mouseSensitivity = sensitivity;
            invertY = invert;
            minVerticalAngle = minAngle;
            maxVerticalAngle = maxAngle;
            freezeYRotation = freezeY;
        }

        public void InitializeAngles()
        {
            if (Camera.main != null)
            {
                cameraYaw = Camera.main.transform.eulerAngles.y;
                cameraPitch = Camera.main.transform.eulerAngles.x;
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void HandleLook(Vector2 lookInput)
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

        public void ToggleMouseLock()
        {
            isMouseLocked = !isMouseLocked;
            Cursor.lockState = isMouseLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !isMouseLocked;
        }

        public bool IsMouseLocked => isMouseLocked;
    }
}