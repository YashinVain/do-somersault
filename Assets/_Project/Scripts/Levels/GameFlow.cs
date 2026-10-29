using System;
using UnityEngine;

namespace JumpGame
{
    public struct DiveOutcome
    {
        public DiveResult result;
        public int stars;
        public bool taskDone;
        public bool taskJustDone;
        public bool newBest;
        public bool unlockedNext;
    }

    // Puts the diver and the buoy ring where the current level wants them and keeps score across levels.
    public class GameFlow : MonoBehaviour
    {
        [SerializeField] LevelSet levels;
        [SerializeField] DivingTower tower;
        [SerializeField] Diver diver;
        [SerializeField] LandingZone zone;
        [SerializeField] DiveCamera cam;
        [Tooltip("How far back from the edge the diver stands, metres.")]
        [SerializeField] float standBack = 0.25f;
        [SerializeField] float hipHeight = 0.9f;

        ProgressData progress;
        int current;
        bool loaded;

        public int Index => current;
        public int Count => levels.levels.Length;
        public LevelInfo Level => levels.levels[current];
        public float Height => tower.Height(Level.platform);
        public float HeightOf(int index) => tower.Height(levels.levels[index].platform);
        public ProgressData Saved => progress;
        public bool CanGoNext => current + 1 < Count && current + 1 < progress.unlocked;
        public bool CanGoBack => current > 0;

        public event Action LevelChanged;
        public event Action<DiveOutcome> Finished;

        bool subscribed;

        void Start()
        {
            if (!loaded) Begin();
        }

        void OnDestroy()
        {
            if (subscribed) diver.Entered -= OnEntered;
        }

        // Off for editor tools that drive the game without touching the player's save.
        public bool Persist { get; set; } = true;

        public void Begin()
        {
            if (!subscribed)
            {
                diver.Entered += OnEntered;
                subscribed = true;
            }

            if (Persist)
            {
                progress = Progress.Load(Count);
            }
            else
            {
                progress = new ProgressData { unlocked = Count };
                progress.Fit(Count);
            }
            Load(Mathf.Min(progress.last, progress.unlocked - 1));
        }

        public void Load(int index)
        {
            loaded = true;
            current = Mathf.Clamp(index, 0, Count - 1);
            var level = Level;
            var edge = tower.Edge(level.platform);

            diver.Init();
            diver.SetStart(edge.position + edge.rotation * new Vector3(0f, hipHeight, -standBack), edge.rotation);

            Vector3 center = edge.position + edge.forward * level.zoneDistance;
            center.y = zone.WaterLevel;
            zone.Configure(center, level.zoneRadius, level.zoneDrift);

            // Film from the outer side of the platform so the rest of the tower stays behind the diver.
            cam.SetSide(edge.position.x > 0.1f ? -1f : 1f);
            cam.Snap();

            progress.last = current;
            LevelChanged?.Invoke();
        }

        public void Next()
        {
            if (CanGoNext) Load(current + 1);
        }

        public void Back()
        {
            if (CanGoBack) Load(current - 1);
        }

        public void Retry() => diver.ResetToStart();

        void OnEntered(DiveResult r)
        {
            var level = Level;
            var o = new DiveOutcome
            {
                result = r,
                stars = level.StarsFor(r.score),
                taskDone = level.TaskDone(r)
            };

            o.taskJustDone = o.taskDone && !progress.tasks[current];
            o.newBest = r.score > progress.best[current];
            progress.best[current] = Mathf.Max(progress.best[current], r.score);
            progress.stars[current] = Mathf.Max(progress.stars[current], o.stars);
            progress.tasks[current] |= o.taskDone;

            if (o.stars > 0 && progress.unlocked < current + 2 && current + 1 < Count)
            {
                progress.unlocked = current + 2;
                o.unlockedNext = true;
            }

            if (Persist) Progress.Save(progress);
            Finished?.Invoke(o);
        }
    }
}
