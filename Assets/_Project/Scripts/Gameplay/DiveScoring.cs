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
        BellyFlop,
        Slipped,
        HitTower,
        OnTower
    }

    public struct DiveResult
    {
        public EntryKind entry;
        public int halfTurns;
        public bool inZone;
        public bool hitBuoys;
        public float multiplier;
        public int score;

        public bool Failed => entry >= EntryKind.BellyFlop;

        public string EntryLabel => entry switch
        {
            EntryKind.HeadClean => "Чистый вход!",
            EntryKind.Head => "Головой вниз",
            EntryKind.FeetClean => "Ровно солдатиком",
            EntryKind.Feet => "Солдатиком",
            EntryKind.Cannonball => "Бомбочка!",
            EntryKind.Slipped => "Сорвался!",
            EntryKind.HitTower => "Удар о вышку!",
            EntryKind.OnTower => "Остался на вышке",
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
        const int PointsPerHalfTurn = 100;

        // Slipped, hit the tower or never left it: no points.
        public static DiveResult Fail(EntryKind kind, bool inZone) => new DiveResult
        {
            entry = kind,
            inZone = inZone,
            multiplier = 1f
        };

        // rotation: total forward rotation in degrees since leaving the stand (0 = upright).
        // Touching the buoy rope cancels the zone bonus.
        public static DiveResult Evaluate(float rotation, float tuck, bool inZone, bool hitBuoys, float zoneMultiplier)
        {
            var r = new DiveResult
            {
                inZone = inZone,
                hitBuoys = hitBuoys,
                multiplier = inZone && !hitBuoys ? zoneMultiplier : 1f,
                halfTurns = Mathf.Max(0, Mathf.RoundToInt(rotation / 180f))
            };

            float n = Mathf.Repeat(rotation, 360f);
            float fromHead = Mathf.Abs(n - 180f);
            float fromFeet = Mathf.Min(n, 360f - n);

            int bonus;
            if (tuck > 0.5f)
            {
                r.entry = EntryKind.Cannonball;
                r.score = Mathf.RoundToInt((50 + r.halfTurns * 30) * r.multiplier);
                return r;
            }

            if (fromHead <= 15f) { r.entry = EntryKind.HeadClean; bonus = 150; }
            else if (fromHead <= 35f) { r.entry = EntryKind.Head; bonus = 75; }
            else if (fromFeet <= 15f) { r.entry = EntryKind.FeetClean; bonus = 60; }
            else if (fromFeet <= 35f) { r.entry = EntryKind.Feet; bonus = 30; }
            else
            {
                r.entry = EntryKind.BellyFlop;
                r.score = 0;
                return r;
            }

            r.score = Mathf.RoundToInt((r.halfTurns * PointsPerHalfTurn + bonus) * r.multiplier);
            return r;
        }
    }
}
