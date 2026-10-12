using UnityEngine;

namespace EclipseGenesis.Player
{
    /// <summary>Temporary on-screen readout for tuning locomotion in the sandbox. Toggle with F1.</summary>
    public sealed class MovementDebugOverlay : MonoBehaviour
    {
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private bool visible = true;

        private GUIStyle _style;

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current?.f1Key.wasPressedThisFrame == true) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible || movement == null) return;
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };

            float fov = targetCamera != null ? targetCamera.fieldOfView : 0f;
            string text =
                $"ECLIPSE GENESIS · Movement sandbox (F1)\n" +
                $"State: {movement.State}   Stance: {movement.Stance}\n" +
                $"Speed: {movement.HorizontalSpeed:0.00} m/s   Vertical: {movement.Velocity.y:0.00}\n" +
                $"Grounded: {movement.IsGrounded}   Height: {movement.CurrentHeight:0.00} m\n" +
                $"Tactical charge: {movement.TacticalSprintCharge01:P0}   FOV: {fov:0.0}\n" +
                $"FPS: {1f / Mathf.Max(0.0001f, Time.smoothDeltaTime):0}";

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(12, 12, 360, 118), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(22, 18, 350, 110), text, _style);
        }
    }
}
