using System.Collections;
using System.IO;
using EclipseGenesis.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace EclipseGenesis.Tests
{
    /// <summary>
    /// Drives the real input bindings with a virtual keyboard/mouse and checks the resulting locomotion.
    /// Each test builds its own tiny world so it does not depend on the sandbox scene layout.
    /// </summary>
    public sealed class PlayerMovementTests : InputTestFixture
    {
        private Keyboard _keyboard;
        private Mouse _mouse;
        private GameObject _world;
        private PlayerMovement _movement;
        private PlayerLook _look;
        private MovementSettings _settings;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            _world = new GameObject("TestWorld");
            Box("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(80f, 1f, 80f));
        }

        public override void TearDown()
        {
            PlayerLook.SetCursorLocked(false);
            Object.Destroy(_world);
            if (_movement != null) Object.Destroy(_movement.gameObject);
            if (_settings != null) Object.Destroy(_settings);
            base.TearDown();
        }

        // ------------------------------------------------------------------ tests

        [UnityTest]
        public IEnumerator Player_LandsOnFloor_AndIsGrounded()
        {
            SpawnPlayer(new Vector3(0f, 1.5f, 0f));
            yield return Seconds(1f);

            Assert.IsTrue(_movement.IsGrounded, "player should be grounded after falling");
            Assert.That(_movement.transform.position.y, Is.EqualTo(0f).Within(0.1f));
            Assert.AreEqual(LocomotionState.Idle, _movement.State);
        }

        [UnityTest]
        public IEnumerator HoldingW_AcceleratesToRunSpeed_AndMovesForward()
        {
            SpawnPlayer(Vector3.zero);
            yield return Seconds(0.3f);

            float startZ = _movement.transform.position.z;
            Press(_keyboard.wKey);
            yield return Seconds(1.2f);

            Assert.That(_movement.HorizontalSpeed, Is.EqualTo(_settings.runSpeed).Within(0.15f));
            Assert.Greater(_movement.transform.position.z - startZ, 3.5f);
            Assert.AreEqual(LocomotionState.Run, _movement.State);

            Release(_keyboard.wKey);
            yield return Seconds(0.6f);
            Assert.Less(_movement.HorizontalSpeed, 0.05f, "player should decelerate to a stop");
        }

        [UnityTest]
        public IEnumerator Sprint_IsFasterThanRun_AndDoubleTapGivesTacticalSprint()
        {
            SpawnPlayer(Vector3.zero);
            yield return Seconds(0.3f);

            Press(_keyboard.wKey);
            yield return null; // one key event per frame: queued events in the same frame overwrite each other
            Press(_keyboard.leftShiftKey);
            yield return Seconds(1f);
            Assert.IsTrue(_movement.IsSprinting);
            Assert.That(_movement.HorizontalSpeed, Is.EqualTo(_settings.sprintSpeed).Within(0.15f));

            // Double tap: two presses inside the tactical window.
            Release(_keyboard.leftShiftKey);
            yield return null;
            Press(_keyboard.leftShiftKey);
            yield return null;
            Release(_keyboard.leftShiftKey);
            yield return null;
            Press(_keyboard.leftShiftKey);
            yield return Seconds(1f);
            Assert.IsTrue(_movement.IsTacticalSprinting, "double tap should trigger tactical sprint");
            Assert.That(_movement.HorizontalSpeed, Is.EqualTo(_settings.tacticalSprintSpeed).Within(0.15f));

            Release(_keyboard.wKey);
            yield return Seconds(0.2f);
            Assert.IsFalse(_movement.IsSprinting, "releasing forward must end the sprint");
        }

        [UnityTest]
        public IEnumerator Crouch_LowersCapsule_AndCannotStandUnderLowCeiling()
        {
            SpawnPlayer(Vector3.zero);
            yield return Seconds(0.3f);

            Press(_keyboard.cKey);
            yield return null;
            Release(_keyboard.cKey);
            yield return Seconds(0.6f);
            Assert.IsTrue(_movement.IsCrouching);
            Assert.That(_movement.CurrentHeight, Is.EqualTo(_settings.crouchHeight).Within(0.02f));

            // Ceiling at 1.3 m directly above the crouched player.
            Box("LowCeiling", new Vector3(0f, 1.5f, 0f), new Vector3(4f, 0.4f, 4f));
            yield return null;

            Press(_keyboard.cKey);
            yield return null;
            Release(_keyboard.cKey);
            yield return Seconds(0.4f);
            Assert.IsTrue(_movement.IsCrouching, "must stay crouched when there is no headroom");
            Assert.Less(_movement.CurrentHeight, 1.3f);
        }

        [UnityTest]
        public IEnumerator SprintThenCrouch_Slides_ThenRecoversToCrouch()
        {
            SpawnPlayer(Vector3.zero);
            yield return Seconds(0.3f);

            Press(_keyboard.wKey);
            yield return null; // one key event per frame: queued events in the same frame overwrite each other
            Press(_keyboard.leftShiftKey);
            yield return null;
            Release(_keyboard.leftShiftKey);
            yield return Seconds(1f);
            Assert.IsTrue(_movement.IsSprinting);

            bool slideStarted = false;
            _movement.SlideStarted += () => slideStarted = true;
            Press(_keyboard.cKey);
            yield return null;
            Release(_keyboard.cKey);
            yield return Seconds(0.15f);

            Assert.IsTrue(slideStarted);
            Assert.IsTrue(_movement.IsSliding);
            Assert.Greater(_movement.HorizontalSpeed, _settings.sprintSpeed, "slide should start with a boost");

            Release(_keyboard.wKey);
            yield return Seconds(1.6f);
            Assert.IsFalse(_movement.IsSliding, "slide must end on its own");
            Assert.IsTrue(_movement.IsCrouching, "slide recovers into crouch when sprint is not held");
        }

        [UnityTest]
        public IEnumerator Jump_ReachesConfiguredHeight_AndLands()
        {
            SpawnPlayer(Vector3.zero);
            yield return Seconds(0.3f);

            float peak = 0f;
            bool landed = false;
            _movement.Landed += _ => landed = true;
            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);

            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                peak = Mathf.Max(peak, _movement.transform.position.y);
                yield return null;
            }

            Assert.That(peak, Is.EqualTo(_settings.jumpHeight).Within(0.12f));
            Assert.IsTrue(landed);
            Assert.IsTrue(_movement.IsGrounded);
        }

        [UnityTest]
        public IEnumerator RunningIntoWall_DoesNotPassThrough()
        {
            Box("Wall", new Vector3(0f, 2f, 3f), new Vector3(10f, 4f, 0.3f));
            SpawnPlayer(Vector3.zero);
            yield return Seconds(0.3f);

            Press(_keyboard.wKey);
            yield return null; // one key event per frame: queued events in the same frame overwrite each other
            Press(_keyboard.leftShiftKey);
            yield return Seconds(2f);

            float wallFace = 3f - 0.15f;
            Assert.Less(_movement.transform.position.z + _settings.radius, wallFace + 0.02f);
            Assert.Less(_movement.HorizontalSpeed, 0.5f, "speed must not accumulate against the wall");
        }

        [UnityTest]
        public IEnumerator Stairs_CanBeClimbed()
        {
            for (int i = 0; i < 6; i++)
            {
                float h = 0.2f * (i + 1);
                Box($"Step{i}", new Vector3(0f, h * 0.5f, 2f + i * 0.35f), new Vector3(3f, h, 0.35f));
            }
            Box("TopLanding", new Vector3(0f, 0.6f, 5.925f), new Vector3(3f, 1.2f, 4f));
            SpawnPlayer(Vector3.zero);
            yield return Seconds(0.3f);

            Press(_keyboard.wKey);
            yield return Seconds(1.3f);

            Assert.Greater(_movement.transform.position.y, 1.1f, "player should have climbed the 1.2 m stairs");
        }

        [UnityTest]
        public IEnumerator MouseDelta_RotatesYawAndPitch_WithPitchClamp()
        {
            SpawnPlayer(Vector3.zero);
            yield return null;

            float yaw0 = _movement.transform.eulerAngles.y;
            Set(_mouse.delta, new Vector2(500f, 0f));
            yield return null;
            Set(_mouse.delta, Vector2.zero);
            yield return null;
            Assert.That(Mathf.DeltaAngle(yaw0, _movement.transform.eulerAngles.y), Is.EqualTo(40f).Within(0.5f));

            Set(_mouse.delta, new Vector2(0f, 5000f));
            yield return null;
            Set(_mouse.delta, Vector2.zero);
            yield return null;
            Assert.That(_look.Pitch, Is.EqualTo(-85f).Within(0.01f), "looking up must clamp at the limit");
        }

        // ------------------------------------------------------------------ helpers

        private void SpawnPlayer(Vector3 position)
        {
            _settings = ScriptableObject.CreateInstance<MovementSettings>();
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Input/EclipseGenesisControls.inputactions"));

            var go = new GameObject("TestPlayer");
            go.SetActive(false);
            go.transform.position = position;
            go.AddComponent<CharacterController>();

            var reader = go.AddComponent<PlayerInputReader>();
            reader.Actions = InputActionAsset.FromJson(json);

            _movement = go.AddComponent<PlayerMovement>();
            _movement.Settings = _settings;
            _movement.Input = reader;

            var pivot = new GameObject("CameraRoot").transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(0f, 1.68f, 0f);

            _look = go.AddComponent<PlayerLook>();
            _look.Input = reader;
            _look.PitchPivot = pivot;

            go.SetActive(true);
        }

        private void Box(string name, Vector3 position, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(_world.transform, false);
            box.transform.position = position;
            box.transform.localScale = size;
            Physics.SyncTransforms();
        }

        private static IEnumerator Seconds(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
        }
    }
}
