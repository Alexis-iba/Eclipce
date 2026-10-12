using System.Linq;
using EclipseGenesis.EditorTools;
using EclipseGenesis.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace EclipseGenesis.Tests
{
    /// <summary>Static checks on project configuration and generated assets (no Play Mode needed).</summary>
    public sealed class ProjectSetupTests
    {
        private static readonly string[] RequiredActions =
        {
            "Move", "Look", "Jump", "Sprint", "Crouch", "Fire", "Aim",
            "Reload", "Interact", "SwitchWeapon", "Weapon1", "Weapon2", "Weapon3", "Grenade", "Inspect", "Pause",
        };

        [Test]
        public void RenderPipeline_IsHDRP_InLinearColorSpace()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            Assert.IsNotNull(pipeline, "no render pipeline asset assigned");
            StringAssert.Contains("HDRenderPipelineAsset", pipeline.GetType().Name);
            Assert.AreEqual(ColorSpace.Linear, PlayerSettings.colorSpace);
        }

        [Test]
        public void InputActions_ContainAllPlayerActions_WithBindings()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(FpsTestSceneBuilder.InputActionsPath);
            Assert.IsNotNull(asset);
            var map = asset.FindActionMap("Player", true);
            foreach (string name in RequiredActions)
            {
                var action = map.FindAction(name);
                Assert.IsNotNull(action, $"missing action {name}");
                Assert.Greater(action.bindings.Count, 0, $"{name} has no bindings");
            }
            Assert.AreEqual(2, asset.controlSchemes.Count, "expected Keyboard&Mouse and Gamepad schemes");
        }

        [Test]
        public void MovementSettings_AreCoherent()
        {
            var s = AssetDatabase.LoadAssetAtPath<MovementSettings>(FpsTestSceneBuilder.MovementSettingsPath);
            Assert.IsNotNull(s, "run Eclipse Genesis/Build/EG_FPS_Test first");
            Assert.Less(s.walkSpeed, s.runSpeed);
            Assert.Less(s.runSpeed, s.sprintSpeed);
            Assert.Less(s.sprintSpeed, s.tacticalSprintSpeed);
            Assert.Less(s.slideHeight, s.crouchHeight);
            Assert.Less(s.crouchHeight, s.standingHeight);
            Assert.Less(s.slideMinStartSpeed, s.sprintSpeed, "slides must be reachable from a normal sprint");
            Assert.Less(s.gravity, 0f);
        }

        [Test]
        public void PlayerPrefab_HasNoMissingReferences()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FpsTestSceneBuilder.PlayerPrefabPath);
            Assert.IsNotNull(prefab, "run Eclipse Genesis/Build/EG_FPS_Test first");
            Assert.AreEqual("EG_Player", prefab.name);
            Assert.IsNotNull(prefab.GetComponentInChildren<Camera>());
            Assert.IsNotNull(prefab.GetComponentInChildren<Camera>().transform.Find("WeaponHolder"), "missing weapon mount point");

            foreach (var behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Assert.IsNotNull(behaviour, "prefab contains a missing script");
                if (behaviour.GetType().Namespace?.StartsWith("EclipseGenesis") != true) continue;
                var so = new SerializedObject(behaviour);
                var it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (it.name == "m_Script") continue;
                    Assert.IsNotNull(it.objectReferenceValue, $"{behaviour.GetType().Name}.{it.name} is not assigned");
                }
            }
        }

        [TestCase("Crouch", "<Keyboard>/leftCtrl")]
        [TestCase("Interact", "<Keyboard>/f")]
        [TestCase("Weapon1", "<Keyboard>/1")]
        [TestCase("Weapon2", "<Keyboard>/2")]
        [TestCase("Weapon3", "<Keyboard>/3")]
        [TestCase("Grenade", "<Keyboard>/g")]
        [TestCase("Pause", "<Keyboard>/escape")]
        [TestCase("Sprint", "<Keyboard>/leftShift")]
        [TestCase("Reload", "<Keyboard>/r")]
        public void ControlLayout_MatchesDesign(string action, string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(FpsTestSceneBuilder.InputActionsPath);
            var a = asset.FindAction(action, true);
            Assert.IsTrue(a.bindings.Any(b => b.path == path), $"{action} should be bound to {path}");
        }

    }
}
