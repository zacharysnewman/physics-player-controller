using UnityEngine;

namespace ZacharysNewman.PPC
{
    [CreateAssetMenu(fileName = "PlayerControllerConfig", menuName = "PPC/Player Controller Config", order = 0)]
    public class PlayerControllerConfig : ScriptableObject
    {
        [Header("Component Configurations")]
        [SerializeField] private PlayerMovementConfig movementConfig;
        [SerializeField] private PlayerJumpConfig jumpConfig;
        [SerializeField] private CameraControllerConfig cameraConfig;
        [SerializeField] private GroundCheckerConfig groundCheckerConfig;

        [Header("Input Configuration")]
        [SerializeField] private float inputSensitivity = 1f;

        // Public properties
        public PlayerMovementConfig MovementConfig => movementConfig;
        public PlayerJumpConfig JumpConfig => jumpConfig;
        public CameraControllerConfig CameraConfig => cameraConfig;
        public GroundCheckerConfig GroundCheckerConfig => groundCheckerConfig;
        public float InputSensitivity => inputSensitivity;

        // Public methods for runtime modification
        public void SetComponentConfigs(
            PlayerMovementConfig movement,
            PlayerJumpConfig jump,
            CameraControllerConfig camera,
            GroundCheckerConfig groundChecker)
        {
            movementConfig = movement;
            jumpConfig = jump;
            cameraConfig = camera;
            groundCheckerConfig = groundChecker;
        }


    }
}