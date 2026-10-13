using System.Collections.Generic;
using System.IO;
using EclipseGenesis.Core;
using EclipseGenesis.EditorTools.Art;
using EclipseGenesis.EditorTools.Facility;
using EclipseGenesis.EditorTools.OpenZone;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace EclipseGenesis.EditorTools.OpenWorld
{
    /// <summary>
    /// Eclipse_Genesis_OpenWorld_Realistic, first slice: the residential street of the concept art (Docs/REFERENCE_COMPARISON.md,
    /// panel 2). A ~100 x 100 m playable block: two-lane cracked asphalt street with sidewalks and curbs, wooden houses on
    /// both sides (Blender models with glass and interiors; House_A furnished and explorable), a brick corner shop, round
    /// deciduous trees, wooden power poles with sagging cables, street props, abandoned cars, a military barricade, a low
    /// sunset sun at the end of the street with clouds, volumetric haze and neutral (non-orange) grading.
    /// Real art comes from ArtLibrary (Blender exports + CC0 Poly Haven). Nothing here replaces the previous scenes.
    /// </summary>
    public static class ResidentialStreetBuilder
    {
        public const string ScenePath = "Assets/_EclipseGenesis/Scenes/Eclipse_Genesis_OpenWorld_Realistic.unity";
        public const string ProfilePath = "Assets/_EclipseGenesis/Settings/OpenWorldRealistic_VolumeProfile.asset";
        public const string NavMeshPath = "Assets/_EclipseGenesis/Scenes/OpenWorld/OpenWorldRealistic_NavMesh.asset";
        public const string MeshLibraryPath = "Assets/_EclipseGenesis/Art/Generated/OpenWorldRealisticMeshes.asset";
        public const string TerrainFolder = "Assets/_EclipseGenesis/Art/Generated/TerrainOpenWorld";
        public static readonly Vector3 SpawnPosition = new Vector3(1.2f, 0.05f, -52f);

        private const float RoadHalf = 3.7f, CurbW = 0.2f, WalkW = 1.8f;
        private static float WalkOuter => RoadHalf + CurbW + WalkW;    // 5.7 m from the centre line
        private const float FrontLine = 14f;                            // house front walls at |x| = 14 m

        private static FacilityKit _kit;
        private static Terrain _terrain;
        private static System.Random _rng;
        private static float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        [MenuItem("Eclipse Genesis/Build/Open World Realistic (residential street)")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            if (!ArtLibrary.Has(ArtLibrary.HouseA)) throw new FileNotFoundException("Blender buildings missing: run house_builder.build_street_set()");
            _rng = new System.Random(2026);
            _kit = FacilitySceneBuilder.CreateKit(MeshLibraryPath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("OpenWorld_ResidentialStreet").transform;

            BuildTerrain(root);
            BuildStreet(root);
            BuildHouses(root);
            BuildTrees(root);
            BuildGrass(root);
            BuildPowerLines(root);
            BuildProps(root);
            BuildBarricade(root);
            FacilitySceneBuilder.MarkStatic(root);
            BuildLighting(root);
            BuildNavigation();
            var player = SpawnPlayer();
            BuildMission(root, player);
            BuildQaMarkers();
            new GameObject("PerformanceLogger").AddComponent<PerformanceLogger>();

            FpsTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[EclipseGenesis] Built {ScenePath}");
        }

        private static Material Lib(string id) => PolyHavenLibrary.LibraryMaterial(id);

        private static Material Paint(string name, Color color, float smooth)
        {
            string path = $"{PolyHavenLibrary.MaterialFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("HDRP/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ terrain

        private static void BuildTerrain(Transform root)
        {
            const int res = 257;
            var size = new Vector3(600f, 80f, 600f);
            var origin = new Vector3(-300f, -1f, -260f);
            var data = new TerrainData { heightmapResolution = res, alphamapResolution = 512, baseMapResolution = 1024 };
            data.size = size;
            data.terrainLayers = new[]
            {
                PolyHavenLibrary.TerrainLayerFor("leafy_grass"), PolyHavenLibrary.TerrainLayerFor("grass_ground"),
                PolyHavenLibrary.TerrainLayerFor("dry_mud_field_001"), PolyHavenLibrary.TerrainLayerFor("rocky_terrain_02"),
            };
            float Height(float x, float z)
            {
                // Flat neighbourhood; gentle rolls beyond it; hills and ridges far away (horizon silhouette).
                float dx = Mathf.Max(Mathf.Abs(x) - 70f, 0f), dz = Mathf.Max(-90f - z, z - 140f, 0f);
                float away = Mathf.Sqrt(dx * dx + dz * dz);
                float roll = (OpenZoneTerrain.Noise(x * 0.01f, z * 0.01f, 41) - 0.5f) * 6f * OpenZoneTerrain.Smooth(0f, 40f, away);
                float hills = OpenZoneTerrain.Smooth(40f, 200f, away) * (12f + 45f * OpenZoneTerrain.Noise(x * 0.006f, z * 0.006f, 43));
                return roll + hills;
            }
            var h = new float[res, res];
            for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
            {
                float x = origin.x + ix / (float)(res - 1) * size.x, z = origin.z + iz / (float)(res - 1) * size.z;
                h[iz, ix] = Mathf.Clamp01((Height(x, z) - origin.y) / size.y);
            }
            data.SetHeights(0, 0, h);
            var splat = new float[512, 512, 4];
            for (int iz = 0; iz < 512; iz++)
            for (int ix = 0; ix < 512; ix++)
            {
                float x = origin.x + ix / 511f * size.x, z = origin.z + iz / 511f * size.z;
                float slope = data.GetSteepness(ix / 511f, iz / 511f);
                float rock = OpenZoneTerrain.Smooth(20f, 32f, slope);
                float dry = OpenZoneTerrain.Smooth(0.45f, 0.62f, OpenZoneTerrain.Noise(x * 0.025f, z * 0.025f, 47));
                float mud = OpenZoneTerrain.Smooth(0.62f, 0.72f, OpenZoneTerrain.Noise(x * 0.05f, z * 0.05f, 49));
                float shoulder = 1f - OpenZoneTerrain.Smooth(WalkOuter, WalkOuter + 1.2f, Mathf.Abs(x));   // worn verge
                mud = Mathf.Max(mud, shoulder * 0.8f);
                float wRock = rock, wMud = mud * (1f - wRock), wDry = dry * (1f - wRock - wMud), wLeafy = Mathf.Max(0f, 1f - wRock - wMud - wDry);
                splat[iz, ix, 0] = wLeafy; splat[iz, ix, 1] = wDry; splat[iz, ix, 2] = wMud; splat[iz, ix, 3] = wRock;
            }
            data.SetAlphamaps(0, 0, splat);
            FpsTestSceneBuilder.EnsureFolder(TerrainFolder);
            AssetDatabase.DeleteAsset($"{TerrainFolder}/OpenWorld_TerrainData.asset");
            AssetDatabase.CreateAsset(data, $"{TerrainFolder}/OpenWorld_TerrainData.asset");
            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.transform.SetParent(root, false);
            go.transform.position = origin;
            var t = go.GetComponent<Terrain>();
            _terrain = t;
            t.heightmapPixelError = 3f;
            t.basemapDistance = 160f;
            t.drawInstanced = true;
        }

        // ------------------------------------------------------------------ street

        private static void BuildStreet(Transform root)
        {
            var g = FacilityKit.Group(root, "Street");
            var asphalt = Lib("asphalt_02");   // neutral grey (worn_asphalt reads brown)
            var walk = Lib("concrete_pavement_02");
            var curb = Lib("concrete_floor_worn_001");
            var paint = Paint("M_EG_RoadPaint", new Color(0.72f, 0.7f, 0.62f), 0.15f);
            const float z0 = -80f, z1 = 150f;
            for (float z = z0; z < z1; z += 23f)
            {
                float len = Mathf.Min(23f, z1 - z), zc = z + len / 2;
                _kit.Box(g, "Asphalt", new Vector3(0f, -0.04f, zc), new Vector3(RoadHalf * 2f, 0.12f, len), asphalt);
                foreach (float s in new[] { -1f, 1f })
                {
                    _kit.Box(g, "Curb", new Vector3(s * (RoadHalf + CurbW / 2), 0.0f, zc), new Vector3(CurbW, 0.34f, len), curb);
                    _kit.Box(g, "Sidewalk", new Vector3(s * (RoadHalf + CurbW + WalkW / 2), -0.01f, zc), new Vector3(WalkW, 0.3f, len), walk);
                }
            }
            // Cross street with crosswalks at z = 56.
            _kit.Box(g, "Asphalt_Cross", new Vector3(0f, -0.045f, 56f), new Vector3(160f, 0.12f, 7.4f), asphalt);
            for (int i = 0; i < 9; i++)
                foreach (float z in new[] { 51.4f, 60.6f })
                    _kit.Box(g, "Crosswalk", new Vector3(-3.0f + i * 0.75f, 0.022f, z), new Vector3(0.42f, 0.005f, 1.8f), paint, collider: false);
            // Faded, broken centre line (3 m dashes every 9 m, some missing).
            for (float z = z0 + 2f; z < z1; z += 9f)
            {
                if (z > 50f && z < 62f) continue;
                if (_rng.NextDouble() < 0.15) continue;
                _kit.Box(g, "LaneDash", new Vector3(R(-0.04f, 0.04f), 0.022f, z), new Vector3(0.14f, 0.005f, R(2.2f, 3f)), paint, collider: false);
            }
            // Driveways and walkways to the porches.
            foreach (var lot in Lots)
            {
                float s = Mathf.Sign(lot.X);
                _kit.Box(g, "Walkway", new Vector3(s * (WalkOuter + (FrontLine - 2.4f - WalkOuter) / 2f), -0.02f, lot.Z), new Vector3(FrontLine - 2.4f - WalkOuter, 0.08f, 1.2f), walk, collider: false);
                if (lot.Driveway)
                    _kit.Box(g, "Driveway", new Vector3(s * (WalkOuter + 6f), -0.02f, lot.Z + lot.Width * 0.5f + 1.8f), new Vector3(12f, 0.08f, 3.2f), curb, collider: false);
            }
            // Grime, oil and scorch decals on the asphalt.
            for (int i = 0; i < 26; i++)
                _kit.Decal(g, i % 3 == 0 ? _kit.M.DecalScorch : _kit.M.DecalGrime, new Vector3(R(-3f, 3f), 0.02f, R(-70f, 140f)), Vector3.up, R(1.5f, 3.5f), R(0f, 360f));
            // Manhole covers.
            foreach (float z in new[] { -30f, 12f, 47f })
                ArtLibrary.Place(ArtLibrary.PolyHaven("Environment", "water_manhole_cover"), g, new Vector3(R(-1.5f, 1.5f), 0.02f, z), R(0f, 360f), 1f, colliders: false);
        }

        // ------------------------------------------------------------------ houses

        private readonly struct Lot
        {
            public readonly float X, Z, Width;
            public readonly string Model;
            public readonly bool Driveway, Explorable;
            public Lot(float x, float z, string model, float width, bool driveway = false, bool explorable = false)
            { X = x; Z = z; Model = model; Width = width; Driveway = driveway; Explorable = explorable; }
        }

        /// <summary>Lots along the street (x = side, z = position). Width = facade length along the street.</summary>
        private static readonly Lot[] Lots =
        {
            new Lot(-1f, -38f, ArtLibrary.HouseD, 8.6f, driveway: true),
            new Lot(-1f, -21f, ArtLibrary.HouseA, 9f, explorable: true),
            new Lot(-1f, -4f, ArtLibrary.HouseC, 10f, driveway: true),
            new Lot(-1f, 12f, ArtLibrary.HouseB, 8f),
            new Lot(-1f, 28f, ArtLibrary.HouseA, 9f, driveway: true),
            new Lot(-1f, 74f, ArtLibrary.HouseC, 10f),
            new Lot(-1f, 92f, ArtLibrary.HouseB, 8f),
            new Lot(-1f, 110f, ArtLibrary.HouseD, 8.6f),
            new Lot(1f, -30f, ArtLibrary.HouseB, 8f, driveway: true),
            new Lot(1f, -13f, ArtLibrary.HouseC, 10f),
            new Lot(1f, 4f, ArtLibrary.HouseD, 8.6f, driveway: true),
            new Lot(1f, 20f, ArtLibrary.HouseA, 9f),
            new Lot(1f, 80f, ArtLibrary.HouseD, 8.6f),
            new Lot(1f, 98f, ArtLibrary.HouseA, 9f),
            new Lot(1f, 116f, ArtLibrary.HouseC, 10f),
        };

        private static float DepthOf(string model) =>
            model == ArtLibrary.HouseB ? 7.6f : model == ArtLibrary.HouseC ? 8.4f : 8f;

        private static void BuildHouses(Transform root)
        {
            var g = FacilityKit.Group(root, "Houses");
            int i = 0;
            foreach (var lot in Lots)
            {
                float s = Mathf.Sign(lot.X);
                float cx = s * (FrontLine + DepthOf(lot.Model) / 2f);
                var lotGo = FacilityKit.Group(g, $"Lot_{++i:00}_{Path.GetFileNameWithoutExtension(lot.Model)}");
                lotGo.localPosition = new Vector3(cx, 0f, lot.Z);
                lotGo.localRotation = Quaternion.Euler(0f, s < 0 ? 90f : -90f, 0f);   // model front (+Z) faces the street
                ArtLibrary.Place(lot.Model, lotGo, Vector3.zero, 0f);
                if (lot.Explorable) Furnish(lotGo);
                else if (_rng.NextDouble() < 0.6)
                    _kit.Point(lotGo, "WindowGlow", new Vector3(-2.5f, 2f, 0f), 260f, 6f, new Color(1f, 0.75f, 0.5f));
                // Porch clutter.
                if (_rng.NextDouble() < 0.6)
                    ArtLibrary.Place(ArtLibrary.PolyHaven("Environment", "cardboard_box_01"), lotGo, new Vector3(R(-2.5f, 2.5f), 0.45f, DepthOf(lot.Model) / 2f + 1.2f), R(0f, 360f));
            }
            // Corner shop on the right, facing the street right behind the sidewalk.
            var shop = FacilityKit.Group(g, "Lot_Shop");
            shop.localPosition = new Vector3(WalkOuter + 0.6f + 6f, 0f, 40f);
            shop.localRotation = Quaternion.Euler(0f, -90f, 0f);
            ArtLibrary.Place(ArtLibrary.ShopA, shop, Vector3.zero, 0f);
            ArtLibrary.Place(ArtLibrary.PolyHaven("Props", "steel_frame_shelves_01"), shop, new Vector3(-4f, 0.15f, -3f), 0f);
            ArtLibrary.Place(ArtLibrary.PolyHaven("Props", "steel_frame_shelves_01"), shop, new Vector3(-1.5f, 0.15f, -3f), 0f);
            ArtLibrary.Place(ArtLibrary.PolyHaven("Props", "drawer_cabinet"), shop, new Vector3(3.5f, 0.15f, -4.5f), 0f);
            _kit.Point(shop, "ShopLight", new Vector3(0f, 3.6f, 0f), 500f, 9f, new Color(1f, 0.85f, 0.65f));
        }

        /// <summary>House_A interior (house_builder.py layout): living room on -X, hall and stairs on +X, two bedrooms upstairs.</summary>
        private static void Furnish(Transform house)
        {
            const float f = 0.45f, l2 = 3.45f;
            void P(string cat, string id, float x, float y, float z, float yaw) =>
                ArtLibrary.Place(ArtLibrary.PolyHaven(cat, id), house, new Vector3(x, y, z), yaw + R(-5f, 5f));
            P("Props", "Sofa_01", -3.5f, f, 0.8f, 90f);
            P("Props", "ArmChair_01", -2.2f, f, 2.9f, 160f);
            P("Props", "dining_table", -2.4f, f, -2.3f, 0f);
            P("Props", "painted_wooden_chair_02", -2.4f, f, -1.4f, 180f);
            P("Props", "painted_wooden_chair_02", -1.6f, f, -2.5f, 265f);
            P("Props", "wooden_bookshelf_worn", -0.6f, f, -3.55f, 0f);
            P("Props", "drawer_cabinet", 1.8f, f, 3.55f, 180f);
            P("Environment", "cardboard_box_01", 0.8f, f, -1.2f, 30f);
            P("Props", "old_bed_frame", -2.8f, l2, -2.1f, 90f);
            P("Props", "steel_frame_shelves_01", -2.8f, l2, 3.55f, 180f);
            P("Props", "painted_wooden_chair_02", 1.0f, l2, 2.6f, 40f);
            _kit.Point(house, "LivingLamp", new Vector3(-2.5f, 2.9f, 0.3f), 420f, 7f, new Color(1f, 0.78f, 0.55f));
            _kit.Point(house, "UpstairsLamp", new Vector3(-2.5f, 5.6f, -1.2f), 300f, 6f, new Color(1f, 0.8f, 0.6f));
            _kit.ReflectionProbe(house, new RoomRect(-4.3f, 4.3f, -3.8f, 3.8f, 6f));
            for (int i = 0; i < 4; i++)
                _kit.Decal(house, _kit.M.DecalGrime, new Vector3(R(-3.5f, 2f), f + 0.01f, R(-3f, 3f)), Vector3.up, R(0.8f, 1.6f), R(0f, 360f));
        }

        // ------------------------------------------------------------------ vegetation

        private static void BuildTrees(Transform root)
        {
            var g = FacilityKit.Group(root, "Trees");
            var spots = new List<Vector3>();
            foreach (var lot in Lots)
            {
                float s = Mathf.Sign(lot.X);
                spots.Add(new Vector3(s * R(8.5f, 10.5f), 0f, lot.Z + lot.Width * 0.5f + R(2.5f, 4f)));   // front yard, between houses
                if (_rng.NextDouble() < 0.7) spots.Add(new Vector3(s * R(26f, 32f), 0f, lot.Z + R(-4f, 4f)));   // back yard
            }
            for (int i = 0; i < 40; i++)
            {
                float x = R(36f, 90f) * (i % 2 == 0 ? -1f : 1f), z = R(-80f, 150f);
                spots.Add(new Vector3(x, 0f, z));
            }
            foreach (var p in spots)
            {
                var t = ArtLibrary.PlaceTree(ArtLibrary.Broadleaves[_rng.Next(ArtLibrary.Broadleaves.Length)], g, p + Vector3.down * 0.1f, R(0f, 360f), R(0.85f, 1.2f), 0.35f);
                if (t != null) t.name = "Tree_Broadleaf";
            }
            // Wooded hills around the neighbourhood (horizon silhouette): mixed pines and broadleaves on the terrain.
            for (int i = 0, placed = 0; i < 2000 && placed < 260; i++)
            {
                float x = R(-260f, 260f), z = R(-240f, 320f);
                if (Mathf.Abs(x) < 70f && z > -90f && z < 140f) continue;
                float y = _terrain.SampleHeight(new Vector3(x, 0f, z)) + _terrain.transform.position.y;
                if (OpenZoneTerrain.Noise(x * 0.015f, z * 0.015f, 71) < 0.42f) continue;   // clumps and clearings
                string model = _rng.NextDouble() < 0.55 ? ArtLibrary.Pines[_rng.Next(ArtLibrary.Pines.Length)] : ArtLibrary.Broadleaves[_rng.Next(ArtLibrary.Broadleaves.Length)];
                ArtLibrary.PlaceTree(model, g, new Vector3(x, y - 0.2f, z), R(0f, 360f), R(0.9f, 1.4f), 0.3f);
                placed++;
            }
        }

        // ------------------------------------------------------------------ grass

        public const string GrassTexturePath = "Assets/_EclipseGenesis/Art/Generated/Textures/GrassBlades.png";

        /// <summary>Tall overgrown grass (concept art front yards): crossed alpha cards in 16 m chunk meshes.</summary>
        private static void BuildGrass(Transform root)
        {
            var g = FacilityKit.Group(root, "Grass");
            var mat = GrassMaterial();
            const float chunk = 16f;
            var chunks = new Dictionary<Vector2Int, (List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)>();
            bool Blocked(float x, float z)
            {
                float ax = Mathf.Abs(x);
                if (ax < WalkOuter + 0.25f) return true;                                   // road, curbs, sidewalks
                if (z > 52f && z < 61f) return true;                                          // cross street
                foreach (var lot in Lots)
                {
                    if (Mathf.Sign(lot.X) != Mathf.Sign(x)) continue;
                    float d = DepthOf(lot.Model);
                    if (ax > FrontLine - 3.4f && ax < FrontLine + d + 1.2f && Mathf.Abs(z - lot.Z) < lot.Width * 0.5f + 0.8f) return true;   // house + porch
                    if (ax < FrontLine - 2f && Mathf.Abs(z - lot.Z) < 0.9f) return true;     // walkway
                    if (lot.Driveway && ax < WalkOuter + 12.2f && Mathf.Abs(z - (lot.Z + lot.Width * 0.5f + 1.8f)) < 1.8f) return true;
                }
                if (x > WalkOuter && x < WalkOuter + 13f && z > 33f && z < 47f) return true; // shop
                return false;
            }
            int clumps = 0;
            for (int i = 0; i < 26000 && clumps < 9000; i++)
            {
                float x = R(-60f, 60f), z = R(-75f, 140f);
                if (Blocked(x, z)) continue;
                // Denser near the verges and in unkempt patches.
                float patch = OpenZoneTerrain.Noise(x * 0.08f, z * 0.08f, 61);
                float verge = 1f - OpenZoneTerrain.Smooth(WalkOuter + 0.3f, WalkOuter + 3f, Mathf.Abs(x));
                if (_rng.NextDouble() > Mathf.Clamp01(patch * 1.3f - 0.15f + verge * 0.5f)) continue;
                var key = new Vector2Int(Mathf.FloorToInt(x / chunk), Mathf.FloorToInt(z / chunk));
                if (!chunks.TryGetValue(key, out var c)) chunks[key] = c = (new List<Vector3>(), new List<Vector3>(), new List<Vector2>(), new List<int>());
                bool dry = OpenZoneTerrain.Noise(x * 0.05f, z * 0.05f, 63) > 0.52f;
                float h = R(0.45f, 1.0f) * (dry ? 0.85f : 1f), w = R(0.6f, 1.0f);
                float yaw = R(0f, 180f);
                for (int k = 0; k < 3; k++)
                {
                    float a = (yaw + k * 60f) * Mathf.Deg2Rad;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (w * 0.5f);
                    var lean = new Vector3(R(-0.12f, 0.12f), 0f, R(-0.12f, 0.12f));
                    var b = new Vector3(x, -0.05f, z);
                    int s = c.v.Count;
                    c.v.Add(b - d); c.v.Add(b + d); c.v.Add(b + d + Vector3.up * h + lean); c.v.Add(b - d + Vector3.up * h + lean);
                    for (int q = 0; q < 4; q++) c.n.Add(Vector3.up);   // soft, terrain-like lighting
                    float u0 = dry ? 0.5f : 0f, u1 = dry ? 1f : 0.5f;
                    float off = (float)_rng.NextDouble() * 0.25f;
                    c.uv.Add(new Vector2(u0 + off * 0.5f, 0f)); c.uv.Add(new Vector2(u0 + off * 0.5f + 0.25f, 0f));
                    c.uv.Add(new Vector2(u0 + off * 0.5f + 0.25f, 1f)); c.uv.Add(new Vector2(u0 + off * 0.5f, 1f));
                    c.t.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
                }
                clumps++;
            }
            foreach (var kv in chunks)
            {
                var mesh = new Mesh { name = $"Grass_{kv.Key.x}_{kv.Key.y}" };
                mesh.SetVertices(kv.Value.v);
                mesh.SetNormals(kv.Value.n);
                mesh.SetUVs(0, kv.Value.uv);
                mesh.SetTriangles(kv.Value.t, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                var go = new GameObject(mesh.name);
                go.transform.SetParent(g, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            Debug.Log($"[EclipseGenesis] Grass: {clumps} clumps in {chunks.Count} chunks");
        }

        /// <summary>Procedural blade atlas: left half green/yellow grass, right half dry straw (alpha cut-out).</summary>
        private static Material GrassMaterial()
        {
            const int W = 512, H = 256;
            var px = new Color32[W * H];
            var rnd = new System.Random(7);
            float Rn(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            for (int half = 0; half < 2; half++)
            {
                for (int i = 0; i < 90; i++)
                {
                    float x0 = half * 256 + Rn(4f, 252f), height = Rn(0.45f, 0.98f) * H, bend = Rn(-40f, 40f), width = Rn(2.5f, 5f);
                    Color baseC = half == 0
                        ? Color.Lerp(new Color(0.22f, 0.3f, 0.1f), new Color(0.45f, 0.48f, 0.2f), Rn(0f, 1f))
                        : Color.Lerp(new Color(0.55f, 0.47f, 0.28f), new Color(0.72f, 0.64f, 0.42f), Rn(0f, 1f));
                    for (int y = 0; y < height; y++)
                    {
                        float t = y / height;
                        float cx = x0 + bend * t * t;
                        float hw = width * (1f - t) + 0.4f;
                        Color c = Color.Lerp(baseC * 0.85f, baseC * 1.2f, t);
                        for (int x = Mathf.FloorToInt(cx - hw); x <= Mathf.CeilToInt(cx + hw); x++)
                        {
                            int xi = Mathf.Clamp(x, half * 256, half * 256 + 255);
                            float cov = Mathf.Clamp01(hw - Mathf.Abs(x - cx) + 0.5f);
                            if (cov <= 0f) continue;
                            int idx = y * W + xi;
                            var old = px[idx];
                            byte a = (byte)Mathf.Max(old.a, cov * 255f);
                            px[idx] = new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), a);
                        }
                    }
                }
            }
            // Bleed colour into transparent texels so mip-mapped edges do not turn dark.
            for (int i = 0; i < px.Length; i++) if (px[i].a == 0) px[i] = new Color32(90, 95, 50, 0);
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            FpsTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(GrassTexturePath));
            File.WriteAllBytes(GrassTexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(GrassTexturePath, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(GrassTexturePath);
            imp.alphaIsTransparency = true;
            imp.mipMapsPreserveCoverage = true;
            imp.alphaTestReferenceValue = 0.4f;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();

            string path = $"{PolyHavenLibrary.MaterialFolder}/M_EG_GrassCards.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("HDRP/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GrassTexturePath));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.18f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_AlphaCutoffEnable", 1f);
            m.SetFloat("_AlphaCutoff", 0.4f);
            m.SetFloat("_DoubleSidedEnable", 1f);
            m.enableInstancing = true;
            HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ power lines

        private static void BuildPowerLines(Transform root)
        {
            var g = FacilityKit.Group(root, "PowerLines");
            var wood = Lib("weathered_plank_siding");
            var cable = Paint("M_EG_Cable", new Color(0.03f, 0.03f, 0.035f), 0.35f);
            var insulator = Paint("M_EG_Insulator", new Color(0.55f, 0.6f, 0.55f), 0.7f);
            var wires = new List<(Vector3 a, Vector3 b, float sag)>();
            var right = new List<Vector3>();
            for (float z = -66f; z <= 140f; z += 31f) right.Add(new Vector3(WalkOuter + 0.7f, 0f, z));
            var left = new List<Vector3>();
            for (float z = -50f; z <= 140f; z += 38f) left.Add(new Vector3(-WalkOuter - 0.7f, 0f, z));

            void Pole(Vector3 p, float h, float arm, bool transformer)
            {
                var pole = FacilityKit.Group(g, "UtilityPole");
                pole.localPosition = p;
                pole.localRotation = Quaternion.Euler(R(-1.5f, 1.5f), R(-4f, 4f), R(-1.5f, 1.5f));
                _kit.Cylinder(pole, "Pole", new Vector3(0f, h / 2f, 0f), 0.13f, h, wood, Quaternion.identity);
                _kit.Box(pole, "Crossarm", new Vector3(0f, h - 0.45f, 0f), new Vector3(arm, 0.12f, 0.12f), wood, collider: false);
                _kit.Box(pole, "Brace", new Vector3(0f, h - 0.85f, 0f), new Vector3(arm * 0.55f, 0.06f, 0.06f), wood, collider: false, yaw: 0f);
                for (int i = 0; i < 3; i++)
                    _kit.Cylinder(pole, "Insulator", new Vector3(-arm * 0.42f + i * arm * 0.42f, h - 0.3f, 0f), 0.05f, 0.18f, insulator, Quaternion.identity, collider: false);
                if (transformer)
                    _kit.Cylinder(pole, "Transformer", new Vector3(0.38f, h - 2.2f, 0f), 0.3f, 0.95f, Paint("M_EG_Transformer", new Color(0.42f, 0.44f, 0.42f), 0.4f), Quaternion.identity, collider: false);
            }
            for (int i = 0; i < right.Count; i++) Pole(right[i], 9.5f, 2.4f, i % 2 == 1);
            for (int i = 0; i < left.Count; i++) Pole(left[i], 8.2f, 1.6f, false);
            for (int i = 1; i < right.Count; i++)
            {
                for (int k = 0; k < 3; k++)
                {
                    float dx = -1.0f + k * 1.0f;
                    wires.Add((right[i - 1] + new Vector3(dx, 9.2f, 0f), right[i] + new Vector3(dx, 9.2f, 0f), 0.9f));
                }
                wires.Add((right[i - 1] + new Vector3(0.15f, 7.2f, 0f), right[i] + new Vector3(0.15f, 7.2f, 0f), 1.2f));   // telecom
            }
            for (int i = 1; i < left.Count; i++)
                for (int k = 0; k < 2; k++)
                    wires.Add((left[i - 1] + new Vector3(-0.6f + k * 1.2f, 7.95f, 0f), left[i] + new Vector3(-0.6f + k * 1.2f, 7.95f, 0f), 1.0f));
            // Service drops to every house, many crossing the street (the concept art's tangle of cables).
            foreach (var lot in Lots)
            {
                float s = Mathf.Sign(lot.X);
                var poles = s > 0 ? right : left;
                Vector3 nearest = poles[0];
                foreach (var p in poles) if (Mathf.Abs(p.z - lot.Z) < Mathf.Abs(nearest.z - lot.Z)) nearest = p;
                var eave = new Vector3(s * (FrontLine + 0.2f), lot.Model == ArtLibrary.HouseB ? 3.2f : 6.0f, lot.Z + R(-2f, 2f));
                wires.Add((nearest + new Vector3(0f, s > 0 ? 7.2f : 7.6f, 0f), eave, 0.6f));
                if (s < 0 && _rng.NextDouble() < 0.6)
                {
                    Vector3 across = right[0];
                    foreach (var p in right) if (Mathf.Abs(p.z - lot.Z) < Mathf.Abs(across.z - lot.Z)) across = p;
                    wires.Add((across + new Vector3(-0.9f, 7.2f, 0f), eave + Vector3.up * 0.3f, 1.4f));
                }
            }
            // One fallen line hanging to the ground (invasion damage).
            wires.Add((right[3] + new Vector3(1f, 9.2f, 0f), right[3] + new Vector3(4f, 0.05f, 9f), 0.4f));

            var mesh = CableMesh(wires, 0.014f);
            var go = new GameObject("Cables");
            go.transform.SetParent(g, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = cable;
            mr.shadowCastingMode = ShadowCastingMode.On;
        }

        /// <summary>All cables as one mesh: 6-sided tubes along catenary-like curves.</summary>
        private static Mesh CableMesh(List<(Vector3 a, Vector3 b, float sag)> wires, float radius)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            const int segs = 14, sides = 5;
            foreach (var (a, b, sag) in wires)
            {
                var pts = new Vector3[segs + 1];
                for (int i = 0; i <= segs; i++)
                {
                    float t = i / (float)segs;
                    pts[i] = Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t);
                }
                int start = verts.Count;
                for (int i = 0; i <= segs; i++)
                {
                    Vector3 dir = (i < segs ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]).normalized;
                    Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
                    if (side.sqrMagnitude < 0.01f) side = Vector3.right;
                    Vector3 up = Vector3.Cross(side, dir);
                    for (int k = 0; k < sides; k++)
                    {
                        float ang = k / (float)sides * Mathf.PI * 2f;
                        verts.Add(pts[i] + (side * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * radius);
                    }
                }
                for (int i = 0; i < segs; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a0 = start + i * sides + k, a1 = start + i * sides + (k + 1) % sides;
                    int b0 = a0 + sides, b1 = a1 + sides;
                    tris.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                }
            }
            var mesh = new Mesh { name = "Cables", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ props, vehicles, barricade

        private static void BuildProps(Transform root)
        {
            var g = FacilityKit.Group(root, "StreetProps");
            string Env(string id) => ArtLibrary.PolyHaven("Environment", id);
            // Vehicles: a tarp-covered car at the left curb and one in a right driveway.
            ArtLibrary.Place(Env("covered_car"), g, new Vector3(-2.4f, 0.02f, -9f), 182f);
            ArtLibrary.Place(Env("covered_car"), g, new Vector3(WalkOuter + 5.5f, 0f, -27.5f), 95f);
            ArtLibrary.Place(Env("rusted_wheel_rim_01"), g, new Vector3(-4.6f, 0.02f, -14f), 30f);
            ArtLibrary.Place(Env("old_tyre"), g, new Vector3(-4.4f, 0.32f, -13.2f), 80f);
            // Sidewalk furniture.
            foreach (float z in new[] { -42f, -12f, 18f })
                ArtLibrary.Place(Env("street_lamp_02"), g, new Vector3(-WalkOuter + 0.5f, 0.15f, z), 90f);
            ArtLibrary.Place(Env("fire_hydrant"), g, new Vector3(WalkOuter - 0.4f, 0.15f, -6f), R(0f, 360f));
            ArtLibrary.Place(Env("utility_box_01"), g, new Vector3(WalkOuter + 1.4f, 0f, -35.3f), -90f);
            foreach (var lot in Lots)
            {
                if (_rng.NextDouble() > 0.55) continue;
                float s = Mathf.Sign(lot.X);
                ArtLibrary.Place(Env("metal_trash_can"), g, new Vector3(s * (WalkOuter + 0.6f), 0f, lot.Z + lot.Width * 0.5f + 0.6f), R(0f, 360f));
            }
            for (int i = 0; i < 5; i++)
                ArtLibrary.Place(Env("Barrel_01"), g, new Vector3(R(-5f, 5f) > 0 ? R(4.6f, 5.4f) : R(-5.4f, -4.6f), 0.15f, R(-40f, 40f)), R(0f, 360f));
            for (int i = 0; i < 8; i++)
                ArtLibrary.Place(Env("cardboard_box_01"), g, new Vector3(R(-6f, 6f), 0.02f, R(-45f, 45f)), R(0f, 360f));
        }

        private static void BuildBarricade(Transform root)
        {
            var g = FacilityKit.Group(root, "Barricade_Checkpoint");
            string barrier = ArtLibrary.PolyHaven("Environment", "concrete_road_barrier_02");
            float[] xs = { -3.3f, -1.1f, 1.1f, 3.3f };
            for (int i = 0; i < xs.Length; i++)
                ArtLibrary.Place(barrier, g, new Vector3(xs[i], 0.02f, 64f + (i % 2) * 0.6f), 90f + R(-8f, 8f));
            var props = new OpenZoneProps(_kit, 77);
            props.Sandbags(g, new Vector3(-6.5f, 0.15f, 63f), 0f, 3f);
            props.Sandbags(g, new Vector3(6.5f, 0.15f, 63f), 0f, 3f);
            _kit.Barrier(g, new Vector3(-1.5f, 0.02f, 45f), 25f);
            _kit.Barrier(g, new Vector3(2.4f, 0.02f, 47.5f), -12f);
            props.ChainFence(g, new Vector3(-12f, 0f, 66f), new Vector3(-6.5f, 0f, 66f));
            props.ChainFence(g, new Vector3(6.5f, 0f, 66f), new Vector3(12f, 0f, 66f));
            _kit.Box(g, "CheckpointSign", new Vector3(0f, 2.6f, 66.5f), new Vector3(4f, 0.8f, 0.06f), _kit.M.Hazard, collider: false);
            _kit.Cylinder(g, "SignPost_L", new Vector3(-2f, 1.3f, 66.5f), 0.05f, 2.6f, _kit.M.Graphite_, Quaternion.identity, collider: false);
            _kit.Cylinder(g, "SignPost_R", new Vector3(2f, 1.3f, 66.5f), 0.05f, 2.6f, _kit.M.Graphite_, Quaternion.identity, collider: false);
        }

        // ------------------------------------------------------------------ lighting

        private static void BuildLighting(Transform root)
        {
            // Sun low at the end of the street (north, slightly right), as in the concept art.
            var sun = new GameObject("Sun");
            sun.transform.SetParent(root, false);
            sun.transform.rotation = Quaternion.Euler(8f, 196f, 0f);
            sun.AddHDLight(LightType.Directional);
            var light = sun.GetComponent<Light>();
            light.lightUnit = LightUnit.Lux;
            light.intensity = 30000f;
            light.useColorTemperature = true;
            light.colorTemperature = 4600f;
            light.shadows = LightShadows.Soft;
            if (sun.TryGetComponent(out HDAdditionalLightData hd)) hd.angularDiameter = 1.2f;

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
            env.cloudType.Override((int)CloudType.CloudLayer);
            env.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
            Add<PhysicallyBasedSky>();
            var clouds = Add<CloudLayer>();
            clouds.opacity.Override(0.45f);
            clouds.layerA.rotation.Override(40f);
            clouds.layerA.altitude.Override(2500f);

            var exposure = Add<Exposure>();
            exposure.mode.Override(ExposureMode.AutomaticHistogram);
            exposure.limitMin.Override(9f);
            exposure.limitMax.Override(13.5f);
            exposure.compensation.Override(1.3f);   // backlit sunset: the histogram alone underexposes the street (verified with captures)
            exposure.adaptationMode.Override(AdaptationMode.Fixed);   // converge immediately (progressive adaptation left captures and first frames dark)

            var fog = Add<Fog>();
            fog.enabled.Override(true);
            fog.enableVolumetricFog.Override(true);
            fog.meanFreePath.Override(380f);
            fog.baseHeight.Override(0f);
            fog.maximumHeight.Override(60f);
            fog.albedo.Override(new Color(0.86f, 0.89f, 0.93f));
            fog.anisotropy.Override(0.65f);
            fog.depthExtent.Override(90f);

            Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var color = Add<ColorAdjustments>();
            color.contrast.Override(8f);
            color.saturation.Override(4f);
            Add<WhiteBalance>().temperature.Override(-10f);   // cool the sky and shadows; the sun stays warm
            var split = Add<SplitToning>();
            split.shadows.Override(new Color(0.42f, 0.5f, 0.62f));
            split.highlights.Override(new Color(0.56f, 0.53f, 0.49f));
            split.balance.Override(-20f);
            Add<Bloom>().intensity.Override(0.15f);
            var ao = Add<ScreenSpaceAmbientOcclusion>();
            ao.intensity.Override(0.9f);
            ao.radius.Override(1.4f);
            Add<ScreenSpaceReflection>().enabled.Override(true);
            Add<ContactShadows>().enable.Override(true);
            var indirect = Add<IndirectLightingController>();   // readable, bluish shadows as in the concept art
            indirect.indirectDiffuseLightingMultiplier.Override(1.8f);
            indirect.reflectionLightingMultiplier.Override(1.3f);
            Add<Vignette>().intensity.Override(0.15f);
            var shadows = Add<HDShadowSettings>();
            shadows.maxShadowDistance.Override(160f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var vol = new GameObject("Global_Volume").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
            _kit.ReflectionProbe(root, new RoomRect(-30f, 30f, -70f, 70f, 20f));
        }

        // ------------------------------------------------------------------ gameplay

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

        /// <summary>"Operación Primer Contacto" open-map flow: first objective = reach the residential zone (trigger by House_A).</summary>
        private static void BuildMission(Transform root, GameObject player)
        {
            var g = FacilityKit.Group(root, "Mission_PrimerContacto");
            EclipseGenesis.World.TriggerVolume Volume(string name, Vector3 c, Vector3 size)
            {
                var go = new GameObject(name);
                go.transform.SetParent(g, false);
                go.transform.position = c;
                var v = go.AddComponent<EclipseGenesis.World.TriggerVolume>();
                v.Size = size;
                return v;
            }
            var residential = Volume("Obj1_ResidentialZone", new Vector3(0f, 2f, -21f), new Vector3(30f, 5f, 4f));
            var toBase = Volume("Obj2_TowardsGenesis", new Vector3(0f, 2f, 70f), new Vector3(30f, 5f, 4f));
            var manager = g.gameObject.AddComponent<EclipseGenesis.Mission.MissionManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("openZoneOperation").boolValue = true;
            so.FindProperty("player").objectReferenceValue = player.GetComponent<EclipseGenesis.Combat.PlayerHealth>();
            so.FindProperty("labVolume").objectReferenceValue = residential;
            so.FindProperty("securityDoorVolume").objectReferenceValue = toBase;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildQaMarkers()
        {
            var root = new GameObject("QA").transform;
            void View(string n, Vector3 p, Vector3 e) { var t = new GameObject($"QA_View_{n}").transform; t.SetParent(root); t.SetPositionAndRotation(p, Quaternion.Euler(e)); }
            View("R1_StreetFPS", new Vector3(1.2f, 1.68f, -50f), new Vector3(1f, 4f, 0f));          // concept panel 2 framing
            View("R2_StreetMid", new Vector3(-1.5f, 1.68f, -16f), new Vector3(2f, 12f, 0f));
            View("R3_HouseInterior", new Vector3(-15.5f, 2.13f, -21.5f), new Vector3(8f, -23f, 0f));
            View("R4_Porch", new Vector3(-6.5f, 1.85f, -28f), new Vector3(3f, -48f, 0f));
            View("R5_Checkpoint", new Vector3(0.5f, 1.68f, 40f), new Vector3(2f, 0f, 0f));
            View("R6_Aerial", new Vector3(45f, 38f, -70f), new Vector3(28f, -30f, 0f));
        }
    }
}
