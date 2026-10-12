using System.Collections.Generic;
using System.IO;
using System.Linq;
using EclipseGenesis.AI;
using EclipseGenesis.Core;
using EclipseGenesis.EditorTools.Combat;
using EclipseGenesis.EditorTools.Facility;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace EclipseGenesis.EditorTools.OpenZone
{
    /// <summary>
    /// Builds Genesis_Outpost (kept separate from the hand-built Eclipse_OpenZone scene): a 400 x 400 m outdoor map (deployment outpost → destroyed town → alien-infested crater)
    /// around the Genesis bunker, which contains the full Genesis_Research_Facility interior and its mission.
    /// Command line: Unity -batchmode -executeMethod EclipseGenesis.EditorTools.OpenZone.OpenZoneBuilder.BuildFromCommandLine
    /// </summary>
    public static class OpenZoneBuilder
    {
        public const string ScenePath = "Assets/_EclipseGenesis/Scenes/Genesis_Outpost.unity";
        public const string PreviousVersionBackup = "Assets/_EclipseGenesis/Scenes/Backups/Genesis_Outpost_previous.unity";
        public const string NavMeshPath = "Assets/_EclipseGenesis/Scenes/OpenZone/Genesis_Outpost_NavMesh.asset";
        public const string ProfilePath = "Assets/_EclipseGenesis/Settings/Outpost_Outdoor_VolumeProfile.asset";
        public const string MeshLibraryPath = "Assets/_EclipseGenesis/Art/Generated/OpenZoneMeshes.asset";
        public static readonly Vector3 SpawnPosition = new Vector3(0f, 0.05f, -210f);

        /// <summary>Bounding box of the Genesis bunker that wraps the facility interior.</summary>
        public static readonly Bounds Bunker = new Bounds(new Vector3(-7.5f, 4.4f, 57.2f), new Vector3(53.6f, 8.8f, 116f));

        [MenuItem("Eclipse Genesis/Build/Genesis Outpost (open map, separate scene)")]
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
            BackupPreviousVersion();
            var kit = FacilitySceneBuilder.CreateKit(MeshLibraryPath);
            var props = new OpenZoneProps(kit, 4242);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("OpenZone").transform;

            var terrain = BuildTerrain(root);
            FacilitySceneBuilder.BuildWorld(kit, openZoneEntrance: true);
            BuildBunker(root, props);
            BuildRoads(root, props, terrain);
            BuildOutpost(root, props);
            BuildTown(root, props);
            BuildInfestation(root, props, terrain);
            Scatter(root, props, terrain);
            FacilitySceneBuilder.MarkStatic(root);

            BuildLighting(root);
            ConvertFacilityVolumeToLocal(FacilitySceneBuilder.BuildVolume());
            BuildNavigation();
            var player = SpawnPlayer();
            MissionBuilder.Apply(kit, player);
            SpawnOutdoorAliens(root);
            BuildQaMarkers();
            new GameObject("PerformanceLogger").AddComponent<PerformanceLogger>();

            FpsTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildSceneRegistry.Sync();
            AssetDatabase.SaveAssets();
            Debug.Log($"[EclipseGenesis] Built {ScenePath}: {kit.AllLights.Count} lights ({kit.ShadowedLights.Count} shadowed), {kit.Meshes.Count} meshes");
        }

        // ------------------------------------------------------------------ terrain & layout

        private static OpenZoneTerrain BuildTerrain(Transform root)
        {
            var t = new OpenZoneTerrain();
            t.Pads.Add(new OpenZoneTerrain.Pad(-40f, 26f, -16f, 122f, 12f, -0.05f));   // Genesis bunker
            t.Pads.Add(new OpenZoneTerrain.Pad(-60f, 60f, -150f, -40f, 18f, -0.04f));  // town
            t.Pads.Add(new OpenZoneTerrain.Pad(-34f, 34f, -234f, -176f, 14f, -0.04f)); // outpost
            t.Pads.Add(new OpenZoneTerrain.Pad(-12f, 12f, -40f, -14f, 10f, -0.04f));   // checkpoint before the bunker
            t.Roads.Add((new Vector2(0f, -234f), new Vector2(0f, -10f), 3.6f));
            t.Roads.Add((new Vector2(-62f, -95f), new Vector2(62f, -95f), 3f));
            t.Roads.Add((new Vector2(-4f, -62f), new Vector2(-82f, -42f), 2.4f));
            t.SmallCraters.Add((new Vector2(12f, -128f), 4f));
            t.SmallCraters.Add((new Vector2(-26f, -70f), 3.5f));
            t.SmallCraters.Add((new Vector2(34f, -30f), 5f));
            t.SmallCraters.Add((new Vector2(-44f, -162f), 4f));
            t.Build(root);
            return t;
        }

        private static void BuildBunker(Transform root, OpenZoneProps p)
        {
            var g = FacilityKit.Group(root, "GenesisBunker");
            var k = p.K;
            Vector3 c = Bunker.center, s = Bunker.size;
            float x0 = c.x - s.x / 2, x1 = c.x + s.x / 2, z0 = c.z - s.z / 2, z1 = c.z + s.z / 2, h = s.y;
            const float t = 0.4f;
            // Shell walls (south face has the entrance cut out around the corridor's blast door at x = 0).
            k.Box(g, "Wall_N", new Vector3(c.x, h / 2, z1 + t / 2), new Vector3(s.x + 2 * t, h, t), p.Concrete);
            k.Box(g, "Wall_W", new Vector3(x0 - t / 2, h / 2, c.z), new Vector3(t, h, s.z), p.Concrete);
            k.Box(g, "Wall_E", new Vector3(x1 + t / 2, h / 2, c.z), new Vector3(t, h, s.z), p.Concrete);
            k.Box(g, "Facade_W", new Vector3((x0 - t - 1.3f) / 2, h / 2, z0 - t / 2), new Vector3(-1.3f - (x0 - t), h, t), p.Concrete);
            k.Box(g, "Facade_E", new Vector3((x1 + t + 1.3f) / 2, h / 2, z0 - t / 2), new Vector3(x1 + t - 1.3f, h, t), p.Concrete);
            k.Box(g, "Facade_Lintel", new Vector3(0f, (2.9f + h) / 2, z0 - t / 2), new Vector3(2.6f, h - 2.9f, t), p.Concrete);
            k.Box(g, "Roof", new Vector3(c.x, h + 0.15f, c.z), new Vector3(s.x + 2 * t, 0.3f, s.z + 2 * t), p.Concrete);

            // Entrance dressing.
            k.Box(g, "EntranceFrame_L", new Vector3(-1.75f, 1.8f, z0 - t - 0.35f), new Vector3(0.6f, 3.6f, 0.7f), k.M.Graphite_);
            k.Box(g, "EntranceFrame_R", new Vector3(1.75f, 1.8f, z0 - t - 0.35f), new Vector3(0.6f, 3.6f, 0.7f), k.M.Graphite_);
            k.Box(g, "EntranceFrame_Top", new Vector3(0f, 3.9f, z0 - t - 0.35f), new Vector3(4.1f, 0.6f, 0.7f), k.M.Graphite_);
            k.Box(g, "HazardBand", new Vector3(0f, 3.9f, z0 - t - 0.71f), new Vector3(4.1f, 0.3f, 0.02f), k.M.Hazard, collider: false);
            k.Box(g, "GenesisSign", new Vector3(0f, 6.4f, z0 - t - 0.03f), new Vector3(9f, 1.1f, 0.06f), k.M.StripBlue, collider: false);
            k.Box(g, "SignFrame", new Vector3(0f, 6.4f, z0 - t - 0.01f), new Vector3(9.4f, 1.5f, 0.04f), k.M.Graphite_, collider: false);
            k.Beacon(g, new Vector3(0f, 4.4f, z0 - t - 0.75f), rotate: true, Vector3.back);
            foreach (float x in new[] { -12f, 12f })
            {
                var lamp = k.SpotDown(g, "Floodlight", new Vector3(x, 7.6f, z0 - t - 0.6f), 9000f, 80f, 30f, new Color(1f, 0.93f, 0.82f), shadows: x < 0f);
                lamp.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
                k.Box(g, "FloodHousing", new Vector3(x, 7.7f, z0 - t - 0.35f), new Vector3(0.9f, 0.4f, 0.6f), k.M.Graphite_, collider: false);
            }
            // Checkpoint in front of the entrance.
            k.Barrier(g, new Vector3(-5f, 0f, -14f), 0f);
            k.Barrier(g, new Vector3(5f, 0f, -14f), 0f);
            k.Barrier(g, new Vector3(-8f, 0f, -24f), 20f);
            p.Sandbags(g, new Vector3(-7f, 0f, -6f), 0f, 3f);
            p.Sandbags(g, new Vector3(7f, 0f, -6f), 0f, 3f);
            var booth = FacilityKit.Group(g, "GuardBooth");
            booth.localPosition = new Vector3(9f, 0f, -12f);
            k.Room(booth, "Booth", new RoomRect(-1.5f, 1.5f, -1.5f, 1.5f, 2.6f), k.M.FloorConcrete, p.Concrete, p.Concrete, p.Concrete,
                new Opening(Side.West, 0f, 1f, 2.1f), Opening.Window(Side.South, 0f, 2f, 1f, 2f), Opening.Window(Side.North, 0f, 2f, 1f, 2f));
            p.ChainFence(g, new Vector3(-36f, 0f, -3f), new Vector3(-12f, 0f, -3f));
            p.ChainFence(g, new Vector3(12f, 0f, -3f), new Vector3(24f, 0f, -3f));
            // Infection creeping up the facade.
            k.Biomass(g, new Vector3(-18f, 0f, z0 - t - 0.8f), 1.3f, 8);
            k.Biomass(g, new Vector3(-20f, 2.5f, z0 - t - 0.3f), 0.9f, 5);
        }

        private static void BuildRoads(Transform root, OpenZoneProps p, OpenZoneTerrain t)
        {
            var g = FacilityKit.Group(root, "Roads");
            // Asphalt main road from the outpost gate to the bunker checkpoint, with a broken centre line.
            for (float z = -176f; z < -18f; z += 20f)
            {
                float len = Mathf.Min(20f, -18f - z);
                p.K.Box(g, "Asphalt", new Vector3(0f, -0.01f, z + len / 2), new Vector3(7f, 0.06f, len), p.Asphalt);
                for (float dz = 1f; dz < len; dz += 6f)
                    p.K.Box(g, "LaneMark", new Vector3(0f, 0.022f, z + dz + 1.5f), new Vector3(0.15f, 0.005f, 3f), p.RoadLine, collider: false);
            }
            p.K.Box(g, "Asphalt_Cross", new Vector3(0f, -0.012f, -95f), new Vector3(120f, 0.06f, 6f), p.Asphalt);
            // Potholes / scorch on the road.
            foreach (var z in new[] { -150f, -112f, -71f, -40f })
                p.K.Decal(g, p.K.M.DecalScorch, new Vector3(p.R(-2f, 2f), 0f, z), Vector3.up, p.R(2f, 3.5f), p.R(0f, 360f));
        }

        private static void BuildOutpost(Transform root, OpenZoneProps p)
        {
            var g = FacilityKit.Group(root, "Outpost_Deployment");
            var k = p.K;
            p.Hesco(g, new Vector3(-28f, 0f, -180f), new Vector3(-5.5f, 0f, -180f));
            p.Hesco(g, new Vector3(5.5f, 0f, -180f), new Vector3(28f, 0f, -180f));
            p.Hesco(g, new Vector3(-28f, 0f, -230f), new Vector3(-28f, 0f, -180f));
            p.Hesco(g, new Vector3(28f, 0f, -230f), new Vector3(28f, 0f, -180f));
            p.Hesco(g, new Vector3(-28f, 0f, -230f), new Vector3(28f, 0f, -230f));
            p.Watchtower(g, new Vector3(-23f, 0f, -185f), 0f);
            p.Watchtower(g, new Vector3(23f, 0f, -225f), 180f);
            p.Helipad(g, new Vector3(-14f, 0f, -214f));
            p.Truck(g, new Vector3(13f, 0f, -200f), 0f, burnt: false);
            p.Truck(g, new Vector3(18f, 0f, -201f), 4f, burnt: false);
            p.Truck(g, new Vector3(-9f, 0f, -192f), 32f, burnt: true);
            k.Container(g, new Vector3(20f, 0f, -216f), 90f, k.M.ContainerRed);
            k.Container(g, new Vector3(16.5f, 0f, -216f), 90f, k.M.Crate);
            p.Sandbags(g, new Vector3(-6.5f, 0f, -183f), 90f, 3f);
            p.Sandbags(g, new Vector3(6.5f, 0f, -183f), 90f, 3f);
            for (int i = 0; i < 6; i++) k.Crate(g, new Vector3(-20f + i * 1.25f, 0f, -224f), 1.1f, p.R(-8f, 8f));
            for (int i = 0; i < 5; i++) p.Barrel(g, new Vector3(22f + p.R(-1f, 1f), 0f, -206f + i * 0.8f));
            var tent = FacilityKit.Group(g, "CommandTent");
            tent.localPosition = new Vector3(-16f, 0f, -198f);
            k.Room(tent, "Tent", new RoomRect(-3f, 3f, -2.5f, 2.5f, 2.6f), k.M.FloorPlate, p.Canvas, p.Canvas, p.Canvas, new Opening(Side.East, 0f, 1.6f, 2.2f));
            k.Terminal(tent, new Vector3(-1.5f, 0f, 0f), 90f, alert: false);
            k.Point(tent, "TentLight", new Vector3(0f, 2.2f, 0f), 900f, 7f, new Color(1f, 0.85f, 0.65f));
            foreach (float x in new[] { -5f, 5f })
            {
                var flood = k.SpotDown(g, "GateFlood", new Vector3(x, 6f, -178f), 7000f, 90f, 28f, new Color(1f, 0.9f, 0.75f), shadows: false);
                flood.transform.localRotation = Quaternion.Euler(60f, x < 0 ? 20f : -20f, 0f);
                k.Cylinder(g, "FloodPole", new Vector3(x, 3f, -178.5f), 0.09f, 6f, k.M.Graphite_, Quaternion.identity);
            }
        }

        private static void BuildTown(Transform root, OpenZoneProps p)
        {
            var g = FacilityKit.Group(root, "Town_Destroyed");
            p.House(g, "House_A", new Vector3(-16f, 0f, -60f), -90f, 9f, 8f, damaged: false, lit: true);
            p.House(g, "House_B", new Vector3(-16f, 0f, -80f), -90f, 8f, 7f, damaged: true, lit: false);
            p.CommandPost(g, "CommandPost", new Vector3(-19f, 0f, -118f), -90f);
            p.Ruin(g, "Ruin_A", new Vector3(-17f, 0f, -140f), -90f, 8f, 7f);
            p.House(g, "House_C", new Vector3(16f, 0f, -62f), 90f, 10f, 8f, damaged: false, lit: true);
            p.Warehouse(g, "Warehouse", new Vector3(21f, 0f, -80f), 90f);
            p.House(g, "House_D", new Vector3(16f, 0f, -120f), 90f, 9f, 8f, damaged: true, lit: true);
            p.House(g, "House_E", new Vector3(16f, 0f, -140f), 90f, 8f, 7f, damaged: false, lit: false);
            p.House(g, "House_F", new Vector3(40f, 0f, -106f), 180f, 9f, 7f, damaged: false, lit: true);
            p.House(g, "House_G", new Vector3(-40f, 0f, -106f), 180f, 8f, 7f, damaged: true, lit: false);
            p.House(g, "House_H", new Vector3(42f, 0f, -84f), 0f, 8f, 7f, damaged: false, lit: false);
            p.Ruin(g, "Ruin_B", new Vector3(-42f, 0f, -84f), 0f, 9f, 7f);

            // Street: wrecks, barricades, poles, lamps, yard walls and fences.
            p.Car(g, new Vector3(2.4f, 0f, -52f), 12f);
            p.Car(g, new Vector3(-1.8f, 0f, -104f), 196f);
            p.Car(g, new Vector3(3.4f, 0f, -133f), 348f);
            p.Car(g, new Vector3(26f, 0f, -93.5f), 82f);
            p.Car(g, new Vector3(-30f, 0f, -96.8f), 265f);
            p.Truck(g, new Vector3(-3f, 0f, -150f), 160f, burnt: true);
            p.K.Barrier(g, new Vector3(-2f, 0f, -45f), 8f);
            p.K.Barrier(g, new Vector3(2.6f, 0f, -47f), -12f);
            p.Sandbags(g, new Vector3(5.5f, 0f, -43f), 0f, 3f);
            p.Sandbags(g, new Vector3(-9f, 0f, -97.5f), 0f, 3f);
            p.Rubble(g, new Vector3(-8f, 0f, -79f), 1.5f);
            p.Rubble(g, new Vector3(9f, 0f, -124f), 1.4f);

            var poles = new List<Vector3>();
            for (float z = -170f; z <= -30f; z += 28f) poles.Add(new Vector3(6.5f, 0f, z));
            for (int i = 0; i < poles.Count; i++)
            {
                bool fallen = i == 3;
                p.PowerPole(g, poles[i], 0f, fallen);
                if (i > 0 && !fallen && i - 1 != 3)
                    foreach (float x in new[] { -0.9f, 0f, 0.9f })
                        p.Wire(g, poles[i - 1] + new Vector3(x, 8.6f, 0f), poles[i] + new Vector3(x, 8.6f, 0f));
            }
            for (float z = -160f; z <= -40f; z += 30f) p.StreetLamp(g, new Vector3(-5.5f, 0f, z), 90f, working: z > -140f && z != -100f);

            p.LowWall(g, new Vector3(-10.5f, 0f, -54f), new Vector3(-10.5f, 0f, -66f));
            p.LowWall(g, new Vector3(10.5f, 0f, -54f), new Vector3(10.5f, 0f, -70f));
            p.LowWall(g, new Vector3(-10.5f, 0f, -112f), new Vector3(-10.5f, 0f, -124f));
            p.ChainFence(g, new Vector3(30f, 0f, -70f), new Vector3(30f, 0f, -90f));
            p.ChainFence(g, new Vector3(-30f, 0f, -112f), new Vector3(-30f, 0f, -128f));
            p.K.ReflectionProbe(g, new RoomRect(-60f, 60f, -150f, -40f, 24f));
        }

        private static void BuildInfestation(Transform root, OpenZoneProps p, OpenZoneTerrain t)
        {
            var g = FacilityKit.Group(root, "Infestation_Crater");
            Vector2 c = t.CraterCenter;
            float Ground(float x, float z) => t.GroundAt(x, z);
            p.CrashedHull(g, new Vector3(c.x, Ground(c.x, c.y) - 1.2f, c.y), 35f);
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f + p.R(-0.3f, 0.3f);
                float r = p.R(16f, 30f);
                float x = c.x + Mathf.Cos(a) * r, z = c.y + Mathf.Sin(a) * r;
                p.AlienSpire(g, new Vector3(x, Ground(x, z) - 0.3f, z), p.R(6f, 12f));
            }
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f + 0.4f;
                float r = p.R(9f, 15f);
                float x = c.x + Mathf.Cos(a) * r, z = c.y + Mathf.Sin(a) * r;
                p.Nest(g, new Vector3(x, Ground(x, z), z), 4 + i % 3);
            }
            for (int i = 0; i < 14; i++)
            {
                float a = p.R(0f, Mathf.PI * 2f), r = p.R(4f, 38f);
                float x = c.x + Mathf.Cos(a) * r, z = c.y + Mathf.Sin(a) * r;
                p.K.Biomass(g, new Vector3(x, Ground(x, z), z), p.R(0.8f, 1.8f), 5);
            }
            for (int i = 0; i < 4; i++)
            {
                float a = i * 1.6f, r = p.R(6f, 26f);
                float x = c.x + Mathf.Cos(a) * r, z = c.y + Mathf.Sin(a) * r;
                p.K.Point(g, "AnomalyLight", new Vector3(x, Ground(x, z) + 2f, z), 1800f, 16f, new Color(0.5f, 0.2f, 1f));
            }
            p.K.Smoke(g, new Vector3(c.x, 2.5f, c.y), new Vector3(70f, 7f, 70f), 22f, new Color(0.55f, 0.35f, 0.7f));
            p.K.Audio(g, "Amb_Infestation", new Vector3(c.x, 3f, c.y), ProceduralAudioClips.AlienScreech(), 0.15f, 60f);
        }

        /// <summary>Dead trees, shrubs and boulders scattered away from roads, pads and buildings.</summary>
        private static void Scatter(Transform root, OpenZoneProps p, OpenZoneTerrain t)
        {
            var g = FacilityKit.Group(root, "Nature");
            int trees = 0, shrubs = 0, rocks = 0, guard = 0;
            while ((trees < 70 || shrubs < 140 || rocks < 60) && guard++ < 20000)
            {
                float x = p.R(-185f, 185f), z = p.R(-245f, 125f);
                if (Bunker.Contains(new Vector3(x, 1f, z)) || (x > -45f && x < 30f && z > -20f && z < 125f)) continue;
                if (t.RoadDistance(x, z) < 5f) continue;
                if (x > -64f && x < 64f && z > -154f && z < -38f && (Mathf.Abs(x) > 8f) && InsideBuildingLot(x, z)) continue;
                if (x > -36f && x < 36f && z > -236f && z < -174f) continue;
                if (Vector2.Distance(new Vector2(x, z), t.CraterCenter) < 14f) continue;
                float y = t.GroundAt(x, z);
                var n = t.Terrain.terrainData.GetInterpolatedNormal((x - OpenZoneTerrain.Origin.x) / OpenZoneTerrain.Size.x, (z - OpenZoneTerrain.Origin.z) / OpenZoneTerrain.Size.z);
                float slope = Vector3.Angle(n, Vector3.up);
                var pos = new Vector3(x, y, z);
                if (rocks < 60 && (slope > 16f || p.Chance(0.15f))) { p.Boulder(g, pos, p.R(0.8f, 3.2f)); rocks++; }
                else if (trees < 70 && slope < 24f && p.Chance(0.5f)) { p.DeadTree(g, pos); trees++; }
                else if (shrubs < 140 && slope < 30f) { p.AddShrub(g, pos); shrubs++; }
            }
        }

        private static bool InsideBuildingLot(float x, float z) =>
            (Mathf.Abs(x) > 8f && Mathf.Abs(x) < 50f && z > -150f && z < -50f);

        // ------------------------------------------------------------------ lighting & volumes

        private static void BuildLighting(Transform root)
        {
            var sun = new GameObject("Sun");
            sun.transform.SetParent(root, false);
            sun.transform.rotation = Quaternion.Euler(13f, -38f, 0f);
            sun.AddHDLight(LightType.Directional);
            var light = sun.GetComponent<Light>();
            light.lightUnit = LightUnit.Lux;
            light.intensity = 20000f;
            light.useColorTemperature = true;
            light.colorTemperature = 4100f;
            light.shadows = LightShadows.Soft;

            FpsTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(ProfilePath));
            AssetDatabase.DeleteAsset(ProfilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            T Add<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }

            var env = Add<VisualEnvironment>();
            env.skyType.Override((int)SkyType.PhysicallyBased);
            env.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
            Add<PhysicallyBasedSky>();

            var exposure = Add<Exposure>();
            exposure.mode.Override(ExposureMode.AutomaticHistogram);
            exposure.limitMin.Override(8.5f);
            exposure.limitMax.Override(13f);
            exposure.compensation.Override(0.4f);

            var fog = Add<Fog>();
            fog.enabled.Override(true);
            fog.enableVolumetricFog.Override(true);
            fog.meanFreePath.Override(320f);
            fog.baseHeight.Override(0f);
            fog.maximumHeight.Override(45f);
            fog.albedo.Override(new Color(0.9f, 0.82f, 0.74f));
            fog.anisotropy.Override(0.7f);
            fog.depthExtent.Override(80f);

            Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var color = Add<ColorAdjustments>();
            color.contrast.Override(16f);
            color.saturation.Override(-10f);
            color.colorFilter.Override(new Color(1.03f, 0.98f, 0.93f));
            Add<WhiteBalance>().temperature.Override(6f);
            Add<Bloom>().intensity.Override(0.1f);
            var ao = Add<ScreenSpaceAmbientOcclusion>();
            ao.intensity.Override(0.8f);
            ao.radius.Override(1.6f);
            Add<ScreenSpaceReflection>().enabled.Override(true);
            Add<Vignette>().intensity.Override(0.2f);
            var shadows = Add<HDShadowSettings>();
            shadows.maxShadowDistance.Override(180f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var vol = new GameObject("Outdoor_Volume").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 0f;
            vol.sharedProfile = profile;
        }

        /// <summary>The facility keeps its tuned interior look (fixed exposure, gradient sky) inside the bunker only.</summary>
        private static void ConvertFacilityVolumeToLocal(Volume v)
        {
            v.gameObject.name = "Facility_Interior_Volume";
            v.isGlobal = false;
            v.priority = 10f;
            v.blendDistance = 1.5f;
            var box = v.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Bunker.center;
            box.size = Bunker.size + new Vector3(-0.4f, 0f, -0.6f);
        }

        // ------------------------------------------------------------------ navigation, actors, QA

        private static void BuildNavigation()
        {
            var go = new GameObject("NavMesh");
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            FpsTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(NavMeshPath));
            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            EditorUtility.SetDirty(surface);
        }

        private static GameObject SpawnPlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FpsTestSceneBuilder.PlayerPrefabPath);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.name = "EG_Player";
            player.transform.SetPositionAndRotation(SpawnPosition, Quaternion.identity);
            return player;
        }

        private static void SpawnOutdoorAliens(Transform root)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AlienBuilder.PrefabPath);
            var g = FacilityKit.Group(root, "OutdoorAliens");
            var groups = new (Vector3 spawn, Vector3[] route)[]
            {
                (new Vector3(-8f, 0f, -74f), new[] { new Vector3(-8f, 0f, -60f), new Vector3(8f, 0f, -88f), new Vector3(-6f, 0f, -100f) }),
                (new Vector3(10f, 0f, -110f), new[] { new Vector3(28f, 0f, -98f), new Vector3(9f, 0f, -128f), new Vector3(-9f, 0f, -112f) }),
                (new Vector3(30f, 0f, -92f), new[] { new Vector3(48f, 0f, -96f), new Vector3(32f, 0f, -74f) }),
                (new Vector3(-95f, 0f, -30f), new[] { new Vector3(-118f, 0f, -22f), new Vector3(-100f, 0f, -52f) }),
                (new Vector3(-118f, 0f, -45f), new[] { new Vector3(-126f, 0f, -30f), new Vector3(-92f, 0f, -40f) }),
                (new Vector3(-88f, 0f, -48f), new[] { new Vector3(-74f, 0f, -42f), new Vector3(-104f, 0f, -14f) }),
                (new Vector3(-112f, 0f, -14f), new[] { new Vector3(-96f, 0f, -12f), new Vector3(-130f, 0f, -40f) }),
            };
            for (int i = 0; i < groups.Length; i++)
            {
                var a = (GameObject)PrefabUtility.InstantiatePrefab(prefab, g);
                a.name = $"EG_Alien_Stalker_Out_{i + 1}";
                Vector3 s = groups[i].spawn;
                if (UnityEngine.AI.NavMesh.SamplePosition(s, out var hit, 6f, UnityEngine.AI.NavMesh.AllAreas)) s = hit.position;
                a.transform.SetPositionAndRotation(s, Quaternion.Euler(0f, i * 47f, 0f));
                var route = FacilityKit.Group(g, $"Route_Out_{i + 1}");
                var pts = groups[i].route.Select((p, n) =>
                {
                    var pt = FacilityKit.Group(route, $"Point_{n + 1}");
                    pt.position = UnityEngine.AI.NavMesh.SamplePosition(p, out var h, 6f, UnityEngine.AI.NavMesh.AllAreas) ? h.position : p;
                    return pt;
                }).ToArray();
                ProtoKit.SetRefs(a.GetComponent<AlienBrain>(), "patrolPoints", pts);
            }
        }

        private static void BuildQaMarkers()
        {
            var root = new GameObject("QA").transform;
            void Nav(string n, Vector3 p) { var t = new GameObject($"Nav_{n}").transform; t.SetParent(root); t.position = p; }
            void View(string n, Vector3 p, Vector3 e) { var t = new GameObject($"QA_View_{n}").transform; t.SetParent(root); t.SetPositionAndRotation(p, Quaternion.Euler(e)); }

            Nav("Outpost", new Vector3(0f, 0f, -205f));
            Nav("TownStreet", new Vector3(0f, 0f, -95f));
            Nav("HouseInterior", new Vector3(-16f, 0f, -61.5f));
            Nav("CommandPostRoof", new Vector3(-19f, 7f, -121f));
            Nav("Warehouse", new Vector3(21f, 0f, -80f));
            Nav("Infestation", new Vector3(-90f, 0f, -45f));
            Nav("GenesisEntrance", new Vector3(0f, 0f, -8f));
            Nav("GenesisArmory", new Vector3(0f, 0f, 22f));

            View("01_Deployment", new Vector3(0f, 1.68f, -212f), new Vector3(3f, 0f, 0f));
            View("02_TownStreet", new Vector3(-3f, 1.68f, -150f), new Vector3(2f, 6f, 0f));
            View("03_HouseInterior", new Vector3(-14.5f, 1.6f, -58f), new Vector3(6f, 200f, 0f));
            View("04_Rooftop", new Vector3(-19f, 8.7f, -122f), new Vector3(6f, 60f, 0f));
            View("05_Infestation", new Vector3(-70f, 3f, -42f), new Vector3(6f, -95f, 0f));
            View("06_GenesisEntrance", new Vector3(4f, 1.68f, -32f), new Vector3(-3f, -6f, 0f));
            View("07_Aerial", new Vector3(80f, 70f, -230f), new Vector3(28f, -25f, 0f));
        }

        private static void BackupPreviousVersion()
        {
            if (!File.Exists(ScenePath) || File.Exists(PreviousVersionBackup)) return;
            FpsTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(PreviousVersionBackup));
            AssetDatabase.CopyAsset(ScenePath, PreviousVersionBackup);
        }
    }
}
