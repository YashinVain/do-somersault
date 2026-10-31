using System.Text;
using UnityEditor;
using UnityEngine;

namespace JumpGame.EditorTools
{
    // Prints what Unity made of the FBX files in Models: the hierarchy, each part's local transform and material names.
    public static class ModelDebug
    {
        public static void Dump()
        {
            var sb = new StringBuilder();
            foreach (var path in new[] { "Props/Trees", "Props/Rocks", "Props/Cliffs", "Props/Houses", "Characters/Diver" })
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Models/{path}.fbx");
                if (go == null) continue;
                sb.AppendLine("== " + path);
                Walk(go.transform, 0, sb);
            }
            System.IO.File.WriteAllText("Logs/model_dump.txt", sb.ToString());
        }

        static void Walk(Transform t, int depth, StringBuilder sb)
        {
            var r = t.GetComponent<Renderer>();
            var mf = t.GetComponent<MeshFilter>();
            string mats = r == null ? "" : " mats=[" + string.Join(",", System.Array.ConvertAll(r.sharedMaterials, m => m == null ? "null" : m.name)) + "]";
            string mesh = mf != null && mf.sharedMesh != null ? $" mesh={mf.sharedMesh.name} bounds={mf.sharedMesh.bounds.size.ToString("0.00")} subs={mf.sharedMesh.subMeshCount}" : "";
            sb.AppendLine($"{new string(' ', depth * 2)}{t.name} pos={t.localPosition.ToString("0.00")} rot={t.localEulerAngles.ToString("0")} scale={t.localScale.ToString("0.00")}{mesh}{mats}");
            foreach (Transform c in t) Walk(c, depth + 1, sb);
        }
    }
}
