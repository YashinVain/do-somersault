using UnityEngine;

namespace JumpGame
{
    // Swims an oval loop on the water. Ducklings are the same with a small lag behind the mother.
    public class PathSwimmer : MonoBehaviour
    {
        [SerializeField] Vector3 center;
        [SerializeField] Vector2 radii = new Vector2(6f, 3f);
        [Tooltip("Radians per second.")]
        [SerializeField] float speed = 0.12f;
        [SerializeField] float phase;
        [SerializeField] Transform head;

        void Update()
        {
            float a = phase + Time.time * speed;
            var p = center + new Vector3(Mathf.Cos(a) * radii.x, 0f, Mathf.Sin(a) * radii.y);
            Vector3 world = transform.parent != null ? transform.parent.TransformPoint(p) : p;
            p.y += WaterMotion.Height(world) + Mathf.Sin(Time.time * 2.1f + phase * 5f) * 0.015f;

            var tangent = new Vector3(-Mathf.Sin(a) * radii.x, 0f, Mathf.Cos(a) * radii.y);
            transform.localPosition = p;
            transform.localRotation = Quaternion.LookRotation(tangent);

            if (head != null)
                head.localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Sin(Time.time * 1.3f + phase * 7f)) * 25f, 0f, 0f);
        }
    }
}
