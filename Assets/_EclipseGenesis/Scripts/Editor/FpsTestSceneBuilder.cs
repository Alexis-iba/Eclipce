using System.IO;
using System.Linq;
using EclipseGenesis.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace EclipseGenesis.EditorTools
{
    /// <summary>
    /// Builds the EG_FPS_Test scene (provisional geometry) and the FPS player prefab from code, so the setup is
    /// reproducible from the command line: Unity -batchmode -executeMethod EclipseGenesis.EditorTools.FpsTestSceneBuilder.BuildFromCommandLine
    /// </summary>
    public static class FpsTestSceneBuilder
    {
        public const string ScenePath = "Assets/_EclipseGenesis/Scenes/EG_FPS_Test.unity";
        public const string PlayerPrefabPath = "Assets/_EclipseGenesis/Prefabs/Player/EG_Player.prefab";
        public const string MovementSettingsPath = "Assets/_EclipseGenesis/Data/Player/DefaultMovementSettings.asset";
        public const string InputActionsPath = "Assets/_EclipseGenesis/Input/EclipseGenesisControls.inputactions";
        private const string MaterialsFolder = "Assets/_EclipseGenesis/Materials/Prototype";
        private const string SkyProfilePath = "Assets/Settings/SkyandFogSettingsProfile.asset";

        [MenuItem("Eclipse Genesis/Build/EG_FPS_Test Scene")]
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
            RemoveStaleProjectWideActions();
            var settings = EnsureAsset<MovementSettings>(MovementSettingsPath);
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions == null) throw new FileNotFoundException("Input actions asset missing", InputActionsPath);

            var mats = new Materials();
            GameObject prefab = BuildPlayerPrefab(settings, actions);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildLighting();
            BuildLayout(mats);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.name = "EG_Player";
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -20f), Quaternion.identity);

            EnsureFolder(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[EclipseGenesis] Built {ScenePath} and {PlayerPrefabPath}");
        }

        // ------------------------------------------------------------------ player

        private static GameObject BuildPlayerPrefab(MovementSettings settings, InputActionAsset actions)
        {
            var root = new GameObject("EG_Player");
            try
            {
                root.tag = "Player";
                var cc = root.AddComponent<CharacterController>();
                cc.height = settings.standingHeight;
                cc.radius = settings.radius;
                cc.center = new Vector3(0f, settings.standingHeight * 0.5f, 0f);

                var reader = root.AddComponent<PlayerInputReader>();
                SetRef(reader, "actions", actions);

                var movement = root.AddComponent<PlayerMovement>();
                SetRef(movement, "settings", settings);
                SetRef(movement, "input", reader);

                var eye = new GameObject("CameraRoot").transform;
                eye.SetParent(root.transform, false);
                eye.localPosition = new Vector3(0f, settings.standingHeight - 0.12f, 0f);

                var offset = new GameObject("CameraOffset").transform;
                offset.SetParent(eye, false);

                var camGo = new GameObject("MainCamera");
                camGo.tag = "MainCamera";
                camGo.transform.SetParent(offset, false);
                var cam = camGo.AddComponent<Camera>();
                cam.nearClipPlane = 0.03f;
                cam.farClipPlane = 1000f;
                cam.fieldOfView = 68f;
                camGo.AddComponent<HDAdditionalCameraData>();
                camGo.AddComponent<AudioListener>();

                // Mount point for first-person weapons (Phase 3). Empty on purpose: no fake weapon yet.
                var weaponHolder = new GameObject("WeaponHolder").transform;
                weaponHolder.SetParent(camGo.transform, false);
                weaponHolder.localPosition = new Vector3(0.2f, -0.22f, 0.4f);

                var look = root.AddComponent<PlayerLook>();
                SetRef(look, "input", reader);
                SetRef(look, "pitchPivot", eye);

                var fx = root.AddComponent<PlayerCameraEffects>();
                SetRef(fx, "movement", movement);
                SetRef(fx, "eyePivot", eye);
                SetRef(fx, "offsetPivot", offset);
                SetRef(fx, "targetCamera", cam);

                var overlay = root.AddComponent<MovementDebugOverlay>();
                SetRef(overlay, "movement", movement);
                SetRef(overlay, "look", look);
                SetRef(overlay, "targetCamera", cam);

                EnsureFolder(Path.GetDirectoryName(PlayerPrefabPath));
                return PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ------------------------------------------------------------------ lighting

        private static void BuildLighting()
        {
            var sun = new GameObject("Sun");
            sun.AddHDLight(LightType.Directional);
            var light = sun.GetComponent<Light>();
            light.lightUnit = LightUnit.Lux;
            light.intensity = 100000f;
            light.colorTemperature = 5600f;
            light.useColorTemperature = true;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

            var volumeGo = new GameObject("Sky and Fog Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(SkyProfilePath);
            if (volume.sharedProfile == null) Debug.LogWarning($"[EclipseGenesis] Sky profile not found at {SkyProfilePath}");
        }

        // ------------------------------------------------------------------ layout

        private static void BuildLayout(Materials m)
        {
            var env = new GameObject("Environment").transform;

            // Floor and perimeter (60 x 60 m).
            Block(env, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(60f, 0.2f, 60f), m.Floor);
            Block(env, "Wall_N", new Vector3(0f, 2f, 30.25f), new Vector3(60.5f, 4f, 0.5f), m.Wall);
            Block(env, "Wall_S", new Vector3(0f, 2f, -30.25f), new Vector3(60.5f, 4f, 0.5f), m.Wall);
            Block(env, "Wall_E", new Vector3(30.25f, 2f, 0f), new Vector3(0.5f, 4f, 60f), m.Wall);
            Block(env, "Wall_W", new Vector3(-30.25f, 2f, 0f), new Vector3(0.5f, 4f, 60f), m.Wall);

            // Sprint lane with a marker every 5 m (left side).
            for (int i = 0; i <= 10; i++)
            {
                Block(env, $"LaneMarker_{i * 5}m", new Vector3(-6f, 0.005f, -25f + i * 5f), new Vector3(3f, 0.01f, 0.12f), i % 2 == 0 ? m.Accent : m.Glow);
            }

            // Stairs: 10 steps of 0.2 m rise / 0.35 m run, then a landing.
            var stairs = new GameObject("Stairs").transform;
            stairs.SetParent(env, false);
            for (int i = 0; i < 10; i++)
            {
                float h = 0.2f * (i + 1);
                Block(stairs, $"Step_{i + 1}", new Vector3(8f, h * 0.5f, -10f + i * 0.35f + 0.175f), new Vector3(3f, h, 0.35f), m.Obstacle);
            }
            Block(stairs, "Landing", new Vector3(8f, 1f, -5.5f), new Vector3(3f, 2f, 2f), m.Obstacle);

            // Ramps: walkable 20°, walkable 35°, and 55° which must stop the player (slope limit 45°).
            Ramp(env, "Ramp_20deg", new Vector3(14f, 0f, -10f), 20f, 8f, m.Obstacle);
            Ramp(env, "Ramp_35deg", new Vector3(18f, 0f, -10f), 35f, 6f, m.Obstacle);
            Ramp(env, "Ramp_55deg_Blocked", new Vector3(22f, 0f, -10f), 55f, 4f, m.Accent);

            // Low tunnel (1.3 m clearance): requires crouch or slide, and blocks standing up inside.
            var tunnel = new GameObject("LowTunnel").transform;
            tunnel.SetParent(env, false);
            Block(tunnel, "Roof", new Vector3(0f, 1.5f, 8f), new Vector3(4f, 0.4f, 8f), m.Wall);
            Block(tunnel, "Side_L", new Vector3(-2.25f, 0.65f, 8f), new Vector3(0.5f, 1.3f, 8f), m.Wall);
            Block(tunnel, "Side_R", new Vector3(2.25f, 0.65f, 8f), new Vector3(0.5f, 1.3f, 8f), m.Wall);
            Block(tunnel, "Strip", new Vector3(0f, 1.29f, 8f), new Vector3(0.1f, 0.02f, 7.8f), m.Glow);

            // Jump boxes: 0.5 m (step/jump), 0.9 m (jump), 1.4 m (should not be reachable).
            Block(env, "JumpBox_0.5m", new Vector3(-14f, 0.25f, -8f), new Vector3(2f, 0.5f, 2f), m.Obstacle);
            Block(env, "JumpBox_0.9m", new Vector3(-17f, 0.45f, -8f), new Vector3(2f, 0.9f, 2f), m.Obstacle);
            Block(env, "JumpBox_1.4m", new Vector3(-20f, 0.7f, -8f), new Vector3(2f, 1.4f, 2f), m.Accent);

            // Cover and pillars to check collisions and corner sliding.
            for (int i = 0; i < 4; i++)
            {
                Block(env, $"Pillar_{i}", new Vector3(-12f + i * 4f, 2f, 18f), new Vector3(0.8f, 4f, 0.8f), m.Wall);
                Block(env, $"Cover_{i}", new Vector3(10f + i * 3.5f, 0.6f, 14f + (i % 2) * 2f), new Vector3(2.4f, 1.2f, 0.6f), m.Obstacle);
            }

            // Platform gap for momentum jumps.
            Block(env, "Platform_A", new Vector3(18f, 0.5f, 6f), new Vector3(4f, 1f, 6f), m.Obstacle);
            Block(env, "Platform_B", new Vector3(18f, 0.5f, 14f), new Vector3(4f, 1f, 6f), m.Obstacle);

            foreach (Transform t in env.GetComponentsInChildren<Transform>())
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
            }
        }

        private static GameObject Block(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Ramp rising along +Z from <paramref name="foot"/> with the given angle and horizontal length.</summary>
        private static void Ramp(Transform parent, string name, Vector3 foot, float angle, float length, Material material)
        {
            float rad = angle * Mathf.Deg2Rad;
            float slopeLength = length / Mathf.Cos(rad);
            float rise = Mathf.Tan(rad) * length;
            const float thickness = 0.3f;

            var go = Block(parent, name, Vector3.zero, new Vector3(3f, thickness, slopeLength), material);
            go.transform.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            Vector3 surfaceCenter = foot + new Vector3(0f, rise * 0.5f, length * 0.5f);
            go.transform.localPosition = surfaceCenter - go.transform.up * (thickness * 0.5f);
        }

        // ------------------------------------------------------------------ assets

        private sealed class Materials
        {
            public readonly Material Floor = Lit("M_Proto_Floor", new Color(0.16f, 0.17f, 0.18f), 0.35f, 0.1f);
            public readonly Material Wall = Lit("M_Proto_Wall", new Color(0.42f, 0.43f, 0.45f), 0.45f, 0.2f);
            public readonly Material Obstacle = Lit("M_Proto_Obstacle", new Color(0.28f, 0.29f, 0.31f), 0.5f, 0.6f);
            public readonly Material Accent = Lit("M_Proto_AccentRed", new Color(0.62f, 0.07f, 0.06f), 0.4f, 0f);
            public readonly Material Glow = Emissive("M_Proto_GlowBlue", new Color(0.1f, 0.55f, 1f), 60000f);
        }

        private static Material Lit(string name, Color color, float smoothness, float metallic)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                EnsureFolder(MaterialsFolder);
                mat = new Material(Shader.Find("HDRP/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material Emissive(string name, Color color, float nits)
        {
            var mat = Lit(name, color * 0.2f, 0.6f, 0f);
            HDMaterial.SetUseEmissiveIntensity(mat, true);
            HDMaterial.SetEmissiveColor(mat, color);
            HDMaterial.SetEmissiveIntensity(mat, nits, EmissiveIntensityUnit.Nits);
            HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            EnsureFolder(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void RemoveStaleProjectWideActions()
        {
            const string key = "com.unity.input.settings.actions";
            if (EditorBuildSettings.TryGetConfigObject(key, out Object current) && current == null)
            {
                EditorBuildSettings.RemoveConfigObject(key);
            }
        }

        private static void AddSceneToBuild(string path)
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => !string.IsNullOrEmpty(s.path) && File.Exists(s.path) && s.path != path)
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) throw new MissingReferenceException($"{target.GetType().Name}.{field} not found");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
