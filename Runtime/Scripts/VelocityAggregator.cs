using UnityEngine;

namespace ZacharysNewman.PPC
{
    [RequireComponent(typeof(Rigidbody))]
    public class VelocityAggregator : MonoBehaviour
    {
        private Rigidbody rb;
        private IVelocityLayer[] layers;
        private VerticalVelocityLayer verticalLayer;
        private PlayerMovement playerMovement;

        public Vector3 LastTargetVelocity { get; private set; }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            layers = GetComponents<IVelocityLayer>();
            verticalLayer = GetComponent<VerticalVelocityLayer>();
            playerMovement = GetComponent<PlayerMovement>();
        }

        public void Apply(float dt)
        {
            if (layers == null) return;

            bool hasExclusive = false;
            foreach (var layer in layers)
            {
                if (layer.IsActive && layer.IsExclusive) { hasExclusive = true; break; }
            }

            // Feed platform Y to vertical layer before it contributes
            if (verticalLayer != null && playerMovement != null)
            {
                verticalLayer.SetPlatformY(playerMovement.BaseVelocity.y);
            }

            Vector3 targetVelocity = Vector3.zero;
            foreach (var layer in layers)
            {
                if (!layer.IsActive) continue;
                if (hasExclusive && !layer.IsExclusive) continue;
                targetVelocity += layer.GetVelocityContribution(dt);
            }

            LastTargetVelocity = targetVelocity;

            Vector3 delta = targetVelocity - rb.linearVelocity;
            rb.AddForce(delta / dt, ForceMode.Acceleration);
        }
    }
}
