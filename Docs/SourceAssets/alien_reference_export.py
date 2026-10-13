import bpy, shutil, os
folder="C:/Users/ibarr/EclipseGenesis/Assets/_EclipseGenesis/Art/Imported/Aliens/ReferenceV1"
source="C:/Users/ibarr/EclipseGenesis/Docs/SourceAssets/ReferenceActionLibrary.blend"
master="C:/Users/ibarr/EclipseGenesis/Docs/SourceAssets/Eclipse_ReferenceAliens.blend"
shutil.copyfile(master,source)
for species in ["Stalker","Ravager","Spitter"]:
 with bpy.data.libraries.load(source,link=False) as (a,b):b.actions=[n for n in a.actions if n.startswith(species+"_") and n not in bpy.data.actions]
 for ob in bpy.data.objects:
  if ob.type=="ARMATURE" and ob.animation_data:ob.animation_data.action=None
 for a in list(bpy.data.actions):
  if not a.name.startswith(species+"_"):bpy.data.actions.remove(a)
 rig=bpy.data.objects["Rig_"+species];mesh=bpy.data.objects["SK_"+species+"_Reference"];rig.location=(0,0,0);mesh.location=(0,0,0)
 bpy.ops.object.select_all(action="DESELECT");rig.select_set(True);mesh.select_set(True);bpy.context.view_layer.objects.active=rig
 rig.animation_data.action=bpy.data.actions[species+"_Idle"];bpy.context.scene.frame_set(1)
 bpy.ops.export_scene.fbx(filepath=folder+"/SK_"+species+"_Reference.fbx",use_selection=True,add_leaf_bones=False,axis_forward="-Z",axis_up="Y",bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,mesh_smooth_type="FACE",path_mode="COPY",embed_textures=True)
 rig.animation_data.action=None
with bpy.data.libraries.load(source,link=False) as (a,b):b.actions=[n for n in a.actions if n not in bpy.data.actions]
for s,x in [("Stalker",-4),("Ravager",0),("Spitter",4)]:
 rig=bpy.data.objects["Rig_"+s];rig.location.x=x;rig.animation_data.action=bpy.data.actions[s+"_Idle"];bpy.data.objects["SK_"+s+"_Reference"].location=(0,0,0)
bpy.context.scene.frame_set(1);bpy.ops.wm.save_as_mainfile(filepath=master)
print("Exported corrected models with six clips each.")
