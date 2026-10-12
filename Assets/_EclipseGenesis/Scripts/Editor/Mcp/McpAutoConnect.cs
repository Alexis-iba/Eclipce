using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

namespace EclipseGenesis.EditorTools.Mcp
{
    /// <summary>
    /// Keeps the Editor connected to MCP for Unity without manual clicks:
    /// 1. Turns on the package's own "Auto-start on load" option (EditorPref MCPForUnity.AutoStartOnLoad), so
    ///    every Editor launch starts the local HTTP server (127.0.0.1:8080) and connects.
    /// 2. Safety net: a few seconds after each script reload, if the bridge is still down but the server is
    ///    listening, it does what the "Connect" button does (Bridge.StartAsync).
    /// Toggle with Eclipse Genesis > MCP > Auto Connect.
    /// </summary>
    [InitializeOnLoad]
    public static class McpAutoConnect
    {
        private const string PrefKey = "EclipseGenesis.McpAutoConnect";
        private const string PackageAutoStartKey = "MCPForUnity.AutoStartOnLoad";
        private const string MenuPath = "Eclipse Genesis/MCP/Auto Connect";
        private const string Host = "127.0.0.1";
        private const int Port = 8080;
        private const double SafetyNetDelaySeconds = 15;

        private static double _checkAt;

        static McpAutoConnect()
        {
            if (Application.isBatchMode || !EditorPrefs.GetBool(PrefKey, true)) return;

            if (!EditorPrefs.GetBool(PackageAutoStartKey, false))
            {
                EditorPrefs.SetBool(PackageAutoStartKey, true);
                Debug.Log("[EclipseGenesis] Enabled MCP for Unity 'Auto-start on load' (server + connection start with the Editor).");
            }

            _checkAt = EditorApplication.timeSinceStartup + SafetyNetDelaySeconds;
            EditorApplication.update += SafetyNet;
        }

        private static void SafetyNet()
        {
            if (EditorApplication.timeSinceStartup < _checkAt) return;
            EditorApplication.update -= SafetyNet;
            _ = TryConnectAsync(force: false);
        }

        [MenuItem("Eclipse Genesis/MCP/Connect Now")]
        public static void ConnectNow() => _ = TryConnectAsync(force: true);

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            bool enabled = !EditorPrefs.GetBool(PrefKey, true);
            EditorPrefs.SetBool(PrefKey, enabled);
            EditorPrefs.SetBool(PackageAutoStartKey, enabled);
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PrefKey, true));
            return true;
        }

        private static async Task TryConnectAsync(bool force)
        {
            try
            {
                var bridge = MCPServiceLocator.Bridge;
                if (bridge.IsRunning)
                {
                    if (force) Debug.Log("[EclipseGenesis] MCP bridge already connected.");
                    return;
                }
                if (!IsServerListening())
                {
                    if (force) Debug.LogWarning($"[EclipseGenesis] MCP server not listening on {Host}:{Port}. Start it in Window > MCP for Unity.");
                    return;
                }

                bool started = await bridge.StartAsync();
                Debug.Log(started
                    ? $"[EclipseGenesis] MCP bridge connected to http://{Host}:{Port}"
                    : "[EclipseGenesis] MCP bridge failed to start (see Window > MCP for Unity).");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EclipseGenesis] MCP auto-connect error: {e.Message}");
            }
        }

        private static bool IsServerListening()
        {
            try
            {
                using var client = new TcpClient();
                return client.ConnectAsync(Host, Port).Wait(300) && client.Connected;
            }
            catch
            {
                return false;
            }
        }
    }
}
