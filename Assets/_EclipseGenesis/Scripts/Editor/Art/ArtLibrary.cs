using UnityEditor;
using UnityEngine;

namespace EclipseGenesis.EditorTools.Art
{
    /// <summary>
    /// Paths and placement helpers for the real art used by the scene generators: models exported from our Blender
    /// generators (ArtSource/Blender) and CC0 Poly Haven downloads, both under Art/Imported. Generators check
    /// <see cref="Has"/> and fall back to their procedural placeholders when an asset is missing.
    /// </summary>
    public static class ArtLibrary
    {
        public const string Root = ImportedModelConventions.Root;
        /// <summary>Residential buildings (ArtSource/Blender/Environment/house_builder.py v2, with glass and variants).</summary>
        public const string HouseA = Root + "Buildings/SM_House_A.fbx";
        public const string HouseB = Root + "Buildings/SM_House_B.fbx";
        public const string HouseC = Root + "Buildings/SM_House_C.fbx";
        public const string HouseD = Root + "Buildings/SM_House_D.fbx";
        public const string ShopA = Root + "Buildings/SM_Shop_A.fbx";
        /// <summary>Round-crowned deciduous trees with leaf-cluster cards (nature_builder.py), heights 11 / 9 / 13 m.</summary>
        public static readonly string[] Broadleaves = { Root + "Vegetation/SM_Tree_Broadleaf_A.fbx", Root + "Vegetation/SM_Tree_Broadleaf_B.fbx", Root + "Vegetation/SM_Tree_Broadleaf_C.fbx" };
        /// <summary>Game-ready pines (ArtSource/Blender/Environment/nature_builder.py), heights 13 / 10 / 8 m.</summary>
        public static readonly string[] Pines = { Root + "Vegetation/SM_Pine_A.fbx", Root + "Vegetation/SM_Pine_B.fbx", Root + "Vegetation/SM_Pine_C.fbx" };
        /// <summary>Decimated Poly Haven scans: boulder (~1.5 m), moss rock set (~8 m cluster), fallen log (~4 m).</summary>
        public const string RockBoulder = Root + "Environment/SM_Rock_Boulder.fbx";
        public const string RockMossSet = Root + "Environment/SM_Rock_MossSet.fbx";
        public const string DeadLog = Root + "Environment/SM_Log_Dead.fbx";

        public static string PolyHaven(string category, string id) => $"{Root}{category}/PolyHaven/{id}/{id}.fbx";

        public static bool Has(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;

        /// <summary>Instantiates a model (prefab link kept) under <paramref name="parent"/>; returns null when the asset is missing.</summary>
        public static GameObject Place(string path, Transform parent, Vector3 localPos, float yaw, float scale = 1f, bool colliders = true)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (src == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                if (!colliders || c.name.EndsWith("_Glass")) Object.DestroyImmediate(c);   // glass never blocks movement or shots
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
            return go;
        }

        /// <summary>Places a tree without per-twig colliders and gives its trunk a simple capsule.</summary>
        public static GameObject PlaceTree(string path, Transform parent, Vector3 localPos, float yaw, float scale, float trunkRadius)
        {
            var go = Place(path, parent, localPos, yaw, scale, colliders: false);
            if (go == null) return null;
            var cap = go.AddComponent<CapsuleCollider>();
            cap.radius = trunkRadius / scale;
            cap.height = 4f / scale;
            cap.center = new Vector3(0f, cap.height * 0.5f, 0f);
            return go;
        }
    }
}
