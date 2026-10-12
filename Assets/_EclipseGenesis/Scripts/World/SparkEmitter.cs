using UnityEngine;

namespace EclipseGenesis.World
{
    /// <summary>Electrical fault: random bursts of a small particle system, a short light flash and a crackle.</summary>
    public sealed class SparkEmitter : MonoBehaviour
    {
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private Light flash;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip crackle;
        [SerializeField] private Vector2 intervalRange = new Vector2(2.5f, 7f);
        [SerializeField] private Vector2Int burstCount = new Vector2Int(10, 26);
        [SerializeField] private float flashIntensity = 900f;
        [SerializeField] private float flashSeconds = 0.08f;

        private float _next;
        private float _flashEnd;

        public int BurstsFired { get; private set; }

        private void Start()
        {
            _next = Time.time + Random.Range(0.5f, intervalRange.y);
            if (flash != null) flash.intensity = 0f;
        }

        private void Update()
        {
            if (Time.time >= _next) Burst();
            if (flash != null) flash.intensity = Time.time < _flashEnd ? flashIntensity * Random.Range(0.6f, 1f) : 0f;
        }

        public void Burst()
        {
            _next = Time.time + Random.Range(intervalRange.x, intervalRange.y);
            _flashEnd = Time.time + flashSeconds;
            if (sparks != null) sparks.Emit(Random.Range(burstCount.x, burstCount.y + 1));
            if (audioSource != null && crackle != null) audioSource.PlayOneShot(crackle, Random.Range(0.6f, 1f));
            BurstsFired++;
        }
    }
}
