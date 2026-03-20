using UnityEngine;

namespace ZacharysNewman.PPC
{
    [CreateAssetMenu(fileName = "GroundCheckerConfig", menuName = "PPC/Ground Checker Config", order = 4)]
    public class GroundCheckerConfig : ScriptableObject
    {
        [Header("Ground Check Settings")]
        [SerializeField] private float groundCheckDistance = 0.15f; // Rays originate at the bottom of the capsule sphere; only needs to span the ground gap
        [SerializeField] private LayerMask groundLayerMask = -1;
        [SerializeField] [Range(0.1f, 2.0f)] private float groundCheckRadiusMultiplier = 0.9f;

        [Header("Ceiling Check Settings")]
        [SerializeField] private float ceilingCheckDistance = 0.1f; // Rays originate at the top of the capsule sphere; only needs to span the ceiling gap
        [SerializeField] private LayerMask ceilingLayerMask = -1;
        [SerializeField] private float maxSlopeAngle = 45f;

        // Public properties
        public float GroundCheckDistance => groundCheckDistance;
        public LayerMask GroundLayerMask => groundLayerMask;
        public float GroundCheckRadiusMultiplier => groundCheckRadiusMultiplier;
        public float CeilingCheckDistance => ceilingCheckDistance;
        public LayerMask CeilingLayerMask => ceilingLayerMask;
        public float MaxSlopeAngle => maxSlopeAngle;

        // Public methods for runtime modification
        public void SetGroundCheckParameters(float distance, LayerMask layerMask)
        {
            groundCheckDistance = distance;
            groundLayerMask = layerMask;
        }

        public void SetCeilingCheckParameters(float distance, LayerMask layerMask)
        {
            ceilingCheckDistance = distance;
            ceilingLayerMask = layerMask;
        }


    }
}