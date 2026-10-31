using System;
using System.Collections.Generic;
using UnityEngine;

namespace JumpGame
{
    public enum SkinRole { Skin, Face, Outfit, Cap, Strap, Lens, Shirt, Hair, Accent, Metal, ShinR }

    [Serializable]
    public struct TintedPart
    {
        public Renderer renderer;
        public SkinRole role;
    }

    [Serializable]
    public struct GearPart
    {
        public GameObject part;
        public Gear gear;
    }

    // Dresses the diver: tints the body parts and switches props on and off. Nothing here touches the body's
    // size or mass, so the physics and the medals don't depend on the character.
    public class DiverSkin : MonoBehaviour
    {
        [SerializeField] TintedPart[] tinted;
        [SerializeField] GearPart[] gear;
        [Tooltip("Torso, shirt, trunks and ring: the parts that get wider on a heavy character.")]
        [SerializeField] Transform[] widen;
        [SerializeField] Transform[] lenses;
        [SerializeField] Transform shinR;
        [Tooltip("Authored character models; when it has one for the character, the placeholder body above is hidden.")]
        [SerializeField] DiverModel model;

        static readonly Color Metal = new Color(0.75f, 0.78f, 0.82f);

        // The colour and gloss of a role for a character; shared by the placeholder body and the authored models.
        public static void Colour(SkinRole role, CharacterSkin s, out Color colour, out float gloss)
        {
            bool blade = (s.gear & Gear.Blade) != 0;
            float skinGloss = s.metallic ? 0.85f : 0.3f;
            switch (role)
            {
                case SkinRole.Skin: colour = s.skin; gloss = skinGloss; break;
                case SkinRole.Face: colour = s.face; gloss = skinGloss; break;
                case SkinRole.Outfit: colour = s.outfit; gloss = 0.3f; break;
                case SkinRole.Cap: colour = s.cap; gloss = 0.55f; break;
                case SkinRole.Strap: colour = s.strap; gloss = 0.5f; break;
                case SkinRole.Lens: colour = s.lens; gloss = 0.9f; break;
                case SkinRole.Shirt: colour = s.shirt; gloss = 0.25f; break;
                case SkinRole.Hair: colour = s.hair; gloss = 0.2f; break;
                case SkinRole.Accent: colour = s.accent; gloss = 0.4f; break;
                case SkinRole.Metal: colour = Metal; gloss = 0.8f; break;
                default: colour = blade ? Metal : s.skin; gloss = blade ? 0.8f : skinGloss; break; // ShinR
            }
        }

        readonly Dictionary<SkinRole, Material> materials = new Dictionary<SkinRole, Material>();
        Vector3[] widenBase, lensBase;
        Vector3 shinBase;
        bool ready;

        void Prepare()
        {
            if (ready) return;
            ready = true;

            // One private material per role so tinting never edits the shared assets.
            for (int i = 0; i < tinted.Length; i++)
            {
                var r = tinted[i];
                if (r.renderer == null) continue;
                if (!materials.TryGetValue(r.role, out var m))
                {
                    m = new Material(r.renderer.sharedMaterial) { name = "Skin_" + r.role };
                    materials[r.role] = m;
                }
                r.renderer.sharedMaterial = m;
            }

            widenBase = new Vector3[widen.Length];
            for (int i = 0; i < widen.Length; i++) widenBase[i] = widen[i].localScale;
            lensBase = new Vector3[lenses.Length];
            for (int i = 0; i < lenses.Length; i++) lensBase[i] = lenses[i].localScale;
            if (shinR != null) shinBase = shinR.localScale;
        }

        void OnDestroy()
        {
            foreach (var m in materials.Values) if (m != null) DestroyImmediate(m);
        }

        public void Apply(CharacterSkin s)
        {
            Prepare();

            bool blade = (s.gear & Gear.Blade) != 0;
            foreach (var kv in materials)
            {
                Colour(kv.Key, s, out var colour, out float gloss);
                kv.Value.color = colour;
                kv.Value.SetFloat("_Glossiness", gloss);
            }

            foreach (var g in gear)
                if (g.part != null) g.part.SetActive((s.gear & g.gear) != 0);

            for (int i = 0; i < widen.Length; i++)
            {
                var b = widenBase[i];
                widen[i].localScale = new Vector3(b.x * s.width, b.y, b.z * Mathf.Lerp(1f, s.width, 0.7f));
            }

            // Eyes without goggles are smaller than goggle lenses.
            float lens = (s.gear & Gear.Goggles) != 0 ? 1f : 0.55f;
            for (int i = 0; i < lenses.Length; i++) lenses[i].localScale = lensBase[i] * lens;

            // A running blade is a thin shaft rather than a calf.
            if (shinR != null) shinR.localScale = blade ? new Vector3(shinBase.x * 0.45f, shinBase.y, shinBase.z * 0.45f) : shinBase;

            if (model != null) model.Use(s);
        }
    }
}
