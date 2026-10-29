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
        [SerializeField] GameObject hud;
        [SerializeField] Button menuButton;

        [Header("Title")]
        [SerializeField] GameObject titlePanel;
        [SerializeField] Button playButton;
        [SerializeField] Text titleMedals;

        [Header("Map UI")]
        [SerializeField] GameObject mapPanel;
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

        readonly List<MapMarker> markers = new List<MapMarker>();
        Camera cam;
        bool open;
        int viewing;
        float time;

        void Start()
        {
            cam = diveCam.GetComponent<Camera>();
            playButton.onClick.AddListener(ShowMap);
            jumpButton.onClick.AddListener(Play);
            menuButton.onClick.AddListener(ShowMap);
            markerTemplate.gameObject.SetActive(false);
            if (flow.Saved == null) flow.Begin();
            ShowTitle();
        }

        public void ShowTitle()
        {
            Enter();
            titlePanel.SetActive(true);
            mapPanel.SetActive(false);
            arrows.gameObject.SetActive(false);
            ShowMarkers(false);
            viewing = flow.Level.location;
            SnapCamera();

            if (titleMedals != null)
            {
                int medals = 0;
                foreach (int s in flow.Saved.stars) medals += s;
                titleMedals.text = $"Медали: {medals} из {flow.Count * 3}";
            }
        }

        public void ShowMap()
        {
            bool fromGame = !open;
            Enter();
            titlePanel.SetActive(false);
            mapPanel.SetActive(true);
            arrows.gameObject.SetActive(true);
            View(flow.Level.location, true);
            if (fromGame) SnapCamera();
        }

        void Enter()
        {
            if (cam == null) cam = diveCam.GetComponent<Camera>();
            if (!open) flow.Retry();
            open = true;
            diveCam.enabled = false;
            diver.AcceptInput = false;
            hud.SetActive(false);
            RenderSettings.fog = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backdrop;
            flow.ShowAllSites(true);
        }

        void Play()
        {
            if (!flow.IsOpen(flow.Index)) return;
            open = false;
            titlePanel.SetActive(false);
            mapPanel.SetActive(false);
            arrows.gameObject.SetActive(false);
            ShowMarkers(false);
            hud.SetActive(true);
            RenderSettings.fog = true;
            cam.clearFlags = CameraClearFlags.Skybox;
            flow.ShowAllSites(false);
            diveCam.enabled = true;
            diveCam.BlendFromCurrent();
            diver.AcceptInput = true;
        }

        void View(int location, bool keepLevel)
        {
            viewing = Mathf.Clamp(location, 0, flow.LocationCount - 1);
            if (!keepLevel || flow.Level.location != viewing)
                flow.Load(flow.LastOpenIn(viewing));
            BuildMarkers();
            Refresh();
        }

        void BuildMarkers()
        {
            var levels = flow.LevelsIn(viewing);
            while (markers.Count < levels.Count)
                markers.Add(Instantiate(markerTemplate, markerTemplate.transform.parent));

            // The landing spots sit close together compared with the whole block, so the buoys line up
            // in a row above the location, facing the map camera, each tied to its own spot by a pole.
            var site = flow.Site(viewing);
            site.MapPose(0f, out var camPos, out var look);
            var right = Vector3.Cross(Vector3.up, (look - camPos).normalized).normalized;
            float size = MarkerSize();

            Vector3 middle = Vector3.zero;
            float tallest = 0f;
            foreach (int l in levels)
            {
                middle += flow.ZoneCenterOf(l);
                tallest = Mathf.Max(tallest, flow.HeightOf(l));
            }
            middle /= levels.Count;

            for (int i = 0; i < markers.Count; i++)
            {
                var m = markers[i];
                bool used = i < levels.Count;
                m.gameObject.SetActive(used);
                if (!used) continue;

                int level = levels[i];
                float across = (i - (levels.Count - 1) * 0.5f) * size * 1.35f;
                float lift = tallest + size * (markerHeight * 0.6f) + (i % 2) * size * 0.35f;
                m.transform.position = middle + Vector3.up * lift + right * across;
                m.PointTo(flow.ZoneCenterOf(level), size * 0.045f);
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

            bool locationOpen = flow.IsOpen(levels[0]);
            lockedText.gameObject.SetActive(!locationOpen);
            if (!locationOpen)
                lockedText.text = $"Откроется после локации «{flow.LocationName(viewing - 1)}»";

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
            if (mapPanel.activeSelf) HandleTap();
        }

        void SnapCamera() => MoveCamera(-1f);

        // Hover over the location with a slow sideways sway; dt < 0 jumps straight there.
        void MoveCamera(float dt)
        {
            flow.Site(viewing).MapPose(Mathf.Sin(time * swaySpeed) * swayAngle, out var pos, out var look);
            var rot = Quaternion.LookRotation(look - pos);
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

        // For the editor capture tool: 0 = title, 1 = map, anything else = back in the game.
        public void Preview(int screen)
        {
            if (cam == null) cam = diveCam.GetComponent<Camera>();
            if (screen != 0 && screen != 1)
            {
                Play();
                return;
            }
            if (screen == 0) ShowTitle();
            else ShowMap();
            SnapCamera();
            FaceCamera();
        }
    }
}
