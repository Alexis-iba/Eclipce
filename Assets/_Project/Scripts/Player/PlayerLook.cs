using UnityEngine;

namespace EclipseGenesis.Player
{
    /// <summary>
    /// Mouse / stick look. Yaw rotates the player body, pitch rotates the camera root,
    /// so movement always follows where the player is facing.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public sealed class PlayerLook : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Transform pitchPivot;

        [Header("Sensitivity")]
        [Tooltip("Degrees per mouse count.")]
        [SerializeField] private float mouseSensitivity = 0.08f;
        [Tooltip("Degrees per second at full stick deflection.")]
        [SerializeField] private float stickSpeed = 220f;
        [SerializeField] private bool invertY;

        [Header("Limits")]
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        [SerializeField] private bool lockCursorOnEnable = true;

        private float _pitch;
        private float _yaw;

        /// <summary>User preference multiplier (settings menu).</summary>
        public float SensitivityMultiplier { get; set; } = 1f;

        /// <summary>Temporary multiplier (e.g. aiming down sights).</summary>
        public float ContextMultiplier { get; set; } = 1f;

        public float Pitch => _pitch;
        public float Yaw => _yaw;
        public Transform PitchPivot { get => pitchPivot; set => pitchPivot = value; }
        public PlayerInputReader Input { get => input; set => input = value; }
        public bool InvertY { get => invertY; set => invertY = value; }

        private void Awake()
        {
            if (input == null) TryGetComponent(out input);
            _yaw = transform.eulerAngles.y;
            if (pitchPivot != null) _pitch = NormalizeAngle(pitchPivot.localEulerAngles.x);
        }

        private void OnEnable()
        {
            if (lockCursorOnEnable) SetCursorLocked(true);
        }

        private void Update()
        {
            if (input == null) return;

            Vector2 look = input.Look;
            Vector2 degrees = input.LookIsDelta
                ? look * mouseSensitivity
                : look * (stickSpeed * Time.deltaTime);
            degrees *= SensitivityMultiplier * ContextMultiplier;

            AddRotation(degrees.x, invertY ? degrees.y : -degrees.y);
        }

        /// <summary>Applies yaw/pitch in degrees. Also used by weapon recoil.</summary>
        public void AddRotation(float yawDegrees, float pitchDegrees)
        {
            _yaw = Mathf.Repeat(_yaw + yawDegrees, 360f);
            _pitch = Mathf.Clamp(_pitch + pitchDegrees, minPitch, maxPitch);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (pitchPivot != null) pitchPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private static float NormalizeAngle(float a) => a > 180f ? a - 360f : a;
    }
}
