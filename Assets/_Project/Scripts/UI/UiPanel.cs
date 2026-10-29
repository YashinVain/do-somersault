using UnityEngine;

namespace JumpGame
{
    // A screen that fades in and out instead of popping. Listed children slide in one after another
    // from the side the panel comes from; Replay() runs that again, e.g. when the map moves to another location.
    [RequireComponent(typeof(CanvasGroup))]
    public class UiPanel : MonoBehaviour
    {
        [SerializeField] float fade = 0.25f;
        [Tooltip("Where listed children start, in canvas pixels away from their place.")]
        [SerializeField] Vector2 slide = new Vector2(-48f, 0f);
        [SerializeField] RectTransform[] staggered;
        [SerializeField] float step = 0.07f;
        [SerializeField] float childTime = 0.4f;

        CanvasGroup group;
        Vector2[] home;
        CanvasGroup[] childGroups;
        float amount;
        float target;
        float age;

        public bool Visible => target > 0f;

        void Prepare()
        {
            if (group != null) return;
            group = GetComponent<CanvasGroup>();
            int n = staggered != null ? staggered.Length : 0;
            home = new Vector2[n];
            childGroups = new CanvasGroup[n];
            for (int i = 0; i < n; i++)
            {
                if (staggered[i] == null) continue;
                home[i] = staggered[i].anchoredPosition;
                childGroups[i] = staggered[i].GetComponent<CanvasGroup>();
                if (childGroups[i] == null) childGroups[i] = staggered[i].gameObject.AddComponent<CanvasGroup>();
            }
        }

        public void Show(bool on, bool instant = false)
        {
            Prepare();
            if (on == (target > 0f) && gameObject.activeSelf == on && !instant) return;

            target = on ? 1f : 0f;
            if (on) age = 0f;
            // Edit-mode tools have no Update, so they get the finished state straight away.
            if (instant || !Application.isPlaying)
            {
                amount = target;
                age = on ? 99f : 0f;
                Apply();
                gameObject.SetActive(on);
                return;
            }
            if (on)
            {
                gameObject.SetActive(true);
                Apply();
            }
            group.blocksRaycasts = on;
        }

        public void Replay()
        {
            Prepare();
            age = Application.isPlaying ? 0f : 99f;
            Apply();
        }

        void Update()
        {
            Prepare();
            float dt = Time.unscaledDeltaTime;
            amount = Mathf.MoveTowards(amount, target, dt / Mathf.Max(0.01f, fade));
            if (target > 0f) age += dt;
            Apply();
            if (target <= 0f && amount <= 0f) gameObject.SetActive(false);
        }

        void Apply()
        {
            group.alpha = Mathf.SmoothStep(0f, 1f, amount);
            group.blocksRaycasts = target > 0f;
            for (int i = 0; i < childGroups.Length; i++)
            {
                if (childGroups[i] == null) continue;
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - i * step) / childTime));
                childGroups[i].alpha = p;
                staggered[i].anchoredPosition = home[i] + slide * (1f - p);
            }
        }
    }
}
