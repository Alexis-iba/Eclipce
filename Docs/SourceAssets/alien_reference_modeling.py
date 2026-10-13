
import bpy, math, random, os, json
from mathutils import Vector
from math import sin,cos,pi
random.seed(641)
for ob in list(bpy.data.objects):
 if ob.name in ["Rig_Stalker","Rig_Ravager","Rig_Spitter","SK_Stalker_Reference","SK_Ravager_Reference","SK_Spitter_Reference"]:bpy.data.objects.remove(ob,do_unlink=True)
for a in list(bpy.data.actions):
 if any(a.name.startswith(s+"_") for s in ["Stalker","Ravager","Spitter"]):bpy.data.actions.remove(a)
scene=bpy.data.scenes.new("EG_AlienReferenceV2")
bpy.context.window.scene=scene
out="C:/Users/ibarr/EclipseGenesis/Assets/_EclipseGenesis/Art/Imported/Aliens/ReferenceV1"
os.makedirs(out,exist_ok=True)
texture="C:/Users/ibarr/EclipseGenesis/Assets/_EclipseGenesis/Art/OpenZoneArt/AlienChitin.png"
def material(name,color,metal=.4,rough=.28,emission=0):
 m=bpy.data.materials.get(name) or bpy.data.materials.new(name);m.use_nodes=True
 p=m.node_tree.nodes.get("Principled BSDF");p.inputs["Base Color"].default_value=(*color,1);p.inputs["Metallic"].default_value=metal;p.inputs["Roughness"].default_value=rough
 if emission:p.inputs["Emission Color"].default_value=(*color,1);p.inputs["Emission Strength"].default_value=emission
 return m
