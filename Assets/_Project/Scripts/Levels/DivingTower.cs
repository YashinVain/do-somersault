using UnityEngine;

namespace JumpGame
{
    // Platforms of the tower. Each edge marker sits on the top surface, at the middle of the front edge, facing the pool.
    // Solids are boxes in the tower's own space (it is never rotated or scaled) that the diver's body bumps into.
    public class DivingTower : MonoBehaviour
    {
        [SerializeField] Transform[] edges;
        [SerializeField] Bounds[] solids;
        [Tooltip("Per spot: +1 films from -X, -1 from +X, 0 picks by the spot's position.")]
        [SerializeField] float[] cameraSides;

        public int Count => edges.Length;
        public float CameraSide(int i) => cameraSides != null && i < cameraSides.Length ? cameraSides[i] : 0f;
        public Transform Edge(int i) => edges[Mathf.Clamp(i, 0, edges.Length - 1)];
        public float Height(int i) => Edge(i).position.y;

        // Deepest overlap of a point with the tower: how far to push it out and in which direction.
        public bool Penetration(Vector3 world, out Vector3 normal, out float depth)
        {
            Vector3 p = world - transform.position;
            normal = Vector3.zero;
            depth = 0f;
            bool hit = false;
            foreach (var b in solids)
            {
                if (!b.Contains(p)) continue;
                Vector3 lo = p - b.min, hi = b.max - p;

                // Leave through the nearest face.
                float d = hi.y; Vector3 n = Vector3.up;
                if (lo.y < d) { d = lo.y; n = Vector3.down; }
                if (hi.z < d) { d = hi.z; n = Vector3.forward; }
                if (lo.z < d) { d = lo.z; n = Vector3.back; }
                if (hi.x < d) { d = hi.x; n = Vector3.right; }
                if (lo.x < d) { d = lo.x; n = Vector3.left; }

                if (d > depth)
                {
                    depth = d;
                    normal = n;
                    hit = true;
                }
            }
            return hit;
        }
    }
}
