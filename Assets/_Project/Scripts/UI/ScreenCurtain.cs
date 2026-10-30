using System;
using UnityEngine;

namespace JumpGame
{
    // A plain full-screen colour that covers the scene while it is rearranged behind it.
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenCurtain : MonoBehaviour
    {
        [SerializeField] float closeTime = 0.18f;
        [SerializeField] float openTime = 0.4f;

        enum Phase { Idle, Closing, Opening }

        CanvasGroup group;
        Phase phase;
        float alpha;
        Action atCover;

        public bool Busy => phase != Phase.Idle;

        void Prepare()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
        }

        // Cover the screen, run the change while nothing can be seen, then uncover.
        public void Dip(Action change)
        {
            if (!Application.isPlaying)
            {
                change?.Invoke();
                return;
            }
            Prepare();
            atCover = change;
            phase = Phase.Closing;
            gameObject.SetActive(true);
            Apply();
        }

        // Start covered and open up, for the first frame of the game.
        public void Reveal()
        {
            if (!Application.isPlaying) return;
            Prepare();
            alpha = 1f;
            phase = Phase.Opening;
            gameObject.SetActive(true);
            Apply();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (phase == Phase.Closing)
            {
                alpha = Mathf.MoveTowards(alpha, 1f, dt / closeTime);
                if (alpha >= 1f)
                {
                    atCover?.Invoke();
                    atCover = null;
                    phase = Phase.Opening;
                }
            }
            else if (phase == Phase.Opening)
            {
                alpha = Mathf.MoveTowards(alpha, 0f, dt / openTime);
                if (alpha <= 0f) phase = Phase.Idle;
            }
            Apply();
            if (phase == Phase.Idle) gameObject.SetActive(false);
        }

        void Apply()
        {
            group.alpha = Mathf.SmoothStep(0f, 1f, alpha);
            group.blocksRaycasts = phase != Phase.Idle;
        }
    }
}
