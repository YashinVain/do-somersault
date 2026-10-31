using System.Collections.Generic;
using UnityEngine;

namespace JumpGame
{
    // Swell shared by everything that floats: the sea surface, the buoy rope, ducks.
    // A flat pool simply has zero amplitude. On top of the swell run the ring waves of splashes: a body going into the water
    // pushes a wave outwards that lifts the surface mesh, shoves the buoys and is drawn as foam by the water shader.
    public static class WaterMotion
    {
        static float amplitude, period = 4f, wavelength = 14f;
        static Vector2 direction = new Vector2(0.3f, 1f).normalized;

        public static float Amplitude => amplitude;

        struct Impact
        {
            public Vector3 origin;
            public float start, strength;
        }

        const int MaxImpacts = 4;
        const float ImpactLife = 5f, WaveSpeed = 2.6f, WaveWidth = 0.35f, WaveDecay = 0.9f;
        static readonly List<Impact> impacts = new List<Impact>();
        static readonly Vector4[] shaderImpacts = new Vector4[MaxImpacts];
        static int pushedFrame = -1;
        static readonly int ImpactsId = Shader.PropertyToID("_Impacts");

        public static bool Rippling => impacts.Count > 0;

        public static void Set(float newAmplitude, float newPeriod, float newWavelength)
        {
            amplitude = newAmplitude;
            period = Mathf.Max(0.5f, newPeriod);
            wavelength = Mathf.Max(1f, newWavelength);
        }

        // A splash at a point: size is the game's splash size (about 0.4 for a soldier up to 2+ for a belly flop).
        public static void AddImpact(Vector3 origin, float size)
        {
            if (impacts.Count >= MaxImpacts) impacts.RemoveAt(0);
            impacts.Add(new Impact { origin = origin, start = Time.time, strength = Mathf.Clamp(0.35f + size * 0.45f, 0.3f, 2f) });
        }

        public static void Clear()
        {
            impacts.Clear();
            pushedFrame = -1;
            for (int i = 0; i < MaxImpacts; i++) shaderImpacts[i] = Vector4.zero;
            Shader.SetGlobalVectorArray(ImpactsId, shaderImpacts);
        }

        // Called once a frame by the water; forgets old impacts and hands the live ones to the shader.
        public static void Tick()
        {
            if (pushedFrame == Time.frameCount) return;
            pushedFrame = Time.frameCount;
            impacts.RemoveAll(i => Time.time - i.start > ImpactLife);
            for (int i = 0; i < MaxImpacts; i++)
            {
                if (i < impacts.Count)
                {
                    var s = impacts[i];
                    shaderImpacts[i] = new Vector4(s.origin.x, s.origin.z, Time.time - s.start, s.strength);
                }
                else shaderImpacts[i] = Vector4.zero;
            }
            Shader.SetGlobalVectorArray(ImpactsId, shaderImpacts);
        }

        public static float Height(Vector3 at) => Height(at, Time.time);

        // Two crossing swells so the surface doesn't look like a single sliding sheet, plus the ring waves of recent splashes.
        public static float Height(Vector3 at, float time)
        {
            float h = 0f;
            if (amplitude > 0f)
            {
                float phase = (at.x * direction.x + at.z * direction.y) / wavelength - time / period;
                float cross = (at.x * 0.8f - at.z * 0.6f) / (wavelength * 0.63f) - time / (period * 0.77f);
                h = amplitude * (Mathf.Sin(phase * Mathf.PI * 2f) * 0.7f + Mathf.Sin(cross * Mathf.PI * 2f) * 0.3f);
            }

            for (int i = 0; i < impacts.Count; i++)
            {
                var s = impacts[i];
                float age = time - s.start;
                if (age < 0f) continue;
                float dx = at.x - s.origin.x, dz = at.z - s.origin.z;
                float x = (Mathf.Sqrt(dx * dx + dz * dz) - WaveSpeed * age) / (WaveWidth + age * 0.12f);
                if (x * x > 16f) continue;
                h += 0.12f * s.strength * Mathf.Exp(-WaveDecay * age) * Mathf.Exp(-x * x) * Mathf.Cos(x * 1.6f);
            }
            return h;
        }
    }
}
