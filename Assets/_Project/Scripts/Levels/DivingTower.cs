using UnityEngine;

namespace JumpGame
{
    // Platforms of the tower. Each edge marker sits on the top surface, at the middle of the front edge, facing the pool.
    // Solids are world-space boxes the diver's body bumps into.
    public class DivingTower : MonoBehaviour
    {
        [SerializeField] Transform[] edges;
        [SerializeField] Bounds[] solids;

        public int Count => edges.Length;
        public Transform Edge(int i) => edges[Mathf.Clamp(i, 0, edges.Length - 1)];
        public float Height(int i) => Edge(i).position.y;

        // Deepest overlap of a point with the tower: how far to push it out and in which direction.
        public bool Penetration(Vector3 p, out Vector3 normal, out float depth)
        {
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
