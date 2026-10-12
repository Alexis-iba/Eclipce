using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace EclipseGenesis.EditorTools.Facility
{
    /// <summary>HDRP Lit / Unlit / Decal materials for the research facility, built on the procedural texture sets.</summary>
    public sealed class FacilityMaterials
    {
        public const string Folder = "Assets/_EclipseGenesis/Materials/Facility";

        // Palette: graphite black, metallic grey, steel, dark red, tech blue.
        public static readonly Color Graphite = new Color(0.11f, 0.115f, 0.125f);
        public static readonly Color MetalGrey = new Color(0.36f, 0.37f, 0.39f);
        public static readonly Color Steel = new Color(0.55f, 0.57f, 0.6f);
        public static readonly Color DarkRed = new Color(0.38f, 0.04f, 0.035f);
        public static readonly Color TechBlue = new Color(0.12f, 0.55f, 1f);

        public readonly Material WallUpper, WallLower, Ceiling, FloorConcrete, FloorPolished, FloorPlate, FloorDark,
            Concrete, SteelTrim, Graphite_, Pipe, PipeRed, Grating, Hazard, Crate, ContainerRed, Rubber,
            ScreenBlue, ScreenRed, ServerLeds, LightPanel, LightPanelWarm, StripBlue, StripRed, BeaconRed,
            Liquid, LiquidDim, Alien, SparkParticle, DecalGrime, DecalScorch, DecalAlienFluid, DoorPanel, StatusBlue, StatusRed;

        public FacilityMaterials()
        {
            var metal = ProceduralTextures.Get("MetalPanel");
            var concrete = ProceduralTextures.Get("Concrete");
            var plate = ProceduralTextures.Get("DiamondPlate");
            var grating = ProceduralTextures.Get("Grating");
            var hazard = ProceduralTextures.Get("Hazard");

            WallUpper = Lit("M_Wall_PanelSteel", metal, MetalGrey * 1.25f, 2f, smooth: (0.25f, 0.75f), metallicMax: 0.15f);
            WallLower = Lit("M_Wall_PanelGraphite", metal, Graphite * 1.3f, 2f, smooth: (0.2f, 0.6f), metallicMax: 0.15f);
            Ceiling = Lit("M_Ceiling_Panel", metal, Graphite, 2f, smooth: (0.1f, 0.5f), metallicMax: 0.15f);
            DoorPanel = Lit("M_Door_Panel", metal, new Color(0.24f, 0.25f, 0.27f), 1.2f, smooth: (0.3f, 0.8f), metallicMax: 0.15f);
            FloorConcrete = Lit("M_Floor_Concrete", concrete, new Color(0.42f, 0.42f, 0.42f), 4f, smooth: (0.05f, 0.6f));
            FloorPolished = Lit("M_Floor_ConcretePolished", concrete, new Color(0.33f, 0.335f, 0.34f), 4f, smooth: (0.35f, 0.9f));
            FloorDark = Lit("M_Floor_ConcreteDark", concrete, new Color(0.2f, 0.2f, 0.21f), 4f, smooth: (0.2f, 0.75f));
            Concrete = Lit("M_Concrete_Wall", concrete, new Color(0.45f, 0.45f, 0.46f), 3f, smooth: (0f, 0.45f));
            FloorPlate = Lit("M_Floor_TreadPlate", plate, Steel * 0.75f, 1f, smooth: (0.2f, 0.85f));
            SteelTrim = Lit("M_Steel_Trim", plate, Steel, 0.6f, smooth: (0.4f, 0.8f));
            Graphite_ = Lit("M_Graphite_Metal", metal, Graphite * 0.9f, 1f, smooth: (0.3f, 0.7f), metallicMax: 0.15f);
            Pipe = Lit("M_Pipe_Steel", metal, Steel * 0.8f, 1f, smooth: (0.35f, 0.85f));
            PipeRed = Lit("M_Pipe_Red", metal, DarkRed * 1.4f, 1f, smooth: (0.3f, 0.7f));
            Crate = Lit("M_Crate_Military", metal, new Color(0.17f, 0.19f, 0.17f), 1f, smooth: (0.15f, 0.55f), metallicMax: 0.15f);
            ContainerRed = Lit("M_Container_Red", metal, DarkRed * 1.2f, 2.4f, smooth: (0.1f, 0.5f), metallicMax: 0.15f);
            Rubber = Lit("M_Rubber_Black", concrete, new Color(0.04f, 0.04f, 0.045f), 1f, smooth: (0.1f, 0.3f));
            Hazard = Lit("M_Hazard_Stripes", hazard, Color.white, 1f, smooth: (0.1f, 0.5f));

            Grating = Lit("M_Grating", grating, Steel * 0.8f, 0.5f, smooth: (0.3f, 0.8f));
            Grating.SetFloat("_AlphaCutoffEnable", 1f);
            Grating.SetFloat("_AlphaCutoff", 0.5f);
            Grating.SetFloat("_DoubleSidedEnable", 1f);
            Finish(Grating);

            var screenBlue = ProceduralTextures.Get("ScreenBlue");
            var screenRed = ProceduralTextures.Get("ScreenRed");
            ScreenBlue = Emissive("M_Screen_Blue", screenBlue, Color.white, 1f, 70f);
            ScreenRed = Emissive("M_Screen_RedAlert", screenRed, Color.white, 1f, 70f);
            ServerLeds = Emissive("M_ServerRack_Leds", ProceduralTextures.Get("ServerLeds"), Color.white, 2.2f, 120f);

            LightPanel = Glow("M_LightPanel_Cool", new Color(0.85f, 0.92f, 1f), 450f);
            LightPanelWarm = Glow("M_LightPanel_Warm", new Color(1f, 0.85f, 0.7f), 380f);
            StripBlue = Glow("M_Strip_Blue", TechBlue, 90f);
            StripRed = Glow("M_Strip_Red", new Color(1f, 0.08f, 0.05f), 80f);
            BeaconRed = Glow("M_Beacon_Red", new Color(1f, 0.05f, 0.03f), 450f);
            StatusBlue = Glow("M_Door_StatusBlue", TechBlue, 350f);
            StatusRed = Glow("M_Door_StatusRed", new Color(1f, 0.06f, 0.04f), 350f);
            Liquid = Glow("M_Tank_Liquid", new Color(0.15f, 0.75f, 1f), 90f, smoothness: 0.95f);
            LiquidDim = Glow("M_Tank_LiquidSpecimen", new Color(0.2f, 0.95f, 0.75f), 30f, smoothness: 0.95f);

            var flesh = ProceduralTextures.Get("AlienFlesh");
            Alien = Emissive("M_Alien_Biomass", flesh, Color.white, 1.5f, 40f, smooth: (0.3f, 0.95f));

            SparkParticle = UnlitAdditive("M_Particle_Spark", new Color(1f, 0.55f, 0.15f), 3000f);

            DecalGrime = Decal("M_Decal_Grime", ProceduralTextures.Get("DecalGrime"));
            DecalScorch = Decal("M_Decal_Scorch", ProceduralTextures.Get("DecalScorch"));
            DecalAlienFluid = Decal("M_Decal_AlienFluid", ProceduralTextures.Get("DecalAlienFluid"));
        }

        // ------------------------------------------------------------------ factories

        private static Material LoadOrCreate(string name, string shader)
        {
            string path = $"{Folder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                FpsTestSceneBuilder.EnsureFolder(Folder);
                mat = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader.name != shader)
            {
                mat.shader = Shader.Find(shader);
            }
            return mat;
        }

        /// <param name="tileMeters">World size covered by one texture repeat (meshes carry UVs in metres).</param>
        private static Material Lit(string name, ProceduralTextures.TextureSet set, Color tint, float tileMeters, (float min, float max) smooth, float metallicMax = 1f)
        {
            var mat = LoadOrCreate(name, "HDRP/Lit");
            mat.SetColor("_BaseColor", tint);
            mat.SetTexture("_BaseColorMap", set.Albedo);
            mat.SetTexture("_NormalMap", set.Normal);
            mat.SetFloat("_NormalScale", 1f);
            mat.SetTexture("_MaskMap", set.Mask);
            mat.SetFloat("_MetallicRemapMin", 0f);
            mat.SetFloat("_MetallicRemapMax", metallicMax);
            mat.SetFloat("_SmoothnessRemapMin", smooth.min);
            mat.SetFloat("_SmoothnessRemapMax", smooth.max);
            mat.SetFloat("_AORemapMin", 0f);
            mat.SetFloat("_AORemapMax", 1f);
            mat.SetTextureScale("_BaseColorMap", Vector2.one / tileMeters);
            Finish(mat);
            return mat;
        }

        private static Material Emissive(string name, ProceduralTextures.TextureSet set, Color tint, float tileMeters, float nits, (float min, float max)? smooth = null)
        {
            var mat = Lit(name, set, tint, tileMeters, smooth ?? (0.6f, 0.95f));
            mat.SetTexture("_EmissiveColorMap", set.Emissive);
            HDMaterial.SetUseEmissiveIntensity(mat, true);
            HDMaterial.SetEmissiveColor(mat, Color.white);
            HDMaterial.SetEmissiveIntensity(mat, nits, EmissiveIntensityUnit.Nits);
            Finish(mat);
            return mat;
        }

        private static Material Glow(string name, Color color, float nits, float smoothness = 0.6f)
        {
            var mat = LoadOrCreate(name, "HDRP/Lit");
            mat.SetColor("_BaseColor", color * 0.25f);
            mat.SetTexture("_BaseColorMap", null);
            mat.SetTexture("_MaskMap", null);
            mat.SetTexture("_NormalMap", null);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            HDMaterial.SetUseEmissiveIntensity(mat, true);
            HDMaterial.SetEmissiveColor(mat, color);
            HDMaterial.SetEmissiveIntensity(mat, nits, EmissiveIntensityUnit.Nits);
            Finish(mat);
            return mat;
        }

        private static Material UnlitAdditive(string name, Color color, float nits)
        {
            var mat = LoadOrCreate(name, "HDRP/Unlit");
            mat.SetColor("_UnlitColor", color);
            mat.SetFloat("_SurfaceType", 1f);   // transparent
            mat.SetFloat("_BlendMode", 1f);     // additive
            HDMaterial.SetUseEmissiveIntensity(mat, true);
            HDMaterial.SetEmissiveColor(mat, color);
            HDMaterial.SetEmissiveIntensity(mat, nits, EmissiveIntensityUnit.Nits);
            Finish(mat);
            return mat;
        }

        private static Material Decal(string name, ProceduralTextures.TextureSet set)
        {
            var mat = LoadOrCreate(name, "HDRP/Decal");
            mat.SetTexture("_BaseColorMap", set.Albedo);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_NormalMap", set.Normal);
            mat.SetFloat("_AffectAlbedo", 1f);
            mat.SetFloat("_AffectNormal", 1f);
            // No mask map: metal/smoothness would otherwise apply to the whole projector box (mask blend source = white).
            mat.SetFloat("_AffectMetal", 0f);
            mat.SetFloat("_AffectSmoothness", 0f);
            mat.SetFloat("_AffectAO", 0f);
            mat.SetFloat("_NormalBlendSrc", 0f);
            mat.SetFloat("_MaskBlendSrc", 0f);
            mat.SetFloat("_DecalBlend", 1f);
            mat.enableInstancing = true;
            Finish(mat);
            return mat;
        }

        private static void Finish(Material mat)
        {
            HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);
        }
    }
}
