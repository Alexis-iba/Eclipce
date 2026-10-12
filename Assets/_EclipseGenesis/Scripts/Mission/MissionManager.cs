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

        [SerializeField] private bool openZoneOperation;
        [SerializeField] private Terminal powerTerminal;
        [SerializeField] private GameObject defenseWave;
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
                string text = (openZoneOperation ? _openObjectives : _objectives)[ObjectiveIndex];
                return ObjectiveIndex == (openZoneOperation ? 4 : 3) ? $"{text} ({_killed}/{aliens.Length})" : text;
            }
        }

        /// <summary>All objective texts of the active operation, in order (HUD checklist).</summary>
        public IReadOnlyList<string> Objectives => openZoneOperation ? _openObjectives : _objectives;

        /// <summary>World position of objective <paramref name="index"/> for the compass marker; false when it has none (kill/defend objectives).</summary>
        public bool TryGetObjectivePosition(int index, out Vector3 position)
        {
            Component target = openZoneOperation
                ? index switch { 0 => labVolume, 1 => securityDoorVolume, 2 => emergencyTerminal, 3 => powerTerminal, 5 => researchRoomVolume, 6 => extractionVolume, _ => null }
                : index switch { 0 => labVolume, 1 => securityDoorVolume, 2 => researchRoomVolume, 4 => emergencyTerminal, 5 => exitDoor, 6 => extractionVolume, _ => null };
            position = target != null ? target.transform.position : Vector3.zero;
            return target != null;
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
            if (openZoneOperation) { StartOpenZone(); return; }
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
            if (openZoneOperation) { CompleteOpenZone(index); return; }

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

        private readonly List<string> _openObjectives = new List<string>
        {
            "Dirígete a la zona residencial",
            "Ingresa a la base Genesis",
            "Recupera los datos de la terminal",
            "Restablece la energía de seguridad",
            "Repele el contraataque alienígena",
            "Regresa al exterior",
            "Llega al punto de extracción"
        };

        private void StartOpenZone()
        {
            if (labVolume != null) labVolume.Entered += () => Complete(0);
            if (securityDoorVolume != null) securityDoorVolume.Entered += () => Complete(1);
            if (researchRoomVolume != null) researchRoomVolume.Entered += () => Complete(5);
            if (extractionVolume != null) extractionVolume.Entered += () => Complete(6);
            if (emergencyTerminal != null) { emergencyTerminal.SetAvailable(false); emergencyTerminal.OnActivated += _ => Complete(2); }
            if (powerTerminal != null) { powerTerminal.SetAvailable(false); powerTerminal.OnActivated += _ => Complete(3); }
            if (securityDoor != null) securityDoor.SetLocked(false);
            if (exitDoor != null) exitDoor.SetLocked(true);
            if (roomAlarm != null) roomAlarm.SetAlarm(false);
            if (defenseWave != null) defenseWave.SetActive(false);
            foreach (var a in aliens) if (a != null) a.Died += OnAlienDied;
            if (player != null) player.Died += OnPlayerDied;
            ObjectiveChanged?.Invoke(ObjectiveIndex, CurrentObjective);
        }

        private void CompleteOpenZone(int index)
        {
            if (index == 6) { Finish(MissionState.Victory); return; }
            ObjectiveIndex++;
            if (ObjectiveIndex == 2 && emergencyTerminal != null) emergencyTerminal.SetAvailable(true);
            if (ObjectiveIndex == 3 && powerTerminal != null) powerTerminal.SetAvailable(true);
            if (ObjectiveIndex == 4) {
                if (roomAlarm != null) roomAlarm.SetAlarm(true);
                if (defenseWave != null) defenseWave.SetActive(true);
                if (_killed >= aliens.Length) { Complete(4); return; }
            }
            if (ObjectiveIndex == 5) {
                if (exitDoor != null) exitDoor.SetLocked(false);
                if (roomAlarm != null) roomAlarm.SetAlarm(false);
            }
            ObjectiveChanged?.Invoke(ObjectiveIndex, CurrentObjective);
        }
        private void OnAlienDied(AlienHealth alien)
        {
            _killed++;
            if (ObjectiveIndex == (openZoneOperation ? 4 : 3))
            {
                if (_killed >= aliens.Length) Complete(openZoneOperation ? 4 : 3);
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

        public bool IsOpenZone => openZoneOperation;
        public void ResumeOpenZoneCheckpoint()
        {
            if (!openZoneOperation || State != MissionState.InProgress) return;
            ObjectiveIndex = 2;
            if (emergencyTerminal != null) emergencyTerminal.SetAvailable(true);
            ObjectiveChanged?.Invoke(ObjectiveIndex, CurrentObjective);
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
