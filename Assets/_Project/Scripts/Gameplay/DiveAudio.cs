using System.Collections;
using UnityEngine;

namespace JumpGame
{
    // Turns what happens in a dive into sound: the creak at the edge, the push-off, turns, knocks, the splash
    // sized to the entry, the medal jingle and the coins on the result screen.
    public class DiveAudio : MonoBehaviour
    {
        [SerializeField] Diver diver;
        [SerializeField] GameFlow flow;
        [Tooltip("Matches the pause before the result panel shows.")]
        [SerializeField] float resultDelay = 0.9f;

        float nextCreak;

        void OnEnable()
        {
            diver.Crouched += OnCrouched;
            diver.TookOff += OnTookOff;
            diver.TurnCompleted += OnTurn;
            diver.Knocked += OnKnocked;
            diver.Entered += OnEntered;
            flow.Finished += OnFinished;
        }

        void OnDisable()
        {
            diver.Crouched -= OnCrouched;
            diver.TookOff -= OnTookOff;
            diver.TurnCompleted -= OnTurn;
            diver.Knocked -= OnKnocked;
            diver.Entered -= OnEntered;
            flow.Finished -= OnFinished;
        }

        void Update()
        {
            // The board creaks louder the closer the diver is to tipping over.
            if (diver.Current == Diver.State.Crouching && diver.LeanDanger > 0.5f && Time.time >= nextCreak)
            {
                Sounds.Play(Cue.Creak, 0.5f + diver.LeanDanger * 0.5f, 0.9f + diver.LeanDanger * 0.3f);
                nextCreak = Time.time + 0.65f;
            }
        }

        void OnCrouched() => Sounds.Play(Cue.Tuck, 0.5f);

        void OnTookOff() => Sounds.Play(Cue.Step, 0.8f);

        void OnTurn(int turns) => Sounds.Play(Cue.Turn, 0.8f, 1f + 0.08f * Mathf.Min(turns, 6));

        void OnKnocked(float strength) => Sounds.Play(Cue.Knock, Mathf.Clamp01(0.4f + strength * 0.3f));

        void OnEntered(DiveResult r)
        {
            if (r.entry == EntryKind.OnTower) return;

            float size = Splash.SizeFor(r.entry) * diver.Perk.splash;
            var cue = size < 0.55f ? Cue.SplashSmall : size < 1.3f ? Cue.SplashMedium : Cue.SplashBig;
            Sounds.Play(cue, Mathf.Clamp(0.55f + size * 0.3f, 0.5f, 1f), Mathf.Lerp(1.08f, 0.9f, Mathf.Clamp01(size / 2f)));

            if (r.Failed) Sounds.Play(Cue.Fail, 0.6f);
            if (r.Failed && diver.Perk.failScore > 0) StartCoroutine(After(0.15f, () => Sounds.Play(Cue.Slime, 0.7f)));
            if (diver.Perk.breaks) StartCoroutine(Fault());
        }

        // A short burst of crackles for the robot's short circuit.
        IEnumerator Fault()
        {
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSeconds(0.18f);
                Sounds.Play(Cue.Fault, 0.55f, Random.Range(0.8f, 1.2f));
            }
        }

        void OnFinished(DiveOutcome o)
        {
            if (o.stars > 0)
                StartCoroutine(After(resultDelay, () => Sounds.Play(o.stars == 1 ? Cue.Medal1 : o.stars == 2 ? Cue.Medal2 : Cue.Medal3, 0.8f)));
            if (o.coins > 0)
                StartCoroutine(After(resultDelay + 0.25f, () => Sounds.Play(Cue.Coins, 0.8f)));
        }

        static IEnumerator After(float seconds, System.Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action();
        }
    }
}
