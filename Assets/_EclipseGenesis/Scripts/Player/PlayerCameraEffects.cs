using UnityEngine;

namespace EclipseGenesis.Player
{
    /// <summary>
    /// Procedural camera feel: eye height follows the capsule (crouch/slide), subtle head bob,
    /// landing dip, slide roll and FOV kick for sprint. Everything is damped so nothing snaps.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class PlayerCameraEffects : MonoBehaviour
    {
        [SerializeField] private PlayerMovement movement;
        [Tooltip("Pivot that carries pitch; its local height is driven to the eye height.")]
        [SerializeField] private Transform eyePivot;
        [Tooltip("Child of the eye pivot that receives bob, dip and roll offsets.")]
        [SerializeField] private Transform offsetPivot;
        [SerializeField] private Camera targetCamera;

        [Header("Field of view (vertical degrees)")]
        [SerializeField] private float baseFov = 68f;
        [SerializeField] private float sprintFovBonus = 4f;
        [SerializeField] private float tacticalFovBonus = 8f;
        [SerializeField] private float slideFovBonus = 6f;
        [SerializeField] private float fovDamping = 8f;

        [Header("Eye height")]
        [SerializeField] private float eyeHeightDamping = 14f;

        [Header("Head bob")]
        [SerializeField] private float bobFrequency = 1.9f;
        [SerializeField] private float bobVertical = 0.022f;
        [SerializeField] private float bobHorizontal = 0.012f;
        [SerializeField] private float bobReferenceSpeed = 4.6f;

        [Header("Landing")]
        [SerializeField] private float landingDipPerSpeed = 0.012f;
        [SerializeField] private float maxLandingDip = 0.14f;
        [SerializeField] private float landingRecovery = 9f;

        [Header("Slide")]
        [SerializeField] private float slideRoll = 4f;
        [SerializeField] private float rollDamping = 8f;

        [Header("Aiming")]
        [Tooltip("Head bob and roll scale while aiming down sights, so the sight picture stays steady.")]
        [Range(0f, 1f)] [SerializeField] private float aimMotionScale = 0.15f;

        private float _eyeHeight;
        private float _bobPhase;
        private float _bobWeight;
        private float _dip;
        private float _dipVelocity;
        private float _roll;
        private float _aimBlend;

        /// <summary>FOV multiplier driven by aiming down sights (1 = hip fire).</summary>
        public float AimFovMultiplier { get; set; } = 1f;

        public float BaseFov
        {
            get => baseFov;
            set => baseFov = Mathf.Clamp(value, 50f, 100f);
        }

        public Camera TargetCamera => targetCamera;

        private void Awake()
        {
            if (movement == null) movement = GetComponentInParent<PlayerMovement>();
            _eyeHeight = movement != null ? movement.EyeHeight : 1.68f;
        }

        private void OnEnable()
        {
            if (movement != null) movement.Landed += OnLanded;
        }

        private void OnDisable()
        {
            if (movement != null) movement.Landed -= OnLanded;
        }

        private void LateUpdate()
        {
            if (movement == null) return;
            float dt = Time.deltaTime;

            _eyeHeight = Damp(_eyeHeight, movement.EyeHeight, eyeHeightDamping, dt);
            if (eyePivot != null) eyePivot.localPosition = new Vector3(0f, _eyeHeight, 0f);

            _aimBlend = Damp(_aimBlend, movement.IsAiming ? 1f : 0f, 12f, dt);
            float motionScale = Mathf.Lerp(1f, aimMotionScale, _aimBlend);

            UpdateBob(dt);
            UpdateDip(dt);

            float targetRoll = movement.IsSliding ? slideRoll : 0f;
            _roll = Damp(_roll, targetRoll, rollDamping, dt);

            if (offsetPivot != null)
            {
                float bobX = Mathf.Cos(_bobPhase) * bobHorizontal * _bobWeight * motionScale;
                float bobY = Mathf.Abs(Mathf.Sin(_bobPhase)) * bobVertical * _bobWeight * motionScale;
                offsetPivot.localPosition = new Vector3(bobX, bobY - _dip, 0f);
                offsetPivot.localRotation = Quaternion.Euler(0f, 0f, _roll * motionScale);
            }

            if (targetCamera != null)
            {
                float bonus = movement.IsSliding ? slideFovBonus
                    : movement.IsTacticalSprinting ? tacticalFovBonus
                    : movement.IsSprinting ? sprintFovBonus
                    : 0f;
                float target = (baseFov + bonus) * AimFovMultiplier;
                targetCamera.fieldOfView = Damp(targetCamera.fieldOfView, target, fovDamping, dt);
            }
        }

        private void UpdateBob(float dt)
        {
            bool bobbing = movement.IsGrounded && !movement.IsSliding && movement.HorizontalSpeed > 0.3f;
            float speed01 = Mathf.Clamp(movement.HorizontalSpeed / bobReferenceSpeed, 0f, 1.8f);
            _bobWeight = Damp(_bobWeight, bobbing ? speed01 : 0f, 10f, dt);
            if (bobbing) _bobPhase += dt * bobFrequency * Mathf.PI * Mathf.Max(0.6f, speed01);
        }

        private void UpdateDip(float dt)
        {
            // Critically damped spring back to zero.
            float accel = -landingRecovery * landingRecovery * _dip - 2f * landingRecovery * _dipVelocity;
            _dipVelocity += accel * dt;
            _dip += _dipVelocity * dt;
        }

        private void OnLanded(float impactSpeed)
        {
            if (impactSpeed < 2.5f) return;
            _dipVelocity += Mathf.Min(maxLandingDip, impactSpeed * landingDipPerSpeed) * landingRecovery;
        }

        private static float Damp(float current, float target, float lambda, float dt)
            => Mathf.Lerp(current, target, 1f - Mathf.Exp(-lambda * dt));
    }
}
