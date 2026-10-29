using UnityEngine;

namespace JumpGame
{
    // Procedural poses for the body, plus the mass model the flight physics reads from it.
    public class DiverPose : MonoBehaviour
    {
        [SerializeField] Transform rig;
        [SerializeField] Transform spine;
        [SerializeField] Transform armL, armR;
        [SerializeField] Transform foreL, foreR;
        [SerializeField] Transform thighL, thighR;
        [SerializeField] Transform kneeL, kneeR;
        [SerializeField] Transform ankleL, ankleR;
        [SerializeField] float thighLength = 0.44f;
        [SerializeField] float shinLength = 0.43f;

        // Angles in degrees. arm: 0 = hanging, 90 = forward, 180 = overhead. elbow: positive bends the forearm forward.
        // thigh: negative lifts the knee. knee: positive bends the shin back. ankle: positive points the toes down the shin.
        struct Pose
        {
            public float arm, elbow, spine, thigh, knee, ankle;
            public Pose(float arm, float elbow, float spine, float thigh, float knee, float ankle)
            {
                this.arm = arm; this.elbow = elbow; this.spine = spine; this.thigh = thigh; this.knee = knee; this.ankle = ankle;
            }
        }

        // On the platform the ankle is solved to keep the sole flat, so the ankle value there is ignored.
        static readonly Pose Stand = new Pose(4f, 10f, 0f, 0f, 0f, 0f);
        static readonly Pose Crouch = new Pose(-40f, 20f, 30f, -80f, 120f, 0f);
        static readonly Pose Reach = new Pose(172f, 4f, -6f, 0f, 4f, 70f);
        static readonly Pose Tuck = new Pose(28f, 75f, 40f, -128f, 150f, 55f);
        static readonly Pose Pike = new Pose(95f, 8f, 12f, -12f, 6f, 70f);
        static readonly Pose Streamline = new Pose(179f, 0f, 0f, 0f, 0f, 80f);
        static readonly Pose Float = new Pose(150f, 20f, 6f, -8f, 20f, 35f);

        public float Crouched { get; set; }
        public float Tucked { get; set; }
        public bool Airborne { get; set; }
        public bool JustLaunched { get; set; }
        public bool HeadFirst { get; set; }
        public bool Underwater { get; set; }
        // Fell off the edge while still holding: the crouch curls up instead of opening.
        public bool Slipping { get; set; }

        float arm, elbow, spineBend, thigh, knee, ankle;
        float armVel, elbowVel, spineVel, thighVel, kneeVel, ankleVel;
        float airTime, idleTime;

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
                idleTime += dt;
                target = Lerp(Stand, Crouch, Crouched);
                target.arm += Mathf.Sin(idleTime * 2.1f) * 3f * (1f - Crouched);
            }
            else
            {
                airTime += dt;
                // After a real takeoff the arms swing up; after a slip the body only opens when asked to.
                Pose open = Slipping ? Crouch
                    : JustLaunched && airTime < 0.25f ? Reach
                    : HeadFirst ? Streamline : Pike;
                if (Slipping) open.ankle = Tuck.ankle;
                target = Lerp(open, Tuck, Tucked);
            }

            arm = Mathf.SmoothDamp(arm, target.arm, ref armVel, 0.09f / speed, Mathf.Infinity, dt);
            elbow = Mathf.SmoothDamp(elbow, target.elbow, ref elbowVel, 0.08f / speed, Mathf.Infinity, dt);
            spineBend = Mathf.SmoothDamp(spineBend, target.spine, ref spineVel, 0.1f / speed, Mathf.Infinity, dt);
            thigh = Mathf.SmoothDamp(thigh, target.thigh, ref thighVel, 0.07f / speed, Mathf.Infinity, dt);
            knee = Mathf.SmoothDamp(knee, target.knee, ref kneeVel, 0.09f / speed, Mathf.Infinity, dt);

            if (Airborne || Underwater)
                ankle = Mathf.SmoothDamp(ankle, target.ankle, ref ankleVel, 0.12f / speed, Mathf.Infinity, dt);
            else
            {
                ankle = FlatAnkle();
                ankleVel = 0f;
            }

