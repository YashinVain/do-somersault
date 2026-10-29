using UnityEngine;
using UnityEngine.UI;

namespace JumpGame
{
    // The drawn title background: rolling waves, drifting clouds, rising bubbles, a title that sways a little.
    public class TitleBackdrop : MonoBehaviour
    {
        [SerializeField] RawImage[] waves;
        [SerializeField] float[] waveSpeeds;
        [SerializeField] RectTransform[] clouds;
        [SerializeField] float cloudSpeed = 14f;
        [SerializeField] RectTransform[] bubbles;
        [SerializeField] float waterTop = 220f;
        [SerializeField] RectTransform title;
        [SerializeField] RectTransform playButton;

        float t;
        float[] bubbleSeed;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            float width = ((RectTransform)transform).rect.width;

            for (int i = 0; i < waves.Length; i++)
            {
                var uv = waves[i].uvRect;
                uv.x = t * waveSpeeds[i];
                waves[i].uvRect = uv;
            }

            for (int i = 0; i < clouds.Length; i++)
            {
                var p = clouds[i].anchoredPosition;
                p.x += cloudSpeed * (1f + i * 0.35f) * dt;
                if (p.x > width * 0.5f + 260f) p.x = -width * 0.5f - 260f;
                clouds[i].anchoredPosition = p;
            }

            bubbleSeed ??= new float[bubbles.Length];
            for (int i = 0; i < bubbles.Length; i++)
            {
                var p = bubbles[i].anchoredPosition;
                p.y += (26f + (i % 3) * 12f) * dt;
                p.x += Mathf.Sin(t * 2f + i * 1.7f) * 12f * dt;
                if (p.y > waterTop - 30f)
                {
                    p.y = Random.Range(10f, 60f);
                    p.x = Random.Range(-width * 0.45f, width * 0.45f);
                }
                bubbles[i].anchoredPosition = p;
            }

            title.localRotation = Quaternion.Euler(0f, 0f, -4f + Mathf.Sin(t * 1.3f) * 1.4f);
            float pulse = 1f + Mathf.Max(0f, Mathf.Sin(t * 3.2f)) * 0.05f;
            playButton.localScale = new Vector3(pulse, pulse, 1f);
        }
    }
}
