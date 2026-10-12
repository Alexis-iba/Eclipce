using UnityEngine;
using UnityEngine.InputSystem;

namespace EclipseGenesis.Player
{
    /// <summary>
    /// Temporary on-screen readout for tuning locomotion in the test scene.
    /// F1 toggles it; [ and ] change mouse sensitivity (saved in PlayerPrefs).
    /// </summary>
    public sealed class MovementDebugOverlay : MonoBehaviour
    {
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private PlayerLook look;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private bool visible = true;
        [SerializeField] private float sensitivityStep = 0.1f;

        private GUIStyle _style;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) visible = !visible;
            if (look == null) return;
            if (kb.leftBracketKey.wasPressedThisFrame) look.SensitivityMultiplier -= sensitivityStep;
            if (kb.rightBracketKey.wasPressedThisFrame) look.SensitivityMultiplier += sensitivityStep;
        }

        private void OnGUI()
        {
            if (!visible || movement == null) return;
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };

            float fov = targetCamera != null ? targetCamera.fieldOfView : 0f;
            float sens = look != null ? look.SensitivityMultiplier : 1f;
            string text =
                $"ECLIPSE GENESIS · EG_FPS_Test (F1)\n" +
                $"State: {movement.State}   Stance: {movement.Stance}\n" +
                $"Speed: {movement.HorizontalSpeed:0.00} m/s   Vertical: {movement.Velocity.y:0.00}\n" +
                $"Grounded: {movement.IsGrounded}   Height: {movement.CurrentHeight:0.00} m\n" +
                $"Tactical charge: {movement.TacticalSprintCharge01:P0}   FOV: {fov:0.0}\n" +
                $"Sensitivity: x{sens:0.0}  ( [ / ] )\n" +
                $"FPS: {1f / Mathf.Max(0.0001f, Time.smoothDeltaTime):0}";

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(12, 12, 360, 136), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(22, 18, 350, 130), text, _style);
        }
    }
}
