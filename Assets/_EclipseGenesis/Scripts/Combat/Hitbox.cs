using System;
using UnityEngine;

namespace EclipseGenesis.Combat
{
    /// <summary>Collider region of a creature with its own damage multiplier (head shots, limbs).</summary>
    public sealed class Hitbox : MonoBehaviour
    {
        [SerializeField] private HitZone zone = HitZone.Body;
        [SerializeField] private float multiplier = 1f;
        [SerializeField] private MonoBehaviour owner;

        private IDamageable _owner;

        public HitZone Zone => zone;
        public float Multiplier => multiplier;
        public IDamageable Owner => _owner ??= owner as IDamageable ?? GetComponentInParent<IDamageable>();

        public void Configure(HitZone z, float mult, MonoBehaviour damageable)
        {
            zone = z;
            multiplier = mult;
            owner = damageable;
            _owner = damageable as IDamageable;
        }
    }
}
