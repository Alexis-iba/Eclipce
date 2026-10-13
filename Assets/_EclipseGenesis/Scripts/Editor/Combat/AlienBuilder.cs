using System.IO;
using System.Linq;
using EclipseGenesis.AI;
using EclipseGenesis.Combat;
using EclipseGenesis.EditorTools.Facility;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace EclipseGenesis.EditorTools.Combat
{
    /// <summary>
    /// Builds the provisional Stalker: an original quadruped predator assembled from primitives (dark bio-armour
    /// plates, elongated head with four bioluminescent eyes, four jointed legs, front claws), plus its AI components.
    /// Stand-in for an authored, rigged model (Docs/ASSETS_REQUIRED.md).
    /// </summary>
    public static class AlienBuilder
    {
        public const string PrefabPath = "Assets/_EclipseGenesis/Prefabs/Aliens/EG_Alien_Stalker.prefab";
        public const string DataPath = "Assets/_EclipseGenesis/Data/AI/Stalker.asset";

        public static GameObject Build()
        {
            var carapace = ProtoKit.Lit("M_Stalker_Carapace", new Color(0.035f, 0.032f, 0.045f), 0.25f, 0.78f);
            var plates = ProtoKit.Lit("M_Stalker_ArmorPlates", new Color(0.06f, 0.05f, 0.075f), 0.35f, 0.85f);
            var flesh = AssetDatabase.LoadAssetAtPath<Material>("Assets/_EclipseGenesis/Materials/Facility/M_Alien_Biomass.mat")
                        ?? ProtoKit.Lit("M_Stalker_Flesh", new Color(0.12f, 0.04f, 0.14f), 0f, 0.6f);
            var bone = ProtoKit.Lit("M_Stalker_Claw", new Color(0.16f, 0.15f, 0.17f), 0.1f, 0.7f);
            var eyes = ProtoKit.Glow("M_Stalker_Eyes", new Color(0.25f, 0.95f, 1f), 1800f);
            var vein = ProtoKit.Glow("M_Stalker_Veins", new Color(0.55f, 0.2f, 1f), 120f);

            var data = AssetDatabase.LoadAssetAtPath<AlienData>(DataPath);
            if (data == null)
            {
                FpsTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(DataPath));
                data = ScriptableObject.CreateInstance<AlienData>();
                AssetDatabase.CreateAsset(data, DataPath);
            }
            data.speciesName = "Stalker";
            data.screechClip = ProceduralAudioClips.AlienScreechV2();
            data.attackClip = ProceduralAudioClips.AlienAttackV2();
            data.hurtClip = ProceduralAudioClips.AlienHurtV2();
            data.deathClip = ProceduralAudioClips.AlienDeathV2();
            EditorUtility.SetDirty(data);

            var root = new GameObject("EG_Alien_Stalker");
            try
            {
                var t = root.transform;
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.45f;
                agent.height = 1.3f;
                agent.baseOffset = 0f;
                agent.speed = data.patrolSpeed;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

                var health = root.AddComponent<AlienHealth>();
                ProtoKit.SetRef(health, "data", data);

                if (AssetDatabase.LoadAssetAtPath<GameObject>(OrganicModelPath) != null)
                {
                    BuildOrganic(root, data, health);
                    FpsTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(PrefabPath));
                    var organicPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                    AssetDatabase.SaveAssets();
                    return organicPrefab;
                }

                // ---------------- body
                var torso = ProtoKit.Group(t, "Torso", new Vector3(0f, 0.78f, 0f));
                ProtoKit.Part(torso, "Thorax", PrimitiveType.Capsule, new Vector3(0f, 0f, 0.05f), new Vector3(0.52f, 1.15f, 0.5f), carapace, new Vector3(90f, 0f, 0f));
                ProtoKit.Part(torso, "Abdomen", PrimitiveType.Sphere, new Vector3(0f, 0.04f, -0.58f), new Vector3(0.5f, 0.42f, 0.62f), flesh);
                for (int i = 0; i < 4; i++)
                {
                    float z = 0.42f - i * 0.28f;
                    ProtoKit.Part(torso, "BackPlate", PrimitiveType.Sphere, new Vector3(0f, 0.2f, z), new Vector3(0.5f - i * 0.03f, 0.14f, 0.32f), plates, new Vector3(-8f, 0f, 0f));
                    ProtoKit.Part(torso, "Ridge", PrimitiveType.Capsule, new Vector3(0f, 0.29f, z), new Vector3(0.05f, 0.18f, 0.05f), bone, new Vector3(-30f, 0f, 0f));
                }
                foreach (float x in new[] { -0.24f, 0.24f })
                {
                    ProtoKit.Part(torso, "SidePlate", PrimitiveType.Sphere, new Vector3(x, 0.02f, 0.1f), new Vector3(0.12f, 0.3f, 0.7f), plates);
                    ProtoKit.Part(torso, "Vein", PrimitiveType.Capsule, new Vector3(x * 1.05f, -0.06f, -0.25f), new Vector3(0.02f, 0.5f, 0.02f), vein, new Vector3(90f, 0f, 0f), false);
                }

                // ---------------- head
                var neck = ProtoKit.Group(torso, "Neck", new Vector3(0f, 0.08f, 0.62f));
                var head = ProtoKit.Group(neck, "Head", new Vector3(0f, 0.04f, 0.12f));
                ProtoKit.Part(head, "Skull", PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0.16f), new Vector3(0.3f, 0.24f, 0.52f), carapace);
                ProtoKit.Part(head, "Crest", PrimitiveType.Sphere, new Vector3(0f, 0.13f, 0.05f), new Vector3(0.2f, 0.09f, 0.42f), plates, new Vector3(-12f, 0f, 0f));
                ProtoKit.Part(head, "Jaw", PrimitiveType.Sphere, new Vector3(0f, -0.09f, 0.2f), new Vector3(0.22f, 0.09f, 0.38f), flesh);
                foreach (float x in new[] { -0.07f, 0.07f })
                {
                    ProtoKit.Part(head, "Mandible", PrimitiveType.Capsule, new Vector3(x, -0.11f, 0.42f), new Vector3(0.025f, 0.16f, 0.025f), bone, new Vector3(100f, x > 0 ? -10f : 10f, 0f));
                }
                var eyeRenderers = new System.Collections.Generic.List<Renderer>();
                foreach (var p in new[] { new Vector3(-0.08f, 0.06f, 0.36f), new Vector3(0.08f, 0.06f, 0.36f), new Vector3(-0.115f, 0.03f, 0.29f), new Vector3(0.115f, 0.03f, 0.29f) })
                    eyeRenderers.Add(ProtoKit.Part(head, "Eye", PrimitiveType.Sphere, p, new Vector3(0.045f, 0.03f, 0.045f), eyes, default, false).GetComponent<Renderer>());
                var eyeLightGo = new GameObject("EyeGlow");
                eyeLightGo.transform.SetParent(head, false);
                eyeLightGo.transform.localPosition = new Vector3(0f, 0.05f, 0.45f);
                eyeLightGo.AddHDLight(LightType.Point);
                var eyeLight = eyeLightGo.GetComponent<Light>();
                eyeLight.lightUnit = LightUnit.Lumen;
                eyeLight.intensity = 160f;
                eyeLight.range = 3f;
                eyeLight.color = new Color(0.3f, 0.95f, 1f);
                eyeLight.shadows = LightShadows.None;
                var headBox = ProtoKit.Group(head, "Hitbox_Head", new Vector3(0f, 0f, 0.18f));
                var headCol = headBox.gameObject.AddComponent<SphereCollider>();
                headCol.radius = 0.2f;
                headBox.gameObject.AddComponent<Hitbox>().Configure(HitZone.Head, 2.2f, health);

                // ---------------- legs (FL, FR, BL, BR); front legs end in claws
                var hips = new Transform[4];
                Transform clawL = null, clawR = null;
                var hipPos = new[] { new Vector3(-0.24f, -0.06f, 0.36f), new Vector3(0.24f, -0.06f, 0.36f), new Vector3(-0.24f, -0.04f, -0.42f), new Vector3(0.24f, -0.04f, -0.42f) };
                for (int i = 0; i < 4; i++)
                {
                    float side = hipPos[i].x < 0f ? -1f : 1f;
                    bool front = i < 2;
                    var hip = ProtoKit.Group(torso, i switch { 0 => "Leg_FL", 1 => "Leg_FR", 2 => "Leg_BL", _ => "Leg_BR" }, hipPos[i]);
                    hips[i] = hip;
                    var knee = new Vector3(side * 0.32f, 0.18f, front ? 0.12f : -0.1f);
                    ProtoKit.Limb(hip, "Thigh", Vector3.zero, knee, 0.075f, carapace);
                    ProtoKit.Part(hip, "ThighPlate", PrimitiveType.Sphere, knee * 0.5f + new Vector3(0f, 0.05f, 0f), new Vector3(0.14f, 0.1f, 0.24f), plates);
                    var shin = ProtoKit.Group(hip, "Shin", knee);
                    var foot = new Vector3(side * 0.12f, -0.95f, front ? 0.2f : -0.05f);
                    ProtoKit.Limb(shin, "Calf", Vector3.zero, foot, 0.055f, flesh);
                    ProtoKit.Part(shin, "KneeSpike", PrimitiveType.Capsule, new Vector3(0f, 0.08f, -0.02f), new Vector3(0.035f, 0.2f, 0.035f), bone, new Vector3(-35f, 0f, 0f));
                    if (front)
                    {
                        var claw = ProtoKit.Group(shin, "Claw", foot);
                        ProtoKit.Part(claw, "Blade", PrimitiveType.Capsule, new Vector3(0f, 0.02f, 0.16f), new Vector3(0.035f, 0.36f, 0.02f), bone, new Vector3(80f, 0f, 0f));
                        ProtoKit.Part(claw, "Blade2", PrimitiveType.Capsule, new Vector3(side * 0.04f, 0.0f, 0.13f), new Vector3(0.03f, 0.28f, 0.02f), bone, new Vector3(85f, side * 12f, 0f));
                        if (i == 0) clawL = shin; else clawR = shin;
                    }
                    else
                    {
                        ProtoKit.Part(shin, "Foot", PrimitiveType.Sphere, foot, new Vector3(0.1f, 0.05f, 0.16f), bone);
                    }
                    var limbBox = ProtoKit.Group(hip, "Hitbox_Limb", knee * 0.5f);
                    var lc = limbBox.gameObject.AddComponent<CapsuleCollider>();
                    lc.radius = 0.09f;
                    lc.height = 0.5f;
                    lc.direction = 0;
                    limbBox.gameObject.AddComponent<Hitbox>().Configure(HitZone.Limb, 0.7f, health);
                }

                var bodyBox = ProtoKit.Group(torso, "Hitbox_Body", new Vector3(0f, 0.02f, -0.1f));
                var bc = bodyBox.gameObject.AddComponent<CapsuleCollider>();
                bc.direction = 2;
                bc.radius = 0.32f;
                bc.height = 1.5f;
                bodyBox.gameObject.AddComponent<Hitbox>().Configure(HitZone.Body, 1f, health);

                AddComponents(root, data, health, torso, head, eyeLightGo.transform, eyeLight, clawL, clawR, hips, eyeRenderers.ToArray());

                FpsTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(PrefabPath));
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Organic rigged Stalker from Blender (ArtSource/Blender/Aliens/stalker_builder.py).</summary>
        public const string OrganicModelPath = "Assets/_EclipseGenesis/Art/Imported/Aliens/SK_Stalker.fbx";

        /// <summary>
        /// Uses the skinned SK_Stalker: inserts root-aligned pivots above the bones AlienBody drives (so its procedural
        /// gait / attack / death / head-look deform the mesh exactly like the previous rig), and re-creates the
        /// hitboxes, eye light and voice on those pivots.
        /// </summary>
        private static void BuildOrganic(GameObject root, AlienData data, AlienHealth health)
        {
            var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(OrganicModelPath), root.transform);
            model.name = "Stalker_Model";
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            Transform Bone(string n) => model.GetComponentsInChildren<Transform>(true).First(t => t.name == n);
            Transform Pivot(string n)
            {
                var bone = Bone(n);
                var p = new GameObject("Pivot_" + n).transform;
                p.SetParent(bone.parent, false);
                p.position = bone.position;
                p.rotation = root.transform.rotation;
                bone.SetParent(p, true);
                return p;
            }
            var torso = Pivot("Torso");
            var head = Pivot("Head");
            var hips = new[] { Pivot("Leg_FL"), Pivot("Leg_FR"), Pivot("Leg_BL"), Pivot("Leg_BR") };
            var clawL = Pivot("Shin_FL");
            var clawR = Pivot("Shin_FR");
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;   // procedural bones move the mesh beyond its import bounds
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            var eyeRenderers = model.GetComponentsInChildren<Renderer>(true).Where(r => r.name.Contains("Eyes")).ToArray();

            var eyeLightGo = new GameObject("EyeGlow");
            eyeLightGo.transform.SetParent(head, false);
            eyeLightGo.transform.localPosition = new Vector3(0f, 0.02f, 0.3f);
            eyeLightGo.AddHDLight(LightType.Point);
            var eyeLight = eyeLightGo.GetComponent<Light>();
            eyeLight.lightUnit = LightUnit.Lumen;
            eyeLight.intensity = 140f;
            eyeLight.range = 3f;
            eyeLight.color = new Color(1f, 0.18f, 0.08f);
            eyeLight.shadows = LightShadows.None;

            var headBox = ProtoKit.Group(head, "Hitbox_Head", new Vector3(0f, 0f, 0.2f));
            headBox.gameObject.AddComponent<SphereCollider>().radius = 0.2f;
            headBox.gameObject.AddComponent<Hitbox>().Configure(HitZone.Head, 2.2f, health);
            foreach (var hip in hips)
            {
                var limbBox = ProtoKit.Group(hip, "Hitbox_Limb", new Vector3(Mathf.Sign(hip.localPosition.x) * 0.15f, -0.05f, 0.05f));
                var lc = limbBox.gameObject.AddComponent<CapsuleCollider>();
                lc.radius = 0.1f;
                lc.height = 0.5f;
                lc.direction = 0;
                limbBox.gameObject.AddComponent<Hitbox>().Configure(HitZone.Limb, 0.7f, health);
            }
            var bodyBox = ProtoKit.Group(torso, "Hitbox_Body", new Vector3(0f, 0.05f, -0.1f));
            var bc = bodyBox.gameObject.AddComponent<CapsuleCollider>();
            bc.direction = 2;
            bc.radius = 0.34f;
            bc.height = 1.6f;
            bodyBox.gameObject.AddComponent<Hitbox>().Configure(HitZone.Body, 1f, health);

            AddComponents(root, data, health, torso, head, eyeLightGo.transform, eyeLight, clawL, clawR, hips, eyeRenderers);
        }

        private static void AddComponents(GameObject root, AlienData data, AlienHealth health, Transform torso, Transform head, Transform eyes,
            Light eyeLight, Transform clawL, Transform clawR, Transform[] hips, Renderer[] eyeRenderers)
        {
            {
                var voiceGo = new GameObject("Voice");
                voiceGo.transform.SetParent(head, false);
                var voice = voiceGo.AddComponent<AudioSource>();
                voice.spatialBlend = 1f;
                voice.rolloffMode = AudioRolloffMode.Linear;
                voice.minDistance = 2f;
                voice.maxDistance = 30f;
                voice.playOnAwake = false;
                voice.dopplerLevel = 0f;

                var perception = root.AddComponent<AlienPerception>();
                ProtoKit.SetRef(perception, "data", data);
                ProtoKit.SetRef(perception, "eyes", eyes);

                var brain = root.AddComponent<AlienBrain>();
                ProtoKit.SetRef(brain, "data", data);
                ProtoKit.SetRef(brain, "perception", perception);
                ProtoKit.SetRef(brain, "health", health);
                ProtoKit.SetRef(brain, "voice", voice);

                var body = root.AddComponent<AlienBody>();
                ProtoKit.SetRef(body, "brain", brain);
                ProtoKit.SetRef(body, "health", health);
                ProtoKit.SetRef(body, "torso", torso);
                ProtoKit.SetRef(body, "head", head);
                ProtoKit.SetRef(body, "clawLeft", clawL);
                ProtoKit.SetRef(body, "clawRight", clawR);
                ProtoKit.SetRef(body, "eyeLight", eyeLight);
                ProtoKit.SetRefs(body, "legs", hips);
                ProtoKit.SetRefs(body, "eyeRenderers", eyeRenderers);
            }
        }
    }
}
