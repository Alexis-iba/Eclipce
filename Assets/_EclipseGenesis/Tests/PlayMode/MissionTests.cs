using System.Collections;
using System.Linq;
using EclipseGenesis.AI;
using EclipseGenesis.Combat;
using EclipseGenesis.Mission;
using EclipseGenesis.Player;
using EclipseGenesis.UI;
using EclipseGenesis.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EclipseGenesis.Tests
{
    /// <summary>
    /// OPERACIÓN PRIMER CONTACTO end to end in the real scene: every objective is reached through the same scene
    /// systems the player uses (volumes, doors, alien deaths, the terminal via the F key), then victory; and defeat.
    /// The player is moved with Teleport to keep the test fast; combat itself is covered by CombatTests.
    /// </summary>
    public sealed class MissionTests : InputTestFixture
    {
        private const string SceneName = "Genesis_Research_Facility";
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
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
            SceneManager.SetActiveScene(SceneManager.CreateScene("EmptyAfterMission"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        private static IEnumerator Seconds(float s)
        {
            for (float t = 0f; t < s; t += Time.deltaTime) yield return null;
        }

        private static IEnumerator MoveTo(PlayerMovement player, Vector3 position, float yaw = 0f)
        {
            player.Teleport(position, Quaternion.Euler(0f, yaw, 0f));
            player.GetComponent<PlayerLook>().AddRotation(Mathf.DeltaAngle(player.GetComponent<PlayerLook>().Yaw, yaw), 0f);
            yield return Seconds(0.3f);
        }

        [UnityTest]
        public IEnumerator Mission_CanBeCompleted_ObjectiveByObjective()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return Seconds(1f);

            var mission = MissionManager.Current;
            Assert.IsNotNull(mission, "MissionManager missing");
            var player = Object.FindAnyObjectByType<PlayerMovement>();
            var health = player.GetComponent<PlayerHealth>();
            var hud = player.GetComponent<HUDController>();
            health.Invulnerable = true; // objective flow only; combat is tested separately
            var doors = Object.FindObjectsByType<AutoDoor>();
            var securityDoor = doors.Single(d => d.name == "Door_ToCombatZone");
            var exitDoor = doors.Single(d => d.name == "Door_Exit");

            Assert.AreEqual(0, mission.ObjectiveIndex);
            StringAssert.Contains("Explora el laboratorio Genesis", hud.ObjectiveText);
            Assert.AreEqual(3, mission.AliensTotal);
            Assert.IsTrue(securityDoor.IsLocked && exitDoor.IsLocked);

            // 1. Explore the lab.
            yield return MoveTo(player, new Vector3(0f, 0.05f, 45f));
            Assert.AreEqual(1, mission.ObjectiveIndex, "entering the lab completes objective 1");
            StringAssert.Contains("Localiza la puerta de seguridad", hud.ObjectiveText);

            // 2. Locate the security door: access is granted.
            yield return MoveTo(player, new Vector3(0f, 0.05f, 63f));
            Assert.AreEqual(2, mission.ObjectiveIndex);
            Assert.IsFalse(securityDoor.IsLocked, "locating the door unlocks it");
            yield return MoveTo(player, new Vector3(0f, 0.05f, 66f)); // walk up to the door (3.2 m trigger)
            yield return Seconds(1.2f);
            Assert.Greater(securityDoor.OpenAmount, 0.9f, "the security door opens for the player");

            // 3. Enter the research room: it seals behind the player.
            yield return MoveTo(player, new Vector3(0f, 0.05f, 77f));
            Assert.AreEqual(3, mission.ObjectiveIndex);
            Assert.IsTrue(securityDoor.IsLocked, "the room locks behind the player");
            StringAssert.Contains("(0/3)", hud.ObjectiveText);

            // 4. Eliminate the three Stalkers (damage goes through the real damage pipeline).
            var aliens = Object.FindObjectsByType<AlienHealth>();
            Assert.AreEqual(3, aliens.Length);
            Assert.IsFalse(aliens.Any(a => Vector3.Distance(a.transform.position, player.transform.position) < 6f), "no alien may spawn on top of the player");
            var terminal = Object.FindAnyObjectByType<Terminal>();
            Assert.IsFalse(terminal.CanInteract, "terminal stays locked while Stalkers live");
            for (int i = 0; i < aliens.Length; i++)
            {
                aliens[i].ApplyDamage(new DamageInfo(1000f, aliens[i].transform.position, Vector3.forward, player.gameObject));
                yield return null;
                if (i < aliens.Length - 1) StringAssert.Contains($"({i + 1}/3)", hud.ObjectiveText);
            }
            Assert.AreEqual(4, mission.ObjectiveIndex);
            Assert.IsTrue(aliens.All(a => a.GetComponent<AlienBrain>().State == AlienState.Dead));

            // 5. Activate the emergency terminal with the real interaction (look at it and press F).
            Assert.IsTrue(terminal.CanInteract);
            Vector3 tp = terminal.transform.position;
            yield return MoveTo(player, new Vector3(tp.x, 0.05f, tp.z - 1.4f), 0f);
            var look = player.GetComponent<PlayerLook>();
            look.AddRotation(0f, 30f - look.Pitch); // look down at the screen
            yield return Seconds(0.2f);
            Assert.IsNotNull(player.GetComponent<PlayerInteraction>().Focused, "terminal should be under the crosshair");
            Press(_keyboard.fKey);
            yield return Seconds(0.1f);
            Release(_keyboard.fKey);
            yield return null;
            Assert.IsTrue(terminal.Activated, "F should activate the terminal");
            Assert.AreEqual(5, mission.ObjectiveIndex);
            Assert.IsFalse(exitDoor.IsLocked, "terminal unlocks the exit");

            // 6. Open the exit (walk up to it).
            yield return MoveTo(player, new Vector3(-8f, 0.05f, 101.5f));
            yield return Seconds(1.2f);
            Assert.AreEqual(6, mission.ObjectiveIndex, "the exit door opening completes objective 6");

            // 7. Reach extraction: victory screen.
            yield return MoveTo(player, new Vector3(-8f, 0.05f, 111f));
            Assert.AreEqual(MissionState.Victory, mission.State);
            yield return null;
            Assert.IsTrue(hud.EndScreenVisible, "victory screen must be shown");
        }

        [UnityTest]
        public IEnumerator Mission_IsLost_WhenThePlayerDies()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return Seconds(1f);
            var mission = MissionManager.Current;
            var health = Object.FindAnyObjectByType<PlayerHealth>();
            var hud = health.GetComponent<HUDController>();

            health.ApplyDamage(new DamageInfo(60f, health.TargetPoint, Vector3.forward, null));
            yield return null;
            Assert.Less(hud.ShieldFill, 0.5f, "HUD shield bar reflects damage");
            Assert.AreEqual(MissionState.InProgress, mission.State);

            health.ApplyDamage(new DamageInfo(500f, health.TargetPoint, Vector3.forward, null));
            yield return null;
            Assert.IsFalse(health.IsAlive);
            Assert.AreEqual(MissionState.Defeat, mission.State);
            Assert.IsTrue(hud.EndScreenVisible, "defeat screen must be shown");
            Assert.AreEqual(0f, hud.HealthFill, 0.001f);
        }
    }
}
