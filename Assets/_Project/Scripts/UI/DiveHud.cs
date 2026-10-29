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
        [SerializeField] Text resultTask;
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

        void OnEnable()
        {
            flow.Finished += OnFinished;
            flow.LevelChanged += RefreshLevel;
            diver.Restarted += OnRestarted;
        }

        void OnDisable()
        {
            flow.Finished -= OnFinished;
            flow.LevelChanged -= RefreshLevel;
            diver.Restarted -= OnRestarted;
        }

        void Start()
        {
            prevButton.onClick.AddListener(flow.Back);
            nextButton.onClick.AddListener(flow.Next);
            retryButton.onClick.AddListener(flow.Retry);
            nextLevelButton.onClick.AddListener(flow.Next);
            resultPanel.gameObject.SetActive(false);
            RefreshLevel();
        }

        void RefreshLevel()
        {
            var level = flow.Level;
            var saved = flow.Saved;
            if (saved == null) return;

            levelTitle.text = $"Уровень {flow.Index + 1}  ·  {flow.Height:0.#} м".Replace('.', ',');
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
        }

        void RefreshArrows()
        {
            bool standing = diver.Current == Diver.State.Ready;
            prevButton.gameObject.SetActive(standing && flow.CanGoBack);
            nextButton.gameObject.SetActive(standing && flow.CanGoNext);
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
                    SetHint("Зажми, чтобы присесть и наклониться.\nОтпусти — прыжок!");
                    turns.enabled = false;
                    break;

                case Diver.State.Crouching:
                    SetHint(diver.LeanDanger > 0.7f ? "Сейчас сорвёшься!" : "");
                    break;

                case Diver.State.Airborne:
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

            // Tapping anywhere outside the buttons is a quick retry.
            if (t > resultDelay + 0.3f && HoldInput.IsHeld)
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
            else
                resultDetails.text = r.TurnsLabel + "   " + (r.inZone ? "в зоне ×" + r.multiplier.ToString("0.#") : "мимо зоны");

            resultScore.text = r.score.ToString();
            resultScore.color = r.Failed ? Bad : Good;

            for (int i = 0; i < 3; i++)
                resultMedals[i].color = Fade(MedalColors[i], i < outcome.stars ? 1f : Dim);

            if (outcome.taskJustDone) resultTask.text = "Задание выполнено!";
            else if (outcome.taskDone) resultTask.text = "Задание снова выполнено";
            else resultTask.text = "Задание: " + flow.Level.TaskText;
            resultTask.color = outcome.taskDone ? TaskDone : new Color(1f, 1f, 1f, 0.75f);

            nextLevelButton.gameObject.SetActive(flow.CanGoNext);
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
            }
            else
            {
                SetHint("Зажми, чтобы присесть и наклониться.\nОтпусти — прыжок!");
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
