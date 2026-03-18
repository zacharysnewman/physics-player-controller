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
                accumulatedY = platformY;
            }
            else
            {
                accumulatedY += Physics.gravity.y * gravityScale * deltaTime;
            }
            return new Vector3(0f, accumulatedY, 0f);
        }

        public void SetGrounded(bool grounded)
        {
            bool wasGrounded = isGrounded;
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
