using UnityEngine;

namespace ZacharysNewman.PPC
{
    [CreateAssetMenu(fileName = "PlayerClimbConfig", menuName = "PPC/Player Climb Config", order = 1)]
    public class PlayerClimbConfig : ScriptableObject
    {
        [Header("Climbing Settings")]
        [SerializeField] private float ladderClimbSpeed = 3f;
        [SerializeField] private LayerMask ladderLayerMask = 1 << 0; // Default layer

        // Public properties
        public float LadderClimbSpeed => ladderClimbSpeed;
        public LayerMask LadderLayerMask => ladderLayerMask;

        // Public methods for runtime modification
        public void SetClimbSpeed(float speed)
        {
            ladderClimbSpeed = speed;
        }

        public void SetLadderLayerMask(LayerMask mask)
        {
            ladderLayerMask = mask;
        }
    }
}