using UnityEngine;

namespace JumpGame
{
    // A crab: runs sideways to a random spot, stops, looks around, runs again.
    public class Scuttler : MonoBehaviour
    {
        [SerializeField] float range = 2.5f;
        [SerializeField] float speed = 0.9f;
        [SerializeField] Transform[] legs;
        [SerializeField] Transform[] claws;

        Vector3 origin, side;
        float x, target, pause, seed;

        void Start()
        {
            origin = transform.localPosition;
            side = transform.localRotation * Vector3.right;
            seed = Random.value * 10f;
            target = Random.Range(-range, range);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float moving = 0f;

            if (pause > 0f) pause -= dt;
            else
            {
                float next = Mathf.MoveTowards(x, target, speed * dt);
                moving = Mathf.Abs(next - x) / Mathf.Max(dt, 0.0001f) / speed;
                x = next;
                transform.localPosition = origin + side * x;
                if (Mathf.Approximately(x, target))
                {
                    pause = Random.Range(0.6f, 2.8f);
                    target = Random.Range(-range, range);
                }
            }

            float t = Time.time * 26f;
            for (int i = 0; i < legs.Length; i++)
                legs[i].localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 1f : -1f) * 22f * moving * Mathf.Sin(t + i * 1.3f));

            // Claws open and shut now and then, more often while standing still.
            float snap = Mathf.Max(0f, Mathf.Sin(Time.time * 2.3f + seed)) * (1f - moving);
            for (int i = 0; i < claws.Length; i++)
                claws[i].localRotation = Quaternion.Euler(-snap * 35f, 0f, 0f);
        }
    }
}
