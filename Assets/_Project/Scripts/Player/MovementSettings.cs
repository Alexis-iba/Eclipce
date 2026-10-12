using UnityEngine;

namespace EclipseGenesis.Player
{
    /// <summary>Tunable data for <see cref="PlayerMovement"/>. All distances in metres, speeds in m/s.</summary>
    [CreateAssetMenu(menuName = "Eclipse Genesis/Player/Movement Settings", fileName = "MovementSettings")]
    public sealed class MovementSettings : ScriptableObject
    {
        [Header("Speeds")]
        public float walkSpeed = 2.4f;
        public float runSpeed = 4.6f;
        public float sprintSpeed = 6.6f;
        public float tacticalSprintSpeed = 8.2f;
        public float crouchSpeed = 2.2f;
        [Tooltip("Stick deflection below this walks instead of running.")]
        [Range(0f, 1f)] public float walkInputThreshold = 0.5f;

        [Header("Acceleration")]
        public float groundAcceleration = 32f;
        public float groundDeceleration = 26f;
        public float airAcceleration = 7f;
        [Tooltip("Sprint only engages when forward input exceeds this value.")]
        [Range(0f, 1f)] public float sprintForwardThreshold = 0.6f;

        [Header("Tactical sprint")]
        [Tooltip("Max time between two Sprint presses to trigger tactical sprint.")]
        public float tacticalDoubleTapWindow = 0.3f;
        public float tacticalSprintDuration = 4f;
        public float tacticalSprintRecharge = 6f;

        [Header("Jump & gravity")]
        public float jumpHeight = 1.15f;
        public float gravity = -22f;
        public float groundedStickVelocity = -2f;
        public float coyoteTime = 0.12f;
        public float jumpBuffer = 0.12f;
        public float maxFallSpeed = -40f;

        [Header("Capsule")]
        public float standingHeight = 1.8f;
        public float crouchHeight = 1.15f;
        public float slideHeight = 0.9f;
        public float radius = 0.35f;
        public float heightChangeSpeed = 9f;
        public float stepOffset = 0.35f;
        public float slopeLimit = 45f;

        [Header("Slide")]
        [Tooltip("Minimum horizontal speed needed to start a slide.")]
        public float slideMinStartSpeed = 5.8f;
        public float slideStartBoost = 9.2f;
        public float slideFriction = 7.5f;
        public float slideMaxDuration = 0.95f;
        [Tooltip("Speed below which the slide ends.")]
        public float slideEndSpeed = 3f;
        [Tooltip("Extra acceleration applied along downhill slopes while sliding.")]
        public float slideSlopeAcceleration = 10f;
        public float slideSteering = 1.5f;
        public float slideCooldown = 0.35f;
    }
}
