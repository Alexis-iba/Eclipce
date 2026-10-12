using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace EclipseGenesis.EditorTools.Art
{
    /// <summary>
    /// Turns the CC0 Poly Haven downloads under Art/Imported into HDRP-ready assets:
    ///  - texture import rules (normal maps, linear data maps, cubemap HDRIs);
    ///  - packs each surface's AO/roughness/metal (arm or separate maps) into an HDRP mask map
    ///    (R metal, G AO, B detail mask, A smoothness) and merges alpha cut-outs into the base map;
    ///  - creates HDRP/Lit materials (M_PH_*) and TerrainLayers (TL_PH_*) for the texture library;
    ///  - remaps every Poly Haven FBX material to an HDRP material built from its own textures.
    /// Source licence: CC0 (polyhaven.com). See Docs/ASSETS_REQUIRED.md.
    /// </summary>
    public sealed class PolyHavenLibrary : AssetPostprocessor
    {
        public const string Root = ImportedModelConventions.Root;
        public const string MaterialFolder = "Assets/_EclipseGenesis/Materials/PolyHaven";
        private static readonly Regex MapName = new Regex(@"^(?<surface>.+?)_(?<map>diff|nor_gl|arm|rough|metal|alpha|ao|disp|mask|basealpha)(_\dk)?$", RegexOptions.IgnoreCase);

        /// <summary>World metres covered by one repeat of each library texture.</summary>
        public static readonly Dictionary<string, float> TileMeters = new Dictionary<string, float>
        {
            { "sparse_grass", 3f }, { "dry_mud_field_001", 4f }, { "rocky_terrain_02", 5f }, { "rocky_trail", 4f },
            { "asphalt_02", 4f }, { "weathered_plank_siding", 2.5f }, { "wood_floor_worn", 2f }, { "white_plaster_02", 2f },
            { "roof_09", 3f }, { "corrugated_iron_02", 2f }, { "cracked_concrete_wall", 3f }, { "concrete_floor_worn_001", 3f },
            { "metal_plate", 2f }, { "rust_coarse_01", 2f },
        };

        private static readonly string[] TerrainSurfaces = { "sparse_grass", "dry_mud_field_001", "rocky_terrain_02", "rocky_trail", "asphalt_02" };

        // ------------------------------------------------------------------ import rules

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (TextureImporter)assetImporter;
            string file = Path.GetFileNameWithoutExtension(assetPath);
            if (assetPath.EndsWith(".hdr") || assetPath.EndsWith(".exr"))
            {
                importer.textureShape = TextureImporterShape.TextureCube;
                importer.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
                importer.sRGBTexture = false;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }
            var m = MapName.Match(file);
            if (!m.Success) return;
            string map = m.Groups["map"].Value.ToLowerInvariant();
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            switch (map)
            {
                case "nor_gl":
                    importer.textureType = TextureImporterType.NormalMap;
                    break;
                case "diff":
                case "basealpha":
                    importer.sRGBTexture = true;
                    importer.alphaIsTransparency = map == "basealpha";
                    break;
                default: // data maps (arm, rough, metal, ao, alpha, disp, mask)
                    importer.sRGBTexture = false;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    break;
            }
        }

        // ------------------------------------------------------------------ build

        [MenuItem("Eclipse Genesis/Art/Build Poly Haven Materials")]
        public static void BuildAll()
        {
            var report = new List<string>();
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var dir in AllSurfaceFolders()) PackSurfaces(dir);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            FpsTestSceneBuilder.EnsureFolder(MaterialFolder);
            foreach (string id in TileMeters.Keys)
            {
                var mat = LibraryMaterial(id);
                report.Add(mat != null ? $"material {mat.name}" : $"MISSING textures for {id}");
            }
            foreach (string id in TerrainSurfaces)
            {
                var layer = TerrainLayerFor(id);
                if (layer != null) report.Add($"terrain layer {layer.name}");
            }
            foreach (string fbx in AssetDatabase.FindAssets("t:Model", new[] { Root.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath))
                report.Add(fbx.Contains("/PolyHaven/") ? RemapModel(fbx) : RemapBlenderExport(fbx));
            AssetDatabase.SaveAssets();
            Debug.Log("[EclipseGenesis] Poly Haven library built:\n" + string.Join("\n", report));
        }

        private static IEnumerable<string> AllSurfaceFolders()
        {
            var dirs = new HashSet<string>();
            foreach (string f in Directory.GetFiles(Root, "*_diff*.*", SearchOption.AllDirectories))
                if (!f.EndsWith(".meta")) dirs.Add(Path.GetDirectoryName(f).Replace('\\', '/'));
            return dirs;
        }

        /// <summary>Surfaces in a folder, keyed by name, each with its maps (diff, nor_gl, arm, rough, metal, alpha, ao, mask, basealpha).</summary>
        private static Dictionary<string, Dictionary<string, string>> Surfaces(string dir)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();
            foreach (string f in Directory.GetFiles(dir))
            {
                if (f.EndsWith(".meta")) continue;
                var m = MapName.Match(Path.GetFileNameWithoutExtension(f));
                if (!m.Success) continue;
                string surface = m.Groups["surface"].Value;
                if (!result.TryGetValue(surface, out var maps)) result[surface] = maps = new Dictionary<string, string>();
                maps[m.Groups["map"].Value.ToLowerInvariant()] = f.Replace('\\', '/');
            }
            return result;
        }

        private static void PackSurfaces(string dir)
        {
            foreach (var kv in Surfaces(dir))
            {
                var maps = kv.Value;
                if (!maps.ContainsKey("mask"))
                {
                    var mask = BuildMask(maps);
                    if (mask != null) File.WriteAllBytes($"{dir}/{kv.Key}_mask.png", mask);
                }
                if (maps.ContainsKey("alpha") && maps.ContainsKey("diff") && !maps.ContainsKey("basealpha"))
                    File.WriteAllBytes($"{dir}/{kv.Key}_basealpha.png", MergeAlpha(maps["diff"], maps["alpha"]));
            }
        }

        private static Texture2D Load(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!path.EndsWith(".exr", System.StringComparison.OrdinalIgnoreCase))
            {
                tex.LoadImage(File.ReadAllBytes(path));
                return tex;
            }
            // EXR data maps (some Poly Haven models): read the imported texture back through a linear render target.
            var src = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (src == null) return tex;
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            tex.Reinitialize(src.width, src.height);
            tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        private static byte[] BuildMask(Dictionary<string, string> maps)
        {
            Color32[] arm = null, rough = null, metal = null, ao = null;
            int w = 0, h = 0;
            void Read(string key, ref Color32[] px)
            {
                if (!maps.TryGetValue(key, out string p)) return;
                var t = Load(p);
                if (w == 0) { w = t.width; h = t.height; }
                if (t.width == w && t.height == h) px = t.GetPixels32();
                Object.DestroyImmediate(t);
            }
            Read("arm", ref arm);
            Read("rough", ref rough);
            Read("metal", ref metal);
            Read("ao", ref ao);
            if (arm == null && rough == null) return null;

            var outPx = new Color32[w * h];
            for (int i = 0; i < outPx.Length; i++)
            {
                byte r = arm != null ? arm[i].g : rough[i].r;
                byte m = arm != null ? arm[i].b : metal != null ? metal[i].r : (byte)0;
                byte o = arm != null ? arm[i].r : ao != null ? ao[i].r : (byte)255;
                outPx[i] = new Color32(m, o, 255, (byte)(255 - r));
            }
            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            outTex.SetPixels32(outPx);
            byte[] png = outTex.EncodeToPNG();
            Object.DestroyImmediate(outTex);
            return png;
        }

        private static byte[] MergeAlpha(string diffPath, string alphaPath)
        {
            var diff = Load(diffPath);
            var alpha = Load(alphaPath);
            var px = diff.GetPixels32();
            if (alpha.width == diff.width && alpha.height == diff.height)
            {
                var a = alpha.GetPixels32();
                for (int i = 0; i < px.Length; i++) px[i].a = a[i].r;
            }
            var outTex = new Texture2D(diff.width, diff.height, TextureFormat.RGBA32, false);
            outTex.SetPixels32(px);
            byte[] png = outTex.EncodeToPNG();
            Object.DestroyImmediate(diff);
            Object.DestroyImmediate(alpha);
            Object.DestroyImmediate(outTex);
            return png;
        }

        // ------------------------------------------------------------------ materials

        public static Material LibraryMaterial(string id)
        {
            var maps = Surfaces($"{Root}Textures/{id}").TryGetValue(id, out var m) ? m : null;
            if (maps == null || !maps.ContainsKey("diff")) return null;
            var mat = SurfaceMaterial($"M_PH_{id}", maps, cutout: false);
            float tile = TileMeters.TryGetValue(id, out float t) ? t : 2.5f;
            mat.SetTextureScale("_BaseColorMap", Vector2.one / tile);
            HDMaterial.ValidateMaterial(mat);
            return mat;
        }

        private static Material SurfaceMaterial(string name, Dictionary<string, string> maps, bool cutout)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("HDRP/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            Texture2D Tex(string key) => maps.TryGetValue(key, out string p) ? AssetDatabase.LoadAssetAtPath<Texture2D>(p) : null;
            bool alpha = cutout && maps.ContainsKey("basealpha");
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseColorMap", alpha ? Tex("basealpha") : Tex("diff"));
            mat.SetTexture("_NormalMap", Tex("nor_gl"));
            mat.SetFloat("_NormalScale", 1f);
            var mask = Tex("mask");
            mat.SetTexture("_MaskMap", mask);
            mat.SetFloat("_MetallicRemapMin", 0f);
            mat.SetFloat("_MetallicRemapMax", 1f);
            mat.SetFloat("_SmoothnessRemapMin", 0f);
            mat.SetFloat("_SmoothnessRemapMax", 1f);
            mat.SetFloat("_AORemapMin", 0f);
            mat.SetFloat("_AORemapMax", 1f);
            if (mask == null) { mat.SetFloat("_Smoothness", 0.3f); mat.SetFloat("_Metallic", 0f); }
            mat.SetFloat("_AlphaCutoffEnable", alpha ? 1f : 0f);
            mat.SetFloat("_AlphaCutoff", 0.45f);
            mat.SetFloat("_DoubleSidedEnable", alpha ? 1f : 0f);
            mat.doubleSidedGI = alpha;
            mat.enableInstancing = true;
            HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static TerrainLayer TerrainLayerFor(string id)
        {
            var maps = Surfaces($"{Root}Textures/{id}").TryGetValue(id, out var m) ? m : null;
            if (maps == null) return null;
            string path = $"{MaterialFolder}/TL_PH_{id}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(maps["diff"]);
            layer.normalMapTexture = maps.TryGetValue("nor_gl", out string n) ? AssetDatabase.LoadAssetAtPath<Texture2D>(n) : null;
            layer.maskMapTexture = maps.TryGetValue("mask", out string k) ? AssetDatabase.LoadAssetAtPath<Texture2D>(k) : null;
            layer.normalScale = 1f;
            float tile = TileMeters.TryGetValue(id, out float t) ? t : 3f;
            layer.tileSize = new Vector2(tile, tile);
            layer.maskMapRemapMin = Vector4.zero;
            layer.maskMapRemapMax = Vector4.one;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        /// <summary>Remaps the FBX's embedded materials to HDRP materials built from the textures beside it.</summary>
        private static string RemapModel(string fbxPath)
        {
            string dir = Path.GetDirectoryName(fbxPath).Replace('\\', '/');
            var surfaces = new Dictionary<string, Dictionary<string, string>>();
            foreach (string sub in new[] { dir, dir + "/textures" })
                if (Directory.Exists(sub))
                    foreach (var kv in Surfaces(sub)) surfaces[kv.Key] = kv.Value;
            if (surfaces.Count == 0) return $"{fbxPath}: no textures";

            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            var sourceNames = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Renderer>()
                .SelectMany(r => r.sharedMaterials).Where(x => x != null).Select(x => x.name).Distinct().ToList();
            foreach (var pair in importer.GetExternalObjectMap())
                if (pair.Key.type == typeof(Material) && !sourceNames.Contains(pair.Key.name)) sourceNames.Add(pair.Key.name);

            string model = Path.GetFileNameWithoutExtension(fbxPath);
            var log = new List<string>();
            foreach (string src in sourceNames)
            {
                string key = Match(src, model, surfaces.Keys.ToList());
                if (key == null) { log.Add($"{src}=?"); continue; }
                bool cutout = surfaces[key].ContainsKey("basealpha");
                var mat = SurfaceMaterial($"M_PH_{key}", surfaces[key], cutout);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src), mat);
                log.Add($"{src}->{mat.name}");
            }
            importer.SaveAndReimport();
            return $"{model}: {string.Join(", ", log)}";
        }

        /// <summary>Material names used by the Blender generators (ArtSource/Blender) and the library surface each one uses.</summary>
        public static readonly Dictionary<string, string> BlenderMaterials = new Dictionary<string, string>
        {
            { "EG_Siding", "weathered_plank_siding" }, { "EG_Trim", "white_plaster_02" }, { "EG_Roof", "roof_09" },
            { "EG_Concrete", "concrete_floor_worn_001" }, { "EG_WoodFloor", "wood_floor_worn" }, { "EG_Plaster", "white_plaster_02" },
            { "EG_Metal", "metal_plate" }, { "EG_Rust", "rust_coarse_01" }, { "EG_Corrugated", "corrugated_iron_02" },
            { "EG_Asphalt", "asphalt_02" }, { "EG_ConcreteWall", "cracked_concrete_wall" },
        };

        /// <summary>Remaps EG_* materials of FBX files exported by our Blender generators to the HDRP library materials.</summary>
        private static string RemapBlenderExport(string fbxPath)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            var names = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Renderer>()
                .SelectMany(r => r.sharedMaterials).Where(x => x != null).Select(x => x.name)
                .Concat(importer.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).Select(k => k.name))
                .Distinct().ToList();
            var log = new List<string>();
            foreach (string src in names)
            {
                Material mat;
                if (src.StartsWith("EG_PH_"))   // optimised copies of Poly Haven scans keep their surface material
                    mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_PH_{src.Substring(6)}.mat");
                else if (BlenderMaterials.TryGetValue(src, out string id))
                    mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_PH_{id}.mat") ?? LibraryMaterial(id);
                else continue;
                if (mat == null) { log.Add($"{src}=?"); continue; }
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src), mat);
                log.Add($"{src}->{mat.name}");
            }
            if (log.Count == 0) return $"{Path.GetFileName(fbxPath)}: no EG_* materials";
            importer.SaveAndReimport();
            return $"{Path.GetFileNameWithoutExtension(fbxPath)}: {string.Join(", ", log)}";
        }

        private static string Match(string materialName, string model, List<string> surfaces)
        {
            string n = materialName.ToLowerInvariant();
            var exact = surfaces.FirstOrDefault(s => s.ToLowerInvariant() == n);
            if (exact != null) return exact;
            var contains = surfaces.Where(s => s.ToLowerInvariant().EndsWith(n) || n.EndsWith(s.ToLowerInvariant()) || s.ToLowerInvariant().Contains(n))
                .OrderBy(s => s.Length).FirstOrDefault();
            if (contains != null) return contains;
            if (n.Contains("trunk") || n.Contains("branch"))
            {
                var bark = surfaces.FirstOrDefault(s => s.ToLowerInvariant().Contains("bark"));
                if (bark != null) return bark;
            }
            return surfaces.Count == 1 ? surfaces[0] : surfaces.FirstOrDefault(s => s.ToLowerInvariant() == model.ToLowerInvariant());
        }
    }
}
