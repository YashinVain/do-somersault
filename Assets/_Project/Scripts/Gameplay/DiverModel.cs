using System;
using System.Collections.Generic;
using UnityEngine;

namespace JumpGame
{
    // Puts an authored character (an FBX from Blender) in place of the placeholder body. The model's bones copy the
    // rotations of the body's joints, so the physics, the poses and the medals stay exactly as they are; only the look changes.
    // The bone and material names a model must use are listed in Docs/Blender.md.
    public class DiverModel : MonoBehaviour
    {
        [SerializeField] DiverPose pose;
        [Tooltip("Character models by name; a skin picks one with its `model` field, the first one is the default.")]
        [SerializeField] GameObject[] models;
        [Tooltip("The primitives of the placeholder body: hidden while an authored model is shown.")]
        [SerializeField] Renderer[] placeholder;

        const string FatShape = "Fat";

        class Bound
        {
            public GameObject root;
            public Transform[] bones;
            public Transform[] joints;
            public Quaternion[] rest;
            public Vector3 hipsRest;
            public Dictionary<SkinRole, Material> materials = new Dictionary<SkinRole, Material>();
            public Renderer[] renderers;
            public List<(GameObject part, Gear gear)> gear = new List<(GameObject, Gear)>();
            public List<(GameObject part, Gear gear)> without = new List<(GameObject, Gear)>();
            public List<(SkinnedMeshRenderer mesh, int index)> fat = new List<(SkinnedMeshRenderer, int)>();
        }

        readonly Dictionary<GameObject, Bound> bound = new Dictionary<GameObject, Bound>();
        Bound active;

        public bool HasModels => models != null && models.Length > 0 && models[0] != null;

        // Shows the model of this character (or the placeholder if it has none) and dresses it.
        public void Use(CharacterSkin s)
        {
            Bound target = null;
            if (HasModels)
            {
                var prefab = Find(s.model);
                if (prefab != null && !bound.TryGetValue(prefab, out target))
                {
                    target = Bind(prefab);
                    bound[prefab] = target;
                }
            }

            if (active != null && active != target) active.root.SetActive(false);
            active = target;
            if (placeholder != null)
                foreach (var r in placeholder) if (r != null) r.enabled = target == null;

            if (active == null) return;
            active.root.SetActive(true);
            Dress(active, s);
            Retarget(active);
        }

        GameObject Find(string name)
        {
            if (!string.IsNullOrEmpty(name))
                foreach (var m in models)
                    if (m != null && m.name == name) return m;
            return models[0];
        }

        Bound Bind(GameObject prefab)
        {
            var b = new Bound { root = Instantiate(prefab, transform) };
            b.root.name = prefab.name;
            b.root.transform.localPosition = Vector3.zero;
            b.root.transform.localRotation = Quaternion.identity;
            b.root.transform.localScale = Vector3.one;

            var names = DiverPose.JointNames;
            var bones = new List<Transform>();
            var joints = new List<Transform>();
            var children = b.root.GetComponentsInChildren<Transform>(true);
            foreach (var n in names)
            {
                Transform bone = null;
                foreach (var c in children) if (c.name == n) { bone = c; break; }
                if (bone == null) { Debug.LogError($"DiverModel: model '{prefab.name}' has no bone '{n}'"); continue; }
                bones.Add(bone);
                joints.Add(pose.Joint(n));
            }

            // Parents first, so a child's position follows its parent's new rotation.
            var order = new List<int>();
            for (int i = 0; i < bones.Count; i++) order.Add(i);
            order.Sort((x, y) => Depth(bones[x]).CompareTo(Depth(bones[y])));
            b.bones = new Transform[bones.Count];
            b.joints = new Transform[bones.Count];
            b.rest = new Quaternion[bones.Count];
            var inverse = Quaternion.Inverse(transform.rotation);
            for (int i = 0; i < order.Count; i++)
            {
                b.bones[i] = bones[order[i]];
                b.joints[i] = joints[order[i]];
                b.rest[i] = inverse * b.bones[i].rotation;
                if (b.bones[i].name == "Hips") b.hipsRest = transform.InverseTransformPoint(b.bones[i].position);
            }

            // One private material per role, so tinting never edits the imported assets.
            b.renderers = b.root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in b.renderers)
            {
                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    if (shared[i] == null || !RoleOf(shared[i].name, out var role)) continue;
                    if (!b.materials.TryGetValue(role, out var m))
                    {
                        m = new Material(shared[i]) { name = "Skin_" + role };
                        b.materials[role] = m;
                    }
                    shared[i] = m;
                }
                r.sharedMaterials = shared;

