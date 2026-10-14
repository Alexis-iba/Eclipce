using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using EclipseGenesis.Mission;
using EclipseGenesis.World;
using EclipseGenesis.AI;

namespace EclipseGenesis.EditorTools
{
    public static class ReferenceWorldAssembly
    {
        static void Set(UnityEngine.Object o,string field,UnityEngine.Object value) { var so=new SerializedObject(o);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo(); }
        static GameObject Box(Transform p,string n,Vector3 pos,Vector3 size,Material m) { var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;g.isStatic=true;return g; }
        static TriggerVolume Trigger(Transform p,string n,Vector3 pos,Vector3 size) { var g=new GameObject(n);g.transform.SetParent(p,false);g.transform.position=pos;var v=g.AddComponent<TriggerVolume>();v.Size=size;return v; }
        public static void Assemble()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit play first");
            var world=SceneManager.GetSceneByName("Eclipse_Genesis_ReferenceWorld");
            if(!world.IsValid())throw new InvalidOperationException("ReferenceWorld must be loaded");
            if(GameObject.Find("ReferenceWorld_GenesisBase")!=null)return;
            var source=SceneManager.GetSceneByName("Genesis_Research_Facility");
            if(!source.IsValid())source=EditorSceneManager.OpenScene("Assets/_EclipseGenesis/Scenes/Genesis_Research_Facility.unity",OpenSceneMode.Additive);
            SceneManager.SetActiveScene(world);
            var main=world.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MissionManager>(true)).Single();
            var player=world.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EclipseGenesis.Combat.PlayerHealth>(true)).Single();
            var root=new GameObject("ReferenceWorld_GenesisBase").transform;
            var importedMission=source.GetRootGameObjects().First(g=>g.name=="Mission_PrimerContacto");
            UnityEngine.Object.DestroyImmediate(importedMission.GetComponent<MissionManager>());
            foreach(var g in source.GetRootGameObjects())
            {
                if(g.name!="Facility"&&g!=importedMission&&g.name!="PostProcess_Volume")continue;
                SceneManager.MoveGameObjectToScene(g,world);g.transform.SetParent(root,true);
            }
            root.position=new Vector3(0,0,145);
            EditorSceneManager.CloseScene(source,true);
            var local=root.GetComponentInChildren<Volume>();
            if(local!=null){local.isGlobal=false;local.priority=5;var c=local.gameObject.AddComponent<BoxCollider>();c.isTrigger=true;c.center=new Vector3(-7.5f,4,57);c.size=new Vector3(55,10,116);local.blendDistance=3;}
            var all=root.GetComponentsInChildren<Transform>(true);
            T Find<T>(string n) where T:Component=>all.First(t=>t.name==n).GetComponent<T>();
            var data=Find<Terminal>("Terminal_Emergency");
            var power=UnityEngine.Object.Instantiate(data.gameObject, data.transform.parent);power.name="Terminal_PowerGrid";power.transform.position=new Vector3(5,0,243);
            var pso=new SerializedObject(power.GetComponent<Terminal>());pso.FindProperty("prompt").stringValue="Restablecer energía de seguridad";pso.ApplyModifiedPropertiesWithoutUndo();
            foreach(var door in root.GetComponentsInChildren<AutoDoor>())door.SetLocked(false);
            var entrance=world.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TriggerVolume>(true)).First(t=>t.name=="Obj2_TowardsGenesis");entrance.transform.position=new Vector3(0,2,150);
            Set(main,"player",player);Set(main,"securityDoorVolume",entrance);Set(main,"emergencyTerminal",data);Set(main,"powerTerminal",power.GetComponent<Terminal>());
            Set(main,"securityDoor",Find<AutoDoor>("Door_BlastSealed"));Set(main,"exitDoor",Find<AutoDoor>("Door_Exit"));
            Set(main,"researchRoomVolume",Trigger(root,"ReturnToExterior",new Vector3(0,2,139),new Vector3(9,5,5)));
            Set(main,"extractionVolume",Trigger(root,"CrashZone_Extraction",new Vector3(62,2,272),new Vector3(14,5,14)));
            var wave=new GameObject("Genesis_DefenseWave").transform;wave.SetParent(root,false);
            var oldAliens=root.GetComponentsInChildren<AlienHealth>();
            var aliens=new AlienHealth[oldAliens.Length];
            for(int i=0;i<oldAliens.Length;i++)
            {
                string species=i==0?"Ravager":i==1?"Stalker":"Spitter";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_EclipseGenesis/Prefabs/Aliens/ReferenceV1/EG_Alien_"+species+"_Reference.prefab");
                var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab,wave);g.transform.SetPositionAndRotation(oldAliens[i].transform.position,oldAliens[i].transform.rotation);aliens[i]=g.GetComponent<AlienHealth>();oldAliens[i].gameObject.SetActive(false);
            }
            var ms=new SerializedObject(main);var list=ms.FindProperty("aliens");list.arraySize=aliens.Length;for(int i=0;i<aliens.Length;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=aliens[i];ms.ApplyModifiedPropertiesWithoutUndo();Set(main,"defenseWave",wave.gameObject);
            var terrain=Terrain.activeTerrain;var td=UnityEngine.Object.Instantiate(terrain.terrainData);td.name="ReferenceWorld_Terrain";
            AssetDatabase.CreateAsset(td,"Assets/_EclipseGenesis/Art/Generated/TerrainOpenWorld/ReferenceWorld_Terrain.asset");terrain.terrainData=td;terrain.GetComponent<TerrainCollider>().terrainData=td;
            var heights=td.GetHeights(0,0,td.heightmapResolution,td.heightmapResolution);int count=td.heightmapResolution;
            for(int z=0;z<count;z++)for(int x=0;x<count;x++)
            {
                float wx=terrain.transform.position.x+x*td.size.x/(count-1),wz=terrain.transform.position.z+z*td.size.z/(count-1);
                float distance=Mathf.Max(-43-wx,wx-76,135-wz,wz-291);
                float weight=1-Mathf.Clamp01(distance/12);if(weight<=0)continue;
                heights[z,x]=Mathf.Lerp(heights[z,x],(.0f-terrain.transform.position.y)/td.size.y,weight);
            }
            td.SetHeights(0,0,heights);
            var concrete=AssetDatabase.LoadAssetAtPath<Material>("Assets/_EclipseGenesis/Materials/Facility/M_Wall_PanelSteel.mat");
            var steel=AssetDatabase.LoadAssetAtPath<Material>("Assets/_EclipseGenesis/Materials/Facility/M_Pipe_Steel.mat");
            var asphalt=AssetDatabase.LoadAssetAtPath<Material>("Assets/_EclipseGenesis/Materials/PolyHaven/M_PH_asphalt_02.mat");
            Box(root,"BaseAccessRoad",new Vector3(0,-.12f,-13),new Vector3(7.4f,.2f,30),asphalt);
            Box(root,"BaseRoof",new Vector3(-7.5f,8.6f,57),new Vector3(55,.3f,116),concrete);
            Box(root,"FacadeWest",new Vector3(-17.5f,4.3f,-.8f),new Vector3(31,8.6f,.35f),concrete);
            Box(root,"FacadeEast",new Vector3(10.2f,4.3f,-.8f),new Vector3(16,8.6f,.35f),concrete);
            Box(root,"EntranceLintel",new Vector3(0,6.8f,-.8f),new Vector3(4.2f,3.6f,.35f),concrete);
            for(int i=0;i<6;i++){Box(root,"RoofVent",new Vector3(-20+(i%3)*14,9.2f,18+(i/3)*47),new Vector3(3,1.2f,4),steel);}
            var sign=new GameObject("GENESIS_ExteriorSign");sign.transform.SetParent(root,false);sign.transform.localPosition=new Vector3(0,5.1f,-1.05f);sign.transform.localRotation=Quaternion.Euler(0,180,0);var text=sign.AddComponent<TextMesh>();text.text="GENESIS  /  RESEARCH DIVISION";text.fontSize=70;text.characterSize=.05f;text.anchor=TextAnchor.MiddleCenter;text.color=Color.white;
            var pad=Box(root,"Extraction_Helipad",new Vector3(62,.015f,127),new Vector3(16,.08f,16),concrete);
            var h=new GameObject("ExtractionMark");h.transform.SetParent(root,false);h.transform.localPosition=new Vector3(62,.07f,127);h.transform.localRotation=Quaternion.Euler(90,0,0);var ht=h.AddComponent<TextMesh>();ht.text="H";ht.fontSize=90;ht.characterSize=.12f;ht.anchor=TextAnchor.MiddleCenter;ht.color=Color.white;
            EditorSceneManager.MarkSceneDirty(world);EditorSceneManager.SaveScene(world);AssetDatabase.SaveAssets();
        }
        public static void BakeNavigation()
        {
            var disabled=UnityEngine.Object.FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsInactive.Include,FindObjectsSortMode.None).SelectMany(a=>a.GetComponentsInChildren<Collider>(true)).Concat(UnityEngine.Object.FindObjectsByType<AutoDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None).SelectMany(d=>d.GetComponentsInChildren<Collider>(true))).Concat(UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None).Cast<Collider>()).Where(c=>c.enabled).Distinct().ToArray();
            foreach(var c in disabled)c.enabled=false;
            var surfaces=UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);foreach(var s in surfaces)s.RemoveData();
            var surface=surfaces.First();surface.collectObjects=CollectObjects.All;surface.useGeometry=UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();
            foreach(var c in disabled)c.enabled=true;
            string path="Assets/_EclipseGenesis/Scenes/OpenWorld/ReferenceWorld_NavMesh.asset";
            var existing=AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(path);
            if(existing==null)AssetDatabase.CreateAsset(surface.navMeshData,path);
            else { EditorUtility.CopySerialized(surface.navMeshData,existing);surface.RemoveData();surface.navMeshData=existing;surface.AddData();EditorUtility.SetDirty(existing); }
            EditorUtility.SetDirty(surface);EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
    }
}

