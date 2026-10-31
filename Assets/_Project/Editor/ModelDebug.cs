using System.IO;
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

        // Does turning a bone of the imported character really move the mesh?
        public static void TestSkin()
        {
            var sb = new StringBuilder();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Models/Characters/Diver.fbx");
            var go = Object.Instantiate(prefab);
            var renderers = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            sb.AppendLine($"skinned renderers: {renderers.Length}, mesh renderers: {go.GetComponentsInChildren<MeshRenderer>(true).Length}, animators: {go.GetComponentsInChildren<Animator>(true).Length}");
            SkinnedMeshRenderer body = null;
            foreach (var r in renderers) if (r.name == "Body") body = r;
            if (body == null) { sb.AppendLine("no Body"); File.WriteAllText("Logs/skin_test.txt", sb.ToString()); return; }
            sb.AppendLine($"Body bones: {body.bones.Length}, rootBone: {(body.rootBone != null ? body.rootBone.name : "none")}, weights: {body.sharedMesh.GetAllBoneWeights().Length}");
            var shin = System.Array.Find(go.GetComponentsInChildren<Transform>(), x => x.name == "Shin_L");
            sb.AppendLine($"Shin_L in renderer bones: {System.Array.IndexOf(body.bones, shin)}");
            var before = new Mesh(); body.BakeMesh(before);
            var vb = before.vertices;
            shin.Rotate(Vector3.right, 90f, Space.World);
            var after = new Mesh(); body.BakeMesh(after);
            var va = after.vertices;
            float max = 0f; for (int i = 0; i < vb.Length; i++) max = Mathf.Max(max, (va[i] - vb[i]).magnitude);
            sb.AppendLine($"largest vertex move after turning Shin_L by 90 degrees: {max:0.000} m ({vb.Length} vertices)");
            Object.DestroyImmediate(go);
            File.WriteAllText("Logs/skin_test.txt", sb.ToString());
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
