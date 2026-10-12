using UnityEngine;

namespace EclipseGenesis.World
{
    /// <summary>Damaged fixture: mostly steady with occasional short dropouts and a subtle buzz in intensity.</summary>
    public sealed class FlickerLight : MonoBehaviour
    {
        [SerializeField] private Light targetLight;
        [SerializeField] private float baseIntensity = 2000f;
        [Tooltip("Average seconds between flicker bursts.")]
        [SerializeField] private float burstInterval = 4f;
        [SerializeField] private float burstDuration = 0.45f;
        [Range(0f, 1f)] [SerializeField] private float minFactor = 0.08f;
        [Range(0f, 0.3f)] [SerializeField] private float buzz = 0.05f;

        private float _nextBurst;
        private float _burstEnd;
        private float _seed;

        private void Awake()
        {
            _seed = Random.value * 100f;
            _nextBurst = Time.time + Random.Range(0.5f, burstInterval);
        }

        private void Update()
        {
            if (targetLight == null) return;
            float t = Time.time;

            if (t >= _nextBurst)
            {
                _burstEnd = t + burstDuration * Random.Range(0.5f, 1.5f);
                _nextBurst = _burstEnd + burstInterval * Random.Range(0.5f, 1.5f);
            }

            float factor = 1f - buzz * Mathf.PerlinNoise(_seed, t * 8f);
            if (t < _burstEnd)
            {
                // Hard on/off steps read as an electrical fault, not a smooth fade.
                bool on = Mathf.PerlinNoise(_seed + 7f, t * 25f) > 0.5f;
                factor = on ? factor : minFactor;
            }
            targetLight.intensity = baseIntensity * factor;
        }
    }
}
