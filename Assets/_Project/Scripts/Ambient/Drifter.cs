using UnityEngine;

namespace JumpGame
{
    // Something light floating on the pool, like an inflatable duck. It wanders slowly
    // and gets shoved away by splashes that land close by.
    public class Drifter : MonoBehaviour
    {
        [SerializeField] Diver diver;
        [SerializeField] Vector2 minXZ = new Vector2(-4f, -2f);
        [SerializeField] Vector2 maxXZ = new Vector2(4f, 14f);
        [SerializeField] float wander = 0.15f;
        [SerializeField] float splashReach = 3.5f;

        Vector3 velocity;
        float spin, seed;

        void Start() => seed = Random.value * 100f;

        void OnEnable()
        {
            if (diver != null) diver.Entered += OnSplash;
        }

        void OnDisable()
        {
            if (diver != null) diver.Entered -= OnSplash;
        }

        void OnSplash(DiveResult r)
        {
            Vector3 d = transform.position - diver.EntryPoint;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist > splashReach || dist < 0.01f) return;
            float k = 1f - dist / splashReach;
            velocity += d / dist * (1.6f * k * Splash.SizeFor(r.entry));
            spin += Random.Range(-90f, 90f) * k;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time * 0.07f + seed;
            var push = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, 0f, Mathf.PerlinNoise(0f, t) - 0.5f) * wander;
            velocity += push * dt;
            velocity *= 1f - Mathf.Min(1f, 0.8f * dt);

            var p = transform.localPosition + velocity * dt;
            // Bounce softly off the pool walls.
            if (p.x < minXZ.x || p.x > maxXZ.x) velocity.x = -velocity.x * 0.5f;
            if (p.z < minXZ.y || p.z > maxXZ.y) velocity.z = -velocity.z * 0.5f;
            p.x = Mathf.Clamp(p.x, minXZ.x, maxXZ.x);
            p.z = Mathf.Clamp(p.z, minXZ.y, maxXZ.y);
            p.y = WaterMotion.Height(transform.position) + Mathf.Sin(Time.time * 1.8f + seed) * 0.02f;
            transform.localPosition = p;

            spin = Mathf.MoveTowards(spin, 6f, 40f * dt);
            transform.Rotate(0f, spin * dt, 0f, Space.World);
        }
    }
}