shell=material("Reference_ObsidianChitin",(.095,.08,.085),.68,.27)
n=shell.node_tree.nodes.new("ShaderNodeTexImage");n.image=bpy.data.images.load(texture,check_existing=True);shell.node_tree.links.new(n.outputs["Color"],shell.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
edge=material("Reference_BoneEdges",(.075,.064,.058),.72,.24)
flesh=material("Reference_OrganicJoint",(.07,.025,.028),.15,.42)
dark=material("Reference_Mouth",(.008,.006,.008),0,.4)
blue=material("Reference_StalkerBlue",(.025,.35,1),.4,.22,10)
orange=material("Reference_RavagerOrange",(1,.09,.008),.45,.22,8)
green=material("Reference_SpitterGreen",(.33,.9,.015),.1,.18,2.5)
sac=material("Reference_ToxicMembrane",(.14,.37,.009),.2,.15,.7)
parts=[];bones={}
def objmesh(name,verts,faces,mat,bone,uv=None):
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
 ob=bpy.data.objects.new(name,mesh);scene.collection.objects.link(ob);ob.data.materials.append(mat)
 if uv:
  layer=mesh.uv_layers.new(name="UVMap")
  for poly in mesh.polygons:
   for li in poly.loop_indices:layer.data[li].uv=uv[mesh.loops[li].vertex_index]
 for p in mesh.polygons:p.use_smooth=True
 group=ob.vertex_groups.new(name=bone);group.add(list(range(len(verts))),1,"REPLACE")
 parts.append(ob);return ob
def organic(name,c,s,mat,bone,ridges=.05,rings=12,segs=24):
 if mat==shell and any(k in name for k in ["Plate","Armor","Shoulder","Pelvis"]):return armored_patch(name,c,s,mat,bone)
 verts=[];uv=[];faces=[]
 for j in range(rings+1):
  ph=pi*j/rings
  for i in range(segs):
   a=2*pi*i/segs
   w=1+ridges*(sin(a*5+ph*7)*.6+sin(a*11-ph*3)*.4)
   verts.append((c[0]+s[0]*sin(ph)*cos(a)*w,c[1]+s[1]*sin(ph)*sin(a)*w,c[2]+s[2]*cos(ph)*w));uv.append((i/segs,j/rings))
 for j in range(rings):
  for i in range(segs):k=j*segs+i;nn=j*segs+(i+1)%segs;faces.append((k+segs,nn+segs,nn,k))
 return objmesh(name,verts,faces,mat,bone,uv)
def tube(name,points,radii,mat,bone,segs=10):
 points=[Vector(p) for p in points];verts=[];faces=[];uv=[]
 for j,p in enumerate(points):
  direction=(points[min(j+1,len(points)-1)]-points[max(0,j-1)]).normalized()
  x=direction.cross(Vector((0,0,1)))
  if x.length<.1:x=direction.cross(Vector((1,0,0)))
  x.normalize();y=direction.cross(x).normalized()
  for i in range(segs):
   a=2*pi*i/segs;q=p+(x*cos(a)+y*sin(a))*radii[j];verts.append(q);uv.append((i/segs,j/(len(points)-1)))
 for j in range(len(points)-1):
  for i in range(segs):a=j*segs+i;b=j*segs+(i+1)%segs;faces.append((a,b,b+segs,a+segs))
 faces.extend([tuple(reversed(range(segs))),tuple(range((len(points)-1)*segs,len(points)*segs))])
 return objmesh(name,verts,faces,mat,bone,uv)
def spike(name,a,b,c,r,mat,bone):
 tube(name,[a,b,c],[r,r*.62,.002],mat,bone,10)
def bone(name,head,tail,parent=None):
 bones[name]=(head,tail,parent)
def limb(name,a,k,b,parent,scale=.1):
 bone(name,a,k,parent);bone(name+"_Lower",k,b,name);bone(name+"_Hand",b,tuple(Vector(b)+Vector((0,-.23,-.04))),name+"_Lower")
 tube(name+"_Tendon",[a,k],[scale*.72,scale*.5],flesh,name)
 tube(name+"_Shin",[k,b],[scale*.50,scale*.35],flesh,name+"_Lower")
 va=Vector(a);vk=Vector(k);vb=Vector(b)
 for j in range(3):
  c=va.lerp(vk,(j+.5)/3);s=scale*(1.2-j*.14)
  organic(name+"_ArmorUpper"+str(j),c,(s,s*.85,(va-vk).length/4),shell,name,.07)
 for j in range(3):
  c=vk.lerp(vb,(j+.5)/3);s=scale*(.9-j*.13)
  organic(name+"_ArmorLower"+str(j),c,(s,s*.8,(vk-vb).length/4),shell,name+"_Lower",.07)
 organic(name+"_Joint",k,(scale*.65,scale*.65,scale*.65),edge,name+"_Lower")
 for j in range(4):
  c=Vector(b)+Vector(((j-1.5)*scale*.52,-.04,-.02));tip=c+Vector(((j-1.5)*.025,-scale*2.2,-scale*1.8))
  spike(name+"_Claw"+str(j),c,c+Vector((0,-scale*1.6,-scale*.15)),tip,scale*.25,edge,name+"_Hand")
def face(c,species,scale,mat):
 x,y,z=c
 organic("Skull",(x,y-(.17*scale if species=="Stalker" else 0),z),((.21 if species=="Stalker" else .27)*scale,(.62 if species=="Stalker" else .43)*scale,.26*scale),shell,"Head",.06)
 organic("CranialCrest",(x,y+.12*scale,z+.16*scale),(.17*scale,.5*scale,.18*scale),shell,"Head",.05)
 for side in [-1,1]:
  organic("EyeSensor",(x+side*.19*scale,y-.28*scale,z+.07*scale),(.025*scale,.12*scale,.014*scale),mat,"Head",0,8,16)
  spike("CheekBlade",(x+side*.23*scale,y-.06*scale,z+.12*scale),(x+side*.37*scale,y-.25*scale,z+.08*scale),(x+side*.31*scale,y-.51*scale,z-.14*scale),.09*scale,shell,"Head")
  spike("CrownHorn",(x+side*.17*scale,y+.1*scale,z+.23*scale),(x+side*.22*scale,y+.38*scale,z+.33*scale),(x+side*.17*scale,y+.70*scale,z+.48*scale),.09*scale,edge,"Head")
 organic("Maw",(x,y-.36*scale,z-.12*scale),(.18*scale,.10*scale,.14*scale),dark,"Head",0)
 for i in range(9):
  xx=x+(i-4)*.035*scale
  spike("UpperTooth",(xx,y-.43*scale,z-.045*scale),(xx,y-.45*scale,z-.12*scale),(xx,y-.43*scale,z-.20*scale),.022*scale,edge,"Head")
  spike("LowerTooth",(xx,y-.42*scale,z-.24*scale),(xx,y-.45*scale,z-.17*scale),(xx,y-.43*scale,z-.11*scale),.017*scale,edge,"Head")
def plates(center,width,height,boneName,glow,rows=5):
 x,y,z=center
 for j in range(rows):
  zz=z+height*(.45-j/(rows-1))
  for side in [-1,1]:
   organic("OverlappingPlate",(x+side*width*.35,y-.10,zz),(width*.42,.20,height/(rows-1)*1.05),shell,boneName,.09)
   tube("EnergyFissure",[(x+side*.03,y-.23,zz+.08),(x+side*width*.18,y-.29,zz),(x+side*width*.42,y-.23,zz-.07)],[.015,.021,.009],glow,boneName,6)

def armored_patch(name,c,s,mat,bone):
 outline=[(-.75,.24),(-.5,.82),(.12,1),(.78,.48),(.94,-.05),(.24,-1.05),(-.35,-.75),(-.90,-.18)]
 verts=[];uv=[];faces=[]
 # two beveled perimeter rings and an irregular raised central ridge
 for factor,depth in [(1,0),(.78,-.36),(.43,-.93)]:
  for x,z in outline:verts.append((c[0]+x*s[0],c[1]+depth*s[1],c[2]+z*s[2]));uv.append((x*.43+.5,z*.43+.5))
 for ring in range(2):
  for j in range(8):a=ring*8+j;b=ring*8+(j+1)%8;faces.append((a,b,b+8,a+8))
 verts.append((c[0]-.06*s[0],c[1]-1.12*s[1],c[2]+.08*s[2]));uv.append((.5,.5))
 for j in range(8):faces.append((16+j,16+(j+1)%8,24))
 verts.append((c[0],c[1]+.25*s[1],c[2]));uv.append((.5,.5))
 for j in range(8):faces.append(((j+1)%8,j,25))
 ob=objmesh(name,verts,faces,mat,bone,uv)
 return ob

def create(species):
 global parts,bones
 parts=[];bones={}
 isR=species=="Ravager";isS=species=="Spitter";glow=orange if isR else green if isS else blue
 height=3.5 if isR else 2.5 if isS else 2.0
 tz=2.22 if isR else 1.3 if isS else 1.28
 bone("Root",(0,0,0),(0,0,.35))
 bone("Torso",(0,0,tz),(0,0,tz+.35),"Root")
 bone("Head",(0,-.25,tz+.25),(0,-.65,tz+.25),"Torso")
 if isS:
  organic("Thorax",(0,0,1.30),(.57,.86,.43),shell,"Torso",.08)
  face((0,-1.02,1.39),species,1.35,green)
  for i,(x,y,z,r) in enumerate([(-.43,.15,1.75,.45),(.43,.15,1.75,.45),(0,.48,2.05,.51),(0,-.08,1.96,.39)]):
   name="Sac_"+str(i);bone(name,(x,y,z),(x,y,z+.2),"Torso")
   organic("ToxicSac_"+str(i),(x,y,z),(r,r*.85,r),sac,name,.04,16,32)
   for j in range(4):
    a=j*pi/2
    tube("MembraneVein",[(x+cos(a)*r*.91,y+sin(a)*r*.8,z-.25),(x+cos(a)*r,y+sin(a)*r*.9,z),(x+cos(a)*r*.68,y+sin(a)*r*.66,z+.33)],[.014,.023,.012],edge,name,6)
   for j in range(5):
    a=j*2*pi/5
    organic("PlasmaPocket",(x+cos(a)*r*.55,y-sin(a)*r*.52,z+.15),(r*.17,r*.11,r*.14),green,name,0,8,12)
  for side in [-1,1]:
   limb("Arm_"+("L" if side<0 else "R"),(side*.45,-.6,1.35),(side*.86,-.97,.65),(side*.75,-1.1,.12),"Torso",.13)
   limb("Leg_"+("L" if side<0 else "R"),(side*.45,.66,1.15),(side*.86,.88,.52),(side*.72,.53,.12),"Torso",.14)
 else:
  wide=.81 if isR else .46
  organic("Core",(0,0,tz),(wide,.40 if isR else .28,.58 if isR else .40),flesh,"Torso",.06)
  plates((0,-.12,tz+.05),wide*1.7,1.1 if isR else .65,"Torso",glow,4 if isR else 4)
  organic("Pelvis",(0,.02,1.40 if isR else .99),(.47 if isR else .30,.32,.32 if isR else .23),shell,"Torso",.08)
  face((0,-.16,2.88 if isR else 1.66),species,1.38 if isR else 1,glow)
  for side in [-1,1]:
   if isR:
    limb("Arm_"+("L" if side<0 else "R"),(side*.79,0,2.62),(side*.98,-.09,1.75),(side*.99,-.17,.98),"Torso",.26)
    limb("Leg_"+("L" if side<0 else "R"),(side*.35,.08,1.47),(side*.46,-.18,.78),(side*.44,.02,.18),"Torso",.28)
   else:
    limb("Arm_"+("L" if side<0 else "R"),(side*.43,0,1.55),(side*.61,-.08,.96),(side*.64,-.32,.35),"Torso",.12)
    limb("Leg_"+("L" if side<0 else "R"),(side*.22,.08,.98),(side*.32,-.22,.57),(side*.28,.17,.11),"Torso",.145)
   for j in range(5):
    organic("ShoulderLayer",(side*(wide+j*.026),.02+j*.055,tz+.43-j*.058),(.33 if isR else .22,.31,.17 if isR else .1),shell,"Torso",.1)
    a=(side*(wide+j*.03),.05+j*.12,tz+.48)
    spike("ShoulderSpine",a,(side*(wide+.26),.14+j*.13,tz+.65),(side*(wide+.36),.23+j*.12,tz+.82-j*.015),.095 if isR else .055,edge,"Torso")
  for j in range(7):
   zz=tz-.30+j*.11
   organic("DorsalPlate",(0,.27,zz),(.23 if isR else .15,.2,.13),shell,"Torso",.07)
   spike("SpineRidge",(0,.29,zz),(0,.43,zz+.08),(0,.62,zz+.15),.085 if isR else .05,edge,"Torso")
 # armature with rigid armor weights, deforming organic joints
 arm=bpy.data.armatures.new("Rig_"+species);rig=bpy.data.objects.new("Rig_"+species,arm);scene.collection.objects.link(rig)
 bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode="EDIT")
 for name,(h,t,parent) in bones.items():
  eb=arm.edit_bones.new(name);eb.head=h;eb.tail=t
  if parent:eb.parent=arm.edit_bones[parent]
 bpy.ops.object.mode_set(mode="OBJECT");rig.select_set(False)
 bpy.ops.object.select_all(action="DESELECT")
 for p in parts:p.select_set(True)
 bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join()
 mesh=parts[0];mesh.name="SK_"+species+"_Reference";mod=mesh.modifiers.new("Skeleton","ARMATURE");mod.object=rig;mesh.parent=rig
 rig.animation_data_create()
 for actionName in ["Idle","Walk","Run","Attack","Hit","Death"]:
  action=bpy.data.actions.new(species+"_"+actionName);rig.animation_data.action=action
  duration={"Idle":60,"Walk":36,"Run":24,"Attack":40,"Hit":16,"Death":60}[actionName]
  for frame in range(1,duration+1,3):
   t=(frame-1)/(duration-1);phase=t*2*pi
   for pb in rig.pose.bones:pb.rotation_mode="XYZ";pb.rotation_euler=(0,0,0);pb.location=(0,0,0);pb.scale=(1,1,1)
   torso=rig.pose.bones["Torso"]
   if actionName=="Idle":
    torso.scale.z=1+.012*sin(phase);rig.pose.bones["Head"].rotation_euler.z=.035*sin(phase)
   if actionName in ["Walk","Run"]:
    run=actionName=="Run";swing=.60 if run else .30
    torso.location.y=(.04 if run else .02)*abs(sin(phase*2));torso.rotation_euler.x=.18 if run else .04;torso.rotation_euler.z=.025*sin(phase)
    for side,offset in [("L",0),("R",pi)]:
     a=sin(phase+offset)
     rig.pose.bones["Leg_"+side].rotation_euler.x=a*swing
     rig.pose.bones["Leg_"+side+"_Lower"].rotation_euler.x=max(0,-a)*.50
     rig.pose.bones["Arm_"+side].rotation_euler.x=(-a if not isS else a)*swing*(.65 if not isS else 1)
     rig.pose.bones["Arm_"+side+"_Lower"].rotation_euler.x=max(0,a)*.35
   if actionName=="Attack":
    wind=sin(min(1,t*1.7)*pi);strike=max(0,sin((t-.34)*pi*2))
    torso.rotation_euler.x=-.17*wind+.38*strike
    if isS:rig.pose.bones["Head"].rotation_euler.x=-.5*wind
    else:
     for side in ["L","R"]:
      rig.pose.bones["Arm_"+side].rotation_euler.x=-1.45*wind+.85*strike
      rig.pose.bones["Arm_"+side+"_Lower"].rotation_euler.x=-.55*wind
     if not isR:torso.location.z=.27*wind
   if actionName=="Hit":torso.rotation_euler.x=-.20*sin(t*pi);torso.rotation_euler.z=.13*sin(t*pi);rig.pose.bones["Head"].rotation_euler.x=.22*sin(t*pi)
   if actionName=="Death":
    e=t*t*(3-2*t);torso.rotation_euler.z=1.3*e;torso.rotation_euler.x=.28*e;torso.location.y=-tz*.72*e
    for side,k in [("L",1),("R",-1)]:
     rig.pose.bones["Leg_"+side].rotation_euler.z=k*.55*e;rig.pose.bones["Leg_"+side+"_Lower"].rotation_euler.x=.8*e
     rig.pose.bones["Arm_"+side].rotation_euler.z=k*.7*e
   for pb in rig.pose.bones:
    if pb.name.startswith("Sac_"):pb.scale=(1+.045*sin(phase+int(pb.name[-1])),)*3 if actionName!="Death" else (1-t*.55,)*3
    pb.keyframe_insert(data_path="rotation_euler",frame=frame);pb.keyframe_insert(data_path="location",frame=frame);pb.keyframe_insert(data_path="scale",frame=frame)
  for pb in rig.pose.bones:
   for prop in ["rotation_euler","location","scale"]:pb.keyframe_insert(data_path=prop,frame=duration)
  action.use_fake_user=True
 rig.animation_data.action=bpy.data.actions.get(species+"_Idle")
 bpy.ops.object.select_all(action="DESELECT");rig.select_set(True);mesh.select_set(True);bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=out+"/SK_"+species+"_Reference.fbx",use_selection=True,add_leaf_bones=False,axis_forward="-Z",axis_up="Y",bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,mesh_smooth_type="FACE",path_mode="COPY",embed_textures=True)
 for pb in rig.pose.bones:pb.rotation_euler=(0,0,0);pb.location=(0,0,0);pb.scale=(1,1,1)
 rig.animation_data.action=None
 mesh.location.x={"Stalker":-4,"Ravager":0,"Spitter":4}[species] # presentation only; exported at origin
 return {"species":species,"vertices":len(mesh.data.vertices),"faces":len(mesh.data.polygons),"bones":len(bones),"file":out+"/SK_"+species+"_Reference.fbx"}
results=[create(s) for s in ["Stalker","Ravager","Spitter"]]
bpy.ops.wm.save_as_mainfile(filepath="C:/Users/ibarr/EclipseGenesis/Docs/SourceAssets/Eclipse_ReferenceAliens.blend")
print(json.dumps(results))
