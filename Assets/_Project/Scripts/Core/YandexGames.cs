using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace JumpGame
{
    [Serializable]
    public class LeaderboardEntry
    {
        public int rank;
        public string name;
        public int score;
    }

    [Serializable]
    class LeaderboardReply
    {
        public LeaderboardEntry[] items = new LeaderboardEntry[0];
    }

    // The game's one door to the Yandex Games platform: cloud save, ads, the leaderboard and the "loaded" signal.
    // Outside a browser (the editor, a Windows build) every call is simulated, so the game plays the same everywhere.
    // The object must be called "YandexGames": the JavaScript side finds it by that name.
    public class YandexGames : MonoBehaviour
    {
        public const string Board = "totalscore";

        static YandexGames instance;

        public static bool Online { get; private set; }
        public static string Language { get; private set; } = "ru";
        public static bool AdShowing { get; private set; }

        // Called once with the saved JSON from the cloud (empty when there is none).
        public static event Action<string> CloudData;

        // The platform allows about one save a second, so changes wait here and go out together.
        string pendingSave;
        int pendingScore = -1;
        float nextSend;

        Action rewarded, afterAd;
        Action<LeaderboardEntry[]> boardCallback;
        bool gotReward, readySent;
        float savedTimeScale = 1f;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void YG_Init(string objectName);
        [DllImport("__Internal")] static extern void YG_Ready();
        [DllImport("__Internal")] static extern void YG_GameplayStart();
        [DllImport("__Internal")] static extern void YG_GameplayStop();
        [DllImport("__Internal")] static extern void YG_LoadData();
        [DllImport("__Internal")] static extern void YG_SaveData(string json);
        [DllImport("__Internal")] static extern void YG_ShowInterstitial();
        [DllImport("__Internal")] static extern void YG_ShowRewarded();
        [DllImport("__Internal")] static extern void YG_SetScore(string board, int score);
        [DllImport("__Internal")] static extern void YG_LoadLeaderboard(string board);
#endif

        void Awake()
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            YG_Init(gameObject.name);
#else
            // Local runs behave as an offline player with no cloud data; a moment later, so the game has subscribed.
            Invoke(nameof(OfflineInit), 0.15f);
#endif
        }

        void OfflineInit() => OnYandexInit("offline|ru");

        void Update()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!Online || Time.unscaledTime < nextSend || (pendingSave == null && pendingScore < 0)) return;
            if (pendingSave != null) YG_SaveData(pendingSave);
            if (pendingScore >= 0) YG_SetScore(Board, pendingScore);
            pendingSave = null;
            pendingScore = -1;
            nextSend = Time.unscaledTime + 2f;
#endif
        }

        // ---------------------------------------------------------------- Calls from the game

        // Tells the platform the game can be played: once, when the title screen is up.
        public static void LoadingFinished()
        {
            if (instance == null || instance.readySent) return;
            instance.readySent = true;
#if UNITY_WEBGL && !UNITY_EDITOR
            YG_Ready();
#endif
        }

        public static void GameplayStart()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (instance != null) YG_GameplayStart();
#endif
        }

        public static void GameplayStop()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (instance != null) YG_GameplayStop();
#endif
        }

        public static void SaveData(string json)
        {
            if (instance != null) instance.pendingSave = json;
        }

        public static void SetScore(int score)
        {
            if (instance != null) instance.pendingScore = score;
        }

        // A full-screen ad at a natural pause; `done` runs when it is over or could not be shown.
        public static void ShowInterstitial(Action done)
        {
            if (instance == null || instance.afterAd != null) { done?.Invoke(); return; }
            instance.afterAd = done;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Online) { YG_ShowInterstitial(); return; }
#endif
            instance.OnAdClosed("0");
        }

        // A video the player chooses to watch for a reward; `onReward` runs only if it was watched to the end.
        public static void ShowRewarded(Action onReward, Action done = null)
        {
            if (instance == null || instance.afterAd != null) { done?.Invoke(); return; }
            instance.rewarded = onReward;
            instance.afterAd = done;
            instance.gotReward = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Online) { YG_ShowRewarded(); return; }
#endif
            // No platform here: give the reward at once so the buttons can be tried.
            instance.OnAdOpened("");
            instance.OnAdRewarded("");
            instance.OnAdClosed("1");
        }

        public static void LoadLeaderboard(Action<LeaderboardEntry[]> callback)
        {
            if (instance == null) { callback?.Invoke(new LeaderboardEntry[0]); return; }
            instance.boardCallback = callback;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Online) { YG_LoadLeaderboard(Board); return; }
#endif
            instance.OnLeaderboard("{\"items\":[{\"rank\":1,\"name\":\"Тестовый игрок\",\"score\":12500},{\"rank\":2,\"name\":\"Ныряльщик\",\"score\":9800},{\"rank\":3,\"name\":\"Вы\",\"score\":4200}]}");
        }

        // ---------------------------------------------------------------- Calls from JavaScript (SendMessage)

        public void OnYandexInit(string reply)
        {
            var parts = reply.Split('|');
            Online = parts[0] == "ok";
            if (parts.Length > 1 && parts[1].Length > 0) Language = parts[1];

#if UNITY_WEBGL && !UNITY_EDITOR
            if (Online) { YG_LoadData(); return; }
#endif
            CloudData?.Invoke("");
        }

        public void OnYandexData(string json) => CloudData?.Invoke(json ?? "");

        // The platform pauses the game for its own overlays; sound and time follow.
        public void OnYandexPause(string flag) => Hold(flag == "1");

        public void OnAdOpened(string _) => Hold(true);

        public void OnAdRewarded(string _) => gotReward = true;

        public void OnAdClosed(string shown)
        {
            Hold(false);
            var reward = rewarded;
            var after = afterAd;
            rewarded = null;
            afterAd = null;
            if (gotReward) reward?.Invoke();
            gotReward = false;
            after?.Invoke();
        }

        public void OnLeaderboard(string json)
        {
            var cb = boardCallback;
            boardCallback = null;
            LeaderboardEntry[] items;
            try { items = JsonUtility.FromJson<LeaderboardReply>(json).items; }
            catch (ArgumentException) { items = new LeaderboardEntry[0]; }
            cb?.Invoke(items);
        }

        // Silence and stillness while an ad or the platform overlay is on top.
        void Hold(bool on)
        {
            if (on == AdShowing) return;
            AdShowing = on;
            if (on)
            {
                savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = savedTimeScale <= 0f ? 1f : savedTimeScale;
            }
            AudioListener.pause = on;
        }
    }
}
