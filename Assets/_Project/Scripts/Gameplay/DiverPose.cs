using UnityEngine;

namespace JumpGame
{
    // Drives the placeholder body. Will be replaced by an animator once the Blender model exists.
    public class DiverPose : MonoBehaviour
    {
        [SerializeField] Transform rig;
        [SerializeField] Transform armL, armR;
        [SerializeField] Transform thighL, thighR;
        [SerializeField] Transform kneeL, kneeR;
        [SerializeField] float sharpness = 14f;

        struct Pose
        {
            public float arm, thigh, knee, drop;
            public Pose(float arm, float thigh, float knee, float drop)
            {
                this.arm = arm; this.thigh = thigh; this.knee = knee; this.drop = drop;
            }
        }

        static readonly Pose Stand = new Pose(0f, 0f, 0f, 0f);
        static readonly Pose Crouch = new Pose(50f, -70f, 120f, 0.3f);
        static readonly Pose Reach = new Pose(175f, 0f, 0f, 0f);
        static readonly Pose Tuck = new Pose(-70f, -125f, 140f, 0f);

        float arm, thigh, knee, drop;

        public float Crouched { get; set; }
        public float Tucked { get; set; }
        public bool Airborne { get; set; }

        void LateUpdate()
        {
            Pose target;
            if (!Airborne)
                target = Lerp(Stand, Crouch, Crouched);
            else
                target = Lerp(Reach, Tuck, Tucked);

            float k = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
            arm = Mathf.Lerp(arm, target.arm, k);
            thigh = Mathf.Lerp(thigh, target.thigh, k);
            knee = Mathf.Lerp(knee, target.knee, k);
            drop = Mathf.Lerp(drop, target.drop, k);

            armL.localRotation = armR.localRotation = Quaternion.Euler(-arm, 0f, 0f);
            thighL.localRotation = thighR.localRotation = Quaternion.Euler(thigh, 0f, 0f);
            kneeL.localRotation = kneeR.localRotation = Quaternion.Euler(knee, 0f, 0f);
            rig.localPosition = new Vector3(0f, -drop, 0f);
        }

        public void Snap()
        {
            arm = thigh = knee = drop = 0f;
        }

        static Pose Lerp(Pose a, Pose b, float t) => new Pose(
            Mathf.Lerp(a.arm, b.arm, t),
            Mathf.Lerp(a.thigh, b.thigh, t),
            Mathf.Lerp(a.knee, b.knee, t),
            Mathf.Lerp(a.drop, b.drop, t));
    }
}
