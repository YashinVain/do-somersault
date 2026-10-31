using UnityEngine;
using UnityEngine.UI;

namespace JumpGame
{
    public class DiveHud : MonoBehaviour
    {
        [SerializeField] Diver diver;
        [SerializeField] GameFlow flow;

        [Header("Level")]
        [SerializeField] Text levelTitle;
        [SerializeField] Button prevButton, nextButton;
        [SerializeField] Image[] goalDots;
        [SerializeField] Text[] goalTexts;
        [SerializeField] Image taskDot;
        [SerializeField] Text taskText;
        [SerializeField] Text best;

        [Header("Dive")]
        [SerializeField] Text turns;
        [SerializeField] Text hint;

        [Header("Result")]
        [SerializeField] RectTransform resultPanel;
        [SerializeField] Text resultTitle;
        [SerializeField] Text resultDetails;
        [SerializeField] Text resultScore;
        [SerializeField] Image[] resultMedals;
        [SerializeField] RectTransform[] judgeCards;
        [SerializeField] Text[] judgeTexts;
        [SerializeField] Text resultTask;
        [SerializeField] Text resultCoins;
        [SerializeField] Text coinCounter;
        [SerializeField] Button restartButton;
        [SerializeField] Button doubleCoinsButton;
        [SerializeField] Button retryButton, nextLevelButton;
        [SerializeField] float resultDelay = 0.9f;

        public static readonly Color[] MedalColors =
        {
            new Color(0.80f, 0.52f, 0.30f),
            new Color(0.80f, 0.84f, 0.88f),
            new Color(1f, 0.80f, 0.26f)
        };

        static readonly Color Good = new Color(1f, 0.85f, 0.3f);
        static readonly Color Bad = new Color(1f, 0.45f, 0.42f);
        static readonly Color TaskOpen = new Color(1f, 1f, 1f, 0.35f);
        static readonly Color TaskDone = new Color(0.45f, 0.9f, 0.55f);
        const float Dim = 0.3f;

        float finishedAt = -1f;
        DiveOutcome outcome;
        bool firstDive = true;
        int dives;

        void OnEnable()
        {
            flow.Finished += OnFinished;
            flow.LevelChanged += RefreshLevel;
            flow.CoinsChanged += RefreshCoins;
            diver.Restarted += OnRestarted;
        }

        void OnDisable()
        {
            flow.Finished -= OnFinished;
            flow.LevelChanged -= RefreshLevel;
            flow.CoinsChanged -= RefreshCoins;
            diver.Restarted -= OnRestarted;
        }

        void Start()
        {
            prevButton.onClick.AddListener(flow.Back);
            nextButton.onClick.AddListener(flow.Next);
            retryButton.onClick.AddListener(flow.Retry);
            restartButton.onClick.AddListener(flow.Retry);
            doubleCoinsButton.onClick.AddListener(DoubleCoins);
            nextLevelButton.onClick.AddListener(OnNextPressed);
            resultPanel.gameObject.SetActive(false);
            FitTitle();
            RefreshLevel();
        }

        // The level name sits between the medal goals on the left and the buttons on the right; on a squarer screen it shrinks to fit.
        void FitTitle()
        {
            var canvasRect = levelTitle.canvas != null ? levelTitle.canvas.transform as RectTransform : null;
            if (canvasRect == null || canvasRect.rect.width < 1f) return;
            levelTitle.resizeTextForBestFit = true;
            levelTitle.resizeTextMinSize = 16;
            levelTitle.resizeTextMaxSize = 28;
            levelTitle.rectTransform.sizeDelta = new Vector2(Mathf.Clamp(canvasRect.rect.width - 780f, 240f, 560f), levelTitle.rectTransform.sizeDelta.y);
        }

