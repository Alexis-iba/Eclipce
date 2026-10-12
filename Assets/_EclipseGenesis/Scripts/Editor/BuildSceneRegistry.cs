using System.IO;
using System.Linq;
using EclipseGenesis.EditorTools.Facility;
using UnityEditor;

namespace EclipseGenesis.EditorTools
{
    /// <summary>Single source of truth for the scene order in Build Settings (first scene = what the .exe opens).</summary>
    public static class BuildSceneRegistry
    {
        public static readonly string[] Order =
        {
            FacilitySceneBuilder.ScenePath,
            FpsTestSceneBuilder.ScenePath,
        };

        public static void Sync()
        {
            EditorBuildSettings.scenes = Order
                .Where(File.Exists)
                .Select(p => new EditorBuildSettingsScene(p, true))
                .ToArray();
        }
    }
}
