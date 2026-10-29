using UnityEngine;
using UnityEngine.UI;

namespace JumpGame
{
    // Title screen and level grid, drawn over the pool. The game HUD is hidden while either is open.
    public class MenuScreen : MonoBehaviour
    {
        [SerializeField] GameFlow flow;
        [SerializeField] Diver diver;
        [SerializeField] GameObject hud;

        [Header("Title")]
        [SerializeField] GameObject mainPanel;
        [SerializeField] Button playButton, levelsButton;
        [SerializeField] Text playLabel;
        [SerializeField] Text medalCount;

        [Header("Levels")]
        [SerializeField] GameObject levelsPanel;
        [SerializeField] Button backButton;
        [SerializeField] RectTransform grid;
        [SerializeField] Button cardTemplate;

        [Header("In game")]
        [SerializeField] Button menuButton;

        static readonly Color Open = new Color(0.17f, 0.64f, 0.61f);
        static readonly Color Current = new Color(0.93f, 0.42f, 0.33f);
        static readonly Color Locked = new Color(0.28f, 0.32f, 0.4f, 0.85f);

        Button[] cards;

        void Start()
        {
            playButton.onClick.AddListener(Play);
            levelsButton.onClick.AddListener(ShowLevels);
            backButton.onClick.AddListener(ShowTitle);
            menuButton.onClick.AddListener(OpenFromGame);
            cardTemplate.gameObject.SetActive(false);
            if (flow.Saved == null) flow.Begin();
            ShowTitle();
        }

        public void ShowTitle()
        {
            Lock(true);
            mainPanel.SetActive(true);
            levelsPanel.SetActive(false);

            var saved = flow.Saved;
            int medals = 0;
            if (saved != null) foreach (int s in saved.stars) medals += s;
            medalCount.text = $"Медали: {medals} из {flow.Count * 3}";
            playLabel.text = medals > 0 ? "Продолжить" : "Играть";
        }

        void ShowLevels()
        {
            mainPanel.SetActive(false);
            levelsPanel.SetActive(true);
            BuildCards();
        }

        void Play()
        {
            mainPanel.SetActive(false);
            levelsPanel.SetActive(false);
            Lock(false);
        }

        void OpenFromGame()
        {
            flow.Retry();
            ShowTitle();
        }

        void Lock(bool menuOpen)
        {
            hud.SetActive(!menuOpen);
            diver.AcceptInput = !menuOpen;
        }

        void BuildCards()
        {
            var saved = flow.Saved;
            if (cards == null)
            {
                cards = new Button[flow.Count];
                for (int i = 0; i < cards.Length; i++)
                {
                    int index = i;
                    cards[i] = Instantiate(cardTemplate, grid);
                    cards[i].gameObject.SetActive(true);
                    cards[i].onClick.AddListener(() => Pick(index));
                }
            }

            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                bool open = i < saved.unlocked;
                card.interactable = open;
                card.image.color = !open ? Locked : i == flow.Index ? Current : Open;

                var texts = card.GetComponentsInChildren<Text>(true);
                texts[0].text = (i + 1).ToString();
                texts[1].text = open ? flow.HeightOf(i).ToString("0.#").Replace('.', ',') + " м" : "закрыт";

                var dots = card.transform.Find("Medals").GetComponentsInChildren<Image>(true);
                for (int m = 0; m < dots.Length; m++)
                {
                    var c = DiveHud.MedalColors[m];
                    c.a = m < saved.stars[i] ? 1f : open ? 0.25f : 0f;
                    dots[m].color = c;
                }
            }
        }

        void Pick(int index)
        {
            flow.Load(index);
            Play();
        }

        // For the editor capture tool: 0 = title, 1 = level grid, anything else = in game.
        public void Preview(int screen)
        {
            if (screen == 0) ShowTitle();
            else if (screen == 1) { ShowTitle(); ShowLevels(); }
            else Play();
        }
    }
}
