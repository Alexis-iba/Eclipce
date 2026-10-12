using System.Linq;
using EclipseGenesis.AI;
using EclipseGenesis.Combat;
using EclipseGenesis.EditorTools.Combat;
using EclipseGenesis.UI;
using EclipseGenesis.Weapons;
using EclipseGenesis.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace EclipseGenesis.Tests
{
    /// <summary>Static checks of the Phase 4 prefabs and data assets.</summary>
    public sealed class CombatAssetsTests
    {
        [Test]
        public void EG47_Prefab_HasAllSockets_AndIsMultiPart()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponBuilder.PrefabPath);
            Assert.IsNotNull(prefab);
            var model = prefab.GetComponent<WeaponModel>();
            Assert.IsNotNull(model.muzzle);
            Assert.IsNotNull(model.sight);
            Assert.IsNotNull(model.shellEject);
            Assert.IsNotNull(model.magazine);
            Assert.IsNotNull(model.leftHand);
            Assert.IsNotNull(model.muzzleFlash);
            Assert.IsNotNull(model.muzzleLight);
            Assert.Greater(prefab.GetComponentsInChildren<Renderer>(true).Length, 30, "not a single cube");
            Assert.AreEqual(0, prefab.GetComponentsInChildren<Collider>(true).Length, "viewmodels must not collide or block shots");
        }

        [Test]
        public void EG47_Data_MatchesDesign()
        {
            var data = AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponBuilder.DataPath);
            Assert.AreEqual("EG-47", data.displayName);
            Assert.AreEqual(30, data.magazineSize);
            Assert.IsTrue(data.automatic);
            Assert.IsNotNull(data.fireClip);
            Assert.IsNotNull(data.reloadClip);
            Assert.IsNotNull(data.viewModelPrefab);
        }

        [Test]
        public void Stalker_Prefab_IsComplete()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AlienBuilder.PrefabPath);
            Assert.IsNotNull(prefab, "EG_Alien_Stalker prefab missing");
            Assert.IsNotNull(prefab.GetComponent<NavMeshAgent>());
            Assert.IsNotNull(prefab.GetComponent<AlienHealth>().Data);
            Assert.IsNotNull(prefab.GetComponent<AlienPerception>().Data);
            Assert.IsNotNull(prefab.GetComponent<AlienBrain>());
            Assert.IsNotNull(prefab.GetComponent<AlienBody>());
            var boxes = prefab.GetComponentsInChildren<Hitbox>(true);
            Assert.AreEqual(6, boxes.Length, "head + body + 4 limbs");
            Assert.AreEqual(1, boxes.Count(b => b.Zone == HitZone.Head));
            Assert.IsTrue(boxes.All(b => b.Owner != null && b.GetComponent<Collider>() != null));
            Assert.Greater(prefab.GetComponentsInChildren<Light>(true).Length, 0, "bioluminescent eyes");
        }

        [Test]
        public void Player_Prefab_HasCombatSystems()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EclipseGenesis.EditorTools.FpsTestSceneBuilder.PlayerPrefabPath);
            Assert.IsNotNull(prefab.GetComponent<PlayerHealth>());
            Assert.IsNotNull(prefab.GetComponent<WeaponController>());
            Assert.IsNotNull(prefab.GetComponent<WeaponEffects>());
            Assert.IsNotNull(prefab.GetComponent<PlayerInteraction>());
            Assert.IsNotNull(prefab.GetComponent<HUDController>());
        }
    }
}
