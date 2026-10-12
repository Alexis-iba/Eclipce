using System;
using System.Collections.Generic;
using EclipseGenesis.Combat;
using EclipseGenesis.Player;
using UnityEngine;

namespace EclipseGenesis.Weapons
{
    /// <summary>
    /// Player weapon handling: loadout slots (1-3 / wheel), hitscan fire with spread and damage falloff,
    /// magazine + reserve ammo, reload, aim down sights and camera recoil with partial auto-recovery.
    /// Presentation (viewmodel animation, effects, HUD) listens to its state and events.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private PlayerLook look;
        [SerializeField] private PlayerCameraEffects cameraEffects;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private Transform aimOrigin;
        [SerializeField] private Transform weaponHolder;
        [SerializeField] private WeaponEffects effects;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private WeaponData[] loadout = Array.Empty<WeaponData>();

        private sealed class Slot
        {
            public WeaponData Data;
            public WeaponModel Model;
            public WeaponViewModel View;
            public int Magazine;
            public int Reserve;
        }

        private readonly List<Slot> _slots = new List<Slot>();
        private int _current = -1;
        private float _nextShotTime;
        private float _sprintBlockUntil;
        private float _equipUntil;
        private float _reloadEnd;
        private float _reloadDuration;
        private float _shotSpread;
        private float _pendingRecovery;
        private float _flashUntil;
        private bool _wasFireHeld;

        public event Action Fired;
        public event Action AmmoChanged;
        public event Action WeaponChanged;
        public event Action<DamageResult> HitConfirmed;
        public event Action ReloadStarted;

        public WeaponData CurrentData => _current >= 0 ? _slots[_current].Data : null;
        public WeaponModel CurrentModel => _current >= 0 ? _slots[_current].Model : null;
        public int Magazine => _current >= 0 ? _slots[_current].Magazine : 0;
        public int Reserve => _current >= 0 ? _slots[_current].Reserve : 0;
        public bool IsReloading { get; private set; }
        public float ReloadProgress => IsReloading ? 1f - Mathf.Clamp01((_reloadEnd - Time.time) / Mathf.Max(0.01f, _reloadDuration)) : 0f;
        public float AimAmount { get; private set; }
        public bool IsAiming { get; private set; }
        public bool IsEquipping => Time.time < _equipUntil;
        public float EquipProgress => CurrentData == null ? 1f : 1f - Mathf.Clamp01((_equipUntil - Time.time) / Mathf.Max(0.01f, CurrentData.equipTime));
        public int ShotsFired { get; private set; }
        public int ShotsHit { get; private set; }

        /// <summary>Current cone half-angle in degrees (drives the dynamic crosshair).</summary>
        public float CurrentSpread { get; private set; }

        public PlayerMovement Movement => movement;
        public PlayerInputReader Input => input;

        private void Awake()
        {
            if (effects != null && TryGetComponent(out CharacterController cc)) effects.Init(new Collider[] { cc });
            foreach (var data in loadout)
            {
                if (data == null) continue;
                var slot = new Slot { Data = data, Magazine = data.magazineSize, Reserve = data.reserveAmmo };
                if (data.viewModelPrefab != null && weaponHolder != null)
                {
                    var go = Instantiate(data.viewModelPrefab, weaponHolder, false);
                    go.name = data.viewModelPrefab.name;
                    slot.Model = go.GetComponent<WeaponModel>();
                    slot.View = go.GetComponent<WeaponViewModel>();
                    if (slot.View != null) slot.View.Bind(this, look);
                    go.SetActive(false);
                }
                _slots.Add(slot);
            }
            if (_slots.Count > 0) Equip(0);
        }

        private void OnDisable()
        {
            if (movement != null) movement.IsAiming = false;
            if (cameraEffects != null) cameraEffects.AimFovMultiplier = 1f;
            if (look != null) look.ContextMultiplier = 1f;
        }

        public void Equip(int index)
        {
            if (index < 0 || index >= _slots.Count || index == _current) return;
            if (_current >= 0 && _slots[_current].Model != null) _slots[_current].Model.gameObject.SetActive(false);
            CancelReload();
            _current = index;
            var slot = _slots[_current];
            if (slot.Model != null) slot.Model.gameObject.SetActive(true);
            _equipUntil = Time.time + slot.Data.equipTime;
            WeaponChanged?.Invoke();
            AmmoChanged?.Invoke();
        }

