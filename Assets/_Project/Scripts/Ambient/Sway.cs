using UnityEngine;

namespace JumpGame
{
    // Gentle back-and-forth around one local axis: flags, reeds, palm leaves.
    public class Sway : MonoBehaviour
    {
        [SerializeField] Vector3 axis = Vector3.forward;
        [SerializeField] float angle = 6f;
        [SerializeField] float speed = 1.2f;
        [SerializeField] float phase;

        Quaternion rest;

        void Start() => rest = transform.localRotation;

        void Update()
        {
            float a = Mathf.Sin(Time.time * speed + phase) * angle
                    + Mathf.Sin(Time.time * speed * 2.7f + phase * 1.7f) * angle * 0.25f;
            transform.localRotation = rest * Quaternion.AngleAxis(a, axis);
        }
    }
}
