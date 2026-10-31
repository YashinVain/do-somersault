using UnityEditor;
using UnityEngine;

namespace JumpGame.EditorTools
{
    // The look of every solid material: which texture family it belongs to, how large the texture is and how much relief, moss and
    // wetness it gets. The family is read from the material's name, so a new material only needs a sensible name.
    public static partial class PrototypeSceneBuilder
    {
        public const string SurfaceShader = "JumpGame/Surface";
        const string SurfaceTextureDir = "Assets/_Project/Textures/";

        struct SurfaceLook
        {
            public string texture;
            public float scale, relief, detail, moss, wet, wetHeight, rim, metallic, gloss;
        }

        static SurfaceLook LookOf(string name, float smoothness)
        {
            string n = name.ToLowerInvariant();
            bool Has(params string[] keys)
            {
                foreach (var k in keys) if (n.Contains(k)) return true;
                return false;
            }

            var look = new SurfaceLook { texture = "painted", scale = 0.5f, relief = 0.5f, detail = 0.55f, rim = 0.22f, gloss = smoothness, wetHeight = 0.7f };

            // Small things that must stay flat colour: markers, medals, lamps, eyes, lenses and glass.
            if (Has("marker", "medal", "arrow", "lamp", "_lit", "flame", "redlight", "eye", "lens", "glass", "centermark", "goggles"))
            {
                look.detail = 0f; look.relief = 0f; look.rim = 0.08f;
                return look;
            }

            if (n.StartsWith("ice")) { look.texture = "ice"; look.scale = 0.35f; look.relief = 1f; look.detail = 1f; look.gloss = Mathf.Max(smoothness, 0.55f); look.wet = 0.3f; look.rim = 0.3f; return look; }
            if (Has("snow")) { look.texture = "snow"; look.scale = 0.3f; look.relief = 0.7f; look.detail = 1f; look.gloss = Mathf.Max(smoothness, 0.3f); look.rim = 0.3f; return look; }

            if (n.StartsWith("town_"))
            {
                if (Has("roof")) { look.texture = "roof"; look.scale = 1.1f; look.relief = 1.5f; look.detail = 1f; return look; }
                if (Has("stone")) { look.texture = "rock"; look.scale = 0.5f; look.relief = 1f; look.detail = 1f; look.wet = 0.6f; look.wetHeight = 1.3f; return look; }
                if (Has("wood", "door", "shutter")) { look.texture = "wood"; look.scale = 0.6f; look.relief = 1.1f; look.detail = 1f; return look; }
                if (Has("iron", "gold")) { look.texture = "metal"; look.scale = 0.6f; look.detail = 0.8f; look.metallic = 0.6f; look.gloss = 0.5f; return look; }
                if (Has("leaf", "green")) { look.texture = "foliage"; look.scale = 0.9f; look.relief = 1f; look.detail = 1f; look.rim = 0.4f; return look; }
                look.texture = "plaster"; look.scale = 0.4f; look.relief = 1f; look.detail = 1f; return look;
            }

            if (Has("roof") && !Has("port")) { look.texture = "roof"; look.scale = 1.1f; look.relief = 1.5f; look.detail = 1f; return look; }
            if (Has("trunk")) { look.texture = "bark"; look.scale = 0.7f; look.relief = 1.3f; look.detail = 1f; return look; }
            if (Has("wood", "yacht_deck", "chairwood")) { look.texture = "wood"; look.scale = 0.6f; look.relief = 1.2f; look.detail = 1f; look.wet = 0.5f; look.wetHeight = 0.6f; return look; }
            if (Has("leaves", "willow", "pine", "palmleaf", "leaf", "reed", "lilypad", "crown"))
            {
                look.texture = "foliage"; look.scale = 0.9f; look.relief = 1f; look.detail = 1f; look.rim = 0.45f; return look;
            }
            if (Has("rock", "stone") && !Has("pooltile"))
            {
                look.texture = "rock"; look.scale = Has("rocka", "rockb", "rockc", "jungle_rock") ? 0.1f : 0.26f; look.relief = 0.9f; look.detail = 1f; look.gloss = Mathf.Max(smoothness, 0.12f);
                look.wet = 0.55f; look.wetHeight = 0.9f;
                if (Has("jungle")) look.moss = 0.85f;
                else if (Has("lake")) look.moss = 0.55f;
                else if (Has("block_rock")) look.moss = 0.3f;
                return look;
            }
            if (Has("grass", "hill", "jungle_cap")) { look.texture = "grass"; look.scale = 0.22f; look.relief = 0.8f; look.detail = 1f; look.wet = 0.25f; return look; }
            if (Has("sand", "mud", "soil", "earth")) { look.texture = "sand"; look.scale = 0.3f; look.relief = 0.8f; look.detail = 1f; look.wet = 0.5f; look.wetHeight = 0.5f; return look; }
            if (Has("concrete", "port_wall", "coping", "pooltile", "poolline", "breaker_", "port_grey", "bridgestone") || n == "deck" || n == "wall" || n == "paintwhite")
            {
                look.texture = n == "wall" || n == "paintwhite" ? "plaster" : "concrete";
                look.scale = 0.35f; look.relief = 1f; look.detail = 1f; look.wet = 0.4f; look.wetHeight = 0.8f;
                return look;
            }
            if (Has("house_lime", "lighthouse_white", "lighthouse_red")) { look.texture = "plaster"; look.scale = 0.4f; look.relief = 1f; look.detail = 1f; return look; }
            if (Has("metal", "rail", "rig_", "port_steel", "port_roof", "iron", "cont_", "tug_", "gold"))
            {
                look.texture = "metal"; look.scale = 0.5f; look.relief = 0.9f; look.detail = 0.9f; look.metallic = Has("rig_white", "tug_white") ? 0.1f : 0.5f;
                look.gloss = Mathf.Max(smoothness, 0.5f); look.rim = 0.3f; look.wet = 0.35f; look.wetHeight = 0.8f;
                return look;
            }
            if (Has("flag_", "umbrella", "chair_red", "chairteal", "lifeguard_", "shirt", "bunting"))
            {
                look.texture = "fabric"; look.scale = 1.8f; look.relief = 1.2f; look.detail = 0.8f; look.gloss = 0.15f; return look;
            }
            return look;
        }

        // Gives a material the surface shader with the textures and settings its name asks for.
        static void ApplyLook(Material mat, string name, float smoothness)
        {
            var look = LookOf(name, smoothness);
            mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(SurfaceTextureDir + look.texture + "_a.png"));
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(SurfaceTextureDir + look.texture + "_n.png"));
            mat.SetFloat("_Scale", look.scale);
            mat.SetFloat("_UVMode", 0f);
            mat.SetFloat("_Detail", look.detail);
            mat.SetFloat("_BumpScale", look.relief);
            mat.SetFloat("_Glossiness", look.gloss);
            mat.SetFloat("_Metallic", look.metallic);
            mat.SetFloat("_MossAmount", look.moss);
            mat.SetFloat("_WetAmount", look.wet);
            mat.SetFloat("_WetHeight", look.wetHeight);
            mat.SetFloat("_Rim", look.rim);
        }
    }
}
