using UnityEngine;

namespace ZacharysNewman.PPC
{
    [CreateAssetMenu(fileName = "PlayerMovementConfig", menuName = "PPC/Player Movement Config", order = 1)]
    public class PlayerMovementConfig : ScriptableObject
    {
        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float runSpeed = 10f;

        [Header("Acceleration Settings")]
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float deceleration = 10f;
        [SerializeField] private float reverseDeceleration = 20f;
        [SerializeField] private float maxVelocityChange = 10f;

        [Header("Acceleration-Based Settings (New)")]
        [SerializeField] private float maxAcceleration = 500f; // maxVelocityChange / Time.fixedDeltaTime

        [Header("Terrain Navigation")]
        [SerializeField] private float slopeAlignmentStrength = 0.5f;
        [SerializeField] private float standingHeight = 2f;
        [SerializeField] private float crouchingHeight = 1f;
        [SerializeField] private float slopeDetectionRayDistance = 3f;

        [Header("Moving Platform Support")]
        [SerializeField] private float velocityMultiplier = 1f;
        [SerializeField] private float maxRotationSpeed = 360f;


        // Public properties
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float ReverseDeceleration => reverseDeceleration;
        public float MaxVelocityChange => maxVelocityChange;
        public float MaxAcceleration => maxAcceleration;
        public float SlopeAlignmentStrength => slopeAlignmentStrength;
        public float StandingHeight => standingHeight;
        public float CrouchingHeight => crouchingHeight;
        public float SlopeDetectionRayDistance => slopeDetectionRayDistance;
        public float VelocityMultiplier => velocityMultiplier;
        public float MaxRotationSpeed => maxRotationSpeed;


        // Public methods for runtime modification
        public void SetMovementSpeeds(float walk, float run)
        {
            walkSpeed = walk;
            runSpeed = run;
        }

        public void SetAcceleration(float accel, float decel, float revDecel)
        {
            acceleration = accel;
            deceleration = decel;
            reverseDeceleration = revDecel;
        }
    }
}