using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

namespace EclipseGenesis.EditorTools.Mcp
{
    /// <summary>
    /// Connects the Editor to the local MCP for Unity HTTP server (127.0.0.1:8080) after every script reload,
    /// doing exactly what the "Connect" button in Window > MCP for Unity does (Bridge.StartAsync).
    /// Only acts when the server is already listening. Disable with the menu toggle below.
    /// </summary>
    [InitializeOnLoad]
    public static class McpAutoConnect
    {
        private const string PrefKey = "EclipseGenesis.McpAutoConnect";
        private const string MenuPath = "Eclipse Genesis/MCP/Auto Connect";
        private const string Host = "127.0.0.1";
        private const int Port = 8080;

        static McpAutoConnect()
        {
            if (Application.isBatchMode || !EditorPrefs.GetBool(PrefKey, true)) return;
            EditorApplication.delayCall += () => _ = TryConnectAsync();
        }

        [MenuItem("Eclipse Genesis/MCP/Connect Now")]
        public static void ConnectNow() => _ = TryConnectAsync(force: true);

        [MenuItem(MenuPath)]
        private static void Toggle() => EditorPrefs.SetBool(PrefKey, !EditorPrefs.GetBool(PrefKey, true));

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PrefKey, true));
            return true;
        }

        private static async Task TryConnectAsync(bool force = false)
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
                    : "[EclipseGenesis] MCP bridge failed to start (see MCP for Unity window).");
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
                var connect = client.ConnectAsync(Host, Port);
                return connect.Wait(300) && client.Connected;
            }
            catch
            {
                return false;
            }
        }
    }
}
