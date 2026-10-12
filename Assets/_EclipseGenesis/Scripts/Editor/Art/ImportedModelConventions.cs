using UnityEditor;
using UnityEngine;

namespace EclipseGenesis.EditorTools.Art
{
    /// <summary>
    /// Import rules for models exported from Blender into Assets/_EclipseGenesis/Art/Imported/{Category}/.
    /// Convention: 1 Blender unit = 1 metre, FBX exported with "Apply Transform" and Forward -Z / Up Y
    /// (ArtSource/Blender/eg_export_fbx.py). Unity then bakes the axis conversion so roots have no -90° rotation.
    /// See Docs/BLENDER_UNITY_PIPELINE.md.
    /// </summary>
    public sealed class ImportedModelConventions : AssetPostprocessor
    {
        public const string Root = "Assets/_EclipseGenesis/Art/Imported/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (ModelImporter)assetImporter;
            string category = assetPath.Substring(Root.Length).Split('/')[0];

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = category == "Aliens" || category == "Characters";
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;

            switch (category)
            {
                case "Vegetation":
                    // Foliage would give mesh colliders on every twig; trunks get simple capsules when placed.
                    importer.addCollider = false;
                    importer.animationType = ModelImporterAnimationType.None;
                    importer.importAnimation = false;
                    break;
                case "Environment":
                case "Vehicles":
                case "Props":
                    // Static scenery: real colliders from the mesh (use simple UCX_ collision meshes for heavy assets).
                    importer.addCollider = true;
                    importer.animationType = ModelImporterAnimationType.None;
                    importer.importAnimation = false;
                    break;
                case "Weapons":
                    // First-person weapons never collide (they would block shots); animations come from clips if present.
                    importer.addCollider = false;
                    importer.animationType = ModelImporterAnimationType.Generic;
                    break;
                case "Aliens":
                    importer.addCollider = false;
                    importer.animationType = ModelImporterAnimationType.Generic;
                    break;
                case "Characters":
                    importer.addCollider = false;
                    importer.animationType = ModelImporterAnimationType.Human;
                    break;
            }
        }
    }
}
