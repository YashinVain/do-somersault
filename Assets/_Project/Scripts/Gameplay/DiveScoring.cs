using UnityEngine;

namespace JumpGame
{
    public enum EntryKind
    {
        HeadClean,
        Head,
        FeetClean,
        Feet,
        Cannonball,
        BellyFlop
    }

    public struct DiveResult
    {
        public EntryKind entry;
        public int halfTurns;
        public float multiplier;
        public int score;

        public bool Failed => entry == EntryKind.BellyFlop;

        public string EntryLabel => entry switch
        {
            EntryKind.HeadClean => "Чистый вход!",
            EntryKind.Head => "Головой вниз",
            EntryKind.FeetClean => "Ровно солдатиком",
            EntryKind.Feet => "Солдатиком",
            EntryKind.Cannonball => "Бомбочка!",
            _ => "Плашмя!"
        };

        public string TurnsLabel
        {
            get
            {
                if (halfTurns <= 0) return "без оборота";
                float turns = halfTurns * 0.5f;
                return turns.ToString(turns % 1f == 0f ? "0" : "0.0") + " об.";
            }
        }
    }

    public static class DiveScoring
    {
        const int PointsPerHalfTurn = 60;

        // rotation: total forward rotation in degrees since takeoff (0 = upright).
        public static DiveResult Evaluate(float rotation, float tuck, float multiplier)
        {
            var r = new DiveResult { multiplier = multiplier };
            r.halfTurns = Mathf.Max(0, Mathf.RoundToInt(rotation / 180f));

            float n = Mathf.Repeat(rotation, 360f);
            float fromHead = Mathf.Abs(n - 180f);
            float fromFeet = Mathf.Min(n, 360f - n);

            int bonus;
            if (tuck > 0.5f)
            {
                r.entry = EntryKind.Cannonball;
                r.score = Mathf.RoundToInt((40 + r.halfTurns * 20) * multiplier);
                return r;
            }

            if (fromHead <= 15f) { r.entry = EntryKind.HeadClean; bonus = 200; }
            else if (fromHead <= 35f) { r.entry = EntryKind.Head; bonus = 100; }
            else if (fromFeet <= 15f) { r.entry = EntryKind.FeetClean; bonus = 60; }
            else if (fromFeet <= 35f) { r.entry = EntryKind.Feet; bonus = 30; }
            else
            {
                r.entry = EntryKind.BellyFlop;
                r.score = 0;
                return r;
            }

            r.score = Mathf.RoundToInt((r.halfTurns * PointsPerHalfTurn + bonus) * multiplier);
            return r;
        }
    }
}
