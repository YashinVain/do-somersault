using UnityEngine;

namespace JumpGame
{
    // A seagull riding the wind in wide circles: a few wing beats, then a glide.
    public class Circler : MonoBehaviour
    {
        [SerializeField] Vector3 center;
        [SerializeField] float radius = 12f;
        [SerializeField] float height = 18f;
        [Tooltip("Radians per second; negative circles the other way.")]
        [SerializeField] float speed = 0.25f;
        [SerializeField] float phase;
        [SerializeField] Transform wingL, wingR;

        void Update()
        {
            float a = phase + Time.time * speed;
            var p = center + new Vector3(Mathf.Cos(a) * radius, height + Mathf.Sin(a * 2.3f) * 1.2f, Mathf.Sin(a) * radius);
            var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * Mathf.Sign(speed);
            float bank = -22f * Mathf.Sign(speed);
            transform.localPosition = p;
            transform.localRotation = Quaternion.LookRotation(tangent) * Quaternion.Euler(0f, 0f, bank);

            // Beat for about two seconds out of five, glide with slightly raised wings the rest.
            float cycle = Mathf.Repeat(Time.time + phase * 3f, 5f);
            float beat = cycle < 2f ? Mathf.Sin(Time.time * 9f) * 38f : 8f;
            wingL.localRotation = Quaternion.Euler(0f, 0f, beat);
            wingR.localRotation = Quaternion.Euler(0f, 0f, -beat);
        }
    }
}
