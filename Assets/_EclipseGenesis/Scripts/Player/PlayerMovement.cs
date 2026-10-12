using System;
using UnityEngine;

namespace EclipseGenesis.Player
{
    public enum Stance { Standing, Crouching, Sliding }

    public enum LocomotionState { Idle, Walk, Run, Sprint, TacticalSprint, Crouch, Slide, Airborne }

    /// <summary>
    /// First-person locomotion on a CharacterController: walk/run/sprint/tactical sprint, crouch, slide and jump,
    /// with acceleration curves, coyote time, jump buffering and headroom checks before standing up.
    /// The transform pivot sits at the feet; the capsule grows upwards from there.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private MovementSettings settings;
        [SerializeField] private PlayerInputReader input;

        private const float HeadClearance = 0.12f;
        private static readonly Collider[] OverlapBuffer = new Collider[16];
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[8];

        private CharacterController _cc;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private Vector3 _groundNormal = Vector3.up;
        private bool _wasGrounded;

        private bool _sprintLatched;
        private bool _tactical;
        private float _tacticalRemaining;
        private float _lastSprintPressTime = float.NegativeInfinity;

        private float _lastGroundedTime = float.NegativeInfinity;
        private float _lastJumpPressTime = float.NegativeInfinity;

        private float _slideTimer;
        private float _slideCooldownUntil;
        private Vector3 _slideDirection;

        public MovementSettings Settings
        {
            get => settings;
            set { settings = value; ApplyCapsuleSettings(); }
        }

        public PlayerInputReader Input { get => input; set => input = value; }

        public Stance Stance { get; private set; } = Stance.Standing;
        public LocomotionState State { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsSprinting => _sprintLatched && Stance == Stance.Standing;
        public bool IsTacticalSprinting => IsSprinting && _tactical;
        public bool IsCrouching => Stance == Stance.Crouching;
        public bool IsSliding => Stance == Stance.Sliding;
        public Vector3 Velocity => _horizontalVelocity + Vector3.up * _verticalVelocity;
        public float HorizontalSpeed => _horizontalVelocity.magnitude;
        public float CurrentHeight => _cc != null ? _cc.height : settings.standingHeight;
        public float EyeHeight => CurrentHeight - HeadClearance;
        public float TacticalSprintCharge01 => settings != null ? _tacticalRemaining / settings.tacticalSprintDuration : 0f;

        /// <summary>Set by the weapon system while aiming down sights: blocks sprint and forces walk speed.</summary>
        public bool IsAiming { get; set; }

        /// <summary>Raised on landing with the downward speed at impact (m/s, positive).</summary>
        public event Action<float> Landed;
        public event Action Jumped;
        public event Action SlideStarted;
        public event Action SlideEnded;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (input == null) TryGetComponent(out input);
            if (settings == null)
            {
                Debug.LogWarning($"{name}: no MovementSettings assigned, using defaults.", this);
                settings = ScriptableObject.CreateInstance<MovementSettings>();
            }
            ApplyCapsuleSettings();
            _tacticalRemaining = settings.tacticalSprintDuration;
        }

        private void ApplyCapsuleSettings()
        {
            if (_cc == null || settings == null) return;
            _cc.radius = settings.radius;
            _cc.height = settings.standingHeight;
            _cc.center = new Vector3(0f, settings.standingHeight * 0.5f, 0f);
            _cc.stepOffset = settings.stepOffset;
            _cc.slopeLimit = settings.slopeLimit;
            _cc.minMoveDistance = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || input == null) return;

            Vector2 move = Vector2.ClampMagnitude(input.Move, 1f);
            bool grounded = _cc.isGrounded;
            ProbeGround(grounded);

            if (grounded) _lastGroundedTime = Time.time;
            if (input.JumpPressed) _lastJumpPressTime = Time.time;

            UpdateSprint(move, grounded, dt);
            UpdateCrouchAndSlide(grounded);

            if (Stance == Stance.Sliding) UpdateSlide(move, grounded, dt);
            else UpdateHorizontal(move, grounded, dt);

