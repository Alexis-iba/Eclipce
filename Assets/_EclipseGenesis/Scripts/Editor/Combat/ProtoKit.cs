using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace EclipseGenesis.EditorTools.Combat
{
    /// <summary>Helpers to assemble provisional multi-part models (weapon, creatures) and their HDRP materials.</summary>
    public static class ProtoKit
    {
        public const string MaterialFolder = "Assets/_EclipseGenesis/Materials/Combat";

        public static Transform Group(Transform parent, string name, Vector3 localPos = default, Vector3 euler = default)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = Quaternion.Euler(euler);
            return t;
        }

        /// <summary>Primitive part without collider. Scale is the final size (cylinders/capsules: y = full length).</summary>
        public static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 size, Material mat, Vector3 euler = default, bool castShadows = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = type == PrimitiveType.Cylinder || type == PrimitiveType.Capsule
                ? new Vector3(size.x, size.y * 0.5f, size.z)
                : size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Cylinder/capsule spanning two local points.</summary>
        public static GameObject Limb(Transform parent, string name, Vector3 from, Vector3 to, float radius, Material mat, bool capsule = true, bool castShadows = true)
        {
            Vector3 d = to - from;
            var go = Part(parent, name, capsule ? PrimitiveType.Capsule : PrimitiveType.Cylinder, (from + to) * 0.5f,
                new Vector3(radius * 2f, d.magnitude + (capsule ? radius * 2f : 0f), radius * 2f), mat, default, castShadows);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            return go;
        }

        public static Material Lit(string name, Color color, float metallic, float smoothness)
        {
            var mat = LoadOrCreate(name, "HDRP/Lit");
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            Finish(mat);
            return mat;
        }

        public static Material Glow(string name, Color color, float nits)
        {
            var mat = LoadOrCreate(name, "HDRP/Lit");
            mat.SetColor("_BaseColor", color * 0.2f);
            mat.SetFloat("_Smoothness", 0.6f);
            HDMaterial.SetUseEmissiveIntensity(mat, true);
            HDMaterial.SetEmissiveColor(mat, color);
            HDMaterial.SetEmissiveIntensity(mat, nits, EmissiveIntensityUnit.Nits);
            Finish(mat);
            return mat;
        }

        public static Material UnlitAdditive(string name, Color color, float nits)
        {
            var mat = LoadOrCreate(name, "HDRP/Unlit");
            mat.SetColor("_UnlitColor", color);
            mat.SetFloat("_SurfaceType", 1f);
            mat.SetFloat("_BlendMode", 1f);
            mat.SetFloat("_DoubleSidedEnable", 1f);
            HDMaterial.SetUseEmissiveIntensity(mat, true);
            HDMaterial.SetEmissiveColor(mat, color);
            HDMaterial.SetEmissiveIntensity(mat, nits, EmissiveIntensityUnit.Nits);
            Finish(mat);
            return mat;
        }

        private static Material LoadOrCreate(string name, string shader)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                FpsTestSceneBuilder.EnsureFolder(MaterialFolder);
                mat = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader.name != shader) mat.shader = Shader.Find(shader);
            return mat;
        }

        private static void Finish(Material mat)
        {
            HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);
        }

        public static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) throw new MissingReferenceException($"{target.GetType().Name}.{field} not found");
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetRefs(Object target, string arrayField, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(arrayField);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
