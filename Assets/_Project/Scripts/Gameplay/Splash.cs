using UnityEngine;

namespace JumpGame
{
    // Entry splash: a crown of drops, a column for rough entries, a foam patch, rings spreading out and bubbles below.
    public class Splash : MonoBehaviour
    {
        [SerializeField] ParticleSystem crown;
        [SerializeField] ParticleSystem jet;
        [SerializeField] ParticleSystem bubbles;
        [SerializeField] Renderer foam;
        [SerializeField] Renderer[] ripples;

        [SerializeField] float foamTime = 2.4f;
        [SerializeField] float rippleTime = 1.8f;
        [SerializeField] float rippleDelay = 0.22f;

        static readonly int Tint = Shader.PropertyToID("_TintColor");
        MaterialPropertyBlock block;
        float age = -1f;
        float size;

        // 0.3 for a clean entry up to 1.6 for a cannonball.
        public static float SizeFor(EntryKind entry) => entry switch
        {
            EntryKind.HeadClean => 0.3f,
            EntryKind.FeetClean => 0.45f,
            EntryKind.Head or EntryKind.Feet => 0.7f,
            EntryKind.Cannonball => 1.6f,
            _ => 1.25f
        };

        void Awake() => Hide();

        bool Ready => crown != null && jet != null && bubbles != null && foam != null && ripples != null;

        void Hide()
        {
            if (!Ready)
            {
                Debug.LogWarning("Splash is not wired up; rebuild the scene with Jump Game > Build Prototype Scene.", this);
                return;
            }
            foam.enabled = false;
            foreach (var r in ripples) r.enabled = false;
        }

        public void Play(Vector3 point, float splashSize)
        {
            if (!Ready) return;
            size = splashSize;
            age = 0f;
            transform.position = point;

            // Many small fast drops read as spray; a few big ones read as lumps.
            var main = crown.main;
            main.startSpeedMultiplier = 2.6f + size * 3.4f;
            main.startSizeMultiplier = 0.035f + size * 0.03f;
            crown.Emit(Mathf.RoundToInt(10 + size * 34));

            if (size > 1f)
            {
                var jm = jet.main;
                jm.startSpeedMultiplier = 3.5f + size * 2.5f;
                jet.Emit(Mathf.RoundToInt(size * 12));
            }

            bubbles.transform.position = point + Vector3.down * 0.45f;
            bubbles.Emit(Mathf.RoundToInt(10 + size * 30));

            foam.enabled = true;
            foreach (var r in ripples) r.enabled = true;
            Step(0f);
        }

        void Update()
        {
            if (Application.isPlaying) Step(Time.deltaTime);
        }

        // Also called by editor tools, which have to advance the particles by hand.
        public void Step(float dt)
        {
            if (!Ready) return;
            if (!Application.isPlaying && dt > 0f)
            {
                crown.Simulate(dt, true, false, false);
                jet.Simulate(dt, true, false, false);
                bubbles.Simulate(dt, true, false, false);
            }
            if (age < 0f) return;

            age += dt;
            block ??= new MaterialPropertyBlock();

            float f = age / foamTime;
            if (f < 1f)
            {
                float s = (0.5f + size) * Mathf.Lerp(1f, 1.6f, 1f - (1f - f) * (1f - f));
                foam.transform.localScale = new Vector3(s, 1f, s);
                SetAlpha(foam, 0.85f * (1f - f) * (1f - f));
            }
            else foam.enabled = false;

            float reach = 1.4f + size * 2.4f;
            bool any = false;
            for (int i = 0; i < ripples.Length; i++)
            {
                float t = (age - i * rippleDelay) / rippleTime;
                var r = ripples[i];
                if (t < 0f) { SetAlpha(r, 0f); any = true; continue; }
                if (t >= 1f) { r.enabled = false; continue; }
                any = true;

                float k = 1f - (1f - t) * (1f - t);
                float s = Mathf.Lerp(0.3f, reach * (1f - i * 0.18f), k);
                r.transform.localScale = new Vector3(s, 1f, s);
                SetAlpha(r, 0.7f * (1f - t) * (1f - t));
            }

            if (!any && !foam.enabled) age = -1f;
        }

        void SetAlpha(Renderer r, float a)
        {
            // The legacy particle shader doubles the tint, so 0.5 is neutral.
            block.SetColor(Tint, new Color(0.5f, 0.5f, 0.5f, 0.5f * a));
            r.SetPropertyBlock(block);
        }
    }
}
