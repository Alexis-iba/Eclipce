using UnityEngine;
using System.Collections;
namespace EclipseGenesis.AI {
 public sealed class AlienAnimationReview : MonoBehaviour {
  public Animator[] characters;
  public Transform spitterMouth;
  public Material acidMaterial;
  public Mesh dropletMesh;
  public string CurrentState {get;private set;}="Idle";
  public void Preview(string state){
   CurrentState=state;
   foreach(var a in characters)if(a!=null){a.speed=1;a.CrossFadeInFixedTime(state,.12f,0,0);}
   if(state=="Attack"&&spitterMouth!=null)StartCoroutine(AcidBurst());
   if(state=="Death"&&spitterMouth!=null){var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name="Review_AcidSpill";Destroy(g.GetComponent<Collider>());g.transform.position=new Vector3(spitterMouth.root.position.x,.012f,spitterMouth.root.position.z);g.transform.localScale=new Vector3(2,.01f,2);g.GetComponent<Renderer>().sharedMaterial=acidMaterial;Destroy(g,8);}
  }
  IEnumerator AcidBurst(){
   yield return new WaitForSeconds(.4f);if(CurrentState!="Attack")yield break;
   var g=new GameObject("Review_AcidBurst");g.transform.position=spitterMouth.position;g.transform.rotation=Quaternion.LookRotation(Vector3.forward);
   var p=g.AddComponent<ParticleSystem>();var main=p.main;main.loop=false;main.duration=.3f;main.startLifetime=.6f;main.startSpeed=6;main.startSize=.04f;main.gravityModifier=.35f;main.maxParticles=30;
   var emission=p.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,24)});
   var shape=p.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=10;shape.radius=.07f;
   var r=g.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Mesh;r.mesh=dropletMesh;r.sharedMaterial=acidMaterial;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;p.Play();Destroy(g,1.5f);
  }
  void Start(){Preview("Idle");}
  void OnGUI(){
   var previous=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*Mathf.Max(.65f,Screen.width/1920f));
   GUI.skin.button.fontSize=18;GUI.skin.box.fontSize=18;GUI.skin.label.fontSize=16;
   GUI.Box(new Rect(20,20,780,90),"ECLIPSE GENESIS / REVISIÓN DE CRIATURAS 3D");
   string[] names={"Idle","Walk","Run","Attack","Hit","Death"};string[] labels={"Espera","Caminar","Correr","Ataque","Daño","Muerte"};
   for(int i=0;i<names.Length;i++)if(GUI.Button(new Rect(34+i*125,59,118,36),labels[i]))Preview(names[i]);
   GUI.Label(new Rect(24,118,780,32),"Izquierda: Stalker · Centro: Ravager · Derecha: Spitter | Estado: "+CurrentState);GUI.matrix=previous;
  }
 }
}