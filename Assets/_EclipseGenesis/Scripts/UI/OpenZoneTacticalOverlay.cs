using UnityEngine;
using UnityEngine.UI;
using EclipseGenesis.Mission;
using EclipseGenesis.AI;
namespace EclipseGenesis.UI {
 public sealed class OpenZoneTacticalOverlay : MonoBehaviour {
  RectTransform map; Text compass; Transform[] buildings; Texture2D tex; Color32[] pixels;
  float refresh; Font font; Canvas canvas; Text rangeText;
  void Start(){
   font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
   var c=new GameObject("OpenZone_TacticalOverlay");c.transform.SetParent(transform,false);canvas=c.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=11;
   var sc=c.AddComponent<CanvasScaler>();sc.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;sc.referenceResolution=new Vector2(1920,1080);
   var g=new GameObject("Radar",typeof(RectTransform),typeof(RawImage));g.transform.SetParent(c.transform,false);map=g.GetComponent<RectTransform>();map.anchorMin=map.anchorMax=new Vector2(0,1);map.pivot=new Vector2(0,1);map.anchoredPosition=new Vector2(40,-40);map.sizeDelta=new Vector2(200,200);
   tex=new Texture2D(128,128,TextureFormat.RGBA32,false);tex.filterMode=FilterMode.Bilinear;g.GetComponent<RawImage>().texture=tex;
   pixels=new Color32[128*128];
   compass=MakeText(c.transform,"Compass",new Vector2(.5f,1),new Vector2(0,-35),new Vector2(600,45),24,TextAnchor.MiddleCenter);
   rangeText=MakeText(c.transform,"Range",new Vector2(0,1),new Vector2(140,-253),new Vector2(200,24),14,TextAnchor.MiddleCenter);rangeText.text="RADAR  /  110 m";
   var objective=transform.Find("HUD_Canvas/Objective");if(objective!=null){var rt=objective.GetComponent<RectTransform>();rt.anchoredPosition=new Vector2(40,-285);}
   buildings=System.Array.FindAll(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None),t=>t.name.StartsWith("Residence_"));
  }
  Text MakeText(Transform p,string name,Vector2 anchor,Vector2 pos,Vector2 size,int fs,TextAnchor align){var g=new GameObject(name,typeof(RectTransform),typeof(Text));g.transform.SetParent(p,false);var rt=g.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=anchor;rt.sizeDelta=size;rt.anchoredPosition=pos;var t=g.GetComponent<Text>();t.font=font;t.fontSize=fs;t.color=new Color(.85f,.91f,.94f);t.alignment=align;return t;}
  void Update(){if(compass==null)return;float yaw=transform.eulerAngles.y;string[] labels={"N","NE","E","SE","S","SW","W","NW"};compass.text=labels[Mathf.RoundToInt(yaw/45)%8]+"    "+Mathf.RoundToInt(yaw).ToString("000")+"°    |    GENESIS";
   if(Time.unscaledTime<refresh)return;refresh=Time.unscaledTime+.25f;Draw();
  }
  void Dot(int x,int y,int radius,Color32 color){for(int j=-radius;j<=radius;j++)for(int i=-radius;i<=radius;i++){int px=x+i,py=y+j;if(px>=0&&px<128&&py>=0&&py<128&&i*i+j*j<=radius*radius)pixels[py*128+px]=color;}}
  Vector2Int Plot(Vector3 p){return new Vector2Int(Mathf.RoundToInt(64+(p.x-transform.position.x)*.58f),Mathf.RoundToInt(64+(p.z-transform.position.z)*.58f));}
  void Draw(){
   for(int y=0;y<128;y++)for(int x=0;x<128;x++){float r=Vector2.Distance(new Vector2(x,y),new Vector2(64,64));pixels[y*128+x]=r>63?new Color32(0,0,0,0):r>61?new Color32(75,151,188,240):new Color32(14,22,25,230);}
   for(int i=3;i<125;i++){var a=Plot(new Vector3(0,0,transform.position.z+(i-64)/.58f));if(a.x>=0&&a.x<128)Dot(a.x,i,2,new Color32(58,67,70,255));}
   foreach(var b in buildings){if(!b.gameObject.activeInHierarchy)continue;var p=Plot(b.position);for(int j=-3;j<=3;j++)for(int i=-4;i<=4;i++)Dot(p.x+i,p.y+j,0,new Color32(101,110,110,255));}
   foreach(var a in Object.FindObjectsByType<AlienHealth>(FindObjectsSortMode.None)){if(!a.IsAlive||Vector3.Distance(transform.position,a.transform.position)>45)continue;var p=Plot(a.transform.position);Dot(p.x,p.y,2,new Color32(238,73,44,255));}
   Vector3 target=new Vector3(0,0,-42);var m=MissionManager.Current;if(m!=null){if(m.ObjectiveIndex==1)target=new Vector3(0,0,44);else if(m.ObjectiveIndex==2)target=new Vector3(-4.6f,0,102.4f);else if(m.ObjectiveIndex==3)target=new Vector3(5,0,98);else if(m.ObjectiveIndex>=5)target=new Vector3(0,0,-94);}
   var goal=Plot(target);Dot(goal.x,goal.y,3,new Color32(235,187,83,255));Dot(64,64,3,new Color32(89,208,245,255));
   float angle=transform.eulerAngles.y*Mathf.Deg2Rad;Dot(64+Mathf.RoundToInt(Mathf.Sin(angle)*7),64+Mathf.RoundToInt(Mathf.Cos(angle)*7),2,new Color32(185,240,255,255));
   tex.SetPixels32(pixels);tex.Apply(false);
  }
  void OnDestroy(){if(tex!=null)Destroy(tex);}
 }
}