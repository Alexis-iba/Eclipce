using System;
using UnityEngine;

namespace EclipseGenesis.Combat
{
    /// <summary>
    /// Player health + regenerating shield. The shield absorbs damage first and recharges after a delay
    /// without being hit; health only comes back through Restore (checkpoints, med kits).
    /// </summary>
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float maxShield = 100f;
        [SerializeField] private float shieldRegenDelay = 4f;
        [SerializeField] private float shieldRegenRate = 35f;

        private float _lastDamageTime = float.NegativeInfinity;

        /// <summary>The active player, for AI targeting. Set while enabled.</summary>
        public static PlayerHealth Active { get; private set; }

        public float Health { get; private set; }
        public float Shield { get; private set; }
        public float MaxHealth => maxHealth;
        public float MaxShield => maxShield;
        public bool IsAlive => Health > 0f;
        public bool Invulnerable { get; set; }

        /// <summary>Raised with the hit and the world-space direction the damage came from.</summary>
        public event Action<DamageInfo> Damaged;
        public event Action Died;
        public event Action Changed;

        /// <summary>Aim point for enemies (chest height).</summary>
        public Vector3 TargetPoint => transform.position + Vector3.up * 1.3f;

        private void Awake()
        {
            Health = maxHealth;
            Shield = maxShield;
        }

        private void OnEnable() => Active = this;

        private void OnDisable()
        {
            if (Active == this) Active = null;
        }

        private void Update()
        {
            if (!IsAlive || Shield >= maxShield || Time.time - _lastDamageTime < shieldRegenDelay) return;
            Shield = Mathf.Min(maxShield, Shield + shieldRegenRate * Time.deltaTime);
            Changed?.Invoke();
        }

        public DamageResult ApplyDamage(DamageInfo info)
        {
            if (!IsAlive || Invulnerable || info.Amount <= 0f) return DamageResult.None;

            _lastDamageTime = Time.time;
            float remaining = info.Amount;
            float absorbed = Mathf.Min(Shield, remaining);
            Shield -= absorbed;
            remaining -= absorbed;
            Health = Mathf.Max(0f, Health - remaining);

            Damaged?.Invoke(info);
            Changed?.Invoke();
            if (Health <= 0f) Died?.Invoke();
            return new DamageResult(true, Health <= 0f, false, info.Amount);
        }

        public void Restore()
        {
            Health = maxHealth;
            Shield = maxShield;
            Changed?.Invoke();
        }
    }
}
