using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EclipseGenesis.EditorTools
{
    /// <summary>
    /// Renders the player camera of a scene to a PNG, for visual QA from the command line (needs a GPU, so no -nographics):
    /// Unity -batchmode -executeMethod EclipseGenesis.EditorTools.SceneCaptureTool.CaptureFromCommandLine -captureScene path -captureOut file.png
    /// </summary>
    public static class SceneCaptureTool
    {
        public static void CaptureFromCommandLine()
        {
            string scene = Arg("-captureScene", SandboxSceneBuilder.ScenePath);
            string output = Arg("-captureOut", "Logs/capture.png");
            int code = Capture(scene, output, 1600, 900) ? 0 : 1;
            EditorApplication.Exit(code);
        }

        public static bool Capture(string scenePath, string outputPath, int width, int height)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[EclipseGenesis] No MainCamera in scene");
                return false;
            }

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            cam.targetTexture = rt;
            // Several frames so exposure, shadows and history buffers settle.
            for (int i = 0; i < 30; i++) cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
            File.WriteAllBytes(outputPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Debug.Log($"[EclipseGenesis] Captured {scenePath} -> {outputPath}");
            return true;
        }

        private static string Arg(string name, string fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }
            return fallback;
        }
    }
}
