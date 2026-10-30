using System;
using UnityEngine;

namespace JumpGame
{
    [Serializable]
    public class ProgressData
    {
        public int last;
        public int[] best = new int[0];
        public int[] stars = new int[0];
        public bool[] tasks = new bool[0];

        public int coins;
        public int skin;
        public bool[] ownedSkins = new bool[0];
        public bool[] ownedLocations = new bool[0];

        public int TotalScore
        {
            get
            {
                int sum = 0;
                foreach (int b in best) sum += b;
                return sum;
            }
        }

        // How far along the save is, to tell a cloud save from a local one: the more advanced one is kept.
        public int Weight
        {
            get
            {
                int w = 0;
                foreach (int s in stars) w += s * 100;
                foreach (bool o in ownedSkins) if (o) w += 50;
                foreach (bool o in ownedLocations) if (o) w += 200;
                return w + TotalScore / 10 + coins / 20;
            }
        }

        public void Fit(int levels, int skins, int locations)
        {
            if (best.Length < levels) Array.Resize(ref best, levels);
            if (stars.Length < levels) Array.Resize(ref stars, levels);
            if (tasks.Length < levels) Array.Resize(ref tasks, levels);
            if (ownedSkins.Length < skins) Array.Resize(ref ownedSkins, skins);
            if (ownedLocations.Length < locations) Array.Resize(ref ownedLocations, locations);
            last = Mathf.Clamp(last, 0, levels - 1);
            skin = Mathf.Clamp(skin, 0, skins - 1);
            ownedSkins[0] = true;
            coins = Mathf.Max(0, coins);
        }
    }

    // Local storage for now. The Yandex cloud save will sit behind the same two calls.
    public static class Progress
    {
        // v3: coins, characters and bought locations; levels open by medals instead of a counter.
        const string Key = "jg_progress_v3";

        public static ProgressData Load(int levels, int skins, int locations)
        {
            ProgressData data = null;
            try
            {
                string json = PlayerPrefs.GetString(Key, "");
                if (json.Length > 0) data = JsonUtility.FromJson<ProgressData>(json);
            }
            catch (ArgumentException)
            {
                // Corrupted entry: start over rather than break the game.
            }

            data ??= new ProgressData();
            data.Fit(levels, skins, locations);
            return data;
        }

        // Writes straight to the save slot; the cloud copy goes out a moment later.
        public static void Save(ProgressData data)
        {
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(Key, json);
            PlayerPrefs.Save();
            YandexGames.SaveData(json);
            YandexGames.SetScore(data.TotalScore);
        }
    }
}