            UpdateJumpAndGravity(grounded, dt);
            UpdateCapsuleHeight(dt);

            Vector3 intended = _horizontalVelocity;
            CollisionFlags flags = _cc.Move((_horizontalVelocity + Vector3.up * _verticalVelocity) * dt);

            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f) _verticalVelocity = 0f;
            if ((flags & CollisionFlags.Sides) != 0) ClipAgainstObstacles(intended, dt);

            IsGrounded = _cc.isGrounded;
            if (IsGrounded && !_wasGrounded) OnLanded();
            _wasGrounded = IsGrounded;
            State = ResolveState(move);
        }

        // ---------------------------------------------------------------- sprint

        private void UpdateSprint(Vector2 move, bool grounded, float dt)
        {
            // Sprint can only be engaged on the ground; an active sprint carries through a jump.
            if (input.SprintPressed && grounded)
            {
                bool doubleTap = Time.time - _lastSprintPressTime <= settings.tacticalDoubleTapWindow;
                if (_sprintLatched && doubleTap && _tacticalRemaining > 0f) _tactical = true;
                _lastSprintPressTime = Time.time;
                _sprintLatched = true;

                if (Stance == Stance.Crouching && !TryChangeStance(Stance.Standing)) _sprintLatched = false;
            }

            bool forwardEnough = move.y >= settings.sprintForwardThreshold;
            if (!forwardEnough || IsAiming || Stance == Stance.Crouching)
            {
                _sprintLatched = false;
            }

            if (!_sprintLatched) _tactical = false;

            if (_tactical && grounded)
            {
                _tacticalRemaining -= dt;
                if (_tacticalRemaining <= 0f) { _tacticalRemaining = 0f; _tactical = false; }
            }
            else if (!_tactical)
            {
                float rate = settings.tacticalSprintDuration / Mathf.Max(0.01f, settings.tacticalSprintRecharge);
                _tacticalRemaining = Mathf.Min(settings.tacticalSprintDuration, _tacticalRemaining + rate * dt);
            }
        }

        // ---------------------------------------------------------------- crouch & slide

        private void UpdateCrouchAndSlide(bool grounded)
        {
            if (!input.CrouchPressed) return;

            switch (Stance)
            {
                case Stance.Sliding:
                    EndSlide(Stance.Crouching);
                    break;
                case Stance.Standing:
                    bool fastEnough = HorizontalSpeed >= settings.slideMinStartSpeed;
                    if (grounded && IsSprinting && fastEnough && Time.time >= _slideCooldownUntil) StartSlide();
                    else { TryChangeStance(Stance.Crouching); _sprintLatched = false; }
                    break;
                case Stance.Crouching:
                    TryChangeStance(Stance.Standing);
                    break;
            }
        }

        private void StartSlide()
        {
            Stance = Stance.Sliding;
            _sprintLatched = false;
            _tactical = false;
            _slideTimer = 0f;
            _slideDirection = _horizontalVelocity.sqrMagnitude > 0.01f ? _horizontalVelocity.normalized : Flat(transform.forward);
            float speed = Mathf.Max(HorizontalSpeed, settings.slideStartBoost);
            _horizontalVelocity = _slideDirection * speed;
            SlideStarted?.Invoke();
        }

        private void UpdateSlide(Vector2 move, bool grounded, float dt)
        {
            _slideTimer += dt;
            float speed = HorizontalSpeed;

            if (grounded)
            {
                Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, _groundNormal);
                Vector3 downhillFlat = Flat(downhill);
                float alongSlide = Vector3.Dot(downhillFlat, _slideDirection);
                speed -= settings.slideFriction * dt;
                if (alongSlide > 0.05f) speed += settings.slideSlopeAcceleration * downhill.magnitude * alongSlide * dt;
            }

            Vector3 wish = WishDirection(move);
            if (wish.sqrMagnitude > 0.01f)
            {
                _slideDirection = Vector3.RotateTowards(_slideDirection, wish.normalized, settings.slideSteering * dt, 0f);
            }

            speed = Mathf.Max(0f, speed);
            _horizontalVelocity = _slideDirection * speed;

            bool tooSlow = speed <= settings.slideEndSpeed;
            bool tooLong = _slideTimer >= settings.slideMaxDuration && speed <= settings.slideStartBoost;
            if (!grounded && _slideTimer > 0.1f) EndSlide(Stance.Standing);
            else if (tooSlow || tooLong) EndSlide(input.SprintHeld ? Stance.Standing : Stance.Crouching);
        }

        private void EndSlide(Stance next)
        {
            if (Stance != Stance.Sliding) return;
            Stance = Stance.Crouching;
            _slideCooldownUntil = Time.time + settings.slideCooldown;
            if (next == Stance.Standing && TryChangeStance(Stance.Standing) && input.SprintHeld) _sprintLatched = true;
            SlideEnded?.Invoke();
        }

        /// <summary>Changes stance, refusing to grow the capsule when something is overhead.</summary>
        private bool TryChangeStance(Stance target)
        {
            if (target == Stance) return true;
            if (TargetHeight(target) > _cc.height + 0.01f && !HasHeadroom(TargetHeight(target))) return false;
            Stance = target;
            return true;
        }

        private float TargetHeight(Stance stance) => stance switch
        {
            Stance.Crouching => settings.crouchHeight,
            Stance.Sliding => settings.slideHeight,
            _ => settings.standingHeight,
        };

        public bool HasHeadroom(float height)
        {
            float r = _cc.radius * 0.95f;
            Vector3 up = transform.up;
            Vector3 bottom = transform.position + up * (_cc.radius + _cc.skinWidth + 0.02f);
            Vector3 top = transform.position + up * Mathf.Max(height - _cc.radius, _cc.radius + 0.03f);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, r, OverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (OverlapBuffer[i] != _cc && !OverlapBuffer[i].transform.IsChildOf(transform)) return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- horizontal

        private void UpdateHorizontal(Vector2 move, bool grounded, float dt)
        {
            Vector3 wish = WishDirection(move);
            float magnitude = Mathf.Clamp01(move.magnitude);

            float speed;
            if (Stance == Stance.Crouching) speed = settings.crouchSpeed;
            else if (IsSprinting) speed = _tactical ? settings.tacticalSprintSpeed : settings.sprintSpeed;
            else if (IsAiming || magnitude < settings.walkInputThreshold) speed = settings.walkSpeed;
            else speed = settings.runSpeed;

            if (!IsSprinting && magnitude < settings.walkInputThreshold && !IsAiming && Stance != Stance.Crouching)
            {
                magnitude /= Mathf.Max(0.01f, settings.walkInputThreshold);
            }

            // Moving backwards is slightly slower: it reads as heavier and more grounded.
            if (move.y < 0f) speed *= Mathf.Lerp(1f, 0.85f, -move.y);

            Vector3 target = wish.sqrMagnitude > 0.0001f ? wish.normalized * (speed * Mathf.Min(1f, magnitude)) : Vector3.zero;

            if (grounded)
            {
                bool speedingUp = target.sqrMagnitude >= _horizontalVelocity.sqrMagnitude;
                float rate = speedingUp ? settings.groundAcceleration : settings.groundDeceleration;
                _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, target, rate * dt);
            }
            else if (target.sqrMagnitude > 0.0001f)
            {
                // Air control steers without bleeding off momentum gained from sprinting or sliding.
                float keep = Mathf.Max(target.magnitude, _horizontalVelocity.magnitude);
                _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, target.normalized * keep, settings.airAcceleration * dt);
            }
        }

        private Vector3 WishDirection(Vector2 move) => Flat(transform.right) * move.x + Flat(transform.forward) * move.y;

        private void ClipAgainstObstacles(Vector3 intended, float dt)
        {
            // Keep only the velocity the controller actually achieved, so running into a wall
            // does not store speed that is released the moment the wall ends.
            Vector3 achieved = Flat(_cc.velocity);
            if (achieved.sqrMagnitude < intended.sqrMagnitude) _horizontalVelocity = achieved;
            if (Stance == Stance.Sliding && achieved.sqrMagnitude > 0.01f) _slideDirection = achieved.normalized;
        }

        // ---------------------------------------------------------------- vertical

        private void UpdateJumpAndGravity(bool grounded, float dt)
        {
            bool buffered = Time.time - _lastJumpPressTime <= settings.jumpBuffer;
            bool coyote = Time.time - _lastGroundedTime <= settings.coyoteTime;

            if (buffered && coyote && _verticalVelocity <= 0.01f)
            {
                _lastJumpPressTime = float.NegativeInfinity;
                if (Stance == Stance.Crouching)
                {
                    // Jump from crouch stands the player up first, like most tactical shooters.
                    TryChangeStance(Stance.Standing);
                }
                else if (Stance != Stance.Sliding || HasHeadroom(settings.standingHeight))
                {
                    if (Stance == Stance.Sliding) EndSlide(Stance.Standing);
                    _verticalVelocity = Mathf.Sqrt(2f * settings.jumpHeight * -settings.gravity);
                    _lastGroundedTime = float.NegativeInfinity;
                    Jumped?.Invoke();
                    return;
                }
            }

            if (grounded && _verticalVelocity < 0f) _verticalVelocity = settings.groundedStickVelocity;
            else _verticalVelocity = Mathf.Max(settings.maxFallSpeed, _verticalVelocity + settings.gravity * dt);
        }

        private void OnLanded()
        {
            float impact = -Mathf.Min(0f, _cc.velocity.y);
            Landed?.Invoke(impact);
        }

        private void ProbeGround(bool grounded)
        {
            _groundNormal = Vector3.up;
            if (!grounded) return;

            Vector3 origin = transform.position + Vector3.up * (_cc.radius + 0.05f);
            int count = Physics.SphereCastNonAlloc(origin, _cc.radius * 0.9f, Vector3.down, HitBuffer, 0.3f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = HitBuffer[i];
                if (hit.collider == _cc || hit.distance <= 0f || hit.distance >= best) continue;
                best = hit.distance;
                _groundNormal = hit.normal;
            }
        }

        // ---------------------------------------------------------------- capsule

        private void UpdateCapsuleHeight(float dt)
        {
            float target = TargetHeight(Stance);
            float current = _cc.height;
            if (Mathf.Approximately(current, target)) return;

            float next = Mathf.Lerp(current, target, 1f - Mathf.Exp(-settings.heightChangeSpeed * dt));
            if (Mathf.Abs(next - target) < 0.005f) next = target;
            if (next > current && !HasHeadroom(next)) return;

            _cc.height = next;
            _cc.center = new Vector3(0f, next * 0.5f, 0f);
        }

        // ---------------------------------------------------------------- helpers

        private LocomotionState ResolveState(Vector2 move)
        {
            if (Stance == Stance.Sliding) return LocomotionState.Slide;
            if (!IsGrounded) return LocomotionState.Airborne;
            if (Stance == Stance.Crouching) return LocomotionState.Crouch;
            if (HorizontalSpeed < 0.2f) return LocomotionState.Idle;
            if (IsTacticalSprinting) return LocomotionState.TacticalSprint;
            if (IsSprinting) return LocomotionState.Sprint;
            return HorizontalSpeed <= settings.walkSpeed + 0.1f ? LocomotionState.Walk : LocomotionState.Run;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        /// <summary>Stops sprint and tactical sprint (firing, reloading).</summary>
        public void CancelSprint()
        {
            _sprintLatched = false;
            _tactical = false;
        }

        /// <summary>Moves the player instantly (spawn, checkpoint respawn) and clears momentum.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _cc.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _cc.enabled = true;
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _sprintLatched = false;
            _tactical = false;
            Stance = Stance.Standing;
            ApplyCapsuleSettings();
        }
    }
}
