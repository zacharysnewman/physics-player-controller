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

        // Public properties
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float ReverseDeceleration => reverseDeceleration;
        public float MaxVelocityChange => maxVelocityChange;

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