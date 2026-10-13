using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using EclipseGenesis.Combat;
namespace EclipseGenesis.AI {
 public sealed class SpitterAttack : MonoBehaviour {
  [SerializeField] Material plasmaMaterial;
  AlienBrain brain;AlienHealth health;float nextShot;
  void Awake(){brain=GetComponent<AlienBrain>();health=GetComponent<AlienHealth>();}
  void OnEnable(){if(brain!=null)brain.StateChanged+=StateChanged;}
  void OnDisable(){if(brain!=null)brain.StateChanged-=StateChanged;}
  void StateChanged(AlienState before,AlienState after){if(after==AlienState.Attack&&Time.time>=nextShot){nextShot=Time.time+2.2f;StartCoroutine(Spit());}}
  IEnumerator Spit(){yield return new WaitForSeconds(.25f);if(health==null||!health.IsAlive)yield break;
   var player=Object.FindFirstObjectByType<PlayerHealth>();if(player==null||!player.IsAlive)yield break;
   var origin=transform.position+Vector3.up*1.1f+transform.forward*.9f;
   var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name="Spitter_Plasma";Destroy(g.GetComponent<Collider>());g.transform.position=origin;g.transform.localScale=Vector3.one*.22f;
   g.GetComponent<Renderer>().sharedMaterial=plasmaMaterial;g.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
   var projectile=g.AddComponent<OrganicProjectile>();projectile.Init((player.transform.position+Vector3.up*.9f-origin).normalized*16,gameObject,18);
  }
 }
 public sealed class OrganicProjectile : MonoBehaviour {
  Vector3 velocity;GameObject source;float damage;float expires;
  public void Init(Vector3 v,GameObject owner,float d){velocity=v;source=owner;damage=d;expires=Time.time+4;}
  void Update(){if(Time.time>expires){Destroy(gameObject);return;}var step=velocity*Time.deltaTime;RaycastHit hit;
   if(Physics.SphereCast(transform.position,.12f,step.normalized,out hit,step.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)){
    if(hit.collider.gameObject==source||hit.collider.transform.IsChildOf(source.transform)){transform.position+=step;return;}
    var p=hit.collider.GetComponentInParent<PlayerHealth>();if(p!=null)p.ApplyDamage(new DamageInfo(damage,hit.point,velocity.normalized,source));Destroy(gameObject);return;
   }
   transform.position+=step;
  }
 }
}