using UnityEngine;

namespace JumpGame
{
    // Throwaway IMGUI overlay for tuning the jump. Real UI comes later.
    public class PrototypeHud : MonoBehaviour
    {
        [SerializeField] Diver diver;
        [SerializeField] float resultDelay = 0.8f;

        float enteredAt = -1f;
        int best;
        GUIStyle big, small;

        void OnEnable() => diver.Entered += OnEntered;
        void OnDisable() => diver.Entered -= OnEntered;

        void OnEntered(DiveResult r)
        {
            enteredAt = Time.time;
            best = Mathf.Max(best, r.score);
        }

        void Update()
        {
            bool showingResult = enteredAt >= 0f && Time.time - enteredAt > resultDelay;
            if (showingResult && HoldInput.IsHeld)
            {
                enteredAt = -1f;
                diver.ResetToStart();
            }
        }

        void OnGUI()
        {
            if (big == null)
            {
                big = new GUIStyle(GUI.skin.label) { fontSize = 42, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                small = new GUIStyle(big) { fontSize = 22 };
            }

            float w = Screen.width, h = Screen.height;
            GUI.Label(new Rect(0, 10, w, 30), "Рекорд: " + best, small);

            switch (diver.Current)
            {
                case Diver.State.Ready:
                    GUI.Label(new Rect(0, h - 90, w, 40), "Зажми, чтобы присесть", small);
                    break;
                case Diver.State.Crouching:
                    DrawCharge(w, h);
                    break;
                case Diver.State.Airborne:
                    GUI.Label(new Rect(0, 50, w, 50), (diver.Rotation / 360f).ToString("0.0") + " об.", big);
                    break;
                case Diver.State.Underwater:
                    if (Time.time - enteredAt > resultDelay)
                        DrawResult(w, h);
                    break;
            }
        }

        void DrawCharge(float w, float h)
        {
            var bar = new Rect(w * 0.3f, h - 80, w * 0.4f, 22);
            GUI.Box(bar, GUIContent.none);
            var fill = bar;
            fill.width *= diver.Charge;
            GUI.color = Color.Lerp(new Color(1f, 0.8f, 0.2f), new Color(1f, 0.3f, 0.2f), diver.Charge);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void DrawResult(float w, float h)
        {
            var r = diver.LastResult;
            GUI.Label(new Rect(0, h * 0.25f, w, 60), r.EntryLabel, big);
            GUI.Label(new Rect(0, h * 0.25f + 60, w, 30), r.TurnsLabel + "  ×" + r.multiplier.ToString("0.#"), small);
            GUI.Label(new Rect(0, h * 0.25f + 100, w, 60), r.score.ToString(), big);
            GUI.Label(new Rect(0, h - 90, w, 40), "Нажми, чтобы прыгнуть ещё раз", small);
        }
    }
}
