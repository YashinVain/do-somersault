using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JumpGame
{
    // "+200 / Оборот!" next to the diver each time a full somersault is completed.
    public class TurnPopups : MonoBehaviour
    {
        [SerializeField] Diver diver;
        [SerializeField] Camera view;
        [SerializeField] Text template;
        [SerializeField] Vector2 screenOffset = new Vector2(120f, 30f);
        [SerializeField] float life = 1.1f;
        [SerializeField] float rise = 70f;

        class Popup
        {
            public Text text;
            public Vector2 start;
            public float age;
        }

        readonly List<Popup> active = new List<Popup>();
        readonly Stack<Text> pool = new Stack<Text>();
        RectTransform area;

        void Awake()
        {
            area = (RectTransform)transform;
            template.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            diver.TurnCompleted += Show;
            diver.Restarted += Clear;
            diver.Entered += FadeOut;
        }

        void OnDisable()
        {
            diver.TurnCompleted -= Show;
            diver.Restarted -= Clear;
            diver.Entered -= FadeOut;
        }

        // The result screen is about to come up: let whatever is still floating fade now.
        void FadeOut(DiveResult _)
        {
            foreach (var p in active) p.age = Mathf.Max(p.age, life * 0.66f);
        }

        public void Show(int turns)
        {
            if (area == null) Awake();
            var text = pool.Count > 0 ? pool.Pop() : Instantiate(template, area);
            text.gameObject.SetActive(true);

            int points = 2 * DiveScoring.PointsPerHalfTurn;
            string label = turns == 1 ? "Оборот!" : turns + " оборота!";
            text.text = $"<size=50><color=#FFD45A>+{points}</color></size>\n{label}";

            // Viewport maths keeps this right for any canvas mode and resolution.
            Vector2 vp = view.WorldToViewportPoint(diver.transform.position);
            Vector2 local = (vp - new Vector2(0.5f, 0.5f)) * area.rect.size;
            // Stack a little higher for each turn so consecutive popups don't sit on top of each other.
            var p = new Popup { text = text, start = local + screenOffset + Vector2.up * (turns - 1) * 18f };
            active.Add(p);
            Place(p);
        }

        void Update() => Step(Time.deltaTime);

        public void Step(float dt)
        {
            if (area == null) Awake();
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var p = active[i];
                p.age += dt;
                if (p.age >= life)
                {
                    Recycle(p);
                    active.RemoveAt(i);
                    continue;
                }
                Place(p);
            }
        }

        void Place(Popup p)
        {
            float t = p.age / life;
            var rt = p.text.rectTransform;
            rt.anchoredPosition = p.start + Vector2.up * (rise * (1f - (1f - t) * (1f - t)));

            // Quick pop in, settle, then fade out over the last third.
            float s = p.age < 0.12f ? Mathf.Lerp(0.5f, 1.15f, p.age / 0.12f)
                    : p.age < 0.3f ? Mathf.Lerp(1.15f, 1f, (p.age - 0.12f) / 0.18f)
                    : 1f;
            rt.localScale = new Vector3(s, s, 1f);

            var c = p.text.color;
            c.a = t < 0.66f ? 1f : 1f - (t - 0.66f) / 0.34f;
            p.text.color = c;
        }

        void Recycle(Popup p)
        {
            p.text.gameObject.SetActive(false);
            pool.Push(p.text);
        }

        public void Clear()
        {
            foreach (var p in active) Recycle(p);
            active.Clear();
        }
    }
}
