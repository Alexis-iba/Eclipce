using System;
using EclipseGenesis.Combat;
using UnityEngine;

namespace EclipseGenesis.AI
{
    /// <summary>Alien hit points. Hitbox children route damage here with their zone multiplier.</summary>
    public sealed class AlienHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private AlienData data;

        public float Health { get; private set; }
        public float MaxHealth => data != null ? data.maxHealth : 100f;
        public bool IsAlive => Health > 0f;

        /// <summary>Raised on every hit that lands (info, remaining health).</summary>
        public event Action<DamageInfo> Damaged;
        public event Action<AlienHealth> Died;

        /// <summary>Global notification for mission/score systems.</summary>
        public static event Action<AlienHealth> AnyDied;

        public AlienData Data { get => data; set { data = value; Health = MaxHealth; } }

        private void Awake() => Health = MaxHealth;

        public DamageResult ApplyDamage(DamageInfo info)
        {
            if (!IsAlive || info.Amount <= 0f) return DamageResult.None;
            float armor = data != null ? data.armor : 0f;
            float dealt = info.Amount * (1f - armor);
            Health = Mathf.Max(0f, Health - dealt);
            Damaged?.Invoke(info);
            bool killed = Health <= 0f;
            if (killed)
            {
                Died?.Invoke(this);
                AnyDied?.Invoke(this);
            }
            return new DamageResult(true, killed, info.Zone == HitZone.Head, dealt);
        }
    }
}
