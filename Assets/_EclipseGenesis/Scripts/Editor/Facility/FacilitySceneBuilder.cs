using System.Collections.Generic;
using System.IO;
using EclipseGenesis.Core;
using EclipseGenesis.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace EclipseGenesis.EditorTools.Facility
{
    /// <summary>
    /// Builds Genesis_Research_Facility: six connected interior zones built from provisional geometry with
    /// procedural PBR materials, HDRP lighting/post-processing, ambience systems and a baked NavMesh.
    /// Command line: Unity -batchmode -executeMethod EclipseGenesis.EditorTools.Facility.FacilitySceneBuilder.BuildFromCommandLine
    /// </summary>
    public static class FacilitySceneBuilder
    {
        public const string ScenePath = "Assets/_EclipseGenesis/Scenes/Genesis_Research_Facility.unity";
        public const string BackupFolder = "Assets/_EclipseGenesis/Scenes/Backups";
        public const string VolumeProfilePath = "Assets/_EclipseGenesis/Settings/Facility_VolumeProfile.asset";
        public const string NavMeshPath = "Assets/_EclipseGenesis/Scenes/Genesis_Research_Facility_NavMesh.asset";
        public const string SmokeMaskPath = "Assets/_EclipseGenesis/Art/Generated/Textures/SmokeNoise3D.asset";

        public static readonly Vector3 SpawnPosition = new Vector3(0f, 0.05f, 3f);

        // Zone footprints (metres). Shared walls meet on the boundaries; doors sit on them.
        public static readonly RoomRect AccessCorridor = new RoomRect(-2f, 2f, 0f, 16f, 3.2f);
        public static readonly RoomRect Armory = new RoomRect(-9f, 9f, 16f, 28f, 4.2f);
        public static readonly RoomRect MilitaryCorridor = new RoomRect(-2f, 2f, 28f, 40f, 3.4f);
        public static readonly RoomRect CentralLab = new RoomRect(-15f, 15f, 40f, 68f, 7f);
        public static readonly RoomRect ExperimentRoom = new RoomRect(-33f, -15f, 44f, 64f, 5f);
        public static readonly RoomRect CombatZone = new RoomRect(-18f, 18f, 68f, 104f, 8f);

        [MenuItem("Eclipse Genesis/Build/Genesis Research Facility")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void BuildFromCommandLine()
        {
            Build();
            EditorApplication.Exit(0);
        }

        public static void Build()
        {
            BackupExistingScenes();
            EnableScreenSpaceReflections();

            var materials = new FacilityMaterials();
            var meshes = new MeshKit();
            var smoke = ProceduralTextures.SmokeNoise3D(SmokeMaskPath);
            var kit = new FacilityKit(materials, meshes, smoke);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var world = new GameObject("Facility").transform;

            BuildAccessCorridor(kit, world);
            BuildArmory(kit, world);
            BuildMilitaryCorridor(kit, world);
            BuildCentralLab(kit, world);
            BuildExperimentRoom(kit, world);
            BuildCombatZone(kit, world);
            BuildGlobalAmbience(kit, world);

            MarkStatic(world);
            BuildVolume();
            BuildNavigation(world);
            BuildQaMarkers();
            SpawnPlayer();
            new GameObject("PerformanceLogger").AddComponent<PerformanceLogger>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildSceneRegistry.Sync();
            AssetDatabase.SaveAssets();
            Debug.Log($"[EclipseGenesis] Built {ScenePath}: {kit.AllLights.Count} lights ({kit.ShadowedLights.Count} with shadows), {meshes.Count} meshes");
        }

        // ------------------------------------------------------------------ zones

        private static void BuildAccessCorridor(FacilityKit k, Transform world)
        {
            var r = AccessCorridor;
            var room = k.Room(world, "01_AccessCorridor", r, k.M.FloorConcrete, k.M.WallUpper, k.M.WallLower, k.M.Ceiling,
                new Opening(Side.South, 0f), new Opening(Side.North, 0f));
            var props = FacilityKit.Group(room, "Props");
            var lights = FacilityKit.Group(room, "Lighting");

            k.Door(room, "Door_BlastSealed", new Vector3(0f, 0f, r.Z0 - 0.15f), 0f, locked: true, depth: 0.3f);
            k.Door(room, "Door_ToArmory", new Vector3(0f, 0f, r.Z1), 0f, locked: false);

            foreach (float z in new[] { 2.5f, 6.5f, 10.5f, 14f })
                k.CeilingLight(lights, new Vector3(0f, 0f, z), r.Height, 1600f, shadows: z == 6.5f || z == 14f);
            k.WallStrip(props, r, Side.East, 2.75f, k.M.StripBlue);
            k.WallStrip(props, r, Side.West, 2.75f, k.M.StripBlue);
            var sealedAlarm = k.Beacon(lights, new Vector3(0f, r.Height - 0.05f, 1.2f), rotate: false, Vector3.up);
            k.Alarm(room, "Alarm_SealedDoor", new[] { sealedAlarm }, new Vector3(0f, 2.5f, 1f), active: true);

            k.Pipe(props, new Vector3(1.72f, 2.95f, r.Z0 - 0.3f), new Vector3(1.72f, 2.95f, r.Z1 + 0.3f), 0.09f, k.M.Pipe);
            k.Pipe(props, new Vector3(1.5f, 3.02f, r.Z0 - 0.3f), new Vector3(1.5f, 3.02f, r.Z1 + 0.3f), 0.06f, k.M.PipeRed);
            k.Box(props, "CableTray", new Vector3(-1.65f, 3.0f, 8f), new Vector3(0.35f, 0.06f, 16f), k.M.SteelTrim, collider: false);
            k.Box(props, "VentGrille", new Vector3(1.97f, 0.45f, 5f), new Vector3(0.04f, 0.5f, 0.9f), k.M.Grating, collider: false);

            k.WallScreen(props, new Vector3(-1.94f, 1.65f, 8f), 90f, new Vector2(1.0f, 0.6f), alert: false);
            k.Box(props, "Keypad", new Vector3(-1.95f, 1.2f, 8.75f), new Vector3(0.05f, 0.22f, 0.16f), k.M.Graphite_, collider: false);

            Grime(k, props, r, 4, 0.8f);
            k.ReflectionProbe(room, r);
            k.Audio(room, "Amb_Vent", new Vector3(1.8f, 0.5f, 5f), ProceduralAudioClips.VentAir(), 0.25f, 10f);
        }

        private static void BuildArmory(FacilityKit k, Transform world)
        {
            var r = Armory;
            var room = k.Room(world, "02_Armory", r, k.M.FloorPlate, k.M.WallUpper, k.M.WallLower, k.M.Ceiling,
                new Opening(Side.South, 0f), new Opening(Side.North, 0f));
            var props = FacilityKit.Group(room, "Props");
            var lights = FacilityKit.Group(room, "Lighting");

            k.Door(room, "Door_ToMilitaryCorridor", new Vector3(0f, 0f, r.Z1), 0f, locked: false);

            foreach (float x in new[] { -5f, 0f, 5f })
            foreach (float z in new[] { 19.5f, 24.5f })
                k.CeilingLight(lights, new Vector3(x, 0f, z), r.Height, 2000f, shadows: x == 0f);

            // Empty weapon racks (no weapons exist yet; racks are scenery only).
            foreach (float z in new[] { 18.6f, 22f, 25.4f })
            {
                var rack = FacilityKit.Group(props, "WeaponRack_Empty");
                rack.localPosition = new Vector3(r.X0 + 0.35f, 0f, z);
                k.Box(rack, "Backboard", new Vector3(0f, 1.35f, 0f), new Vector3(0.08f, 1.9f, 2.6f), k.M.Graphite_);
                foreach (float y in new[] { 0.7f, 1.35f, 2.0f })
                    k.Box(rack, "Rail", new Vector3(0.12f, y, 0f), new Vector3(0.12f, 0.05f, 2.5f), k.M.SteelTrim, collider: false);
                k.Box(rack, "Sign", new Vector3(0.05f, 2.45f, 0f), new Vector3(0.02f, 0.18f, 1.6f), k.M.StripRed, collider: false);
            }
            k.WallScreen(props, new Vector3(r.X0 + 0.06f, 3.25f, 22f), 90f, new Vector2(2.6f, 0.75f), alert: false);

            // Lockers along the east wall.
            for (float z = r.Z0 + 0.8f; z < r.Z1 - 0.6f; z += 0.66f)
            {
                var locker = FacilityKit.Group(props, "Locker");
                locker.localPosition = new Vector3(r.X1 - 0.3f, 0f, z);
                k.Box(locker, "Body", new Vector3(0f, 1.0f, 0f), new Vector3(0.55f, 2.0f, 0.62f), k.M.Graphite_);
                k.Box(locker, "Handle", new Vector3(-0.285f, 1.05f, 0.18f), new Vector3(0.03f, 0.18f, 0.03f), k.M.SteelTrim, collider: false);
                k.Box(locker, "Vents", new Vector3(-0.28f, 1.75f, 0f), new Vector3(0.01f, 0.2f, 0.4f), k.M.Grating, collider: false);
            }

            foreach (float x in new[] { -3.2f, 3.2f })
            {
                var bench = FacilityKit.Group(props, "Workbench");
                bench.localPosition = new Vector3(x, 0f, 22f);
                k.Box(bench, "Top", new Vector3(0f, 0.95f, 0f), new Vector3(2.6f, 0.06f, 1.0f), k.M.SteelTrim);
                k.Box(bench, "Base", new Vector3(0f, 0.46f, 0f), new Vector3(2.4f, 0.9f, 0.85f), k.M.Graphite_);
                for (int i = 0; i < 3; i++)
                    k.Box(bench, "AmmoCase", new Vector3(-0.8f + i * 0.75f, 1.12f, k.Rand(-0.2f, 0.2f)), new Vector3(0.55f, 0.28f, 0.34f), k.M.Crate, yaw: k.Rand(-12f, 12f));
            }
            k.Terminal(props, new Vector3(-6.2f, 0f, r.Z0 + 1.2f), 180f, alert: false);

            // Storage cage in the north-east corner.
            var cage = FacilityKit.Group(props, "StorageCage");
            k.Box(cage, "Fence_South", new Vector3(6.75f, 1.5f, 24.2f), new Vector3(4.5f, 3f, 0.04f), k.M.Grating);
            k.Box(cage, "Fence_West_A", new Vector3(4.5f, 1.5f, 24.85f), new Vector3(0.04f, 3f, 1.3f), k.M.Grating);
            k.Box(cage, "Fence_West_B", new Vector3(4.5f, 1.5f, 27.4f), new Vector3(0.04f, 3f, 1.2f), k.M.Grating);
            foreach (var p in new[] { new Vector3(4.5f, 1.5f, 24.2f), new Vector3(9f, 1.5f, 24.2f), new Vector3(4.5f, 1.5f, 25.5f), new Vector3(4.5f, 1.5f, 26.8f) })
                k.Box(cage, "Post", p, new Vector3(0.08f, 3f, 0.08f), k.M.SteelTrim);
            k.Crate(cage, new Vector3(7f, 0f, 26.2f), 1.0f, 8f);
            k.Crate(cage, new Vector3(7.1f, 1.0f, 26.2f), 0.8f, -6f);
            k.Crate(cage, new Vector3(8.2f, 0f, 25.1f), 1.0f, 0f);

            Grime(k, props, r, 6, 1.2f);
            k.ReflectionProbe(room, r);
            k.Audio(room, "Amb_Hum", new Vector3(0f, 3f, 22f), ProceduralAudioClips.Hum(), 0.18f, 14f);
        }

        private static void BuildMilitaryCorridor(FacilityKit k, Transform world)
        {
            var r = MilitaryCorridor;
            var room = k.Room(world, "03_MilitaryCorridor", r, k.M.FloorDark, k.M.WallUpper, k.M.WallLower, k.M.Ceiling,
                new Opening(Side.South, 0f), new Opening(Side.North, 0f));
            var props = FacilityKit.Group(room, "Props");
            var lights = FacilityKit.Group(room, "Lighting");

            k.Door(room, "Door_ToLab", new Vector3(0f, 0f, r.Z1), 0f, locked: false);
            k.CeilingLight(lights, new Vector3(-0.6f, 0f, 30.5f), r.Height, 1400f, shadows: true);
            k.CeilingLight(lights, new Vector3(-0.6f, 0f, 34f), r.Height, 1400f, shadows: false, flicker: true);
            k.CeilingLight(lights, new Vector3(-0.6f, 0f, 37.5f), r.Height, 1400f, shadows: true);
            var beacon = k.Beacon(lights, new Vector3(0.9f, r.Height - 0.05f, 39.2f), rotate: true, Vector3.up);
            k.Alarm(room, "Alarm_Corridor", new[] { beacon }, new Vector3(0f, 2.5f, 38f), active: true);

            // Walkway plate, overhead duct and pipe runs on both walls.
            k.Box(props, "Walkway", new Vector3(0f, 0.01f, 34f), new Vector3(1.4f, 0.02f, 12f), k.M.FloorPlate, collider: false);
            k.Box(props, "Duct", new Vector3(1.05f, 3.05f, 34f), new Vector3(0.9f, 0.5f, 12.6f), k.M.Pipe);
            foreach (float z in new[] { 30f, 34f, 38f })
                k.Box(props, "DuctBand", new Vector3(1.05f, 3.05f, z), new Vector3(0.94f, 0.54f, 0.08f), k.M.SteelTrim, collider: false);
            k.Pipe(props, new Vector3(-1.8f, 2.3f, r.Z0 - 0.3f), new Vector3(-1.8f, 2.3f, r.Z1 + 0.3f), 0.08f, k.M.Pipe);
            k.Pipe(props, new Vector3(-1.8f, 2.6f, r.Z0 - 0.3f), new Vector3(-1.8f, 2.6f, r.Z1 + 0.3f), 0.06f, k.M.PipeRed);
            k.Pipe(props, new Vector3(1.82f, 1.9f, r.Z0 - 0.3f), new Vector3(1.82f, 1.9f, r.Z1 + 0.3f), 0.07f, k.M.Pipe);
            foreach (float z in new[] { 31f, 37f })
                k.Box(props, "VentGrille", new Vector3(-1.97f, 0.5f, z), new Vector3(0.04f, 0.6f, 1.0f), k.M.Grating, collider: false);

            // Damaged wall panel with an electrical fault.
            var panel = k.Box(props, "DamagedPanel", new Vector3(1.9f, 1.6f, 33.5f), new Vector3(0.05f, 1.1f, 0.8f), k.M.WallUpper, collider: false);
            panel.transform.localRotation = Quaternion.Euler(0f, 0f, 9f);
            k.Sparks(props, new Vector3(1.85f, 2.05f, 33.5f), Vector3.left);
            k.Smoke(props, new Vector3(-1.2f, 0.8f, 37f), new Vector3(1.6f, 1.6f, 2.2f), 3.5f, new Color(0.75f, 0.78f, 0.8f));

            Grime(k, props, r, 5, 0.9f);
            k.ReflectionProbe(room, r);
            k.Audio(room, "Amb_Vent", new Vector3(-1.8f, 0.5f, 37f), ProceduralAudioClips.VentAir(), 0.3f, 9f);
        }

        private static void BuildCentralLab(FacilityKit k, Transform world)
        {
            var r = CentralLab;
            var room = k.Room(world, "04_CentralLab", r, k.M.FloorPolished, k.M.WallUpper, k.M.WallLower, k.M.Ceiling,
                new Opening(Side.South, 0f), new Opening(Side.West, 54f), new Opening(Side.North, 0f));
            var props = FacilityKit.Group(room, "Props");
            var lights = FacilityKit.Group(room, "Lighting");

            k.Door(room, "Door_ToExperiments", new Vector3(r.X0, 0f, 54f), 90f, locked: false);
            k.Door(room, "Door_ToCombatZone", new Vector3(0f, 0f, r.Z1), 0f, locked: false);

            foreach (float x in new[] { -10f, 0f, 10f })
            foreach (float z in new[] { 44f, 50f, 58f, 64f })
            {
                bool shadow = x != 0f && (z == 50f || z == 58f);
                k.CeilingLight(lights, new Vector3(x, 0f, z), r.Height, 3200f, shadow, panelLength: 2f);
            }

            // Pillars.
            foreach (float x in new[] { -9f, 9f })
            foreach (float z in new[] { 47f, 61f })
            {
                k.Box(props, "Pillar", new Vector3(x, r.Height * 0.5f, z), new Vector3(0.8f, r.Height, 0.8f), k.M.WallLower);
                k.Box(props, "PillarGuard", new Vector3(x, 0.5f, z), new Vector3(0.9f, 1.0f, 0.9f), k.M.Hazard, collider: false);
            }

            // Central containment chamber.
            var core = FacilityKit.Group(props, "ContainmentChamber");
            core.localPosition = new Vector3(0f, 0f, 54f);
            k.Cylinder(core, "Base", new Vector3(0f, 0.125f, 0f), 3.2f, 0.25f, k.M.Graphite_, Quaternion.identity);
            k.Cylinder(core, "GlowRing", new Vector3(0f, 0.135f, 0f), 2.95f, 0.27f, k.M.StripBlue, Quaternion.identity, collider: false);
            k.Cylinder(core, "Plinth", new Vector3(0f, 0.15f, 0f), 2.7f, 0.3f, k.M.WallLower, Quaternion.identity, collider: false);
            k.Tank(core, new Vector3(0f, 0.3f, 0f), 1.3f, 4f, k.M.Liquid);
            k.Point(lights, "CoreGlow", new Vector3(0f, 2.4f, 54f), 4500f, 11f, new Color(0.3f, 0.75f, 1f));

            // Lab benches (south and north rows).
            foreach (float z in new[] { 44.5f, 63.5f })
            foreach (float x in new[] { -9f, -3.5f, 3.5f, 9f })
                LabBench(k, props, new Vector3(x, 0f, z), z > 54f ? 180f : 0f);
            k.Terminal(props, new Vector3(-4.6f, 0f, 51f), 90f, alert: false);
            k.Terminal(props, new Vector3(4.6f, 0f, 51f), -90f, alert: false);

            // Server racks along the east wall.
            for (float z = 43.5f; z < 65f; z += 0.72f)
            {
                if (Mathf.Abs(z - 54f) < 1.5f) continue;
                var rack = FacilityKit.Group(props, "ServerRack");
                rack.localPosition = new Vector3(r.X1 - 0.55f, 0f, z);
                k.Box(rack, "Body", new Vector3(0f, 1.1f, 0f), new Vector3(1.0f, 2.2f, 0.68f), k.M.Graphite_);
                k.Box(rack, "Front", new Vector3(-0.505f, 1.1f, 0f), new Vector3(0.01f, 2.0f, 0.6f), k.M.ServerLeds, collider: false);
            }

            k.WallScreen(props, new Vector3(-6f, 4.2f, r.Z1 - 0.06f), 180f, new Vector2(4f, 2f), alert: false);
            k.WallScreen(props, new Vector3(6f, 4.2f, r.Z1 - 0.06f), 180f, new Vector2(4f, 2f), alert: true);

            // Overhead ducts and pipe bundles.
            foreach (float z in new[] { 46.5f, 61.5f })
                k.Box(props, "OverheadDuct", new Vector3(0f, 6.2f, z), new Vector3(30f, 0.8f, 1.2f), k.M.Pipe, collider: false);
            for (int i = 0; i < 3; i++)
                k.Pipe(props, new Vector3(r.X0 - 0.3f, 6.5f - i * 0.25f, r.Z1 - 0.4f - i * 0.22f), new Vector3(r.X1 + 0.3f, 6.5f - i * 0.25f, r.Z1 - 0.4f - i * 0.22f), 0.09f, i == 1 ? k.M.PipeRed : k.M.Pipe);
            k.WallStrip(props, r, Side.South, 3.2f, k.M.StripBlue, 2f);
            k.WallStrip(props, r, Side.North, 3.2f, k.M.StripBlue, 2f);

            Grime(k, props, r, 10, 1.4f);
            k.ReflectionProbe(room, r);
            k.Audio(room, "Amb_ServerHum", new Vector3(13f, 1f, 54f), ProceduralAudioClips.Hum(), 0.3f, 16f);
            k.Audio(room, "Amb_CoreHum", new Vector3(0f, 2f, 54f), ProceduralAudioClips.Hum(), 0.25f, 12f);
        }

        private static void LabBench(FacilityKit k, Transform parent, Vector3 pos, float yaw)
        {
            var g = FacilityKit.Group(parent, "LabBench");
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            k.Box(g, "Top", new Vector3(0f, 0.95f, 0f), new Vector3(2.6f, 0.05f, 0.95f), k.M.SteelTrim);
            k.Box(g, "Cabinet", new Vector3(-0.7f, 0.46f, 0f), new Vector3(1.1f, 0.9f, 0.85f), k.M.Graphite_);
            k.Box(g, "Leg", new Vector3(1.2f, 0.46f, 0f), new Vector3(0.08f, 0.9f, 0.8f), k.M.Graphite_);
            var mon = FacilityKit.Group(g, "Monitor");
            mon.localPosition = new Vector3(0.6f, 1.25f, 0.3f);
            mon.localRotation = Quaternion.Euler(-8f, 180f, 0f);
            k.Box(mon, "Bezel", Vector3.zero, new Vector3(0.62f, 0.4f, 0.04f), k.M.Graphite_, collider: false);
            k.Screen(mon, new Vector3(0f, 0f, 0.022f), new Vector2(0.56f, 0.34f), alert: false, yaw: 180f);
            k.Cylinder(g, "SampleUnit", new Vector3(-0.6f, 1.12f, 0.1f), 0.12f, 0.3f, k.M.Pipe, Quaternion.identity, collider: false);
            k.Box(g, "Analyzer", new Vector3(-0.1f, 1.08f, 0.15f), new Vector3(0.4f, 0.22f, 0.35f), k.M.WallUpper, collider: false);
        }

        private static void BuildExperimentRoom(FacilityKit k, Transform world)
        {
            var r = ExperimentRoom;
            var room = k.Room(world, "05_ExperimentRoom", r, k.M.FloorDark, k.M.WallLower, k.M.WallLower, k.M.Ceiling,
                new Opening(Side.East, 54f));
            var props = FacilityKit.Group(room, "Props");
            var lights = FacilityKit.Group(room, "Lighting");

            k.CeilingLight(lights, new Vector3(-29f, 0f, 49f), r.Height, 900f, shadows: true);
            k.CeilingLight(lights, new Vector3(-19f, 0f, 49f), r.Height, 900f, shadows: false, flicker: true);
            k.CeilingLight(lights, new Vector3(-29f, 0f, 59f), r.Height, 900f, shadows: false);
            k.CeilingLight(lights, new Vector3(-19f, 0f, 59f), r.Height, 900f, shadows: true);
            var beacons = new List<EmergencyLight>
            {
                k.Beacon(lights, new Vector3(-27f, r.Height - 0.05f, 54f), rotate: true, Vector3.up),
                k.Beacon(lights, new Vector3(-21f, r.Height - 0.05f, 54f), rotate: true, Vector3.up),
                k.Beacon(lights, new Vector3(-15.6f, 3.2f, 52f), rotate: false, Vector3.right * -1f),
            };
            k.Alarm(room, "Alarm_Experiments", beacons.ToArray(), new Vector3(-24f, 4f, 54f), active: true);

            // Specimen tanks along both long walls; one is broken.
            foreach (float z in new[] { 45.6f, 62.4f })
            foreach (float x in new[] { -30f, -26.5f, -23f, -19.5f })
            {
                bool broken = Mathf.Approximately(x, -23f) && z > 54f;
                k.Tank(props, new Vector3(x, 0f, z), 0.7f, 2.6f, k.M.LiquidDim, broken);
                if (!broken) k.Point(lights, "TankGlow", new Vector3(x, 1.6f, z + (z > 54f ? -0.9f : 0.9f)), 220f, 4f, new Color(0.3f, 1f, 0.8f));
            }
            k.Decal(props, k.M.DecalAlienFluid, new Vector3(-23f, 0f, 60.8f), Vector3.up, 2.6f, 20f);
            k.Sparks(props, new Vector3(-23f, 2.9f, 61.9f), Vector3.back);
            for (int i = 0; i < 5; i++)
                k.Box(props, "GlassShard", new Vector3(-23f + k.Rand(-1.2f, 1.2f), 0.02f, 61f + k.Rand(-0.8f, 0.6f)), new Vector3(k.Rand(0.1f, 0.3f), 0.015f, k.Rand(0.05f, 0.2f)), k.M.SteelTrim, collider: false, yaw: k.Rand(0f, 180f));

            // Operating tables with surgical lamps.
            foreach (float x in new[] { -27f, -21f })
            {
                var t = FacilityKit.Group(props, "OperatingTable");
                t.localPosition = new Vector3(x, 0f, 54f);
                k.Box(t, "Column", new Vector3(0f, 0.42f, 0f), new Vector3(0.4f, 0.84f, 0.4f), k.M.Graphite_);
                k.Box(t, "Slab", new Vector3(0f, 0.9f, 0f), new Vector3(2.1f, 0.12f, 0.85f), k.M.SteelTrim);
                foreach (float sx in new[] { -0.5f, 0.3f })
                    k.Box(t, "Strap", new Vector3(sx, 0.97f, 0f), new Vector3(0.1f, 0.02f, 0.88f), k.M.Rubber, collider: false);
                k.Cylinder(t, "LampArm", new Vector3(0f, 3.2f, 0f), 0.03f, 3.6f, k.M.SteelTrim, Quaternion.identity, collider: false);
                k.Cylinder(t, "LampHead", new Vector3(0f, 2.2f, 0f), 0.35f, 0.12f, k.M.Graphite_, Quaternion.identity, collider: false);
                k.SpotDown(t, "SurgicalLamp", new Vector3(0f, 2.12f, 0f), 1100f, 60f, 5f, new Color(0.9f, 0.97f, 1f), shadows: x < -24f);
            }

            k.Biomass(props, new Vector3(-31.5f, 0f, 46f), 0.9f, 7);
            k.Biomass(props, new Vector3(-16.6f, 0.2f, 62.6f), 0.7f, 6);
            k.Biomass(props, new Vector3(-32.6f, 3.5f, 58f), 0.6f, 5);
            k.Terminal(props, new Vector3(-17.2f, 0f, 47.2f), -90f, alert: true);
            k.WallScreen(props, new Vector3(r.X0 + 0.06f, 2.6f, 54f), 90f, new Vector2(3f, 1.2f), alert: true);

            k.Smoke(props, new Vector3(-24f, 0.6f, 54f), new Vector3(17f, 1.2f, 19f), 7f, new Color(0.62f, 0.7f, 0.68f));
            Grime(k, props, r, 8, 1.3f);
            for (int i = 0; i < 3; i++)
                k.Decal(props, k.M.DecalAlienFluid, new Vector3(k.Rand(r.X0 + 2f, r.X1 - 2f), 0f, k.Rand(r.Z0 + 3f, r.Z1 - 3f)), Vector3.up, k.Rand(0.8f, 1.6f), k.Rand(0f, 360f));
            k.ReflectionProbe(room, r);
            k.Audio(room, "Amb_Hum", new Vector3(-24f, 2f, 54f), ProceduralAudioClips.Hum(), 0.22f, 14f);
        }

        private static void BuildCombatZone(FacilityKit k, Transform world)
        {
            var r = CombatZone;
            var room = k.Room(world, "06_CombatZone", r, k.M.FloorConcrete, k.M.WallUpper, k.M.Concrete, k.M.Ceiling,
                new Opening(Side.South, 0f), new Opening(Side.North, -8f));
            var props = FacilityKit.Group(room, "Props");
            var cover = FacilityKit.Group(room, "Cover");
            var lights = FacilityKit.Group(room, "Lighting");

            k.Door(room, "Door_ExtractionSealed", new Vector3(-8f, 0f, r.Z1 + 0.15f), 0f, locked: true, depth: 0.3f);

            foreach (float x in new[] { -9f, 9f })
            foreach (float z in new[] { 74f, 86f, 98f })
                k.CeilingLight(lights, new Vector3(x, 0f, z), r.Height, 9000f, shadows: z != 98f, panelLength: 2.2f, warm: true);
            foreach (float z in new[] { 80f, 92f })
                k.CeilingLight(lights, new Vector3(0f, 0f, z), r.Height, 7000f, shadows: false, panelLength: 2.2f, warm: true);
            var beacons = new[]
            {
                k.Beacon(lights, new Vector3(r.X0 + 0.1f, 5f, 80f), rotate: true, Vector3.left * -1f),
                k.Beacon(lights, new Vector3(r.X1 - 0.1f, 5f, 80f), rotate: true, Vector3.right * -1f),
                k.Beacon(lights, new Vector3(r.X0 + 0.1f, 5f, 96f), rotate: true, Vector3.left * -1f),
                k.Beacon(lights, new Vector3(r.X1 - 0.1f, 5f, 96f), rotate: true, Vector3.right * -1f),
            };
            k.Alarm(room, "Alarm_CombatZone", beacons, new Vector3(0f, 6f, 86f), active: true);

            // Roof trusses and columns.
            for (float z = 71f; z < 104f; z += 6f)
                k.Box(props, "Truss", new Vector3(0f, 7.55f, z), new Vector3(36f, 0.5f, 0.3f), k.M.Pipe, collider: false);
            foreach (float x in new[] { -10f, 10f })
            foreach (float z in new[] { 80f, 92f })
                k.Box(props, "Column", new Vector3(x, r.Height * 0.5f, z), new Vector3(0.9f, r.Height, 0.9f), k.M.Concrete);

            // Cover: concrete barriers, half walls, full walls, crates, containers.
            k.Barrier(cover, new Vector3(-6f, 0f, 75f), 0f);
            k.Barrier(cover, new Vector3(6f, 0f, 75f), 0f);
            k.Barrier(cover, new Vector3(-12f, 0f, 86f), 90f);
            k.Barrier(cover, new Vector3(12f, 0f, 86f), 90f);
            k.Barrier(cover, new Vector3(-4f, 0f, 95f), 10f);
            k.Barrier(cover, new Vector3(4.5f, 0f, 90f), -8f);
            k.Box(cover, "HalfWall", new Vector3(-14f, 0.55f, 78f), new Vector3(3.2f, 1.1f, 0.35f), k.M.Concrete);
            k.Box(cover, "HalfWall", new Vector3(14f, 0.55f, 78f), new Vector3(3.2f, 1.1f, 0.35f), k.M.Concrete);
            k.Box(cover, "HalfWall", new Vector3(-7f, 0.55f, 90f), new Vector3(0.35f, 1.1f, 3.2f), k.M.Concrete);
            k.Box(cover, "FullWall", new Vector3(0f, 1.2f, 86f), new Vector3(4f, 2.4f, 0.4f), k.M.Concrete);
            k.Box(cover, "FullWall", new Vector3(-15f, 1.2f, 99f), new Vector3(0.4f, 2.4f, 4f), k.M.Concrete);
            k.Container(cover, new Vector3(-2f, 0f, 78f), 0f, k.M.ContainerRed);
            k.Container(cover, new Vector3(-13.5f, 0f, 92f), 90f, k.M.Crate);
            k.Container(cover, new Vector3(13.5f, 0f, 70.6f), 0f, k.M.ContainerRed);
            k.Crate(cover, new Vector3(-10f, 0f, 72.5f), 1.2f, 6f);
            k.Crate(cover, new Vector3(-8.7f, 0f, 72.3f), 1.2f, -4f);
            k.Crate(cover, new Vector3(-9.4f, 1.2f, 72.4f), 1.1f, 15f);
            k.Crate(cover, new Vector3(10.5f, 0f, 81.5f), 1.2f, 20f);
            k.Crate(cover, new Vector3(-3f, 0f, 99.5f), 1.2f, 0f);
            k.Crate(cover, new Vector3(-1.7f, 0f, 99.8f), 1.0f, 12f);

            // Raised platform on the north-east with stairs and railings.
            var plat = FacilityKit.Group(props, "Platform");
            k.Box(plat, "Deck", new Vector3(9.85f, 1.9f, 100.35f), new Vector3(15.7f, 0.2f, 7.3f), k.M.FloorPlate);
            k.Box(plat, "Support", new Vector3(9.85f, 0.9f, 100.35f), new Vector3(15.3f, 1.8f, 6.9f), k.M.Graphite_);
            for (int i = 0; i < 10; i++)
            {
                float h = 0.2f * (i + 1);
                k.Box(plat, $"Step_{i + 1}", new Vector3(4f, h * 0.5f, 92.25f + i * 0.5f), new Vector3(2f, h, 0.5f), k.M.FloorPlate);
            }
            k.Box(plat, "Rail_Front", new Vector3(11.35f, 2.5f, 96.75f), new Vector3(12.7f, 0.06f, 0.06f), k.M.SteelTrim);
            k.Box(plat, "Rail_Front_Low", new Vector3(11.35f, 2.05f, 96.75f), new Vector3(12.7f, 0.5f, 0.04f), k.M.Grating);
            k.Box(plat, "Rail_West", new Vector3(2.05f, 2.5f, 100.35f), new Vector3(0.06f, 1.0f, 7.2f), k.M.Grating);
            for (float x = 5.2f; x < 17.6f; x += 2f) k.Box(plat, "RailPost", new Vector3(x, 2.5f, 96.75f), new Vector3(0.06f, 1.0f, 0.06f), k.M.SteelTrim, collider: false);

            k.Sparks(props, new Vector3(9.5f, 5f, 80f), Vector3.left);
            k.Smoke(props, new Vector3(-13.5f, 1.2f, 92f), new Vector3(6f, 2.4f, 8f), 8f, new Color(0.5f, 0.5f, 0.52f));
            for (int i = 0; i < 4; i++)
                k.Decal(props, k.M.DecalScorch, new Vector3(k.Rand(-14f, 14f), 0f, k.Rand(72f, 100f)), Vector3.up, k.Rand(1.2f, 2.4f), k.Rand(0f, 360f));
            Grime(k, props, r, 12, 1.8f);
            k.WallStrip(props, r, Side.East, 4.5f, k.M.StripRed, 2f);
            k.WallStrip(props, r, Side.West, 4.5f, k.M.StripRed, 2f);
            k.ReflectionProbe(room, r);
            k.Audio(room, "Amb_Hum", new Vector3(0f, 5f, 86f), ProceduralAudioClips.Hum(), 0.25f, 30f);
        }

        private static void BuildGlobalAmbience(FacilityKit k, Transform world)
        {
            var a = k.Audio(world, "Amb_FacilityBed", Vector3.zero, ProceduralAudioClips.Hum(), 0.08f, 500f, spatial: false);
            a.priority = 64;
        }

        private static void Grime(FacilityKit k, Transform parent, RoomRect r, int count, float maxSize)
        {
            for (int i = 0; i < count; i++)
            {
                var p = new Vector3(k.Rand(r.X0 + 0.6f, r.X1 - 0.6f), 0f, k.Rand(r.Z0 + 0.6f, r.Z1 - 0.6f));
                k.Decal(parent, k.M.DecalGrime, p, Vector3.up, k.Rand(0.6f, maxSize), k.Rand(0f, 360f));
            }
        }

        // ------------------------------------------------------------------ scene services

        private static void MarkStatic(Transform world)
        {
            foreach (var t in world.GetComponentsInChildren<Transform>(true))
            {
                bool moving = t.GetComponentInParent<AutoDoor>() != null && t.name.StartsWith("Leaf") || HasParentNamed(t, "Leaf_") || HasParentNamed(t, "Rotor");
                bool dynamicFx = t.GetComponent<ParticleSystem>() != null || t.GetComponent<Light>() != null;
                if (moving || dynamicFx) continue;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic |
                    StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.ContributeGI);
            }
        }

        private static bool HasParentNamed(Transform t, string prefix)
        {
            for (var p = t; p != null; p = p.parent) if (p.name.StartsWith(prefix)) return true;
            return false;
        }

        private static void BuildVolume()
        {
            FpsTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(VolumeProfilePath));
            AssetDatabase.DeleteAsset(VolumeProfilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);

            T Add<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }

            var env = Add<VisualEnvironment>();
            env.skyType.Override((int)SkyType.Gradient);
            env.skyAmbientMode.Override(SkyAmbientMode.Dynamic);

            var sky = Add<GradientSky>();
            sky.top.Override(new Color(0.16f, 0.19f, 0.24f));
            sky.middle.Override(new Color(0.11f, 0.12f, 0.14f));
            sky.bottom.Override(new Color(0.06f, 0.06f, 0.065f));
            sky.exposure.Override(5f);

            var exposure = Add<Exposure>();
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(4.4f);

            var fog = Add<Fog>();
            fog.enabled.Override(true);
            fog.enableVolumetricFog.Override(true);
            fog.meanFreePath.Override(45f);
            fog.baseHeight.Override(0f);
            fog.maximumHeight.Override(9f);
            fog.albedo.Override(new Color(0.78f, 0.82f, 0.88f));
            fog.anisotropy.Override(0.55f);
            fog.depthExtent.Override(48f);

            var tonemap = Add<Tonemapping>();
            tonemap.mode.Override(TonemappingMode.ACES);

            var color = Add<ColorAdjustments>();
            color.contrast.Override(14f);
            color.saturation.Override(-14f);
            color.colorFilter.Override(new Color(0.96f, 0.98f, 1.03f));

            var wb = Add<WhiteBalance>();
            wb.temperature.Override(-6f);

            var bloom = Add<Bloom>();
            bloom.intensity.Override(0.14f);
            bloom.scatter.Override(0.6f);

            var ssao = Add<ScreenSpaceAmbientOcclusion>();
            ssao.intensity.Override(0.9f);
            ssao.radius.Override(1.4f);

            var ssr = Add<ScreenSpaceReflection>();
            ssr.enabled.Override(true);

            var vignette = Add<Vignette>();
            vignette.intensity.Override(0.22f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var volumeGo = new GameObject("PostProcess_Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;
        }

        private static void BuildNavigation(Transform world)
        {
            var go = new GameObject("NavMesh");
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            EditorUtility.SetDirty(surface);
        }

        /// <summary>Named points used by automated tests (reachability) and screenshot QA.</summary>
        private static void BuildQaMarkers()
        {
            var root = new GameObject("QA").transform;
            void Nav(string n, Vector3 p) { var t = new GameObject($"Nav_{n}").transform; t.SetParent(root); t.position = p; }
            void View(string n, Vector3 p, Vector3 euler) { var t = new GameObject($"QA_View_{n}").transform; t.SetParent(root); t.SetPositionAndRotation(p, Quaternion.Euler(euler)); }

            Nav("AccessCorridor", new Vector3(0f, 0f, 8f));
            Nav("Armory", new Vector3(0f, 0f, 22f + 3f));
            Nav("MilitaryCorridor", new Vector3(0f, 0f, 34f));
            Nav("CentralLab", new Vector3(-6f, 0f, 58f));
            Nav("ExperimentRoom", new Vector3(-24f, 0f, 50f));
            Nav("CombatZone", new Vector3(0f, 0f, 92f));
            Nav("CombatPlatform", new Vector3(12f, 2f, 101f));

            View("01_Spawn", new Vector3(0f, 1.68f, 1.2f), new Vector3(4f, 0f, 0f));
            View("02_Armory", new Vector3(-6.5f, 1.68f, 17.5f), new Vector3(8f, 50f, 0f));
            View("03_Corridor", new Vector3(-0.8f, 1.68f, 29f), new Vector3(2f, 6f, 0f));
            View("04_Lab", new Vector3(-11f, 1.68f, 42f), new Vector3(4f, 40f, 0f));
            View("05_Experiments", new Vector3(-16.8f, 1.68f, 56f), new Vector3(6f, -110f, 0f));
            View("06_Combat", new Vector3(-2f, 1.68f, 70f), new Vector3(2f, 10f, 0f));
        }

        private static void SpawnPlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FpsTestSceneBuilder.PlayerPrefabPath);
            if (prefab == null) throw new FileNotFoundException("Player prefab missing; build EG_FPS_Test first", FpsTestSceneBuilder.PlayerPrefabPath);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.name = "EG_Player";
            player.transform.SetPositionAndRotation(SpawnPosition, Quaternion.identity);
        }

        /// <summary>Copies existing scenes to a dated backup before any rebuild.</summary>
        private static void BackupExistingScenes()
        {
            FpsTestSceneBuilder.EnsureFolder(BackupFolder);
            string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmm");
            foreach (string path in new[] { FpsTestSceneBuilder.ScenePath, ScenePath })
            {
                if (!File.Exists(path)) continue;
                string dest = $"{BackupFolder}/{Path.GetFileNameWithoutExtension(path)}_{stamp}.unity";
                if (!File.Exists(dest)) AssetDatabase.CopyAsset(path, dest);
            }
        }

        /// <summary>SSR is off in the template's HDRP assets; enable it on Balanced and High Fidelity (not Performant).</summary>
        private static void EnableScreenSpaceReflections()
        {
            foreach (string path in new[] { "Assets/Settings/HDRP Balanced.asset", "Assets/Settings/HDRP High Fidelity.asset" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path);
                if (asset == null) continue;
                var so = new SerializedObject(asset);
                var prop = so.FindProperty("m_RenderPipelineSettings.supportSSR");
                if (prop == null || prop.boolValue) continue;
                prop.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
