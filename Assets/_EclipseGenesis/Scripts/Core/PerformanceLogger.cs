using UnityEngine;

namespace EclipseGenesis.Core
{
    /// <summary>
    /// Logs frame-rate statistics to the player log every few seconds ("[EG-PERF] ..."), so performance can be
    /// measured on a real build instead of guessed.
    /// </summary>
    public sealed class PerformanceLogger : MonoBehaviour
    {
        [SerializeField] private float reportSeconds = 10f;
        [SerializeField] private float warmupSeconds = 3f;

        private float _windowStart;
        private int _frames;
        private float _worst;

        private void Start() => ResetWindow();

        private void Update()
        {
            if (Time.unscaledTime < warmupSeconds) return;

            _frames++;
            _worst = Mathf.Max(_worst, Time.unscaledDeltaTime);
            float elapsed = Time.unscaledTime - _windowStart;
            if (elapsed < reportSeconds) return;

            float avgFps = _frames / elapsed;
            float avgMs = 1000f * elapsed / _frames;
            Debug.Log($"[EG-PERF] avg {avgFps:0.0} fps ({avgMs:0.00} ms) · worst frame {_worst * 1000f:0.0} ms · {Screen.width}x{Screen.height} · quality '{QualitySettings.names[QualitySettings.GetQualityLevel()]}'");
            ResetWindow();
        }

        private void ResetWindow()
        {
            _windowStart = Time.unscaledTime;
            _frames = 0;
            _worst = 0f;
        }
    }
}
