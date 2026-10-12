using UnityEngine;

namespace EclipseGenesis.World
{
    public enum EmergencyLightMode { Pulse, Rotate, Steady }

    /// <summary>
    /// Red emergency lamp. Idle it glows dimly; when the alarm is active it pulses or rotates like a beacon.
    /// Only the Light is animated (no per-frame material changes), so it is cheap.
    /// </summary>
    public sealed class EmergencyLight : MonoBehaviour
    {
        [SerializeField] private Light targetLight;
        [SerializeField] private Transform rotor;
        [SerializeField] private EmergencyLightMode alarmMode = EmergencyLightMode.Pulse;
        [SerializeField] private float alarmIntensity = 1600f;
        [SerializeField] private float idleIntensity = 250f;
        [SerializeField] private float pulseHz = 0.9f;
        [SerializeField] private float rotationDegPerSec = 220f;
        [SerializeField] private bool alarmActive;

        private float _phase;

        public bool AlarmActive
        {
            get => alarmActive;
            set => alarmActive = value;
        }

        private void Awake()
        {
            _phase = Random.value;
        }

        private void Update()
        {
            if (targetLight == null) return;

            if (!alarmActive)
            {
                targetLight.intensity = idleIntensity;
                return;
            }

            switch (alarmMode)
            {
                case EmergencyLightMode.Pulse:
                    _phase += Time.deltaTime * pulseHz;
                    float wave = 0.5f + 0.5f * Mathf.Sin(_phase * Mathf.PI * 2f);
                    targetLight.intensity = Mathf.Lerp(idleIntensity, alarmIntensity, wave * wave);
                    break;
                case EmergencyLightMode.Rotate:
                    targetLight.intensity = alarmIntensity;
                    if (rotor != null) rotor.Rotate(0f, rotationDegPerSec * Time.deltaTime, 0f, Space.Self);
                    break;
                default:
                    targetLight.intensity = alarmIntensity;
                    break;
            }
        }
    }
}
