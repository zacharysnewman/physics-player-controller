using UnityEngine;

namespace ZacharysNewman.PPC
{
    [RequireComponent(typeof(Rigidbody))]
    public class VerticalVelocityLayer : MonoBehaviour, IVelocityLayer
    {
        [SerializeField] private float gravityScale = 1f;

        private Rigidbody rb;
        private float accumulatedY;
        private float platformY;
        private float lastPlatformY;
        private bool isGrounded;
        private float lastTargetY;

        public bool IsActive => true;
        public bool IsExclusive => false;

        public float AccumulatedY => accumulatedY;
        public float GravityScale { get => gravityScale; set => gravityScale = value; }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
        }

        public Vector3 GetVelocityContribution(float deltaTime)
        {
            if (isGrounded)
            {
                // Absorb significant external upward forces (launch pads, explosions) while grounded.
                // lastTargetY was 0 (platformY) last step; any upward deviation is external.
                float externalDelta = rb.linearVelocity.y - lastTargetY;
                if (externalDelta > 0.1f)
                {
                    // Launch off the ground and let gravity take it from here
                    accumulatedY = rb.linearVelocity.y;
                    isGrounded = false;
                    accumulatedY += Physics.gravity.y * gravityScale * deltaTime;
                }
                else
                {
                    accumulatedY = platformY;
                }
            }
            else
            {
                // Absorb external vertical forces (gravity pads, explosions, collisions).
                // lastTargetY is what we drove rb.linearVelocity.y toward last step;
                // any remaining deviation came from external forces in the same physics step.
                float externalDelta = rb.linearVelocity.y - lastTargetY;
                if (Mathf.Abs(externalDelta) > 0.01f)
                    accumulatedY += externalDelta;

                accumulatedY += Physics.gravity.y * gravityScale * deltaTime;
            }

            lastTargetY = accumulatedY;
            return new Vector3(0f, accumulatedY, 0f);
        }

        public void SetGrounded(bool grounded)
        {
            bool wasGrounded = isGrounded;

            // Suppress grounded=true while a jump is in flight (accumulatedY well above platform level).
            // Without this, at 60fps a second Update fires before the first FixedUpdate, resets
            // isGrounded=true, and GetVelocityContribution clamps accumulatedY back to platformY,
            // cancelling the jump before physics can apply it.
            if (grounded && accumulatedY > platformY + 0.1f)
                return;

            isGrounded = grounded;

            if (wasGrounded && !grounded)
            {
                // Walk-off dismount: seed vertical velocity from elevator before gravity integrates
                accumulatedY = lastPlatformY;
            }
        }

        public void ApplyJumpImpulse(float jumpVelocity)
        {
            accumulatedY = jumpVelocity + platformY;
            isGrounded = false;
            lastTargetY = accumulatedY; // prime baseline so next frame sees no false external delta
        }

        public void AddVerticalImpulse(float dv)
        {
            accumulatedY += dv;
        }

        public void SetPlatformY(float y)
        {
            lastPlatformY = platformY;
            platformY = y;
        }
    }
}
