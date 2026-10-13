import bpy,math
from mathutils import Vector
for species in ["Stalker","Ravager","Spitter"]:
 rig=bpy.data.objects["Rig_"+species];mesh=bpy.data.objects["SK_"+species+"_Reference"]
 # Recover the source head/jaw position in Blender metres.
 facez=2.88 if species=="Ravager" else 1.39 if species=="Spitter" else 1.66
 facey=-1.02 if species=="Spitter" else -.16
 scale=1.38 if species=="Ravager" else 1.35 if species=="Spitter" else 1
 headGroup=mesh.vertex_groups.get("Head");torsoGroup=mesh.vertex_groups.get("Torso")
 luminous=set()
 for poly in mesh.data.polygons:
  mat=mesh.data.materials[poly.material_index]
  if ("StalkerBlue" in mat.name or "RavagerOrange" in mat.name):luminous.update(poly.vertices)
 for i in luminous:
  v=mesh.data.vertices[i]
  if any(g.group==torsoGroup.index and g.weight>.9 for g in v.groups):v.co.y-=.13
 if species=="Ravager":
  for v in mesh.data.vertices:
   if any(g.group==torsoGroup.index and g.weight>.9 for g in v.groups):v.co.x*=1.15
 # Jaw as a separate articulated bone and weighted teeth, not a frozen mouth.
 bpy.ops.object.select_all(action="DESELECT");rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode="EDIT")
 if "Jaw" not in rig.data.edit_bones:
  eb=rig.data.edit_bones.new("Jaw");eb.head=(0,facey-.24*scale,facez-.16*scale);eb.tail=(0,facey-.46*scale,facez-.16*scale);eb.parent=rig.data.edit_bones["Head"]
 bpy.ops.object.mode_set(mode="OBJECT")
 jawgroup=mesh.vertex_groups.get("Jaw") or mesh.vertex_groups.new(name="Jaw")
 selected=[]
 for v in mesh.data.vertices:
  if any(g.group==headGroup.index and g.weight>.9 for g in v.groups) and v.co.y<facey-.29*scale and v.co.z<facez-.145*scale:selected.append(v.index)
 if selected:headGroup.remove(selected);jawgroup.add(selected,1,"REPLACE")
 for actionName in ["Idle","Walk","Run","Attack","Hit","Death"]:
  action=bpy.data.actions[species+"_"+actionName];rig.animation_data.action=action
  duration={"Idle":60,"Walk":36,"Run":24,"Attack":40,"Hit":16,"Death":60}[actionName]
  for frame in list(range(1,duration+1,3))+[duration]:
   t=(frame-1)/(duration-1);pb=rig.pose.bones["Jaw"];pb.rotation_mode="XYZ"
   amount=.6*math.sin(min(1,t*1.6)*math.pi) if actionName=="Attack" else .55*t if actionName=="Death" else .022*math.sin(t*math.pi*2)
   pb.rotation_euler=(amount,0,0);pb.keyframe_insert(data_path="rotation_euler",frame=frame)
 # Keep the corpse outside the floor; preserve the rest-pose lowest point.
 rig.animation_data.action=bpy.data.actions[species+"_Idle"];bpy.context.scene.frame_set(1)
 deps=bpy.context.evaluated_depsgraph_get();evaluated=mesh.evaluated_get(deps)
 baseline=min((evaluated.matrix_world@v.co).z for v in evaluated.data.vertices)
 rig.animation_data.action=bpy.data.actions[species+"_Death"]
 for frame in list(range(1,61,3))+[60]:
  bpy.context.scene.frame_set(frame);deps=bpy.context.evaluated_depsgraph_get();evaluated=mesh.evaluated_get(deps)
  lowest=min((evaluated.matrix_world@v.co).z for v in evaluated.data.vertices)
  if lowest<baseline:
   torso=rig.pose.bones["Torso"];torso.location.y+=baseline-lowest;torso.keyframe_insert(data_path="location",frame=frame)
 rig.animation_data.action=bpy.data.actions[species+"_Idle"];bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath="C:/Users/ibarr/EclipseGenesis/Docs/SourceAssets/Eclipse_ReferenceAliens.blend")
print("Added animated jaws, visible energy fissures, heavier Ravager torso and grounded corpse poses.")
