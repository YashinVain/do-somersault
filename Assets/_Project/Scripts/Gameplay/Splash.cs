using UnityEngine;

namespace JumpGame
{
    [RequireComponent(typeof(ParticleSystem))]
    public class Splash : MonoBehaviour
    {
        [SerializeField] Transform ripple;
        [SerializeField] float rippleTime = 1.2f;

        ParticleSystem drops;
        float rippleAge = -1f;
        float rippleSize;

        void Awake()
        {
            drops = GetComponent<ParticleSystem>();
            if (ripple != null) ripple.gameObject.SetActive(false);
        }

        public void Play(Vector3 point, EntryKind entry)
        {
            float size = entry switch
            {
                EntryKind.HeadClean => 0.35f,
                EntryKind.FeetClean => 0.5f,
                EntryKind.Head or EntryKind.Feet => 0.75f,
                EntryKind.Cannonball => 1.6f,
                _ => 1.3f
            };

            transform.position = point;
            var main = drops.main;
            main.startSpeedMultiplier = 3f + size * 4f;
            main.startSizeMultiplier = 0.12f + size * 0.08f;
            drops.Emit(Mathf.RoundToInt(15 + size * 60));

            rippleSize = 1.2f + size * 2.5f;
            rippleAge = 0f;
            if (ripple != null)
            {
                ripple.position = point + Vector3.up * 0.02f;
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
            ripple.localScale = new Vector3(s, 0.01f, s);
        }
    }
}
