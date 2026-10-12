"""
ECLIPSE GENESIS - Blender -> Unity FBX export.

Exports the selected objects (or the active collection when nothing is selected) to
  <project>/Assets/_EclipseGenesis/Art/Imported/<CATEGORY>/<NAME>.fbx
with the project conventions (Docs/BLENDER_UNITY_PIPELINE.md):
  - 1 Blender unit = 1 metre, scale applied ("FBX Units Scale").
  - Model built FACING BLENDER +Y (its right side on +X, +Z up). Measured on 2026-10-12: with
    "Apply Transform" + Unity "Bake Axis Conversion" the mapping is Blender (x, y, z) -> Unity (x, z, y),
    so +Y becomes Unity +Z (forward) with no root rotation and no mirroring. The Forward setting
    does not change the result in this combination.
  - Empties named Socket_* are exported (weapons: Socket_Muzzle, Socket_Sight, Socket_ShellEject).
  - No leaf bones; modifiers applied; face smoothing exported.

Usage (Blender Text Editor or Blender MCP execute_blender_code):
    exec(open(r"C:/Users/ibarr/EclipseGenesis/ArtSource/Blender/eg_export_fbx.py").read())
    export("Weapons", "SM_EG47")
"""
import os
import bpy

PROJECT = r"C:/Users/ibarr/EclipseGenesis"
CATEGORIES = ("Weapons", "Aliens", "Characters", "Environment", "Vehicles", "Vegetation", "Props")


def export(category: str, name: str, selection_only: bool = True, forward: str = "-Z") -> str:
    if category not in CATEGORIES:
        raise ValueError(f"category must be one of {CATEGORIES}")
    out_dir = os.path.join(PROJECT, "Assets", "_EclipseGenesis", "Art", "Imported", category)
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, f"{name}.fbx")

    has_selection = any(o.select_get() for o in bpy.context.view_layer.objects)
    animated = category in ("Aliens", "Characters", "Weapons")
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=selection_only and has_selection,
        use_active_collection=selection_only and not has_selection,
        object_types={"EMPTY", "MESH", "ARMATURE"},
        apply_scale_options="FBX_SCALE_UNITS",
        global_scale=1.0,
        axis_forward=forward,
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_tspace=True,
        add_leaf_bones=False,
        bake_anim=animated,
        bake_anim_use_all_actions=animated,
        bake_anim_use_nla_strips=False,
        path_mode="COPY",
        embed_textures=False,
    )
    print(f"[EclipseGenesis] exported {path}")
    return path


def check_weapon_sockets() -> list:
    """Returns the required weapon sockets missing from the scene (empty list = ready to export)."""
    required = ("Socket_Muzzle", "Socket_Sight")
    names = {o.name.split(".")[0] for o in bpy.context.scene.objects}
    return [s for s in required if s not in names]
