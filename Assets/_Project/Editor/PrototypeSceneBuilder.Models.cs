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
                    {
                        // One material per location and purpose ("Lake_Wood"): two locations may call different browns "Wood".
                        string role = shared[i].name;
                        var made = Mat(file.Substring(file.IndexOf('_') + 1) + "_" + role, "#" + ColorUtility.ToHtmlStringRGB(shared[i].color), role.Contains("Glass") ? 0.5f : 0.08f);
                        // Night scenes: windows and lamps (Lit, RedLight, GreenLight) glow.
                        if (role == "Lit") made = Glow(made, 1.6f);
                        else if (role == "RedLight") made = Glow(made, 1.3f);
                        else if (role == "GreenLight") made = Glow(made, 1.2f);
                        else if (role == "Fall") made = Glow(made, 0.06f);        // the waterfall's pale sheet
                        shared[i] = made;
                    }
                r.sharedMaterials = shared;
            }
            return true;
        }

        // The corners of the cliff wall's faceted face, for Blender/lowpoly/loc_cliffs.py: the model has to follow exactly the same rock the
        // collision boxes (WallSolids) were fitted to. Run: Unity -batchmode -executeMethod JumpGame.EditorTools.PrototypeSceneBuilder.DumpCliffWall
        public static void DumpCliffWall()
            => DumpWall(LowPolyMeshes.CliffWall("CliffWall", CliffBlockSize, -6f, CliffTop, 24, 9, 3, CliffBack, CliffFaceZ, 21), "cliff_wall.txt");

        // The same for the glacier of the northern bay (Blender/lowpoly/loc_arctic.py): -executeMethod JumpGame.EditorTools.PrototypeSceneBuilder.DumpGlacierWall
        public static void DumpGlacierWall()
            => DumpWall(LowPolyMeshes.CliffWall("GlacierWall", SeaBlockSize, -6f, GlacierTop, 24, 10, 3, GlacierBack, GlacierFaceZ, 33), "glacier_wall.txt");

        // The jungle's wall and where the game puts its own swaying trees and palms (Blender/lowpoly/loc_jungle.py keeps clear of them):
        // -executeMethod JumpGame.EditorTools.PrototypeSceneBuilder.DumpJungle
        public static void DumpJungle()
        {
            DumpWall(LowPolyMeshes.CliffWall("JungleWall", SeaBlockSize, -6f, JungleTop, 24, 10, 3, JungleBack, JungleFaceZ, 52), "jungle_wall.txt");
            BuildJungleSite();
            var site = GameObject.Find("Site_Jungle").transform;
            var sb = new System.Text.StringBuilder();
            foreach (var t in site.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n == "Jungle_A" || n == "Jungle_B" || n == "Jungle_C" || n == "Palm_A" || n == "Palm_B" || n == "Tree" || n == "Palm")
                {
                    var p = t.position - site.position;
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} {1:F3} {2:F3} {3:F3} {4:F3}", n, p.x, p.y, p.z, t.lossyScale.x));
                }
            }
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Blender", "lowpoly", "data"));
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "jungle_trees.txt"), sb.ToString());
            Debug.Log("jungle trees dumped");
        }

        static void DumpWall(Mesh mesh, string file)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var v in mesh.vertices)
                sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F4} {1:F4} {2:F4}", v.x, v.y, v.z));
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Blender", "lowpoly", "data"));
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, file), sb.ToString());
            Debug.Log(file + " dumped: " + mesh.vertexCount + " vertices");
        }
    }
}
