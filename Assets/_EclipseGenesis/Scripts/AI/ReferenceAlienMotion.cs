using UnityEngine;
using EclipseGenesis.Combat;
namespace EclipseGenesis.AI {
 [DefaultExecutionOrder(210)]
 public sealed class ReferenceAlienMotion : MonoBehaviour {
  [SerializeField] Animator animator;
  [SerializeField] Transform mouth;
  [SerializeField] Material acidMaterial;
  [SerializeField] Mesh dropletMesh;
  [SerializeField] bool corrosive;
  AlienBrain brain;AlienHealth health;string state;float hitUntil;bool spilled;float attackLength=1.625f;
  public string VisualState => state;
  void Awake(){brain=GetComponent<AlienBrain>();health=GetComponent<AlienHealth>();if(animator==null)animator=GetComponentInChildren<Animator>();if(animator!=null){animator.applyRootMotion=false;foreach(var clip in animator.runtimeAnimatorController.animationClips)if(clip.name.EndsWith("_Attack"))attackLength=clip.length;}}
  void OnEnable(){if(health!=null)health.Damaged+=Hit;}
  void OnDisable(){if(health!=null)health.Damaged-=Hit;}
  void Hit(DamageInfo info){hitUntil=Time.time+.28f;if(health!=null&&health.IsAlive)Play("Hit");}
  void Start(){if(corrosive&&mouth!=null)CreateDrips();}
  void LateUpdate(){
   if(brain==null||animator==null)return;
   string wanted;
   float speed=brain.Agent!=null&&brain.Agent.enabled&&brain.Agent.isOnNavMesh?brain.Agent.velocity.magnitude:0;
   if(!health.IsAlive||brain.State==AlienState.Dead)wanted="Death";
   else if(Time.time<hitUntil)wanted="Hit";
   else if(brain.State==AlienState.Attack)wanted="Attack";
   else if(speed>.15f)wanted=(brain.State==AlienState.Chase&&speed>2.6f)?"Run":"Walk";
   else wanted="Idle";
   Play(wanted);
   animator.speed=wanted=="Walk"?Mathf.Clamp(speed/1.8f,.65f,1.4f):wanted=="Run"?Mathf.Clamp(speed/5,.65f,1.35f):wanted=="Attack"?attackLength/Mathf.Max(.3f,health.Data.attackWindup+health.Data.attackRecovery):1;
   if(wanted=="Death"&&!spilled){spilled=true;if(corrosive)Spill();}
  }
  void Play(string name){if(animator==null||state==name)return;state=name;animator.CrossFadeInFixedTime(name,.10f,0,0);}
  void CreateDrips(){
   var go=new GameObject("CorrosiveSaliva");go.transform.SetParent(mouth,false);var p=go.AddComponent<ParticleSystem>();var main=p.main;main.startLifetime=.7f;main.startSpeed=.15f;main.startSize=.025f;main.gravityModifier=.8f;main.maxParticles=20;main.simulationSpace=ParticleSystemSimulationSpace.World;
   var emission=p.emission;emission.rateOverTime=8;
   var shape=p.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.09f;
   var r=go.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Mesh;r.mesh=dropletMesh;r.sharedMaterial=acidMaterial;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
  }
  void Spill(){var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name="SpitterDeath_AcidSpill";Destroy(g.GetComponent<Collider>());g.transform.position=transform.position+Vector3.up*.012f;g.transform.localScale=new Vector3(2.2f,.01f,2.2f);g.GetComponent<Renderer>().sharedMaterial=acidMaterial;Destroy(g,14);}
 }
}