using UnityEngine;

namespace ZacharysNewman.PPC
{
    [CreateAssetMenu(fileName = "GroundCheckerConfig", menuName = "PPC/Ground Checker Config", order = 4)]
    public class GroundCheckerConfig : ScriptableObject
    {
        [Header("Ground Check Settings")]
        [SerializeField] private float groundCheckDistance = 1.15f; // Increased from 0.15f to account for ray origin at capsule center
        [SerializeField] private LayerMask groundLayerMask = -1;

        [Header("Ceiling Check Settings")]
        [SerializeField] private float ceilingCheckDistance = 1.1f; // Increased from 0.1f to account for ray origin at capsule center
        [SerializeField] private LayerMask ceilingLayerMask = -1;
        [SerializeField] private float maxSlopeAngle = 45f;

        // Public properties
        public float GroundCheckDistance => groundCheckDistance;
        public LayerMask GroundLayerMask => groundLayerMask;
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