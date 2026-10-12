using System;
using System.Collections.Generic;
using EclipseGenesis.AI;
using EclipseGenesis.Combat;
using EclipseGenesis.Player;
using EclipseGenesis.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EclipseGenesis.Mission
{
    public enum MissionState { InProgress, Victory, Defeat }

    /// <summary>
    /// OPERACIÓN PRIMER CONTACTO. Linear objectives driven by real scene events: trigger volumes, the security door,
    /// alien deaths, the emergency terminal and the exit door. Victory on reaching extraction, defeat on player death.
    /// </summary>
    public sealed class MissionManager : MonoBehaviour
    {
        public const string MissionName = "OPERACIÓN PRIMER CONTACTO";

        [SerializeField] private PlayerHealth player;
        [SerializeField] private TriggerVolume labVolume;
        [SerializeField] private TriggerVolume securityDoorVolume;
        [SerializeField] private TriggerVolume researchRoomVolume;
        [SerializeField] private TriggerVolume extractionVolume;
        [SerializeField] private AutoDoor securityDoor;
        [SerializeField] private AutoDoor exitDoor;
        [SerializeField] private Terminal emergencyTerminal;
        [SerializeField] private AlarmSystem roomAlarm;
        [SerializeField] private AlienHealth[] aliens = Array.Empty<AlienHealth>();

        private readonly List<string> _objectives = new List<string>
        {
            "Explora el laboratorio Genesis",
            "Localiza la puerta de seguridad",
            "Entra en la sala de investigación",
            "Elimina a los Stalker",
            "Activa la terminal de emergencia",
            "Abre la salida",
            "Llega a la zona de extracción",
        };

        private int _killed;
        private float _startTime;

        public static MissionManager Current { get; private set; }

        public MissionState State { get; private set; } = MissionState.InProgress;
        public int ObjectiveIndex { get; private set; }
        public int ObjectiveCount => _objectives.Count;
        public int AliensKilled => _killed;
        public int AliensTotal => aliens.Length;
        public float ElapsedTime => (State == MissionState.InProgress ? Time.time : _endTime) - _startTime;
        private float _endTime;

        public string CurrentObjective
        {
            get
            {
                if (State == MissionState.Victory) return "Misión completada";
                if (State == MissionState.Defeat) return "Misión fallida";
                string text = _objectives[ObjectiveIndex];
                return ObjectiveIndex == 3 ? $"{text} ({_killed}/{aliens.Length})" : text;
            }
        }

        /// <summary>Objective text changed (index, text) — HUD shows a notification.</summary>
        public event Action<int, string> ObjectiveChanged;
        public event Action<MissionState> Ended;

        private void Awake() => Current = this;

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        private void Start()
        {
            _startTime = Time.time;
            if (labVolume != null) labVolume.Entered += () => Complete(0);
            if (securityDoorVolume != null) securityDoorVolume.Entered += () => Complete(1);
            if (researchRoomVolume != null) researchRoomVolume.Entered += () => Complete(2);
            if (extractionVolume != null) extractionVolume.Entered += () => Complete(6);
            if (emergencyTerminal != null)
            {
                emergencyTerminal.SetAvailable(false);
                emergencyTerminal.OnActivated += _ => Complete(4);
            }
            if (exitDoor != null)
            {
                exitDoor.SetLocked(true);
                exitDoor.Opened += _ => Complete(5);
            }
            if (securityDoor != null) securityDoor.SetLocked(true);
            foreach (var a in aliens) if (a != null) a.Died += OnAlienDied;
            if (player != null) player.Died += OnPlayerDied;

            ObjectiveChanged?.Invoke(ObjectiveIndex, CurrentObjective);
        }

        /// <summary>Completes objective <paramref name="index"/> only if it is the current one (keeps the order strict).</summary>
        public void Complete(int index)
        {
            if (State != MissionState.InProgress || index != ObjectiveIndex) return;

            switch (index)
            {
                case 1: // security door located: grant access
                    if (securityDoor != null) securityDoor.SetLocked(false);
                    break;
                case 2: // inside the research room: seal it behind the player
                    if (securityDoor != null) securityDoor.SetLocked(true);
                    if (roomAlarm != null) roomAlarm.SetAlarm(true);
                    break;
                case 4: // terminal activated: open the way out
                    if (exitDoor != null) exitDoor.SetLocked(false);
                    if (roomAlarm != null) roomAlarm.SetAlarm(false);
                    break;
                case 6:
                    Finish(MissionState.Victory);
                    return;
            }

            ObjectiveIndex++;
            if (ObjectiveIndex == 3 && _killed >= aliens.Length) { Complete(3); return; }
            if (ObjectiveIndex == 4 && emergencyTerminal != null) emergencyTerminal.SetAvailable(true);
            ObjectiveChanged?.Invoke(ObjectiveIndex, CurrentObjective);
        }

        private void OnAlienDied(AlienHealth alien)
        {
            _killed++;
            if (ObjectiveIndex == 3)
            {
                if (_killed >= aliens.Length) Complete(3);
                else ObjectiveChanged?.Invoke(ObjectiveIndex, CurrentObjective);
            }
        }

        private void OnPlayerDied() => Finish(MissionState.Defeat);

        private void Finish(MissionState result)
        {
            if (State != MissionState.InProgress) return;
            State = result;
            _endTime = Time.time;
            if (player != null && player.TryGetComponent(out PlayerInputReader input)) input.SetGameplayEnabled(false);
            PlayerLook.SetCursorLocked(false);
            ObjectiveChanged?.Invoke(ObjectiveIndex, CurrentObjective);
            Ended?.Invoke(result);
        }

        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0
            ? SceneManager.GetActiveScene().buildIndex : 0);

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
