using System.Collections.Generic;
using System.Linq;
using EclipseGenesis.Weapons;
using UnityEditor;
using UnityEngine;

namespace EclipseGenesis.EditorTools.Art
{
    /// <summary>
    /// Builds VM_EG47_Detailed: the provisional EG-47 viewmodel with its primitive gun replaced by the Blender model
    /// (ArtSource/Blender/Weapons/eg47_builder.py -> Art/Imported/Weapons/SM_EG47.fbx). Keeps the provisional arms,
    /// muzzle flash/light, WeaponModel/WeaponViewModel and their tuning; aligns the new holographic sight on the old
    /// sight so ADS stays calibrated, and moves the hands onto the new grip and foregrip. The provisional prefab is kept.
    /// </summary>
    public static class EG47ViewModelBuilder
    {
        public const string ProvisionalPath = "Assets/_EclipseGenesis/Prefabs/Weapons/EG_Weapon_EG47.prefab";
        public const string ModelPath = ImportedModelConventions.Root + "Weapons/SM_EG47.fbx";
        public const string OutputPath = WeaponModelBinder.OutputFolder + "/VM_EG47_Detailed.prefab";
        public const string WeaponDataPath = "Assets/_EclipseGenesis/Data/Weapons/EG47.asset";

        [MenuItem("Eclipse Genesis/Art/Build EG-47 Detailed Viewmodel")]
        public static void BuildFromMenu() => Debug.Log("[EclipseGenesis] " + Build(assign: true));

        public static string Build(bool assign)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (fbx == null) return $"missing {ModelPath}";
            var root = PrefabUtility.LoadPrefabContents(ProvisionalPath);
            try
            {
                var wm = root.GetComponentInChildren<WeaponModel>(true);
                Transform Find(Transform r, string n) => r.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
                var armL = Find(root.transform, "Arm_L");
                var armR = Find(root.transform, "Arm_R");
                var oldGrip = Find(root.transform, "Grip");
                Vector3 oldSight = wm.sight.position;
                Vector3 rightHand = Find(armR, "Hand") != null ? Find(armR, "Hand").position : armR.position;
                Vector3 leftHand = Find(armL, "Hand") != null ? Find(armL, "Hand").position : armL.position;
                Vector3 gripPos = oldGrip != null ? oldGrip.position : rightHand;

                // Keep arms and effects; remove every other piece of the primitive gun.
                var keep = new List<Transform> { armL, armR };
                if (wm.muzzleFlash != null) keep.Add(wm.muzzleFlash.transform);
                if (wm.muzzleLight != null) keep.Add(wm.muzzleLight.transform);
                foreach (var k in keep) k.SetParent(root.transform, true);
                foreach (Transform child in root.transform.Cast<Transform>().ToList())
                    if (!keep.Contains(child)) Object.DestroyImmediate(child.gameObject);

                // New gun, aligned on the old sight.
                var model = (GameObject)Object.Instantiate(fbx, root.transform);
                model.name = "EG47_Model";
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var sight = Find(model.transform, "Socket_Sight");
                model.transform.position += oldSight - sight.position;

                wm.muzzle = Find(model.transform, "Socket_Muzzle");
                wm.sight = sight;
                wm.shellEject = Find(model.transform, "Socket_ShellEject");
                wm.magazine = Find(model.transform, "Magazine");
                wm.leftHand = armL;
                if (wm.muzzleFlash != null) { wm.muzzleFlash.transform.SetParent(wm.muzzle, false); wm.muzzleFlash.transform.localPosition = Vector3.zero; }
                if (wm.muzzleLight != null) { wm.muzzleLight.transform.SetParent(wm.muzzle, false); wm.muzzleLight.transform.localPosition = new Vector3(0f, 0f, 0.02f); }

                var modelArmL = Find(model.transform, "Arm_L");
                var modelArmR = Find(model.transform, "Arm_R");
                if (modelArmL != null && modelArmR != null)
                {
                    // v2 FBX ships gloved hands posed on the grip and foregrip: they replace the provisional arms.
                    Object.DestroyImmediate(armL.gameObject);
                    Object.DestroyImmediate(armR.gameObject);
                    wm.leftHand = modelArmL;
                }
                else
                {
                    // Hands onto the new grip (model origin) and the angled foregrip (Blender (0, 0.335, -0.01)).
                    Vector3 newGrip = model.transform.TransformPoint(new Vector3(0f, -0.03f, -0.02f));
                    Vector3 newFore = model.transform.TransformPoint(new Vector3(0f, -0.005f, 0.335f));
                    armR.position += newGrip - gripPos;
                    armL.position += newFore - leftHand;
                }

                PrefabUtility.SaveAsPrefabAsset(root, OutputPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            string report = $"Built {OutputPath}";
            if (assign)
            {
                var data = AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponDataPath);
                data.viewModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OutputPath);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                report += $" and assigned it to {WeaponDataPath}";
            }
            return report;
        }
    }
}
