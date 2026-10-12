using System.Collections;
using System.Linq;
using EclipseGenesis.AI;
using EclipseGenesis.Combat;
using EclipseGenesis.Mission;
using EclipseGenesis.Player;
using EclipseGenesis.UI;
using EclipseGenesis.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace EclipseGenesis.Tests {
public sealed class OpenZoneIntegrationTests : InputTestFixture {
 Keyboard keyboard;Mouse mouse;
 public override void Setup(){base.Setup();InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();}
 public override void TearDown(){PlayerLook.SetCursorLocked(false);base.TearDown();}
 static IEnumerator Seconds(float s){for(float t=0;t<s;t+=Time.deltaTime)yield return null;}
 static void Warp(GameObject p,Vector3 pos){var cc=p.GetComponent<CharacterController>();cc.enabled=false;p.transform.position=pos;cc.enabled=true;}
 static IEnumerator Load(){yield return SceneManager.LoadSceneAsync("Eclipse_OpenZone");yield return Seconds(1); }
 [UnityTest] public IEnumerator Movement_ADS_Fire_Reload_HUD(){
  yield return Load();var p=GameObject.Find("EG_Player");var w=p.GetComponent<WeaponController>();var hud=p.GetComponent<HUDController>();
  Assert.IsNotNull(w.CurrentModel);Assert.AreEqual("EG_Weapon_EG47_Tactical",w.CurrentModel.name);
  var start=p.transform.position;Press(keyboard.wKey);yield return Seconds(.6f);Release(keyboard.wKey);Assert.Greater(Vector3.Distance(start,p.transform.position),1);
  float fov=Camera.main.fieldOfView;Press(mouse.rightButton);yield return Seconds(.8f);Assert.Greater(w.AimAmount,.9f);Assert.Less(Camera.main.fieldOfView,fov*.9f);
  var sight=Camera.main.WorldToViewportPoint(w.CurrentModel.sight.position);Assert.That(sight.x,Is.EqualTo(.5f).Within(.04f));
  int before=w.Magazine;Press(mouse.leftButton);yield return Seconds(.4f);Release(mouse.leftButton);yield return null;Assert.Less(w.Magazine,before);Assert.AreEqual(w.Magazine.ToString(),hud.AmmoText);Release(mouse.rightButton);
  Assert.IsTrue(w.TryReload());yield return Seconds(w.CurrentData.reloadTime+w.CurrentData.emptyReloadExtra+.5f);Assert.AreEqual(w.CurrentData.magazineSize,w.Magazine);
  Assert.IsNotNull(Object.FindFirstObjectByType<OpenZoneTacticalOverlay>());
  var path=new NavMeshPath();Assert.IsTrue(NavMesh.CalculatePath(new Vector3(0,0,-90),new Vector3(0,0,10),NavMesh.AllAreas,path));Assert.AreEqual(NavMeshPathStatus.PathComplete,path.status);
 }
 [UnityTest] public IEnumerator Data_Power_Defense_Extraction_Victory(){
  yield return Load();var p=GameObject.Find("EG_Player");p.GetComponent<PlayerHealth>().Invulnerable=true;var m=MissionManager.Current;
  Warp(p,new Vector3(0,.2f,-42));yield return Seconds(.2f);Assert.AreEqual(1,m.ObjectiveIndex);
  Warp(p,new Vector3(0,.2f,44));yield return Seconds(.2f);Assert.AreEqual(2,m.ObjectiveIndex);
  var data=GameObject.Find("Terminal_Emergency").GetComponent<EclipseGenesis.World.Terminal>();Assert.IsTrue(data.CanInteract);data.Interact(p);Assert.AreEqual(3,m.ObjectiveIndex);
  var power=GameObject.Find("OpenZone_PowerTerminal").GetComponent<EclipseGenesis.World.Terminal>();Assert.IsTrue(power.CanInteract);power.Interact(p);yield return Seconds(.2f);Assert.AreEqual(4,m.ObjectiveIndex);
  var wave=GameObject.Find("OpenZone_DefenseWave");Assert.IsNotNull(wave);foreach(var a in wave.GetComponentsInChildren<AlienHealth>())a.ApplyDamage(new DamageInfo(999,a.transform.position,Vector3.forward,p));
  Assert.AreEqual(5,m.ObjectiveIndex);Warp(p,new Vector3(0,.2f,-6));yield return Seconds(.2f);Assert.AreEqual(6,m.ObjectiveIndex);
  Warp(p,new Vector3(0,.2f,-94));yield return Seconds(.5f);Assert.AreEqual(MissionState.Victory,m.State);Assert.IsTrue(p.GetComponent<HUDController>().EndScreenVisible);
 }
 [UnityTest] public IEnumerator Spitter_FiresProjectile_AndDamagesPlayer(){
  yield return Load();var p=GameObject.Find("EG_Player");var health=p.GetComponent<PlayerHealth>();var spit=Object.FindFirstObjectByType<SpitterAttack>();Assert.IsNotNull(spit);
  foreach(var a in Object.FindObjectsByType<AlienBrain>(FindObjectsSortMode.None))if(a.gameObject!=spit.gameObject)a.gameObject.SetActive(false);
  Warp(p,new Vector3(0,.2f,-60));var brain=spit.GetComponent<AlienBrain>();brain.Agent.Warp(new Vector3(0,0,-50));spit.transform.rotation=Quaternion.Euler(0,180,0);
  int hits=0;health.Damaged+=_=>hits++;yield return Seconds(7);Assert.Greater(hits,0,"Spitter must inflict damage through a moving projectile");
 }
 [UnityTest] public IEnumerator PlayerDeath_ShowsDefeat(){
  yield return Load();var p=GameObject.Find("EG_Player");p.GetComponent<PlayerHealth>().ApplyDamage(new DamageInfo(999,p.transform.position,Vector3.forward,null));yield return Seconds(.4f);Assert.AreEqual(MissionState.Defeat,MissionManager.Current.State);Assert.IsTrue(p.GetComponent<HUDController>().EndScreenVisible);
 }
}
}