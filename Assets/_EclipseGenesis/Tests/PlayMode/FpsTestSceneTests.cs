using System.Collections;
using EclipseGenesis.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EclipseGenesis.Tests
{
    /// <summary>
    /// Loads the real EG_FPS_Test scene and plays it. Any error logged while it runs fails the test,
    /// so this also catches missing references and exceptions in the scene setup.
    /// </summary>
    public sealed class FpsTestSceneTests : InputTestFixture
    {
        private const string SceneName = "EG_FPS_Test";
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

        /// <summary>Unloads the scene so its player and geometry cannot leak into later tests.</summary>
        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            var scene = SceneManager.GetSceneByName(SceneName);
            if (!scene.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("EmptyAfterFpsTest"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator Scene_Loads_WithPlayerCameraAndWeaponMount()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return Seconds(1f);

            var player = GameObject.Find("EG_Player");
            Assert.IsNotNull(player, "EG_Player missing from scene");
            Assert.IsTrue(player.TryGetComponent(out CharacterController _));
            Assert.IsTrue(player.TryGetComponent(out PlayerInputReader _));
            var movement = player.GetComponent<PlayerMovement>();
            Assert.IsTrue(movement.IsGrounded, "player should stand on the scene floor");

            var cam = player.GetComponentInChildren<Camera>();
            Assert.IsNotNull(cam);
            Assert.AreEqual(cam, Camera.main);
            Assert.IsNotNull(cam.transform.Find("WeaponHolder"), "weapon mount point missing");
        }

        [UnityTest]
        public IEnumerator Scene_PlayerWalks_AndCameraIsSteadierWhileAiming()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return Seconds(0.5f);

            var player = GameObject.Find("EG_Player");
            var movement = player.GetComponent<PlayerMovement>();
            Transform offset = player.transform.Find("CameraRoot/CameraOffset");
            Vector3 start = player.transform.position;

            Press(_keyboard.wKey);
            float hipBob = 0f;
            yield return MeasureBob(offset, 1.5f, v => hipBob = v);
            Assert.Greater(Vector3.Distance(start, player.transform.position), 4f, "player should walk in the scene");

            movement.IsAiming = true; // set by the weapon system from Phase 3 on
            yield return Seconds(0.4f);
            float aimBob = 0f;
            yield return MeasureBob(offset, 1.2f, v => aimBob = v);
            Release(_keyboard.wKey);

            Assert.Greater(hipBob, 0.005f, "some head bob is expected while walking");
            Assert.Less(hipBob, 0.05f, "head bob must stay subtle");
            Assert.Less(aimBob, hipBob * 0.4f, "camera must be steadier while aiming");
        }

        private static IEnumerator MeasureBob(Transform offset, float seconds, System.Action<float> result)
        {
            float max = 0f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                Vector3 p = offset.localPosition;
                max = Mathf.Max(max, new Vector2(p.x, p.y).magnitude);
                yield return null;
            }
            result(max);
        }

        private static IEnumerator Seconds(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
        }
    }
}
