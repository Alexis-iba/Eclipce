using System;
using EclipseGenesis.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace EclipseGenesis.AI
{
    public enum AlienState { Idle, Patrol, Alert, Chase, Attack, Search, Dead }

    /// <summary>
    /// State machine for a melee alien: Idle → Patrol → Alert → Chase → Attack → Search (→ Patrol).
    /// Movement goes through a NavMeshAgent (so it cannot cross walls); attacks only land if the target is
    /// still within reach and in line of sight at the moment of impact.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class AlienBrain : MonoBehaviour
    {
        [SerializeField] private AlienData data;
        [SerializeField] private AlienPerception perception;
        [SerializeField] private AlienHealth health;
        [SerializeField] private AudioSource voice;
        [Tooltip("Optional patrol points; without them the alien wanders around its spawn.")]
        [SerializeField] private Transform[] patrolPoints = Array.Empty<Transform>();
        [Tooltip("Optional: start idle and only patrol after this delay.")]
        [SerializeField] private bool startPatrolling = true;

        private NavMeshAgent _agent;
        private Vector3 _home;
        private int _patrolIndex;
        private float _stateTime;
        private float _idleUntil;
        private float _nextRepath;
        private float _attackHitTime, _attackEndTime, _nextAttackTime;
        private bool _attackHitDone;
        private float _searchUntil;
        private float _stuckTimer;

        public AlienState State { get; private set; } = AlienState.Idle;
        public float StateTime => Time.time - _stateTime;
        public int AttacksLanded { get; private set; }
        /// <summary>0..1 progress of the current attack (for animation).</summary>
        public float AttackPhase { get; private set; }
        public NavMeshAgent Agent => _agent;
        public AlienPerception Perception => perception;

        public event Action<AlienState, AlienState> StateChanged;

        public void Configure(AlienData d, Transform[] points)
        {
            data = d;
            patrolPoints = points ?? Array.Empty<Transform>();
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (perception == null) TryGetComponent(out perception);
            if (health == null) TryGetComponent(out health);
            _home = transform.position;
        }

        private void Start()
        {
            ApplyAgentSettings();
            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Died += _ => Enter(AlienState.Dead);
            }
            Enter(startPatrolling ? AlienState.Patrol : AlienState.Idle);
        }

        private void ApplyAgentSettings()
        {
            if (data == null) return;
            _agent.speed = data.patrolSpeed;
            _agent.acceleration = data.acceleration;
            _agent.angularSpeed = data.angularSpeed;
            _agent.stoppingDistance = 0.2f;
            _agent.autoBraking = true;
        }

        private void Update()
        {
            if (State == AlienState.Dead || data == null) return;
            if (!_agent.isOnNavMesh)
            {
                // Recover if spawned slightly off the mesh.
                if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas)) _agent.Warp(hit.position);
                return;
            }

            var target = perception != null ? perception.Target : null;
            bool targetAlive = target != null && target.IsAlive;
            bool sees = perception != null && perception.CanSeeTarget && targetAlive;

            switch (State)
            {
                case AlienState.Idle:
                    if (sees) { Enter(AlienState.Alert); break; }
                    if (HeardSomething()) break;
                    if (Time.time >= _idleUntil) Enter(AlienState.Patrol);
                    break;

                case AlienState.Patrol:
                    if (sees) { Enter(AlienState.Alert); break; }
                    if (HeardSomething()) break;
                    if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.3f) Enter(AlienState.Idle);
                    break;

                case AlienState.Alert:
                    _agent.isStopped = true;
                    if (sees) FaceTowards(perception.LastKnownPosition, 8f);
                    if (sees && perception.SeenDuration >= data.alertToChaseTime) Enter(AlienState.Chase);
                    else if (!sees && StateTime > 1.2f) Enter(AlienState.Search);
                    break;

                case AlienState.Chase:
                    if (!targetAlive) { Enter(AlienState.Search); break; }
                    if (Time.time - perception.LastSeenTime > data.loseTargetTime) { Enter(AlienState.Search); break; }
                    Vector3 goal = sees ? target.transform.position : perception.LastKnownPosition;
                    if (Time.time >= _nextRepath)
                    {
                        _nextRepath = Time.time + 0.2f;
                        _agent.SetDestination(goal);
                    }
                    if (sees && InAttackRange(target) && Time.time >= _nextAttackTime) { Enter(AlienState.Attack); break; }
                    CheckStuck();
                    break;

                case AlienState.Attack:
                    UpdateAttack(target);
                    break;

                case AlienState.Search:
                    if (sees) { Enter(AlienState.Chase); break; }
                    if (HeardSomething()) break;
                    if (!_agent.pathPending && _agent.remainingDistance <= 0.6f) SetDestination(RandomPointNear(perception.LastKnownPosition, 5f));
                    if (Time.time >= _searchUntil) Enter(AlienState.Patrol);
                    break;
            }
        }

        // ------------------------------------------------------------------ transitions

        private void Enter(AlienState next)
        {
            var previous = State;
            State = next;
            _stateTime = Time.time;
            _stuckTimer = 0f;

            switch (next)
            {
                case AlienState.Idle:
                    _agent.isStopped = true;
                    _idleUntil = Time.time + UnityEngine.Random.Range(data.idleTimeRange.x, data.idleTimeRange.y);
                    break;
                case AlienState.Patrol:
                    _agent.isStopped = false;
                    _agent.speed = data.patrolSpeed;
                    SetDestination(NextPatrolPoint());
                    break;
                case AlienState.Alert:
                    _agent.isStopped = true;
                    if (previous != AlienState.Chase) Play(data.screechClip);
                    break;
                case AlienState.Chase:
                    _agent.isStopped = false;
                    _agent.speed = data.chaseSpeed;
                    _nextRepath = 0f;
                    break;
                case AlienState.Attack:
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero;
                    _attackHitTime = Time.time + data.attackWindup;
                    _attackEndTime = _attackHitTime + data.attackRecovery;
                    _attackHitDone = false;
                    Play(data.attackClip);
                    break;
                case AlienState.Search:
                    _agent.isStopped = false;
                    _agent.speed = Mathf.Lerp(data.patrolSpeed, data.chaseSpeed, 0.5f);
                    _searchUntil = Time.time + data.searchDuration;
                    SetDestination(perception.LastKnownPosition);
                    break;
                case AlienState.Dead:
                    if (_agent.isOnNavMesh) _agent.isStopped = true;
                    _agent.enabled = false;
                    foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
                    Play(data.deathClip);
                    break;
            }
            StateChanged?.Invoke(previous, next);
        }

        // ------------------------------------------------------------------ attack

        private bool InAttackRange(Combat.PlayerHealth target)
        {
            Vector3 d = target.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude <= data.attackRange && Mathf.Abs(target.transform.position.y - transform.position.y) < 1.6f;
        }

        private void UpdateAttack(Combat.PlayerHealth target)
        {
            float total = _attackEndTime - (_attackHitTime - data.attackWindup);
            AttackPhase = Mathf.Clamp01((Time.time - (_attackHitTime - data.attackWindup)) / total);
            if (target != null && !_attackHitDone) FaceTowards(target.transform.position, 10f);

            if (!_attackHitDone && Time.time >= _attackHitTime)
            {
                _attackHitDone = true;
                // Validate at impact: the player may have stepped back or behind cover during the wind-up.
                if (target != null && target.IsAlive)
                {
                    Vector3 d = target.transform.position - transform.position;
                    d.y = 0f;
                    bool inReach = d.magnitude <= data.attackRange + data.attackReachTolerance;
                    bool clear = perception.HasLineOfSight(perception.Eyes.position, target.TargetPoint, target.transform);
                    if (inReach && clear)
                    {
                        target.ApplyDamage(new DamageInfo(data.attackDamage, target.TargetPoint, d.normalized, gameObject));
                        AttacksLanded++;
                    }
                }
            }

            if (Time.time >= _attackEndTime)
            {
                AttackPhase = 0f;
                _nextAttackTime = Time.time + data.attackCooldown;
                Enter(AlienState.Chase);
            }
        }

        // ------------------------------------------------------------------ helpers

        private void OnDamaged(DamageInfo info)
        {
            if (State == AlienState.Dead) return;
            Play(data.hurtClip, 0.6f);
            if (info.Source != null) perception.Reveal(info.Source.transform.position);
            if (State == AlienState.Idle || State == AlienState.Patrol || State == AlienState.Search) Enter(AlienState.Chase);
        }

        private bool HeardSomething()
        {
            if (perception == null || !perception.HasHeardSomething) return false;
            perception.ConsumeHeard();
            perception.Reveal(perception.HeardPosition);
            Enter(AlienState.Search);
            SetDestination(perception.HeardPosition);
            return true;
        }

        private void CheckStuck()
        {
            if (_agent.velocity.sqrMagnitude < 0.04f && _agent.remainingDistance > 1f) _stuckTimer += Time.deltaTime;
            else _stuckTimer = 0f;
            if (_stuckTimer > 1.5f)
            {
                _stuckTimer = 0f;
                _agent.ResetPath();
                SetDestination(RandomPointNear(transform.position, 2.5f));
            }
        }

        private Vector3 NextPatrolPoint()
        {
            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                var p = patrolPoints[_patrolIndex % patrolPoints.Length];
                _patrolIndex++;
                if (p != null) return p.position;
            }
            return RandomPointNear(_home, data.patrolRadius);
        }

        private Vector3 RandomPointNear(Vector3 center, float radius)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector2 r = UnityEngine.Random.insideUnitCircle * radius;
                if (NavMesh.SamplePosition(center + new Vector3(r.x, 0f, r.y), out var hit, 2f, NavMesh.AllAreas)) return hit.position;
            }
            return center;
        }

        private void SetDestination(Vector3 p)
        {
            if (_agent.isOnNavMesh) _agent.SetDestination(p);
        }

        private void FaceTowards(Vector3 point, float speed)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-speed * Time.deltaTime));
        }

        private void Play(AudioClip clip, float volume = 1f)
        {
            if (voice != null && clip != null) voice.PlayOneShot(clip, volume);
        }
    }
}
