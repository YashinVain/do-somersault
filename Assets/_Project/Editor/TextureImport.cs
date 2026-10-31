using UnityEditor;

namespace JumpGame.EditorTools
{
    // Import settings for the generated textures: *_n.png and water_n*.png are normal maps, the rest ordinary colour maps.
    // They tile, they are shown at an angle (hence the anisotropy) and Crunch keeps the WebGL download small.
    public class TextureImport : AssetPostprocessor
    {
        public override uint GetVersion() => 3;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Project/Textures/")) return;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (name == "TitleShade") return;

            var t = (TextureImporter)assetImporter;
            bool normal = name.EndsWith("_n") || name.Contains("_n1") || name.Contains("_n2");
            t.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            t.sRGBTexture = !normal;
            t.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            t.filterMode = UnityEngine.FilterMode.Trilinear;
            t.anisoLevel = 4;
            t.mipmapEnabled = true;
            t.maxTextureSize = 1024;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
            t.crunchedCompression = false;   // Crunch smears fine detail: the textures are small enough to ship as they are
            t.compressionQuality = 100;
            t.alphaSource = TextureImporterAlphaSource.None;
        }
    }
}
