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
            "Reload", "Interact", "SwitchWeapon", "Grenade", "Inspect", "Pause",
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
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(SandboxSceneBuilder.InputActionsPath);
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
            var s = AssetDatabase.LoadAssetAtPath<MovementSettings>(SandboxSceneBuilder.MovementSettingsPath);
            Assert.IsNotNull(s, "run Eclipse Genesis/Build/Movement Sandbox first");
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
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SandboxSceneBuilder.PlayerPrefabPath);
            Assert.IsNotNull(prefab, "run Eclipse Genesis/Build/Movement Sandbox first");
            Assert.IsNotNull(prefab.GetComponentInChildren<Camera>());

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

        [Test]
        public void SandboxScene_IsFirstInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.IsTrue(scenes.Length > 0);
            Assert.AreEqual(SandboxSceneBuilder.ScenePath, scenes.First().path);
            Assert.IsTrue(scenes.All(s => System.IO.File.Exists(s.path)), "build settings reference a missing scene");
        }
    }
}
