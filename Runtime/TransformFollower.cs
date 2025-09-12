using UnityEngine;

namespace ZacharysNewman.PPC
{
    public class TransformFollower : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private Transform positionTarget;
        [SerializeField] private Transform rotationTarget;
        [SerializeField] private PlayerController playerController;

        [Header("Settings")]
        [SerializeField] private bool followPosition = true;
        [SerializeField] private bool followYRotation = true;
        [SerializeField] private bool useLateUpdate = true;
        [SerializeField] private float yOffset = -1f;
        [SerializeField] private bool useDynamicColliderBottom = false;

        private void Update()
        {
            if (!useLateUpdate)
            {
                Follow();
            }
        }

        private void LateUpdate()
        {
            if (useLateUpdate)
            {
                Follow();
            }
        }

        private void Follow()
        {
            if (followPosition)
            {
                if (useDynamicColliderBottom && playerController != null)
                {
                    transform.position = playerController.ColliderBottomPosition;
                }
                else if (positionTarget != null)
                {
                    transform.position = positionTarget.position + Vector3.up * yOffset;
                }
            }

            if (followYRotation && rotationTarget != null)
            {
                transform.rotation = Quaternion.Euler(0, rotationTarget.eulerAngles.y, 0);
            }
        }
    }
}