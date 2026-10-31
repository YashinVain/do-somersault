using UnityEditor;

namespace JumpGame.EditorTools
{
    // Settings every model from Blender gets on import. The game moves the bones itself, so there is no animation clip, but the
    // characters must keep their skin: with the animation type set to None Unity throws the bones away and imports a stiff mesh.
    public class ModelImport : AssetPostprocessor
    {
        // Raising this makes Unity import every model again with the new settings.
        public override uint GetVersion() => 3;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/_Project/Models/")) return;

            var m = (ModelImporter)assetImporter;
            bool character = assetPath.StartsWith("Assets/_Project/Models/Characters/");
            m.animationType = character ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            m.importAnimation = false;
            m.importCameras = false;
            m.importLights = false;
            m.importBlendShapes = true;
            m.optimizeGameObjects = false;   // the bone objects must stay in the hierarchy
            m.meshCompression = ModelImporterMeshCompression.Off;
            m.isReadable = false;
            m.addCollider = false;
            m.importNormals = ModelImporterNormals.Import;
            m.importTangents = ModelImporterTangents.CalculateMikk;
            m.skinWeights = ModelImporterSkinWeights.Custom;
            m.maxBonesPerVertex = 4;
            m.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }
    }
}
