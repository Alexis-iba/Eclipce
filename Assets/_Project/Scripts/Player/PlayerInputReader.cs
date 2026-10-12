using UnityEngine;
using UnityEngine.InputSystem;

namespace EclipseGenesis.Player
{
    /// <summary>
    /// Single entry point between the Input System asset and gameplay code.
    /// Gameplay components read intent from here and never touch InputActions directly.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;

        private InputActionMap _player;
        private InputAction _move, _look, _jump, _sprint, _crouch, _fire, _aim,
            _reload, _interact, _switchWeapon, _grenade, _inspect, _pause;

        public Vector2 Move => _move?.ReadValue<Vector2>() ?? Vector2.zero;
        public Vector2 Look => _look?.ReadValue<Vector2>() ?? Vector2.zero;

        /// <summary>True when the last Look input came from a pointer (per-frame delta) rather than a stick (rate).</summary>
        public bool LookIsDelta => _look?.activeControl == null || _look.activeControl.device is Pointer;

        public bool JumpPressed => Pressed(_jump);
        public bool SprintHeld => Held(_sprint);
        public bool SprintPressed => Pressed(_sprint);
        public bool CrouchPressed => Pressed(_crouch);
        public bool FireHeld => Held(_fire);
        public bool FirePressed => Pressed(_fire);
        public bool AimHeld => Held(_aim);
        public bool ReloadPressed => Pressed(_reload);
        public bool InteractPressed => Pressed(_interact);
        public float SwitchWeapon => _switchWeapon?.ReadValue<float>() ?? 0f;
        public bool GrenadePressed => Pressed(_grenade);
        public bool InspectPressed => Pressed(_inspect);
        public bool PausePressed => Pressed(_pause);

        public InputActionAsset Actions
        {
            get => actions;
            set { actions = value; Bind(); }
        }

        private void Awake() => Bind();

        private void OnEnable() => _player?.Enable();

        private void OnDisable() => _player?.Disable();

        /// <summary>Enables or disables gameplay input (used by pause and menus).</summary>
        public void SetGameplayEnabled(bool enabled)
        {
            if (_player == null) return;
            if (enabled) _player.Enable(); else _player.Disable();
        }

        private void Bind()
        {
            _player?.Disable();
            _player = null;
            if (actions == null) return;

            _player = actions.FindActionMap("Player", throwIfNotFound: true);
            _move = _player.FindAction("Move", true);
            _look = _player.FindAction("Look", true);
            _jump = _player.FindAction("Jump", true);
            _sprint = _player.FindAction("Sprint", true);
            _crouch = _player.FindAction("Crouch", true);
            _fire = _player.FindAction("Fire", true);
            _aim = _player.FindAction("Aim", true);
            _reload = _player.FindAction("Reload", true);
            _interact = _player.FindAction("Interact", true);
            _switchWeapon = _player.FindAction("SwitchWeapon", true);
            _grenade = _player.FindAction("Grenade", true);
            _inspect = _player.FindAction("Inspect", true);
            _pause = _player.FindAction("Pause", true);
            if (isActiveAndEnabled) _player.Enable();
        }

        private static bool Pressed(InputAction a) => a != null && a.WasPressedThisFrame();
        private static bool Held(InputAction a) => a != null && a.IsPressed();
    }
}
