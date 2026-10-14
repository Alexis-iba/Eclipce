using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EclipseGenesis.EditorTools
{
    public static class ReferenceSkylineBuilder
    {
        private static Material steel;
        private static Material rust;
        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.isStatic = true;
            return go;
        }
        private static void Beam(Transform parent, string name, Vector3 a, Vector3 b, float radius)
        {
            var go = Part(parent, name, PrimitiveType.Cylinder, (a + b) * .5f, new Vector3(radius * 2, (b - a).magnitude * .5f, radius * 2), rust);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        }
        private static void Tower(Transform parent, Vector3 position, float yaw)
        {
            var root = new GameObject("WaterTower_Lattice").transform;
            root.SetParent(parent, false); root.localPosition = position; root.localRotation = Quaternion.Euler(0, yaw, 0);
            const float platform = 22;
            Part(root, "Reservoir", PrimitiveType.Cylinder, new Vector3(0, platform + 2.4f, 0), new Vector3(6.4f, 2.4f, 6.4f), steel);
            Part(root, "Roof", PrimitiveType.Sphere, new Vector3(0, platform + 4.8f, 0), new Vector3(6.55f, .8f, 6.55f), steel);
            for (int level = 0; level < 3; level++)
            {
                float y = platform + level * 2.35f;
                for (int i = 0; i < 40; i++)
                {
                    float a = i * Mathf.PI * 2 / 40, b = (i + 1) * Mathf.PI * 2 / 40;
                    Beam(root, "ReservoirHoop", new Vector3(Mathf.Cos(a) * 3.24f, y, Mathf.Sin(a) * 3.24f), new Vector3(Mathf.Cos(b) * 3.24f, y, Mathf.Sin(b) * 3.24f), .04f);
                }
            }
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * .5f + Mathf.PI * .25f;
                Vector3 foot = new Vector3(Mathf.Cos(angle) * 4.6f, 0, Mathf.Sin(angle) * 4.6f);
                Vector3 top = new Vector3(Mathf.Cos(angle) * 2.6f, platform, Mathf.Sin(angle) * 2.6f);
                Beam(root, "MainSupport", foot, top, .15f);
                Part(root, "ConcreteFooting", PrimitiveType.Cube, foot, new Vector3(1, .5f, 1), steel);
                float next = angle + Mathf.PI * .5f;
                for (int j = 0; j < 5; j++)
                {
                    float y0 = 1 + j * 4, y1 = y0 + 4;
                    float r0 = Mathf.Lerp(4.6f, 2.6f, y0 / platform), r1 = Mathf.Lerp(4.6f, 2.6f, y1 / platform);
                    Vector3 a = new Vector3(Mathf.Cos(angle) * r0, y0, Mathf.Sin(angle) * r0);
                    Vector3 b = new Vector3(Mathf.Cos(next) * r1, y1, Mathf.Sin(next) * r1);
                    Vector3 c = new Vector3(Mathf.Cos(next) * r0, y0, Mathf.Sin(next) * r0);
                    Vector3 d = new Vector3(Mathf.Cos(angle) * r1, y1, Mathf.Sin(angle) * r1);
                    Beam(root, "CrossBrace", a, b, .055f); Beam(root, "CrossBrace", c, d, .055f);
                    Beam(root, "TieBeam", a, c, .075f);
                }
            }
            for (int i = 0; i < 40; i++)
            {
                float a = i * Mathf.PI * 2 / 40, b = (i + 1) * Mathf.PI * 2 / 40;
                Vector3 p = new Vector3(Mathf.Cos(a) * 3.9f, platform, Mathf.Sin(a) * 3.9f);
                Vector3 q = new Vector3(Mathf.Cos(b) * 3.9f, platform, Mathf.Sin(b) * 3.9f);
                Beam(root, "CatwalkOuter", p, q, .1f);
                Beam(root, "CatwalkRib", p, new Vector3(Mathf.Cos(a) * 3.15f, platform, Mathf.Sin(a) * 3.15f), .075f);
                Beam(root, "Guardrail", p + Vector3.up, q + Vector3.up, .035f);
                if (i % 2 == 0) Beam(root, "RailPost", p, p + Vector3.up, .035f);
            }
            Beam(root, "AccessLadderL", new Vector3(-.32f, .2f, -3.4f), new Vector3(-.32f, 23, -3.4f), .045f);
            Beam(root, "AccessLadderR", new Vector3(.32f, .2f, -3.4f), new Vector3(.32f, 23, -3.4f), .045f);
            for (float y = .5f; y < 23; y += .35f) Beam(root, "LadderRung", new Vector3(-.32f, y, -3.4f), new Vector3(.32f, y, -3.4f), .025f);
            foreach (var collider in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
        }
        [MenuItem("Eclipse Genesis/Art/Add reference water towers")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit play mode first.");
            if (GameObject.Find("ReferenceWaterTowers") != null) return;
            steel = AssetDatabase.LoadAssetAtPath<Material>("Assets/_EclipseGenesis/Materials/Facility/M_Pipe_Steel.mat");
            rust = AssetDatabase.LoadAssetAtPath<Material>("Assets/_EclipseGenesis/Materials/PolyHaven/M_PH_rust_coarse_01.mat");
            var root = new GameObject("ReferenceWaterTowers").transform;
            foreach (var position in new[] { new Vector3(38, 0, 64), new Vector3(-42, 0, 105) })
            {
                var p = position;
                foreach (var terrain in Terrain.activeTerrains)
                {
                    var relative = p - terrain.transform.position;
                    if (relative.x >= 0 && relative.z >= 0 && relative.x <= terrain.terrainData.size.x && relative.z <= terrain.terrainData.size.z)
                        p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
                }
                Tower(root, p, p.x > 0 ? 14 : -24);
            }
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
