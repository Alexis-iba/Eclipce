using System.Collections;
using System.Linq;
using EclipseGenesis.Player;
using EclipseGenesis.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EclipseGenesis.Tests
{
    /// <summary>
    /// Plays Genesis_Research_Facility with simulated input: the map must be traversable, doors must work,
    /// and the FPS controller must behave as in the test scene. Any logged error fails the test.
    /// </summary>
    public sealed class FacilityPlayTests : InputTestFixture
    {
        private const string SceneName = "Genesis_Research_Facility";
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            // Batch runs lose window focus at random; never let that reset the virtual keyboard mid-test.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        public override void TearDown()
        {
            PlayerLook.SetCursorLocked(false);
            base.TearDown();
        }

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            var scene = SceneManager.GetSceneByName(SceneName);
            if (!scene.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("EmptyAfterFacility"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        private static IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            for (float t = 0f; t < 1f; t += Time.deltaTime) yield return null;
        }

        private static IEnumerator Seconds(float s)
        {
            for (float t = 0f; t < s; t += Time.deltaTime) yield return null;
        }

        [UnityTest]
        public IEnumerator Player_SpawnsGrounded_InTheAccessCorridor()
        {
            yield return Load();
            var player = Object.FindAnyObjectByType<PlayerMovement>();
            Assert.IsNotNull(player);
            Assert.IsTrue(player.IsGrounded, "player must land on the corridor floor");
            Assert.That(player.transform.position.y, Is.EqualTo(0f).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator EveryZone_IsReachable_OnTheNavMesh()
        {
            yield return Load();
            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(0f, 0f, 3f), out var start, 1f, NavMesh.AllAreas), "no NavMesh at spawn");
            var markers = Object.FindObjectsByType<Transform>().Where(t => t.name.StartsWith("Nav_")).ToArray();
            Assert.AreEqual(7, markers.Length);
            var path = new NavMeshPath();
            foreach (var m in markers)
            {
                Assert.IsTrue(NavMesh.SamplePosition(m.position, out var end, 1.5f, NavMesh.AllAreas), $"{m.name} is off the NavMesh");
                Assert.IsTrue(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path), $"no path to {m.name}");
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, $"path to {m.name} is incomplete");
            }
        }

        [UnityTest]
        public IEnumerator Walking_OpensTheDoor_AndEntersTheArmory_WhileSealedDoorStaysShut()
        {
            yield return Load();
            var player = Object.FindAnyObjectByType<PlayerMovement>();
            var doors = Object.FindObjectsByType<AutoDoor>();
            var armoryDoor = doors.Single(d => d.name == "Door_ToArmory");
            var sealedDoor = doors.Single(d => d.name == "Door_BlastSealed");

            float maxOpen = 0f;
            Press(_keyboard.wKey);
            for (float t = 0f; t < 5f; t += Time.deltaTime)
            {
                maxOpen = Mathf.Max(maxOpen, armoryDoor.OpenAmount);
                yield return null;
            }
            Release(_keyboard.wKey);

            Assert.Greater(maxOpen, 0.95f, "the armory door should open fully as the player approaches");
            Assert.Greater(player.transform.position.z, 18f, "the player should have walked through the door into the armory");
            Assert.AreEqual(0f, sealedDoor.OpenAmount, "sealed blast door must never open");
        }

        [UnityTest]
        public IEnumerator FpsSystems_StillWork_InTheFacility()
        {
            yield return Load();
            var player = Object.FindAnyObjectByType<PlayerMovement>();

            Press(_keyboard.wKey);
            yield return null;
            Press(_keyboard.leftShiftKey);
            yield return Seconds(0.8f);
            Assert.IsTrue(player.IsSprinting, "sprint");
            Assert.Greater(player.HorizontalSpeed, 6f);

            Press(_keyboard.leftCtrlKey);
            yield return Seconds(0.15f);
            Assert.IsTrue(player.IsSliding, "slide");
            Release(_keyboard.leftCtrlKey);
            Release(_keyboard.leftShiftKey);
            Release(_keyboard.wKey);
            yield return Seconds(1.5f);
            Assert.IsFalse(player.IsSliding);
        }

        [UnityTest]
        public IEnumerator Ambience_Systems_Run()
        {
            yield return Load();
            var alarms = Object.FindObjectsByType<AlarmSystem>();
            Assert.GreaterOrEqual(alarms.Length, 4);
            Assert.IsTrue(alarms.Any(a => a.IsActive), "at least one area should start in alarm");

            var sparks = Object.FindObjectsByType<SparkEmitter>();
            Assert.GreaterOrEqual(sparks.Length, 3);
            foreach (var s in sparks) s.Burst();
            yield return Seconds(0.3f);
            Assert.IsTrue(sparks.All(s => s.BurstsFired > 0));

            var flicker = Object.FindObjectsByType<FlickerLight>();
            Assert.Greater(flicker.Length, 0);
        }
    }
}
