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

        [Header("Debug Configuration")]
        [SerializeField] private bool visualizeBounds = false;
        [SerializeField] private bool debugLogging = true;
        [SerializeField] private bool visualizeGroundCeilingChecks = true;
        [SerializeField] private bool visualizeVelocity = false;
        [SerializeField] private bool visualizeJump = true;

        // Public properties
        public PlayerMovementConfig MovementConfig => movementConfig;
        public PlayerJumpConfig JumpConfig => jumpConfig;
        public CameraControllerConfig CameraConfig => cameraConfig;
        public GroundCheckerConfig GroundCheckerConfig => groundCheckerConfig;
        public float InputSensitivity => inputSensitivity;
        public bool VisualizeBounds => visualizeBounds;
        public bool DebugLogging => debugLogging;
        public bool VisualizeGroundCeilingChecks => visualizeGroundCeilingChecks;
        public bool VisualizeVelocity => visualizeVelocity;
        public bool VisualizeJump => visualizeJump;

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

        public void SetVisualizationToggles(bool bounds, bool logging, bool groundChecks, bool velocity, bool jump)
        {
            visualizeBounds = bounds;
            debugLogging = logging;
            visualizeGroundCeilingChecks = groundChecks;
            visualizeVelocity = velocity;
            visualizeJump = jump;
        }
    }
}