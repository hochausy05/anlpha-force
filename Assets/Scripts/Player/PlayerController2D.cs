using UnityEngine;
using UnityEngine.InputSystem;

namespace Looter.Foundation
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class PlayerController2D : MonoBehaviour
    {
        [Header("Di chuyển")]
        [SerializeField, Min(0f)] private float moveSpeed = 7f;
        [SerializeField, Min(0f)] private float jumpVelocity = 13f;
        [SerializeField, Min(0.01f)] private float groundCheckDistance = 0.14f;
        [SerializeField, Min(0.01f)] private float wallCheckDistance = 0.06f;
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Min(0f)] private float coyoteTime = 0.1f;
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.1f;

        private Rigidbody2D body;
        private CapsuleCollider2D capsule;
        private SpriteRenderer spriteRenderer;
        private float horizontalInput;
        private bool jumpRequested;
        private float jumpBufferRemaining;
        private float lastGroundedTime = float.NegativeInfinity;
        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
        private ContactFilter2D groundFilter;
        private float facingDirection = 1f;

        public float FacingDirection => facingDirection;

        public void SetFacingDirection(float direction)
        {
            if (Mathf.Abs(direction) < 0.01f) return;
            facingDirection = Mathf.Sign(direction);
            if (spriteRenderer != null) spriteRenderer.flipX = facingDirection < 0f;
        }

        public void ResetForNewRaid()
        {
            horizontalInput = 0f;
            jumpRequested = false;
            jumpBufferRemaining = 0f;
            lastGroundedTime = float.NegativeInfinity;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            capsule = GetComponent<CapsuleCollider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            groundFilter = new ContactFilter2D();
            int worldItemLayer = LayerMask.NameToLayer("WorldItem");
            LayerMask movementQueryLayers = groundLayers;
            if (worldItemLayer >= 0)
                movementQueryLayers &= ~(1 << worldItemLayer);
            groundFilter.SetLayerMask(movementQueryLayers);
            groundFilter.useTriggers = false;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) { horizontalInput = 0f; return; }
            horizontalInput = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontalInput -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontalInput += 1f;
            if (horizontalInput != 0f)
            {
                facingDirection = Mathf.Sign(horizontalInput);
                if (spriteRenderer != null) spriteRenderer.flipX = facingDirection < 0f;
            }
            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
            {
                jumpRequested = true;
                jumpBufferRemaining = jumpBufferTime;
            }
        }

        private void FixedUpdate()
        {
            bool grounded = IsGrounded();
            if (grounded) lastGroundedTime = Time.time;

            Vector2 velocity = body.linearVelocity;
            velocity.x = IsBlockedByWall(horizontalInput) ? 0f : horizontalInput * moveSpeed;
            bool hasJumpPermission = grounded || Time.time - lastGroundedTime <= coyoteTime;
            bool hasBufferedInput = jumpRequested || jumpBufferRemaining > 0f;
            if (hasBufferedInput && hasJumpPermission)
            {
                velocity.y = jumpVelocity;
                lastGroundedTime = float.NegativeInfinity;
                jumpBufferRemaining = 0f;
            }
            else
            {
                jumpBufferRemaining = Mathf.Max(0f, jumpBufferRemaining - Time.fixedDeltaTime);
            }

            jumpRequested = false;
            body.linearVelocity = velocity;
        }

        private bool IsGrounded()
        {
            if (body.linearVelocity.y > 0.05f) return false;

            int hitCount = capsule.Cast(Vector2.down, groundFilter, groundHits, groundCheckDistance);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = groundHits[i];
                if (hit.collider != null && Vector2.Dot(hit.normal, Vector2.up) >= 0.5f) return true;
            }
            return false;
        }

        private bool IsBlockedByWall(float input)
        {
            if (Mathf.Abs(input) < 0.01f) return false;

            Vector2 direction = input > 0f ? Vector2.right : Vector2.left;
            int hitCount = capsule.Cast(direction, groundFilter, groundHits, wallCheckDistance);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = groundHits[i];
                if (hit.collider != null && Vector2.Dot(hit.normal, -direction) >= 0.7f)
                    return true;
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            CapsuleCollider2D groundCollider = GetComponent<CapsuleCollider2D>();
            if (groundCollider == null) return;

            Bounds bounds = groundCollider.bounds;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(
                new Vector3(bounds.center.x, bounds.min.y - groundCheckDistance * 0.5f, bounds.center.z),
                new Vector3(bounds.size.x, groundCheckDistance, 0f));
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            jumpVelocity = Mathf.Max(0f, jumpVelocity);
            groundCheckDistance = Mathf.Max(0.01f, groundCheckDistance);
            wallCheckDistance = Mathf.Max(0.01f, wallCheckDistance);
            coyoteTime = Mathf.Max(0f, coyoteTime);
            jumpBufferTime = Mathf.Max(0f, jumpBufferTime);
        }
    }
}
