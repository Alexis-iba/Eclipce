using System;
using UnityEngine;

namespace EclipseGenesis.Combat
{
    public enum HitZone { Body, Head, Limb }

    /// <summary>Everything a receiver needs to react to a hit.</summary>
    public struct DamageInfo
    {
        public float Amount;
        public Vector3 Point;
        public Vector3 Direction;
        public GameObject Source;
        public HitZone Zone;

        public DamageInfo(float amount, Vector3 point, Vector3 direction, GameObject source, HitZone zone = HitZone.Body)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            Source = source;
            Zone = zone;
        }
    }

    /// <summary>Result reported back to the attacker (hitmarkers, kill feed).</summary>
    public readonly struct DamageResult
    {
        public readonly bool Applied;
        public readonly bool Killed;
        public readonly bool Critical;
        public readonly float Dealt;

        public DamageResult(bool applied, bool killed, bool critical, float dealt)
        {
            Applied = applied; Killed = killed; Critical = critical; Dealt = dealt;
        }

        public static readonly DamageResult None = new DamageResult(false, false, false, 0f);
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        DamageResult ApplyDamage(DamageInfo info);
    }

    /// <summary>Resolves the damage receiver behind any collider, applying hitbox multipliers.</summary>
    public static class DamageRouter
    {
        public static DamageResult Apply(Collider hit, DamageInfo info)
        {
            if (hit == null) return DamageResult.None;
            if (hit.TryGetComponent(out Hitbox box) && box.Owner != null)
            {
                info.Amount *= box.Multiplier;
                info.Zone = box.Zone;
                return box.Owner.ApplyDamage(info);
            }
            var target = hit.GetComponentInParent<IDamageable>();
            return target != null ? target.ApplyDamage(info) : DamageResult.None;
        }
    }

    /// <summary>Gameplay sounds that AI can hear (gunshots, explosions). Static bus: no scene references needed.</summary>
    public static class NoiseEvents
    {
        public static event Action<Vector3, float, GameObject> Emitted;

        public static void Emit(Vector3 position, float radius, GameObject source) => Emitted?.Invoke(position, radius, source);
    }
}
