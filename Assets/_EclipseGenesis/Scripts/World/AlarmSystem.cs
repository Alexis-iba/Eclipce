using UnityEngine;

namespace EclipseGenesis.World
{
    /// <summary>
    /// Groups the emergency lamps and sirens of one area so gameplay (missions, terminals) can raise or clear
    /// an alarm with a single call. The siren is optional and off by default so the scene is not noisy.
    /// </summary>
    public sealed class AlarmSystem : MonoBehaviour
    {
        [SerializeField] private EmergencyLight[] lights;
        [SerializeField] private AudioSource[] sirens;
        [SerializeField] private bool alarmOnStart;
        [SerializeField] private bool sirenWithAlarm;

        public bool IsActive { get; private set; }
        public bool SirenEnabled
        {
            get => sirenWithAlarm;
            set { sirenWithAlarm = value; Apply(); }
        }

        private void Start() => SetAlarm(alarmOnStart);

        public void SetAlarm(bool active)
        {
            IsActive = active;
            Apply();
        }

        private void Apply()
        {
            if (lights != null)
            {
                foreach (var l in lights) if (l != null) l.AlarmActive = IsActive;
            }
            if (sirens == null) return;
            foreach (var s in sirens)
            {
                if (s == null) continue;
                bool play = IsActive && sirenWithAlarm;
                if (play && !s.isPlaying) s.Play();
                else if (!play && s.isPlaying) s.Stop();
            }
        }
    }
}
