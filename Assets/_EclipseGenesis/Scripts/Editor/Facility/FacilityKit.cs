using System.Collections.Generic;
using EclipseGenesis.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace EclipseGenesis.EditorTools.Facility
{
    public enum Side { North, South, East, West }

    /// <summary>A doorway cut into one side of a room. Center is measured along the wall (x for N/S, z for E/W).</summary>
    public readonly struct Opening
    {
        public readonly Side Side;
        public readonly float Center, Width, Height, Bottom;

        /// <param name="bottom">Sill height: 0 for doorways, &gt; 0 for windows.</param>
        public Opening(Side side, float center, float width = 2.6f, float height = 2.9f, float bottom = 0f)
        {
            Side = side; Center = center; Width = width; Height = height; Bottom = bottom;
        }

        public static Opening Window(Side side, float center, float width = 1.4f, float sill = 0.9f, float top = 2.1f) =>
            new Opening(side, center, width, top, sill);
    }

    /// <summary>Axis-aligned room footprint (x0..x1, z0..z1) and ceiling height.</summary>
    public readonly struct RoomRect
    {
        public readonly float X0, X1, Z0, Z1, Height;
        public RoomRect(float x0, float x1, float z0, float z1, float height) { X0 = x0; X1 = x1; Z0 = z0; Z1 = z1; Height = height; }
        public Vector3 Center => new Vector3((X0 + X1) * 0.5f, 0f, (Z0 + Z1) * 0.5f);
        public Vector3 Size => new Vector3(X1 - X0, Height, Z1 - Z0);
    }

    /// <summary>Scene-building vocabulary for the facility: architecture, lights, doors, props and ambience.</summary>
    public sealed class FacilityKit
    {
        public const float WallThickness = 0.3f;
        public const float LowerWallHeight = 1.2f;

        public readonly FacilityMaterials M;
        public readonly MeshKit Meshes;
        public readonly List<Light> ShadowedLights = new List<Light>();
        public readonly List<Light> AllLights = new List<Light>();

        /// <summary>Global brightness trim for every authored light (tuned against the fixed exposure).</summary>
        public const float LightScale = 1.5f;

        private readonly AudioClip _doorClip, _deniedClip, _crackleClip;
        private readonly Texture3D _smokeMask;
        private readonly System.Random _rng = new System.Random(2187);

        public FacilityKit(FacilityMaterials materials, MeshKit meshes, Texture3D smokeMask)
        {
            M = materials;
            Meshes = meshes;
            _smokeMask = smokeMask;
            _doorClip = ProceduralAudioClips.DoorSlide();
            _deniedClip = ProceduralAudioClips.DoorDenied();
            _crackleClip = ProceduralAudioClips.SparkCrackle();
        }

        public float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        public static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        // ------------------------------------------------------------------ primitives

        public GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool collider = true, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = Meshes.Box(size);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        public GameObject Cylinder(Transform parent, string name, Vector3 center, float radius, float height, Material mat, Quaternion rotation, bool collider = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rotation;
            go.AddComponent<MeshFilter>().sharedMesh = Meshes.Cylinder(radius, height);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider)
            {
                var c = go.AddComponent<CapsuleCollider>();
                c.direction = 1;
                c.radius = radius;
                c.height = Mathf.Max(height, radius * 2f);
            }
            return go;
        }

        /// <summary>Straight pipe between two points with flanges every ~3 m.</summary>
        public void Pipe(Transform parent, Vector3 from, Vector3 to, float radius, Material mat)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            var rot = Quaternion.FromToRotation(Vector3.up, d.normalized);
            var pipe = Cylinder(parent, "Pipe", (from + to) * 0.5f, radius, len, mat, rot, collider: false);
            pipe.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
            int flanges = Mathf.Max(1, Mathf.FloorToInt(len / 3f));
            for (int i = 1; i < flanges; i++)
            {
                Cylinder(parent, "Flange", from + d * (i / (float)flanges), radius * 1.45f, 0.06f, M.SteelTrim, rot, collider: false);
            }
        }

        // ------------------------------------------------------------------ rooms

        /// <summary>Floor, ceiling and walls (two-tone with trims) around the rect, leaving the given openings.</summary>
        public Transform Room(Transform parent, string name, RoomRect r, Material floor, Material upper, Material lower, Material ceiling, params Opening[] openings)
        {
            var root = Group(parent, name);
            var arch = Group(root, "Architecture");
            float w = WallThickness;

            Box(arch, "Floor", new Vector3((r.X0 + r.X1) * 0.5f, -0.1f, (r.Z0 + r.Z1) * 0.5f), new Vector3(r.X1 - r.X0 + 2 * w, 0.2f, r.Z1 - r.Z0 + 2 * w), floor);
            Box(arch, "Ceiling", new Vector3((r.X0 + r.X1) * 0.5f, r.Height + 0.1f, (r.Z0 + r.Z1) * 0.5f), new Vector3(r.X1 - r.X0 + 2 * w, 0.2f, r.Z1 - r.Z0 + 2 * w), ceiling);

            Wall(arch, r, Side.North, upper, lower, openings);
            Wall(arch, r, Side.South, upper, lower, openings);
            Wall(arch, r, Side.East, upper, lower, openings);
            Wall(arch, r, Side.West, upper, lower, openings);
            return root;
        }

        private void Wall(Transform parent, RoomRect r, Side side, Material upper, Material lower, Opening[] openings)
        {
            bool alongX = side == Side.North || side == Side.South;
            float start = alongX ? r.X0 - WallThickness : r.Z0;
            float end = alongX ? r.X1 + WallThickness : r.Z1;
            float fixedCoord = side switch
            {
                Side.North => r.Z1 + WallThickness * 0.5f,
                Side.South => r.Z0 - WallThickness * 0.5f,
                Side.East => r.X1 + WallThickness * 0.5f,
                _ => r.X0 - WallThickness * 0.5f,
            };
            // Inner face direction (towards the room) for trims.
            float inward = side == Side.North || side == Side.East ? -1f : 1f;

            var cuts = new List<Opening>();
            foreach (var o in openings) if (o.Side == side) cuts.Add(o);
            cuts.Sort((a, b) => a.Center.CompareTo(b.Center));

            float cursor = start;
            foreach (var o in cuts)
            {
                float a = o.Center - o.Width * 0.5f, b = o.Center + o.Width * 0.5f;
                WallSegment(parent, side, alongX, fixedCoord, inward, cursor, a, 0f, r.Height, upper, lower, true);
                WallSegment(parent, side, alongX, fixedCoord, inward, a, b, o.Height, r.Height, upper, lower, false);
                if (o.Bottom > 0f) WallSegment(parent, side, alongX, fixedCoord, inward, a, b, 0f, o.Bottom, upper, lower, true);
                cursor = b;
            }
            WallSegment(parent, side, alongX, fixedCoord, inward, cursor, end, 0f, r.Height, upper, lower, true);
        }

        private void WallSegment(Transform parent, Side side, bool alongX, float fixedCoord, float inward, float a, float b, float y0, float y1, Material upper, Material lower, bool trims)
        {
            float len = b - a;
            if (len < 0.01f || y1 - y0 < 0.01f) return;
            float mid = (a + b) * 0.5f;

            void Piece(string n, float py0, float py1, Material m)
            {
                if (py1 - py0 < 0.01f) return;
                Vector3 c = alongX ? new Vector3(mid, (py0 + py1) * 0.5f, fixedCoord) : new Vector3(fixedCoord, (py0 + py1) * 0.5f, mid);
                Vector3 s = alongX ? new Vector3(len, py1 - py0, WallThickness) : new Vector3(WallThickness, py1 - py0, len);
                Box(parent, n, c, s, m);
            }

            float split = Mathf.Clamp(LowerWallHeight, y0, y1);
            Piece($"Wall_{side}_Lower", y0, split, lower);
            Piece($"Wall_{side}_Upper", split, y1, upper);

            if (!trims || y0 > 0f) return;
            float face = fixedCoord + inward * (WallThickness * 0.5f);
            void Trim(string n, float y, float h, float depth, Material m, bool collide)
            {
                Vector3 c = alongX ? new Vector3(mid, y, face + inward * depth * 0.5f) : new Vector3(face + inward * depth * 0.5f, y, mid);
                Vector3 s = alongX ? new Vector3(len, h, depth) : new Vector3(depth, h, len);
                Box(parent, n, c, s, m, collide);
            }
            Trim("Baseboard", 0.07f, 0.14f, 0.05f, M.Rubber, true);
            if (y1 > LowerWallHeight + 0.05f) Trim("Trim_Mid", LowerWallHeight, 0.06f, 0.035f, M.SteelTrim, false);
        }

        /// <summary>Emissive strip on an inner wall face (corridor light lines).</summary>
        public void WallStrip(Transform parent, RoomRect r, Side side, float y, Material mat, float margin = 1.6f)
        {
            bool alongX = side == Side.North || side == Side.South;
            float a = (alongX ? r.X0 : r.Z0) + margin, b = (alongX ? r.X1 : r.Z1) - margin;
            float face = side switch { Side.North => r.Z1, Side.South => r.Z0, Side.East => r.X1, _ => r.X0 };
            float inward = side == Side.North || side == Side.East ? -1f : 1f;
            Vector3 c = alongX ? new Vector3((a + b) * 0.5f, y, face + inward * 0.02f) : new Vector3(face + inward * 0.02f, y, (a + b) * 0.5f);
            Vector3 s = alongX ? new Vector3(b - a, 0.04f, 0.04f) : new Vector3(0.04f, 0.04f, b - a);
            Box(parent, "LightStrip", c, s, mat, collider: false);
        }

        public void FloorStripe(Transform parent, Vector3 center, Vector3 size)
        {
            Box(parent, "HazardStripe", center + Vector3.up * 0.003f, new Vector3(size.x, 0.006f, size.z), M.Hazard, collider: false);
        }

        // ------------------------------------------------------------------ lighting

        public Light SpotDown(Transform parent, string name, Vector3 pos, float lumens, float angle, float range, Color color, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.AddHDLight(LightType.Spot);
            var l = go.GetComponent<Light>();
            // Reflector on: the lumens are concentrated in the cone instead of spread over a full sphere.
            l.enableSpotReflector = true;
            l.lightUnit = LightUnit.Lumen;
            l.intensity = lumens * LightScale;
            l.color = color;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.45f;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            Register(l, shadows);
            return l;
        }

        public Light Point(Transform parent, string name, Vector3 pos, float lumens, float range, Color color, bool shadows = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddHDLight(LightType.Point);
            var l = go.GetComponent<Light>();
            l.lightUnit = LightUnit.Lumen;
            l.intensity = lumens * LightScale;
            l.color = color;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            Register(l, shadows);
            return l;
        }

        private void Register(Light l, bool shadows)
        {
            AllLights.Add(l);
            if (shadows) ShadowedLights.Add(l);
        }

        /// <summary>Ceiling fixture: emissive panel plus a downward spot light.</summary>
        public Light CeilingLight(Transform parent, Vector3 floorPos, float ceilingY, float lumens, bool shadows, bool flicker = false, float panelLength = 1.4f, bool warm = false)
        {
            var g = Group(parent, "CeilingLight");
            g.localPosition = new Vector3(floorPos.x, 0f, floorPos.z);
            Box(g, "Housing", new Vector3(0f, ceilingY - 0.04f, 0f), new Vector3(panelLength + 0.12f, 0.08f, 0.42f), M.Graphite_, collider: false);
            var panel = Box(g, "Panel", new Vector3(0f, ceilingY - 0.085f, 0f), new Vector3(panelLength, 0.012f, 0.3f), warm ? M.LightPanelWarm : M.LightPanel, collider: false);
            panel.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var color = warm ? new Color(1f, 0.88f, 0.75f) : new Color(0.88f, 0.94f, 1f);
            var l = SpotDown(g, "Spot", new Vector3(0f, ceilingY - 0.12f, 0f), lumens, 125f, Mathf.Max(10f, ceilingY * 2.6f), color, shadows);
            if (flicker)
            {
                var f = g.gameObject.AddComponent<FlickerLight>();
                SetRef(f, "targetLight", l);
                SetFloat(f, "baseIntensity", lumens);
            }
            return l;
        }

        /// <summary>Red emergency beacon. Rotating beacons sweep a spot light; pulse beacons breathe a point light.</summary>
        public EmergencyLight Beacon(Transform parent, Vector3 pos, bool rotate, Vector3 mountNormal)
        {
            var g = Group(parent, rotate ? "Beacon_Rotating" : "Beacon_Pulse");
            g.localPosition = pos;
            g.localRotation = Quaternion.FromToRotation(Vector3.up, -mountNormal);
            Cylinder(g, "Base", Vector3.zero, 0.12f, 0.06f, M.Graphite_, Quaternion.identity, collider: false);
            var dome = Cylinder(g, "Dome", new Vector3(0f, -0.1f, 0f), 0.09f, 0.16f, M.BeaconRed, Quaternion.identity, collider: false);
            dome.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var red = new Color(1f, 0.06f, 0.03f);
            Light l;
            Transform rotor = null;
            if (rotate)
            {
                rotor = Group(g, "Rotor");
                rotor.localPosition = new Vector3(0f, -0.1f, 0f);
                var lgo = new GameObject("Spot");
                lgo.transform.SetParent(rotor, false);
                lgo.transform.localRotation = Quaternion.Euler(10f, 0f, 0f) * Quaternion.LookRotation(Vector3.forward);
                lgo.AddHDLight(LightType.Spot);
                l = lgo.GetComponent<Light>();
                l.enableSpotReflector = true;
                l.lightUnit = LightUnit.Lumen;
                l.color = red;
                l.spotAngle = 70f;
                l.innerSpotAngle = 25f;
                l.range = 14f;
                l.shadows = LightShadows.None;
                Register(l, false);
            }
            else
            {
                l = Point(g, "Point", new Vector3(0f, -0.25f, 0f), 250f, 7f, red);
            }

            var e = g.gameObject.AddComponent<EmergencyLight>();
            SetRef(e, "targetLight", l);
            if (rotor != null) SetRef(e, "rotor", rotor);
            SetEnum(e, "alarmMode", rotate ? (int)EmergencyLightMode.Rotate : (int)EmergencyLightMode.Pulse);
            SetFloat(e, "alarmIntensity", rotate ? 2600f : 900f);
            SetFloat(e, "idleIntensity", rotate ? 0f : 120f);
            l.intensity = rotate ? 0f : 120f;
            return e;
        }

        public AlarmSystem Alarm(Transform parent, string name, EmergencyLight[] lights, Vector3 sirenPos, bool active)
        {
            var g = Group(parent, name);
            var siren = Audio(g, "Siren", sirenPos, ProceduralAudioClips.Alarm(), 0.5f, 25f, playOnAwake: false);
            var alarm = g.gameObject.AddComponent<AlarmSystem>();
            var so = new SerializedObject(alarm);
            var lp = so.FindProperty("lights");
            lp.arraySize = lights.Length;
            for (int i = 0; i < lights.Length; i++) lp.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
            var sp = so.FindProperty("sirens");
            sp.arraySize = 1;
            sp.GetArrayElementAtIndex(0).objectReferenceValue = siren;
            so.FindProperty("alarmOnStart").boolValue = active;
            so.FindProperty("sirenWithAlarm").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return alarm;
        }

        public void ReflectionProbe(Transform parent, RoomRect r)
        {
            var go = new GameObject("ReflectionProbe");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = r.Center + Vector3.up * Mathf.Min(1.7f, r.Height * 0.5f);
            var probe = go.AddComponent<ReflectionProbe>();
            if (!go.TryGetComponent(out HDAdditionalReflectionData hd)) hd = go.AddComponent<HDAdditionalReflectionData>();
            hd.mode = ProbeSettings.Mode.Realtime;
            hd.realtimeMode = ProbeSettings.RealtimeMode.OnEnable;
            var size = new Vector3(r.X1 - r.X0, r.Height, r.Z1 - r.Z0);
            hd.influenceVolume.shape = InfluenceShape.Box;
            hd.influenceVolume.boxSize = size;
            hd.influenceVolume.boxBlendDistancePositive = Vector3.one * 0.5f;
            hd.influenceVolume.boxBlendDistanceNegative = Vector3.one * 0.5f;
            probe.size = size;
            go.transform.localPosition = new Vector3(r.Center.x, r.Height * 0.5f, r.Center.z);
        }

        // ------------------------------------------------------------------ doors

        /// <summary>Sliding security door centred on a doorway. Yaw 0 = wall along X (door faces Z).</summary>
        public AutoDoor Door(Transform parent, string name, Vector3 center, float yaw, bool locked, float depth = 0.6f)
        {
            var g = Group(parent, name);
            g.localPosition = center;
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);

            const float clear = 2.2f, height = 2.6f, jamb = 0.2f;
            Box(g, "Jamb_L", new Vector3(-clear * 0.5f - jamb * 0.5f, 1.45f, 0f), new Vector3(jamb, 2.9f, depth + 0.1f), M.Graphite_);
            Box(g, "Jamb_R", new Vector3(clear * 0.5f + jamb * 0.5f, 1.45f, 0f), new Vector3(jamb, 2.9f, depth + 0.1f), M.Graphite_);
            Box(g, "Header", new Vector3(0f, height + 0.15f, 0f), new Vector3(clear, 0.3f, depth + 0.1f), M.Graphite_);
            FloorStripe(g, new Vector3(0f, 0f, depth * 0.5f + 0.35f), new Vector3(clear, 0f, 0.5f));
            FloorStripe(g, new Vector3(0f, 0f, -depth * 0.5f - 0.35f), new Vector3(clear, 0f, 0.5f));

            var obstacles = new List<UnityEngine.AI.NavMeshObstacle>();
            var left = Group(g, "Leaf_L");
            left.localPosition = new Vector3(-clear * 0.25f, 0f, 0f);
            var right = Group(g, "Leaf_R");
            right.localPosition = new Vector3(clear * 0.25f, 0f, 0f);
            foreach (var leaf in new[] { left, right })
            {
                var panel = Box(leaf, "Panel", new Vector3(0f, height * 0.5f, 0f), new Vector3(clear * 0.5f, height, 0.12f), M.DoorPanel);
                Box(leaf, "Stripe", new Vector3(0f, 1.1f, 0f), new Vector3(clear * 0.5f, 0.25f, 0.13f), M.Hazard, collider: false);
                Box(leaf, "Window", new Vector3(0f, 1.85f, 0f), new Vector3(0.25f, 0.5f, 0.125f), M.Graphite_, collider: false);
                // Unlocked leaves move at runtime, so they are left out of the baked NavMesh; locked leaves stay in and block it.
                var mod = panel.AddComponent<NavMeshModifier>();
                mod.ignoreFromBuild = !locked;
                if (!locked)
                {
                    // Doors that can be locked at runtime (mission) carve the NavMesh while locked.
                    var obstacle = panel.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                    obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                    obstacle.size = new Vector3(clear * 0.5f, height, 0.4f);
                    obstacle.center = Vector3.zero;
                    obstacle.carving = true;
                    obstacle.carveOnlyStationary = false;
                    obstacles.Add(obstacle);
                }
            }

            var status = new List<Renderer>();
            foreach (float z in new[] { depth * 0.5f + 0.06f, -depth * 0.5f - 0.06f })
            {
                var s = Box(g, "StatusLight", new Vector3(0f, height + 0.15f, z), new Vector3(1.2f, 0.06f, 0.02f), locked ? M.StatusRed : M.StatusBlue, collider: false);
                status.Add(s.GetComponent<Renderer>());
            }

            var audio = Audio(g, "Audio", new Vector3(0f, 2f, 0f), null, 0.8f, 18f, playOnAwake: false);

            var door = g.gameObject.AddComponent<AutoDoor>();
            var so = new SerializedObject(door);
            so.FindProperty("leftLeaf").objectReferenceValue = left;
            so.FindProperty("rightLeaf").objectReferenceValue = right;
            so.FindProperty("slideDistance").floatValue = clear * 0.5f + 0.05f;
            so.FindProperty("locked").boolValue = locked;
            var sr = so.FindProperty("statusRenderers");
            sr.arraySize = status.Count;
            for (int i = 0; i < status.Count; i++) sr.GetArrayElementAtIndex(i).objectReferenceValue = status[i];
            so.FindProperty("unlockedMaterial").objectReferenceValue = M.StatusBlue;
            so.FindProperty("lockedMaterial").objectReferenceValue = M.StatusRed;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.FindProperty("openClip").objectReferenceValue = _doorClip;
            so.FindProperty("lockedClip").objectReferenceValue = _deniedClip;
            var op = so.FindProperty("obstacles");
            op.arraySize = obstacles.Count;
            for (int i = 0; i < obstacles.Count; i++) op.GetArrayElementAtIndex(i).objectReferenceValue = obstacles[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return door;
        }

        // ------------------------------------------------------------------ props

        /// <summary>Desk terminal: pedestal, angled monitor with a scrolling emissive screen, keyboard.</summary>
        public Transform Terminal(Transform parent, Vector3 pos, float yaw, bool alert)
        {
            var g = Group(parent, "Terminal");
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box(g, "Pedestal", new Vector3(0f, 0.5f, 0f), new Vector3(0.9f, 1.0f, 0.55f), M.Graphite_);
            Box(g, "Desk", new Vector3(0f, 1.03f, -0.05f), new Vector3(1.1f, 0.06f, 0.7f), M.SteelTrim);
            var mon = Group(g, "Monitor");
            mon.localPosition = new Vector3(0f, 1.42f, 0.18f);
            mon.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            Box(mon, "Bezel", Vector3.zero, new Vector3(0.95f, 0.6f, 0.06f), M.Graphite_, collider: false);
            Screen(mon, new Vector3(0f, 0f, -0.032f), new Vector2(0.86f, 0.5f), alert);
            Box(g, "Keyboard", new Vector3(0f, 1.075f, -0.2f), new Vector3(0.6f, 0.025f, 0.2f), M.Graphite_, collider: false);
            return g;
        }

        public void Screen(Transform parent, Vector3 localPos, Vector2 size, bool alert, float yaw = 0f)
        {
            var s = Box(parent, alert ? "Screen_Alert" : "Screen", localPos, new Vector3(size.x, size.y, 0.005f), alert ? M.ScreenRed : M.ScreenBlue, collider: false, yaw: yaw);
            s.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            s.AddComponent<ScreenScroller>();
        }

        /// <summary>Large wall display with frame, facing the given yaw.</summary>
        public void WallScreen(Transform parent, Vector3 pos, float yaw, Vector2 size, bool alert)
        {
            var g = Group(parent, "WallScreen");
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box(g, "Frame", Vector3.zero, new Vector3(size.x + 0.12f, size.y + 0.12f, 0.08f), M.Graphite_, collider: false);
            Screen(g, new Vector3(0f, 0f, -0.045f), size, alert);
        }

        public void Crate(Transform parent, Vector3 basePos, float size, float yaw)
        {
            var g = Group(parent, "Crate");
            g.localPosition = basePos;
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box(g, "Body", new Vector3(0f, size * 0.5f, 0f), new Vector3(size, size * 0.96f, size), M.Crate);
            Box(g, "Band", new Vector3(0f, size * 0.5f, 0f), new Vector3(size + 0.02f, 0.08f, size + 0.02f), M.SteelTrim, collider: false);
            Box(g, "Lid", new Vector3(0f, size - 0.01f, 0f), new Vector3(size + 0.03f, 0.04f, size + 0.03f), M.Graphite_, collider: false);
        }

        public void Barrier(Transform parent, Vector3 basePos, float yaw, float length = 2.2f, float height = 1.05f)
        {
            var g = Group(parent, "ConcreteBarrier");
            g.localPosition = basePos;
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box(g, "Foot", new Vector3(0f, 0.18f, 0f), new Vector3(length, 0.36f, 0.62f), M.Concrete);
            Box(g, "Body", new Vector3(0f, 0.36f + (height - 0.36f) * 0.5f, 0f), new Vector3(length, height - 0.36f, 0.32f), M.Concrete);
            Box(g, "Stripe", new Vector3(0f, height - 0.18f, 0f), new Vector3(length + 0.005f, 0.12f, 0.325f), M.Hazard, collider: false);
        }

        public void Container(Transform parent, Vector3 basePos, float yaw, Material shell)
        {
            var g = Group(parent, "ShippingContainer");
            g.localPosition = basePos;
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box(g, "Shell", new Vector3(0f, 1.3f, 0f), new Vector3(6.06f, 2.6f, 2.44f), shell);
            foreach (float x in new[] { -2.98f, 2.98f })
            foreach (float z in new[] { -1.17f, 1.17f })
                Box(g, "CornerPost", new Vector3(x, 1.3f, z), new Vector3(0.14f, 2.62f, 0.14f), M.Graphite_, collider: false);
            for (int i = -5; i <= 5; i++) Box(g, "Rib", new Vector3(i * 0.55f, 1.3f, 1.235f), new Vector3(0.05f, 2.4f, 0.04f), shell, collider: false);
            for (int i = -5; i <= 5; i++) Box(g, "Rib", new Vector3(i * 0.55f, 1.3f, -1.235f), new Vector3(0.05f, 2.4f, 0.04f), shell, collider: false);
        }

        /// <summary>Glowing specimen / containment tank with caps and bars. Broken tanks have no liquid.</summary>
        public void Tank(Transform parent, Vector3 basePos, float radius, float height, Material liquid, bool broken = false)
        {
            var g = Group(parent, broken ? "Tank_Broken" : "Tank");
            g.localPosition = basePos;
            Cylinder(g, "BaseCap", new Vector3(0f, 0.2f, 0f), radius + 0.25f, 0.4f, M.Graphite_, Quaternion.identity);
            Cylinder(g, "TopCap", new Vector3(0f, height + 0.3f, 0f), radius + 0.25f, 0.5f, M.Graphite_, Quaternion.identity);
            if (!broken)
            {
                var liq = Cylinder(g, "Liquid", new Vector3(0f, 0.4f + (height - 0.35f) * 0.5f, 0f), radius, height - 0.35f, liquid, Quaternion.identity);
                liq.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            else
            {
                var inner = Cylinder(g, "EmptyCore", new Vector3(0f, 0.55f, 0f), radius * 0.95f, 0.3f, M.Graphite_, Quaternion.identity);
                inner.name = "Spill";
            }
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (radius + 0.12f);
                float barH = broken && i % 2 == 0 ? height * 0.4f : height;
                Cylinder(g, "Bar", p + Vector3.up * (0.4f + barH * 0.5f), 0.04f, barH, M.SteelTrim, Quaternion.identity, collider: false);
            }
            Pipe(g, new Vector3(radius * 0.5f, height + 0.55f, 0f), new Vector3(radius * 0.5f, height + 3f, 0f), 0.07f, M.Pipe);
        }

        /// <summary>Organic alien growth: a cluster of squashed blobs.</summary>
        public void Biomass(Transform parent, Vector3 pos, float scale, int blobs)
        {
            var g = Group(parent, "AlienBiomass");
            g.localPosition = pos;
            for (int i = 0; i < blobs; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                b.name = "Blob";
                Object.DestroyImmediate(b.GetComponent<SphereCollider>());
                b.transform.SetParent(g, false);
                b.transform.localPosition = new Vector3(Rand(-1f, 1f), Rand(-0.1f, 0.2f), Rand(-1f, 1f)) * scale;
                b.transform.localScale = new Vector3(Rand(0.6f, 1.3f), Rand(0.25f, 0.55f), Rand(0.6f, 1.3f)) * scale;
                b.transform.localRotation = Quaternion.Euler(Rand(-10f, 10f), Rand(0f, 360f), Rand(-10f, 10f));
                b.GetComponent<MeshRenderer>().sharedMaterial = M.Alien;
                b.AddComponent<MeshCollider>();
            }
        }

        // ------------------------------------------------------------------ ambience

        public SparkEmitter Sparks(Transform parent, Vector3 pos, Vector3 normal)
        {
            var g = Group(parent, "Sparks");
            g.localPosition = pos;
            g.localRotation = Quaternion.LookRotation(normal);

            var psGo = new GameObject("Particles");
            psGo.transform.SetParent(g, false);
            var ps = psGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.gravityModifier = 1.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.02f;
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.bounce = 0.35f;
            collision.dampen = 0.4f;
            collision.quality = ParticleSystemCollisionQuality.Low;
            var renderer = psGo.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 3f;
            renderer.velocityScale = 0.04f;
            renderer.sharedMaterial = M.SparkParticle;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var flash = Point(g, "Flash", new Vector3(0f, 0f, 0.15f), 0f, 5f, new Color(1f, 0.7f, 0.35f));
            var audio = Audio(g, "Audio", Vector3.zero, null, 0.7f, 14f, playOnAwake: false);

            var e = g.gameObject.AddComponent<SparkEmitter>();
            var so = new SerializedObject(e);
            so.FindProperty("sparks").objectReferenceValue = ps;
            so.FindProperty("flash").objectReferenceValue = flash;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.FindProperty("crackle").objectReferenceValue = _crackleClip;
            so.ApplyModifiedPropertiesWithoutUndo();
            return e;
        }

        /// <summary>Local volumetric smoke/haze using a scrolling 3D noise mask (needs volumetric fog enabled).</summary>
        public LocalVolumetricFog Smoke(Transform parent, Vector3 center, Vector3 size, float meanFreePath, Color albedo)
        {
            var go = new GameObject("Smoke");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var fog = go.AddComponent<LocalVolumetricFog>();
            fog.parameters.size = size;
            fog.parameters.meanFreePath = meanFreePath;
            fog.parameters.albedo = albedo;
            fog.parameters.volumeMask = _smokeMask;
            fog.parameters.textureTiling = new Vector3(size.x / 4f, size.y / 4f, size.z / 4f);
            fog.parameters.textureScrollingSpeed = new Vector3(0.02f, 0.01f, 0.015f);
            fog.parameters.positiveFade = Vector3.one * 0.3f;
            fog.parameters.negativeFade = Vector3.one * 0.3f;
            fog.parameters.distanceFadeStart = 30f;
            fog.parameters.distanceFadeEnd = 45f;
            return fog;
        }

        public void Decal(Transform parent, Material mat, Vector3 pos, Vector3 normal, float size, float yaw)
        {
            var go = new GameObject("Decal");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos + normal * 0.25f;
            go.transform.localRotation = Quaternion.LookRotation(-normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up) * Quaternion.Euler(0f, 0f, yaw);
            var d = go.AddComponent<DecalProjector>();
            d.material = mat;
            d.size = new Vector3(size, size, 0.5f);
            d.pivot = new Vector3(0f, 0f, 0.25f);
            d.drawDistance = 40f;
        }

        public AudioSource Audio(Transform parent, string name, Vector3 pos, AudioClip clip, float volume, float maxDistance, bool playOnAwake = true, bool spatial = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.volume = volume;
            a.loop = playOnAwake;
            a.playOnAwake = playOnAwake && clip != null;
            a.spatialBlend = spatial ? 1f : 0f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 1.5f;
            a.maxDistance = maxDistance;
            a.dopplerLevel = 0f;
            return a;
        }

        // ------------------------------------------------------------------ serialization helpers

        public static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetEnum(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
