using UnityEngine;

namespace JumpGame
{
    // Picture quality that does not depend on which quality level a platform picks: smooth edges, sharp textures at a slant,
    // and a render size that follows the real screen.
    public static class Quality
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            QualitySettings.antiAliasing = 4;                         // MSAA, so the edges of roofs and rails do not crawl
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.globalTextureMipmapLimit = 0;            // full-size textures
            QualitySettings.lodBias = 2f;
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadowDistance = 90f;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.vSyncCount = 1;
        }
    }
}
