using System.Collections.Generic;
using EclipseGenesis.EditorTools.Facility;
using UnityEditor;
using UnityEngine;

namespace EclipseGenesis.EditorTools.OpenZone
{
    /// <summary>
    /// Outdoor terrain for Eclipse_OpenZone: rolling ground, ridge mountains closing the map, flattened pads for the
    /// outpost, town, roads and the Genesis complex, and an impact crater for the alien infestation. Five PBR layers
    /// painted by slope and zone (dry grass, dirt, rock, road gravel, infected crust).
    /// </summary>
    public sealed class OpenZoneTerrain
    {
        public const string Folder = "Assets/_EclipseGenesis/Art/Generated/Terrain";
        public static readonly Vector3 Origin = new Vector3(-200f, -2f, -260f);
        public static readonly Vector3 Size = new Vector3(400f, 60f, 400f);
        private const int HeightRes = 513;
        private const int SplatRes = 512;

        /// <summary>Axis-aligned rectangle (x0..x1, z0..z1) flattened to ground level with a soft edge.</summary>
        public readonly struct Pad
        {
            public readonly float X0, X1, Z0, Z1, Falloff, Height;
            public Pad(float x0, float x1, float z0, float z1, float falloff, float height = 0f) { X0 = x0; X1 = x1; Z0 = z0; Z1 = z1; Falloff = falloff; Height = height; }
        }

        public readonly List<Pad> Pads = new List<Pad>();
        public readonly List<(Vector2 a, Vector2 b, float half)> Roads = new List<(Vector2, Vector2, float)>();
        public Vector2 CraterCenter = new Vector2(-105f, -35f);
        public float CraterRadius = 24f;
        public readonly List<(Vector2 c, float r)> SmallCraters = new List<(Vector2, float)>();

        public Terrain Terrain { get; private set; }

        public Terrain Build(Transform parent)
        {
            var data = new TerrainData
            {
                heightmapResolution = HeightRes,
                alphamapResolution = SplatRes,
                baseMapResolution = 1024,
            };
            data.size = Size;
            data.terrainLayers = CreateLayers();
            data.SetHeights(0, 0, ComputeHeights());
            data.SetAlphamaps(0, 0, ComputeSplat(data));

            FpsTestSceneBuilder.EnsureFolder(Folder);
            AssetDatabase.DeleteAsset($"{Folder}/OpenZone_TerrainData.asset");
            AssetDatabase.CreateAsset(data, $"{Folder}/OpenZone_TerrainData.asset");

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.transform.SetParent(parent, false);
            go.transform.position = Origin;
            Terrain = go.GetComponent<Terrain>();
            Terrain.heightmapPixelError = 4f;
            Terrain.basemapDistance = 220f;
            Terrain.drawInstanced = true;
            Terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return Terrain;
        }

        /// <summary>World height of the ground at (x, z).</summary>
        public float GroundAt(float x, float z) => Terrain != null ? Terrain.SampleHeight(new Vector3(x, 0f, z)) + Origin.y : 0f;

        // ------------------------------------------------------------------ heights

        private float NaturalHeight(float x, float z)
        {
            float edge = Mathf.Min(Mathf.Min(x - Origin.x, Origin.x + Size.x - x), Mathf.Min(z - Origin.z, Origin.z + Size.z - z));
            float ridge = (1f - Mathf.SmoothStep(0f, 55f, edge)) * (22f + 18f * Noise(x * 0.012f, z * 0.012f, 7));
            float rolling = (Noise(x * 0.011f, z * 0.011f, 3) - 0.5f) * 7f + (Noise(x * 0.045f, z * 0.045f, 5) - 0.5f) * 1.2f;
            return rolling + ridge;
        }

        private float FlattenWeight(float x, float z, out float target)
        {
            float w = 0f;
            target = 0f;
            foreach (var p in Pads)
            {
                float dx = Mathf.Max(p.X0 - x, 0f, x - p.X1);
                float dz = Mathf.Max(p.Z0 - z, 0f, z - p.Z1);
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                float pw = 1f - Mathf.SmoothStep(0f, p.Falloff, d);
                if (pw > w) { w = pw; target = p.Height; }
            }
            foreach (var (a, b, half) in Roads)
            {
                float d = DistanceToSegment(new Vector2(x, z), a, b);
                float rw = 1f - Mathf.SmoothStep(half, half + 9f, d);
                if (rw > w) { w = rw; target = -0.04f; }
            }
            return w;
        }

        public float RoadDistance(float x, float z)
        {
            float best = float.MaxValue;
            foreach (var (a, b, half) in Roads) best = Mathf.Min(best, DistanceToSegment(new Vector2(x, z), a, b) - half);
            return best;
        }

