using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.AI;
using Unity.AI.Navigation;
namespace EclipseGenesis.EditorTools {
public static class OpenZoneArtPass {
 static Transform root;
 static string folder="Assets/_EclipseGenesis/Art/OpenZoneArt";
 static Material plaster,wood,roof,stone,soil,asphalt,steel,leaf,blue,orange;
 static int meshIndex;
 static System.Random rng=new System.Random(734);
 static Material Material(string name,Color color,float metallic=0,float smooth=.25f) {
  var path=folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(m!=null)return m;
  m=new Material(Shader.Find("HDRP/Lit"));m.name=name;m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);
  AssetDatabase.CreateAsset(m,path);return m;
 }
 static float Rand(float min,float max){return min+(float)rng.NextDouble()*(max-min);}
 static Transform Group(string n,Vector3 pos,Transform parent=null) {var t=new GameObject(n).transform;t.SetParent(parent==null?root:parent,false);t.localPosition=pos;return t;}
 static GameObject Box(Transform p,string n,Vector3 pos,Vector3 size,Material m,bool collision=true) {
  var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;
  if(!collision)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
  g.isStatic=true;return g;
 }
 static GameObject MeshObject(Transform p,string n,Vector3[] verts,int[] tris,Vector2[] uv,Material m,bool collider) {
  var mesh=new Mesh();mesh.name=n;mesh.vertices=verts;mesh.triangles=tris;mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();
  AssetDatabase.CreateAsset(mesh,folder+"/Mesh_"+(meshIndex++)+".asset");
  var g=new GameObject(n);g.transform.SetParent(p,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=m;if(collider)g.AddComponent<MeshCollider>().sharedMesh=mesh;g.isStatic=true;return g;
 }
 static GameObject Surface(Transform p,string n,float x,float z,float w,float d,float y,Material m) {
  return MeshObject(p,n,new[]{new Vector3(x-w/2,y,z-d/2),new Vector3(x-w/2,y,z+d/2),new Vector3(x+w/2,y,z+d/2),new Vector3(x+w/2,y,z-d/2)},new[]{0,1,2,0,2,3},new[]{new Vector2(0,0),new Vector2(0,d/4),new Vector2(w/4,d/4),new Vector2(w/4,0)},m,false);
 }
 static void Label(Transform p,string text,Vector3 pos,float size,Color color) {
  var t=new GameObject("Sign_"+text);t.transform.SetParent(p,false);t.transform.localPosition=pos;t.transform.localRotation=Quaternion.Euler(0,180,0);
  var tm=t.AddComponent<TextMesh>();tm.text=text;tm.characterSize=size;tm.fontSize=64;tm.anchor=TextAnchor.MiddleCenter;tm.color=color;
 }
 static void Roof(Transform p,float w,float d,float y) {
  MeshObject(p,"PitchedRoof",new[]{new Vector3(-w/2,y,-d/2),new Vector3(0,y+2.8f,-d/2),new Vector3(0,y+2.8f,d/2),new Vector3(-w/2,y,d/2),new Vector3(0,y+2.8f,-d/2),new Vector3(w/2,y,-d/2),new Vector3(w/2,y,d/2),new Vector3(0,y+2.8f,d/2)},new[]{0,1,2,0,2,3,4,5,6,4,6,7},new[]{new Vector2(0,0),new Vector2(2,0),new Vector2(2,d/2),new Vector2(0,d/2),new Vector2(0,0),new Vector2(2,0),new Vector2(2,d/2),new Vector2(0,d/2)},roof,true);
  foreach(float z in new[]{-d/2+.3f,d/2-.3f})MeshObject(p,"Gable",new[]{new Vector3(-w/2+.4f,y,z),new Vector3(w/2-.4f,y,z),new Vector3(0,y+2.7f,z)},new[]{0,1,2,2,1,0},new[]{Vector2.zero,Vector2.right,Vector2.up},wood,false);
  for(float x=-w/2;x<w/2;x+=.7f){float ry=y+2.8f*(1-Mathf.Abs(x)/(w/2));Box(p,"RoofStandingSeam",new Vector3(x,ry+.025f,0),new Vector3(.035f,.04f,d),steel,false);}
  Box(p,"Chimney",new Vector3(w*.27f,y+2,1),new Vector3(.9f,2.8f,1),stone);
 }
 static void House(int index,Vector3 pos,float yaw) {
  var h=Group("Residence_"+index,pos);h.localRotation=Quaternion.Euler(0,yaw,0);
  float w=12,d=11,height=4.1f;
  Box(h,"Foundation",new Vector3(0,.12f,0),new Vector3(w,.24f,d),stone);
  var wallMat=index%2==0?plaster:wood;
  Box(h,"BackWall",new Vector3(0,height/2,d/2),new Vector3(w,height,.25f),wallMat);
  Box(h,"FrontLeft",new Vector3(-4,height/2,-d/2),new Vector3(4,height,.25f),wallMat);
  Box(h,"FrontRight",new Vector3(4,height/2,-d/2),new Vector3(4,height,.25f),wallMat);
  Box(h,"EntryLintel",new Vector3(0,3.55f,-d/2),new Vector3(4,1.1f,.25f),wallMat);
  foreach(float x in new[]{-w/2,w/2}) {
   Box(h,"WallSill",new Vector3(x,.55f,0),new Vector3(.25f,1.1f,d),wallMat);
   Box(h,"WallHeader",new Vector3(x,3.5f,0),new Vector3(.25f,1.2f,d),wallMat);
   foreach(float z in new[]{-4.4f,0,4.4f})Box(h,"WallPier",new Vector3(x,2,z),new Vector3(.25f,1.8f,2.2f),wallMat);
   foreach(float z in new[]{-2.2f,2.2f}) {
    Box(h,"WindowSillTrim",new Vector3(x,1.15f,z),new Vector3(.45f,.12f,2.25f),wood,false);
    Box(h,"WindowCross",new Vector3(x,2,z),new Vector3(.15f,1.7f,.07f),wood,false);
    Box(h,"WindowCross",new Vector3(x,2,z),new Vector3(.15f,.07f,2.2f),wood,false);
   }
  }
  for(float y=.45f;y<height;y+=.28f) {
   foreach(float x in new[]{-w/2-.14f,w/2+.14f})Box(h,"Weatherboard",new Vector3(x,y,0),new Vector3(.04f,.045f,d),wood,false);
   Box(h,"WeatherboardFrontLeft",new Vector3(-4,y,-d/2-.14f),new Vector3(4,.045f,.04f),wood,false);
   Box(h,"WeatherboardFrontRight",new Vector3(4,y,-d/2-.14f),new Vector3(4,.045f,.04f),wood,false);
  }
  foreach(float x in new[]{-6.1f,6.1f})foreach(float z in new[]{-5.6f,5.6f})Box(h,"CornerTrim",new Vector3(x,2.1f,z),new Vector3(.16f,4.2f,.16f),wood,false);
  Roof(h,13,12,4.2f);
  Box(h,"Porch",new Vector3(0,.12f,-7),new Vector3(10,.24f,3),wood);
  Box(h,"PorchCanopy",new Vector3(0,3,-7),new Vector3(10,.15f,3),roof);
  foreach(float x in new[]{-4.6f,4.6f})Box(h,"PorchPost",new Vector3(x,1.6f,-8.3f),new Vector3(.2f,3.2f,.2f),wood);
  Box(h,"InteriorPartition",new Vector3(1,2,2),new Vector3(.15f,4,7),plaster);
  // kitchen and worn furniture
  for(int i=0;i<4;i++) {Box(h,"KitchenCabinet",new Vector3(-4.5f+i,.7f,4.7f),new Vector3(.95f,1.4f,1),wood);Box(h,"CabinetHandle",new Vector3(-4.5f+i,.95f,4.16f),new Vector3(.25f,.04f,.06f),steel,false);}
  Box(h,"Countertop",new Vector3(-3,1.45f,4.7f),new Vector3(4,.1f,1.15f),stone);
  Box(h,"SofaSeat",new Vector3(-4,.5f,-1),new Vector3(3,.65f,1.15f),soil);
  Box(h,"SofaBack",new Vector3(-4,1,-.45f),new Vector3(3,1,.3f),soil);
  foreach(float x in new[]{-5.4f,-2.6f})Box(h,"SofaArm",new Vector3(x,.8f,-1),new Vector3(.3f,.9f,1.2f),soil);
  Box(h,"Table",new Vector3(-3,.55f,-3),new Vector3(2,.12f,1),wood);
  foreach(float x in new[]{-3.8f,-2.2f})Box(h,"TableLeg",new Vector3(x,.25f,-3),new Vector3(.1f,.5f,.8f),steel);
  Box(h,"MilitaryStorage",new Vector3(4,.6f,3),new Vector3(1.6f,1.2f,1.5f),steel);
  var lg=Group("WarmInteriorLight",new Vector3(0,3.5f,0),h).gameObject;var l=lg.AddComponent<Light>();l.type=LightType.Point;l.range=9;l.intensity=220;l.color=new Color(1,.76f,.48f);lg.AddComponent<HDAdditionalLightData>();
  Label(h,"GENESIS  /  "+index,new Vector3(0,3.6f,-5.67f),.12f,new Color(.8f,.8f,.7f));
 }
 static float Height(float x,float z) {
  float outside=Mathf.Clamp01((Mathf.Abs(x)-65)/75);
  float south=Mathf.Clamp01((-z-105)/100);
  return -.05f+Mathf.Pow(Mathf.Max(outside,south),1.5f)*(14+Mathf.PerlinNoise(x*.009f+3,z*.009f+7)*40);
 }
 static void TerrainMesh() {
  int n=129;var v=new Vector3[n*n];var uv=new Vector2[v.Length];var tr=new List<int>();
  for(int z=0;z<n;z++)for(int x=0;x<n;x++){float wx=-260+x*4.0625f,wz=-300+z*4.0625f;v[z*n+x]=new Vector3(wx,Height(wx,wz),wz);uv[z*n+x]=new Vector2(wx/6,wz/6);if(x<n-1&&z<n-1){int a=z*n+x;tr.AddRange(new[]{a,a+n,a+1,a+1,a+n,a+n+1});}}
  MeshObject(root,"SculptedLandscape",v,tr.ToArray(),uv,soil,true);
 }
 static void Tree(Vector3 p,int i) {
  var t=Group("Pine_"+i,p);
  var trunk=GameObject.CreatePrimitive(PrimitiveType.Cylinder);trunk.name="Trunk";trunk.transform.SetParent(t,false);trunk.transform.localPosition=new Vector3(0,3,0);trunk.transform.localScale=new Vector3(.3f,3,.3f);trunk.GetComponent<Renderer>().sharedMaterial=wood;trunk.isStatic=true;
  var vs=new List<Vector3>();var ts=new List<int>();var uv=new List<Vector2>();
  for(int level=0;level<5;level++) {float y=2+level*.95f,rad=2.8f-level*.4f;int start=vs.Count;vs.Add(new Vector3(0,y+2.6f,0));uv.Add(new Vector2(.5f,1));
   for(int j=0;j<13;j++){float a=j*Mathf.PI*2/12;vs.Add(new Vector3(Mathf.Cos(a)*rad,y+Rand(-.25f,.25f),Mathf.Sin(a)*rad));uv.Add(new Vector2(j/12f,0));if(j<12)ts.AddRange(new[]{start,start+j+2,start+j+1});}
  }
  MeshObject(t,"ConiferCanopy",vs.ToArray(),ts.ToArray(),uv.ToArray(),leaf,false);
 }
 public static void Build() {
  if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first");
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.name!="Eclipse_OpenZone")throw new InvalidOperationException("Open Eclipse_OpenZone first");
  string backup=AssetDatabase.GenerateUniqueAssetPath("Assets/_EclipseGenesis/Scenes/Backups/OpenZone_PreArt.unity");
  EditorSceneManager.SaveScene(scene,backup,true);
  if(GameObject.Find("OpenZone_ArtPass")!=null)throw new InvalidOperationException("Art pass already exists");
  if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/_EclipseGenesis/Art","OpenZoneArt");
  root=new GameObject("OpenZone_ArtPass").transform;
  plaster=Material("WeatheredPlaster",new Color(.48f,.44f,.34f));wood=Material("AgedTimber",new Color(.22f,.15f,.095f));roof=Material("OxidizedRoof",new Color(.105f,.13f,.14f),.5f,.22f);stone=Material("Stone",new Color(.33f,.32f,.28f));soil=Material("DryGround",new Color(.25f,.23f,.15f));asphalt=Material("Road",new Color(.09f,.095f,.1f),0,.12f);steel=Material("Steel",new Color(.09f,.105f,.105f),.65f,.4f);leaf=Material("PineNeedles",new Color(.12f,.17f,.075f));blue=Material("AlienBlue",new Color(.035f,.11f,.2f),.6f,.4f);orange=Material("Ember",new Color(.5f,.13f,.01f));
  blue.SetColor("_EmissiveColor",new Color(.1f,.4f,1)*30);orange.SetColor("_EmissiveColor",new Color(1,.18f,.025f)*18);
  var old=GameObject.Find("OpenZone_Exterior");old.SetActive(false);
  // retain useful cover and the truck from previous pass
  foreach(Transform t in old.transform)if(t.name=="DamagedMilitaryTransport"||t.name=="Barricade"||t.name=="AlienNest"||t.name=="Supply"){var copy=UnityEngine.Object.Instantiate(t.gameObject,root);copy.SetActive(true);}
  TerrainMesh();Surface(root,"RoadNorth",0,-48,9,96,.035f,asphalt);Surface(root,"CrossRoad",0,-52,145,7,.04f,asphalt);
  for(int z=-96;z<0;z+=9)Box(root,"LaneMark",new Vector3(0,.055f,z),new Vector3(.14f,.02f,3),plaster,false);
  for(int i=0;i<8;i++){float x=i%2==0?-26:26;float z=-20-(i/2)*24;House(i+1,new Vector3(x,0,z),i%2==0?90:-90);Surface(root,"Sidewalk",x,-52,17,100,.08f,stone);}
  for(int i=0;i<65;i++){float x=(i%2==0?-1:1)*Rand(48,175),z=Rand(-180,115);if(x<0&&z>25&&z<115&&x>-42)continue;Tree(new Vector3(x,Height(x,z),z),i);}
  for(int i=0;i<100;i++){float x=Rand(-160,160),z=Rand(-175,-5);if(Mathf.Abs(x)<40)continue;var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name="IrregularBoulder";g.transform.SetParent(root,false);g.transform.position=new Vector3(x,Height(x,z)+.4f,z);g.transform.localScale=new Vector3(Rand(1,4),Rand(.7f,2),Rand(1,3));g.GetComponent<Renderer>().sharedMaterial=stone;g.isStatic=true;}
  // Utility poles, drooping wires and road debris
  for(int i=0;i<7;i++){float z=-10-i*14;Box(root,"UtilityPole",new Vector3(10,4.5f,z),new Vector3(.2f,9,.2f),wood);Box(root,"Crossbar",new Vector3(10,8.7f,z),new Vector3(2,.12f,.12f),wood,false);
   if(i<6)for(int wire=0;wire<2;wire++){var go=new GameObject("OverheadWire");go.transform.SetParent(root,false);var lr=go.AddComponent<LineRenderer>();lr.sharedMaterial=steel;lr.startWidth=lr.endWidth=.022f;lr.positionCount=13;for(int j=0;j<13;j++)lr.SetPosition(j,new Vector3(9.3f+wire*1.4f,8.7f-Mathf.Sin(j/12f*Mathf.PI)*.7f,z-j/12f*14));}
  }
  for(int i=0;i<80;i++){float x=Rand(-16,16);if(Mathf.Abs(x)<4)continue;Box(root,"Rubble",new Vector3(x,.1f,Rand(-95,-2)),new Vector3(Rand(.15f,.65f),Rand(.1f,.3f),Rand(.1f,.6f)),stone);}
  // Military perimeter gateway framing preserved Genesis lab
  foreach(float x in new[]{-13f,13f}){Box(root,"SecurityPylon",new Vector3(x,4,-1),new Vector3(2,8,2),stone);Box(root,"SecurityTrim",new Vector3(x,4,-2.03f),new Vector3(.15f,6,.05f),blue,false);}
  Box(root,"GenesisGateway",new Vector3(0,7.4f,-1),new Vector3(28,1.2f,2),steel);
  Label(root,"GENESIS  /  RESEARCH DIVISION",new Vector3(0,7.5f,-2.05f),.23f,Color.white);
  // Original alien vessel silhouette overhead: plated disc with underside energy core
  var ship=Group("AlienCarrier",new Vector3(-55,92,220));ship.localRotation=Quaternion.Euler(9,20,-12);
  var disc=GameObject.CreatePrimitive(PrimitiveType.Sphere);disc.name="ArmouredHull";disc.transform.SetParent(ship,false);disc.transform.localScale=new Vector3(145,14,88);disc.GetComponent<Renderer>().sharedMaterial=steel;UnityEngine.Object.DestroyImmediate(disc.GetComponent<Collider>());
  for(int i=0;i<24;i++){float a=i*Mathf.PI*2/24;var g=Box(ship,"HullRib",new Vector3(Mathf.Cos(a)*62,-2,Mathf.Sin(a)*35),new Vector3(12,3,8),steel,false);g.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);Box(ship,"EnergyVent",new Vector3(Mathf.Cos(a)*60,-4.3f,Mathf.Sin(a)*35),new Vector3(4,.2f,2),blue,false);}
  var beam=new GameObject("CarrierBeam");beam.transform.SetParent(root,false);var line=beam.AddComponent<LineRenderer>();line.sharedMaterial=blue;line.positionCount=2;line.SetPosition(0,new Vector3(-55,84,220));line.SetPosition(1,new Vector3(-55,0,220));line.startWidth=2;line.endWidth=5;
  var player=GameObject.Find("EG_Player");player.transform.position=new Vector3(0,.2f,-95);
  var volume=GameObject.Find("PostProcess_Volume").GetComponent<Volume>();var shadows=volume.sharedProfile.Add<HDShadowSettings>(true);AssetDatabase.AddObjectToAsset(shadows,volume.sharedProfile);shadows.maxShadowDistance.Override(90);
  EditorUtility.SetDirty(volume.sharedProfile);
  Bake();
  EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
 }
 public static void Bake() {
  var surface=UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
  var moving=UnityEngine.Object.FindObjectsByType<EclipseGenesis.World.AutoDoor>(FindObjectsSortMode.None).SelectMany(d=>d.GetComponentsInChildren<Collider>()).Where(c=>!c.isTrigger&&c.enabled).ToArray();
  var agents=UnityEngine.Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);var p=GameObject.Find("EG_Player");var path=AssetDatabase.GetAssetPath(surface.navMeshData);
  try {foreach(var c in moving)c.enabled=false;foreach(var a in agents)a.gameObject.SetActive(false);p.SetActive(false);surface.RemoveData();surface.navMeshData=null;surface.BuildNavMesh();var old=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);EditorUtility.CopySerialized(surface.navMeshData,old);surface.RemoveData();surface.navMeshData=old;surface.AddData();EditorUtility.SetDirty(old);}
  finally {foreach(var c in moving)c.enabled=true;foreach(var a in agents)a.gameObject.SetActive(true);p.SetActive(true);}
 }
}
}