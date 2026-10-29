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
        [SerializeField] float minHeight = 2.4f;

        [Header("Centre hit close-up")]
        [Tooltip("Real seconds, unaffected by slow motion.")]
        [SerializeField] float highlightTime = 2f;
        [SerializeField] float highlightDistance = 3.4f;
        [Tooltip("How far the camera swings round the splash, degrees.")]
        [SerializeField] float highlightOrbit = 35f;

        Camera cam;
        Vector3 focus;
        float distance;
        readonly Vector3[] points = new Vector3[6];

        float shake;
        float highlightAge = -1f;
        Vector3 highlightPoint;

        // Flying in from wherever the camera was left (the level map).
        const float BlendTime = 0.9f;
        float blendAge = -1f;
        Vector3 blendPos;
        Quaternion blendRot;

        public void BlendFromCurrent()
        {
            blendPos = transform.position;
            blendRot = transform.rotation;
            Solve(out focus, out distance);
            blendAge = 0f;
        }

        // A short jolt that dies out in about half a second.
        public void Shake(float strength) => shake = Mathf.Max(shake, strength);

        // Swoop in on the splash, swing round it a little, then go back to the usual shot.
        public void Highlight(Vector3 point)
        {
            highlightPoint = point;
            highlightAge = 0f;
        }

        // +1 films from the -X side of the pool, -1 from the +X side.
        public void SetSide(float sign) => viewDirection.x = Mathf.Abs(viewDirection.x) * Mathf.Sign(sign);

        void Start() => Snap();

        void LateUpdate() => Step(Time.deltaTime);

        public void Snap()
        {
            highlightAge = -1f;
            blendAge = -1f;
            Solve(out focus, out distance);
            // While the level map owns the camera, only remember the shot for later.
            if (enabled) Place();
        }

        public void Step(float dt)
        {
            Solve(out var f, out var d);
            float k = 1f - Mathf.Exp(-followSharpness * dt);
            focus = Vector3.Lerp(focus, f, k);
            distance = Mathf.Lerp(distance, d, k);

            if (highlightAge >= 0f)
            {
                // Real time in play mode so slow motion doesn't stretch it; editor tools pass their own step.
                highlightAge += Application.isPlaying ? Time.unscaledDeltaTime : dt;
                if (highlightAge > highlightTime) highlightAge = -1f;
            }
            Place();
        }

        // 0 → 1 quickly, hold, then ease back to 0 over the last part.
        float HighlightWeight()
        {
            if (highlightAge < 0f) return 0f;
            float t = highlightAge / highlightTime;
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(highlightAge / 0.25f));
            float fall = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) / 0.35f));
            return Mathf.Min(rise, fall);
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
            // After the entry the shot stays wide enough to keep the camera clear of the pool edge.
            d = diver.Current == Diver.State.Underwater ? minDistance * 1.5f : minDistance;
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
            Vector3 f = focus;
            float d = distance;

            float w = HighlightWeight();
            if (w > 0f)
            {
                float swing = highlightOrbit * Mathf.Sign(viewDirection.x) * Mathf.SmoothStep(0f, 1f, highlightAge / highlightTime);
                Vector3 close = Quaternion.AngleAxis(swing, Vector3.up) * dir;
                close = Vector3.Slerp(close, Vector3.down, 0.15f).normalized;
                dir = Vector3.Slerp(dir, close, w).normalized;
                f = Vector3.Lerp(focus, highlightPoint + Vector3.up * 0.5f, w);
                d = Mathf.Lerp(distance, highlightDistance, w);
            }

            Vector3 pos = f - dir * d;
            pos.y = Mathf.Max(pos.y, Mathf.Lerp(minHeight, 0.9f, w));
            transform.position = pos;
            transform.LookAt(f);

            if (blendAge >= 0f)
            {
                blendAge += Application.isPlaying ? Time.unscaledDeltaTime : 0.02f;
                float b = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blendAge / BlendTime));
                transform.SetPositionAndRotation(Vector3.Lerp(blendPos, transform.position, b),
                                                 Quaternion.Slerp(blendRot, transform.rotation, b));
                if (b >= 1f) blendAge = -1f;
            }

            if (shake > 0.001f)
            {
                float t = Time.unscaledTime * 31f;
                var jolt = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * shake;
                transform.position += transform.rotation * jolt;
                shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 0.8f);
            }
        }
    }
}
