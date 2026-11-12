using Looter.Foundation;
using UnityEngine;

namespace Looter.Combat
{
    public sealed class DroneVisualFollower : MonoBehaviour
    {
        [SerializeField] private Transform followTarget;
        [SerializeField] private PlayerController2D playerController;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private float smoothTime = 0.18f;
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private float bobFrequency = 1.8f;
        [SerializeField] private Vector2 followOffset = new Vector2(0.8f, 0.6f);
        private Vector3 velocity;

        public void Configure(Transform target, PlayerController2D controller, DroneSystemSettings settings,
            SpriteRenderer renderer)
        {
            followTarget = target;
            playerController = controller;
            spriteRenderer = renderer;
            if (settings == null) return;
            smoothTime = settings.FollowSmoothTime;
            bobAmplitude = settings.FollowBobAmplitude;
            bobFrequency = settings.FollowBobFrequency;
            followOffset = settings.FollowOffset;
        }

        public void SetDisabled(bool disabled)
        {
            if (spriteRenderer != null) spriteRenderer.color = disabled ? new Color(0.48f, 0.48f, 0.48f, 0.85f) : Color.white;
        }

        private void LateUpdate()
        {
            if (followTarget == null) return;
            float facing = playerController != null ? Mathf.Sign(playerController.FacingDirection) : 1f;
            Vector3 desired = followTarget.position + new Vector3(followOffset.x * facing,
                followOffset.y + Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude, 0f);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            if (spriteRenderer != null) spriteRenderer.flipX = facing < 0f;
        }
    }
}
