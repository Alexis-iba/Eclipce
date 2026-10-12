using UnityEngine;
using EclipseGenesis.Mission;
using EclipseGenesis.Combat;
namespace EclipseGenesis.World {
 public sealed class OpenZoneCheckpoint : MonoBehaviour {
  static bool unlocked,resume;MissionManager mission;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset(){unlocked=false;resume=false;}
  void Start(){
   mission=MissionManager.Current;if(mission==null||!mission.IsOpenZone)return;
   mission.ObjectiveChanged+=ObjectiveChanged;mission.Ended+=Ended;
   if(resume&&unlocked){var cc=GetComponent<CharacterController>();cc.enabled=false;transform.position=new Vector3(0,.2f,44);cc.enabled=true;GetComponent<PlayerHealth>().Restore();mission.ResumeOpenZoneCheckpoint();resume=false;}
  }
  void ObjectiveChanged(int index,string text){if(index>=2)unlocked=true;}
  void Ended(MissionState state){if(state==MissionState.Defeat)resume=unlocked;else if(state==MissionState.Victory){resume=false;unlocked=false;}}
  void OnDestroy(){if(mission!=null){mission.ObjectiveChanged-=ObjectiveChanged;mission.Ended-=Ended;}}
 }
}