        void RefreshLevel()
        {
            FitTitle();
            var level = flow.Level;
            var saved = flow.Saved;
            if (saved == null) return;

            var (number, count) = flow.Set.PlaceInLocation(flow.Index);
            string height = flow.Height.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
            levelTitle.text = $"{flow.LocationName(level.location)}  ·  {number}/{count}  ·  {height} м";
            int[] goals = { level.bronze, level.silver, level.gold };
            int stars = saved.stars[flow.Index];
            for (int i = 0; i < 3; i++)
            {
                goalTexts[i].text = goals[i].ToString();
                goalDots[i].color = Fade(MedalColors[i], i < stars ? 1f : Dim);
            }

            bool taskDone = saved.tasks[flow.Index];
            taskText.text = level.TaskText;
            taskDot.color = taskDone ? TaskDone : TaskOpen;
            best.text = saved.best[flow.Index] > 0 ? "Лучший: " + saved.best[flow.Index] : "";
            RefreshArrows();
            RefreshCoins();
        }

        void RefreshCoins()
        {
            if (coinCounter != null && flow.Saved != null) coinCounter.text = flow.Coins.ToString();
        }

        // The same coins once more for a video, once per dive.
        void DoubleCoins()
        {
            int bonus = outcome.coins;
            bool paid = false;
            doubleCoinsButton.gameObject.SetActive(false);
            YandexGames.ShowRewarded(() =>
            {
                paid = true;
                flow.AddCoins(bonus);
                resultCoins.text = "+" + bonus * 2 + " монет";
                Sounds.Play(Cue.Coins);
            }, () =>
            {
                if (!paid && outcome.coins > 0 && resultPanel.gameObject.activeSelf) doubleCoinsButton.gameObject.SetActive(true);
            });
        }

        // "Дальше" goes to the next dive; after the last one of a location it opens the summary instead.
        void OnNextPressed()
        {
            if (flow.CanGoNext) flow.Next();
            else flow.RequestSummary();
        }

        void RefreshArrows()
        {
            bool standing = diver.Current == Diver.State.Ready;
            prevButton.gameObject.SetActive(standing && flow.CanGoBack);
            nextButton.gameObject.SetActive(standing && flow.CanGoNext);
            restartButton.gameObject.SetActive(!standing);
        }

        void OnFinished(DiveOutcome o)
        {
            outcome = o;
            finishedAt = Time.time;
        }

        void OnRestarted()
        {
            finishedAt = -1f;
            resultPanel.gameObject.SetActive(false);
            RefreshLevel();
        }

        void Update()
        {
            RefreshArrows();

            switch (diver.Current)
            {
                case Diver.State.Ready:
                    SetHint("Зажми — присядь и наклонись, отпусти — прыжок");
                    turns.enabled = false;
                    break;

                case Diver.State.Crouching:
                    SetHint(diver.LeanDanger > 0.7f ? "Сейчас сорвёшься!" : "");
                    break;

                case Diver.State.Airborne:
                    if (diver.Stranded)
                    {
                        // Landed on something that is not water: a countdown to hop off, or start over.
                        turns.enabled = true;
                        turns.text = Mathf.Max(0f, diver.StrandedLeft).ToString("0.0").Replace('.', ',');
                        SetHint("Застрял! Нажми, чтобы выпрыгнуть,\nили «Заново»");
                        break;
                    }
                    turns.enabled = !diver.Slipped;
                    turns.text = (Mathf.Max(0f, diver.Rotation) / 360f).ToString("0.0").Replace('.', ',');
                    SetHint(firstDive && !diver.Slipped ? "Держи — группировка, отпусти — раскрыться" : "");
                    break;

                case Diver.State.Underwater:
                    turns.enabled = false;
                    SetHint("");
                    UpdateResult();
                    break;
            }
        }

        void UpdateResult()
        {
            if (finishedAt < 0f) return;
            float t = Time.time - finishedAt;
            if (t < resultDelay) return;

            if (!resultPanel.gameObject.activeSelf)
                ShowResult();

            float k = Mathf.Clamp01((t - resultDelay) / 0.25f);
            float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.08f;
            resultPanel.localScale = new Vector3(s, s, 1f);
            AnimateJudges(t - resultDelay);

            // Tapping anywhere outside the buttons is a quick retry.
            if (t > resultDelay + 0.3f && diver.AcceptInput && HoldInput.IsHeld)
                flow.Retry();
        }

