using System;
using System.Collections.Generic;
using UnityEngine;

namespace JumpGame
{
    // Puts an authored character (an FBX from Blender) in place of the placeholder body. The model's bones copy the
    // rotations of the body's joints, so the physics, the poses and the medals stay exactly as they are; only the look changes.
    // On top of that copy runs a layer of loose, springy motion (bent knees, flung arms, a nodding head) that never touches
    // the physics: it is what makes the diver look alive instead of a stiff doll.
    // The bone and material names a model must use are listed in Docs/Blender.md.
    public class DiverModel : MonoBehaviour
    {
        [SerializeField] DiverPose pose;
        [Tooltip("Character models by name; a skin picks one with its `model` field, the first one is the default.")]
        [SerializeField] GameObject[] models;
        [Tooltip("The primitives of the placeholder body: hidden while an authored model is shown.")]
        [SerializeField] Renderer[] placeholder;

        const string FatShape = "Fat";

        // The bones the model must have, parents first, and who each hangs from.
        static readonly string[] BoneNames =
        {
            "Hips", "Spine", "Head", "Arm_L", "Arm_R", "ForeArm_L", "ForeArm_R", "Thigh_L", "Thigh_R", "Shin_L", "Shin_R", "Ankle_L", "Ankle_R"
        };

        static string ParentOf(string bone)
        {
            switch (bone)
            {
                case "Spine": case "Thigh_L": case "Thigh_R": return "Hips";
                case "Head": case "Arm_L": case "Arm_R": return "Spine";
                case "ForeArm_L": return "Arm_L";
                case "ForeArm_R": return "Arm_R";
                case "Shin_L": return "Thigh_L";
                case "Shin_R": return "Thigh_R";
                case "Ankle_L": return "Shin_L";
                case "Ankle_R": return "Shin_R";
                default: return null;
            }
        }

        class Bound
        {
            public GameObject root;
            public Transform[] bones;
            public Transform[] joints;       // the body's joint that drives each bone (null for the head)
            public int[] parent;             // index of the parent bone, -1 for the hips
            public Quaternion[] rest;
            public Quaternion[] world;       // this frame's result, kept for the children
            public Vector3 hipsRest;
            public Dictionary<string, int> index = new Dictionary<string, int>();
            // The loose-motion layer: target and current angles (degrees, about the joint's own axes) with their speeds.
            public Vector3[] target, angle, speed;
            public float looseness = 1f;
            public float height = 1f;
            public float headScale = 1f;
            public Dictionary<SkinRole, Material> materials = new Dictionary<SkinRole, Material>();
            public Renderer[] renderers;
            public List<(GameObject part, Gear gear)> gear = new List<(GameObject, Gear)>();
            public List<(GameObject part, Gear gear)> without = new List<(GameObject, Gear)>();
            public List<(SkinnedMeshRenderer mesh, int index)> fat = new List<(SkinnedMeshRenderer, int)>();
        }

        readonly Dictionary<GameObject, Bound> bound = new Dictionary<GameObject, Bound>();
        Bound active;
        Diver diver;
        float clock;
        float lastSpin;
        float spinAccel;

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
            Sync(0f);
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
            diver = GetComponent<Diver>();
            var b = new Bound { root = Instantiate(prefab, transform) };
            b.root.name = prefab.name;
            b.root.transform.localPosition = Vector3.zero;
            b.root.transform.localRotation = Quaternion.identity;
            b.root.transform.localScale = Vector3.one;
            // The importer adds an Animator for the skeleton; nothing may drive the bones except this script.
            foreach (var animator in b.root.GetComponentsInChildren<Animator>(true)) DestroyImmediate(animator);
            b.looseness = prefab.name == "Robot" ? 0.35f : 1f;

            var bones = new List<Transform>();
            var names = new List<string>();
            var children = b.root.GetComponentsInChildren<Transform>(true);
            foreach (var n in BoneNames)
            {
                Transform bone = null;
                foreach (var c in children) if (c.name == n) { bone = c; break; }
                if (bone == null)
                {
                    if (n == "Head") continue;   // the head bone is optional: the head then just rides on the spine
                    Debug.LogError($"DiverModel: model '{prefab.name}' has no bone '{n}'");
                    continue;
                }
                bones.Add(bone);
                names.Add(n);
            }

