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
        [Tooltip("One per entry in the level set's location list, in the same order.")]
        [SerializeField] LocationSite[] sites;
        [SerializeField] Light sun;
        [SerializeField] Diver diver;
        [SerializeField] LandingZone zone;
        [SerializeField] DiveCamera cam;
        [Tooltip("How far back from the edge the diver stands, metres.")]
        [SerializeField] float standBack = 0.25f;
        [SerializeField] float hipHeight = 0.9f;
        [Tooltip("Locations sit side by side as separate blocks and stay switched on; otherwise only the current one is shown.")]
        [SerializeField] bool keepAllSitesActive;
        [SerializeField] float lightBlendTime = 0.8f;

        LocationSite blendFrom;
        float blendT = 1f;

        ProgressData progress;
        int current;
        int activeSite = -1;
        bool loaded;
        Material sky;

        public int Index => current;
        public int Count => levels.levels.Length;
        public LevelInfo Level => levels.levels[current];
        public LevelSet Set => levels;
        public int LocationCount => levels.locations.Length;
        public string LocationName(int location) => levels.locations[location];
        public float Height => HeightOf(current);
        public float HeightOf(int index) => sites[levels.levels[index].location].Tower.Height(levels.levels[index].platform);
        public ProgressData Saved => progress;
        public LocationSite Site(int location) => sites[location];
        public bool IsOpen(int index) => progress != null && index < progress.unlocked;

        public System.Collections.Generic.List<int> LevelsIn(int location)
        {
            var list = new System.Collections.Generic.List<int>();
            for (int i = 0; i < Count; i++)
                if (levels.levels[i].location == location) list.Add(i);
            return list;
        }

        // The furthest level the player can already play in a location, or its first one if none.
        public int LastOpenIn(int location)
        {
            var list = LevelsIn(location);
            int pick = list[0];
            foreach (int i in list) if (IsOpen(i)) pick = i;
            return pick;
        }

        public Transform EdgeOf(int index)
        {
            var level = levels.levels[index];
            return sites[level.location].Tower.Edge(level.platform);
        }

        // The spot a level is jumped from, in the world.
        public Vector3 SpotOf(int index)
        {
            var level = levels.levels[index];
            return sites[level.location].Tower.Edge(level.platform).position;
        }

        public Vector3 ZoneCenterOf(int index)
        {
            var level = levels.levels[index];
            var edge = sites[level.location].Tower.Edge(level.platform);
            Vector3 c = edge.position + edge.forward * level.zoneDistance;
            c.y = zone.WaterLevel;
            return c;
        }
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
            var site = ShowSite(level.location);
            var edge = site.Tower.Edge(level.platform);

            diver.Init();
            diver.SetTower(site.Tower);
            diver.SetStart(edge.position + edge.rotation * new Vector3(0f, hipHeight, -standBack), edge.rotation);

            Vector3 center = edge.position + edge.forward * level.zoneDistance;
            center.y = zone.WaterLevel;
            zone.Configure(center, level.zoneRadius, level.zoneDrift);

            // Film from the outer side of the platform so the rest of the tower stays behind the diver.
            // Measured inside the location, since the blocks themselves stand far apart along X.
            float side = site.Tower.CameraSide(level.platform);
            if (side == 0f) side = site.transform.InverseTransformPoint(edge.position).x > 0.1f ? -1f : 1f;
            cam.SetSide(side);
            cam.Snap();

            progress.last = current;
            LevelChanged?.Invoke();
        }

        // Only the current location's scenery is switched on; its light and fog are applied with it.
        LocationSite ShowSite(int location)
        {
            if (sky == null)
            {
                // Work on a copy so play mode never edits the sky material asset.
                sky = new Material(RenderSettings.skybox);
                RenderSettings.skybox = sky;
            }

            if (location != activeSite)
            {
                // The first location and editor tools get the light at once; switching in play blends it over.
                if (activeSite < 0 || !Application.isPlaying)
                {
                    sites[location].Apply(sun, sky);
                    blendT = 1f;
                }
                else
                {
                    blendFrom = sites[activeSite];
                    blendT = 0f;
                }
                activeSite = location;
                RefreshSites();
            }
            return sites[location];
        }

        bool showAllSites;

        // On the map every block is shown; while diving only the current one, so no far-off block
        // shows through the fog on the horizon.
        public void ShowAllSites(bool all)
        {
            showAllSites = all;
            RefreshSites();
        }

        void RefreshSites()
        {
            for (int i = 0; i < sites.Length; i++)
                sites[i].gameObject.SetActive(i == activeSite || (keepAllSitesActive && showAllSites));
        }

        void Update()
        {
            if (blendT >= 1f || activeSite < 0) return;
            blendT = Mathf.Min(1f, blendT + Time.unscaledDeltaTime / lightBlendTime);
            float t = Mathf.SmoothStep(0f, 1f, blendT);
            LocationSite.Blend(blendFrom, sites[activeSite], t, sun, sky);
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
