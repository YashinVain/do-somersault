using UnityEngine;

namespace JumpGame
{
    // Small moments that sell the dive: a slow beat on a clean entry, a jolt on a bad one.
    public class DiveJuice : MonoBehaviour
    {
        [SerializeField] Diver diver;
        [SerializeField] DiveCamera view;
        [SerializeField] float slowScale = 0.3f;
        [Tooltip("Real seconds the slow motion lasts, including easing back to normal speed.")]
        [SerializeField] float slowTime = 0.7f;

        float slowLeft;

        void OnEnable()
        {
            diver.Entered += OnEntered;
            diver.Knocked += OnKnocked;
            diver.Restarted += ResetTime;
        }

        void OnDisable()
        {
            diver.Entered -= OnEntered;
            diver.Knocked -= OnKnocked;
            diver.Restarted -= ResetTime;
            ResetTime();
        }

        void OnEntered(DiveResult r)
        {
            if (r.bullseye && !r.Failed)
            {
                view.Highlight(diver.EntryPoint);
                slowLeft = slowTime * 1.4f;
                return;
            }

            switch (r.entry)
            {
                case EntryKind.HeadClean:
                case EntryKind.FeetClean:
                    if (r.inZone) slowLeft = slowTime;
                    break;
                case EntryKind.BellyFlop:
                    view.Shake(0.35f);
                    break;
                case EntryKind.Cannonball:
                    view.Shake(0.2f);
                    break;
            }
        }

        void OnKnocked(float strength) => view.Shake(Mathf.Clamp(strength * 0.08f, 0.1f, 0.4f));

        void Update()
        {
            if (slowLeft <= 0f) return;
            slowLeft -= Time.unscaledDeltaTime;
            // Hold the slow part for the first half, then ease back up.
            float t = Mathf.Clamp01(slowLeft / slowTime);
            if (t >= 1f) { Time.timeScale = slowScale; return; }
            Time.timeScale = slowLeft <= 0f ? 1f : Mathf.Lerp(1f, slowScale, Mathf.Clamp01(t * 2f));
        }

        void ResetTime()
        {
            slowLeft = 0f;
            Time.timeScale = 1f;
        }
    }
}
