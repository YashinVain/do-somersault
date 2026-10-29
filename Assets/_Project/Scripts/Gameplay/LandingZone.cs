using System.Collections.Generic;
using UnityEngine;

namespace JumpGame
{
    // Area on the water fenced off by a rope of floats. Landing inside multiplies the score.
    // Floats are springs tied to their neighbours, so a hit or a wave travels along the rope.
    public class LandingZone : MonoBehaviour
    {
        [SerializeField] float radius = 1.3f;
        [SerializeField] float insideMultiplier = 2f;
        [SerializeField] Transform[] floats;
        [SerializeField] LineRenderer rope;

        [Header("Float physics")]
        [SerializeField] float stiffness = 26f;
        [SerializeField] float damping = 3.2f;
        [SerializeField] float ropeCoupling = 40f;
        [SerializeField] float contactRadius = 0.22f;
        [SerializeField] float contactHeight = 0.35f;
        [SerializeField] float idleBob = 0.012f;

        [Header("Waves")]
        [SerializeField] float waveSpeed = 2.2f;
        [SerializeField] float waveWidth = 0.35f;
        [SerializeField] float waveDecay = 1.1f;

        struct Wave
        {
            public Vector3 origin;
            public float start, amplitude;
        }

        Vector3[] rest, offset, velocity;
        Quaternion[] restRotation;
        readonly List<Wave> waves = new List<Wave>();
        float time;

        public float WaterLevel => transform.position.y;
        public float Radius => radius;
        public float InsideMultiplier => insideMultiplier;

        public bool Contains(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= radius * radius;
        }

        void Init()
        {
            int n = floats.Length;
            rest = new Vector3[n];
            offset = new Vector3[n];
            velocity = new Vector3[n];
            restRotation = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                rest[i] = floats[i].localPosition;
                restRotation[i] = floats[i].localRotation;
            }
            if (rope != null)
            {
                rope.positionCount = n;
                rope.loop = true;
            }
        }

        void Update() => Step(Time.deltaTime);

        public void Step(float dt)
        {
            if (rest == null) Init();
            if (dt <= 0f) return;
            time += dt;
            waves.RemoveAll(w => time - w.start > 4f);

            int n = floats.Length;
            for (int i = 0; i < n; i++)
            {
                Vector3 prev = offset[(i + n - 1) % n], next = offset[(i + 1) % n];
                Vector3 force = -stiffness * offset[i] - damping * velocity[i]
                              + ropeCoupling * (prev + next - 2f * offset[i]);
                velocity[i] += force * dt;
            }

            for (int i = 0; i < n; i++)
            {
                offset[i] += velocity[i] * dt;
                offset[i].y = Mathf.Clamp(offset[i].y, -0.3f, 0.3f);

                Vector3 world = transform.position + rest[i];
                float wave = WaveHeight(world);
                float bob = Mathf.Sin(time * 1.7f + i * 0.6f) * idleBob;

                floats[i].localPosition = rest[i] + offset[i] + Vector3.up * (wave + bob);
                Vector3 v = velocity[i];
                floats[i].localRotation = Quaternion.Euler(v.z * 14f, 0f, -v.x * 14f) * restRotation[i];
                if (rope != null) rope.SetPosition(i, floats[i].position);
            }
        }

        float WaveHeight(Vector3 at)
        {
            float h = 0f;
            foreach (var w in waves)
            {
                float age = time - w.start;
                Vector3 d = at - w.origin;
                d.y = 0f;
                float x = (d.magnitude - waveSpeed * age) / waveWidth;
                if (x * x > 16f) continue;
                h += w.amplitude * Mathf.Exp(-waveDecay * age) * Mathf.Exp(-x * x) * Mathf.Cos(x * 1.6f);
            }
            return h;
        }

        // Body points pushing into the rope. Returns true if anything was touched.
        public bool Push(Vector3[] points, Vector3 bodyVelocity)
        {
            if (rest == null) Init();

            bool touched = false;
            Vector3 center = transform.position;
            foreach (var p in points)
            {
                if (Mathf.Abs(p.y - center.y) > contactHeight) continue;
                Vector3 flat = p - center;
                flat.y = 0f;
                if (Mathf.Abs(flat.magnitude - radius) > contactRadius) continue;

                for (int i = 0; i < floats.Length; i++)
                {
                    Vector3 d = floats[i].position - p;
                    d.y *= 0.6f;
                    float dist = d.magnitude;
                    if (dist >= contactRadius) continue;

                    Vector3 away = dist > 0.001f ? d / dist : Vector3.up;
                    float depth = contactRadius - dist;
                    offset[i] += away * depth * 0.8f + Vector3.down * depth * 0.5f;
                    velocity[i] += bodyVelocity * 0.3f + away * 1.5f;
                    touched = true;
                }
            }
            return touched;
        }

        // A splash: a ring wave plus a direct shove for floats close to the entry.
        public void MakeWave(Vector3 origin, float size)
        {
            if (rest == null) Init();
            waves.Add(new Wave { origin = origin, start = time, amplitude = 0.04f + size * 0.07f });

            float reach = 0.8f + size * 0.6f;
            for (int i = 0; i < floats.Length; i++)
            {
                Vector3 d = floats[i].position - origin;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist > reach) continue;
                float k = 1f - dist / reach;
                Vector3 away = dist > 0.001f ? d / dist : Vector3.forward;
                velocity[i] += (away * 1.8f + Vector3.up * 1.2f) * size * k;
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Vector3 prev = transform.position + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2f;
                Vector3 p = transform.position + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
#endif
    }
}