            // Parents first, so a child's position follows its parent's new rotation.
            var order = new List<int>();
            for (int i = 0; i < bones.Count; i++) order.Add(i);
            order.Sort((x, y) => Depth(bones[x]).CompareTo(Depth(bones[y])));
            int count = bones.Count;
            b.bones = new Transform[count];
            b.joints = new Transform[count];
            b.parent = new int[count];
            b.rest = new Quaternion[count];
            b.world = new Quaternion[count];
            b.target = new Vector3[count];
            b.angle = new Vector3[count];
            b.speed = new Vector3[count];
            var inverse = Quaternion.Inverse(transform.rotation);
            for (int i = 0; i < count; i++)
            {
                string n = names[order[i]];
                b.bones[i] = bones[order[i]];
                b.joints[i] = pose.Joint(n);
                b.rest[i] = inverse * b.bones[i].rotation;
                b.index[n] = i;
                if (n == "Hips") b.hipsRest = transform.InverseTransformPoint(b.bones[i].position);
            }
            for (int i = 0; i < count; i++)
            {
                string p = ParentOf(b.bones[i].name);
                b.parent[i] = p != null && b.index.TryGetValue(p, out int pi) ? pi : -1;
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
            // A heavy character is the same body with the "Fat" shape key blended in; the key is authored for the widest one (1.34).
            float fat = Mathf.Clamp01((s.width - 1f) / 0.34f) * 100f;
            foreach (var f in b.fat) f.mesh.SetBlendShapeWeight(f.index, fat);

            // A child stands lower and has a bigger head. The scale is about the hips; the hips are lowered in Retarget so the soles stay put.
            b.height = Mathf.Clamp(s.height, 0.6f, 1.2f);
            b.headScale = Mathf.Clamp(s.headScale, 0.8f, 1.4f);
            b.root.transform.localScale = Vector3.one * b.height;
            if (b.index.TryGetValue("Head", out int head)) b.bones[head].localScale = Vector3.one * b.headScale;
        }

        void LateUpdate() => Sync(Time.deltaTime);

        // Also called by the editor capture tool, which renders without playing; dt 0 jumps straight to the loose pose.
        public void Sync(float dt)
        {
            if (active == null) return;
            if (diver == null) diver = GetComponent<Diver>();
            clock += dt;
            Loose(active, dt);
            Retarget(active);
#if UNITY_EDITOR
            // Outside play mode no frame passes between two captures, so the skin would keep the last pose: toggling forces a re-skin.
            if (!Application.isPlaying)
                foreach (var r in active.renderers)
                    if (r is SkinnedMeshRenderer sm && sm.enabled) { sm.enabled = false; sm.enabled = true; }
#endif
        }

        public void Sync() => Sync(0f);

        // For the capture tool's log.
        public string DebugState()
        {
            if (active == null) return "no model";
            string Angle(string bone) => active.index.TryGetValue(bone, out int i) ? active.angle[i].ToString("0") : "-";
            string Rot(string bone) => active.index.TryGetValue(bone, out int i) ? (Quaternion.Inverse(transform.rotation) * active.bones[i].rotation).eulerAngles.ToString("0") + " rest " + (active.rest[i]).eulerAngles.ToString("0") : "-";
            string Same = "?";
            foreach (var r in active.renderers) if (r is SkinnedMeshRenderer sm && sm.name == "Body") Same = System.Array.IndexOf(sm.bones, active.bones[active.index["Shin_L"]]) + "/" + sm.bones.Length + " root=" + sm.rootBone.name;
            Debug.Log($"BONES shinL {Rot("Shin_L")} thighL {Rot("Thigh_L")} armL {Rot("Arm_L")} inRenderer {Same} jointShin {pose.Joint("Shin_L").localEulerAngles.ToString("0")}");
            return $"air={pose.Airborne} under={pose.Underwater} headFirst={pose.HeadFirst} tuck={pose.Tucked:0.00} crouch={pose.Crouched:0.00} " +
                   $"shinL={Angle("Shin_L")} thighL={Angle("Thigh_L")} armL={Angle("Arm_L")} head={Angle("Head")}";
        }

