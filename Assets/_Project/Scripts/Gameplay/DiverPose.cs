using UnityEngine;

namespace JumpGame
{
    // Procedural poses for the placeholder body. Will be replaced by an animator once the Blender model exists.
    public class DiverPose : MonoBehaviour
    {
        [SerializeField] Transform rig;
        [SerializeField] Transform spine;
        [SerializeField] Transform armL, armR;
        [SerializeField] Transform thighL, thighR;
        [SerializeField] Transform kneeL, kneeR;
        [SerializeField] float thighLength = 0.45f;
        [SerializeField] float shinLength = 0.45f;

        // Angles in degrees. arm: 0 = hanging, 90 = forward, 180 = overhead.
        // thigh: negative lifts the knee. knee: positive bends the shin back.
        struct Pose
        {
            public float arm, spine, thigh, knee;
            public Pose(float arm, float spine, float thigh, float knee)
            {
                this.arm = arm; this.spine = spine; this.thigh = thigh; this.knee = knee;
            }
        }

        static readonly Pose Stand = new Pose(4f, 0f, 0f, 0f);
        static readonly Pose Crouch = new Pose(-45f, 28f, -78f, 118f);
        static readonly Pose Reach = new Pose(172f, -6f, 0f, 4f);
        static readonly Pose Tuck = new Pose(62f, 38f, -128f, 148f);
        static readonly Pose Pike = new Pose(95f, 12f, -12f, 6f);
        static readonly Pose Streamline = new Pose(179f, 0f, 0f, 0f);
        static readonly Pose Float = new Pose(150f, 6f, -8f, 20f);

        public float Crouched { get; set; }
        public float Tucked { get; set; }
        public bool Airborne { get; set; }
        public bool HeadFirst { get; set; }
        public bool Underwater { get; set; }
        public bool Flailing { get; set; }

        float arm, spineBend, thigh, knee;
        float armVel, spineVel, thighVel, kneeVel;
        float airTime;

        void LateUpdate() => Step(Time.deltaTime);

        public void Step(float dt)
        {
            if (dt <= 0f) return;

            Pose target;
            float speed = 1f;

            if (Underwater)
            {
                target = Float;
                speed = 0.35f;
            }
            else if (!Airborne)
            {
                target = Lerp(Stand, Crouch, Crouched);
                target.arm += Mathf.Sin(Time.time * 2.1f) * 3f * (1f - Crouched);
            }
            else if (Flailing)
            {
                airTime += dt;
                float w = airTime * 11f;
                target = new Pose(110f + Mathf.Sin(w) * 70f, -10f, -35f + Mathf.Sin(w * 0.8f) * 30f, 40f + Mathf.Cos(w * 0.8f) * 30f);
                speed = 2f;
            }
            else
            {
                airTime += dt;
                // Arms swing overhead right after takeoff, then settle into the open pose.
                Pose open = airTime < 0.25f ? Reach : (HeadFirst ? Streamline : Pike);
                target = Lerp(open, Tuck, Tucked);
            }

            arm = Mathf.SmoothDamp(arm, target.arm, ref armVel, 0.09f / speed, Mathf.Infinity, dt);
            spineBend = Mathf.SmoothDamp(spineBend, target.spine, ref spineVel, 0.1f / speed, Mathf.Infinity, dt);
            thigh = Mathf.SmoothDamp(thigh, target.thigh, ref thighVel, 0.06f / speed, Mathf.Infinity, dt);
            knee = Mathf.SmoothDamp(knee, target.knee, ref kneeVel, 0.085f / speed, Mathf.Infinity, dt);

            Apply();
        }

        void Apply()
        {
            armL.localRotation = Quaternion.Euler(-arm, 0f, -4f);
            armR.localRotation = Quaternion.Euler(-arm, 0f, 4f);
            spine.localRotation = Quaternion.Euler(spineBend, 0f, 0f);
            thighL.localRotation = thighR.localRotation = Quaternion.Euler(thigh, 0f, 0f);
            kneeL.localRotation = kneeR.localRotation = Quaternion.Euler(knee, 0f, 0f);

            // On the platform the feet stay planted, so the hips sink as the legs fold.
            float drop = 0f;
            if (!Airborne)
            {
                float legHeight = thighLength * Mathf.Cos(thigh * Mathf.Deg2Rad)
                                + shinLength * Mathf.Cos((thigh + knee) * Mathf.Deg2Rad);
                drop = thighLength + shinLength - legHeight;
            }
            rig.localPosition = new Vector3(0f, -drop, 0f);
        }

        public void ResetPose()
        {
            Crouched = Tucked = 0f;
            Airborne = HeadFirst = Underwater = Flailing = false;
            airTime = 0f;
            arm = Stand.arm;
            spineBend = thigh = knee = 0f;
            armVel = spineVel = thighVel = kneeVel = 0f;
            Apply();
        }

        static Pose Lerp(Pose a, Pose b, float t) => new Pose(
            Mathf.Lerp(a.arm, b.arm, t),
            Mathf.Lerp(a.spine, b.spine, t),
            Mathf.Lerp(a.thigh, b.thigh, t),
            Mathf.Lerp(a.knee, b.knee, t));
    }
}
