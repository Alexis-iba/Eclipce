using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.AI.Navigation;
using EclipseGenesis.AI;
using EclipseGenesis.Combat;
using EclipseGenesis.Player;
namespace EclipseGenesis.Tests {
 public sealed class ReferenceAlienTests {
  GameObject player;GameObject alien;
  IEnumerator LoadArena(){
   yield return SceneManager.LoadSceneAsync("EG_FPS_Test");yield return new WaitForSeconds(.5f);
   player=GameObject.Find("EG_Player");player.GetComponent<PlayerInputReader>().SetGameplayEnabled(false);var movement=player.GetComponent<PlayerMovement>();movement.Teleport(new Vector3(500,.05f,-6),Quaternion.identity);movement.enabled=false;
   var root=new GameObject("ReferenceArena");var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.transform.position=new Vector3(500,-.5f,0);floor.transform.localScale=new Vector3(40,1,40);
   var surface=root.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();
  }
  GameObject Spawn(string species,Vector3 pos){
#if UNITY_EDITOR
   var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_EclipseGenesis/Prefabs/Aliens/ReferenceV1/EG_Alien_"+species+"_Reference.prefab");
   Assert.IsNotNull(prefab);alien=Object.Instantiate(prefab,pos,Quaternion.Euler(0,180,0));return alien;
#else
   Assert.Ignore("Editor asset verification only");return null;
#endif
  }
  [UnityTest] public IEnumerator SixClips_DeformEachSpeciesMesh(){
   yield return LoadArena();
   foreach(var species in new[]{"Stalker","Ravager","Spitter"}){
    var g=Spawn(species,new Vector3(508,0,0));g.GetComponent<AlienBrain>().enabled=false;g.GetComponent<ReferenceAlienMotion>().enabled=false;if(g.GetComponent<SpitterAttack>()!=null)g.GetComponent<SpitterAttack>().enabled=false;
    var anim=g.GetComponentInChildren<Animator>();var skin=g.GetComponentInChildren<SkinnedMeshRenderer>();Assert.AreEqual(6,anim.runtimeAnimatorController.animationClips.Distinct().Count());
    yield return null;
    foreach(var state in new[]{"Idle","Walk","Run","Attack","Hit","Death"}){
     anim.Play(state,0,.2f);anim.Update(0);var a=new Mesh();skin.BakeMesh(a);var vertices=a.vertices;
     anim.Play(state,0,.7f);anim.Update(0);var b=new Mesh();skin.BakeMesh(b);var changed=b.vertices;float delta=0;for(int i=0;i<vertices.Length;i++)delta+=Vector3.Distance(vertices[i],changed[i]);
     Assert.Greater(delta,.01f,species+" "+state+" must deform the visible mesh");Object.Destroy(a);Object.Destroy(b);
    }Object.Destroy(g);yield return null;
   }
  }
  [UnityTest] public IEnumerator Stalker_Chases_Attacks_Reacts_AndDies(){
   yield return LoadArena();var g=Spawn("Stalker",new Vector3(500,0,8));var h=g.GetComponent<AlienHealth>();var brain=g.GetComponent<AlienBrain>();var motion=g.GetComponent<ReferenceAlienMotion>();var health=player.GetComponent<PlayerHealth>();int hits=0;health.Damaged+=_=>hits++;
   yield return new WaitForSeconds(1.2f);Assert.IsTrue(brain.Agent.isOnNavMesh);Assert.IsTrue(brain.State==AlienState.Chase||brain.State==AlienState.Attack);
   h.ApplyDamage(new DamageInfo(10,g.transform.position,Vector3.forward,player));yield return null;Assert.AreEqual("Hit",motion.VisualState);
   yield return new WaitForSeconds(3);Assert.Greater(hits,0,"Stalker must deal melee damage");
   h.ApplyDamage(new DamageInfo(999,g.transform.position,Vector3.forward,player));yield return new WaitForSeconds(.2f);Assert.IsFalse(h.IsAlive);Assert.AreEqual("Death",motion.VisualState);
  }
  [UnityTest] public IEnumerator Ravager_HasArmor_AndHeavyMeleeDamage(){
   yield return LoadArena();var g=Spawn("Ravager",new Vector3(500,0,-3.3f));var h=g.GetComponent<AlienHealth>();float before=h.Health;h.ApplyDamage(new DamageInfo(100,g.transform.position,Vector3.forward,player));Assert.That(before-h.Health,Is.EqualTo(78).Within(.1f));
   int hits=0;float damage=0;player.GetComponent<PlayerHealth>().Damaged+=d=>{hits++;damage=d.Amount;};
   yield return new WaitForSeconds(3);Assert.Greater(hits,0);Assert.That(damage,Is.EqualTo(38).Within(.01f));Assert.AreEqual(480,h.MaxHealth);
  }
  [UnityTest] public IEnumerator Spitter_ProjectsAcid_AndSpillsOnDeath(){
   yield return LoadArena();var g=Spawn("Spitter",new Vector3(500,0,5));int hits=0;player.GetComponent<PlayerHealth>().Damaged+=_=>hits++;
   yield return new WaitForSeconds(6);Assert.Greater(hits,0,"Acid projectiles must damage player");Assert.IsNotNull(g.transform.Find("Socket_Mouth")==null?g.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Socket_Mouth"):g.transform.Find("Socket_Mouth"));
   g.GetComponent<AlienHealth>().ApplyDamage(new DamageInfo(999,g.transform.position,Vector3.forward,player));yield return new WaitForSeconds(.2f);Assert.AreEqual("Death",g.GetComponent<ReferenceAlienMotion>().VisualState);Assert.IsNotNull(GameObject.Find("SpitterDeath_AcidSpill"));
  }
 }
}