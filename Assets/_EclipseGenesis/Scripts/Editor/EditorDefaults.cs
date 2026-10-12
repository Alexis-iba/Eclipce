using UnityEditor;
using UnityEngine;

namespace EclipseGenesis.EditorTools
{
    /// <summary>
    /// Project-wide Editor preferences that keep Play Mode stable while scripts are edited externally (Claude/MCP):
    /// never hot-reload scripts during Play Mode — recompile when it ends. A mid-play domain reload loses runtime-only
    /// state (weapon slots, Input System action state) and floods the console with errors.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorDefaults
    {
        private const string ScriptCompilationDuringPlay = "ScriptCompilationDuringPlay";
        private const int RecompileAfterFinishedPlaying = 1;

        static EditorDefaults()
        {
            // Play Mode (and the player) keeps simulating when the window is not focused: required for MCP-driven
            // play tests, where Claude drives the Editor while another window has focus.
            if (!PlayerSettings.runInBackground)
            {
                PlayerSettings.runInBackground = true;
                Debug.Log("[EclipseGenesis] Player setting 'Run In Background' enabled.");
            }

            if (Application.isBatchMode) return;
            if (EditorPrefs.GetInt(ScriptCompilationDuringPlay, 0) != RecompileAfterFinishedPlaying)
            {
                EditorPrefs.SetInt(ScriptCompilationDuringPlay, RecompileAfterFinishedPlaying);
                Debug.Log("[EclipseGenesis] Script changes while playing: recompile after finished playing.");
            }
        }
    }
}
