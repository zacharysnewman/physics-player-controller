using UnityEngine;

namespace ZacharysNewman.PPC
{
    [CreateAssetMenu(fileName = "GroundCheckerConfig", menuName = "PPC/Ground Checker Config", order = 4)]
    public class GroundCheckerConfig : ScriptableObject
    {
        [Header("Ground Check Settings")]
        [SerializeField] private float groundCheckRadius = 0.2f;
        [SerializeField] private float groundCheckDistance = 0.15f;
        [SerializeField] private LayerMask groundLayerMask = -1;

        [Header("Ceiling Check Settings")]
        [SerializeField] private float ceilingCheckRadius = 0.4f;
        [SerializeField] private float ceilingCheckDistance = 0.1f;
        [SerializeField] private LayerMask ceilingLayerMask = -1;

        [Header("Debug Settings")]
        [SerializeField] private bool debugLogging = false;

        // Public properties
        public float GroundCheckRadius => groundCheckRadius;
        public float GroundCheckDistance => groundCheckDistance;
        public LayerMask GroundLayerMask => groundLayerMask;
        public float CeilingCheckRadius => ceilingCheckRadius;
        public float CeilingCheckDistance => ceilingCheckDistance;
        public LayerMask CeilingLayerMask => ceilingLayerMask;
        public bool DebugLogging => debugLogging;

        // Public methods for runtime modification
        public void SetGroundCheckParameters(float radius, float distance, LayerMask layerMask)
        {
            groundCheckRadius = radius;
            groundCheckDistance = distance;
            groundLayerMask = layerMask;
        }

        public void SetCeilingCheckParameters(float radius, float distance, LayerMask layerMask)
        {
            ceilingCheckRadius = radius;
            ceilingCheckDistance = distance;
            ceilingLayerMask = layerMask;
        }

        public void SetDebugLogging(bool enabled)
        {
            debugLogging = enabled;
        }
    }
}