using UnityEngine;

namespace ZacharysNewman.PPC
{
    [CreateAssetMenu(fileName = "PlayerCrouchConfig", menuName = "PPC/Player Crouch Config", order = 3)]
    public class PlayerCrouchConfig : ScriptableObject
    {
        [Header("Crouch Parameters")]
        [SerializeField] private float crouchHeight = 1f;
        [SerializeField] private float crouchSpeed = 2f;
        [SerializeField] private float midAirCrouchBoost = 0f;

        // Public properties
        public float CrouchHeight => crouchHeight;
        public float CrouchSpeed => crouchSpeed;
        public float MidAirCrouchBoost => midAirCrouchBoost;

        // Public methods for runtime modification
        public void SetCrouchParameters(float height, float speed, float boost)
        {
            crouchHeight = height;
            crouchSpeed = speed;
            midAirCrouchBoost = boost;
        }
    }
}