        // ------------------------------------------------------------------ the loose-motion layer

        void Aim(Bound b, string bone, float x, float y = 0f, float z = 0f)
        {
            if (b.index.TryGetValue(bone, out int i)) b.target[i] = new Vector3(x, y, z) * b.looseness;
        }

        // Works out where every joint would like to be given what the diver is doing, then lets springs chase it.
        void Loose(Bound b, float dt)
        {
            for (int i = 0; i < b.target.Length; i++) b.target[i] = Vector3.zero;
            float t = clock;
            bool airborne = pose.Airborne && !pose.Underwater;
            float tuck = pose.Tucked;
            float crouch = pose.Crouched;

            // How hard the body is being whirled: the limbs trail behind it, like the limbs of a ragdoll.
            float spin = diver != null ? diver.Spin : 0f;
            spinAccel = Mathf.Lerp(spinAccel, (spin - lastSpin) / Mathf.Max(dt, 0.008f), Mathf.Clamp01(dt * 10f));
            lastSpin = spin;
            float whirl = Mathf.Clamp(spin / 900f, -1f, 1f);        // -1..1 for about -900..900 degrees a second
            float jolt = Mathf.Clamp(spinAccel / 6000f, -1f, 1f);

            if (pose.Underwater)
            {
                // Floating down: a lazy flutter kick and the arms sweeping slowly.
                float kick = Mathf.Sin(t * 6.5f);
                Aim(b, "Thigh_L", kick * 20f); Aim(b, "Thigh_R", -kick * 20f);
                Aim(b, "Shin_L", 18f + kick * 16f); Aim(b, "Shin_R", 18f - kick * 16f);
                float sweep = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);
                Aim(b, "Arm_L", 0f, 0f, -34f * sweep); Aim(b, "Arm_R", 0f, 0f, 34f * sweep);
                Aim(b, "Head", -10f);
                Aim(b, "Spine", -4f + Mathf.Sin(t * 1.8f) * 3f);
            }
            else if (airborne)
            {
                bool slipped = diver != null && diver.Slipped;
                bool streamline = pose.HeadFirst && !slipped && tuck < 0.3f;
                float open = 1f - tuck;
                // Loose (not streamlined, not tucked): knees bent a little differently each side, toes pointed, arms floating out.
                float loose = open * (streamline ? 0.12f : 1f);
                float sway = Mathf.Sin(t * 3.1f);
                Aim(b, "Thigh_L", -22f * loose - 6f * sway * loose);
                Aim(b, "Thigh_R", -9f * loose + 6f * sway * loose);
                Aim(b, "Shin_L", 44f * loose + 8f * sway * loose);
                Aim(b, "Shin_R", 26f * loose - 8f * sway * loose);
                Aim(b, "Ankle_L", 22f); Aim(b, "Ankle_R", 22f);
                Aim(b, "Arm_L", 0f, 0f, -(26f + 10f * sway) * loose);
                Aim(b, "Arm_R", 0f, 0f, (26f - 10f * sway) * loose);
                Aim(b, "ForeArm_L", -(18f + 8f * sway) * loose); Aim(b, "ForeArm_R", -(14f - 8f * sway) * loose);
                Aim(b, "Spine", -9f * loose + 10f * tuck);
                // The head leads: chin down when tucked, looking up and out while open.
                Aim(b, "Head", 28f * tuck - 14f * loose + (streamline ? 14f : 0f));

                if (slipped)
                {
                    // Fell off the edge: arms windmilling and legs scissoring.
                    float f = t * 12f;
                    Aim(b, "Arm_L", -70f * Mathf.Sin(f), 0f, -35f); Aim(b, "Arm_R", -70f * Mathf.Sin(f + 3f), 0f, 35f);
                    Aim(b, "Thigh_L", 30f * Mathf.Sin(f + 1f)); Aim(b, "Thigh_R", 30f * Mathf.Sin(f + 4f));
                    Aim(b, "Shin_L", 45f); Aim(b, "Shin_R", 45f);
                    Aim(b, "Head", Mathf.Sin(f) * 14f);
                }
                else if (diver != null && diver.Stranded)
                {
                    float f = t * 7f;
                    Aim(b, "Arm_L", -35f * Mathf.Sin(f), 0f, -25f); Aim(b, "Arm_R", -35f * Mathf.Sin(f + 2f), 0f, 25f);
                    Aim(b, "Head", Mathf.Sin(f * 0.5f) * 10f);
                }

                // Whirling flings the limbs out and back; a sudden change of spin makes them swing past and settle.
                float fling = open * Mathf.Abs(whirl);
                AddTo(b, "Arm_L", 0f, 0f, -26f * fling); AddTo(b, "Arm_R", 0f, 0f, 26f * fling);
                AddTo(b, "Arm_L", 34f * jolt * open); AddTo(b, "Arm_R", 34f * jolt * open);
                AddTo(b, "Shin_L", 14f * fling + 18f * jolt * open); AddTo(b, "Shin_R", 14f * fling - 18f * jolt * open);
                AddTo(b, "Head", -16f * jolt);
            }
            else
            {
                // On the platform: breathing, the head looking about, and a crouch with the arms hanging for balance.
                float breathe = Mathf.Sin(t * 1.7f);
                Aim(b, "Spine", breathe * 1.4f);
                Aim(b, "Head", -16f * crouch + Mathf.Sin(t * 0.8f) * 2.5f, Mathf.Sin(t * 0.43f) * 9f * (1f - crouch), Mathf.Sin(t * 0.6f) * 2f);
                Aim(b, "Arm_L", Mathf.Sin(t * 1.7f + 1f) * 1.5f, 0f, -3f * (1f - crouch));
                Aim(b, "Arm_R", Mathf.Sin(t * 1.7f + 2f) * 1.5f, 0f, 3f * (1f - crouch));
                Aim(b, "ForeArm_L", 6f * (1f - crouch)); Aim(b, "ForeArm_R", 6f * (1f - crouch));
            }

