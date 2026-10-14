using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using EclipseGenesis.AI;
using EclipseGenesis.Combat;
using EclipseGenesis.Player;
using EclipseGenesis.Weapons;
using EclipseGenesis.World;
using EclipseGenesis.Mission;

namespace EclipseGenesis.Tests
{
    public sealed class ReferenceWorldTests : InputTestFixture
    {
        Mouse mouse; Keyboard keyboard; GameObject player;
        public override void Setup(){base.Setup();mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;}
        public override void TearDown(){PlayerLook.SetCursorLocked(false);base.TearDown();}
        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Eclipse_Genesis_ReferenceWorld");
            player=Object.FindFirstObjectByType<PlayerMovement>().gameObject;
            yield return new WaitForSeconds(.6f);
        }
        [UnityTest] public IEnumerator FiveWeapons_EquipFireReload_AndHaveVisibleDistinctModels()
        {
            yield return Load();
            var w=player.GetComponent<WeaponController>();
            string[] expected={"EG-47","VX-9","BR-12","NS-50","PLASMA-X"};
            var models=new System.Collections.Generic.HashSet<string>();
            for(int i=0;i<expected.Length;i++)
            {
                w.Equip(i);yield return new WaitForSeconds(.7f);
                Assert.AreEqual(expected[i],w.CurrentData.id);Assert.IsNotNull(w.CurrentModel.muzzle);Assert.IsNotNull(w.CurrentModel.sight);
                Assert.Greater(Vector3.Distance(w.CurrentModel.muzzle.position,w.CurrentModel.sight.position),.15f);
                models.Add(w.CurrentModel.name);
                var planes=GeometryUtility.CalculateFrustumPlanes(Camera.main);
                Assert.IsTrue(w.CurrentModel.GetComponentsInChildren<Renderer>().Any(r=>GeometryUtility.TestPlanesAABB(planes,r.bounds)),expected[i]+" must have visible geometry");
                int ammo=w.Magazine;Press(mouse.leftButton);yield return new WaitForSeconds(.05f);Release(mouse.leftButton);yield return null;
                Assert.Less(w.Magazine,ammo,expected[i]+" must fire");
                Assert.IsTrue(w.TryReload());yield return new WaitForSeconds(w.CurrentData.reloadTime+w.CurrentData.emptyReloadExtra+.25f);
                Assert.AreEqual(w.CurrentData.magazineSize,w.Magazine);Assert.Less(w.Reserve,w.CurrentData.reserveAmmo);
            }
            Assert.AreEqual(5,models.Count);
            Press(keyboard.digit4Key);yield return null;yield return null;Release(keyboard.digit4Key);Assert.AreEqual("NS-50",w.CurrentData.id);
            yield return new WaitForSeconds(.6f);Press(keyboard.digit5Key);yield return null;yield return null;Release(keyboard.digit5Key);Assert.AreEqual("PLASMA-X",w.CurrentData.id);
        }
        [UnityTest] public IEnumerator TownLaboratoryAndExtraction_AreConnectedByNavigation()
        {
            yield return Load();
            foreach(var destination in new[]{new Vector3(0,0,150),new Vector3(0,0,190),new Vector3(-4.6f,0,247.4f),new Vector3(62,0,272)})
            {
                NavMeshHit a,b;Assert.IsTrue(NavMesh.SamplePosition(player.transform.position,out a,3,NavMesh.AllAreas));Assert.IsTrue(NavMesh.SamplePosition(destination,out b,3,NavMesh.AllAreas));
                var path=new NavMeshPath();Assert.IsTrue(NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path));Assert.AreEqual(NavMeshPathStatus.PathComplete,path.status,destination.ToString());
            }
            Assert.IsNotNull(GameObject.Find("ReferenceWorld_CrashZone"));Assert.IsNotNull(GameObject.Find("Distant_GenesisEnergyDome"));
        }
        IEnumerator Move(Vector3 destination)
        {
            player.GetComponent<PlayerMovement>().Teleport(destination,Quaternion.identity);
            yield return new WaitForSeconds(.3f);
        }
        [UnityTest] public IEnumerator Operation_Completes_FromTownThroughGenesisToCrashZone()
        {
            yield return Load();player.GetComponent<PlayerInputReader>().SetGameplayEnabled(false);
            var m=MissionManager.Current;Assert.AreEqual(0,m.ObjectiveIndex);
            yield return Move(new Vector3(0,.05f,-21));Assert.AreEqual(1,m.ObjectiveIndex);
            yield return Move(new Vector3(0,.05f,150));Assert.AreEqual(2,m.ObjectiveIndex);
            var data=Object.FindObjectsByType<Terminal>(FindObjectsSortMode.None).Single(t=>t.name=="Terminal_Emergency");
            var power=Object.FindObjectsByType<Terminal>(FindObjectsSortMode.None).Single(t=>t.name=="Terminal_PowerGrid");
            Assert.IsTrue(data.CanInteract);Assert.IsFalse(power.CanInteract);data.Interact(player);Assert.AreEqual(3,m.ObjectiveIndex);
            Assert.IsTrue(power.CanInteract);power.Interact(player);Assert.AreEqual(4,m.ObjectiveIndex);
            var wave=GameObject.Find("Genesis_DefenseWave");Assert.IsTrue(wave.activeInHierarchy);
            foreach(var alien in wave.GetComponentsInChildren<AlienHealth>())alien.ApplyDamage(new DamageInfo(2000,alien.transform.position,Vector3.forward,player));
            yield return null;Assert.AreEqual(5,m.ObjectiveIndex);
            yield return Move(new Vector3(0,.05f,139));Assert.AreEqual(6,m.ObjectiveIndex);
            yield return Move(new Vector3(62,.1f,272));Assert.AreEqual(MissionState.Victory,m.State);
        }
    }
}
