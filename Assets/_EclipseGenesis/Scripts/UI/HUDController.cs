using EclipseGenesis.Combat;
using EclipseGenesis.Mission;
using EclipseGenesis.Weapons;
using EclipseGenesis.World;
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

        private static readonly Color Panel = new Color(0.02f, 0.025f, 0.03f, 0.62f);
        private static readonly Color Grey = new Color(0.55f, 0.58f, 0.62f, 1f);
        private static readonly Color White = new Color(0.94f, 0.95f, 0.96f, 1f);
        private static readonly Color Red = new Color(0.9f, 0.14f, 0.1f, 1f);
        private static readonly Color Blue = new Color(0.22f, 0.66f, 1f, 1f);

        private Font _font;
        private RectTransform _root;
        private Image _shieldFill, _healthFill, _reloadFill, _vignette, _hitmarkerA, _hitmarkerB, _hitmarkerC, _hitmarkerD;
        private Text _shieldText, _healthText, _weaponName, _ammo, _reserve, _reloadLabel, _objective, _missionTitle, _banner, _bannerTitle, _prompt;
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
        public string ObjectiveText => _objective != null ? _objective.text : string.Empty;
        public float HealthFill => _healthFill != null ? _healthFill.fillAmount : 0f;
        public float ShieldFill => _shieldFill != null ? _shieldFill.fillAmount : 0f;
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
                _objective.transform.parent.gameObject.SetActive(false);
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

            // Bottom-left: shield + health.
            var vitals = Box(_root, "Vitals", new Vector2(0, 0), new Vector2(40, 40), new Vector2(420, 96));
            Label(vitals, "ESCUDO", 15, Grey, new Vector2(18, -12), new Vector2(120, 20), TextAnchor.UpperLeft);
            _shieldText = Label(vitals, "100", 15, Blue, new Vector2(-18, -12), new Vector2(80, 20), TextAnchor.UpperRight, rightAlign: true);
            _shieldFill = Bar(vitals, new Vector2(18, -34), new Vector2(384, 8), Blue);
            Label(vitals, "SALUD", 15, Grey, new Vector2(18, -50), new Vector2(120, 20), TextAnchor.UpperLeft);
            _healthText = Label(vitals, "100", 15, White, new Vector2(-18, -50), new Vector2(80, 20), TextAnchor.UpperRight, rightAlign: true);
            _healthFill = Bar(vitals, new Vector2(18, -72), new Vector2(384, 10), White);

            // Bottom-right: weapon + ammo.
            var weapon = Box(_root, "Weapon", new Vector2(1, 0), new Vector2(-40, 40), new Vector2(360, 110));
            _weaponName = Label(weapon, "EG-47", 16, Grey, new Vector2(18, -12), new Vector2(200, 22), TextAnchor.UpperLeft);
            Label(weapon, "AUTO", 13, Blue, new Vector2(-18, -14), new Vector2(80, 20), TextAnchor.UpperRight, rightAlign: true);
            _ammo = Label(weapon, "30", 52, White, new Vector2(18, -36), new Vector2(150, 60), TextAnchor.UpperLeft);
            _reserve = Label(weapon, "/ 120", 22, Grey, new Vector2(130, -62), new Vector2(160, 30), TextAnchor.UpperLeft);
            _reloadLabel = Label(weapon, "RECARGANDO", 13, Blue, new Vector2(-18, -66), new Vector2(160, 20), TextAnchor.UpperRight, rightAlign: true);
            _reloadFill = Bar(weapon, new Vector2(18, -96), new Vector2(324, 4), Blue);

            // Top-left: mission + objective.
            var obj = Box(_root, "Objective", new Vector2(0, 1), new Vector2(40, -40), new Vector2(520, 78));
            _missionTitle = Label(obj, MissionManager.MissionName, 14, Blue, new Vector2(18, -12), new Vector2(480, 20), TextAnchor.UpperLeft);
            Img(obj, "Accent", Blue, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(3, 0));
            _objective = Label(obj, "", 21, White, new Vector2(18, -36), new Vector2(490, 32), TextAnchor.UpperLeft);

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
                _shieldFill.fillAmount = health.Shield / Mathf.Max(1f, health.MaxShield);
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
                _ammo.text = weapons.Magazine.ToString();
                _ammo.color = weapons.Magazine <= weapons.CurrentData.magazineSize * 0.2f ? Red : White;
                _reserve.text = $"/ {weapons.Reserve}";
                bool reloading = weapons.IsReloading;
                _reloadLabel.text = reloading ? "RECARGANDO" : weapons.Magazine == 0 ? (weapons.Reserve > 0 ? "[R] RECARGAR" : "SIN MUNICIÓN") : "";
                _reloadFill.fillAmount = reloading ? weapons.ReloadProgress : 0f;
                UpdateCrosshair();
            }

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
            _objective.text = $"■  {text}";
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
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0.45f, 1f, d)));
            }
            tex.Apply();
            return tex;
        }
    }
}
