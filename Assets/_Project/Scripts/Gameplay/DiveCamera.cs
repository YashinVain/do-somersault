using UnityEngine;

namespace JumpGame
{
    // Raised side camera. It looks along a fixed direction and backs off just enough
    // to keep the diver and the whole buoy ring on screen, whatever the platform height or screen shape.
    [RequireComponent(typeof(Camera))]
    public class DiveCamera : MonoBehaviour
    {
        [SerializeField] Diver diver;
        [SerializeField] LandingZone zone;
        [SerializeField] Vector3 viewDirection = new Vector3(7.5f, -2.5f, 1.5f);
        [SerializeField] float fov = 52f;
        [Tooltip("Share of the screen the framed points may use, from the centre to the edge.")]
        [SerializeField, Range(0.5f, 1f)] float fill = 0.82f;
        [Tooltip("Extra room kept free at the top of the screen for the HUD.")]
        [SerializeField, Range(0f, 0.4f)] float topReserve = 0.16f;
        [SerializeField] float minDistance = 5f;
        [Tooltip("Where the framing centre sits between the diver (0) and the ring (1).")]
        [SerializeField, Range(0f, 1f)] float focusBias = 0.5f;
        [SerializeField] float followSharpness = 3.5f;
        [SerializeField] float minHeight = 1.2f;

        Camera cam;
        Vector3 focus;
        float distance;
        readonly Vector3[] points = new Vector3[6];

        // +1 films from the -X side of the pool, -1 from the +X side.
        public void SetSide(float sign) => viewDirection.x = Mathf.Abs(viewDirection.x) * Mathf.Sign(sign);

        void Start() => Snap();

        void LateUpdate() => Step(Time.deltaTime);

        public void Snap()
        {
            Solve(out focus, out distance);
            Place();
        }

        public void Step(float dt)
        {
            Solve(out var f, out var d);
            float k = 1f - Mathf.Exp(-followSharpness * dt);
            focus = Vector3.Lerp(focus, f, k);
            distance = Mathf.Lerp(distance, d, k);
            Place();
        }

        void Solve(out Vector3 f, out float d)
        {
            if (cam == null) cam = GetComponent<Camera>();
            cam.fieldOfView = fov;

            Quaternion rot = Quaternion.LookRotation(viewDirection);
            Vector3 right = rot * Vector3.right, up = rot * Vector3.up, fwd = rot * Vector3.forward;

            // After the entry the shot stays on the splash, not on the body sinking below.
            Vector3 body = diver.Current == Diver.State.Underwater
                ? diver.EntryPoint + Vector3.up * 0.5f
                : diver.transform.position;
            Vector3 ring = zone.transform.position;
            float r = zone.Radius + 0.2f;

            points[0] = body + Vector3.up * 1.1f;
            points[1] = body + Vector3.down * 1f;
            points[2] = ring + right * r;
            points[3] = ring - right * r;
            points[4] = ring + fwd * r;
            points[5] = ring - fwd * r;

            f = Vector3.Lerp(body, ring, focusBias);

            float tanHalf = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float tanDown = tanHalf * fill;
            float tanUp = tanHalf * (fill - topReserve);
            float tanSide = tanDown * cam.aspect;
            d = minDistance;
            foreach (var p in points)
            {
                Vector3 rel = p - f;
                float depth = Vector3.Dot(rel, fwd);
                float v = Vector3.Dot(rel, up);
                d = Mathf.Max(d, Mathf.Abs(v) / (v > 0f ? tanUp : tanDown) - depth);
                d = Mathf.Max(d, Mathf.Abs(Vector3.Dot(rel, right)) / tanSide - depth);
            }
        }

        void Place()
        {
            Vector3 dir = viewDirection.normalized;
            Vector3 pos = focus - dir * distance;
            pos.y = Mathf.Max(pos.y, minHeight);
            transform.position = pos;
            transform.LookAt(focus);
        }
    }
}
