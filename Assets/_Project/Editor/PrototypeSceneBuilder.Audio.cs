using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace JumpGame.EditorTools
{
    // Hooks the CC0 sound files in Assets/_Project/Audio up to the game's cues. See Audio/CREDITS.md for where they came from.
    public static partial class PrototypeSceneBuilder
    {
        const string AudioDir = "Assets/_Project/Audio";

        static AudioClip[] Clips(string folder, params string[] names)
            => names.Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{folder}/{n}.ogg")).Where(c => c != null).ToArray();

        // Every clip in a folder whose name starts with the prefix, shortest first.
        static AudioClip[] ByLength(string folder, string prefix)
            => AssetDatabase.FindAssets("t:AudioClip", new[] { $"{AudioDir}/{folder}" })
                .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null && c.name.StartsWith(prefix))
                .OrderBy(c => c.length).ToArray();

        static AudioClip[] Custom(Cue cue)
        {
            if (!AssetDatabase.IsValidFolder(AudioDir + "/Custom")) return new AudioClip[0];
            return AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir + "/Custom" })
                .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null && c.name.StartsWith(cue.ToString(), System.StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        static void BuildSounds(Diver diver, GameFlow flow)
        {
            var splashes = ByLength("Water", "splash_");
            var jingles = ByLength("Jingles", "jingles_HIT");
            int third = Mathf.Max(1, splashes.Length / 3);
            Debug.Log($"Sounds: {splashes.Length} splashes, {jingles.Length} jingles");

            // Bigger splashes are the longer recordings; medals go up with the length of the jingle.
            var cues = new (Cue cue, AudioClip[] clips, float volume, float pitchMin, float pitchMax)[]
            {
                (Cue.Click, Clips("Interface", "click_001", "click_002", "click_003"), 0.55f, 0.97f, 1.03f),
                (Cue.Hover, Clips("Interface", "tick_001", "tick_002"), 0.35f, 0.95f, 1.05f),
                (Cue.Back, Clips("Interface", "back_001"), 0.6f, 1f, 1f),
                (Cue.Confirm, Clips("Interface", "confirmation_001", "confirmation_002"), 0.6f, 1f, 1f),
                (Cue.Error, Clips("Interface", "error_001"), 0.5f, 1f, 1f),
                (Cue.Switch, Clips("Interface", "switch_001", "switch_002", "switch_003"), 0.6f, 0.95f, 1.05f),
                (Cue.Select, Clips("Interface", "select_001", "select_002", "select_003"), 0.55f, 0.95f, 1.08f),
                (Cue.Open, Clips("Interface", "open_001", "maximize_001"), 0.6f, 1f, 1f),
                (Cue.Close, Clips("Interface", "close_001", "minimize_001"), 0.6f, 1f, 1f),
                (Cue.Whoosh, Clips("Effects", "cloth1", "cloth2", "cloth3", "cloth4"), 0.6f, 0.75f, 0.9f),
                (Cue.Coins, Clips("Effects", "handleCoins", "handleCoins2"), 0.8f, 0.98f, 1.04f),
                (Cue.Knock, Clips("Effects", "dropLeather", "footstep00", "footstep01"), 0.7f, 0.8f, 0.95f),
                (Cue.Step, Clips("Effects", "footstep00", "footstep01", "footstep02", "footstep03"), 0.7f, 0.95f, 1.05f),
                (Cue.Creak, Clips("Effects", "creak1", "creak2", "creak3"), 0.6f, 0.9f, 1.1f),
                (Cue.Tuck, Clips("Effects", "cloth1", "cloth2", "cloth3", "cloth4"), 0.45f, 1f, 1.15f),
                (Cue.Turn, Clips("Interface", "pluck_001", "pluck_002"), 0.5f, 1f, 1f),
                (Cue.SplashSmall, Clips("Water", "peludo_splash1", "peludo_splash2").Concat(splashes.Take(third / 2)).ToArray(), 0.8f, 0.95f, 1.1f),
                (Cue.SplashMedium, splashes.Skip(third).Take(third).ToArray(), 0.9f, 0.95f, 1.05f),
                (Cue.SplashBig, splashes.Skip(third * 2).ToArray(), 1f, 0.92f, 1.04f),
                (Cue.Bubbles, Clips("Water", "bubble_01", "bubble_02", "bubble_03"), 0.5f, 0.95f, 1.1f),
                (Cue.Fault, Clips("Interface", "glitch_001", "glitch_002", "glitch_003", "glitch_004"), 0.6f, 1f, 1f),
                (Cue.Slime, Clips("Water", "slime_01", "slime_02", "slime_03"), 0.7f, 0.95f, 1.05f),
                (Cue.Fail, Clips("Interface", "drop_001", "error_003"), 0.5f, 0.9f, 1f),
                (Cue.Medal1, jingles.Skip(Mathf.Min(3, jingles.Length - 1)).Take(1).ToArray(), 0.7f, 1f, 1f),
                (Cue.Medal2, jingles.Skip(Mathf.Min(8, jingles.Length - 1)).Take(1).ToArray(), 0.7f, 1f, 1f),
                (Cue.Medal3, jingles.Skip(Mathf.Max(0, jingles.Length - 1)).Take(1).ToArray(), 0.75f, 1f, 1f)
            };

            // Anything the author drops into Audio/Custom wins: files named after a cue (SplashBig_1.mp3, Coins.wav) replace its clips.
            for (int i = 0; i < cues.Length; i++)
            {
                var own = Custom(cues[i].cue);
                if (own.Length > 0) cues[i].clips = own;
            }

            var go = new GameObject("Sounds");
            var sounds = go.AddComponent<Sounds>();
            var so = new SerializedObject(sounds);
            var arr = so.FindProperty("cues");
            arr.arraySize = cues.Length;
            for (int i = 0; i < cues.Length; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("cue").enumValueIndex = (int)cues[i].cue;
                var clips = e.FindPropertyRelative("clips");
                clips.arraySize = cues[i].clips.Length;
                for (int c = 0; c < cues[i].clips.Length; c++) clips.GetArrayElementAtIndex(c).objectReferenceValue = cues[i].clips[c];
                e.FindPropertyRelative("volume").floatValue = cues[i].volume;
                e.FindPropertyRelative("pitch").vector2Value = new Vector2(cues[i].pitchMin, cues[i].pitchMax);
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            var audio = flow.gameObject.AddComponent<DiveAudio>();
            Link(audio, "diver", diver);
            Link(audio, "flow", flow);
        }

        // The on/off button for sound, shown on the title and on the map.
        static RectTransform SoundButton(UiKit ui, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string color, bool flat)
        {
            var b = ui.Btn("Sound", parent, anchor, pos, size, "Звук: вкл", color, 22);
            if (flat) Flatten(b);
            var toggle = b.gameObject.AddComponent<SoundToggle>();
            Link(toggle, "button", b);
            Link(toggle, "label", b.GetComponentInChildren<Text>());
            return (RectTransform)b.transform;
        }
    }
}
