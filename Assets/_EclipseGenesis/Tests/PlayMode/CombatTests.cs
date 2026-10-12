using System.Collections;
using EclipseGenesis.AI;
using EclipseGenesis.Combat;
using EclipseGenesis.Player;
using EclipseGenesis.UI;
using EclipseGenesis.Weapons;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EclipseGenesis.Tests
{
    /// <summary>
    /// Phase 4 combat checks with the real prefabs: EG-47 (fire, ammo, reload, ADS, HUD) in EG_FPS_Test, and the
    /// Stalker AI in a small arena built at runtime with its own NavMesh.
    /// </summary>
    public sealed class CombatTests : InputTestFixture
    {
        private Keyboard _keyboard;
        private Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        public override void TearDown()
        {
            PlayerLook.SetCursorLocked(false);
            base.TearDown();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var active = SceneManager.GetActiveScene();
            var empty = SceneManager.CreateScene("EmptyAfterCombat_" + Time.frameCount);
            SceneManager.SetActiveScene(empty);
            if (active.isLoaded) yield return SceneManager.UnloadSceneAsync(active);
        }

        private static IEnumerator Seconds(float s)
        {
            for (float t = 0f; t < s; t += Time.deltaTime) yield return null;
        }

        private IEnumerator LoadFpsTest()
        {
            yield return SceneManager.LoadSceneAsync("EG_FPS_Test", LoadSceneMode.Single);
            yield return Seconds(1f); // landing + weapon equip
        }

        // ------------------------------------------------------------------ weapon

        [UnityTest]
        public IEnumerator Rifle_IsVisible_InTheLowerRightOfTheView()
        {
            yield return LoadFpsTest();
            var weapons = Object.FindAnyObjectByType<WeaponController>();
            Assert.IsNotNull(weapons, "WeaponController missing on EG_Player");
            Assert.AreEqual("EG-47", weapons.CurrentData.displayName);
            var model = weapons.CurrentModel;
            Assert.IsNotNull(model, "EG-47 viewmodel not instantiated");
            Assert.IsTrue(model.gameObject.activeInHierarchy);

            var cam = Camera.main;
            Vector3 vp = cam.WorldToViewportPoint(model.transform.position);
            Assert.Greater(vp.z, 0f, "weapon must be in front of the camera");
            Assert.That(vp.x, Is.InRange(0.5f, 1.05f), "weapon should sit on the right half");
            Assert.That(vp.y, Is.InRange(-0.1f, 0.5f), "weapon should sit on the lower half");
            int renderers = model.GetComponentsInChildren<Renderer>(false).Length;
            Assert.Greater(renderers, 20, "provisional model should be multi-part, not a single cube");
        }

        [UnityTest]
        public IEnumerator Firing_ConsumesAmmo_UpdatesHud_AndReloadRefills()
        {
            yield return LoadFpsTest();
            var weapons = Object.FindAnyObjectByType<WeaponController>();
            var hud = Object.FindAnyObjectByType<HUDController>();
            int mag0 = weapons.Magazine, reserve0 = weapons.Reserve;
            Assert.AreEqual(30, mag0);
            int fired = 0;
            weapons.Fired += () => fired++;

            Press(_mouse.leftButton);
            yield return Seconds(0.5f);
            Release(_mouse.leftButton);
            yield return null;

            Assert.Greater(fired, 3, "automatic fire should shoot several rounds in 0.5 s");
            Assert.AreEqual(mag0 - fired, weapons.Magazine, "each shot uses one round");
            Assert.AreEqual(weapons.Magazine.ToString(), hud.AmmoText, "HUD ammo must show the real magazine");

            Press(_keyboard.rKey);
            yield return Seconds(0.1f);
            Release(_keyboard.rKey);
            Assert.IsTrue(weapons.IsReloading);
            yield return Seconds(weapons.CurrentData.reloadTime + 0.3f);
            Assert.IsFalse(weapons.IsReloading);
            Assert.AreEqual(30, weapons.Magazine);
            Assert.AreEqual(reserve0 - fired, weapons.Reserve);
        }

        [UnityTest]
        public IEnumerator AimDownSights_NarrowsFov_ReducesSpread_AndCentersWeapon()
        {
            yield return LoadFpsTest();
            var weapons = Object.FindAnyObjectByType<WeaponController>();
            var cam = Camera.main;
            float hipFov = cam.fieldOfView, hipSpread = weapons.CurrentSpread;
            float hipX = cam.WorldToViewportPoint(weapons.CurrentModel.sight.position).x;

            Press(_mouse.rightButton);
            yield return Seconds(0.8f);
            Assert.IsTrue(weapons.IsAiming);
            Assert.Less(cam.fieldOfView, hipFov * 0.9f, "ADS must zoom in");
            Assert.Less(weapons.CurrentSpread, hipSpread * 0.5f, "ADS must tighten spread");
            float adsX = cam.WorldToViewportPoint(weapons.CurrentModel.sight.position).x;
            Assert.That(adsX, Is.EqualTo(0.5f).Within(0.03f), "sight must be centred when aiming");
            Assert.Greater(Mathf.Abs(hipX - 0.5f), 0.05f);
            Release(_mouse.rightButton);
        }

        [UnityTest]
        public IEnumerator Recoil_KicksTheCameraUp()
        {
            yield return LoadFpsTest();
            var look = Object.FindAnyObjectByType<PlayerLook>();
            float pitch0 = look.Pitch;
            Press(_mouse.leftButton);
            yield return Seconds(0.4f);
            Release(_mouse.leftButton);
            Assert.Less(look.Pitch, pitch0 - 0.5f, "sustained fire should climb (negative pitch = up)");
        }

        // ------------------------------------------------------------------ alien

        private static GameObject SpawnStalker(Vector3 pos, float yaw)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_EclipseGenesis/Prefabs/Aliens/EG_Alien_Stalker.prefab");
            Assert.IsNotNull(prefab, "EG_Alien_Stalker prefab missing");
            return Object.Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f));
