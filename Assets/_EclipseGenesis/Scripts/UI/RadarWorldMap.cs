using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EclipseGenesis.UI
{
    public sealed class RadarWorldMap : MonoBehaviour
    {
        private struct Footprint { public Bounds bounds; public bool road; }
        private readonly List<Footprint> footprints = new List<Footprint>();
        private Texture2D texture;
        private readonly Color32[] pixels = new Color32[128 * 128];
        private Camera view;
        private float nextRefresh;

        public void Initialize(Camera camera)
        {
            view = camera;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("Lot_")) continue;
                var renderers = t.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                footprints.Add(new Footprint { bounds = bounds });
            }
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                bool road = false;
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.name.ToLowerInvariant().Contains("asphalt")) road = true;
                if (road) footprints.Add(new Footprint { bounds = r.bounds, road = true });
            }
            if (footprints.Count == 0) { GetComponent<RawImage>().enabled = false; enabled = false; return; }
            texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            GetComponent<RawImage>().texture = texture;
        }

        private void LateUpdate()
        {
            if (texture == null || view == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .15f;
            Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 origin = view.transform.position;
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float dx = x - 63.5f, dy = y - 63.5f;
                if (dx * dx + dy * dy > 61f * 61f) { pixels[y * 128 + x] = new Color32(0, 0, 0, 0); continue; }
                Vector3 world = origin + (right * dx + forward * dy) * (120f / 128f);
                var color = new Color32(32, 38, 37, 240);
                foreach (var f in footprints)
                {
                    var min = f.bounds.min; var max = f.bounds.max;
                    if (world.x < min.x || world.x > max.x || world.z < min.z || world.z > max.z) continue;
                    if (f.road) { color = new Color32(65, 71, 73, 245); continue; }
                    float edge = Mathf.Min(world.x - min.x, max.x - world.x, world.z - min.z, max.z - world.z);
                    byte shade = edge < .8f ? (byte)130 : (byte)(92 + ((Mathf.FloorToInt(world.z * 1.5f) & 1) * 8));
                    color = new Color32(shade, shade, (byte)(shade + 2), 255);
                    break;
                }
                pixels[y * 128 + x] = color;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        private void OnDestroy() { if (texture != null) Destroy(texture); }
    }
}

