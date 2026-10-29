using UnityEngine;

namespace JumpGame
{
    [RequireComponent(typeof(ParticleSystem))]
    public class Splash : MonoBehaviour
    {
        [SerializeField] Transform ripple;
        [SerializeField] float rippleTime = 1.4f;

        ParticleSystem drops;
        float rippleAge = -1f;
        float rippleSize;

        ParticleSystem Drops => drops != null ? drops : drops = GetComponent<ParticleSystem>();

        // 0.3 for a clean entry up to 1.6 for a cannonball.
        public static float SizeFor(EntryKind entry) => entry switch
        {
            EntryKind.HeadClean => 0.3f,
            EntryKind.FeetClean => 0.45f,
            EntryKind.Head or EntryKind.Feet => 0.7f,
            EntryKind.Cannonball => 1.6f,
            _ => 1.25f
        };

        void Awake()
        {
            if (ripple != null) ripple.gameObject.SetActive(false);
        }

        public void Play(Vector3 point, float size)
        {
            transform.position = point;
            var main = Drops.main;
            main.startSpeedMultiplier = 2.5f + size * 3.5f;
            main.startSizeMultiplier = 0.08f + size * 0.06f;
            Drops.Emit(Mathf.RoundToInt(12 + size * 50));

            rippleSize = 1f + size * 2.2f;
            rippleAge = 0f;
            if (ripple != null)
            {
                ripple.position = point + Vector3.up * 0.015f;
                ripple.gameObject.SetActive(true);
            }
        }

        void Update()
        {
            if (rippleAge < 0f || ripple == null) return;

            rippleAge += Time.deltaTime;
            float t = rippleAge / rippleTime;
            if (t >= 1f)
            {
                ripple.gameObject.SetActive(false);
                rippleAge = -1f;
                return;
            }

            float s = Mathf.Lerp(0.2f, rippleSize, 1f - (1f - t) * (1f - t));
            float thin = Mathf.Lerp(1f, 0.2f, t);
            ripple.localScale = new Vector3(s, thin, s);
        }
    }
}
