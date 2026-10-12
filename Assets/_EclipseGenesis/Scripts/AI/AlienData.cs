using UnityEngine;

namespace EclipseGenesis.AI
{
    /// <summary>Balance data of one alien species.</summary>
    [CreateAssetMenu(menuName = "Eclipse Genesis/AI/Alien Data", fileName = "AlienData")]
    public sealed class AlienData : ScriptableObject
    {
        public string speciesName = "Stalker";

        [Header("Health")]
        public float maxHealth = 120f;
        [Range(0f, 0.9f)] public float armor = 0f;

        [Header("Movement")]
        public float patrolSpeed = 2.2f;
        public float chaseSpeed = 6.2f;
        public float acceleration = 22f;
        public float angularSpeed = 540f;

        [Header("Perception")]
        public float sightRange = 24f;
        [Range(10f, 360f)] public float sightAngle = 140f;
        [Tooltip("Within this distance the player is noticed regardless of angle.")]
        public float proximityRange = 3f;
        [Tooltip("Crouching players are detected at this fraction of the sight range.")]
        [Range(0.2f, 1f)] public float crouchDetectionFactor = 0.6f;
        public float hearingMultiplier = 1f;
        [Tooltip("Seconds of continuous sight needed to go from Alert to Chase (Stalkers react to movement faster).")]
        public float alertToChaseTime = 0.5f;
        public float loseTargetTime = 3f;
        public float searchDuration = 7f;

        [Header("Patrol")]
        public float patrolRadius = 9f;
        public Vector2 idleTimeRange = new Vector2(1.5f, 3.5f);

        [Header("Melee attack")]
        public float attackRange = 1.9f;
        public float attackDamage = 18f;
        public float attackWindup = 0.35f;
        public float attackRecovery = 0.55f;
        public float attackCooldown = 0.6f;
        [Tooltip("Extra reach allowed at the moment of impact (lunge).")]
        public float attackReachTolerance = 0.35f;

        [Header("Presentation")]
        public AudioClip screechClip;
        public AudioClip attackClip;
        public AudioClip hurtClip;
        public AudioClip deathClip;
    }
}
