using System;
using UnityEngine;

namespace EclipseGenesis.World
{
    public enum DoorState { Closed, Opening, Open, Closing }

    /// <summary>
    /// Sliding security door with two leaves. Opens automatically when the player is near unless locked.
    /// Leaves keep their colliders, so a closed door blocks the player for real.
    /// </summary>
    public sealed class AutoDoor : MonoBehaviour
    {
        [SerializeField] private Transform leftLeaf;
        [SerializeField] private Transform rightLeaf;
        [Tooltip("How far each leaf slides sideways (local X).")]
        [SerializeField] private float slideDistance = 1.15f;
        [SerializeField] private float openSeconds = 0.6f;
        [Tooltip("Player distance (m) that opens the door.")]
        [SerializeField] private float triggerRadius = 3.2f;
        [SerializeField] private float closeDelay = 1f;
        [SerializeField] private bool locked;

        [Header("Status lights")]
        [SerializeField] private Renderer[] statusRenderers;
        [SerializeField] private Material unlockedMaterial;
        [SerializeField] private Material lockedMaterial;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openClip;
        [SerializeField] private AudioClip lockedClip;

        private Transform _player;
        private Vector3 _leftClosed, _rightClosed;
        private float _open01;
        private float _lastNearTime = float.NegativeInfinity;
        private bool _wasNear;

        public DoorState State { get; private set; } = DoorState.Closed;
        public bool IsLocked => locked;
        public bool IsOpen => State == DoorState.Open;
        public float OpenAmount => _open01;

        public event Action<AutoDoor> Opened;

        private void Awake()
        {
            if (leftLeaf != null) _leftClosed = leftLeaf.localPosition;
            if (rightLeaf != null) _rightClosed = rightLeaf.localPosition;
            RefreshStatus();
        }

        private void Start()
        {
            var playerGo = GameObject.FindWithTag("Player");
            if (playerGo != null) _player = playerGo.transform;
        }

        private void Update()
        {
            bool near = _player != null && IsWithinTrigger(_player.position);
            if (near) _lastNearTime = Time.time;

            if (near && !_wasNear && locked && audioSource != null && lockedClip != null) audioSource.PlayOneShot(lockedClip);
            _wasNear = near;

            bool wantOpen = !locked && Time.time - _lastNearTime <= closeDelay;
            float target = wantOpen ? 1f : 0f;
            if (Mathf.Approximately(_open01, target))
            {
                SetState(wantOpen ? DoorState.Open : DoorState.Closed);
                return;
            }

            if (wantOpen && State == DoorState.Closed && audioSource != null && openClip != null) audioSource.PlayOneShot(openClip);
            SetState(wantOpen ? DoorState.Opening : DoorState.Closing);

            _open01 = Mathf.MoveTowards(_open01, target, Time.deltaTime / Mathf.Max(0.05f, openSeconds));
            ApplyLeaves();
        }

        private bool IsWithinTrigger(Vector3 worldPos)
        {
            Vector3 local = transform.InverseTransformPoint(worldPos);
            return Mathf.Abs(local.x) < 1.6f && Mathf.Abs(local.z) < triggerRadius && local.y > -1f && local.y < 3f;
        }

        private void ApplyLeaves()
        {
            float eased = Mathf.SmoothStep(0f, 1f, _open01);
            if (leftLeaf != null) leftLeaf.localPosition = _leftClosed + Vector3.left * (slideDistance * eased);
            if (rightLeaf != null) rightLeaf.localPosition = _rightClosed + Vector3.right * (slideDistance * eased);
        }

        private void SetState(DoorState next)
        {
            if (State == next) return;
            State = next;
            if (next == DoorState.Open) Opened?.Invoke(this);
        }

        /// <summary>Locks or unlocks the door (mission scripts, terminals).</summary>
        public void SetLocked(bool value)
        {
            locked = value;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            Material m = locked ? lockedMaterial : unlockedMaterial;
            if (m == null || statusRenderers == null) return;
            foreach (var r in statusRenderers)
            {
                if (r != null) r.sharedMaterial = m;
            }
        }
    }
}
