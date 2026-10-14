using UnityEngine;
using UnityEngine.Rendering;


namespace EclipseGenesis.Combat
{
    // Keep the first-person weapon readable when the world camera faces the sunset.
    public sealed class ViewmodelSurfaceLight : MonoBehaviour
    {
        private GameObject fill;
        private void Start()
        {
            var camera = GetComponentInParent<Camera>();
            if (camera == null) return;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
                renderer.renderingLayerMask |= 2u;
            fill = new GameObject("ViewmodelSurfaceFill");
            fill.transform.SetParent(camera.transform, false);
            fill.transform.localPosition = new Vector3(-.25f, .3f, -.2f);
            var light = fill.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 2f;
            light.shadows = LightShadows.None;
            light.color = new Color(1f, .95f, .86f);
            var hd = fill.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
            hd.lightlayersMask = (UnityEngine.Rendering.HighDefinition.RenderingLayerMask)2;
            light.lightUnit = LightUnit.Lumen;
            light.intensity = 3000f;
        }
        private void OnDestroy()
        {
            if (fill != null) Destroy(fill);
        }
    }
}

