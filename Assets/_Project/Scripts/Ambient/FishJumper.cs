using UnityEngine;

namespace JumpGame
{
    // Every few seconds a fish flips out of the water somewhere in the given area and drops back in.
    public class FishJumper : MonoBehaviour
    {
        [SerializeField] Vector3 areaCenter;
        [SerializeField] Vector2 areaSize = new Vector2(30f, 20f);
        [SerializeField] Vector2 interval = new Vector2(3f, 8f);
        [SerializeField] float jumpTime = 0.8f;
        [SerializeField] float jumpHeight = 0.7f;
        [SerializeField] float jumpLength = 1.2f;

        Renderer[] parts;
        float wait, age = -1f;
        Vector3 start, dir;

        void Start()
        {
            parts = GetComponentsInChildren<Renderer>();
            Show(false);
            wait = Random.Range(interval.x, interval.y);
        }

        void Update()
        {
            if (age < 0f)
            {
                wait -= Time.deltaTime;
                if (wait > 0f) return;
                start = areaCenter + new Vector3(Random.Range(-0.5f, 0.5f) * areaSize.x, 0f, Random.Range(-0.5f, 0.5f) * areaSize.y);
                float a = Random.Range(0f, Mathf.PI * 2f);
                dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                age = 0f;
                Show(true);
            }

            age += Time.deltaTime;
            float t = age / jumpTime;
            if (t >= 1f)
            {
                age = -1f;
                wait = Random.Range(interval.x, interval.y);
                Show(false);
                return;
            }

            // A simple arc; the fish noses up on the way out and down on the way back.
            var p = start + dir * (jumpLength * t) + Vector3.up * (4f * jumpHeight * t * (1f - t) - 0.15f);
            float pitch = Mathf.Lerp(-55f, 55f, t);
            transform.localPosition = p;
            transform.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(pitch, 0f, 0f);
        }

        void Show(bool on)
        {
            foreach (var r in parts) r.enabled = on;
        }
    }
}
