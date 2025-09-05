using UnityEngine;

namespace ZacharysNewman.PPC
{
    [CreateAssetMenu(fileName = "PlayerJumpConfig", menuName = "PPC/Player Jump Config", order = 2)]
    public class PlayerJumpConfig : ScriptableObject
    {
        [Header("Jump Parameters")]
        [SerializeField] private float jumpForce = 10f;
        [SerializeField] private float jumpBufferTime = 0.2f;
        [SerializeField] private float coyoteTime = 0.1f;

        [Header("Debug Settings")]
        [SerializeField] private bool debugLogging = false;

        // Public properties
        public float JumpForce => jumpForce;
        public float JumpBufferTime => jumpBufferTime;
        public float CoyoteTime => coyoteTime;
        public bool DebugLogging => debugLogging;

        // Public methods for runtime modification
        public void SetJumpParameters(float force, float bufferTime, float coyote)
        {
            jumpForce = force;
            jumpBufferTime = bufferTime;
            coyoteTime = coyote;
        }

        public void SetDebugLogging(bool enabled)
        {
            debugLogging = enabled;
        }
    }
}