using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EclipseGenesis.EditorTools
{
    /// <summary>
    /// Windows player builds. From the command line:
    /// Unity -batchmode -nographics -executeMethod EclipseGenesis.EditorTools.BuildTools.BuildWindowsFromCommandLine
    /// </summary>
    public static class BuildTools
    {
        public const string WindowsOutput = "Builds/Windows/EclipseGenesis.exe";

        [MenuItem("Eclipse Genesis/Build/Windows Player")]
        public static void BuildWindowsFromMenu() => BuildWindows();

        public static void BuildWindowsFromCommandLine()
        {
            EditorApplication.Exit(BuildWindows() ? 0 : 1);
        }

        public static bool BuildWindows()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[EclipseGenesis] No scenes enabled in Build Settings");
                return false;
            }

            PlayerSettings.companyName = "Eclipse Genesis";
            PlayerSettings.productName = "ECLIPSE GENESIS";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = WindowsOutput,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[EclipseGenesis] Windows build {summary.result}: {summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime}");
            return summary.result == BuildResult.Succeeded;
        }
    }
}
