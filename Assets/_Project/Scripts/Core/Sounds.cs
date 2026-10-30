using System;
using System.Collections.Generic;
using UnityEngine;

namespace JumpGame
{
    public enum Cue
    {
        Click, Hover, Back, Confirm, Error, Switch, Select, Open, Close, Whoosh,
        Coins, Knock, Step, Creak, Tuck, Turn,
        SplashSmall, SplashMedium, SplashBig, Bubbles, Fault, Slime, Fail,
        Medal1, Medal2, Medal3
    }

    [Serializable]
    public class CueClips
    {
        public Cue cue;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Each play picks a pitch between these two, so repeats do not sound identical.")]
        public Vector2 pitch = new Vector2(0.96f, 1.04f);
    }

    // One place that plays every sound of the game. Calls are safe when no sound object exists (editor tools, tests).
    public class Sounds : MonoBehaviour
    {
        const string MuteKey = "jg_muted";
        const int Voices = 12;

        [SerializeField] CueClips[] cues;

        static Sounds instance;
        static bool? muted;

        readonly Dictionary<Cue, CueClips> table = new Dictionary<Cue, CueClips>();
        readonly Dictionary<Cue, int> last = new Dictionary<Cue, int>();
        AudioSource[] voices;
        int next;

        public static bool Muted
        {
            get
            {
                if (muted == null) muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
                return muted.Value;
            }
            set
            {
                muted = value;
                PlayerPrefs.SetInt(MuteKey, value ? 1 : 0);
                PlayerPrefs.Save();
                AudioListener.volume = value ? 0f : 1f;
            }
        }

        void Awake()
        {
            instance = this;
            foreach (var c in cues) table[c.cue] = c;

            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
            }

            AudioListener.volume = Muted ? 0f : 1f;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // The platform asks games to go silent when the tab is hidden or an ad is showing.
        void OnApplicationFocus(bool focus) => AudioListener.pause = !focus;

        void OnApplicationPause(bool paused) => AudioListener.pause = paused;

        public static void Play(Cue cue, float volume = 1f, float pitch = 1f)
        {
            if (instance != null) instance.PlayCue(cue, volume, pitch);
        }

        void PlayCue(Cue cue, float volume, float pitch)
        {
            if (!Application.isPlaying || !table.TryGetValue(cue, out var c) || c.clips == null || c.clips.Length == 0) return;

            // Never the same take twice in a row.
            int pick = UnityEngine.Random.Range(0, c.clips.Length);
            if (c.clips.Length > 1 && last.TryGetValue(cue, out int before) && pick == before) pick = (pick + 1) % c.clips.Length;
            last[cue] = pick;

            var src = voices[next];
            next = (next + 1) % voices.Length;
            src.clip = c.clips[pick];
            src.volume = c.volume * volume;
            src.pitch = UnityEngine.Random.Range(c.pitch.x, c.pitch.y) * pitch;
            src.Play();
        }
    }
}
