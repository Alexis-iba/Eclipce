using EclipseGenesis.Combat;
using EclipseGenesis.Mission;
using EclipseGenesis.Weapons;
using EclipseGenesis.World;
using EclipseGenesis.AI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace EclipseGenesis.UI
{
    /// <summary>
    /// Military sci-fi HUD built from code at startup (no fragile scene UI references):
    /// shield/health, weapon + ammo with reload bar, objective, dynamic crosshair, hitmarker, directional damage,
    /// low-health vignette, interaction prompt, objective banner and victory/defeat screens.
    /// Palette: black, dark grey, white, red, tech blue.
    /// </summary>
    public sealed class HUDController : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [SerializeField] private WeaponController weapons;
        [SerializeField] private PlayerInteraction interaction;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private AudioSource uiAudio;
        [SerializeField] private AudioClip hitmarkerClip;

        private static readonly Color Panel = new Color(0.02f, 0.025f, 0.03f, 0.72f);
        private static readonly Color Grey = new Color(0.55f, 0.58f, 0.62f, 1f);
        private static readonly Color White = new Color(0.94f, 0.95f, 0.96f, 1f);
        private static readonly Color Red = new Color(0.9f, 0.14f, 0.1f, 1f);
        private static readonly Color Blue = new Color(0.22f, 0.66f, 1f, 1f);

        private static readonly Color Amber = new Color(1f, 0.72f, 0.2f, 1f);
        private static readonly Color Dim = new Color(0.55f, 0.58f, 0.62f, 0.55f);
        private const float RadarSize = 176f;
        private const float RadarRange = 60f;
        private const int ShieldPlates = 3;
        private const int MaxObjectiveRows = 4;

        private Font _font;
        private RectTransform _root;
        private Image _healthFill, _reloadFill, _vignette, _hitmarkerA, _hitmarkerB, _hitmarkerC, _hitmarkerD;
        private readonly Image[] _shieldPlates = new Image[ShieldPlates];
        private float _shieldFraction = 1f;
        private Text _shieldText, _healthText, _weaponName, _ammo, _reserve, _reloadLabel, _fireMode, _missionTitle, _banner, _bannerTitle, _prompt;
        private string _objectiveText = string.Empty;
        private RectTransform _objectivePanel, _objectiveList;
        private Text[] _objectiveRows = new Text[0];
        private RectTransform _radar, _radarObjective;
        private Text _heading, _objectiveDistance;
        private readonly Text[] _radarCardinals = new Text[4];
        private readonly Image[] _radarBlips = new Image[12];
        private AlienHealth[] _aliens = new AlienHealth[0];
        private float _nextAlienScan;
        private CanvasGroup _bannerGroup, _endGroup;
        private RectTransform[] _crosshair = new RectTransform[4];
        private Image _crossDot;
        private readonly Image[] _damageArcs = new Image[4];
        private readonly float[] _damageTimes = new float[4];
        private readonly Vector3[] _damageDirs = new Vector3[4];
        private int _nextArc;
        private float _hitmarkerTime = -10f;
        private bool _hitmarkerKill;
        private float _bannerTime = -10f;
        private Text _endTitle, _endStats;
        private GameObject _endButtons;

        public string AmmoText => _ammo != null ? _ammo.text : string.Empty;
        public string ObjectiveText => _objectiveText;
        public float HealthFill => _healthFill != null ? _healthFill.fillAmount : 0f;
        public float ShieldFill => _shieldFraction;
        public bool HitmarkerVisible => _hitmarkerA != null && _hitmarkerA.color.a > 0.01f;
        public bool EndScreenVisible => _endGroup != null && _endGroup.alpha > 0.5f;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCanvas();
        }

        private void OnEnable()
        {
            if (health != null) { health.Damaged += OnDamaged; }
            if (weapons != null) { weapons.HitConfirmed += OnHit; }
        }

        private void OnDisable()
        {
            if (health != null) { health.Damaged -= OnDamaged; }
            if (weapons != null) { weapons.HitConfirmed -= OnHit; }
            var m = MissionManager.Current;
            if (m != null) { m.ObjectiveChanged -= OnObjective; m.Ended -= OnMissionEnded; }
        }

        private void Start()
        {
            var m = MissionManager.Current;
            if (m != null)
            {
                m.ObjectiveChanged += OnObjective;
                m.Ended += OnMissionEnded;
                OnObjective(m.ObjectiveIndex, m.CurrentObjective);
            }
            else
            {
                _objectivePanel.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ build

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("HUD_Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            _root = canvasGo.GetComponent<RectTransform>();

            // Low-health vignette (behind everything).
            _vignette = Img(_root, "Vignette", Red, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _vignette.sprite = Sprite.Create(RadialTexture(), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
            _vignette.color = new Color(Red.r, Red.g, Red.b, 0f);
            _vignette.raycastTarget = false;

            BuildVitals();
            BuildWeaponCard();
            BuildRadar();
            BuildObjectivePanel();

            // Objective banner.
            var banner = new GameObject("Banner", typeof(RectTransform)).GetComponent<RectTransform>();
            banner.SetParent(_root, false);
            Anchor(banner, new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(760, 80));
            _bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            Img(banner, "Bg", Panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _bannerTitle = Label(banner, "OBJETIVO ACTUALIZADO", 14, Blue, new Vector2(0, -12), new Vector2(760, 20), TextAnchor.UpperCenter, center: true);
            _banner = Label(banner, "", 24, White, new Vector2(0, -36), new Vector2(760, 34), TextAnchor.UpperCenter, center: true);
            _bannerGroup.alpha = 0f;

            // Crosshair.
            for (int i = 0; i < 4; i++)
            {
                var tick = Img(_root, "Cross", White, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, i % 2 == 0 ? new Vector2(2, 12) : new Vector2(12, 2));
                _crosshair[i] = tick.rectTransform;
                tick.raycastTarget = false;
            }
            _crossDot = Img(_root, "CrossDot", White, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3, 3));

            // Hitmarker (4 diagonal strokes).
            _hitmarkerA = Hit(new Vector2(-12, 12), 45f);
            _hitmarkerB = Hit(new Vector2(12, 12), -45f);
            _hitmarkerC = Hit(new Vector2(-12, -12), -45f);
            _hitmarkerD = Hit(new Vector2(12, -12), 45f);

            // Directional damage arcs.
            for (int i = 0; i < _damageArcs.Length; i++)
            {
                var pivot = new GameObject("DamageDir", typeof(RectTransform)).GetComponent<RectTransform>();
                pivot.SetParent(_root, false);
                Anchor(pivot, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                var arc = Img(pivot, "Arc", Red, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(150, 10));
                arc.color = new Color(Red.r, Red.g, Red.b, 0f);
                _damageArcs[i] = arc;
                _damageTimes[i] = -10f;
            }

            // Interaction prompt.
            var promptBox = new GameObject("Prompt", typeof(RectTransform)).GetComponent<RectTransform>();
            promptBox.SetParent(_root, false);
            Anchor(promptBox, new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(600, 40));
            _prompt = Label(promptBox, "", 22, White, Vector2.zero, new Vector2(600, 40), TextAnchor.MiddleCenter, center: true);

            BuildEndScreen();
        }

        private void BuildVitals()
        {
            // Bottom-left: segmented armour plates (shield) over a health bar.
            var vitals = Box(_root, "Vitals", new Vector2(0, 0), new Vector2(32, 32), new Vector2(340, 84));
            Img(vitals, "Accent", Blue, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(3, 0));
            Label(vitals, "ESCUDO", 14, Grey, new Vector2(18, -10), new Vector2(120, 20), TextAnchor.UpperLeft);
            _shieldText = Label(vitals, "100", 16, Blue, new Vector2(-18, -9), new Vector2(80, 20), TextAnchor.UpperRight, rightAlign: true);
            float plateW = (304f - (ShieldPlates - 1) * 5f) / ShieldPlates;
            for (int i = 0; i < ShieldPlates; i++)
                _shieldPlates[i] = Bar(vitals, new Vector2(18 + i * (plateW + 5f), -30), new Vector2(plateW, 8), Blue);
            Label(vitals, "SALUD", 14, Grey, new Vector2(18, -48), new Vector2(120, 20), TextAnchor.UpperLeft);
            _healthText = Label(vitals, "100", 16, White, new Vector2(-18, -47), new Vector2(80, 20), TextAnchor.UpperRight, rightAlign: true);
            _healthFill = Bar(vitals, new Vector2(18, -64), new Vector2(304, 10), White);
        }

        private void BuildWeaponCard()
        {
            // Bottom-right: weapon silhouette, name, fire mode, magazine / reserve, reload bar and grenade slots.
            var card = Box(_root, "Weapon", new Vector2(1, 0), new Vector2(-32, 32), new Vector2(390, 122));
            Img(card, "Accent", Blue, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-3, 0), new Vector2(3, 0));
            var icon = new GameObject("WeaponIcon", typeof(RectTransform)).GetComponent<RectTransform>();
            icon.SetParent(card, false);
            Anchor(icon, new Vector2(0, 1), new Vector2(16, -14), new Vector2(190, 62));
            RifleSilhouette(icon, new Color(0.94f, 0.95f, 0.96f, 0.85f));
            _weaponName = Label(card, "EG-47", 22, White, new Vector2(-20, -10), new Vector2(200, 28), TextAnchor.UpperRight, rightAlign: true);
            _fireMode = Label(card, "AUTO", 13, Blue, new Vector2(-20, -38), new Vector2(120, 18), TextAnchor.UpperRight, rightAlign: true);
            _reserve = Label(card, "/ 120", 22, Grey, new Vector2(-20, -66), new Vector2(110, 30), TextAnchor.UpperRight, rightAlign: true);
            _ammo = Label(card, "30", 50, White, new Vector2(-112, -50), new Vector2(110, 56), TextAnchor.UpperRight, rightAlign: true);
            _reloadLabel = Label(card, "", 13, Blue, new Vector2(140, -88), new Vector2(150, 20), TextAnchor.UpperLeft);
            _reloadFill = Bar(card, new Vector2(16, -108), new Vector2(358, 3), Blue);

            // Grenade slots (lethal / tactical) under the weapon icon. Visual only until the grenade system exists (see ROADMAP).
            for (int i = 0; i < 2; i++)
            {
                var slot = Img(card, "GrenadeSlot", new Color(1f, 1f, 1f, 0.08f), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(40, 22));
                slot.rectTransform.pivot = new Vector2(0, 1);
                slot.rectTransform.anchoredPosition = new Vector2(16 + i * 46, -84);
                var pin = Img(slot.rectTransform, "Grenade", i == 0 ? Dim : new Color(Blue.r, Blue.g, Blue.b, 0.55f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-6, 0), new Vector2(11, 14));
                pin.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
                Label(slot.rectTransform, "0", 13, Dim, new Vector2(-4, -3), new Vector2(16, 18), TextAnchor.UpperRight, rightAlign: true);
            }
            Label(card, "[G]", 12, Dim, new Vector2(110, -88), new Vector2(30, 18), TextAnchor.UpperLeft);
        }

        private void RifleSilhouette(RectTransform parent, Color c)
        {
            // Simple side view of the EG-47 (muzzle to the right), built from rectangles so it scales cleanly.
            void Part(string n, float x, float y, float w, float h, float rot = 0f)
            {
                var img = Img(parent, n, c, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(w, h));
                img.rectTransform.pivot = new Vector2(0, 0);
                img.rectTransform.anchoredPosition = new Vector2(x, y);
                img.rectTransform.localRotation = Quaternion.Euler(0, 0, rot);
            }
            Part("Stock", 0, 30, 34, 16);
            Part("StockTube", 30, 36, 14, 7);
            Part("Receiver", 42, 30, 70, 18);
            Part("Rail", 50, 48, 58, 3);
            Part("Sight", 64, 51, 26, 10);
            Part("Handguard", 112, 32, 46, 14);
            Part("Barrel", 158, 36, 22, 5);
            Part("Muzzle", 178, 34, 10, 9);
            Part("Grip", 54, 9, 11, 22, 16f);
            Part("Magazine", 82, 6, 14, 25, -12f);
            Part("Trigger", 64, 24, 14, 6);
        }

        private void BuildRadar()
        {
            // Top-left: heading-up radar with rotating cardinal letters, objective marker and nearby alien blips.
            var frame = new GameObject("Radar", typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(_root, false);
            Anchor(frame, new Vector2(0, 1), new Vector2(32, -32), new Vector2(RadarSize, RadarSize));
            var disk = Sprite.Create(CircleTexture(0f), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
            var ring = Sprite.Create(CircleTexture(0.94f), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
            var bg = Img(frame, "Disk", new Color(0.02f, 0.03f, 0.035f, 0.72f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bg.sprite = disk;
            var mapObject = new GameObject("MapFootprints", typeof(RectTransform), typeof(RawImage));
            mapObject.transform.SetParent(frame, false);
            var mapRect = mapObject.GetComponent<RectTransform>();
            mapRect.anchorMin = Vector2.zero; mapRect.anchorMax = Vector2.one;
            mapRect.offsetMin = mapRect.offsetMax = Vector2.zero;
            mapObject.AddComponent<RadarWorldMap>().Initialize(viewCamera);
            var outer = Img(frame, "Ring", new Color(Blue.r, Blue.g, Blue.b, 0.8f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            outer.sprite = ring;
            var inner = Img(frame, "RingInner", new Color(1f, 1f, 1f, 0.12f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(RadarSize * 0.5f, RadarSize * 0.5f));
            inner.sprite = ring;
            Img(frame, "AxisV", new Color(1f, 1f, 1f, 0.06f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1, RadarSize - 16));
            Img(frame, "AxisH", new Color(1f, 1f, 1f, 0.06f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(RadarSize - 16, 1));
            _radar = frame;

            string[] names = { "N", "E", "S", "O" };
            for (int i = 0; i < 4; i++)
            {
                var l = Label(frame, names[i], i == 0 ? 18 : 15, i == 0 ? Amber : Grey, Vector2.zero, new Vector2(24, 22), TextAnchor.MiddleCenter, center: true);
                l.rectTransform.anchorMin = l.rectTransform.anchorMax = l.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _radarCardinals[i] = l;
            }

            for (int i = 0; i < _radarBlips.Length; i++)
            {
                _radarBlips[i] = Img(frame, "Blip", Red, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8, 8));
                _radarBlips[i].sprite = disk;
                _radarBlips[i].enabled = false;
            }

            var marker = Img(frame, "ObjectiveMarker", Amber, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12, 12));
            marker.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
            _radarObjective = marker.rectTransform;

            var player = Label(frame, "▲", 18, White, Vector2.zero, new Vector2(24, 24), TextAnchor.MiddleCenter, center: true);
            player.rectTransform.anchorMin = player.rectTransform.anchorMax = player.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            _heading = Label(frame, "N 000°", 15, White, new Vector2(0, -RadarSize - 6), new Vector2(RadarSize, 20), TextAnchor.UpperCenter, center: true);
            _objectiveDistance = Label(frame, "", 14, Amber, new Vector2(0, -RadarSize - 26), new Vector2(RadarSize, 20), TextAnchor.UpperCenter, center: true);
        }

        private void BuildObjectivePanel()
        {
            // Right of the radar: operation name and the full objective checklist.
            _objectivePanel = Box(_root, "Objective", new Vector2(0, 1), new Vector2(32 + RadarSize + 14, -32), new Vector2(350, 60));
            Img(_objectivePanel, "Accent", Blue, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(3, 0));
            _missionTitle = Label(_objectivePanel, MissionManager.MissionName, 13, Blue, new Vector2(16, -10), new Vector2(330, 20), TextAnchor.UpperLeft);
            _objectiveList = new GameObject("List", typeof(RectTransform)).GetComponent<RectTransform>();
            _objectiveList.SetParent(_objectivePanel, false);
            Anchor(_objectiveList, new Vector2(0, 1), new Vector2(0, -34), new Vector2(350, 0));
        }

        private void RefreshObjectiveList(int index, string currentText)
        {
            var m = MissionManager.Current;
            var all = m != null ? m.Objectives : null;
            if (all == null || all.Count == 0) return;
            // Compact checklist (concept art): a window of up to four objectives, the previous one included once done.
            int rows = Mathf.Min(MaxObjectiveRows, all.Count);
            if (_objectiveRows.Length != rows)
            {
                foreach (var row in _objectiveRows) if (row != null) Destroy(row.gameObject);
                _objectiveRows = new Text[rows];
                for (int i = 0; i < rows; i++)
                    _objectiveRows[i] = Label(_objectiveList, "", 15, Grey, new Vector2(16, -i * 22), new Vector2(330, 22), TextAnchor.UpperLeft);
                _objectivePanel.sizeDelta = new Vector2(_objectivePanel.sizeDelta.x, 42 + rows * 22);
            }
            bool finished = m.State != MissionState.InProgress;
            int first = Mathf.Clamp(index - 1, 0, all.Count - rows);
            for (int r = 0; r < rows; r++)
            {
                int i = first + r;
                bool done = i < index || (finished && m.State == MissionState.Victory);
                bool current = i == index && !finished;
                _objectiveRows[r].text = done ? $"■  {all[i]}" : current ? $"►  {currentText}" : $"□  {all[i]}";
                _objectiveRows[r].color = done ? new Color(Blue.r, Blue.g, Blue.b, 0.75f) : current ? White : new Color(0.85f, 0.87f, 0.9f, 0.85f);
                _objectiveRows[r].fontStyle = current ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void UpdateRadar(float t)
        {
            if (viewCamera == null || _radar == null) return;
            Vector3 fwd = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = transform.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 origin = transform.position;
            float radius = RadarSize * 0.5f;
            Vector2 ToRadar(Vector3 world) => new Vector2(Vector3.Dot(world, right), Vector3.Dot(world, fwd));

            Vector3[] cardinals = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            for (int i = 0; i < 4; i++)
                _radarCardinals[i].rectTransform.anchoredPosition = ToRadar(cardinals[i]) * (radius - 15f);
            float yaw = Mathf.Repeat(Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg, 360f);
            string[] dirs = { "N", "NE", "E", "SE", "S", "SO", "O", "NO" };
            _heading.text = $"{dirs[Mathf.RoundToInt(yaw / 45f) % 8]}  {Mathf.RoundToInt(yaw) % 360:000}°";

            var m = MissionManager.Current;
            if (m != null && m.State == MissionState.InProgress && m.TryGetObjectivePosition(m.ObjectiveIndex, out var target))
            {
                Vector3 rel = target - origin;
                rel.y = 0f;
                Vector2 p = ToRadar(rel) / RadarRange * radius;
                if (p.magnitude > radius - 10f) p = p.normalized * (radius - 10f);
                _radarObjective.gameObject.SetActive(true);
                _radarObjective.anchoredPosition = p;
                float pulse = 1f + 0.15f * Mathf.Sin(t * 4f);
                _radarObjective.localScale = new Vector3(pulse, pulse, 1f);
                _objectiveDistance.text = $"OBJETIVO  {Mathf.RoundToInt(rel.magnitude)} m";
            }
            else
            {
                _radarObjective.gameObject.SetActive(false);
                _objectiveDistance.text = "";
            }

            // Proximity sensor: living aliens inside the radar range.
            if (t >= _nextAlienScan)
            {
                _aliens = FindObjectsByType<AlienHealth>();
                _nextAlienScan = t + 1f;
            }
            int blip = 0;
            foreach (var a in _aliens)
            {
                if (blip >= _radarBlips.Length) break;
                if (a == null || !a.IsAlive) continue;
                Vector3 rel = a.transform.position - origin;
                rel.y = 0f;
                if (rel.magnitude > RadarRange) continue;
                var img = _radarBlips[blip++];
                img.enabled = true;
                img.rectTransform.anchoredPosition = ToRadar(rel) / RadarRange * (radius - 6f);
                img.color = new Color(Red.r, Red.g, Red.b, 0.7f + 0.3f * Mathf.Sin(t * 6f + blip));
            }
            for (int i = blip; i < _radarBlips.Length; i++) _radarBlips[i].enabled = false;
        }

        private static Texture2D CircleTexture(float innerRadius)
        {
            // Anti-aliased disk (innerRadius 0) or ring (innerRadius = fraction of the outer radius).
            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)) / 63.5f;
                float a = Mathf.Clamp01((1f - d) * 64f);
                if (innerRadius > 0f) a *= Mathf.Clamp01((d - innerRadius) * 64f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            tex.Apply();
            return tex;
        }

        private void BuildEndScreen()
        {
            var end = new GameObject("EndScreen", typeof(RectTransform)).GetComponent<RectTransform>();
            end.SetParent(_root, false);
            end.anchorMin = Vector2.zero; end.anchorMax = Vector2.one; end.offsetMin = end.offsetMax = Vector2.zero;
            _endGroup = end.gameObject.AddComponent<CanvasGroup>();
            Img(end, "Dim", new Color(0f, 0f, 0f, 0.78f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(end, false);
            Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 380));
            Img(panel, "Bg", new Color(0.05f, 0.055f, 0.065f, 0.95f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Img(panel, "Line", Blue, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -2), new Vector2(0, 2));
            Label(panel, MissionManager.MissionName, 16, Grey, new Vector2(0, -28), new Vector2(760, 24), TextAnchor.UpperCenter, center: true);
            _endTitle = Label(panel, "", 46, White, new Vector2(0, -62), new Vector2(760, 60), TextAnchor.UpperCenter, center: true);
            _endStats = Label(panel, "", 20, White, new Vector2(0, -140), new Vector2(700, 110), TextAnchor.UpperCenter, center: true);

            _endButtons = new GameObject("Buttons", typeof(RectTransform));
            var buttons = _endButtons.GetComponent<RectTransform>();
            buttons.SetParent(panel, false);
            Anchor(buttons, new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(600, 56));
            Button(buttons, "REINTENTAR", new Vector2(-150, 0), () => MissionManager.Current?.Restart());
            Button(buttons, "SALIR", new Vector2(150, 0), () => MissionManager.Current?.Quit());

            _endGroup.alpha = 0f;
            _endGroup.interactable = false;
            _endGroup.blocksRaycasts = false;
        }

        // ------------------------------------------------------------------ update

        private void Update()
        {
            float t = Time.unscaledTime;
            if (health != null)
            {
                _shieldFraction = health.Shield / Mathf.Max(1f, health.MaxShield);
                for (int i = 0; i < ShieldPlates; i++) _shieldPlates[i].fillAmount = Mathf.Clamp01(_shieldFraction * ShieldPlates - i);
                _healthFill.fillAmount = health.Health / Mathf.Max(1f, health.MaxHealth);
                _shieldText.text = Mathf.CeilToInt(health.Shield).ToString();
                _healthText.text = Mathf.CeilToInt(health.Health).ToString();
                float low = 1f - Mathf.Clamp01(health.Health / (health.MaxHealth * 0.35f));
                _healthFill.color = Color.Lerp(White, Red, low);
                float pulse = 0.75f + 0.25f * Mathf.Sin(t * 5f);
                _vignette.color = new Color(Red.r, Red.g, Red.b, low * 0.55f * pulse);
            }

            if (weapons != null && weapons.CurrentData != null)
            {
                _weaponName.text = weapons.CurrentData.displayName;
                _fireMode.text = weapons.CurrentData.pellets > 1 ? "BOMBEO" : weapons.CurrentData.automatic ? "AUTO" : "SEMI";
                _ammo.text = weapons.Magazine.ToString();
                _ammo.color = weapons.Magazine <= weapons.CurrentData.magazineSize * 0.2f ? Red : White;
                _reserve.text = $"/ {weapons.Reserve}";
                bool reloading = weapons.IsReloading;
                _reloadLabel.text = reloading ? "RECARGANDO" : weapons.Magazine == 0 ? (weapons.Reserve > 0 ? "[R] RECARGAR" : "SIN MUNICIÓN") : "";
                _reloadFill.fillAmount = reloading ? weapons.ReloadProgress : 0f;
                UpdateCrosshair();
            }

            UpdateRadar(t);

            _prompt.text = interaction != null && interaction.Focused != null && interaction.Focused.CanInteract ? $"[F]  {interaction.Focused.Prompt}" : "";

            float hm = Mathf.Clamp01(1f - (t - _hitmarkerTime) / 0.28f);
            var hmColor = _hitmarkerKill ? Red : White;
            hmColor.a = hm;
            _hitmarkerA.color = _hitmarkerB.color = _hitmarkerC.color = _hitmarkerD.color = hmColor;

            _bannerGroup.alpha = Mathf.Clamp01(3.2f - (t - _bannerTime)) * Mathf.Clamp01((t - _bannerTime) * 6f);

            UpdateDamageArcs(t);
        }

        private void UpdateCrosshair()
        {
            float aim = weapons.AimAmount;
            float fov = viewCamera != null ? viewCamera.fieldOfView : 68f;
            float px = Mathf.Tan(weapons.CurrentSpread * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * 540f;
            float gap = 8f + px;
            Vector2[] dirs = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
            for (int i = 0; i < 4; i++)
            {
                _crosshair[i].anchoredPosition = dirs[i] * (gap + 6f);
                var img = _crosshair[i].GetComponent<Image>();
                img.color = new Color(1f, 1f, 1f, (1f - aim) * (weapons.Movement != null && weapons.Movement.IsSprinting ? 0.25f : 0.9f));
            }
            _crossDot.color = new Color(1f, 1f, 1f, 0.9f * (1f - aim));
        }

        private void UpdateDamageArcs(float t)
        {
            if (viewCamera == null) return;
            Vector3 fwd = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up).normalized;
            for (int i = 0; i < _damageArcs.Length; i++)
            {
                float age = t - _damageTimes[i];
                float a = Mathf.Clamp01(1f - age / 1.4f);
                var c = _damageArcs[i].color;
                c.a = a * 0.85f;
                _damageArcs[i].color = c;
                if (a <= 0f) continue;
                Vector3 toSource = Vector3.ProjectOnPlane(-_damageDirs[i], Vector3.up);
                float angle = Vector3.SignedAngle(fwd, toSource, Vector3.up);
                _damageArcs[i].rectTransform.parent.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }
        }

        // ------------------------------------------------------------------ events

        private void OnHit(DamageResult result)
        {
            _hitmarkerTime = Time.unscaledTime;
            _hitmarkerKill = result.Killed;
            if (uiAudio != null && hitmarkerClip != null) uiAudio.PlayOneShot(hitmarkerClip, result.Killed ? 1f : 0.6f);
        }

        private void OnDamaged(DamageInfo info)
        {
            _damageDirs[_nextArc] = info.Direction.sqrMagnitude > 0.001f ? info.Direction.normalized : -transform.forward;
            _damageTimes[_nextArc] = Time.unscaledTime;
            _nextArc = (_nextArc + 1) % _damageArcs.Length;
        }

        private void OnObjective(int index, string text)
        {
            _objectiveText = $"■  {text}";
            RefreshObjectiveList(index, text);
            if (index > 0 || Time.timeSinceLevelLoad > 0.5f)
            {
                _banner.text = text;
                _bannerTime = Time.unscaledTime;
            }
        }

        private void OnMissionEnded(MissionState state)
        {
            var m = MissionManager.Current;
            bool win = state == MissionState.Victory;
            _endTitle.text = win ? "MISIÓN COMPLETADA" : "MISIÓN FALLIDA";
            _endTitle.color = win ? Blue : Red;
            float acc = weapons != null && weapons.ShotsFired > 0 ? 100f * weapons.ShotsHit / weapons.ShotsFired : 0f;
            int seconds = m != null ? Mathf.FloorToInt(m.ElapsedTime) : 0;
            _endStats.text = $"Tiempo  {seconds / 60:00}:{seconds % 60:00}\nStalker eliminados  {(m != null ? m.AliensKilled : 0)} / {(m != null ? m.AliensTotal : 0)}\nPrecisión  {acc:0}%";
            _endGroup.alpha = 1f;
            _endGroup.interactable = true;
            _endGroup.blocksRaycasts = true;
            EnsureEventSystem();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // ------------------------------------------------------------------ ui helpers

        private static void Anchor(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = anchor;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        private RectTransform Box(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            Anchor(r, anchor, pos, size);
            var bg = r.gameObject.AddComponent<Image>();
            bg.color = Panel;
            bg.raycastTarget = false;
            return r;
        }

        private static Image Img(RectTransform parent, string name, Color c, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.pivot = new Vector2(0.5f, 0.5f);
            if (anchorMin == anchorMax)
            {
                r.anchoredPosition = pos;
                r.sizeDelta = size;
            }
            else
            {
                r.offsetMin = pos;
                r.offsetMax = pos + size;
            }
            var img = r.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        private Image Bar(RectTransform parent, Vector2 topLeft, Vector2 size, Color fill)
        {
            var bg = Img(parent, "BarBg", new Color(1f, 1f, 1f, 0.12f), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, size);
            bg.rectTransform.pivot = new Vector2(0, 1);
            bg.rectTransform.anchoredPosition = topLeft;
            var f = Img(bg.rectTransform, "Fill", fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            f.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            f.type = Image.Type.Filled;
            f.fillMethod = Image.FillMethod.Horizontal;
            f.fillAmount = 1f;
            return f;
        }

        private Text Label(RectTransform parent, string text, int size, Color color, Vector2 pos, Vector2 box, TextAnchor align, bool rightAlign = false, bool center = false)
        {
            var r = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            Vector2 anchor = center ? new Vector2(0.5f, 1f) : rightAlign ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = anchor;
            r.anchoredPosition = pos;
            r.sizeDelta = box;
            var t = r.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        private Image Hit(Vector2 pos, float rotation)
        {
            var img = Img(_root, "Hitmarker", White, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(3, 13));
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, rotation);
            img.color = new Color(1, 1, 1, 0);
            return img;
        }

        private void Button(RectTransform parent, string text, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var r = new GameObject(text, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            Anchor(r, new Vector2(0.5f, 0.5f), pos, new Vector2(260, 52));
            var bg = r.gameObject.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.11f, 0.13f, 1f);
            var b = r.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(0.22f, 0.66f, 1f, 1f);
            colors.pressedColor = new Color(0.12f, 0.4f, 0.7f, 1f);
            b.colors = colors;
            b.onClick.AddListener(onClick);
            Label(r, text, 20, White, Vector2.zero, new Vector2(260, 52), TextAnchor.MiddleCenter, center: true).rectTransform.anchoredPosition = new Vector2(0, 0);
            var lt = r.GetChild(0) as RectTransform;
            lt.anchorMin = lt.anchorMax = lt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Texture2D RadialTexture()
        {
            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)) / 90f;
                float s = Mathf.Clamp01((d - 0.45f) / 0.55f);
                tex.SetPixel(x, y, new Color(1, 1, 1, s * s * (3f - 2f * s)));
            }
            tex.Apply();
            return tex;
        }
    }
}

