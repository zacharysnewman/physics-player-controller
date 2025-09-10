using UnityEngine;

namespace ZacharysNewman.PPC
{
    public class MovingPlatform : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private Transform endPoint;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float rotateSpeed = 90f;

        [Header("Rotation Settings")]
        [SerializeField] private Vector3 rotationAxis = Vector3.up;
        [SerializeField] private float rotationAngle = 180f;

        private Vector3 startPosition;
        private Quaternion startRotation;
        private Quaternion endRotation;
        private float journeyLength;
        private float startTime;

        private void Start()
        {
            if (endPoint == null)
            {
                Debug.LogError("MovingPlatform: End point is not assigned!");
                enabled = false;
                return;
            }

            startPosition = transform.position;
            startRotation = transform.rotation;
            endRotation = Quaternion.AngleAxis(rotationAngle, rotationAxis) * startRotation;

            journeyLength = Vector3.Distance(startPosition, endPoint.position);
            startTime = Time.time;
        }

        private void Update()
        {
            if (endPoint == null) return;

            // Calculate ping pong progress (0 to 1, back and forth)
            float distCovered = (Time.time - startTime) * moveSpeed;
            float fractionOfJourney = Mathf.PingPong(distCovered / journeyLength, 1f);

            // Smoothly interpolate position
            transform.position = Vector3.Lerp(startPosition, endPoint.position, fractionOfJourney);

            // Smoothly interpolate rotation
            transform.rotation = Quaternion.Lerp(startRotation, endRotation, fractionOfJourney);
        }

        private void OnDrawGizmos()
        {
            if (endPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, endPoint.position);
                Gizmos.DrawSphere(endPoint.position, 0.1f);
            }
        }
    }
}