using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EclipseGenesis.EditorTools.Facility
{
    /// <summary>
    /// Generates tileable PBR texture sets (albedo, normal, HDRP mask map, optional emissive) as PNG assets.
    /// These are provisional stand-ins for authored/scanned textures, listed in Docs/ASSETS_REQUIRED.md.
    /// Albedo maps are near-neutral so one set can be tinted per material with _BaseColor.
    /// </summary>
    public static class ProceduralTextures
    {
        public const string Folder = "Assets/_EclipseGenesis/Art/Generated/Textures";

        public sealed class TextureSet
        {
            public Texture2D Albedo, Normal, Mask, Emissive;
        }

        private sealed class Canvas
        {
            public readonly int N;
            public readonly Color[] Albedo;
            public readonly float[] Height, Metal, Ao, Smooth;
            public Color[] Emissive;

            public Canvas(int n)
            {
                N = n;
                Albedo = new Color[n * n];
                Height = new float[n * n];
                Metal = new float[n * n];
                Ao = new float[n * n];
                Smooth = new float[n * n];
                for (int i = 0; i < Ao.Length; i++) Ao[i] = 1f;
            }
        }

        private static readonly Dictionary<string, TextureSet> Cache = new Dictionary<string, TextureSet>();

        public static TextureSet Get(string name, bool force = false)
        {
            if (!force && Cache.TryGetValue(name, out var cached) && cached.Albedo != null) return cached;
            var set = LoadOrGenerate(name, force);
            Cache[name] = set;
            return set;
        }

        // ------------------------------------------------------------------ generation entry

        private static TextureSet LoadOrGenerate(string name, bool force)
        {
            string albedoPath = $"{Folder}/{name}_Albedo.png";
            if (force || !File.Exists(albedoPath))
            {
                Canvas c = name switch
                {
                    "MetalPanel" => MetalPanel(1024),
                    "Concrete" => Concrete(1024),
                    "DiamondPlate" => DiamondPlate(512),
                    "Grating" => Grating(512),
                    "Hazard" => Hazard(512),
                    "ScreenBlue" => Screen(512, new Color(0.15f, 0.6f, 1f), 11),
                    "ScreenRed" => Screen(512, new Color(1f, 0.12f, 0.08f), 23),
                    "ServerLeds" => ServerLeds(256),
                    "AlienFlesh" => AlienFlesh(512),
                    "DecalGrime" => Decal(256, new Color(0.05f, 0.045f, 0.04f), 3, 0.85f),
                    "DecalScorch" => Decal(256, new Color(0.01f, 0.01f, 0.01f), 7, 1f),
                    "DecalAlienFluid" => Decal(256, new Color(0.16f, 0.03f, 0.22f), 13, 0.9f),
                    "GroundGrass" => GroundGrass(1024),
                    "GroundDirt" => GroundDirt(1024),
                    "GroundRock" => GroundRock(1024),
                    "GroundGravel" => GroundGravel(1024),
                    "GroundInfected" => GroundInfected(1024),
                    "Asphalt" => Asphalt(1024),
                    "Plaster" => Plaster(1024),
                    "Bark" => Bark(512),
                    _ => throw new ArgumentException($"Unknown procedural texture '{name}'"),
                };
                Write(name, c);
            }
            return Load(name);
        }

        private static void Write(string name, Canvas c)
        {
            Directory.CreateDirectory(Folder);
            int n = c.N;
            var albedo = new Color[n * n];
            var mask = new Color[n * n];
            for (int i = 0; i < albedo.Length; i++)
            {
                albedo[i] = c.Albedo[i];
                mask[i] = new Color(c.Metal[i], c.Ao[i], 1f, c.Smooth[i]);
            }
            Save($"{Folder}/{name}_Albedo.png", n, albedo);
            Save($"{Folder}/{name}_Mask.png", n, mask);
            Save($"{Folder}/{name}_Normal.png", n, NormalFromHeight(c.Height, n, 6f));
            if (c.Emissive != null) Save($"{Folder}/{name}_Emissive.png", n, c.Emissive);

            AssetDatabase.Refresh();
            Configure($"{Folder}/{name}_Albedo.png", srgb: true, normal: false);
            Configure($"{Folder}/{name}_Mask.png", srgb: false, normal: false);
            Configure($"{Folder}/{name}_Normal.png", srgb: false, normal: true);
            if (c.Emissive != null) Configure($"{Folder}/{name}_Emissive.png", srgb: true, normal: false);
        }

        private static TextureSet Load(string name) => new TextureSet
        {
            Albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{name}_Albedo.png"),
            Normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{name}_Normal.png"),
            Mask = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{name}_Mask.png"),
            Emissive = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{name}_Emissive.png"),
        };

        private static void Save(string path, int n, Color[] pixels)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, true);
            tex.SetPixels(pixels);
            tex.Apply(false);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void Configure(string path, bool srgb, bool normal)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ noise

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777215f;
            }
        }

        private static int Wrap(int v, int p) => ((v % p) + p) % p;

        /// <summary>Tileable value noise: x,y in cells, period in cells.</summary>
        private static float Value(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = Wrap(x0, period), xb = Wrap(x0 + 1, period), ya = Wrap(y0, period), yb = Wrap(y0 + 1, period);
            float a = Hash(xa, ya, seed), b = Hash(xb, ya, seed), c = Hash(xa, yb, seed), d = Hash(xb, yb, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Tileable fractal noise over u,v in [0,1).</summary>
        private static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int period = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += Value(u * period, v * period, period, seed + o * 31) * amp;
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }

        private static float Ridged(float u, float v, int basePeriod, int octaves, int seed)
        {
            float n = Fbm(u, v, basePeriod, octaves, seed);
            return 1f - Mathf.Abs(n * 2f - 1f);
        }

        private static Color[] NormalFromHeight(float[] h, int n, float strength)
        {
            var result = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float l = h[y * n + Wrap(x - 1, n)], r = h[y * n + Wrap(x + 1, n)];
                float d = h[Wrap(y - 1, n) * n + x], u = h[Wrap(y + 1, n) * n + x];
                var nrm = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                result[y * n + x] = new Color(nrm.x * 0.5f + 0.5f, nrm.y * 0.5f + 0.5f, nrm.z * 0.5f + 0.5f, 1f);
            }
            return result;
        }

        private static float Seam(float t, float width)
        {
            // Distance to the nearest integer line, as a 0 (in groove) .. 1 (flat) profile with a bevel.
            float d = Mathf.Abs(t - Mathf.Round(t));
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(width * 0.5f, width * 1.6f, d));
        }

        // ------------------------------------------------------------------ materials

        /// <summary>2 x 2 m: two 1 x 2 m panels with bevelled seams, inset sub-panels, rivets, brushed wear.</summary>
        private static Canvas MetalPanel(int n)
        {
            var c = new Canvas(n);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                float u = (float)x / n, v = (float)y / n;
                float pu = u * 2f, pv = v;                         // panel space: 2 panels across
                float seam = Mathf.Min(Seam(pu, 0.012f), Seam(pv, 0.006f));
                float lu = pu - Mathf.Floor(pu), lv = pv;          // local panel coords

                float inset = (lu > 0.1f && lu < 0.9f && lv > 0.08f && lv < 0.5f) ? 1f : 0f;
                float insetEdge = Mathf.Min(Seam((lu - 0.1f) / 0.8f, 0.015f), Seam((lv - 0.08f) / 0.42f, 0.015f));
                float rivet = 0f;
                foreach (var p in new[] { new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.03f), new Vector2(0.05f, 0.97f), new Vector2(0.95f, 0.97f), new Vector2(0.05f, 0.6f), new Vector2(0.95f, 0.6f) })
                {
                    float dd = Vector2.Distance(new Vector2(lu, lv * 2f), new Vector2(p.x, p.y * 2f));
                    rivet = Mathf.Max(rivet, Mathf.Clamp01(1f - dd / 0.02f));
                }

                float brushed = Fbm(u * 0.25f, v, 4, 3, 5) * 0.5f + Value(u * 512f, v * 8f, 512, 9) * 0.5f;
                float grime = Fbm(u, v, 3, 5, 17);
                float scratches = Mathf.Pow(Ridged(u, v, 16, 3, 41), 18f);

                c.Height[i] = seam * (0.85f - inset * 0.25f * insetEdge) + rivet * 0.35f + scratches * -0.05f;
                float edgeWear = (1f - seam) > 0.2f && (1f - seam) < 0.9f ? 0.15f : 0f;
                float baseTone = 0.62f + (brushed - 0.5f) * 0.12f - (grime - 0.5f) * 0.25f + edgeWear + scratches * 0.05f;
                baseTone *= Mathf.Lerp(0.35f, 1f, seam);
                c.Albedo[i] = new Color(baseTone, baseTone, baseTone * 1.02f, 1f);
                c.Metal[i] = Mathf.Lerp(0.3f, 0.9f, seam);
                c.Ao[i] = Mathf.Lerp(0.35f, 1f, seam) * Mathf.Lerp(0.85f, 1f, insetEdge);
                c.Smooth[i] = Mathf.Clamp01(0.42f + (brushed - 0.5f) * 0.25f - (grime - 0.5f) * 0.3f + scratches * 0.06f);
            }
            return c;
        }

        /// <summary>4 x 4 m slab with an expansion joint, blotches, pits and polished patches.</summary>
        private static Canvas Concrete(int n)
        {
            var c = new Canvas(n);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                float u = (float)x / n, v = (float)y / n;
                float joint = Mathf.Min(Seam(u, 0.004f), Seam(v, 0.004f));
                float blotch = Fbm(u, v, 4, 5, 3);
                float grain = Value(u * 400f, v * 400f, 400, 7);
                float pits = Fbm(u, v, 64, 2, 11) > 0.8f ? 1f : 0f;
                float stain = Mathf.SmoothStep(0.55f, 0.75f, Fbm(u, v, 3, 4, 29));
                float polish = Fbm(u, v, 2, 3, 51);

                c.Height[i] = joint * 0.8f + blotch * 0.08f + grain * 0.03f - pits * 0.12f;
                float tone = 0.58f + (blotch - 0.5f) * 0.25f + (grain - 0.5f) * 0.06f - stain * 0.1f - pits * 0.07f;
                tone *= Mathf.Lerp(0.45f, 1f, joint);
                c.Albedo[i] = new Color(tone, tone * 0.99f, tone * 0.97f, 1f);
                c.Metal[i] = 0f;
                c.Ao[i] = Mathf.Lerp(0.5f, 1f, joint) * (1f - pits * 0.4f);
                c.Smooth[i] = Mathf.Clamp01(0.22f + polish * 0.28f - stain * 0.08f - pits * 0.15f);
            }
            return c;
        }

        /// <summary>1 x 1 m tread plate with alternating diamond bumps.</summary>
        private static Canvas DiamondPlate(int n)
        {
            var c = new Canvas(n);
            const int cells = 12;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                float u = (float)x / n, v = (float)y / n;
                float cu = u * cells, cv = v * cells;
                int ix = Mathf.FloorToInt(cu), iy = Mathf.FloorToInt(cv);
                float fx = cu - ix - 0.5f, fy = cv - iy - 0.5f;
                bool flip = ((ix + iy) & 1) == 1;
                float a = flip ? fx + fy : fx - fy, b = flip ? fx - fy : fx + fy;
                float bump = Mathf.Clamp01(1f - (Mathf.Abs(a) / 0.42f + Mathf.Abs(b) / 0.1f));
                bump = Mathf.SmoothStep(0f, 1f, bump * 2.5f);
                float wear = Fbm(u, v, 4, 4, 61);
                float scratches = Mathf.Pow(Ridged(u, v, 12, 3, 77), 16f);

                c.Height[i] = bump * 0.6f;
                float tone = 0.6f + (wear - 0.5f) * 0.3f + bump * 0.12f + scratches * 0.2f;
                c.Albedo[i] = new Color(tone, tone, tone, 1f);
                c.Metal[i] = 0.95f;
                c.Ao[i] = Mathf.Lerp(0.8f, 1f, bump);
                c.Smooth[i] = Mathf.Clamp01(0.45f + bump * 0.18f - (wear - 0.5f) * 0.3f + scratches * 0.2f);
            }
            return c;
        }

        /// <summary>Square-mesh grating; holes are transparent in albedo alpha (used with alpha clipping).</summary>
        private static Canvas Grating(int n)
        {
            var c = new Canvas(n);
            const int cells = 8;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                float u = (float)x / n, v = (float)y / n;
                float bars = Mathf.Min(Seam(u * cells, 0.22f), Seam(v * cells, 0.22f));
                float solid = 1f - bars;
                float wear = Fbm(u, v, 4, 3, 83);
                c.Height[i] = solid;
                float tone = 0.55f + (wear - 0.5f) * 0.25f;
                c.Albedo[i] = new Color(tone, tone, tone, solid > 0.5f ? 1f : 0f);
                c.Metal[i] = 0.9f;
                c.Smooth[i] = 0.4f + (wear - 0.5f) * 0.2f;
            }
            return c;
        }

        /// <summary>Worn dark-red / black diagonal warning stripes (1 x 1 m).</summary>
        private static Canvas Hazard(int n)
        {
            var c = new Canvas(n);
            var red = new Color(0.42f, 0.04f, 0.035f);
            var black = new Color(0.03f, 0.03f, 0.032f);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                float u = (float)x / n, v = (float)y / n;
                float stripe = Mathf.Repeat((u + v) * 4f, 1f) < 0.5f ? 1f : 0f;
                float wear = Mathf.SmoothStep(0.62f, 0.72f, Fbm(u, v, 8, 4, 91));
                Color paint = stripe > 0.5f ? red : black;
                Color bare = new Color(0.35f, 0.35f, 0.34f);
                c.Albedo[i] = Color.Lerp(paint, bare, wear);
                c.Height[i] = 1f - wear * 0.2f;
                c.Metal[i] = wear * 0.6f;
                c.Smooth[i] = 0.35f + wear * 0.1f;
            }
            return c;
        }

        /// <summary>Emissive control-screen content: grid, text rows, bar charts and a trace line. Tiles vertically.</summary>
        private static Canvas Screen(int n, Color tint, int seed)
        {
            var c = new Canvas(n) { Emissive = new Color[n * n] };
            var rng = new System.Random(seed);
            var rows = new float[64];
            for (int r = 0; r < rows.Length; r++) rows[r] = 0.15f + (float)rng.NextDouble() * 0.5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                float u = (float)x / n, v = (float)y / n;
                float glow = 0.04f;
                if (Mathf.Repeat(u * 16f, 1f) < 0.04f || Mathf.Repeat(v * 16f, 1f) < 0.04f) glow = 0.12f;

                if (u < 0.55f)
                {
                    int row = Mathf.FloorToInt(v * 64f);
                    float inRow = Mathf.Repeat(v * 64f, 1f);
                    float word = Value(u * 24f, row, 24, seed);
                    if (inRow > 0.25f && inRow < 0.75f && u > 0.04f && u < rows[row] && word > 0.3f) glow = 0.8f;
                }
                else
                {
                    float bar = Mathf.Floor((u - 0.55f) * 20f);
                    float height = 0.2f + 0.6f * Value(bar, 0f, 20, seed + 3);
                    if (Mathf.Repeat((u - 0.55f) * 20f, 1f) < 0.7f && Mathf.Repeat(v * 2f, 1f) < height * 0.5f) glow = 0.55f;
                    float trace = 0.75f + 0.18f * Mathf.Sin(u * 40f + Mathf.Sin(u * 7f) * 3f);
                    if (Mathf.Abs(Mathf.Repeat(v * 2f, 1f) - trace) < 0.006f) glow = 1f;
                }

                c.Emissive[i] = new Color(tint.r * glow, tint.g * glow, tint.b * glow, 1f);
                c.Albedo[i] = new Color(0.02f, 0.025f, 0.03f, 1f);
                c.Height[i] = 1f;
                c.Smooth[i] = 0.9f;
            }
            return c;
        }

        private static Canvas ServerLeds(int n)
        {
            var c = new Canvas(n) { Emissive = new Color[n * n] };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                float u = (float)x / n, v = (float)y / n;
                int unit = Mathf.FloorToInt(v * 16f);
                float lv = Mathf.Repeat(v * 16f, 1f);
                float seam = Seam(v * 16f, 0.04f);
                float vent = (u > 0.35f && Mathf.Repeat(u * 40f, 1f) < 0.5f && lv > 0.3f && lv < 0.7f) ? 0.3f : 1f;
                c.Albedo[i] = new Color(0.05f, 0.055f, 0.06f, 1f) * vent;
                c.Height[i] = seam * vent;
                c.Metal[i] = 0.6f;
                c.Smooth[i] = 0.5f;
                Color led = Color.black;
                for (int k = 0; k < 4; k++)
                {
                    // Distance measured in rack-unit space (one unit = 1/16 of the texture height).
                    float du = (u - (0.06f + k * 0.05f)) * 16f, dv = lv - 0.5f;
                    if (du * du + dv * dv < 0.03f)
                    {
                        float pick = Hash(k, unit, 5);
                        led = pick < 0.1f ? new Color(1f, 0.1f, 0.05f) : pick < 0.6f ? new Color(0.1f, 0.6f, 1f) : new Color(0.2f, 1f, 0.4f);
                    }
                }
                c.Emissive[i] = led;
            }
            return c;
        }

        private static Canvas AlienFlesh(int n)
        {
            var c = new Canvas(n) { Emissive = new Color[n * n] };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                float u = (float)x / n, v = (float)y / n;
                float body = Fbm(u, v, 4, 5, 101);
                float veins = Mathf.Pow(Ridged(u, v, 3, 4, 107), 10f);
                float pores = Fbm(u, v, 48, 2, 113);
                c.Height[i] = body * 0.6f + veins * 0.5f - pores * 0.1f;
                c.Albedo[i] = Color.Lerp(new Color(0.05f, 0.02f, 0.07f), new Color(0.16f, 0.06f, 0.18f), body) + new Color(0.05f, 0.02f, 0.08f) * veins;
                c.Metal[i] = 0f;
                c.Smooth[i] = 0.55f + veins * 0.25f;
                c.Emissive[i] = new Color(0.45f, 0.15f, 1f) * veins;
            }
            return c;
        }

        /// <summary>Round, ragged stain; opacity lives in albedo alpha (HDRP decal convention).</summary>
        private static Canvas Decal(int n, Color color, int seed, float maxOpacity)
        {
            var c = new Canvas(n);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                float u = (float)x / n, v = (float)y / n;
                float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                float edge = Fbm(u, v, 4, 4, seed) * 0.6f;
                float a = Mathf.Clamp01((1f - r) * 1.6f + edge - 0.45f);
                a *= 0.6f + 0.4f * Fbm(u, v, 16, 3, seed + 1);
                c.Albedo[i] = new Color(color.r, color.g, color.b, Mathf.Clamp01(a) * maxOpacity);
                c.Height[i] = a * 0.3f;
                c.Smooth[i] = 0.6f;
            }
            return c;
        }

        // ------------------------------------------------------------------ outdoor (Eclipse_OpenZone)

        private static void Fill(Canvas c, System.Func<float, float, (Color albedo, float height, float metal, float smooth, float ao)> f)
        {
            int n = c.N;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                var s = f((float)x / n, (float)y / n);
                c.Albedo[i] = s.albedo;
                c.Height[i] = s.height;
                c.Metal[i] = s.metal;
                c.Smooth[i] = s.smooth;
                c.Ao[i] = s.ao;
            }
        }

        /// <summary>Dry, sparse grass over soil: olive/straw blades with dirt showing through (tile ~6 m).</summary>
        private static Canvas GroundGrass(int n)
        {
            var c = new Canvas(n);
            Fill(c, (u, v) =>
            {
                float patch = Fbm(u, v, 3, 5, 401);
                float blades = Value(u * 380f, v * 95f, 380, 403) * 0.6f + Value(u * 190f, v * 760f, 190, 405) * 0.4f;
                float soil = Mathf.SmoothStep(0.62f, 0.75f, Fbm(u, v, 6, 4, 407));
                var olive = new Color(0.24f, 0.25f, 0.13f);
                var straw = new Color(0.42f, 0.37f, 0.22f);
                var dirt = new Color(0.27f, 0.22f, 0.16f);
                Color grass = Color.Lerp(olive, straw, patch) * (0.75f + 0.45f * blades);
                Color a = Color.Lerp(grass, dirt * (0.85f + 0.3f * blades), soil);
                return (a, blades * (1f - soil) * 0.7f + 0.2f, 0f, 0.12f + 0.08f * soil, Mathf.Lerp(0.65f, 1f, blades));
            });
            return c;
        }

        /// <summary>Compacted soil with pebbles and tyre-worn patches (tile ~5 m).</summary>
        private static Canvas GroundDirt(int n)
        {
            var c = new Canvas(n);
            Fill(c, (u, v) =>
            {
                float base_ = Fbm(u, v, 4, 5, 411);
                float pebbles = Mathf.SmoothStep(0.72f, 0.8f, Value(u * 120f, v * 120f, 120, 413));
                float fine = Value(u * 500f, v * 500f, 500, 415);
                var col = Color.Lerp(new Color(0.24f, 0.19f, 0.14f), new Color(0.38f, 0.31f, 0.23f), base_) * (0.9f + 0.2f * fine);
                col = Color.Lerp(col, new Color(0.45f, 0.43f, 0.4f), pebbles * 0.8f);
                return (col, base_ * 0.4f + pebbles * 0.5f + fine * 0.1f, 0f, 0.15f + pebbles * 0.15f, Mathf.Lerp(0.7f, 1f, base_));
            });
            return c;
        }

        /// <summary>Weathered grey rock with cracks and lichen (tile ~8 m).</summary>
        private static Canvas GroundRock(int n)
        {
            var c = new Canvas(n);
            Fill(c, (u, v) =>
            {
                float mass = Fbm(u, v, 3, 6, 421);
                float cracks = Mathf.Pow(Ridged(u, v, 5, 4, 423), 8f);
                float lichen = Mathf.SmoothStep(0.62f, 0.72f, Fbm(u, v, 8, 3, 425));
                var col = Color.Lerp(new Color(0.27f, 0.26f, 0.25f), new Color(0.47f, 0.45f, 0.42f), mass);
                col = Color.Lerp(col, new Color(0.33f, 0.34f, 0.22f), lichen * 0.6f) * (1f - cracks * 0.6f);
                return (col, mass - cracks * 0.6f, 0f, 0.25f + mass * 0.15f, 1f - cracks * 0.5f);
            });
            return c;
        }

        /// <summary>Road gravel: packed grey-brown stones (tile ~4 m).</summary>
        private static Canvas GroundGravel(int n)
        {
            var c = new Canvas(n);
            Fill(c, (u, v) =>
            {
                float stones = Value(u * 160f, v * 160f, 160, 431);
                float big = Mathf.SmoothStep(0.55f, 0.7f, Value(u * 60f, v * 60f, 60, 433));
                float base_ = Fbm(u, v, 4, 4, 435);
                var col = Color.Lerp(new Color(0.3f, 0.28f, 0.25f), new Color(0.5f, 0.47f, 0.42f), stones * 0.7f + base_ * 0.3f);
                col *= 1f - big * 0.25f;
                return (col, stones * 0.6f + big * 0.4f, 0f, 0.2f + stones * 0.1f, Mathf.Lerp(0.6f, 1f, stones));
            });
            return c;
        }

        /// <summary>Alien-infected ground: dark violet crust with raised veins (tile ~6 m).</summary>
        private static Canvas GroundInfected(int n)
        {
            var c = new Canvas(n);
            Fill(c, (u, v) =>
            {
                float crust = Fbm(u, v, 4, 5, 441);
                float veins = Mathf.Pow(Ridged(u, v, 3, 4, 443), 9f);
                var col = Color.Lerp(new Color(0.06f, 0.035f, 0.07f), new Color(0.16f, 0.07f, 0.17f), crust);
                col += new Color(0.18f, 0.06f, 0.28f) * veins;
                return (col, crust * 0.5f + veins * 0.6f, 0f, 0.35f + veins * 0.45f, 1f);
            });
            return c;
        }

        /// <summary>Worn asphalt with aggregate, patches and cracks (tile ~6 m).</summary>
        private static Canvas Asphalt(int n)
        {
            var c = new Canvas(n);
            Fill(c, (u, v) =>
            {
                float agg = Value(u * 420f, v * 420f, 420, 451);
                float patch = Mathf.SmoothStep(0.6f, 0.7f, Fbm(u, v, 3, 4, 453));
                float cracks = Mathf.Pow(Ridged(u, v, 6, 3, 455), 14f);
                float tone = 0.15f + agg * 0.07f - patch * 0.04f - cracks * 0.08f;
                return (new Color(tone, tone, tone * 1.02f), 0.6f + agg * 0.2f - cracks * 0.5f, 0f, 0.18f + patch * 0.1f, 1f - cracks * 0.5f);
            });
            return c;
        }

        /// <summary>Exterior stucco with dirt streaks running down (tile ~3 m).</summary>
        private static Canvas Plaster(int n)
        {
            var c = new Canvas(n);
            Fill(c, (u, v) =>
            {
                float grain = Value(u * 300f, v * 300f, 300, 461);
                float blotch = Fbm(u, v, 3, 4, 463);
                float streak = Mathf.SmoothStep(0.55f, 0.85f, Fbm(u * 1f, v * 0.08f, 12, 3, 465)) * (1f - v) * 1.2f;
                float tone = 0.62f + (blotch - 0.5f) * 0.18f + (grain - 0.5f) * 0.06f - streak * 0.22f;
                return (new Color(tone, tone * 0.97f, tone * 0.92f), grain * 0.3f + blotch * 0.2f, 0f, 0.1f + blotch * 0.08f, 1f);
            });
            return c;
        }

        /// <summary>Burnt bark with vertical fissures (tile ~1 m).</summary>
        private static Canvas Bark(int n)
        {
            var c = new Canvas(n);
            Fill(c, (u, v) =>
            {
                float fiss = Mathf.Pow(Ridged(u * 1f, v * 0.25f, 10, 3, 471), 4f);
                float tone = 0.08f + 0.1f * Fbm(u, v, 6, 3, 473) - fiss * 0.05f;
                return (new Color(tone * 1.1f, tone, tone * 0.9f), 1f - fiss, 0f, 0.12f, 1f - fiss * 0.4f);
            });
            return c;
        }

        /// <summary>Tileable 3D noise used as a density mask for local volumetric smoke.</summary>
        public static Texture3D SmokeNoise3D(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture3D>(path);
            if (existing != null) return existing;
            const int n = 32;
            var tex = new Texture3D(n, n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color[n * n * n];
            for (int z = 0; z < n; z++)
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float a = Fbm((float)x / n, (float)y / n, 4, 3, 200 + z) * 0.5f + Fbm((float)x / n, (float)z / n, 4, 3, 300 + y) * 0.5f;
                a = Mathf.SmoothStep(0.3f, 0.75f, a);
                px[x + y * n + z * n * n] = new Color(a, a, a, a);
            }
            tex.SetPixels(px);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }
    }
}
