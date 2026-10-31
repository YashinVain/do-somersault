using UnityEngine;

namespace JumpGame
{
    // Faceted water: a grid whose corners follow the shared swell. Every triangle keeps its own vertices
    // so the light catches each facet, the low-poly sea look.
    [RequireComponent(typeof(MeshFilter))]
    public class WaveSurface : MonoBehaviour
    {
        [SerializeField] Vector2 size = new Vector2(240f, 240f);
        [SerializeField] int cells = 40;

        Mesh mesh;
        Vector3[] grid;     // one point per grid corner, local space
        float[] heights;    // swell height per grid corner, worked out once per frame
        Vector3[] verts;    // triangle soup
        int[] corner;       // which grid corner each vertex follows

        void Awake()
        {
            int n = cells + 1;
            grid = new Vector3[n * n];
            heights = new float[n * n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                    grid[z * n + x] = new Vector3((x / (float)cells - 0.5f) * size.x, 0f, (z / (float)cells - 0.5f) * size.y);

            verts = new Vector3[cells * cells * 6];
            corner = new int[verts.Length];
            var tris = new int[verts.Length];
            int v = 0;
            for (int z = 0; z < cells; z++)
                for (int x = 0; x < cells; x++)
                {
                    int a = z * n + x, b = a + 1, c = a + n, d = c + 1;
                    foreach (int i in new[] { a, c, d, a, d, b })
                    {
                        corner[v] = i;
                        tris[v] = v;
                        v++;
                    }
                }

            mesh = new Mesh { name = "Waves" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            Refresh();
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(size.x, 4f, size.y));
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        bool visible = true;

        // Only the water a camera can see is worth moving; the other locations' seas stay still.
        void OnBecameVisible() => visible = true;
        void OnBecameInvisible() => visible = false;

        void Update()
        {
            WaterMotion.Tick();
            if (!visible || WaterMotion.Amplitude <= 0f) return;
            Refresh();
            mesh.RecalculateNormals();
        }

        void Refresh()
        {
            // The surface is never rotated or scaled, so world position is just an offset.
            Vector3 origin = transform.position;
            for (int i = 0; i < grid.Length; i++)
                heights[i] = WaterMotion.Height(origin + grid[i]);

            for (int i = 0; i < verts.Length; i++)
            {
                var p = grid[corner[i]];
                p.y = heights[corner[i]];
                verts[i] = p;
            }
            mesh.vertices = verts;
        }
    }
}
