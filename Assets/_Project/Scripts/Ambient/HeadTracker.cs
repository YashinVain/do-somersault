using UnityEngine;

namespace JumpGame
{
    // Turns a head towards a target, within how far a neck can turn.
    public class HeadTracker : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] float maxYaw = 70f;
        [SerializeField] float maxPitch = 35f;
        [SerializeField] float sharpness = 4f;

        Quaternion rest;

        void Start() => rest = transform.localRotation;

        void LateUpdate()
        {
            if (target == null || transform.parent == null) return;

            Vector3 local = transform.parent.InverseTransformPoint(target.position) - transform.localPosition;
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -maxYaw, maxYaw);
            float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -maxPitch, maxPitch);

            var wanted = rest * Quaternion.Euler(pitch, yaw, 0f);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, wanted, 1f - Mathf.Exp(-sharpness * Time.deltaTime));
        }
    }
}
