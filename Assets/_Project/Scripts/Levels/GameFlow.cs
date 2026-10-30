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
        public int coins;
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

        // ---- Which levels and locations are open

        // Editor tools that drive the game open everything.
        public bool OpenAll { get; set; }

        public int PriceOf(int location) => levels.PriceOf(location);
        public bool IsPaid(int location) => levels.PriceOf(location) > 0;
        public bool Owns(int location) => progress != null && (!IsPaid(location) || progress.ownedLocations[location]);

        // Free locations open one after another: the last dive of the one before needs a medal. Paid ones need buying.
        public bool LocationOpen(int location)
        {
            if (OpenAll) return true;
            if (progress == null) return false;
            if (IsPaid(location)) return progress.ownedLocations[location];
            if (location == 0) return true;
            var before = LevelsIn(location - 1);
            return progress.stars[before[before.Count - 1]] > 0;
        }

        // Inside a location every dive needs a medal on the one before.
        public bool IsOpen(int index)
        {
            if (OpenAll) return true;
            if (progress == null) return false;
            int loc = levels.levels[index].location;
            if (!LocationOpen(loc)) return false;
            return index == 0 || levels.levels[index - 1].location != loc || progress.stars[index - 1] > 0;
        }

        public bool AtLocationEnd
        {
            get
            {
                var list = LevelsIn(Level.location);
                return list[list.Count - 1] == current;
            }
        }

        public int NextLocation => Level.location + 1 < LocationCount ? Level.location + 1 : -1;

        // Medals, points and how many dives are done, for the summary of one location.
        public (int medals, int medalsMax, int score, int done, int count) StatsOf(int location)
        {
            var list = LevelsIn(location);
            int medals = 0, score = 0, done = 0;
            foreach (int i in list)
            {
                medals += progress.stars[i];
                score += progress.best[i];
                if (progress.stars[i] > 0) done++;
            }
            return (medals, list.Count * 3, score, done, list.Count);
        }

        // ---- Coins and the shop

        const int CoinsPerPoint = 25;
        const int TaskBonus = 15;
        static readonly int[] MedalBonus = { 20, 30, 50 };

        public int Coins => progress.coins;
        public int SkinIndex => progress.skin;
        public bool OwnsSkin(int i) => progress.ownedSkins[i];

        public event Action CoinsChanged;
        public event Action SkinChanged;
        public event Action SummaryRequested;

        public void RequestSummary() => SummaryRequested?.Invoke();

        public bool BuyLocation(int location)
        {
            int price = PriceOf(location);
            if (!IsPaid(location) || progress.ownedLocations[location] || progress.coins < price) return false;
            progress.coins -= price;
            progress.ownedLocations[location] = true;
            Commit();
            return true;
        }

        public bool BuySkin(int skin)
        {
            int price = Skins.All[skin].price;
            if (progress.ownedSkins[skin] || progress.coins < price) return false;
            progress.coins -= price;
            progress.ownedSkins[skin] = true;
            progress.skin = skin;
            Commit();
            ApplySkin();
            SkinChanged?.Invoke();
            return true;
        }

        public void SelectSkin(int skin)
        {
            if (!progress.ownedSkins[skin]) return;
            progress.skin = skin;
            Commit();
            ApplySkin();
            SkinChanged?.Invoke();
        }

        // Dresses the diver in the chosen character, or in any other one for the shop's preview.
        public void ApplySkin()
        {
            ShowSkin(progress.skin);
            diver.Perk = Skins.All[progress.skin].perk;
        }

        public void ShowSkin(int skin)
        {
            var dress = diver.GetComponent<DiverSkin>();
            if (dress != null) dress.Apply(Skins.All[skin]);
        }

        void Commit()
        {
            if (Persist) Progress.Save(progress);
            CoinsChanged?.Invoke();
        }

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
        // Walking between dives stays inside one location; the next location comes from the summary screen.
        public bool CanGoNext => current + 1 < Count && levels.levels[current + 1].location == Level.location && IsOpen(current + 1);
        public bool CanGoBack => current > 0 && levels.levels[current - 1].location == Level.location;

        public event Action LevelChanged;
        public event Action<DiveOutcome> Finished;

        bool subscribed;
        bool hadSpot;

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

            int skinCount = Skins.All.Length;
            if (Persist)
            {
                progress = Progress.Load(Count, skinCount, LocationCount);
            }
            else
            {
                progress = new ProgressData { coins = 5000 };
                progress.Fit(Count, skinCount, LocationCount);
                for (int i = 0; i < progress.ownedLocations.Length; i++) progress.ownedLocations[i] = true;
                OpenAll = true;
            }

            ApplySkin();
            int start = progress.last;
            if (!IsOpen(start)) start = LastOpenIn(levels.levels[start].location);
            Load(start);
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
            zone.Configure(center, level.zoneRadius, level.zoneDrift, edge.forward);

            // Film from the outer side of the platform so the rest of the tower stays behind the diver.
            // Measured inside the location, since the blocks themselves stand far apart along X.
            float side = site.Tower.CameraSide(level.platform);
            if (side == 0f) side = site.transform.InverseTransformPoint(edge.position).x > 0.1f ? -1f : 1f;
            cam.SetSide(side);
            cam.SetLean(site.Tower.CameraLean(level.platform));
            cam.SetHeading(edge.eulerAngles.y);
            cam.SetObstacles(site.Tower);

            // Moving between spots in play glides the camera over; the first placement and editor tools just snap.
            bool glide = Application.isPlaying && cam.enabled && hadSpot;
            if (glide) cam.BlendFromCurrent();
            else cam.Snap();
            hadSpot = true;

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

            // Coins: a little for every point, and a bonus the first time each medal is won on a dive.
            int before = progress.stars[current];
            int coins = r.score / CoinsPerPoint;
            for (int medal = before; medal < o.stars; medal++) coins += MedalBonus[medal];
            if (o.taskJustDone) coins += TaskBonus;
            coins = Mathf.RoundToInt(coins * Skins.All[progress.skin].perk.coins);
            o.coins = coins;
            progress.coins += coins;

            progress.best[current] = Mathf.Max(progress.best[current], r.score);
            progress.stars[current] = Mathf.Max(progress.stars[current], o.stars);
            progress.tasks[current] |= o.taskDone;
            o.unlockedNext = before == 0 && o.stars > 0;

            Commit();
            Finished?.Invoke(o);
        }
    }
}
