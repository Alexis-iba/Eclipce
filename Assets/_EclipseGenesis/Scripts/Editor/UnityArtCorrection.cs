using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace EclipseGenesis.EditorTools
{
    public static class UnityArtCorrection
    {
        [MenuItem("Eclipse Genesis/Art/Apply clean oak foliage")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit play mode before editing foliage.");
            const string folder = "Assets/_EclipseGenesis/Art/Generated/";
            const string materialPath = "Assets/_EclipseGenesis/Materials/PolyHaven/M_OakBranchCleanV2.mat";
            var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/_EclipseGenesis/Materials/PolyHaven/M_PH_tree_small_02_leaves.mat");
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(source) { name = "M_OakBranchCleanV2" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "Textures/OakBranchCleanV2.png"));
            material.SetTexture("_NormalMap", null);
            material.SetTexture("_MaskMap", null);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Smoothness", .25f);
            material.SetFloat("_AlphaCutoff", .72f);
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            var meshes = new Dictionary<Mesh, Mesh>();
            int changed = 0;
            foreach (var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var renderer = filter.GetComponent<Renderer>();
                if (renderer == null || !renderer.sharedMaterials.Contains(source)) continue;
                Mesh mesh;
                if (!meshes.TryGetValue(filter.sharedMesh, out mesh))
                {
                    string path = folder + filter.sharedMesh.name + "_CleanUV.asset";
                    mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (mesh == null)
                    {
                        mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                        mesh.name = filter.sharedMesh.name + "_CleanUV";
                        var uv = mesh.uv;
                        var triangles = mesh.GetTriangles(Array.IndexOf(renderer.sharedMaterials, source));
                        // Each original foliage card is a quad with six triangle indices.
                        for (int start = 0; start < triangles.Length; start += 6)
                        {
                            var ids = triangles.Skip(start).Take(6).Distinct().ToArray();
                            float x0 = ids.Min(i => uv[i].x), x1 = ids.Max(i => uv[i].x);
                            float y0 = ids.Min(i => uv[i].y), y1 = ids.Max(i => uv[i].y);
                            foreach (int i in ids)
                                uv[i] = new Vector2((uv[i].x - x0) / Mathf.Max(.001f, x1 - x0), (uv[i].y - y0) / Mathf.Max(.001f, y1 - y0));
                        }
                        mesh.uv = uv;
                        AssetDatabase.CreateAsset(mesh, path);
                    }
                    meshes[filter.sharedMesh] = mesh;
                }
                filter.sharedMesh = mesh;
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m == source ? material : m).ToArray();
                EditorUtility.SetDirty(filter);
                EditorUtility.SetDirty(renderer);
                changed++;
            }
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Clean oak foliage applied to " + changed + " trees.");
        }
    }
}
