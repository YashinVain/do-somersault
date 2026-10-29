using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JumpGame.EditorTools
{
    // Builds the grey-box pool scene used to tune the dive. Safe to rerun: it overwrites the scene.
    public static class PrototypeSceneBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Pool_Prototype.unity";
        const string MaterialDir = "Assets/_Project/Materials/Prototype";

        const float PlatformTop = 5f;

        [MenuItem("Jump Game/Build Prototype Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(MaterialDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var water = Mat("Water", "#2FB3C6", 0.6f);
            var deck = Mat("Deck", "#E8D6B0");
            var tower = Mat("Tower", "#EE6A55");
            var skin = Mat("Skin", "#F2C2A0");
            var trunks = Mat("Trunks", "#283B8F");
            var ringOuter = Mat("RingOuter", "#FFFFFF");
            var ringMid = Mat("RingMid", "#FFC743");
            var ringCenter = Mat("RingCenter", "#FF5462");
            var foam = Mat("Foam", "#E9FBFF");

            BuildLight();
            BuildPool(water, deck);
            BuildTower(tower);

            var target = BuildTarget(ringOuter, ringMid, ringCenter);
            var splash = BuildSplash(foam);
            var diver = BuildDiver(skin, trunks);

            Link(diver, "target", target);
            Link(diver, "splash", splash);

            BuildCamera(diver.GetComponent<Diver>());

            var hud = new GameObject("Hud").AddComponent<PrototypeHud>();
            Link(hud, "diver", diver.GetComponent<Diver>());

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Prototype scene saved to " + ScenePath);
        }

        static void BuildLight()
        {
            var go = new GameObject("Sun");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = Hex("#FFF4E0");
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Hex("#9CB8C8");
        }

        static void BuildPool(Material water, Material deck)
        {
            var root = new GameObject("Pool").transform;

            Box("Water", root, new Vector3(0f, -0.05f, 6f), new Vector3(10f, 0.1f, 18f), water);
            Box("Deck_Left", root, new Vector3(-7f, 0.15f, 6f), new Vector3(4f, 0.5f, 22f), deck);
            Box("Deck_Right", root, new Vector3(7f, 0.15f, 6f), new Vector3(4f, 0.5f, 22f), deck);
            Box("Deck_Back", root, new Vector3(0f, 0.15f, -5f), new Vector3(10f, 0.5f, 4f), deck);
            Box("Deck_Far", root, new Vector3(0f, 0.15f, 17f), new Vector3(10f, 0.5f, 4f), deck);
        }

        static void BuildTower(Material mat)
        {
            var root = new GameObject("Tower").transform;
            Box("Column", root, new Vector3(0f, PlatformTop * 0.5f, -4.2f), new Vector3(1.4f, PlatformTop, 1.4f), mat);
            Box("Platform", root, new Vector3(0f, PlatformTop - 0.15f, -2.1f), new Vector3(1.8f, 0.3f, 4.2f), mat);
        }

        static WaterTarget BuildTarget(Material outer, Material mid, Material center)
        {
            var go = new GameObject("Target");
            go.transform.position = new Vector3(0f, 0f, 3.2f);

            Disc("Ring_Outer", go.transform, 3f, 0.004f, outer);
            Disc("Ring_Mid", go.transform, 1.8f, 0.008f, mid);
            Disc("Ring_Center", go.transform, 0.8f, 0.012f, center);

            return go.AddComponent<WaterTarget>();
        }

        static Splash BuildSplash(Material foam)
        {
            var go = new GameObject("Splash");
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = 5f;
            main.startSize = 0.2f;
            main.gravityModifier = 1.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = Hex("#E9FBFF");
            main.maxParticles = 400;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.25f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMat();

            var ripple = Disc("Ripple", null, 1f, 0f, foam);

            var splash = go.AddComponent<Splash>();
            Link(splash, "ripple", ripple);
            return splash;
        }

        static GameObject BuildDiver(Material skin, Material trunks)
        {
            var root = new GameObject("Diver");
            root.transform.position = new Vector3(0f, PlatformTop + 0.9f, -0.35f);

            var rig = new GameObject("Rig").transform;
            rig.SetParent(root.transform, false);

            Box("Hips", rig, new Vector3(0f, 0.05f, 0f), new Vector3(0.42f, 0.2f, 0.24f), trunks);
            Box("Torso", rig, new Vector3(0f, 0.4f, 0f), new Vector3(0.44f, 0.55f, 0.24f), skin);
            Sphere("Head", rig, new Vector3(0f, 0.86f, 0f), 0.3f, skin);

            var armL = Limb("Arm_L", rig, new Vector3(-0.29f, 0.64f, 0f), 0.62f, 0.12f, skin);
            var armR = Limb("Arm_R", rig, new Vector3(0.29f, 0.64f, 0f), 0.62f, 0.12f, skin);

            var thighL = Limb("Thigh_L", rig, new Vector3(-0.11f, 0f, 0f), 0.45f, 0.17f, skin);
            var thighR = Limb("Thigh_R", rig, new Vector3(0.11f, 0f, 0f), 0.45f, 0.17f, skin);
            var kneeL = Limb("Shin_L", thighL, new Vector3(0f, -0.45f, 0f), 0.45f, 0.14f, skin);
            var kneeR = Limb("Shin_R", thighR, new Vector3(0f, -0.45f, 0f), 0.45f, 0.14f, skin);

            var pose = root.AddComponent<DiverPose>();
            Link(pose, "rig", rig);
            Link(pose, "armL", armL);
            Link(pose, "armR", armR);
            Link(pose, "thighL", thighL);
            Link(pose, "thighR", thighR);
            Link(pose, "kneeL", kneeL);
            Link(pose, "kneeR", kneeR);

            var diver = root.AddComponent<Diver>();
            Link(diver, "pose", pose);
            return root;
        }

        static GameObject BuildCamera(Diver diver)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("#A6DCF2");
            cam.fieldOfView = 50f;
            go.AddComponent<AudioListener>();

            var follow = go.AddComponent<DiveCamera>();
            Link(follow, "diver", diver);
            return go;
        }

        // A pivot at the joint with the visible segment hanging below it.
        static Transform Limb(string name, Transform parent, Vector3 joint, float length, float thickness, Material mat)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = joint;
            Box(name + "_Mesh", pivot, new Vector3(0f, -length * 0.5f, 0f), new Vector3(thickness, length, thickness), mat);
            return pivot;
        }

        static Transform Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
            => Primitive(PrimitiveType.Cube, name, parent, pos, size, mat);

        static Transform Sphere(string name, Transform parent, Vector3 pos, float diameter, Material mat)
            => Primitive(PrimitiveType.Sphere, name, parent, pos, Vector3.one * diameter, mat);

        static Transform Disc(string name, Transform parent, float radius, float y, Material mat)
            => Primitive(PrimitiveType.Cylinder, name, parent, new Vector3(0f, y, 0f), new Vector3(radius * 2f, 0.002f, radius * 2f), mat);

        static Transform Primitive(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        static Material Mat(string name, string hex, float smoothness = 0.08f)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = Hex(hex);
            mat.SetFloat("_Glossiness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material ParticleMat()
        {
            string path = $"{MaterialDir}/SplashDrop.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        static void Link(Object owner, string field, Object value)
        {
            var so = new SerializedObject(owner);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"{owner.GetType().Name} has no field '{field}'");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Link(GameObject owner, string field, Object value)
            => Link(owner.GetComponent<Diver>(), field, value);

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