                if (r is SkinnedMeshRenderer sm && sm.sharedMesh != null)
                {
                    int fat = sm.sharedMesh.GetBlendShapeIndex(FatShape);
                    if (fat >= 0) b.fat.Add((sm, fat));
                    sm.updateWhenOffscreen = true;
                }
            }

            // Props are children named Gear_<Name> (or Gear_<Name>_<Name> for several): shown when the character has any of them.
            // NoGear_<Name> is the opposite: the part that a prop replaces (the leg under a prosthetic).
            foreach (var c in children)
            {
                bool not = c.name.StartsWith("NoGear_");
                if (!not && !c.name.StartsWith("Gear_")) continue;
                Gear flags = Gear.None;
                foreach (var token in c.name.Substring(not ? 7 : 5).Split('_'))
                    if (Enum.TryParse(token, out Gear g)) flags |= g;
                if (flags == Gear.None) continue;
                (not ? b.without : b.gear).Add((c.gameObject, flags));
            }
            return b;
        }

        static int Depth(Transform t)
        {
            int d = 0;
            for (; t != null; t = t.parent) d++;
            return d;
        }

        static bool RoleOf(string materialName, out SkinRole role)
        {
            // Blender adds ".001" to duplicates and Unity may add " (Instance)".
            int cut = materialName.IndexOfAny(new[] { '.', ' ' });
            string key = cut > 0 ? materialName.Substring(0, cut) : materialName;
            return Enum.TryParse(key, out role);
        }

        void Dress(Bound b, CharacterSkin s)
        {
            foreach (var kv in b.materials)
            {
                DiverSkin.Colour(kv.Key, s, out var colour, out float gloss);
                kv.Value.color = colour;
                if (kv.Value.HasProperty("_Glossiness")) kv.Value.SetFloat("_Glossiness", gloss);
            }
            foreach (var g in b.gear) g.part.SetActive((s.gear & g.gear) != 0);
            foreach (var g in b.without) g.part.SetActive((s.gear & g.gear) == 0);
            // A heavy character is the same body with the "Fat" shape key blended in (authored as +50 % width).
            float fat = Mathf.Clamp01((s.width - 1f) / 0.5f) * 100f;
            foreach (var f in b.fat) f.mesh.SetBlendShapeWeight(f.index, fat);
        }

        void LateUpdate() => Sync();

        // Also called by the editor capture tool, which renders without playing.
        public void Sync()
        {
            if (active != null) Retarget(active);
        }

        // Each bone takes the turn of its joint, measured from the rest pose in the diver's own space.
        void Retarget(Bound b)
        {
            var body = transform.rotation;
            var inverse = Quaternion.Inverse(body);
            for (int i = 0; i < b.bones.Length; i++)
            {
                var turn = inverse * b.joints[i].rotation;
                b.bones[i].rotation = body * turn * b.rest[i];
                if (b.bones[i].name == "Hips")
                    b.bones[i].position = transform.TransformPoint(b.hipsRest + b.joints[i].localPosition);
            }
        }

        void OnDestroy()
        {
            foreach (var b in bound.Values)
                foreach (var m in b.materials.Values) if (m != null) Destroy(m);
        }
    }
}
