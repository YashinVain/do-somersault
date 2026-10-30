using UnityEngine;
using UnityEngine.UI;

namespace JumpGame
{
    // The top of the all-time leaderboard, opened from the title screen.
    public class LeaderboardScreen : MonoBehaviour
    {
        [SerializeField] UiPanel panel;
        [SerializeField] UiPanel titlePanel;
        [SerializeField] Button openButton, backButton;
        [SerializeField] Text[] names, scores;
        [SerializeField] Text status;

        void Start()
        {
            openButton.onClick.AddListener(Open);
            backButton.onClick.AddListener(Close);
            panel.Show(false, true);
        }

        public bool IsOpen => panel.Visible;

        void Open()
        {
            titlePanel.Show(false);
            panel.Show(true);
            Clear("Загрузка…");
            YandexGames.LoadLeaderboard(Fill);
        }

        public void Close()
        {
            panel.Show(false);
            titlePanel.Show(true);
        }

        void Clear(string message)
        {
            status.text = message;
            for (int i = 0; i < names.Length; i++)
            {
                names[i].text = "";
                scores[i].text = "";
            }
        }

        void Fill(LeaderboardEntry[] items)
        {
            if (!panel.Visible) return;
            if (items == null || items.Length == 0)
            {
                Clear(YandexGames.Online ? "Пока никого нет. Сыграй и попади в рейтинг!" : "Рейтинг доступен на Яндекс Играх");
                return;
            }

            status.text = "";
            for (int i = 0; i < names.Length; i++)
            {
                bool used = i < items.Length;
                names[i].text = used ? $"{items[i].rank}.   {items[i].name}" : "";
                scores[i].text = used ? items[i].score.ToString() : "";
            }
        }
    }
}
