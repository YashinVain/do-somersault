using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JumpGame
{
    // Title and level map. The camera hovers over the real location; each level is a buoy marker
    // over its landing spot, and two 3D arrows move between locations.
    public class MapScreen : MonoBehaviour
    {
        [SerializeField] GameFlow flow;
        [SerializeField] Diver diver;
        [SerializeField] DiveCamera diveCam;
        [SerializeField] UiPanel hud;
        [SerializeField] ScreenCurtain curtain;
        [SerializeField] Button menuButton;

        [Header("Title")]
        [SerializeField] UiPanel titlePanel;
        [SerializeField] Button playButton;
        [SerializeField] Text titleMedals;

        [Header("Map UI")]
        [SerializeField] UiPanel mapPanel;
        [SerializeField] Text locationName;
        [SerializeField] Text locationInfo;
        [SerializeField] GameObject card;
        [SerializeField] Text cardTitle;
        [SerializeField] Text cardTask;
        [SerializeField] Text cardBest;
        [SerializeField] Image[] cardMedals;
        [SerializeField] Text[] cardGoals;
        [SerializeField] Button jumpButton;
        [SerializeField] Text lockedText;
        [SerializeField] Button buyButton;
        [SerializeField] Text buyLabel;
        [SerializeField] Button backButton;
        [SerializeField] Text mapCoins;

        [Header("Location summary")]
        [SerializeField] UiPanel summaryPanel;
        [SerializeField] Text summaryTitle, summaryTotals, summaryCoins, summaryNextLabel;
        [SerializeField] Text[] summaryRowLabels, summaryRowScores;
        [SerializeField] Image[] summaryRowDots;
        [SerializeField] Button summaryNext, summaryStay;

        [Header("3D")]
        [SerializeField] MapMarker markerTemplate;
        [SerializeField] Transform arrows;
        [SerializeField] Transform arrowLeft, arrowRight;
        [SerializeField] Material ringOpen, ringCurrent, ringLocked, medalOff;
        [SerializeField] Material[] medalOn;
        [SerializeField] float markerHeight = 2.4f;

        [Header("Camera")]
        [Tooltip("Plain colour behind the location blocks while the map is up; fog is off so whole blocks show.")]
        [SerializeField] Color backdrop = new Color(0.13f, 0.22f, 0.31f);
        [SerializeField] float swayAngle = 14f;
        [SerializeField] float swaySpeed = 0.18f;
        [SerializeField] float flySharpness = 2.5f;

        enum Mode { Title, Map }

        readonly List<MapMarker> markers = new List<MapMarker>();
        Camera cam;
        bool open;
        Mode mode;
        int viewing;
        int resume = -1;
        float time;

        void Start()
        {
            cam = diveCam.GetComponent<Camera>();
            playButton.onClick.AddListener(ShowMap);
            jumpButton.onClick.AddListener(Play);
            menuButton.onClick.AddListener(ShowMap);
            buyButton.onClick.AddListener(BuyHere);
            backButton.onClick.AddListener(BackToTitle);
            summaryNext.onClick.AddListener(GoToNextLocation);
            summaryStay.onClick.AddListener(() => summaryPanel.Show(false));
            flow.SummaryRequested += ShowSummary;
            flow.CoinsChanged += RefreshCoins;
            summaryPanel.Show(false, true);
            markerTemplate.gameObject.SetActive(false);
            if (flow.Saved == null) flow.Begin();
            ShowTitle();
            if (curtain != null) curtain.Reveal();
        }

        // The title is the real location with the diver on its highest spot, seen from below.
        public void ShowTitle()
        {
            Enter(false);
            mode = Mode.Title;
            viewing = flow.Level.location;

            // Remember where the player was; the hero shot needs the diver up on the top spot.
            if (resume < 0) resume = flow.Index;
            var list = flow.LevelsIn(viewing);
            flow.Load(list[list.Count - 1]);

            titlePanel.Show(true);
            mapPanel.Show(false, true);
            arrows.gameObject.SetActive(false);
            ShowMarkers(false);
            SnapCamera();

            RefreshCoins();
        }

        public void ShowMap()
        {
            if (curtain != null) curtain.Dip(EnterMap);
            else EnterMap();
        }

        void EnterMap()
        {
            if (mode == Mode.Title && resume >= 0) flow.Load(resume);
            resume = -1;

            Enter(true);
            mode = Mode.Map;
            titlePanel.Show(false, true);
            mapPanel.Show(true);
            arrows.gameObject.SetActive(true);
            View(flow.Level.location, true);
            SnapCamera();
        }

        // On the map every location block is shown over a plain colour; the title and the game use the sky.
        void Enter(bool map)
        {
            if (cam == null) cam = diveCam.GetComponent<Camera>();
            if (!open) flow.Retry();
            open = true;
            diveCam.enabled = false;
            diver.AcceptInput = false;
            hud.Show(false, true);
            RenderSettings.fog = !map;
            cam.clearFlags = map ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            cam.backgroundColor = backdrop;
            flow.ShowAllSites(map);
        }

        void Play()
        {
            if (!flow.IsOpen(flow.Index)) return;
            open = false;
            mapPanel.Show(false);
            titlePanel.Show(false, true);
            arrows.gameObject.SetActive(false);
            ShowMarkers(false);
            hud.Show(true);
            RenderSettings.fog = true;
            cam.clearFlags = CameraClearFlags.Skybox;
            flow.ShowAllSites(false);
            diveCam.enabled = true;
            diveCam.BlendFromCurrent();
            diver.AcceptInput = true;
        }

        void BackToTitle()
        {
            if (curtain != null) curtain.Dip(ShowTitle);
            else ShowTitle();
        }

        void BuyHere()
        {
            if (flow.BuyLocation(viewing))
            {
                flow.Load(flow.LastOpenIn(viewing));
                BuildMarkers();
                Refresh();
                mapPanel.Replay();
            }
        }

        void RefreshCoins()
        {
            if (mapCoins != null) mapCoins.text = flow.Coins.ToString();
            if (titleMedals != null && mode == Mode.Title) titleMedals.text = TitleStats();
        }

        string TitleStats()
        {
            int medals = 0;
            foreach (int s in flow.Saved.stars) medals += s;
            return $"Медали {medals} из {flow.Count * 3}     Монеты {flow.Coins}";
        }

        // ---- End of a location: totals, then a flight to the next one.

        void ShowSummary()
        {
            int location = flow.Level.location;
            var (medals, medalsMax, score, done, count) = flow.StatsOf(location);
            var list = flow.LevelsIn(location);
            var saved = flow.Saved;

            summaryTitle.text = $"{flow.LocationName(location)}: итоги";
            for (int i = 0; i < summaryRowLabels.Length; i++)
            {
                bool used = i < list.Count;
                summaryRowLabels[i].gameObject.SetActive(used);
                summaryRowScores[i].gameObject.SetActive(used);
                for (int d = 0; d < 3; d++) summaryRowDots[i * 3 + d].gameObject.SetActive(used);
                if (!used) continue;

                int level = list[i];
                string h = flow.HeightOf(level).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
                summaryRowLabels[i].text = $"{i + 1}   ·   {h} м";
                summaryRowScores[i].text = saved.best[level] > 0 ? saved.best[level].ToString() : "—";
                for (int d = 0; d < 3; d++)
                {
                    var c = DiveHud.MedalColors[d];
                    c.a = d < saved.stars[level] ? 1f : 0.22f;
                    summaryRowDots[i * 3 + d].color = c;
                }
            }

            summaryTotals.text = $"Медали {medals} из {medalsMax}      Очки {score}";
            summaryCoins.text = $"Монет сейчас: {flow.Coins}";

            int next = flow.NextLocation;
            if (next < 0) summaryNextLabel.text = "К карте";
            else summaryNextLabel.text = flow.IsPaid(next) && !flow.Owns(next) ? "Дальше" : "Следующая локация";
            summaryPanel.Show(true);
        }

        // Dip the curtain over the swap to the map, then let the camera fly from this location to the next.
        void GoToNextLocation()
        {
            int from = flow.Level.location, next = flow.NextLocation;
            summaryPanel.Show(false);
            void Swap()
            {
                resume = -1;
                Enter(true);
                mode = Mode.Map;
                titlePanel.Show(false, true);
                mapPanel.Show(true);
                arrows.gameObject.SetActive(true);
                viewing = from;
                SnapCamera();
                View(next >= 0 ? next : from, false);
            }
            if (curtain != null) curtain.Dip(Swap);
            else Swap();
        }

        void View(int location, bool keepLevel)
        {
            int previous = viewing;
            viewing = Mathf.Clamp(location, 0, flow.LocationCount - 1);
            if (!keepLevel || flow.Level.location != viewing)
                flow.Load(flow.LastOpenIn(viewing));
            BuildMarkers();
            Refresh();
            if (viewing != previous) mapPanel.Replay();
        }

        void BuildMarkers()
        {
            var levels = flow.LevelsIn(viewing);
            while (markers.Count < levels.Count)
                markers.Add(Instantiate(markerTemplate, markerTemplate.transform.parent));

            // Each buoy hovers right over the place its dive starts from, so the numbers climb with the heights.
            // Spots that are close on screen push their buoys apart sideways; the pole still ends at the spot.
            var site = flow.Site(viewing);
            site.MapPose(0f, out var camPos, out var look);
            var view = Quaternion.LookRotation(look - camPos);
            Vector3 right = view * Vector3.right, up = view * Vector3.up;
            float size = MarkerSize();
            float gap = size * 1.5f;

            var spots = new Vector3[levels.Count];
            var at = new Vector3[levels.Count];
            for (int i = 0; i < levels.Count; i++)
            {
                spots[i] = flow.SpotOf(levels[i]);
                at[i] = spots[i] + Vector3.up * size * 1.2f;
            }

            // Separate overlapping buoys in the screen plane only, leaving their depth alone.
            for (int pass = 0; pass < 60; pass++)
            {
                bool moved = false;
                for (int a = 0; a < at.Length; a++)
                    for (int b = a + 1; b < at.Length; b++)
                    {
                        Vector3 d = at[b] - at[a];
                        Vector2 flat = new Vector2(Vector3.Dot(d, right), Vector3.Dot(d, up));
                        float len = flat.magnitude;
                        if (len >= gap) continue;
                        // Taller one goes up, lower one goes down; flat ties split sideways.
                        Vector2 dir = len > 0.001f ? flat / len : Vector2.right;
                        float push = (gap - len) * 0.5f;
                        Vector3 world = (right * dir.x + up * dir.y) * push;
                        at[a] -= world;
                        at[b] += world;
                        moved = true;
                    }
                if (!moved) break;
            }

            for (int i = 0; i < markers.Count; i++)
            {
                var m = markers[i];
                bool used = i < levels.Count;
                m.gameObject.SetActive(used);
                if (!used) continue;

                m.transform.position = at[i];
                m.PointTo(spots[i], size * 0.05f);
            }
        }

        // Bigger for maps seen from further away, so a buoy is about the same size on screen everywhere.
        float MarkerSize() => Mathf.Clamp(flow.Site(viewing).MapDistance * 0.06f, 1f, 9f);

        void ShowMarkers(bool on)
        {
            foreach (var m in markers) m.gameObject.SetActive(false);
            if (on) BuildMarkers();
        }

        void Refresh()
        {
            var saved = flow.Saved;
            var levels = flow.LevelsIn(viewing);
            for (int i = 0; i < levels.Count; i++)
            {
                int level = levels[i];
                markers[i].Set(level, i + 1, flow.IsOpen(level), level == flow.Index, saved.stars[level],
                               ringOpen, ringCurrent, ringLocked, medalOn, medalOff);
            }

            locationName.text = flow.LocationName(viewing);
            int medals = 0;
            foreach (int l in levels) medals += saved.stars[l];
            locationInfo.text = $"Медали: {medals} из {levels.Count * 3}";

            bool locationOpen = flow.LocationOpen(viewing);
            bool buyable = flow.IsPaid(viewing) && !flow.Owns(viewing);
            lockedText.gameObject.SetActive(!locationOpen);
            buyButton.gameObject.SetActive(buyable);
            if (!locationOpen)
            {
                if (buyable)
                {
                    int price = flow.PriceOf(viewing);
                    lockedText.text = "Платная локация";
                    buyLabel.text = flow.Coins >= price ? $"Купить за {price}" : $"Нужно {price} монет";
                    buyButton.interactable = flow.Coins >= price;
                }
                else
                {
                    lockedText.text = $"Откроется после локации «{flow.LocationName(viewing - 1)}»";
                }
            }
            RefreshCoins();

            card.SetActive(locationOpen);
            if (locationOpen) RefreshCard();

            arrowLeft.gameObject.SetActive(viewing > 0);
            arrowRight.gameObject.SetActive(viewing < flow.LocationCount - 1);
        }

        void RefreshCard()
        {
            var level = flow.Level;
            var (number, count) = flow.Set.PlaceInLocation(flow.Index);
            bool levelOpen = flow.IsOpen(flow.Index);
            cardTitle.text = $"Прыжок {number} из {count}  ·  {flow.Height:0.#} м".Replace('.', ',');
            cardTask.text = levelOpen ? level.TaskText : "Сначала возьми бронзу на прошлом прыжке";

            int best = flow.Saved.best[flow.Index];
            cardBest.text = best > 0 ? "Лучший: " + best : "";
            int[] goals = { level.bronze, level.silver, level.gold };
            int stars = flow.Saved.stars[flow.Index];
            for (int i = 0; i < 3; i++)
            {
                cardGoals[i].text = goals[i].ToString();
                var c = DiveHud.MedalColors[i];
                c.a = i < stars ? 1f : 0.3f;
                cardMedals[i].color = c;
            }
            jumpButton.interactable = levelOpen;
        }

        void Update()
        {
            if (!open) return;
            time += Time.unscaledDeltaTime;

            MoveCamera(Time.unscaledDeltaTime);
            FaceCamera();
            if (mode == Mode.Map && mapPanel.Visible) HandleTap();
        }

        void SnapCamera() => MoveCamera(-1f);

        // Low in front of the highest platform looking up at the diver, who stands in the right part of the frame
        // so the left stays free for the menu. It drifts slowly round him.
        void TitlePose(out Vector3 pos, out Quaternion rot)
        {
            var list = flow.LevelsIn(viewing);
            int top = list[list.Count - 1];
            var edge = flow.EdgeOf(top);
            Vector3 subject = edge.position + Vector3.up * 1f;

            Vector3 offset = edge.forward * 7.5f + edge.right * 3.5f;
            offset = Quaternion.AngleAxis(Mathf.Sin(time * 0.12f) * 9f, Vector3.up) * offset;
            pos = new Vector3(subject.x + offset.x, Mathf.Max(1.8f, edge.position.y - 5f), subject.z + offset.z);

            Vector3 toward = (subject - pos).normalized;
            Vector3 camRight = Vector3.Cross(Vector3.up, toward).normalized;
            rot = Quaternion.LookRotation(subject - camRight * 2.6f - pos);
        }

        // Hover over the location with a slow sideways sway; dt < 0 jumps straight there.
        void MoveCamera(float dt)
        {
            Vector3 pos;
            Quaternion rot;
            if (mode == Mode.Title)
            {
                TitlePose(out pos, out rot);
            }
            else
            {
                flow.Site(viewing).MapPose(Mathf.Sin(time * swaySpeed) * swayAngle, out pos, out var look);
                rot = Quaternion.LookRotation(look - pos);
            }
            if (dt < 0f)
            {
                cam.transform.SetPositionAndRotation(pos, rot);
                return;
            }
            float k = 1f - Mathf.Exp(-flySharpness * dt);
            cam.transform.position = Vector3.Lerp(cam.transform.position, pos, k);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, rot, k);
        }

        void FaceCamera()
        {
            foreach (var m in markers)
            {
                if (!m.gameObject.activeSelf) continue;
                m.Face.rotation = Quaternion.LookRotation(m.Face.position - cam.transform.position);
                bool current = m.Level == flow.Index;
                float size = MarkerSize();
                float bob = current ? Mathf.Sin(time * 3f) * 0.12f * size : 0f;
                m.Face.localPosition = new Vector3(0f, bob, 0f);
                m.SetSize(size * (current ? 1.18f : 1f));
            }

            // Arrows sit at the screen edges, whatever its shape, and wobble a little so they read as 3D.
            const float depth = 7f;
            float halfW = depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect;
            float wobble = Mathf.Sin(time * 2.2f) * 14f;
            float pulse = 1f + Mathf.Sin(time * 4f) * 0.04f;
            arrowRight.localPosition = new Vector3(halfW * 0.78f, -0.3f, depth);
            arrowLeft.localPosition = new Vector3(-halfW * 0.78f, -0.3f, depth);
            arrowRight.localRotation = Quaternion.Euler(0f, wobble, 0f);
            arrowLeft.localRotation = Quaternion.Euler(0f, -wobble, 180f);
            arrowRight.localScale = arrowLeft.localScale = Vector3.one * pulse;
        }

        void HandleTap()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            var es = EventSystem.current;
            if (es != null && es.IsPointerOverGameObject()) return;
            if (Input.touchCount > 0 && es != null && es.IsPointerOverGameObject(Input.GetTouch(0).fingerId)) return;

            if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out var hit, 300f)) return;

            if (hit.transform.IsChildOf(arrowLeft)) View(viewing - 1, false);
            else if (hit.transform.IsChildOf(arrowRight)) View(viewing + 1, false);
            else
            {
                var marker = hit.transform.GetComponentInParent<MapMarker>();
                if (marker != null && flow.IsOpen(marker.Level))
                {
                    flow.Load(marker.Level);
                    Refresh();
                }
            }
        }

        public void PreviewSummary() => ShowSummary();

        // For the editor capture tool: 0 = title, 1 = map, anything else = back in the game.
        public void Preview(int screen)
        {
            if (cam == null) cam = diveCam.GetComponent<Camera>();
            if (screen != 0 && screen != 1)
            {
                Play();
                return;
            }
            if (screen == 0)
            {
                ShowTitle();
            }
            else
            {
                resume = -1;
                EnterMap();
            }
            SnapCamera();
            FaceCamera();
        }
    }
}
