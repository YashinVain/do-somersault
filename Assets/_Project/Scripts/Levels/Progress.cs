using System;
using UnityEngine;

namespace JumpGame
{
    [Serializable]
    public class ProgressData
    {
        public int unlocked = 1;
        public int last;
        public int[] best = new int[0];
        public int[] stars = new int[0];
        public bool[] tasks = new bool[0];

        public void Fit(int count)
        {
            if (best.Length < count) Array.Resize(ref best, count);
            if (stars.Length < count) Array.Resize(ref stars, count);
            if (tasks.Length < count) Array.Resize(ref tasks, count);
            unlocked = Mathf.Clamp(unlocked, 1, count);
            last = Mathf.Clamp(last, 0, count - 1);
        }
    }

    // Local storage for now. The Yandex cloud save will sit behind the same two calls.
    public static class Progress
    {
        const string Key = "jg_progress_v1";

        public static ProgressData Load(int levelCount)
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
            data.Fit(levelCount);
            return data;
        }

        public static void Save(ProgressData data)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
