using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EclipseGenesis.EditorTools.Facility
{
    /// <summary>
    /// Box and cylinder meshes whose UVs are in metres, so tiled PBR textures keep a constant world scale
    /// whatever the piece size (unlike scaled primitive cubes, which stretch). Meshes are cached by size and
    /// stored as sub-assets of one library asset.
    /// </summary>
    public sealed class MeshKit
    {
        public const string LibraryPath = "Assets/_EclipseGenesis/Art/Generated/FacilityMeshes.asset";
        private readonly string _libraryPath;

        private readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>();
        private readonly Mesh _library;

        /// <param name="libraryPath">Each generated scene owns its own mesh library so rebuilding one scene never breaks another.</param>
        public MeshKit(string libraryPath = LibraryPath)
        {
            _libraryPath = libraryPath;
            AssetDatabase.DeleteAsset(_libraryPath);
            FpsTestSceneBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(_libraryPath));
            _library = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(_libraryPath) };
            AssetDatabase.CreateAsset(_library, _libraryPath);
        }

        public int Count => _cache.Count;

        public Mesh Box(Vector3 size)
        {
            string key = $"box_{size.x:0.###}_{size.y:0.###}_{size.z:0.###}";
            if (_cache.TryGetValue(key, out var m)) return m;

            m = new Mesh { name = key };
            Vector3 h = size * 0.5f;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();

            void Face(Vector3 normal, Vector3 a, Vector3 b, Vector3 c, Vector3 d, System.Func<Vector3, Vector2> map)
            {
                int i = v.Count;
                v.AddRange(new[] { a, b, c, d });
                for (int k = 0; k < 4; k++) n.Add(normal);
                uv.Add(map(a)); uv.Add(map(b)); uv.Add(map(c)); uv.Add(map(d));
                t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }

            // UVs measured from the minimum corner, in metres.
            Vector2 X(Vector3 p) => new Vector2(p.z + h.z, p.y + h.y);
            Vector2 XN(Vector3 p) => new Vector2(h.z - p.z, p.y + h.y);
            Vector2 Y(Vector3 p) => new Vector2(p.x + h.x, p.z + h.z);
            Vector2 Z(Vector3 p) => new Vector2(h.x - p.x, p.y + h.y);
            Vector2 ZN(Vector3 p) => new Vector2(p.x + h.x, p.y + h.y);

            Face(Vector3.right, new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, -h.y, h.z), XN);
            Face(Vector3.left, new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, -h.y, -h.z), X);
            Face(Vector3.up, new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, h.y, -h.z), Y);
            Face(Vector3.down, new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, -h.y, h.z), Y);
            Face(Vector3.forward, new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, -h.y, h.z), Z);
            Face(Vector3.back, new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, -h.y, -h.z), ZN);

            return Store(key, m, v, n, uv, t);
        }

        /// <summary>Capped cylinder along local Y, centred on the origin.</summary>
        public Mesh Cylinder(float radius, float height, int segments = 20)
        {
            string key = $"cyl_{radius:0.###}_{height:0.###}_{segments}";
            if (_cache.TryGetValue(key, out var m)) return m;

            m = new Mesh { name = key };
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            float hh = height * 0.5f, circumference = 2f * Mathf.PI * radius;

            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(dir * radius + Vector3.down * hh); n.Add(dir); uv.Add(new Vector2(s / (float)segments * circumference, 0f));
                v.Add(dir * radius + Vector3.up * hh); n.Add(dir); uv.Add(new Vector2(s / (float)segments * circumference, height));
            }
            for (int s = 0; s < segments; s++)
            {
                int i = s * 2;
                t.AddRange(new[] { i, i + 1, i + 3, i, i + 3, i + 2 });
            }

            foreach (float sign in new[] { 1f, -1f })
            {
                int centre = v.Count;
                v.Add(Vector3.up * hh * sign); n.Add(Vector3.up * sign); uv.Add(new Vector2(radius, radius));
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Cos(a) * radius, hh * sign, Mathf.Sin(a) * radius);
                    v.Add(p); n.Add(Vector3.up * sign); uv.Add(new Vector2(p.x + radius, p.z + radius));
                }
                for (int s = 0; s < segments; s++)
                {
                    if (sign > 0) t.AddRange(new[] { centre, centre + s + 2, centre + s + 1 });
                    else t.AddRange(new[] { centre, centre + s + 1, centre + s + 2 });
                }
            }

            return Store(key, m, v, n, uv, t);
        }

        /// <summary>Stores an externally generated mesh (rocks, organic shapes) in this library, cached by key.</summary>
        public Mesh Register(string key, System.Func<Mesh> make)
        {
            if (_cache.TryGetValue(key, out var existing)) return existing;
            var m = make();
            m.name = key;
            AssetDatabase.AddObjectToAsset(m, _library);
            _cache[key] = m;
            return m;
        }

        private Mesh Store(string key, Mesh m, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            AssetDatabase.AddObjectToAsset(m, _library);
            _cache[key] = m;
            return m;
        }
    }
}
