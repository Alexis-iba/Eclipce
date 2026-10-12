using System;
using System.Globalization;
using EclipseGenesis.Player;
using UnityEngine;

namespace EclipseGenesis.Core
{
    /// <summary>
    /// Development switches read from the player command line (QA screenshots, quick testing):
    ///   -egSpawn x,y,z,yaw   start the player at a position/heading instead of the scene spawn.
    /// No effect when the argument is absent.
    /// </summary>
    public static class DevCommandLine
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Apply()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-egTrace") >= 0) new GameObject("EG_Trace").AddComponent<Trace>();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-egSpawn") continue;
                string[] p = args[i + 1].Split(',');
                if (p.Length < 3) return;
                var c = CultureInfo.InvariantCulture;
                var pos = new Vector3(float.Parse(p[0], c), float.Parse(p[1], c), float.Parse(p[2], c));
                float yaw = p.Length > 3 ? float.Parse(p[3], c) : 0f;
                var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
                if (player == null) return;
                player.Teleport(pos, Quaternion.Euler(0f, yaw, 0f));
                if (player.TryGetComponent(out PlayerLook look)) look.AddRotation(Mathf.DeltaAngle(look.Yaw, yaw), 0f);
                Debug.Log($"[EclipseGenesis] -egSpawn: player moved to {pos} yaw {yaw}");
            }
        }

        /// <summary>-egTrace: logs player position, move input and input devices once per second.</summary>
        private sealed class Trace : MonoBehaviour
        {
            private float _next;

            private void Start()
            {
                foreach (var d in UnityEngine.InputSystem.InputSystem.devices)
                    Debug.Log($"[EG-TRACE] device: {d.layout} '{d.displayName}' ({d.description.interfaceName})");
            }

            private void Update()
            {
                if (Time.unscaledTime < _next) return;
                _next = Time.unscaledTime + 1f;
                var p = FindAnyObjectByType<PlayerMovement>();
                if (p == null) return;
                var input = p.GetComponent<PlayerInputReader>();
                var moveAction = input != null && input.Actions != null ? input.Actions.FindAction("Move") : null;
                string control = moveAction?.activeControl != null ? moveAction.activeControl.path : "none";
                Debug.Log($"[EG-TRACE] t={Time.time:0.0} pos={p.transform.position} speed={p.HorizontalSpeed:0.00} move={input?.Move} via={control} state={p.State}");
            }
        }
    }
}
