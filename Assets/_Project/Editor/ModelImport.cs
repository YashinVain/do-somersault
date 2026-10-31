using UnityEditor;

namespace JumpGame.EditorTools
{
    // Settings every model from Blender gets on import: the game moves the bones itself, so there is no animation and
    // the bone objects must stay in the hierarchy.
    public class ModelImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/_Project/Models/")) return;

            var m = (ModelImporter)assetImporter;
            m.animationType = ModelImporterAnimationType.None;
            m.importAnimation = false;
            m.importCameras = false;
            m.importLights = false;
            m.importBlendShapes = true;
            m.optimizeGameObjects = false;
            m.meshCompression = ModelImporterMeshCompression.Off;
            m.isReadable = false;
            m.addCollider = false;
            m.importNormals = ModelImporterNormals.Import;
            m.importTangents = ModelImporterTangents.None;
            m.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }
    }
}
