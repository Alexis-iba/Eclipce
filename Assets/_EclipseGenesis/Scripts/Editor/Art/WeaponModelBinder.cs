using System.IO;
using System.Linq;
using EclipseGenesis.Weapons;
using UnityEditor;
using UnityEngine;

namespace EclipseGenesis.EditorTools.Art
{
    /// <summary>
    /// Turns an imported weapon FBX (Art/Imported/Weapons) into a first-person viewmodel prefab that plugs into the
    /// existing WeaponController: finds the Blender empties named Socket_Muzzle, Socket_Sight, Socket_ShellEject, the
    /// Magazine object and the left-hand group (Arm_L), then adds WeaponModel + WeaponViewModel and copies the muzzle
    /// flash and light from the provisional EG-47. Assign the result to a WeaponData.viewModelPrefab.
    /// </summary>
    public static class WeaponModelBinder
    {
        public const string OutputFolder = "Assets/_EclipseGenesis/Prefabs/Weapons/Imported";
        private const string ProvisionalEg47 = "Assets/_EclipseGenesis/Prefabs/Weapons/EG_Weapon_EG47.prefab";

        [MenuItem("Eclipse Genesis/Art/Create Weapon Viewmodel From Selected FBX")]
        public static void CreateFromSelection()
        {
            var model = Selection.activeObject as GameObject;
            string path = model != null ? AssetDatabase.GetAssetPath(model) : null;
            if (string.IsNullOrEmpty(path) || !path.StartsWith(ImportedModelConventions.Root + "Weapons/"))
            {
                EditorUtility.DisplayDialog("Weapon viewmodel", "Select an FBX inside Art/Imported/Weapons.", "OK");
                return;
            }
            var prefab = Create(path, out string report);
            Debug.Log($"[EclipseGenesis] {report}");
            if (prefab != null) Selection.activeObject = prefab;
        }

        /// <summary>Builds the prefab; returns null and explains why when required sockets are missing.</summary>
        public static GameObject Create(string fbxPath, out string report)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                Transform Find(string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name || t.name.StartsWith(name + "."));
                var muzzle = Find("Socket_Muzzle");
                var sight = Find("Socket_Sight");
                var eject = Find("Socket_ShellEject");
                var magazine = Find("Magazine");
                var leftHand = Find("Arm_L");
                var missing = new[] { ("Socket_Muzzle", muzzle), ("Socket_Sight", sight) }.Where(x => x.Item2 == null).Select(x => x.Item1).ToArray();
                if (missing.Length > 0)
                {
                    report = $"{fbxPath}: missing required empties {string.Join(", ", missing)}; see Docs/BLENDER_UNITY_PIPELINE.md";
                    return null;
                }

                foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var wm = root.AddComponent<WeaponModel>();
                wm.muzzle = muzzle;
                wm.sight = sight;
                wm.shellEject = eject != null ? eject : muzzle;
                wm.magazine = magazine;
                wm.leftHand = leftHand;

                // Reuse the provisional EG-47 flash and light so effects work immediately.
                var provisional = AssetDatabase.LoadAssetAtPath<GameObject>(ProvisionalEg47)?.GetComponent<WeaponModel>();
                if (provisional != null && provisional.muzzleFlash != null)
                {
                    var flash = Object.Instantiate(provisional.muzzleFlash, muzzle, false);
                    flash.name = "MuzzleFlash";
                    flash.SetActive(false);
                    wm.muzzleFlash = flash;
                    var light = Object.Instantiate(provisional.muzzleLight.gameObject, muzzle, false);
                    light.name = "MuzzleLight";
                    wm.muzzleLight = light.GetComponent<Light>();
                    wm.muzzleLight.enabled = false;
                }

                var view = root.AddComponent<WeaponViewModel>();
                var so = new SerializedObject(view);
                so.FindProperty("model").objectReferenceValue = wm;
                so.ApplyModifiedPropertiesWithoutUndo();

                FpsTestSceneBuilder.EnsureFolder(OutputFolder);
                string outPath = $"{OutputFolder}/VM_{Path.GetFileNameWithoutExtension(fbxPath)}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, outPath);
                report = $"Created {outPath} (magazine: {(magazine != null ? "yes" : "no")}, left hand: {(leftHand != null ? "yes" : "no")})";
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