            // Springs a little under-damped, so everything arrives with a small overshoot.
            const float stiffness = 260f, damping = 17f;
            int steps = dt <= 0f ? 0 : Mathf.Clamp(Mathf.CeilToInt(dt / 0.01f), 1, 8);
            if (steps == 0)
            {
                for (int i = 0; i < b.angle.Length; i++) { b.angle[i] = b.target[i]; b.speed[i] = Vector3.zero; }
                return;
            }
            float h = dt / steps;
            for (int s = 0; s < steps; s++)
                for (int i = 0; i < b.angle.Length; i++)
                {
                    var accel = (b.target[i] - b.angle[i]) * stiffness - b.speed[i] * damping;
                    b.speed[i] += accel * h;
                    b.angle[i] += b.speed[i] * h;
                }
        }

        void AddTo(Bound b, string bone, float x, float y = 0f, float z = 0f)
        {
            if (b.index.TryGetValue(bone, out int i)) b.target[i] += new Vector3(x, y, z) * b.looseness;
        }

        // ------------------------------------------------------------------ copying the joints onto the bones

        // Each bone takes the turn of its joint, measured from the rest pose in the diver's own space, plus its loose motion.
        void Retarget(Bound b)
        {
            var body = transform.rotation;
            for (int i = 0; i < b.bones.Length; i++)
            {
                int p = b.parent[i];
                var basis = p < 0 ? body : b.world[p] * Quaternion.Inverse(b.rest[p]);
                var turn = b.joints[i] != null ? b.joints[i].localRotation : Quaternion.identity;
                var extra = Quaternion.Euler(b.angle[i]);
                b.world[i] = basis * turn * extra * b.rest[i];
                b.bones[i].rotation = b.world[i];
                if (p < 0)
                {
                    // Scaling about the hips lifts the soles; this drops the hips by the missing leg length (0.875 m for a full-size body).
                    var drop = b.joints[i] != null ? b.joints[i].localPosition * b.height : Vector3.zero;
                    b.bones[i].position = transform.TransformPoint(b.hipsRest * b.height + new Vector3(0f, -0.875f * (1f - b.height), 0f) + drop);
                }
            }
        }

        void OnDestroy()
        {
            foreach (var b in bound.Values)
                foreach (var m in b.materials.Values) if (m != null) Destroy(m);
        }
    }
}
