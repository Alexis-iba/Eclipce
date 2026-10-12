using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EclipseGenesis.Weapons
{
    /// <summary>
    /// Pooled, allocation-free combat effects: tracers, impact sparks, alien blood bursts and ejected casings.
    /// One instance per player; created from data assigned by the builder.
    /// </summary>
    public sealed class WeaponEffects : MonoBehaviour
    {
        [SerializeField] private Material tracerMaterial;
        [SerializeField] private Material sparkMaterial;
        [SerializeField] private Material alienBloodMaterial;
        [SerializeField] private Mesh casingMesh;
        [SerializeField] private Material casingMaterial;
        [SerializeField] private float tracerSeconds = 0.05f;

        private const int TracerCount = 8;
        private const int CasingCount = 24;

        private readonly List<(LineRenderer line, float until)> _tracers = new List<(LineRenderer, float)>();
        private readonly Queue<Rigidbody> _casings = new Queue<Rigidbody>();
        private ParticleSystem _sparks, _blood;
        private Transform _root;
        private int _nextTracer;
        private Collider[] _ownerColliders;

        public void Init(Collider[] ownerColliders) => _ownerColliders = ownerColliders;

        private void Awake()
        {
            var root = new GameObject("WeaponFX").transform;
            root.SetParent(null, false);
            _root = root;

            for (int i = 0; i < TracerCount; i++)
            {
                var go = new GameObject("Tracer");
                go.transform.SetParent(root, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = tracerMaterial;
                lr.widthMultiplier = 0.012f;
                lr.positionCount = 2;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.enabled = false;
                _tracers.Add((lr, 0f));
            }

            _sparks = MakeBurst(root, "ImpactSparks", sparkMaterial, new Vector2(4f, 9f), 0.25f, new Vector2(0.008f, 0.02f), 1.2f);
            _blood = MakeBurst(root, "AlienBlood", alienBloodMaterial, new Vector2(1.5f, 4f), 0.45f, new Vector2(0.03f, 0.07f), 0.8f);

            if (casingMesh != null)
            {
                for (int i = 0; i < CasingCount; i++)
                {
                    var c = new GameObject("Casing");
                    c.layer = 2; // Ignore Raycast: shots and AI line of sight never hit casings
                    c.transform.SetParent(root, false);
                    c.transform.localScale = new Vector3(0.012f, 0.022f, 0.012f);
                    c.AddComponent<MeshFilter>().sharedMesh = casingMesh;
                    var mr = c.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = casingMaterial;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    c.AddComponent<CapsuleCollider>();
                    var rb = c.AddComponent<Rigidbody>();
                    rb.mass = 0.01f;
                    rb.interpolation = RigidbodyInterpolation.None;
                    c.SetActive(false);
                    _casings.Enqueue(rb);
                }
            }
        }

        private static ParticleSystem MakeBurst(Transform parent, string name, Material mat, Vector2 speed, float life, Vector2 size, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.01f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 2f;
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        private void Update()
        {
            for (int i = 0; i < _tracers.Count; i++)
            {
                var (line, until) = _tracers[i];
                if (line.enabled && Time.time >= until) line.enabled = false;
            }
        }

        public void Tracer(Vector3 from, Vector3 to)
        {
            var (line, _) = _tracers[_nextTracer];
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.enabled = true;
            _tracers[_nextTracer] = (line, Time.time + tracerSeconds);
            _nextTracer = (_nextTracer + 1) % _tracers.Count;
        }

        public void Impact(Vector3 point, Vector3 normal, bool organic)
        {
            var ps = organic ? _blood : _sparks;
            if (ps == null) return;
            ps.transform.SetPositionAndRotation(point + normal * 0.01f, Quaternion.LookRotation(normal));
            ps.Emit(organic ? 14 : 10);
        }

        public void EjectCasing(Transform port, Vector3 inheritVelocity)
        {
            if (port == null || _casings.Count == 0) return;
            var rb = _casings.Dequeue();
            rb.gameObject.SetActive(false);
            rb.transform.SetPositionAndRotation(port.position, port.rotation * Quaternion.Euler(0f, 0f, 90f));
            rb.gameObject.SetActive(true);
            rb.linearVelocity = inheritVelocity + port.right * Random.Range(1.6f, 2.4f) + port.up * Random.Range(0.8f, 1.4f);
            rb.angularVelocity = Random.insideUnitSphere * 20f;
            if (_ownerColliders != null)
            {
                var col = rb.GetComponent<Collider>();
                foreach (var oc in _ownerColliders) if (oc != null) Physics.IgnoreCollision(col, oc);
            }
            _casings.Enqueue(rb);
        }
    }
}
