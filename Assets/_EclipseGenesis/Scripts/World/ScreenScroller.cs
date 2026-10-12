using UnityEngine;

namespace EclipseGenesis.World
{
    /// <summary>Scrolls the emissive texture of a control screen so terminals look alive. Uses one material instance per screen.</summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class ScreenScroller : MonoBehaviour
    {
        private static readonly int EmissiveMap = Shader.PropertyToID("_EmissiveColorMap");
        private static readonly int BaseMap = Shader.PropertyToID("_BaseColorMap");

        [SerializeField] private Vector2 speed = new Vector2(0f, 0.04f);
        [Tooltip("Seconds between updates; screens refresh in steps like a real display.")]
        [SerializeField] private float stepSeconds = 0.12f;

        private Material _material;
        private Vector2 _offset;
        private float _next;

        private void Awake()
        {
            _material = GetComponent<Renderer>().material;
            _offset = new Vector2(Random.value, Random.value);
        }

        private void Update()
        {
            if (_material == null || Time.time < _next) return;
            _next = Time.time + stepSeconds;
            _offset += speed * stepSeconds;
            _material.SetTextureOffset(EmissiveMap, _offset);
            _material.SetTextureOffset(BaseMap, _offset);
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
