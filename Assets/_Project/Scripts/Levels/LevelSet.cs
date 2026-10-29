using System;
using System.Globalization;
using UnityEngine;

namespace JumpGame
{
    public enum TaskKind
    {
        HeadFirst,
        FeetFirst,
        Turns,
        CleanTurns,
        CannonballInZone,
        ZoneNoBuoys,
        NoTuck,
        Bullseye
    }

    [Serializable]
    public class LevelInfo
    {
        [Tooltip("Index into LevelSet.locations.")]
        public int location;
        [Tooltip("Index of the platform on that location's tower.")]
        public int platform;
        [Tooltip("Distance from the platform edge to the centre of the buoy ring, metres.")]
        public float zoneDistance = 4f;
        public float zoneRadius = 1.3f;
        [Tooltip("The ring drifts sideways by this many metres each way. 0 keeps it still.")]
        public float zoneDrift;
        public int bronze = 300, silver = 600, gold = 900;
        public TaskKind task;
        public float taskTurns;

        public int StarsFor(int score) => score >= gold ? 3 : score >= silver ? 2 : score >= bronze ? 1 : 0;

        public string TaskText => task switch
        {
            TaskKind.HeadFirst => "Войди в воду головой",
            TaskKind.FeetFirst => "Войди в воду солдатиком",
            TaskKind.Turns => "Сделай " + Turns(taskTurns),
            TaskKind.CleanTurns => "Чистый вход и " + Turns(taskTurns),
            TaskKind.CannonballInZone => "Бомбочка в зону",
            TaskKind.NoTuck => "Попади в зону без группировки",
            TaskKind.Bullseye => "Попади в самый центр круга",
            _ => "Попади в зону, не задев буйки"
        };

        public bool TaskDone(DiveResult r)
        {
            float turns = r.halfTurns * 0.5f;
            bool cleanZone = r.inZone && !r.hitBuoys;
            return task switch
            {
                TaskKind.HeadFirst => r.entry == EntryKind.HeadClean || r.entry == EntryKind.Head,
                TaskKind.FeetFirst => r.entry == EntryKind.FeetClean || r.entry == EntryKind.Feet,
                TaskKind.Turns => !r.Failed && turns >= taskTurns,
                TaskKind.CleanTurns => r.entry == EntryKind.HeadClean && turns >= taskTurns,
                TaskKind.CannonballInZone => r.entry == EntryKind.Cannonball && cleanZone,
                TaskKind.NoTuck => !r.Failed && !r.usedTuck && cleanZone,
                TaskKind.Bullseye => !r.Failed && r.bullseye,
                _ => !r.Failed && cleanZone
            };
        }

        static string Turns(float n)
        {
            // Culture data may be stripped in WebGL builds, so the decimal comma is put in by hand.
            string num = n.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',');
            if (n % 1f != 0f) return num + " оборота";
            int i = Mathf.RoundToInt(n);
            return num + (i == 1 ? " оборот" : i < 5 ? " оборота" : " оборотов");
        }
    }

    [CreateAssetMenu(menuName = "Jump Game/Level Set")]
    public class LevelSet : ScriptableObject
    {
        public string[] locations;
        [Tooltip("Coins to unlock each location, 0 for the ones that open by playing. Same order as locations.")]
        public int[] locationPrices;
        public LevelInfo[] levels;

        public int PriceOf(int location) => locationPrices != null && location < locationPrices.Length ? locationPrices[location] : 0;

        // Position of a level inside its location, counting from 1, and how many the location has.
        public (int number, int count) PlaceInLocation(int index)
        {
            int loc = levels[index].location, number = 0, count = 0;
            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i].location != loc) continue;
                count++;
                if (i <= index) number++;
            }
            return (number, count);
        }
    }
}
