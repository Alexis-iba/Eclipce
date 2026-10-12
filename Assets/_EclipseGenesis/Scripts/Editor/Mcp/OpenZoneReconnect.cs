using UnityEditor;
namespace EclipseGenesis.EditorTools.Mcp {
 [InitializeOnLoad] public static class OpenZoneReconnect {
  static OpenZoneReconnect() { EditorApplication.delayCall += McpAutoConnect.ConnectNow; }
 }
}
