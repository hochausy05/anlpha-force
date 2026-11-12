using UnityEngine;

namespace Looter.Foundation
{
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float smoothTime = 0.12f;
        [SerializeField] private Vector2 horizontalWorldBounds = new Vector2(-24f, 24f);
        [SerializeField] private Vector2 verticalCenterBounds = new Vector2(-2f, 6f);
        [SerializeField] private Vector2 followOffset;
        private Vector3 velocity;
        private Camera sceneCamera;

        public void Configure(Transform followTarget, Vector2 xWorldBounds, Vector2 yCameraCenterBounds,
            Vector2 offset = default)
        {
            target = followTarget;
            horizontalWorldBounds = xWorldBounds;
            verticalCenterBounds = yCameraCenterBounds;
            followOffset = offset;
        }

        private void Awake() => sceneCamera = GetComponent<Camera>();

        private void LateUpdate()
        {
            if (target == null || sceneCamera == null || !sceneCamera.orthographic) return;

            Vector3 destination = GetClampedDestination();

            if ((destination - transform.position).sqrMagnitude < 0.0001f)
            {
                transform.position = destination;
                velocity = Vector3.zero;
                return;
            }

            Vector3 smoothed = Vector3.SmoothDamp(transform.position, destination, ref velocity, smoothTime);
            float halfWidth = sceneCamera.orthographicSize * sceneCamera.aspect;
            float minX = horizontalWorldBounds.x + halfWidth;
            float maxX = horizontalWorldBounds.y - halfWidth;
            if (minX > maxX) minX = maxX = (horizontalWorldBounds.x + horizontalWorldBounds.y) * 0.5f;
            float boundedX = Mathf.Clamp(smoothed.x, minX, maxX);
            float boundedY = Mathf.Clamp(smoothed.y, verticalCenterBounds.x, verticalCenterBounds.y);
            if (!Mathf.Approximately(boundedX, smoothed.x)) velocity.x = 0f;
            if (!Mathf.Approximately(boundedY, smoothed.y)) velocity.y = 0f;
            transform.position = new Vector3(boundedX, boundedY, destination.z);
        }

        public void SnapToTarget()
        {
            if (sceneCamera == null) sceneCamera = GetComponent<Camera>();
            if (target == null || sceneCamera == null || !sceneCamera.orthographic) return;
            transform.position = GetClampedDestination();
            velocity = Vector3.zero;
        }

        private Vector3 GetClampedDestination()
        {

            float halfWidth = sceneCamera.orthographicSize * sceneCamera.aspect;
            float minX = horizontalWorldBounds.x + halfWidth;
            float maxX = horizontalWorldBounds.y - halfWidth;
            if (minX > maxX) minX = maxX = (horizontalWorldBounds.x + horizontalWorldBounds.y) * 0.5f;

            Vector3 followPosition = target.position + (Vector3)followOffset;
            return new Vector3(
                Mathf.Clamp(followPosition.x, minX, maxX),
                Mathf.Clamp(followPosition.y, verticalCenterBounds.x, verticalCenterBounds.y),
                transform.position.z);
        }
    }
}
