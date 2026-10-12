using System.IO;
using System.Linq;
using EclipseGenesis.EditorTools;
using EclipseGenesis.EditorTools.Facility;
using EclipseGenesis.Player;
using EclipseGenesis.World;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace EclipseGenesis.Tests
{
    /// <summary>Static validation of Genesis_Research_Facility: references, materials, colliders, doors, lighting, navigation.</summary>
    public sealed class FacilitySceneTests
    {
        [OneTimeSetUp]
        public void OpenScene()
        {
            Assert.IsTrue(File.Exists(FacilitySceneBuilder.ScenePath), "facility scene missing: run Eclipse Genesis/Build/Genesis Research Facility");
            EditorSceneManager.OpenScene(FacilitySceneBuilder.ScenePath, OpenSceneMode.Single);
        }

        private static T[] All<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include);

        [Test]
        public void Scene_HasExactlyOnePlayer_AtSpawn()
        {
            var players = All<PlayerMovement>();
            Assert.AreEqual(1, players.Length);
            Assert.AreEqual("EG_Player", players[0].name);
            Assert.Less(Vector3.Distance(players[0].transform.position, FacilitySceneBuilder.SpawnPosition), 0.01f);
        }

        [Test]
        public void Scene_HasNoMissingScripts_OrUnassignedReferences()
        {
            foreach (var go in All<GameObject>())
                Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go), $"missing script on {go.name}");

            foreach (var behaviour in All<MonoBehaviour>().Where(b => b.GetType().Namespace?.StartsWith("EclipseGenesis") == true))
            {
                var it = new SerializedObject(behaviour).GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.name == "m_Script") continue;
                    if (behaviour is EmergencyLight && it.name == "rotor") continue; // only rotating beacons have a rotor
                    Assert.IsNotNull(it.objectReferenceValue, $"{behaviour.name}/{behaviour.GetType().Name}.{it.propertyPath} is not assigned");
                }
            }
        }

        [Test]
        public void AllRenderers_UseValidHdrpMaterials()
        {
            foreach (var r in All<Renderer>())
            foreach (var m in r.sharedMaterials)
            {
                Assert.IsNotNull(m, $"{r.name} has an empty material slot");
                StringAssert.StartsWith("HDRP/", m.shader.name, $"{r.name} uses non-HDRP shader {m.shader.name}");
            }
        }

        [Test]
        public void Architecture_HasColliders()
        {
            var pieces = All<MeshRenderer>().Where(r => r.transform.parent != null && r.transform.parent.name == "Architecture").ToArray();
            Assert.Greater(pieces.Length, 50);
            foreach (var r in pieces)
            {
                if (r.name.StartsWith("Trim_")) continue; // decorative mouldings only
                Assert.IsTrue(r.TryGetComponent(out Collider _), $"{r.transform.parent.parent.name}/{r.name} has no collider");
            }
        }

        [Test]
        public void Doors_AreWired_AndSealedDoorsAreLocked()
        {
            var doors = All<AutoDoor>();
            Assert.AreEqual(7, doors.Length, "5 interior doors + 2 sealed doors expected");
            foreach (var d in doors)
            {
                bool shouldBeLocked = d.name.Contains("Sealed");
                Assert.AreEqual(shouldBeLocked, d.IsLocked, $"{d.name} lock state");
            }
        }

        [Test]
        public void Lighting_IsWithinBudget_AndPostProcessIsConfigured()
        {
            var lights = All<Light>();
            int shadowed = lights.Count(l => l.shadows != LightShadows.None);
            Assert.LessOrEqual(lights.Length, 70, "light count budget");
            Assert.LessOrEqual(shadowed, 20, "shadow-casting light budget");
            Assert.IsFalse(lights.Any(l => l.type == LightType.Directional), "interior map should not use a sun");

            var volume = All<Volume>().Single(v => v.isGlobal);
            var profile = volume.sharedProfile;
            Assert.IsTrue(profile.TryGet(out Exposure exposure) && exposure.mode.value == ExposureMode.Fixed);
            Assert.IsTrue(profile.TryGet(out Fog fog) && fog.enabled.value && fog.enableVolumetricFog.value, "volumetric fog");
            Assert.IsTrue(profile.TryGet(out ScreenSpaceAmbientOcclusion ao) && ao.intensity.value > 0f, "ambient occlusion");
            Assert.IsTrue(profile.TryGet(out Tonemapping tm) && tm.mode.value == TonemappingMode.ACES, "tonemapping");
            Assert.IsTrue(profile.TryGet(out Bloom bloom) && bloom.intensity.value <= 0.2f, "bloom must stay moderate");
        }

        [Test]
        public void Navigation_IsBaked()
        {
            var surface = All<NavMeshSurface>().Single();
            Assert.IsNotNull(surface.navMeshData, "NavMesh not baked");
            Assert.IsTrue(File.Exists(FacilitySceneBuilder.NavMeshPath));
        }

        [Test]
        public void BuildSettings_OpenFacilityFirst_AndKeepFpsTest()
        {
            var scenes = EditorBuildSettings.scenes.Select(s => s.path).ToArray();
            CollectionAssert.AreEqual(BuildSceneRegistry.Order, scenes);
        }

        [Test]
        public void FpsTestScene_WasBackedUp()
        {
            Assert.IsTrue(Directory.Exists(FacilitySceneBuilder.BackupFolder));
            Assert.IsTrue(Directory.GetFiles(FacilitySceneBuilder.BackupFolder, "EG_FPS_Test_*.unity").Length > 0);
        }
    }
}
