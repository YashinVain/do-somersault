using UnityEngine;

namespace JumpGame
{
    // Swell shared by everything that floats: the sea surface, the buoy rope, ducks.
    // A flat pool simply has zero amplitude.
    public static class WaterMotion
    {
        static float amplitude, period = 4f, wavelength = 14f;
        static Vector2 direction = new Vector2(0.3f, 1f).normalized;

        public static float Amplitude => amplitude;

        public static void Set(float newAmplitude, float newPeriod, float newWavelength)
        {
            amplitude = newAmplitude;
            period = Mathf.Max(0.5f, newPeriod);
            wavelength = Mathf.Max(1f, newWavelength);
        }

        public static float Height(Vector3 at) => Height(at, Time.time);

        // Two crossing swells so the surface doesn't look like a single sliding sheet.
        public static float Height(Vector3 at, float time)
        {
            if (amplitude <= 0f) return 0f;
            float phase = (at.x * direction.x + at.z * direction.y) / wavelength - time / period;
            float cross = (at.x * 0.8f - at.z * 0.6f) / (wavelength * 0.63f) - time / (period * 0.77f);
            return amplitude * (Mathf.Sin(phase * Mathf.PI * 2f) * 0.7f + Mathf.Sin(cross * Mathf.PI * 2f) * 0.3f);
        }
    }
}
