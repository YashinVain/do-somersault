using UnityEngine;

namespace JumpGame
{
    // Three judges, 0 to 10 in half points. The middle one is strict, the right one soft-hearted.
    public static class JudgePanel
    {
        static readonly float[] Temper = { 0f, -0.5f, 0.3f };

        public static float[] Score(DiveResult r, int seed)
        {
            float b = r.entry switch
            {
                EntryKind.HeadClean => 9f,
                EntryKind.FeetClean => 8f,
                EntryKind.Head => 7.5f,
                EntryKind.Feet => 6.5f,
                EntryKind.Cannonball => 4f,
                EntryKind.BellyFlop => 1.5f,
                EntryKind.OnTower => 0f,
                _ => 0.5f
            };

            if (!r.Failed)
            {
                // Harder dives impress a little more; a sloppy finish costs.
                b += Mathf.Min(1f, r.halfTurns * 0.25f);
                if (!r.inZone) b -= 2f;
                else if (r.hitBuoys) b -= 1f;
            }

            var rng = new System.Random(seed);
            var scores = new float[3];
            for (int i = 0; i < 3; i++)
            {
                float noise = ((float)rng.NextDouble() - 0.5f) * 1f;
                float s = Mathf.Clamp(b + Temper[i] + noise, 0f, 10f);
                scores[i] = Mathf.Round(s * 2f) * 0.5f;
            }
            return scores;
        }

        public static string Format(float s) => s.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
    }
}
