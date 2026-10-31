using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace JumpGame.EditorTools
{
    // Props authored in Blender (Assets/_Project/Models/Props, built by Blender/make_nature.py and make_houses.py).
    // The scene builder uses them where they exist and falls back to its own primitives where they do not.
    public static partial class PrototypeSceneBuilder
    {
        const string PropDir = "Assets/_Project/Models/Props/";

        static readonly Dictionary<string, GameObject> propFiles = new();

        static GameObject PropFile(string file)
        {
            if (!propFiles.TryGetValue(file, out var go))
            {
                go = AssetDatabase.LoadAssetAtPath<GameObject>(PropDir + file + ".fbx");
                propFiles[file] = go;
            }
            return go;
        }

        public static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        // A copy of one prop of a model file, placed in the scene; each material slot gets the colour the caller wants.
        static Transform Prop(string file, string name, Transform parent, Vector3 at, Vector3 scale, float yaw,
                              params (string slot, Material mat)[] slots)
        {
            var src = PropFile(file);
            if (src == null) return null;
            var part = FindDeep(src.transform, name);
            if (part == null) { Debug.LogWarning($"Prop '{name}' is not in {file}.fbx"); return null; }

            var copy = Object.Instantiate(part.gameObject);
            copy.name = name;
            var t = copy.transform;
            t.SetParent(parent, false);
            t.localPosition = at;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            t.localScale = scale;
            Retint(t, slots);
            return t;
        }

        static void Retint(Transform root, (string slot, Material mat)[] slots)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    foreach (var (slot, mat) in slots)
                        if (mats[i].name == slot) { mats[i] = mat; break; }
                }
                r.sharedMaterials = mats;
            }
        }

        static Mesh PropMesh(string file, string name)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(PropDir + file + ".fbx"))
                if (o is Mesh m && m.name == name) return m;
            return null;
        }

        // A stable pick among variants from where the prop stands, so a rebuild puts the same tree in the same place.
        static int Variant(Vector3 at, int count) => Mathf.Abs(Mathf.FloorToInt(at.x * 7.3f + at.z * 13.1f + at.y * 3.7f)) % count;
    }
}