            Apply();
        }

        // Cancels the shin's tilt so the sole stays level with the platform.
        float FlatAnkle() => -(thigh + knee);

        void Apply()
        {
            armL.localRotation = Quaternion.Euler(-arm, 0f, -8f);
            armR.localRotation = Quaternion.Euler(-arm, 0f, 8f);
            foreL.localRotation = foreR.localRotation = Quaternion.Euler(-elbow, 0f, 0f);
            spine.localRotation = Quaternion.Euler(spineBend, 0f, 0f);
            thighL.localRotation = thighR.localRotation = Quaternion.Euler(thigh, 0f, 0f);
            kneeL.localRotation = kneeR.localRotation = Quaternion.Euler(knee, 0f, 0f);
            ankleL.localRotation = ankleR.localRotation = Quaternion.Euler(ankle, 0f, 0f);

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

        // Foot points in ankle space: the sole sits 3 cm below the ankle joint.
        static readonly Vector3 Toe = new Vector3(0f, -0.03f, 0.16f);
        static readonly Vector3 Heel = new Vector3(0f, -0.03f, -0.08f);

        Vector3 Local(Transform t, Vector3 p) => transform.InverseTransformPoint(t.TransformPoint(p));

        // Middle of both toe tips in the diver's local space: the point the body tips over on the edge.
        public Vector3 LocalToes() => (Local(ankleL, Toe) + Local(ankleR, Toe)) * 0.5f;

        public Vector3 LocalCenterOfMass()
        {
            Vector3 c = Vector3.zero;
            float m = 0f;
            void Add(Vector3 p, float w) { c += p * w; m += w; }
            ForEachMass(Add);
            return c / m;
        }

        // Moment of inertia around the somersault axis, through the given centre of mass.
        public float Inertia(Vector3 com)
        {
            float i = 0.025f; // the segments' own inertia, roughly constant
            void Add(Vector3 p, float w)
            {
                float dy = p.y - com.y, dz = p.z - com.z;
                i += w * (dy * dy + dz * dz);
            }
            ForEachMass(Add);
            return i;
        }

        delegate void MassVisitor(Vector3 localPos, float mass);

        // Segment centres and masses (fractions of body mass).
        void ForEachMass(MassVisitor visit)
        {
            visit(Local(rig, Vector3.zero), 0.12f);
            visit(Local(spine, new Vector3(0f, 0.25f, 0f)), 0.38f);
            visit(Local(spine, new Vector3(0f, 0.63f, 0f)), 0.08f);
            visit(Local(armL, new Vector3(0f, -0.13f, 0f)), 0.028f);
            visit(Local(armR, new Vector3(0f, -0.13f, 0f)), 0.028f);
            visit(Local(foreL, new Vector3(0f, -0.15f, 0f)), 0.022f);
            visit(Local(foreR, new Vector3(0f, -0.15f, 0f)), 0.022f);
            visit(Local(thighL, new Vector3(0f, -0.2f, 0f)), 0.1f);
            visit(Local(thighR, new Vector3(0f, -0.2f, 0f)), 0.1f);
            visit(Local(kneeL, new Vector3(0f, -0.24f, 0.02f)), 0.06f);
            visit(Local(kneeR, new Vector3(0f, -0.24f, 0.02f)), 0.06f);
        }

        // Lowest world-space height of the body's extremities.
        public float LowestPoint()
        {
            float y = float.MaxValue;
            void Check(Transform t, Vector3 p) => y = Mathf.Min(y, t.TransformPoint(p).y);

            Check(spine, new Vector3(0f, 0.8f, 0f));
            Check(rig, new Vector3(0f, -0.14f, 0f));
            Check(foreL, Vector3.zero);
            Check(foreR, Vector3.zero);
            Check(foreL, new Vector3(0f, -0.34f, 0f));
            Check(foreR, new Vector3(0f, -0.34f, 0f));
            Check(kneeL, Vector3.zero);
            Check(kneeR, Vector3.zero);
            foreach (var a in new[] { ankleL, ankleR })
            {
                Check(a, Toe);
                Check(a, Heel);
            }
            return y;
        }

        // Points used for contact with the tower and the floats.
        public void ContactPoints(Vector3[] into)
        {
            into[0] = spine.TransformPoint(new Vector3(0f, 0.63f, 0f));
            into[1] = spine.TransformPoint(new Vector3(0f, 0.25f, 0f));
            into[2] = rig.position;
            into[3] = foreL.TransformPoint(new Vector3(0f, -0.3f, 0f));
            into[4] = foreR.TransformPoint(new Vector3(0f, -0.3f, 0f));
            into[5] = kneeL.position;
            into[6] = kneeR.position;
            into[7] = ankleL.TransformPoint(Toe);
            into[8] = ankleR.TransformPoint(Toe);
            into[9] = ankleL.TransformPoint(Heel);
            into[10] = ankleR.TransformPoint(Heel);
            into[11] = spine.TransformPoint(new Vector3(0f, 0.79f, 0f));
            into[12] = foreL.position;
            into[13] = foreR.position;
        }

        public const int ContactCount = 14;

        public void ResetPose()
        {
            Crouched = Tucked = 0f;
            Airborne = JustLaunched = HeadFirst = Underwater = Slipping = false;
            airTime = 0f;
            arm = Stand.arm;
            elbow = Stand.elbow;
            spineBend = thigh = knee = ankle = 0f;
            armVel = elbowVel = spineVel = thighVel = kneeVel = ankleVel = 0f;
            Apply();
        }

        static Pose Lerp(Pose a, Pose b, float t) => new Pose(
            Mathf.Lerp(a.arm, b.arm, t),
            Mathf.Lerp(a.elbow, b.elbow, t),
            Mathf.Lerp(a.spine, b.spine, t),
            Mathf.Lerp(a.thigh, b.thigh, t),
            Mathf.Lerp(a.knee, b.knee, t),
            Mathf.Lerp(a.ankle, b.ankle, t));
    }
}
