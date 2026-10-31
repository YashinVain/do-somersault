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

        // The colours every structure model falls back on; a caller lists the slots it wants different first.
        static (string slot, Material mat)[] StructureSlots(params (string slot, Material mat)[] first)
        {
            var common = new (string, Material)[]
            {
                ("Steel", Mat("Metal", "#BFC6CE", smoothness: 0.6f)), ("Black", Mat("Struct_Black", "#1E2126")),
                ("White", Mat("Struct_White", "#E8E6DE")), ("Red", Mat("Struct_Red", "#C9413A")), ("Gold", Mat("Struct_Gold", "#C69A3C", smoothness: 0.6f)),
                ("Seat", Mat("Gondola_Seat", "#9B2F36")), ("Rubber", Mat("Struct_Rubber", "#16181B")),
                ("Lamp", Glow(Mat("Struct_Lamp", "#FFE7A3", smoothness: 0.9f), 0.7f)), ("Stone", Mat("Town_StoneDark", "#9A9080")),
                ("Wood", Mat("Struct_Wood", "#8A5E3B")), ("WoodDark", Mat("WoodDark", "#6E4A2F")), ("Thatch", Mat("Jungle_Thatch", "#B59A4E")),
                ("Glass", Mat("Struct_Glass", "#24384A", smoothness: 0.9f)), ("Teak", Mat("Yacht_Deck", "#B88A5A")), ("Hull", Mat("Yacht_Hull", "#F4F3EE", smoothness: 0.6f)),
                ("Stripe", Mat("Yacht_Stripe", "#1F4E79")), ("Paint", Mat("Cont_Blue", "#2F5B94")), ("DerrickRed", Mat("Rig_Red", "#C2412F")),
                ("WallSteel", Mat("Port_Wall", "#8A8F96")), ("RoofSteel", Mat("Port_Roof", "#3D4249")),
                ("Penguin_Black", Mat("Penguin_Black", "#1C2230", smoothness: 0.3f)), ("Penguin_White", Mat("Penguin_White", "#F4F4F0", smoothness: 0.2f)),
                ("Penguin_Orange", Mat("Penguin_Orange", "#F28C28")), ("Eye", Mat("Eye", "#1B1F2A")), ("Ice", Mat("Ice_B", "#B3D3E5", smoothness: 0.5f))
            };
            var all = new (string slot, Material mat)[first.Length + common.Length];
            first.CopyTo(all, 0);
            for (int i = 0; i < common.Length; i++) all[first.Length + i] = common[i];
            return all;
        }

        static Mesh IceMesh => PropMesh("Structures", "Ice_" + "ABC"[iceCall++ % 3]) ?? RockMesh;
        static int iceCall;

        // A stable pick among variants from where the prop stands, so a rebuild puts the same tree in the same place.
        static int Variant(Vector3 at, int count) => Mathf.Abs(Mathf.FloorToInt(at.x * 7.3f + at.z * 13.1f + at.y * 3.7f)) % count;
    }
}
