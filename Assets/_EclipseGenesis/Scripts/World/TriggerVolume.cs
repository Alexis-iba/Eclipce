using System;
using EclipseGenesis.Player;
using UnityEngine;

namespace EclipseGenesis.World
{
    /// <summary>Box volume that reports when the player enters (uses bounds checks, no physics callbacks needed).</summary>
    public sealed class TriggerVolume : MonoBehaviour
    {
        [SerializeField] private Vector3 size = new Vector3(4f, 3f, 4f);

        private Transform _player;
        private bool _inside;

        public Vector3 Size { get => size; set => size = value; }
        public bool PlayerInside => _inside;
        public event Action Entered;

        private void Start()
        {
            var p = FindAnyObjectByType<PlayerMovement>();
            if (p != null) _player = p.transform;
        }

        private void Update()
        {
            if (_player == null) return;
            Vector3 local = transform.InverseTransformPoint(_player.position + Vector3.up * 0.9f);
            bool inside = Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.y) <= size.y * 0.5f && Mathf.Abs(local.z) <= size.z * 0.5f;
            if (inside && !_inside) Entered?.Invoke();
            _inside = inside;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.1f, 0.6f, 1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
