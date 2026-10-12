using EclipseGenesis.Combat;
using EclipseGenesis.Player;
using UnityEngine;

namespace EclipseGenesis.AI
{
    /// <summary>
    /// Sight (range + view cone + line-of-sight ray from the eyes) and hearing (NoiseEvents) for one alien.
    /// Sampled a few times per second, not every frame.
    /// </summary>
    public sealed class AlienPerception : MonoBehaviour
    {
        [SerializeField] private AlienData data;
        [SerializeField] private Transform eyes;
        [SerializeField] private float sampleInterval = 0.12f;

        private static readonly RaycastHit[] Hits = new RaycastHit[12];
        private float _nextSample;
        private Collider[] _ownColliders;

        public bool CanSeeTarget { get; private set; }
        public float SeenDuration { get; private set; }
        public float LastSeenTime { get; private set; } = float.NegativeInfinity;
        public Vector3 LastKnownPosition { get; private set; }
        public bool HasHeardSomething { get; private set; }
        public Vector3 HeardPosition { get; private set; }
        public PlayerHealth Target => PlayerHealth.Active;
        public Transform Eyes => eyes != null ? eyes : transform;

        public AlienData Data { get => data; set => data = value; }

        private void Awake() => _ownColliders = GetComponentsInChildren<Collider>(true);

        private void OnEnable() => NoiseEvents.Emitted += OnNoise;

        private void OnDisable() => NoiseEvents.Emitted -= OnNoise;

        private void Update()
        {
            if (Time.time < _nextSample) return;
            float elapsed = sampleInterval + (Time.time - _nextSample);
            _nextSample = Time.time + sampleInterval;

            bool seen = Sample();
            SeenDuration = seen ? SeenDuration + elapsed : 0f;
            CanSeeTarget = seen;
            if (seen)
            {
                LastSeenTime = Time.time;
                LastKnownPosition = Target.transform.position;
            }
        }

        private bool Sample()
        {
            var target = Target;
            if (target == null || !target.IsAlive || data == null) return false;

            Vector3 eye = Eyes.position;
            Vector3 aim = target.TargetPoint;
            Vector3 toTarget = aim - eye;
            float distance = toTarget.magnitude;

            float range = data.sightRange;
            if (target.TryGetComponent(out PlayerMovement mv) && (mv.IsCrouching || mv.IsSliding)) range *= data.crouchDetectionFactor;
            if (distance > range) return false;

            if (distance > data.proximityRange)
            {
                Vector3 flat = Vector3.ProjectOnPlane(toTarget, Vector3.up);
                Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (Vector3.Angle(fwd, flat) > data.sightAngle * 0.5f) return false;
            }

            return HasLineOfSight(eye, aim, target.transform);
        }

        /// <summary>True if nothing solid (other than this alien) sits between the two points.</summary>
        public bool HasLineOfSight(Vector3 from, Vector3 to, Transform target)
        {
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            int n = Physics.RaycastNonAlloc(from, dir / dist, Hits, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Transform nearestHit = null;
            for (int i = 0; i < n; i++)
            {
                var c = Hits[i].collider;
                if (IsOwn(c)) continue;
                if (Hits[i].distance < nearest)
                {
                    nearest = Hits[i].distance;
                    nearestHit = c.transform;
                }
            }
            return nearestHit == null || nearestHit.IsChildOf(target);
        }

        private bool IsOwn(Collider c)
        {
            foreach (var own in _ownColliders) if (own == c) return true;
            return false;
        }

        private void OnNoise(Vector3 position, float radius, GameObject source)
        {
            if (data == null) return;
            if ((position - transform.position).sqrMagnitude > radius * radius * data.hearingMultiplier * data.hearingMultiplier) return;
            HasHeardSomething = true;
            HeardPosition = position;
        }

        public void ConsumeHeard() => HasHeardSomething = false;

        /// <summary>Being shot reveals the attacker's position immediately.</summary>
        public void Reveal(Vector3 position)
        {
            LastKnownPosition = position;
            HasHeardSomething = true;
            HeardPosition = position;
        }
    }
}
