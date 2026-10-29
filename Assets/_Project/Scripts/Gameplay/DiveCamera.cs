using UnityEngine;

namespace JumpGame
{
    // Raised side camera that keeps both the diver and the buoy zone in frame.
    [RequireComponent(typeof(Camera))]
    public class DiveCamera : MonoBehaviour
    {
        [SerializeField] Diver diver;
        [SerializeField] LandingZone zone;
        [SerializeField] Vector3 offset = new Vector3(-7.5f, 2.5f, -1.5f);
        [Tooltip("How much the framing leans towards the zone while standing on the tower.")]
        [SerializeField, Range(0f, 1f)] float zoneBiasStanding = 0.3f;
        [SerializeField, Range(0f, 1f)] float zoneBiasFlying = 0.2f;
        [SerializeField] float followSharpness = 3.5f;
        [SerializeField] float minHeight = 1.2f;

        [Header("Lens")]
        [SerializeField] float baseFov = 55f;
        [Tooltip("On narrow (portrait) screens the vertical FOV grows to keep at least this much horizontally.")]
        [SerializeField] float minHorizontalFov = 64f;

        Camera cam;
        Vector3 focus;

        void Start() => Snap();

        void LateUpdate() => Step(Time.deltaTime);

        public void Snap()
        {
            focus = WantedFocus();
            Place();
        }

        public void Step(float dt)
        {
            float k = 1f - Mathf.Exp(-followSharpness * dt);
            focus = Vector3.Lerp(focus, WantedFocus(), k);
            Place();
        }

        Vector3 WantedFocus()
        {
            Vector3 d = diver.transform.position;
            Vector3 z = zone.transform.position;

            switch (diver.Current)
            {
                case Diver.State.Ready:
                case Diver.State.Crouching:
                    return Vector3.Lerp(d, z, zoneBiasStanding);
                case Diver.State.Airborne:
                    return Vector3.Lerp(d, z, zoneBiasFlying);
                default:
                    d.y = Mathf.Max(d.y, zone.WaterLevel + 0.6f);
                    return d;
            }
        }

        void Place()
        {
            if (cam == null) cam = GetComponent<Camera>();
            float hHalf = minHorizontalFov * 0.5f * Mathf.Deg2Rad;
            float vForH = 2f * Mathf.Atan(Mathf.Tan(hHalf) / cam.aspect) * Mathf.Rad2Deg;
            cam.fieldOfView = Mathf.Max(baseFov, vForH);

            Vector3 pos = focus + offset;
            pos.y = Mathf.Max(pos.y, minHeight);
            transform.position = pos;
            transform.LookAt(focus);
        }
    }
}
