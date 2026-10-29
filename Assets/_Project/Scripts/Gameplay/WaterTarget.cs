using UnityEngine;

namespace JumpGame
{
    // Floating rings on the water surface. Radii go from the center outwards.
    public class WaterTarget : MonoBehaviour
    {
        [SerializeField] float[] radii = { 0.8f, 1.8f, 3f };
        [SerializeField] float[] multipliers = { 3f, 2f, 1f };
        [SerializeField] float missMultiplier = 0.5f;

        public float WaterLevel => transform.position.y;

        public float MultiplierAt(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            float dist = d.magnitude;
            for (int i = 0; i < radii.Length; i++)
                if (dist <= radii[i]) return multipliers[i];
            return missMultiplier;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            foreach (float r in radii)
            {
                Vector3 prev = transform.position + new Vector3(r, 0f, 0f);
                for (int i = 1; i <= 32; i++)
                {
                    float a = i / 32f * Mathf.PI * 2f;
                    Vector3 p = transform.position + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    Gizmos.DrawLine(prev, p);
                    prev = p;
                }
            }
        }
#endif
    }
}
