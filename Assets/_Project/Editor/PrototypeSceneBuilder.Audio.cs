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
            // The sounds picked in SoundLab: one clip per cue, the pitch range gives the variety.
            var cues = new (Cue cue, AudioClip[] clips, float volume, float pitchMin, float pitchMax)[]
            {
                (Cue.Click, Clips("Interface", "click_001"), 0.55f, 0.97f, 1.03f),
                (Cue.Hover, Clips("Interface", "tick_001"), 0.35f, 0.95f, 1.05f),
                (Cue.Back, Clips("Interface", "drop_001"), 0.6f, 1f, 1f),
                (Cue.Confirm, Clips("Sfx100", "bell_01"), 0.6f, 1f, 1f),
                (Cue.Error, Clips("Sfx100", "noise_01"), 0.5f, 1f, 1f),
                (Cue.Switch, Clips("Sfx100", "sfx100v2_switch_01"), 0.6f, 0.95f, 1.05f),
                (Cue.Select, Clips("Interface", "tick_002"), 0.55f, 0.95f, 1.08f),
                (Cue.Open, Clips("Sfx100", "paper_01"), 0.6f, 1f, 1f),
                (Cue.Close, Clips("Sfx100", "sfx100v2_door_02"), 0.5f, 1f, 1f),
                (Cue.Whoosh, Clips("Effects", "cloth3"), 0.6f, 0.75f, 0.9f),
                (Cue.Coins, Clips("Effects", "handleCoins"), 0.8f, 0.98f, 1.04f),
                (Cue.Knock, Clips("Sfx100", "sfx100v2_footstep_wood_02"), 0.7f, 0.85f, 1f),
                (Cue.Step, Clips("Sfx100", "sfx100v2_footstep_wood_01"), 0.7f, 0.95f, 1.05f),
                (Cue.Creak, Clips("Effects", "creak1"), 0.6f, 0.9f, 1.1f),
                (Cue.Tuck, Clips("Effects", "cloth4"), 0.45f, 1f, 1.15f),
                (Cue.Turn, Clips("Sfx100", "bell_02"), 0.5f, 1f, 1f),
                (Cue.SplashSmall, Clips("Water", "splash_14"), 0.8f, 0.95f, 1.1f),
                (Cue.SplashMedium, Clips("Water", "splash_01"), 0.9f, 0.95f, 1.05f),
                (Cue.SplashBig, Clips("Water", "splash_02"), 1f, 0.92f, 1.04f),
                (Cue.Bubbles, Clips("Water", "bubble_02"), 0.5f, 0.95f, 1.1f),
                (Cue.Fault, Clips("Interface", "glitch_004"), 0.6f, 0.8f, 1.2f),
                (Cue.Slime, Clips("Water", "slime_01"), 0.7f, 0.95f, 1.05f),
                (Cue.Fail, Clips("Interface", "drop_001"), 0.5f, 0.9f, 1f),
                // Bronze and gold are the same jingle, the bronze one a little lower.
                (Cue.Medal1, Clips("Jingles", "jingles_HIT15"), 0.7f, 0.85f, 0.85f),
                (Cue.Medal2, Clips("Jingles", "jingles_HIT03"), 0.7f, 1f, 1f),
                (Cue.Medal3, Clips("Jingles", "jingles_HIT15"), 0.75f, 1f, 1f)
            };

            // Anything the author drops into Audio/Custom wins: files named after a cue (SplashBig_1.mp3, Coins.wav) replace its clips.
            for (int i = 0; i < cues.Length; i++)
            {
                var own = Custom(cues[i].cue);
                if (own.Length > 0) cues[i].clips = own;
                if (cues[i].clips.Length == 0) Debug.LogWarning($"Sounds: no clip for {cues[i].cue}");
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
