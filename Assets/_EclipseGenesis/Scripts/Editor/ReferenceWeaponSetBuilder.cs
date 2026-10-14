using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using EclipseGenesis.Weapons;

namespace EclipseGenesis.EditorTools
{
    public static class ReferenceWeaponSetBuilder
    {
        public const string Folder = "Assets/_EclipseGenesis/Art/Imported/Weapons/ReferenceWeapons";
        public const string Prefabs = "Assets/_EclipseGenesis/Prefabs/Weapons/Imported";
        static Transform Find(Transform t, string n) => t.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == n || x.name.StartsWith(n + "."));
        static Material MaterialFor(string name)
        {
            string path = Folder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            string standard = "Assets/_EclipseGenesis/Materials/PolyHaven/M_" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(standard);
            if (existing != null) return existing;
            m = new Material(Shader.Find("HDRP/Lit")) { name = name };
            Color color = name.Contains("Sand") ? new Color(.32f,.23f,.13f) : name.Contains("Armor") ? new Color(.16f,.035f,.25f) : new Color(.3f,.31f,.33f);
            m.SetColor("_BaseColor", color); m.SetFloat("_Metallic", name.Contains("Sand") ? 0 : .55f); m.SetFloat("_Smoothness", .35f);
            if (name.Contains("Core")) { m.SetColor("_BaseColor", new Color(.25f,.08f,.8f)); m.SetColor("_EmissiveColor", new Color(.35f,.05f,1f) * 1800f); }
            HDMaterial.ValidateMaterial(m); AssetDatabase.CreateAsset(m,path); return m;
        }
        public static void Build()
        {
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/VM_EG47_Detailed.prefab");
            foreach (string id in new[] { "VX9", "BR12", "NS50", "PLASMAX" })
            {
                var root = UnityEngine.Object.Instantiate(original); root.name = "VM_" + id + "_Reference";
                try
                {
                    var wm = root.GetComponent<WeaponModel>();
                    Vector3 sightPosition = wm.sight.position;
                    var left = Find(root.transform,"Arm_L"); var right = Find(root.transform,"Arm_R");
                    if (left != null) left.SetParent(root.transform,true);
                    if (right != null) right.SetParent(root.transform,true);
                    if (wm.muzzleFlash != null) wm.muzzleFlash.transform.SetParent(root.transform,true);
                    if (wm.muzzleLight != null) wm.muzzleLight.transform.SetParent(root.transform,true);
                    var oldModel = root.transform.Find("EG47_Model");
                    if (oldModel != null) UnityEngine.Object.DestroyImmediate(oldModel.gameObject);
                    var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/SM_" + id + ".fbx"),root.transform);
                    model.name = id + "_Model";
                    var sight = Find(model.transform,"Socket_Sight");
                    model.transform.position += sightPosition - sight.position;
                    foreach (var r in model.GetComponentsInChildren<Renderer>())
                    {
                        r.sharedMaterials = r.sharedMaterials.Select(m => MaterialFor(m.name.Replace(" (Instance)",""))).ToArray();
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                    foreach (var c in model.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                    wm.muzzle = Find(model.transform,"Socket_Muzzle"); wm.sight = sight;
                    wm.shellEject = Find(model.transform,"Socket_ShellEject"); wm.magazine = Find(model.transform,"Magazine") ?? Find(model.transform,"PowerCell"); wm.leftHand = left;
                    if (left != null) left.localPosition += Vector3.forward * (id == "VX9" ? -.12f : id == "NS50" ? .025f : -.055f);
                    if (wm.muzzleFlash != null) wm.muzzleFlash.transform.SetParent(wm.muzzle,false);
                    if (wm.muzzleLight != null) wm.muzzleLight.transform.SetParent(wm.muzzle,false);
                    var prefab = PrefabUtility.SaveAsPrefabAsset(root,Prefabs + "/" + root.name + ".prefab");
                    string display = id == "PLASMAX" ? "PLASMA-X" : id == "VX9" ? "VX-9" : id == "BR12" ? "BR-12" : "NS-50";
                    string path = "Assets/_EclipseGenesis/Data/Weapons/" + display + ".asset";
                    var data = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
                    if (data == null) { data = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_EclipseGenesis/Data/Weapons/EG47.asset")); AssetDatabase.CreateAsset(data,path); }
                    data.id = data.displayName = display; data.viewModelPrefab = prefab;
                    data.description = id == "VX9" ? "Subfusil" : id == "BR12" ? "Escopeta" : id == "NS50" ? "Rifle de precisión" : "Arma alienígena";
                    data.magazineSize = id == "VX9" ? 32 : id == "BR12" ? 8 : id == "NS50" ? 5 : 24;
                    data.reserveAmmo = id == "VX9" ? 160 : id == "BR12" ? 40 : id == "NS50" ? 30 : 96;
                    data.automatic = id == "VX9" || id == "PLASMAX";
                    data.pellets = id == "BR12" ? 8 : 1;
                    data.damage = id == "VX9" ? 19 : id == "BR12" ? 10 : id == "NS50" ? 92 : 38;
                    data.roundsPerMinute = id == "VX9" ? 850 : id == "BR12" ? 90 : id == "NS50" ? 55 : 320;
                    data.effectiveRange = id == "NS50" ? 150 : id == "BR12" ? 16 : id == "VX9" ? 28 : 60;
                    data.maxRange = id == "NS50" ? 400 : id == "BR12" ? 65 : 180;
                    data.adsSpread = id == "NS50" ? .035f : id == "BR12" ? 2.2f : .2f;
                    data.hipSpread = id == "BR12" ? 4.2f : id == "NS50" ? 4 : 2.1f;
                    data.adsFovMultiplier = id == "NS50" ? .4f : .78f;
                    data.reloadTime = id == "NS50" ? 3.1f : id == "BR12" ? 3.4f : 2.1f;
                    EditorUtility.SetDirty(data);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
            var loadout = new[] { "EG47", "VX-9", "BR-12", "NS-50", "PLASMA-X" }.Select(n => AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_EclipseGenesis/Data/Weapons/"+n+".asset")).ToArray();
            foreach (var controller in UnityEngine.Object.FindObjectsByType<WeaponController>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var so = new SerializedObject(controller); var array = so.FindProperty("loadout"); array.arraySize = loadout.Length;
                for (int i=0;i<loadout.Length;i++) array.GetArrayElementAtIndex(i).objectReferenceValue=loadout[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}