        private float CraterOffset(float x, float z)
        {
            float off = 0f;
            float r = Vector2.Distance(new Vector2(x, z), CraterCenter) / CraterRadius;
            if (r < 1.6f) off += r < 1f ? -3.2f * (1f - r * r) : 1.6f * Mathf.Sin((r - 1f) / 0.6f * Mathf.PI);
            foreach (var (c, rad) in SmallCraters)
            {
                float rr = Vector2.Distance(new Vector2(x, z), c) / rad;
                if (rr < 1.5f) off += rr < 1f ? -1.1f * (1f - rr * rr) : 0.5f * Mathf.Sin((rr - 1f) / 0.5f * Mathf.PI);
            }
            return off;
        }

        private float[,] ComputeHeights()
        {
            var h = new float[HeightRes, HeightRes];
            for (int iz = 0; iz < HeightRes; iz++)
            for (int ix = 0; ix < HeightRes; ix++)
            {
                float x = Origin.x + ix / (float)(HeightRes - 1) * Size.x;
                float z = Origin.z + iz / (float)(HeightRes - 1) * Size.z;
                float natural = NaturalHeight(x, z);
                float w = FlattenWeight(x, z, out float target);
                float y = Mathf.Lerp(natural, target, w) + CraterOffset(x, z) * (1f - w * 0.85f);
                h[iz, ix] = Mathf.Clamp01((y - Origin.y) / Size.y);
            }
            return h;
        }

        // ------------------------------------------------------------------ splat

        private TerrainLayer[] CreateLayers()
        {
            return new[]
            {
                Layer("TL_DryGrass", "GroundGrass", 6f),
                Layer("TL_Dirt", "GroundDirt", 5f),
                Layer("TL_Rock", "GroundRock", 9f),
                Layer("TL_Gravel", "GroundGravel", 4f),
                Layer("TL_Infected", "GroundInfected", 6f),
            };
        }

        private static TerrainLayer Layer(string name, string texture, float tile)
        {
            var set = ProceduralTextures.Get(texture);
            string path = $"{Folder}/{name}.terrainlayer";
            FpsTestSceneBuilder.EnsureFolder(Folder);
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = set.Albedo;
            layer.normalMapTexture = set.Normal;
            layer.maskMapTexture = set.Mask;
            layer.normalScale = 1f;
            layer.tileSize = new Vector2(tile, tile);
            layer.smoothness = 0.2f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private float[,,] ComputeSplat(TerrainData data)
        {
            var map = new float[SplatRes, SplatRes, 5];
            for (int iz = 0; iz < SplatRes; iz++)
            for (int ix = 0; ix < SplatRes; ix++)
            {
                float nx = ix / (float)(SplatRes - 1), nz = iz / (float)(SplatRes - 1);
                float x = Origin.x + nx * Size.x, z = Origin.z + nz * Size.z;
                float slope = data.GetSteepness(nx, nz);

                float rock = Mathf.SmoothStep(22f, 34f, slope);
                float road = 1f - Mathf.SmoothStep(-0.5f, 1.5f, RoadDistance(x, z));
                float crater = 1f - Mathf.SmoothStep(CraterRadius * 0.9f, CraterRadius * 2.1f, Vector2.Distance(new Vector2(x, z), CraterCenter) + (Noise(x * 0.08f, z * 0.08f, 11) - 0.5f) * 14f);
                float wear = 0f;
                foreach (var p in Pads)
                {
                    float dx = Mathf.Max(p.X0 - x, 0f, x - p.X1), dz = Mathf.Max(p.Z0 - z, 0f, z - p.Z1);
                    wear = Mathf.Max(wear, 1f - Mathf.SmoothStep(0f, 6f, Mathf.Sqrt(dx * dx + dz * dz)));
                }
                float dirtNoise = Mathf.SmoothStep(0.58f, 0.7f, Noise(x * 0.03f, z * 0.03f, 13));
                float dirt = Mathf.Max(wear * 0.75f, dirtNoise * 0.8f);

                float wInf = crater;
                float wRoad = road * (1f - wInf);
                float wRock = rock * (1f - wRoad) * (1f - wInf);
                float wDirt = dirt * (1f - wRoad) * (1f - wRock) * (1f - wInf);
                float wGrass = Mathf.Max(0f, 1f - wInf - wRoad - wRock - wDirt);
                float sum = wGrass + wDirt + wRock + wRoad + wInf;
                map[iz, ix, 0] = wGrass / sum;
                map[iz, ix, 1] = wDirt / sum;
                map[iz, ix, 2] = wRock / sum;
                map[iz, ix, 3] = wRoad / sum;
                map[iz, ix, 4] = wInf / sum;
            }
            return map;
        }

        // ------------------------------------------------------------------ helpers

        public static float Noise(float x, float z, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f, f = 1f;
            for (int o = 0; o < 4; o++)
            {
                sum += Mathf.PerlinNoise(x * f + seed * 17.13f, z * f + seed * 31.7f) * amp;
                norm += amp;
                amp *= 0.5f;
                f *= 2f;
            }
            return sum / norm;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
