using UnityEngine;
using UnityEngine.UI;

namespace JumpGame
{
    public class DiveHud : MonoBehaviour
    {
        [SerializeField] Diver diver;
        [SerializeField] Text best;
        [SerializeField] Text turns;
        [SerializeField] Text hint;
        [SerializeField] RectTransform resultPanel;
        [SerializeField] Text resultTitle;
        [SerializeField] Text resultDetails;
        [SerializeField] Text resultScore;
        [SerializeField] Text resultTap;
        [SerializeField] float resultDelay = 0.9f;

        static readonly Color Good = new Color(1f, 0.85f, 0.3f);
        static readonly Color Bad = new Color(1f, 0.45f, 0.42f);

        float enteredAt = -1f;
        int record;
        bool airHintShown;

        void OnEnable()
        {
            diver.Entered += OnEntered;
            diver.Restarted += OnRestarted;
        }

        void OnDisable()
        {
            diver.Entered -= OnEntered;
            diver.Restarted -= OnRestarted;
        }

        void Start()
        {
            resultPanel.gameObject.SetActive(false);
            best.text = "Рекорд: 0";
        }

        void OnEntered(DiveResult r)
        {
            enteredAt = Time.time;
            if (r.score > record)
            {
                record = r.score;
                best.text = "Рекорд: " + record;
            }
        }

        void OnRestarted()
        {
            enteredAt = -1f;
            resultPanel.gameObject.SetActive(false);
        }

        void Update()
        {
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
                    turns.text = (Mathf.Max(0f, diver.Rotation) / 360f).ToString("0.0");
                    if (!airHintShown && !diver.Slipped)
                        SetHint("Держи — группировка, отпусти — раскрыться");
                    else
                        SetHint("");
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
            float t = Time.time - enteredAt;
            if (t < resultDelay) return;

            if (!resultPanel.gameObject.activeSelf)
                ShowResult(diver.LastResult);

            // Small pop when the panel appears.
            float k = Mathf.Clamp01((t - resultDelay) / 0.25f);
            float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.12f;
            resultPanel.localScale = new Vector3(s, s, 1f);
            resultTap.color = new Color(1f, 1f, 1f, 0.6f + Mathf.Sin(Time.time * 4f) * 0.4f);

            if (t > resultDelay + 0.3f && HoldInput.IsHeld)
                diver.ResetToStart();
        }

        void ShowResult(DiveResult r)
        {
            airHintShown = true;
            resultPanel.gameObject.SetActive(true);
            resultTitle.text = r.EntryLabel;
            resultTitle.color = r.Failed ? Bad : Color.white;

            if (r.entry == EntryKind.Slipped)
                resultDetails.text = "Передержал на краю";
            else
                resultDetails.text = r.TurnsLabel + "   " + (r.inZone ? "в зоне ×" + r.multiplier.ToString("0.#") : "мимо зоны");

            resultScore.text = r.score.ToString();
            resultScore.color = r.Failed ? Bad : Good;
        }

        void SetHint(string text)
        {
            if (hint.text != text) hint.text = text;
        }
    }
}