        void ShowResult()
        {
            firstDive = false;
            var r = outcome.result;
            resultPanel.gameObject.SetActive(true);

            resultTitle.text = r.EntryLabel;
            resultTitle.color = r.Failed ? Bad : Color.white;

            if (r.entry == EntryKind.Slipped)
                resultDetails.text = "Передержал на краю";
            else if (r.entry == EntryKind.HitTower)
                resultDetails.text = "Отпрыгни подальше от края";
            else if (r.entry == EntryKind.OnTower)
                resultDetails.text = "Толчок был слишком слабым";
            else if (r.hitBuoys)
                resultDetails.text = r.TurnsLabel + "   задел буйки";
            else if (r.bullseye)
                resultDetails.text = r.TurnsLabel + "   в самый центр ×" + r.multiplier.ToString("0.#");
            else
                resultDetails.text = r.TurnsLabel + "   " + (r.inZone ? "в зоне ×" + r.multiplier.ToString("0.#") : "мимо зоны");

            resultScore.text = r.score.ToString();
            resultScore.color = r.Failed ? Bad : Good;

            for (int i = 0; i < 3; i++)
                resultMedals[i].color = Fade(MedalColors[i], i < outcome.stars ? 1f : Dim);

            var scores = JudgePanel.Score(r, ++dives * 7919 + r.score);
            for (int i = 0; i < judgeTexts.Length; i++)
            {
                judgeTexts[i].text = JudgePanel.Format(scores[i]);
                judgeTexts[i].color = scores[i] >= 8f ? new Color(0.13f, 0.5f, 0.32f)
                                    : scores[i] < 3f ? new Color(0.75f, 0.2f, 0.2f)
                                    : new Color(0.12f, 0.15f, 0.25f);
            }
            AnimateJudges(0f);

            if (outcome.taskJustDone) resultTask.text = "Задание выполнено!";
            else if (outcome.taskDone) resultTask.text = "Задание снова выполнено";
            else resultTask.text = "Задание: " + flow.Level.TaskText;
            resultTask.color = outcome.taskDone ? TaskDone : new Color(1f, 1f, 1f, 0.75f);

            string coinsLine = outcome.coins > 0 ? "+" + outcome.coins + " монет" : "";
            if (!string.IsNullOrEmpty(r.note)) coinsLine += (coinsLine.Length > 0 ? "   ·   " : "") + r.note;
            resultCoins.text = coinsLine;
            doubleCoinsButton.gameObject.SetActive(outcome.coins > 0);

            bool summary = !flow.CanGoNext && flow.AtLocationEnd && flow.Saved.stars[flow.Index] > 0;
            nextLevelButton.gameObject.SetActive(flow.CanGoNext || summary);
            var label = nextLevelButton.GetComponentInChildren<Text>();
            if (label != null) label.text = summary ? "Итоги" : "Дальше";
        }

        // Cards go up one after another, each with a small overshoot.
        void AnimateJudges(float t)
        {
            for (int i = 0; i < judgeCards.Length; i++)
            {
                float x = Mathf.Clamp01((t - 0.25f - i * 0.18f) / 0.3f) - 1f;
                const float c1 = 1.70158f, c3 = c1 + 1f;
                float s = 1f + c3 * x * x * x + c1 * x * x; // ease-out-back
                judgeCards[i].localScale = new Vector3(s, s, 1f);
            }
        }

        // For the editor capture tool, which renders the HUD without entering play mode.
        public void Preview(DiveOutcome? result)
        {
            RefreshLevel();
            turns.enabled = false;
            resultPanel.localScale = Vector3.one;
            resultPanel.gameObject.SetActive(result.HasValue);
            if (result.HasValue)
            {
                outcome = result.Value;
                SetHint("");
                ShowResult();
                AnimateJudges(5f);
            }
            else
            {
                SetHint("Зажми — присядь и наклонись, отпусти — прыжок");
            }
        }

        void SetHint(string text)
        {
            if (hint.text != text) hint.text = text;
        }

        static Color Fade(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
