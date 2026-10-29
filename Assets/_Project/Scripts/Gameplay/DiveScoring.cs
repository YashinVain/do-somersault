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
        public bool bullseye;
        public bool hitBuoys;
        public bool usedTuck;
        public float multiplier;
        public int score;
        // A line about the character's twist on this dive, shown on the result screen.
        public string note;

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
                string s = turns.ToString(turns % 1f == 0f ? "0" : "0.0", System.Globalization.CultureInfo.InvariantCulture);
                return s.Replace('.', ',') + " об.";
            }
        }
    }

    public static class DiveScoring
    {
        // Applies the character's perk to a finished dive: bonuses, a consolation score for failures, a note for the screen.
        public static void ApplyPerk(ref DiveResult r, Perk perk)
        {
            if (perk == null || perk == Perk.None) return;

            if (r.Failed)
            {
                if (perk.failScore > 0 && r.entry != EntryKind.OnTower)
                {
                    r.score = perk.failScore;
                    r.note = "Не больно!";
                }
            }
            else
            {
                float m = r.multiplier;
                if (r.entry == EntryKind.HeadClean) r.score += Mathf.RoundToInt(perk.headClean * m);
                if (r.entry == EntryKind.FeetClean) r.score += Mathf.RoundToInt(perk.feetClean * m);
                if (r.bullseye) r.score += Mathf.RoundToInt(perk.bullseye * m);
                if (r.entry == EntryKind.Cannonball) r.score = Mathf.RoundToInt(r.score * perk.cannonball);
                if (perk.splash > 1.5f && r.entry == EntryKind.Cannonball) r.note = "Плюх!";
            }
            if (perk.breaks) r.note = "Робот замкнул и сломался";
        }

        public const int PointsPerHalfTurn = 100;
        public const int CenterBonus = 100;

        // Slipped, hit the tower or never left it: no points.
        public static DiveResult Fail(EntryKind kind, bool inZone) => new DiveResult
        {
            entry = kind,
            inZone = inZone,
            multiplier = 1f
        };

        // rotation: total forward rotation in degrees since leaving the stand (0 = upright).
        // Only dives inside the ring score. Touching the rope on the way in cancels the ring bonus,
        // hitting the small mark in the middle adds a flat bonus before the multiplier.
        public static DiveResult Evaluate(float rotation, float tuck, bool inZone, bool bullseye, bool hitBuoys, float zoneMultiplier)
        {
            var r = new DiveResult
            {
                inZone = inZone,
                bullseye = inZone && bullseye,
                hitBuoys = hitBuoys,
                multiplier = !inZone ? 0f : hitBuoys ? 1f : zoneMultiplier,
                halfTurns = Mathf.Max(0, Mathf.RoundToInt(rotation / 180f))
            };
            int center = r.bullseye ? CenterBonus : 0;

            float n = Mathf.Repeat(rotation, 360f);
            float fromHead = Mathf.Abs(n - 180f);
            float fromFeet = Mathf.Min(n, 360f - n);

            int bonus;
            if (tuck > 0.5f)
            {
                r.entry = EntryKind.Cannonball;
                r.score = Mathf.RoundToInt((50 + r.halfTurns * 30 + center) * r.multiplier);
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

            r.score = Mathf.RoundToInt((r.halfTurns * PointsPerHalfTurn + bonus + center) * r.multiplier);
            return r;
        }
    }
}
