using UnityEngine;
using UnityEngine.UI;

namespace JumpGame
{
    // Character shop on the title screen. The diver on the hero shot wears whoever is shown, so you see the
    // character before buying; closing the shop dresses him in the chosen one again.
    public class ShopScreen : MonoBehaviour
    {
        [SerializeField] GameFlow flow;
        [SerializeField] UiPanel panel;
        [SerializeField] UiPanel titlePanel;
        [SerializeField] Button openButton, backButton, prevButton, nextButton, actionButton;
        [SerializeField] Text nameText, blurbText, perkText, statusText, counterText, coinsText, actionLabel;

        int shown;

        void Start()
        {
            openButton.onClick.AddListener(Open);
            backButton.onClick.AddListener(Close);
            prevButton.onClick.AddListener(() => Step(-1));
            nextButton.onClick.AddListener(() => Step(1));
            actionButton.onClick.AddListener(Act);
            flow.CoinsChanged += Refresh;
            panel.Show(false, true);
        }

        void OnDestroy()
        {
            if (flow != null) flow.CoinsChanged -= Refresh;
        }

        public void Open()
        {
            shown = flow.SkinIndex;
            titlePanel.Show(false);
            panel.Show(true);
            Refresh();
        }

        public void Hide() => panel.Show(false, true);

        void Close()
        {
            panel.Show(false);
            titlePanel.Show(true);
            flow.ApplySkin();
        }

        void Step(int d)
        {
            int n = Skins.All.Length;
            shown = (shown + d + n) % n;
            Sounds.Play(Cue.Select, 0.8f);
            Refresh();
        }

        void Act()
        {
            if (flow.OwnsSkin(shown))
            {
                flow.SelectSkin(shown);
                Sounds.Play(Cue.Confirm);
            }
            else if (flow.BuySkin(shown))
            {
                Sounds.Play(Cue.Coins);
                Sounds.Play(Cue.Confirm, 0.7f);
            }
            Refresh();
        }

        // For the editor capture tool.
        public void Preview(int index)
        {
            shown = index;
            titlePanel.Show(false, true);
            panel.Show(true, true);
            Refresh();
        }

        void Refresh()
        {
            if (flow.Saved == null || !panel.gameObject.activeInHierarchy) return;
            var s = Skins.All[shown];
            flow.ShowSkin(shown);

            bool owned = flow.OwnsSkin(shown), selected = shown == flow.SkinIndex;
            nameText.text = s.title;
            blurbText.text = s.blurb;
            perkText.text = s.perk == Perk.None ? "" : "<b>" + s.perk.title + "</b>" + '\n' + s.perk.text;
            counterText.text = $"{shown + 1} / {Skins.All.Length}";
            coinsText.text = flow.Coins.ToString();

            statusText.text = selected ? "Выбран" : owned ? "Куплен" : s.price + " монет";
            statusText.color = selected ? new Color(0.5f, 0.9f, 0.6f) : owned ? new Color(0.78f, 0.82f, 0.86f) : new Color(1f, 0.8f, 0.26f);

            actionButton.interactable = !selected && (owned || flow.Coins >= s.price);
            actionLabel.text = selected ? "Выбран" : owned ? "Выбрать" : flow.Coins >= s.price ? "Купить" : "Не хватает монет";
        }
    }
}
