using System.IO;
using EclipseGenesis.EditorTools.Facility;
using EclipseGenesis.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace EclipseGenesis.EditorTools.Combat
{
    /// <summary>
    /// Builds the provisional EG-47 viewmodel (multi-part rifle + gloved arms), its WeaponData and effect materials.
    /// The model is a stand-in for an authored FBX (Docs/ASSETS_REQUIRED.md); a replacement only needs a WeaponModel
    /// component with the same sockets.
    /// </summary>
    public static class WeaponBuilder
    {
        public const string PrefabPath = "Assets/_EclipseGenesis/Prefabs/Weapons/EG_Weapon_EG47.prefab";
        public const string DataPath = "Assets/_EclipseGenesis/Data/Weapons/EG47.asset";

        public sealed class Assets
        {
            public WeaponData Data;
            public Material Tracer, Spark, AlienBlood, Casing;
            public Mesh CasingMesh;
        }

        public static Assets Build()
        {
            var gunmetal = ProtoKit.Lit("M_EG47_Gunmetal", new Color(0.09f, 0.095f, 0.1f), 0.85f, 0.55f);
            var polymer = ProtoKit.Lit("M_EG47_Polymer", new Color(0.075f, 0.08f, 0.085f), 0f, 0.35f);
            var trim = ProtoKit.Lit("M_EG47_SteelTrim", new Color(0.45f, 0.47f, 0.5f), 0.9f, 0.6f);
            var accentRed = ProtoKit.Lit("M_EG47_AccentRed", new Color(0.35f, 0.03f, 0.03f), 0.2f, 0.45f);
            var blue = ProtoKit.Glow("M_EG47_BlueLight", new Color(0.15f, 0.6f, 1f), 160f);
            var reticle = ProtoKit.Glow("M_EG47_Reticle", new Color(1f, 0.15f, 0.1f), 900f);
            var lens = ProtoKit.Lit("M_EG47_Lens", new Color(0.05f, 0.12f, 0.12f), 0f, 0.95f);
            var glove = ProtoKit.Lit("M_Arms_Glove", new Color(0.05f, 0.05f, 0.052f), 0f, 0.3f);
            var sleeve = ProtoKit.Lit("M_Arms_Sleeve", new Color(0.12f, 0.13f, 0.12f), 0f, 0.2f);
            var flashMat = ProtoKit.UnlitAdditive("M_FX_MuzzleFlash", new Color(1f, 0.62f, 0.25f), 6000f);

            var assets = new Assets
            {
                Tracer = ProtoKit.UnlitAdditive("M_FX_Tracer", new Color(1f, 0.8f, 0.45f), 2500f),
                Spark = ProtoKit.UnlitAdditive("M_FX_ImpactSpark", new Color(1f, 0.6f, 0.2f), 3000f),
                AlienBlood = ProtoKit.UnlitAdditive("M_FX_AlienBlood", new Color(0.2f, 1f, 0.85f), 600f),
                Casing = ProtoKit.Lit("M_FX_Casing", new Color(0.75f, 0.55f, 0.25f), 1f, 0.75f),
                CasingMesh = Resources.GetBuiltinResource<Mesh>("New-Capsule.fbx"),
            };

            var root = new GameObject("EG_Weapon_EG47");
            try
            {
                var model = root.AddComponent<WeaponModel>();
                var t = root.transform;

                // Receiver group (origin near the grip).
                ProtoKit.Part(t, "Receiver", PrimitiveType.Cube, new Vector3(0f, 0f, 0.05f), new Vector3(0.058f, 0.085f, 0.32f), gunmetal, castShadows: false);
                ProtoKit.Part(t, "ReceiverSide_L", PrimitiveType.Cube, new Vector3(-0.0295f, 0.005f, 0.05f), new Vector3(0.003f, 0.05f, 0.24f), polymer, castShadows: false);
                ProtoKit.Part(t, "ReceiverSide_R", PrimitiveType.Cube, new Vector3(0.0295f, 0.005f, 0.05f), new Vector3(0.003f, 0.05f, 0.24f), polymer, castShadows: false);
                ProtoKit.Part(t, "TopRail", PrimitiveType.Cube, new Vector3(0f, 0.049f, 0.08f), new Vector3(0.026f, 0.012f, 0.36f), trim, castShadows: false);
                for (int i = 0; i < 12; i++)
                    ProtoKit.Part(t, "RailTooth", PrimitiveType.Cube, new Vector3(0f, 0.057f, -0.08f + i * 0.028f), new Vector3(0.026f, 0.005f, 0.012f), trim, castShadows: false);
                ProtoKit.Part(t, "ChargingHandle", PrimitiveType.Cube, new Vector3(-0.036f, 0.02f, 0.14f), new Vector3(0.018f, 0.012f, 0.03f), accentRed, castShadows: false);
                ProtoKit.Part(t, "EjectionPortCover", PrimitiveType.Cube, new Vector3(0.0305f, 0.018f, 0.06f), new Vector3(0.002f, 0.022f, 0.06f), trim, castShadows: false);

                // Handguard with tech lighting.
                ProtoKit.Part(t, "Handguard", PrimitiveType.Cube, new Vector3(0f, -0.005f, 0.34f), new Vector3(0.064f, 0.07f, 0.26f), polymer, castShadows: false);
                ProtoKit.Part(t, "HandguardTop", PrimitiveType.Cube, new Vector3(0f, 0.033f, 0.34f), new Vector3(0.05f, 0.008f, 0.26f), gunmetal, castShadows: false);
                foreach (float x in new[] { -0.0325f, 0.0325f })
                {
                    ProtoKit.Part(t, "LightStrip", PrimitiveType.Cube, new Vector3(x, -0.004f, 0.34f), new Vector3(0.002f, 0.008f, 0.19f), blue, castShadows: false);
                    for (int i = 0; i < 4; i++)
                        ProtoKit.Part(t, "Vent", PrimitiveType.Cube, new Vector3(x, -0.025f, 0.27f + i * 0.045f), new Vector3(0.002f, 0.012f, 0.028f), gunmetal, castShadows: false);
                }

                // Barrel and muzzle brake.
                ProtoKit.Part(t, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.005f, 0.56f), new Vector3(0.022f, 0.2f, 0.022f), gunmetal, new Vector3(90f, 0f, 0f), false);
                ProtoKit.Part(t, "GasBlock", PrimitiveType.Cube, new Vector3(0f, 0.012f, 0.5f), new Vector3(0.03f, 0.03f, 0.03f), trim, castShadows: false);
                ProtoKit.Part(t, "MuzzleBrake", PrimitiveType.Cylinder, new Vector3(0f, 0.005f, 0.685f), new Vector3(0.036f, 0.07f, 0.036f), gunmetal, new Vector3(90f, 0f, 0f), false);
                foreach (float z in new[] { 0.67f, 0.69f })
                    ProtoKit.Part(t, "BrakePort", PrimitiveType.Cube, new Vector3(0f, 0.005f, z), new Vector3(0.04f, 0.008f, 0.006f), polymer, castShadows: false);

                // Pistol grip, trigger guard, magazine well.
                ProtoKit.Part(t, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.085f, -0.055f), new Vector3(0.034f, 0.11f, 0.045f), polymer, new Vector3(-18f, 0f, 0f), false);
                ProtoKit.Part(t, "TriggerGuard", PrimitiveType.Cube, new Vector3(0f, -0.06f, 0.0f), new Vector3(0.01f, 0.006f, 0.07f), gunmetal, castShadows: false);
                ProtoKit.Part(t, "Trigger", PrimitiveType.Cube, new Vector3(0f, -0.05f, -0.005f), new Vector3(0.006f, 0.02f, 0.006f), trim, new Vector3(15f, 0f, 0f), false);
                ProtoKit.Part(t, "MagWell", PrimitiveType.Cube, new Vector3(0f, -0.052f, 0.115f), new Vector3(0.044f, 0.03f, 0.09f), gunmetal, castShadows: false);

                var mag = ProtoKit.Group(t, "Magazine", new Vector3(0f, -0.07f, 0.12f), new Vector3(-12f, 0f, 0f));
                ProtoKit.Part(mag, "Body", PrimitiveType.Cube, new Vector3(0f, -0.07f, 0f), new Vector3(0.034f, 0.15f, 0.075f), polymer, castShadows: false);
                ProtoKit.Part(mag, "Base", PrimitiveType.Cube, new Vector3(0f, -0.148f, 0f), new Vector3(0.04f, 0.012f, 0.082f), accentRed, castShadows: false);
                ProtoKit.Part(mag, "Window", PrimitiveType.Cube, new Vector3(0.0175f, -0.06f, 0f), new Vector3(0.002f, 0.08f, 0.012f), blue, castShadows: false);

                // Stock.
                ProtoKit.Part(t, "StockTube", PrimitiveType.Cylinder, new Vector3(0f, 0.005f, -0.17f), new Vector3(0.03f, 0.12f, 0.03f), gunmetal, new Vector3(90f, 0f, 0f), false);
                ProtoKit.Part(t, "Stock", PrimitiveType.Cube, new Vector3(0f, -0.015f, -0.27f), new Vector3(0.044f, 0.085f, 0.13f), polymer, castShadows: false);
                ProtoKit.Part(t, "StockStripe", PrimitiveType.Cube, new Vector3(0f, -0.015f, -0.27f), new Vector3(0.046f, 0.012f, 0.1f), accentRed, castShadows: false);
                ProtoKit.Part(t, "ButtPad", PrimitiveType.Cube, new Vector3(0f, -0.018f, -0.34f), new Vector3(0.046f, 0.1f, 0.02f), glove, castShadows: false);

                // Holographic sight.
                var sightGroup = ProtoKit.Group(t, "HoloSight", new Vector3(0f, 0.062f, 0.02f));
                ProtoKit.Part(sightGroup, "Base", PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.036f, 0.012f, 0.075f), gunmetal, castShadows: false);
                ProtoKit.Part(sightGroup, "Hood_L", PrimitiveType.Cube, new Vector3(-0.019f, 0.024f, 0.01f), new Vector3(0.004f, 0.04f, 0.05f), gunmetal, castShadows: false);
                ProtoKit.Part(sightGroup, "Hood_R", PrimitiveType.Cube, new Vector3(0.019f, 0.024f, 0.01f), new Vector3(0.004f, 0.04f, 0.05f), gunmetal, castShadows: false);
                ProtoKit.Part(sightGroup, "Hood_Top", PrimitiveType.Cube, new Vector3(0f, 0.045f, 0.01f), new Vector3(0.042f, 0.004f, 0.05f), gunmetal, castShadows: false);
                ProtoKit.Part(sightGroup, "Lens", PrimitiveType.Cube, new Vector3(0f, 0.024f, 0.03f), new Vector3(0.034f, 0.038f, 0.002f), lens, castShadows: false);
                ProtoKit.Part(sightGroup, "ReticleDot", PrimitiveType.Sphere, new Vector3(0f, 0.026f, 0.029f), new Vector3(0.0025f, 0.0025f, 0.0005f), reticle, castShadows: false);
                var sight = ProtoKit.Group(sightGroup, "Socket_Sight", new Vector3(0f, 0.026f, 0.029f));

                // Sockets.
                var muzzle = ProtoKit.Group(t, "Socket_Muzzle", new Vector3(0f, 0.005f, 0.725f));
                var eject = ProtoKit.Group(t, "Socket_ShellEject", new Vector3(0.034f, 0.02f, 0.06f));

                var flash = ProtoKit.Group(muzzle, "MuzzleFlash");
                ProtoKit.Part(flash, "Petal_A", PrimitiveType.Quad, new Vector3(0f, 0f, 0.04f), new Vector3(0.12f, 0.03f, 1f), flashMat, new Vector3(0f, 90f, 0f), false);
                ProtoKit.Part(flash, "Petal_B", PrimitiveType.Quad, new Vector3(0f, 0f, 0.04f), new Vector3(0.12f, 0.03f, 1f), flashMat, new Vector3(90f, 90f, 0f), false);
                ProtoKit.Part(flash, "Core", PrimitiveType.Quad, Vector3.zero, new Vector3(0.07f, 0.07f, 1f), flashMat, default, false);
                flash.gameObject.SetActive(false);

                var lightGo = new GameObject("MuzzleLight");
                lightGo.transform.SetParent(muzzle, false);
                lightGo.transform.localPosition = new Vector3(0f, 0f, 0.05f);
                lightGo.AddHDLight(LightType.Point);
                var light = lightGo.GetComponent<Light>();
                light.lightUnit = LightUnit.Lumen;
                light.intensity = 2500f;
                light.range = 6f;
                light.color = new Color(1f, 0.7f, 0.4f);
                light.shadows = LightShadows.None;
                light.enabled = false;

                // Provisional gloved arms; the left hand is a group so it can follow the magazine on reload.
                var rightArm = ProtoKit.Group(t, "Arm_R");
                ProtoKit.Part(rightArm, "Hand", PrimitiveType.Cube, new Vector3(0.012f, -0.075f, -0.055f), new Vector3(0.05f, 0.085f, 0.09f), glove, new Vector3(-18f, 0f, 0f), false);
                ProtoKit.Part(rightArm, "Finger", PrimitiveType.Cube, new Vector3(0.0f, -0.045f, 0.0f), new Vector3(0.018f, 0.018f, 0.04f), glove, castShadows: false);
                ProtoKit.Limb(rightArm, "Forearm", new Vector3(0.03f, -0.1f, -0.1f), new Vector3(0.12f, -0.22f, -0.42f), 0.038f, sleeve, castShadows: false);
                ProtoKit.Limb(rightArm, "Cuff", new Vector3(0.026f, -0.095f, -0.095f), new Vector3(0.04f, -0.11f, -0.13f), 0.042f, glove, castShadows: false);

                var leftArm = ProtoKit.Group(t, "Arm_L");
                ProtoKit.Part(leftArm, "Hand", PrimitiveType.Cube, new Vector3(-0.012f, -0.05f, 0.33f), new Vector3(0.05f, 0.05f, 0.1f), glove, new Vector3(0f, 0f, 20f), false);
                ProtoKit.Part(leftArm, "Thumb", PrimitiveType.Cube, new Vector3(-0.03f, -0.015f, 0.34f), new Vector3(0.016f, 0.016f, 0.05f), glove, castShadows: false);
                ProtoKit.Limb(leftArm, "Forearm", new Vector3(-0.03f, -0.075f, 0.3f), new Vector3(-0.2f, -0.25f, 0.0f), 0.036f, sleeve, castShadows: false);
                ProtoKit.Limb(leftArm, "Cuff", new Vector3(-0.028f, -0.07f, 0.305f), new Vector3(-0.05f, -0.09f, 0.27f), 0.04f, glove, castShadows: false);

                model.muzzle = muzzle;
                model.sight = sight;
                model.shellEject = eject;
                model.magazine = mag;
                model.leftHand = leftArm;
                model.muzzleFlash = flash.gameObject;
                model.muzzleLight = light;

                var view = root.AddComponent<WeaponViewModel>();
                ProtoKit.SetRef(view, "model", model);

                FpsTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(PrefabPath));
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

                var data = AssetDatabase.LoadAssetAtPath<WeaponData>(DataPath);
                if (data == null)
                {
                    FpsTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(DataPath));
                    data = ScriptableObject.CreateInstance<WeaponData>();
                    AssetDatabase.CreateAsset(data, DataPath);
                }
                data.id = "EG-47";
                data.displayName = "EG-47";
                data.description = "Rifle de asalto GENESIS";
                data.viewModelPrefab = prefab;
                data.fireClip = ProceduralAudioClips.RifleShotV2();
                data.reloadClip = ProceduralAudioClips.RifleReload();
                data.dryFireClip = ProceduralAudioClips.DryFire();
                EditorUtility.SetDirty(data);
                assets.Data = data;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            return assets;
        }
    }
}
