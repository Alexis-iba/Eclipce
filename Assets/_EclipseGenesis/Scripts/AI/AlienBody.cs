using EclipseGenesis.Combat;
using UnityEngine;

namespace EclipseGenesis.AI
{
    /// <summary>
    /// Procedural animation for the provisional Stalker body (no rig or clips exist yet): four-legged gait driven by
    /// agent speed, breathing, head tracking, claw strike, hit flinch, and a death collapse with fading eye glow.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class AlienBody : MonoBehaviour
    {
        [SerializeField] private AlienBrain brain;
        [SerializeField] private AlienHealth health;
        [SerializeField] private Transform torso;
        [SerializeField] private Transform head;
        [SerializeField] private Transform[] legs = new Transform[4];   // FL, FR, BL, BR hips
        [SerializeField] private Transform clawLeft;
        [SerializeField] private Transform clawRight;
        [SerializeField] private Light eyeLight;
        [SerializeField] private Renderer[] eyeRenderers;
        [SerializeField] private float strideFrequency = 2.2f;

        private Vector3 _torsoRest, _headRestPos;
        private Quaternion _headRest, _torsoRestRot, _clawLRest, _clawRRest;
        private Quaternion[] _legRest;
        private float _phase;
        private float _flinch, _flinchVel;
        private float _death;
        private float _eyeBase;

        private void Awake()
        {
            if (torso != null) { _torsoRest = torso.localPosition; _torsoRestRot = torso.localRotation; }
            if (head != null) { _headRest = head.localRotation; _headRestPos = head.localPosition; }
            if (clawLeft != null) _clawLRest = clawLeft.localRotation;
            if (clawRight != null) _clawRRest = clawRight.localRotation;
            _legRest = new Quaternion[legs.Length];
            for (int i = 0; i < legs.Length; i++) if (legs[i] != null) _legRest[i] = legs[i].localRotation;
            if (eyeLight != null) _eyeBase = eyeLight.intensity;
            if (health != null) health.Damaged += OnDamaged;
        }

        private void OnDamaged(DamageInfo info) => _flinchVel += 9f;

        private void LateUpdate()
        {
            if (brain == null) return;
            float dt = Time.deltaTime;
            bool dead = brain.State == AlienState.Dead;

            if (dead)
            {
                AnimateDeath(dt);
                return;
            }

            float speed = brain.Agent != null && brain.Agent.enabled ? brain.Agent.velocity.magnitude : 0f;
            float run = Mathf.Clamp01(speed / 6f);
            _phase += dt * strideFrequency * Mathf.Lerp(0.6f, 2.4f, run) * (speed > 0.1f ? 1f : 0.15f);

            // Spring flinch.
            float acc = -160f * _flinch - 18f * _flinchVel;
            _flinchVel += acc * dt;
            _flinch += _flinchVel * dt;

            // Legs: diagonal pairs move together (trot).
            float swing = Mathf.Lerp(6f, 38f, run) * (speed > 0.1f ? 1f : 0.1f);
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                float offset = (i == 0 || i == 3) ? 0f : Mathf.PI;
                float a = Mathf.Sin(_phase * Mathf.PI * 2f + offset) * swing;
                legs[i].localRotation = _legRest[i] * Quaternion.Euler(a, 0f, 0f);
            }

            // Torso: bob with gait, crouch lower when chasing, lunge forward on attack.
            float attack = brain.State == AlienState.Attack ? Mathf.Sin(Mathf.Clamp01(brain.AttackPhase * 1.6f) * Mathf.PI) : 0f;
            float crouch = brain.State == AlienState.Chase || brain.State == AlienState.Alert ? 0.08f : 0f;
            float bob = Mathf.Abs(Mathf.Sin(_phase * Mathf.PI * 2f)) * 0.04f * run + Mathf.Sin(Time.time * 2.1f) * 0.01f;
            if (torso != null)
            {
                torso.localPosition = _torsoRest + new Vector3(0f, bob - crouch - attack * 0.08f, attack * 0.35f - _flinch * 0.05f);
                torso.localRotation = _torsoRestRot * Quaternion.Euler(attack * 14f - _flinch * 10f + crouch * 40f, 0f, Mathf.Sin(_phase * Mathf.PI * 2f) * 2f * run);
            }

            // Head looks at the player when aware of it.
            if (head != null)
            {
                Quaternion look = _headRest;
                var target = brain.Perception != null ? brain.Perception.Target : null;
                bool aware = brain.State == AlienState.Alert || brain.State == AlienState.Chase || brain.State == AlienState.Attack;
                if (aware && target != null)
                {
                    Vector3 local = head.parent.InverseTransformPoint(target.TargetPoint);
                    var desired = Quaternion.LookRotation(local.normalized, Vector3.up);
                    look = Quaternion.RotateTowards(_headRest, desired, 50f);
                }
                head.localRotation = Quaternion.Slerp(head.localRotation, look, 1f - Mathf.Exp(-10f * dt));
            }

            // Claws rise during wind-up then slash.
            float raise = brain.State == AlienState.Attack ? Mathf.Clamp01(brain.AttackPhase * 3f) * (1f - Mathf.Clamp01((brain.AttackPhase - 0.4f) * 4f)) : 0f;
            float slash = brain.State == AlienState.Attack ? Mathf.Clamp01((brain.AttackPhase - 0.35f) * 4f) * (1f - Mathf.Clamp01((brain.AttackPhase - 0.7f) * 3f)) : 0f;
            if (clawLeft != null) clawLeft.localRotation = _clawLRest * Quaternion.Euler(-70f * raise + 50f * slash, 0f, 0f);
            if (clawRight != null) clawRight.localRotation = _clawRRest * Quaternion.Euler(-70f * raise + 50f * slash, 0f, 0f);

            if (eyeLight != null)
                eyeLight.intensity = _eyeBase * (brain.State == AlienState.Chase || brain.State == AlienState.Attack ? 1.6f : 1f);
        }

        private void AnimateDeath(float dt)
        {
            _death = Mathf.MoveTowards(_death, 1f, dt / 0.9f);
            float e = Mathf.SmoothStep(0f, 1f, _death);
            if (torso != null)
            {
                torso.localPosition = Vector3.Lerp(torso.localPosition, _torsoRest + new Vector3(0f, -0.38f, 0.1f), e);
                torso.localRotation = Quaternion.Slerp(torso.localRotation, _torsoRestRot * Quaternion.Euler(10f, 0f, 75f), e);
            }
            for (int i = 0; i < legs.Length; i++)
                if (legs[i] != null) legs[i].localRotation = Quaternion.Slerp(legs[i].localRotation, _legRest[i] * Quaternion.Euler(0f, 0f, i % 2 == 0 ? 70f : -70f), e);
            if (head != null) head.localRotation = Quaternion.Slerp(head.localRotation, _headRest * Quaternion.Euler(35f, 0f, 20f), e);
            if (eyeLight != null) eyeLight.intensity = _eyeBase * (1f - e);
            if (eyeRenderers != null && _death >= 1f)
                foreach (var r in eyeRenderers) if (r != null) r.enabled = false;
        }
    }
}
