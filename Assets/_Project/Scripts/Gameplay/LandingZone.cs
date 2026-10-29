using UnityEngine;

namespace JumpGame
{
    // Area on the water fenced off by a ring of buoys. Landing inside multiplies the score.
    public class LandingZone : MonoBehaviour
    {
        [SerializeField] float radius = 1.6f;
        [SerializeField] float insideMultiplier = 2f;
        [SerializeField] Transform[] buoys;
        [SerializeField] float bobHeight = 0.04f;
        [SerializeField] float bobSpeed = 1.6f;

        float[] restY;

        public float WaterLevel => transform.position.y;
        public float Radius => radius;
        public float InsideMultiplier => insideMultiplier;

        public bool Contains(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= radius * radius;
        }

        void Start()
        {
            restY = new float[buoys.Length];
            for (int i = 0; i < buoys.Length; i++) restY[i] = buoys[i].localPosition.y;
        }

        void Update()
        {
            float t = Time.time * bobSpeed;
            for (int i = 0; i < buoys.Length; i++)
            {
                var p = buoys[i].localPosition;
                p.y = restY[i] + Mathf.Sin(t + i * 0.9f) * bobHeight;
                buoys[i].localPosition = p;
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Vector3 prev = transform.position + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2f;
                Vector3 p = transform.position + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
#endif
    }
}