        private void Update()
        {
            if (_current < 0 || input == null) return;
            var slot = _slots[_current];
            var data = slot.Data;
            float dt = Time.deltaTime;
            bool alive = health == null || health.IsAlive;

            HandleSwitching();
            UpdateAim(data, dt, alive);

            if (alive && input.ReloadPressed) TryReload();
            if (IsReloading && Time.time >= _reloadEnd) FinishReload();

            bool fireHeld = input.FireHeld;
            bool firePressed = fireHeld && !_wasFireHeld;
            _wasFireHeld = fireHeld;
            bool wantFire = alive && (data.automatic ? fireHeld : firePressed);

            if (wantFire && movement != null && movement.IsSprinting)
            {
                movement.CancelSprint();
                _sprintBlockUntil = Time.time + data.sprintToFireTime;
            }

            if (wantFire && CanFire())
            {
                if (slot.Magazine > 0) Fire(slot);
                else if (firePressed) DryFire(slot);
            }

            UpdateSpread(data, dt);
            UpdateRecoilRecovery(data, dt);
            UpdateMuzzleFlash();
        }

        private void HandleSwitching()
        {
            int direct = input.WeaponSlotPressed;
            if (direct > 0) Equip(direct - 1);
            float wheel = input.SwitchWeapon;
            if (_slots.Count > 1 && Mathf.Abs(wheel) > 0.5f && !IsEquipping)
                Equip((_current + (wheel > 0 ? 1 : -1) + _slots.Count) % _slots.Count);
        }

        private void UpdateAim(WeaponData data, float dt, bool alive)
        {
            IsAiming = alive && input.AimHeld && !IsEquipping;
            AimAmount = Mathf.MoveTowards(AimAmount, IsAiming ? 1f : 0f, dt / Mathf.Max(0.01f, data.adsTime));
            float eased = Mathf.SmoothStep(0f, 1f, AimAmount);
            if (movement != null) movement.IsAiming = IsAiming;
            if (cameraEffects != null) cameraEffects.AimFovMultiplier = Mathf.Lerp(1f, data.adsFovMultiplier, eased);
            if (look != null) look.ContextMultiplier = Mathf.Lerp(1f, data.adsSensitivity, eased);
        }

        private bool CanFire() =>
            !IsReloading && !IsEquipping && Time.time >= _nextShotTime && Time.time >= _sprintBlockUntil &&
            (movement == null || !movement.IsSprinting);

