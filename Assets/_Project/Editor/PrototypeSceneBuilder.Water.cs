using UnityEditor;
using UnityEngine;

namespace JumpGame.EditorTools
{
    // Water materials: the colour of each sea and the ripple, foam and caustic textures they share.
    public static partial class PrototypeSceneBuilder
    {
        public const string WaterShader = "JumpGame/Water";

        static bool IsWater(string name)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("fall")) return false;           // the waterfall is a white sheet, not a sea
            return n.Contains("water") || n == "sea" || n.EndsWith("sea") || n.Contains("lagoon");
        }

        static void ApplyWater(Material mat, string hex, float alpha, float smoothness)
        {
            var shallow = Hex(hex);
            // The deep colour is the same hue, darker and bluer, so each location keeps its own sea.
            Color.RGBToHSV(shallow, out float h, out float s, out float v);
            var deep = Color.HSVToRGB(Mathf.Repeat(h + 0.015f, 1f), Mathf.Min(1f, s * 1.1f), v * 0.55f);
            shallow.a = alpha * 0.8f;
            deep.a = Mathf.Min(1f, alpha + 0.12f);
            mat.SetColor("_Color", shallow);
            mat.SetColor("_DeepColor", deep);
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(SurfaceTextureDir + "water_n1.png"));
            mat.SetTexture("_BumpMap2", AssetDatabase.LoadAssetAtPath<Texture2D>(SurfaceTextureDir + "water_n2.png"));
            mat.SetTexture("_FoamTex", AssetDatabase.LoadAssetAtPath<Texture2D>(SurfaceTextureDir + "foam_a.png"));
            mat.SetTexture("_Caustics", AssetDatabase.LoadAssetAtPath<Texture2D>(SurfaceTextureDir + "caustics_a.png"));
            mat.SetFloat("_Scale", 0.11f);
            mat.SetFloat("_Flow", 0.03f);
            mat.SetFloat("_BumpScale", 0.55f);
            mat.SetFloat("_Glossiness", Mathf.Max(smoothness, 0.85f));
        }
    }
}
