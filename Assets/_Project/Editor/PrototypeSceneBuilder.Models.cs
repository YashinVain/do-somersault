using UnityEditor;
using UnityEngine;

namespace JumpGame.EditorTools
{
    // Whole locations modelled in Blender (Models/Locations/Loc_<Name>.fbx, one file per location, see Docs/ModelBrief.md).
    // When the file exists it replaces the box-built look of the site; the jump marks and the solid boxes the game builds from code stay
    // exactly as they were, so the physics and the levels do not change, only what the player sees.
    public static partial class PrototypeSceneBuilder
    {
        // `replaced`: children of the site whose renderers the model takes over. Renderers named Water* always stay (the sea is the
        // game's own shader). `hideLife`: parts of the site's Life group the model already has (bunting, a rubber duck ...).
        static bool UseLocationModel(Transform site, string file, string[] replaced, string[] hideLife = null, string[] keep = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Models/Locations/{file}.fbx");
            if (prefab == null) return false;

            // `keep`: things inside the replaced parts that stay (palms that sway, a bird): a renderer with one of these in its path is left on.
            bool Kept(Transform t, Transform top)
            {
                if (keep == null) return false;
                for (; t != null && t != top.parent; t = t.parent)
                    foreach (var key in keep)
                        if (t.name.Contains(key)) return true;
                return false;
            }
            // Every child with one of these names (a name may repeat: two beaches, three sea rocks).
            foreach (Transform part in site)
            {
                if (System.Array.IndexOf(replaced, part.name) < 0) continue;
                foreach (var r in part.GetComponentsInChildren<Renderer>(true))
                    if (!r.gameObject.name.Contains("Water") && !Kept(r.transform, part)) r.enabled = false;
            }
            if (hideLife != null)
            {
                var life = site.Find("Life");
                if (life != null)
                    foreach (var r in life.GetComponentsInChildren<Renderer>(true))
                        foreach (var key in hideLife)
                            if (r.gameObject.name.Contains(key) || (r.transform.parent != null && r.transform.parent.name.Contains(key))) { r.enabled = false; break; }
            }

            var model = (GameObject)Object.Instantiate(prefab, site);
            model.name = file;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            foreach (var animator in model.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                    if (shared[i] != null)
                        // One material per location and purpose ("Lake_Wood"): two locations may call different browns "Wood".
                        shared[i] = Mat(file.Substring(file.IndexOf('_') + 1) + "_" + shared[i].name, "#" + ColorUtility.ToHtmlStringRGB(shared[i].color), shared[i].name.Contains("Glass") ? 0.5f : 0.08f);
                r.sharedMaterials = shared;
            }
            return true;
        }

        // The corners of the cliff wall's faceted face, for Blender/lowpoly/loc_cliffs.py: the model has to follow exactly the same rock the
        // collision boxes (WallSolids) were fitted to. Run: Unity -batchmode -executeMethod JumpGame.EditorTools.PrototypeSceneBuilder.DumpCliffWall
        public static void DumpCliffWall()
        {
            var mesh = LowPolyMeshes.CliffWall("CliffWall", CliffBlockSize, -6f, CliffTop, 24, 9, 3, CliffBack, CliffFaceZ, 21);
            var sb = new System.Text.StringBuilder();
            foreach (var v in mesh.vertices)
                sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F4} {1:F4} {2:F4}", v.x, v.y, v.z));
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Blender", "lowpoly", "data"));
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "cliff_wall.txt"), sb.ToString());
            Debug.Log("cliff wall dumped: " + mesh.vertexCount + " vertices");
        }
    }
}