        private void Fire(Slot slot)
        {
            var data = slot.Data;
            slot.Magazine--;
            _nextShotTime = Time.time + data.SecondsPerShot;
            ShotsFired++;

            Vector3 origin = aimOrigin.position;
            Vector3 muzzle = slot.Model != null && slot.Model.muzzle != null ? slot.Model.muzzle.position : origin;
            bool anyHit = false;
            for (int p = 0; p < Mathf.Max(1, data.pellets); p++)
            {
                Vector3 dir = SpreadDirection(aimOrigin.forward, CurrentSpread);
                if (Physics.Raycast(origin, dir, out RaycastHit hit, data.maxRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    float falloff = hit.distance <= data.effectiveRange ? 1f
                        : Mathf.Lerp(1f, data.minDamageFactor, Mathf.InverseLerp(data.effectiveRange, data.maxRange, hit.distance));
                    var info = new DamageInfo(data.damage * falloff, hit.point, dir, gameObject);
                    bool organic = hit.collider.TryGetComponent(out Hitbox _);
                    var result = DamageRouter.Apply(hit.collider, info);
                    if (result.Applied)
                    {
                        anyHit = true;
                        HitConfirmed?.Invoke(result);
                    }
                    effects?.Impact(hit.point, hit.normal, organic);
                    effects?.Tracer(muzzle, hit.point);
                }
                else
                {
                    effects?.Tracer(muzzle, origin + dir * 60f);
                }
            }
            if (anyHit) ShotsHit++;

            _shotSpread = Mathf.Min(data.maxShotSpread, _shotSpread + data.spreadPerShot);

            float kick = data.recoilVertical * Mathf.Lerp(1f, data.adsRecoilMultiplier, AimAmount);
            float side = UnityEngine.Random.Range(-data.recoilHorizontal, data.recoilHorizontal);
            look?.AddRotation(side, -kick);
            _pendingRecovery += kick * data.recoilRecoveryFraction;

            if (slot.Model != null) _flashUntil = Time.time + 0.045f;
            slot.View?.Kick();
            effects?.EjectCasing(slot.Model != null ? slot.Model.shellEject : null, movement != null ? movement.Velocity : Vector3.zero);
            if (audioSource != null && data.fireClip != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
                audioSource.PlayOneShot(data.fireClip, 0.9f);
            }
            NoiseEvents.Emit(transform.position, data.noiseRadius, gameObject);

            Fired?.Invoke();
            AmmoChanged?.Invoke();
            if (slot.Magazine == 0 && slot.Reserve > 0) TryReload();
        }

        private void DryFire(Slot slot)
        {
            _nextShotTime = Time.time + 0.25f;
            if (audioSource != null && slot.Data.dryFireClip != null) audioSource.PlayOneShot(slot.Data.dryFireClip, 0.7f);
            if (slot.Reserve > 0) TryReload();
        }

        public bool TryReload()
        {
            if (_current < 0 || IsReloading || IsEquipping) return false;
            var slot = _slots[_current];
            if (slot.Magazine >= slot.Data.magazineSize || slot.Reserve <= 0) return false;

            IsReloading = true;
            _reloadDuration = slot.Data.reloadTime + (slot.Magazine == 0 ? slot.Data.emptyReloadExtra : 0f);
            _reloadEnd = Time.time + _reloadDuration;
            if (movement != null && movement.IsSprinting) movement.CancelSprint();
            if (audioSource != null && slot.Data.reloadClip != null)
            {
                audioSource.pitch = 1f;
                audioSource.PlayOneShot(slot.Data.reloadClip, 0.8f);
            }
            ReloadStarted?.Invoke();
            return true;
        }

        private void FinishReload()
        {
            var slot = _slots[_current];
            int needed = slot.Data.magazineSize - slot.Magazine;
            int taken = Mathf.Min(needed, slot.Reserve);
            slot.Magazine += taken;
            slot.Reserve -= taken;
            IsReloading = false;
            AmmoChanged?.Invoke();
        }

        private void CancelReload()
        {
            IsReloading = false;
        }

        private void UpdateSpread(WeaponData data, float dt)
        {
            _shotSpread = Mathf.MoveTowards(_shotSpread, 0f, data.spreadRecovery * dt);
            float moving = movement != null ? Mathf.Clamp01(movement.HorizontalSpeed / 4.6f) : 0f;
            bool airborne = movement != null && !movement.IsGrounded;
            float baseSpread = Mathf.Lerp(data.hipSpread, data.adsSpread, AimAmount);
            float move = data.movingSpreadAdd * moving * Mathf.Lerp(1f, 0.3f, AimAmount);
            float stance = movement != null && movement.IsCrouching ? 0.75f : 1f;
            CurrentSpread = (baseSpread + move + _shotSpread + (airborne ? 2f : 0f)) * stance;
        }

        private void UpdateRecoilRecovery(WeaponData data, float dt)
        {
            if (_pendingRecovery <= 0f || look == null) return;
            if (Time.time < _nextShotTime && input.FireHeld) return; // recover between bursts, not mid-spray
            float step = Mathf.Min(_pendingRecovery, data.recoilRecoverySpeed * dt);
            look.AddRotation(0f, step);
            _pendingRecovery -= step;
        }

        private void UpdateMuzzleFlash()
        {
            var model = CurrentModel;
            if (model == null) return;
            bool on = Time.time < _flashUntil;
            if (model.muzzleFlash != null && model.muzzleFlash.activeSelf != on)
            {
                model.muzzleFlash.SetActive(on);
                if (on) model.muzzleFlash.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            }
            if (model.muzzleLight != null) model.muzzleLight.enabled = on;
        }

        private static Vector3 SpreadDirection(Vector3 forward, float degrees)
        {
            if (degrees <= 0.001f) return forward;
            Vector2 r = UnityEngine.Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
            var rot = Quaternion.LookRotation(forward);
            return (forward + rot * new Vector3(r.x, r.y, 0f)).normalized;
        }

        /// <summary>Adds ammo to the reserve of the current weapon (pickups, supply points).</summary>
        public void AddReserve(int rounds)
        {
            if (_current < 0) return;
            _slots[_current].Reserve += rounds;
            AmmoChanged?.Invoke();
        }
    }
}
