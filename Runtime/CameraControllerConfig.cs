using UnityEngine;

namespace ZacharysNewman.PPC
{
    [CreateAssetMenu(fileName = "CameraControllerConfig", menuName = "PPC/Camera Controller Config", order = 3)]
    public class CameraControllerConfig : ScriptableObject
    {
        [Header("Camera Settings")]
        [SerializeField] private float mouseSensitivity = 12f;
        [SerializeField] private bool invertY = false;
        [SerializeField] private float minVerticalAngle = -80f;
        [SerializeField] private float maxVerticalAngle = 80f;
        [SerializeField] private bool freezeYRotation = true;

        // Public properties
        public float MouseSensitivity => mouseSensitivity;
        public bool InvertY => invertY;
        public float MinVerticalAngle => minVerticalAngle;
        public float MaxVerticalAngle => maxVerticalAngle;
        public bool FreezeYRotation => freezeYRotation;

        // Public methods for runtime modification
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