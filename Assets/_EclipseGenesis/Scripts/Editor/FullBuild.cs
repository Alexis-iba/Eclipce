using EclipseGenesis.EditorTools.Combat;
using EclipseGenesis.EditorTools.Facility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EclipseGenesis.EditorTools
{
    /// <summary>
    /// Regenerates every generated asset and scene in dependency order:
    /// EG-47 + player prefab + EG_FPS_Test → Stalker prefab → Genesis_Research_Facility with the mission.
    /// Command line: Unity -batchmode -executeMethod EclipseGenesis.EditorTools.FullBuild.BuildFromCommandLine
    /// </summary>
    public static class FullBuild
    {
        [MenuItem("Eclipse Genesis/Build/Everything", priority = 0)]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll();
            EditorSceneManager.OpenScene(FacilitySceneBuilder.ScenePath);
        }

        public static void BuildFromCommandLine()
        {
            BuildAll();
            EditorApplication.Exit(0);
        }

        public static void BuildAll()
        {
            FpsTestSceneBuilder.Build();
            AlienBuilder.Build();
            FacilitySceneBuilder.Build();
            Debug.Log("[EclipseGenesis] Full build complete");
        }
    }
}
