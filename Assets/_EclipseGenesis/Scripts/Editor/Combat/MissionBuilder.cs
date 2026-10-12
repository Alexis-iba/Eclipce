using System.Linq;
using EclipseGenesis.AI;
using EclipseGenesis.Combat;
using EclipseGenesis.EditorTools.Facility;
using EclipseGenesis.Mission;
using EclipseGenesis.World;
using UnityEditor;
using UnityEngine;

namespace EclipseGenesis.EditorTools.Combat
{
    /// <summary>
    /// Adds OPERACIÓN PRIMER CONTACTO to the freshly built facility: objective volumes, three Stalkers with patrol
    /// routes in the research room (far from the entrance), the emergency terminal and the MissionManager wiring.
    /// Runs after the NavMesh bake so creatures never become part of the walkable mesh.
    /// </summary>
    public static class MissionBuilder
    {
        public static readonly Vector3[] StalkerSpawns =
        {
            new Vector3(-10f, 0f, 90f),
            new Vector3(10f, 0f, 84f),
            new Vector3(1.5f, 0f, 96f),
        };

        public static void Apply(FacilityKit kit, GameObject player)
        {
            var root = new GameObject("Mission_PrimerContacto").transform;

            var securityDoor = Find<AutoDoor>("Door_ToCombatZone");
            var exitDoor = Find<AutoDoor>("Door_Exit");
            var alarm = Find<AlarmSystem>("Alarm_CombatZone");
            var terminalGo = GameObject.Find("Terminal_Emergency");
            if (terminalGo == null) throw new MissingReferenceException("Terminal_Emergency not found in facility");

            // Emergency terminal.
            var terminal = terminalGo.AddComponent<Terminal>();
            var status = terminalGo.transform.Find("StatusLight").GetComponent<Renderer>();
            var green = ProtoKit.Glow("M_Terminal_Done", new Color(0.2f, 1f, 0.45f), 350f);
            var termAudio = kit.Audio(terminalGo.transform, "Audio", new Vector3(0f, 1.2f, 0f), null, 0.8f, 12f, playOnAwake: false);
            ProtoKit.SetRef(terminal, "statusLight", status);
            ProtoKit.SetRef(terminal, "lockedMaterial", kit.M.StatusRed);
            ProtoKit.SetRef(terminal, "readyMaterial", kit.M.StatusBlue);
            ProtoKit.SetRef(terminal, "doneMaterial", green);
            ProtoKit.SetRef(terminal, "audioSource", termAudio);
            ProtoKit.SetRef(terminal, "activateClip", ProceduralAudioClips.DoorDenied());

            // Objective volumes.
            var lab = Volume(root, "Obj1_Lab", new Vector3(0f, 1.5f, 45f), new Vector3(28f, 3f, 8f));
            var door = Volume(root, "Obj2_SecurityDoor", new Vector3(0f, 1.5f, 64f), new Vector3(10f, 3f, 6f));
            var room = Volume(root, "Obj3_ResearchRoom", new Vector3(0f, 2f, 77.5f), new Vector3(34f, 4f, 6f));
            var exit = Volume(root, "Obj7_Extraction", new Vector3(-8f, 1.5f, 111f), new Vector3(3.6f, 3f, 4f));

            // Stalkers with patrol routes inside the research room.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AlienBuilder.PrefabPath);
            var routes = new[]
            {
                new[] { new Vector3(-12f, 0f, 96f), new Vector3(-8f, 0f, 80f), new Vector3(-14f, 0f, 74f) },
                new[] { new Vector3(12f, 0f, 92f), new Vector3(6f, 0f, 80f), new Vector3(14f, 0f, 76f) },
                new[] { new Vector3(-2f, 0f, 92f), new Vector3(6f, 0f, 94f), new Vector3(0f, 0f, 82f) },
            };
            var aliens = new AlienHealth[StalkerSpawns.Length];
            var alienRoot = new GameObject("Aliens").transform;
            alienRoot.SetParent(root, false);
            for (int i = 0; i < StalkerSpawns.Length; i++)
            {
                var a = (GameObject)PrefabUtility.InstantiatePrefab(prefab, alienRoot);
                a.name = $"EG_Alien_Stalker_{i + 1}";
                a.transform.SetPositionAndRotation(StalkerSpawns[i], Quaternion.Euler(0f, 180f + i * 25f, 0f));
                var route = new GameObject($"PatrolRoute_{i + 1}").transform;
                route.SetParent(alienRoot, false);
                var points = routes[i].Select((p, n) =>
                {
                    var pt = new GameObject($"Point_{n + 1}").transform;
                    pt.SetParent(route, false);
                    pt.position = p;
                    return pt;
                }).ToArray();
                ProtoKit.SetRefs(a.GetComponent<AlienBrain>(), "patrolPoints", points);
                aliens[i] = a.GetComponent<AlienHealth>();
            }

            var manager = root.gameObject.AddComponent<MissionManager>();
            ProtoKit.SetRef(manager, "player", player.GetComponent<PlayerHealth>());
            ProtoKit.SetRef(manager, "labVolume", lab);
            ProtoKit.SetRef(manager, "securityDoorVolume", door);
            ProtoKit.SetRef(manager, "researchRoomVolume", room);
            ProtoKit.SetRef(manager, "extractionVolume", exit);
            ProtoKit.SetRef(manager, "securityDoor", securityDoor);
            ProtoKit.SetRef(manager, "exitDoor", exitDoor);
            ProtoKit.SetRef(manager, "emergencyTerminal", terminal);
            ProtoKit.SetRef(manager, "roomAlarm", alarm);
            ProtoKit.SetRefs(manager, "aliens", aliens);
        }

        private static TriggerVolume Volume(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var v = go.AddComponent<TriggerVolume>();
            v.Size = size;
            return v;
        }

        private static T Find<T>(string name) where T : Component
        {
            var go = GameObject.Find(name);
            if (go == null || !go.TryGetComponent(out T c)) throw new MissingReferenceException($"{typeof(T).Name} '{name}' not found");
            return c;
        }
    }
}
