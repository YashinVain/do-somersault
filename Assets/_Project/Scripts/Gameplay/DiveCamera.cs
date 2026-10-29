using UnityEngine;

namespace JumpGame
{
    public class DiveCamera : MonoBehaviour
    {
        [SerializeField] Diver diver;
        [SerializeField] Vector3 offset = new Vector3(-7f, 1.5f, -1.5f);
        [SerializeField] Vector3 lookOffset = new Vector3(0f, -0.5f, 1.5f);
        [SerializeField] float followSharpness = 5f;
        [SerializeField] float minHeight = 0.6f;

        Vector3 look;

        void Start()
        {
            transform.position = diver.transform.position + offset;
            look = diver.transform.position + lookOffset;
            transform.LookAt(look);
        }

        void LateUpdate()
        {
            Vector3 focus = diver.transform.position;
            if (diver.Current == Diver.State.Underwater)
                focus.y = Mathf.Max(focus.y, 0f);

            float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            Vector3 wanted = focus + offset;
            wanted.y = Mathf.Max(wanted.y, minHeight);

            transform.position = Vector3.Lerp(transform.position, wanted, k);
            look = Vector3.Lerp(look, focus + lookOffset, k);
            transform.LookAt(look);
        }
    }
}