#else
            return null;
#endif
        }

        /// <summary>Flat 40 x 40 m arena, optional wall, runtime NavMesh, player from the real prefab.</summary>
        private static PlayerHealth BuildArena(Vector3 playerPos, bool wallBetween)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            if (wallBetween)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = new Vector3(0f, 2f, 5f);
                wall.transform.localScale = new Vector3(30f, 4f, 0.5f);
            }
            var surface = new GameObject("NavMesh").AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
#if UNITY_EDITOR
            var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_EclipseGenesis/Prefabs/Player/EG_Player.prefab");
            var player = Object.Instantiate(playerPrefab, playerPos, Quaternion.Euler(0f, 180f, 0f));
            return player.GetComponent<PlayerHealth>();
#else
            return null;
#endif
        }

        [UnityTest]
        public IEnumerator Stalker_DetectsChasesAndAttacks_OnlyWithinReach()
        {
            var player = BuildArena(new Vector3(0f, 0.05f, -2f), wallBetween: false);
            var alien = SpawnStalker(new Vector3(0f, 0f, 14f), 180f);
            var brain = alien.GetComponent<AlienBrain>();
            yield return Seconds(0.3f);

            float closestHitDistance = 0f;
            bool detected = false, chased = false;
            player.Damaged += info => closestHitDistance = Mathf.Max(closestHitDistance, Vector3.Distance(Flat(info.Source.transform.position), Flat(player.transform.position)));

            for (float t = 0f; t < 8f && player.Health >= player.MaxHealth; t += Time.deltaTime)
            {
                detected |= brain.State == AlienState.Alert || brain.State == AlienState.Chase;
                chased |= brain.State == AlienState.Chase;
                yield return null;
            }

            Assert.IsTrue(detected, "Stalker should notice a player in plain view at 16 m");
            Assert.IsTrue(chased, "Stalker should chase");
            Assert.Greater(brain.AttacksLanded, 0, "Stalker should land a melee attack");
            Assert.Less(player.Shield + player.Health, player.MaxShield + player.MaxHealth, "player must take damage");
            var data = brain.GetComponent<AlienPerception>().Data;
            Assert.LessOrEqual(closestHitDistance, data.attackRange + data.attackReachTolerance + 0.05f, "attacks must not land from an impossible distance");
        }

        [UnityTest]
        public IEnumerator Stalker_CannotSeeThroughWalls()
        {
            var player = BuildArena(new Vector3(0f, 0.05f, -2f), wallBetween: true);
            var alien = SpawnStalker(new Vector3(0f, 0f, 12f), 180f);
            var perception = alien.GetComponent<AlienPerception>();
            yield return Seconds(2f);
            Assert.IsFalse(perception.CanSeeTarget, "a solid wall must block line of sight");
            Assert.AreEqual(player.MaxHealth, player.Health);
        }

        [UnityTest]
        public IEnumerator ShootingTheStalker_DamagesAndKillsIt_WithHitmarker()
        {
            var player = BuildArena(new Vector3(0f, 0.05f, -2f), wallBetween: false);
            player.Invulnerable = true;
            var alien = SpawnStalker(new Vector3(0f, 0f, -9f), 0f);
            var health = alien.GetComponent<AlienHealth>();
            var brain = alien.GetComponent<AlienBrain>();
            var weapons = player.GetComponent<WeaponController>();
            var hud = player.GetComponent<HUDController>();
            int hits = 0, kills = 0;
            bool hitmarkerSeen = false;
            weapons.HitConfirmed += r => { hits++; if (r.Killed) kills++; };
            yield return Seconds(0.8f);

            // Keep the crosshair on the alien's body while firing.
            var look = player.GetComponent<PlayerLook>();
            Press(_mouse.leftButton);
            for (float t = 0f; t < 6f && health.IsAlive; t += Time.deltaTime)
            {
                Vector3 to = alien.transform.position + Vector3.up * 0.8f - Camera.main.transform.position;
                float yaw = Quaternion.LookRotation(to).eulerAngles.y;
                float pitch = -Mathf.Asin(to.normalized.y) * Mathf.Rad2Deg;
                look.AddRotation(Mathf.DeltaAngle(look.Yaw, yaw), pitch - look.Pitch);
                hitmarkerSeen |= hud.HitmarkerVisible;
                if (weapons.Magazine == 0) { Release(_mouse.leftButton); yield return Seconds(3f); Press(_mouse.leftButton); }
                yield return null;
            }
            Release(_mouse.leftButton);

            Assert.Greater(hits, 0, "shots should register on the Stalker's hitboxes");
            Assert.IsTrue(hitmarkerSeen, "HUD hitmarker should appear on hit");
            Assert.IsFalse(health.IsAlive, "Stalker should die");
            Assert.AreEqual(1, kills);
            Assert.AreEqual(AlienState.Dead, brain.State);
            yield return Seconds(1.2f);
            Assert.IsFalse(alien.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled, "dead aliens stop navigating");
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
