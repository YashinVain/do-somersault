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
        static bool UseLocationModel(Transform site, string file, string[] replaced, string[] hideLife = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Models/Locations/{file}.fbx");
            if (prefab == null) return false;

            foreach (var name in replaced)
            {
                var part = site.Find(name);
                if (part == null) continue;
                foreach (var r in part.GetComponentsInChildren<Renderer>(true))
                    if (!r.gameObject.name.StartsWith("Water")) r.enabled = false;
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
                        shared[i] = Mat(shared[i].name, "#" + ColorUtility.ToHtmlStringRGB(shared[i].color), shared[i].name.Contains("Glass") ? 0.5f : 0.08f);
                r.sharedMaterials = shared;
            }
            return true;
        }
    }
}
