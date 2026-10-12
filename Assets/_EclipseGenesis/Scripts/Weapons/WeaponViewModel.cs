using EclipseGenesis.Player;
using UnityEngine;

namespace EclipseGenesis.Weapons
{
    /// <summary>
    /// Procedural first-person animation for a weapon model (no animation clips exist yet):
    /// hip/ADS blend with the sight on the camera axis, look sway, walk bob, sprint and tactical-sprint poses,
    /// spring recoil kick, reload (magazine out/in with the left hand), equip raise, inspect and wall avoidance.
    /// Lives on the weapon root, a child of the camera's WeaponHolder.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class WeaponViewModel : MonoBehaviour
    {
        [SerializeField] private WeaponModel model;
        [Tooltip("Distance in front of the camera where the sight sits when fully aimed.")]
        [SerializeField] private float adsSightDistance = 0.2f;

        [Header("Sway & bob")]
        [SerializeField] private float swayDegreesPerUnit = 0.04f;
        [SerializeField] private float maxSway = 5f;
        [SerializeField] private float swaySmoothing = 10f;
        [SerializeField] private float bobAmount = 0.012f;

        [Header("Recoil spring")]
        [SerializeField] private float kickBack = 0.03f;
        [SerializeField] private float kickPitch = 3.2f;
        [SerializeField] private float springStiffness = 260f;
        [SerializeField] private float springDamping = 22f;

        private WeaponController _controller;
        private PlayerLook _look;
        private Vector3 _adsPosition;
        private Vector3 _magazineRest;
        private Vector3 _leftHandRest;
        private Vector2 _sway;
        private float _bobPhase;
        private float _kick, _kickVelocity;
        private float _kickRoll;
        private float _sprintW, _tacticalW, _wallW, _reloadW, _inspectW;
        private float _inspectUntil;
        private float _landDip, _landVel;

        public void Bind(WeaponController controller, PlayerLook look)
        {
            _controller = controller;
            _look = look;
            if (_controller.Movement != null) _controller.Movement.Landed += OnLanded;
        }

        private void Awake()
        {
            if (model == null) TryGetComponent(out model);
            if (model != null && model.magazine != null) _magazineRest = model.magazine.localPosition;
            if (model != null && model.leftHand != null) _leftHandRest = model.leftHand.localPosition;
        }

        private void OnEnable()
        {
            // The sight must end on the camera axis: solve for the weapon offset in holder space.
            var holder = transform.parent;
            if (model != null && model.sight != null && holder != null)
            {
                Vector3 sightInWeapon = transform.InverseTransformPoint(model.sight.position);
                Vector3 holderInCamera = holder.localPosition;
                _adsPosition = new Vector3(0f, 0f, adsSightDistance) - holderInCamera - sightInWeapon;
            }
        }

        private void OnDestroy()
        {
            if (_controller != null && _controller.Movement != null) _controller.Movement.Landed -= OnLanded;
        }

        private void OnLanded(float impact) => _landVel -= Mathf.Min(impact * 0.012f, 0.06f) * 10f;

        public void Kick()
        {
            _kickVelocity += 16f;
            _kickRoll = Random.Range(-1.5f, 1.5f);
        }

        private void LateUpdate()
        {
            if (_controller == null) return;
            float dt = Time.deltaTime;
            var movement = _controller.Movement;
            float aim = Mathf.SmoothStep(0f, 1f, _controller.AimAmount);

            // Pose weights.
            bool sprinting = movement != null && movement.IsSprinting && !_controller.IsReloading;
            bool tactical = sprinting && movement.IsTacticalSprinting;
            _sprintW = Damp(_sprintW, sprinting && !tactical ? 1f : 0f, 10f, dt);
            _tacticalW = Damp(_tacticalW, tactical ? 1f : 0f, 10f, dt);
            _reloadW = Damp(_reloadW, _controller.IsReloading ? 1f : 0f, 12f, dt);
            _wallW = Damp(_wallW, WallAhead() ? 1f : 0f, 8f, dt);

            if (_controller.Input != null && _controller.Input.InspectPressed && !_controller.IsReloading && aim < 0.05f)
                _inspectUntil = Time.time + 1.8f;
            bool inspecting = Time.time < _inspectUntil && !_controller.IsReloading && aim < 0.05f && !sprinting;
            _inspectW = Damp(_inspectW, inspecting ? 1f : 0f, 6f, dt);

            // Look sway (lags behind camera rotation).
            Vector2 lookDelta = _controller.Input != null && _controller.Input.LookIsDelta ? _controller.Input.Look : Vector2.zero;
            Vector2 targetSway = Vector2.ClampMagnitude(-lookDelta * swayDegreesPerUnit * 10f, maxSway) * Mathf.Lerp(1f, 0.25f, aim);
            _sway = Vector2.Lerp(_sway, targetSway, 1f - Mathf.Exp(-swaySmoothing * dt));

            // Walk bob.
            float speed = movement != null && movement.IsGrounded && !movement.IsSliding ? movement.HorizontalSpeed : 0f;
            _bobPhase += dt * Mathf.Lerp(6f, 11f, Mathf.Clamp01(speed / 7f)) * (speed > 0.2f ? 1f : 0f);
            float bobW = Mathf.Clamp01(speed / 4.6f) * Mathf.Lerp(1f, 0.12f, aim);
            Vector3 bob = new Vector3(Mathf.Cos(_bobPhase) * bobAmount, Mathf.Abs(Mathf.Sin(_bobPhase)) * bobAmount * 0.8f, 0f) * bobW;

            // Recoil spring.
            float accel = -springStiffness * _kick - springDamping * _kickVelocity;
            _kickVelocity += accel * dt;
            _kick += _kickVelocity * dt;
            float landAccel = -120f * _landDip - 20f * _landVel;
            _landVel += landAccel * dt;
            _landDip += _landVel * dt;

            // Reload envelope: tilt in, magazine out and back, tilt out.
            float rp = _controller.ReloadProgress;
            float magOut = _controller.IsReloading ? Mathf.Clamp01(Mathf.Min(Mathf.InverseLerp(0.12f, 0.3f, rp), Mathf.InverseLerp(0.85f, 0.6f, rp))) : 0f;
            if (model != null && model.magazine != null) model.magazine.localPosition = _magazineRest + new Vector3(0f, -0.22f, -0.05f) * magOut;
            if (model != null && model.leftHand != null) model.leftHand.localPosition = Vector3.Lerp(_leftHandRest, _magazineRest + new Vector3(0f, -0.2f, -0.05f), magOut * 0.85f);

            float equip = 1f - Mathf.SmoothStep(0f, 1f, _controller.EquipProgress);

            Vector3 pos = Vector3.Lerp(Vector3.zero, _adsPosition, aim)
                          + bob
                          + new Vector3(0f, _landDip, -_kick * kickBack * Mathf.Lerp(1f, 0.6f, aim))
                          + new Vector3(0.02f, -0.05f, 0f) * _sprintW
                          + new Vector3(-0.08f, 0.03f, -0.06f) * _tacticalW
                          + new Vector3(-0.02f, -0.035f, 0f) * _reloadW
                          + new Vector3(0f, -0.07f, -0.12f) * _wallW
                          + new Vector3(-0.05f, 0.02f, 0f) * _inspectW
                          + new Vector3(0f, -0.28f, 0f) * equip;

            Vector3 euler = new Vector3(-_sway.y, _sway.x, _sway.x * 0.6f)
                            + new Vector3(-_kick * kickPitch, 0f, _kickRoll * _kick)
                            + new Vector3(8f, -38f, 14f) * _sprintW
                            + new Vector3(-40f, -12f, 32f) * _tacticalW
                            + new Vector3(-6f, 12f, 28f) * _reloadW
                            + new Vector3(28f, -22f, 0f) * _wallW
                            + new Vector3(-8f, -55f, 22f) * _inspectW
                            + new Vector3(35f, 0f, 0f) * equip;

            transform.localPosition = pos;
            transform.localRotation = Quaternion.Euler(euler);
        }

        private bool WallAhead()
        {
            var cam = transform.parent != null ? transform.parent.parent : null;
            if (cam == null) return false;
            // Only static geometry lowers the weapon; creatures in melee range must not.
            return Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, 0.75f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                   && hit.collider.GetComponent<EclipseGenesis.Combat.Hitbox>() == null && hit.rigidbody == null;
        }

        private static float Damp(float a, float b, float lambda, float dt) => Mathf.Lerp(a, b, 1f - Mathf.Exp(-lambda * dt));
    }
}
