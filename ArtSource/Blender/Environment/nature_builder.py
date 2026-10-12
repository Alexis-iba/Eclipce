"""
ECLIPSE GENESIS - game-ready nature assets, built in Blender from the CC0 Poly Haven sources.

    exec(open(r"C:/Users/ibarr/EclipseGenesis/ArtSource/Blender/Environment/nature_builder.py").read())
    build_pines()        # SM_Pine_A/B/C  -> Art/Imported/Vegetation  (~600 triangles each)
    optimise_scans()     # SM_Rock_Boulder, SM_Rock_MossSet, SM_Log_Dead -> Art/Imported/Environment (decimated)

Why: the scanned fir_tree_01 has ~2-4 million triangles per tree and boulder_01 ~124k, unusable when scattered
70+ times. Pines are rebuilt as a tapered trunk plus crossed alpha cards that sample the sprigs of the Poly Haven
twig atlas; scans are decimated (normal maps keep the surface detail).
Materials are named EG_PH_<poly haven surface>; Unity's PolyHavenLibrary maps them to M_PH_<surface>.
Orientation and units as in Docs/BLENDER_UNITY_PIPELINE.md (metres, +Z up, origin on the ground).
"""
import math
import os
import random
import bmesh
import bpy
from mathutils import Matrix, Vector

PROJECT = r"C:/Users/ibarr/EclipseGenesis"
IMPORTED = PROJECT + "/Assets/_EclipseGenesis/Art/Imported"

# Sprig rectangles in the twig atlas (u0, v0, u1, v1); stems at v0, tips at v1.
SPRIGS = [
    (0.31, 0.23, 0.645, 0.60),
    (0.645, 0.17, 0.95, 0.55),
    (0.65, 0.63, 0.94, 0.96),
    (0.185, 0.69, 0.42, 0.95),
]


def _material(name, color):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    return mat


def _link(obj, collection_name):
    col = bpy.data.collections.get(collection_name) or bpy.data.collections.new(collection_name)
    if col.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(col)
    old = bpy.data.objects.get(obj.name.split(".")[0])
    if old is not None and old is not obj:
        name = old.name
        bpy.data.objects.remove(old, do_unlink=True)
        obj.name = name
    col.objects.link(obj)
    return obj


def _export(obj, category, name):
    exec(open(PROJECT + "/ArtSource/Blender/eg_export_fbx.py").read(), globals())
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    return export(category, name)


def pine(name, seed, height):
    rnd = random.Random(seed)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")

    # Tapered 8-sided trunk (bark UVs in metres).
    sides, rings = 8, 6
    r0, r1 = 0.22 + height * 0.006, 0.04
    verts = []
    for j in range(rings + 1):
        t = j / rings
        z = t * height * 0.97
        r = r0 + (r1 - r0) * t
        ring = [bm.verts.new((math.cos(a) * r, math.sin(a) * r, z)) for a in (i / sides * math.tau for i in range(sides))]
        verts.append(ring)
    for j in range(rings):
        for i in range(sides):
            k = (i + 1) % sides
            f = bm.faces.new((verts[j][i], verts[j][k], verts[j + 1][k], verts[j + 1][i]))
            f.material_index = 1
            f.smooth = True
            for loop, (ui, vj) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
                loop[uv].uv = (ui / sides * 1.2, vj / rings * height * 0.5)

    def card(origin, direction, length, width, roll, sprig):
        d = direction.normalized()
        side = d.cross(Vector((0, 0, 1)))
        if side.length < 1e-3:
            side = Vector((1, 0, 0))
        side.normalize()
        side = Matrix.Rotation(roll, 3, d) @ side
        u0, v0, u1, v1 = sprig
        p = [origin - side * width * 0.5, origin + side * width * 0.5,
             origin + d * length + side * width * 0.5, origin + d * length - side * width * 0.5]
        vs = [bm.verts.new(x) for x in p]
        f = bm.faces.new(vs)
        f.material_index = 0
        f.smooth = True
        for loop, c in zip(f.loops, ((u0, v0), (u1, v0), (u1, v1), (u0, v1))):
            loop[uv].uv = c

    # Whorls of drooping branches, longer at the bottom (conifer silhouette).
    z = 1.6 + rnd.uniform(0, 0.4)
    while z < height - 0.6:
        t = (z - 1.6) / (height - 2.2)
        length = 0.6 + (1 - t) ** 1.1 * (2.4 + height * 0.06)
        count = rnd.randint(5, 7)
        phase = rnd.uniform(0, math.tau)
        for i in range(count):
            a = phase + i / count * math.tau + rnd.uniform(-0.25, 0.25)
            droop = math.radians(rnd.uniform(12, 32) + (1 - t) * 10)
            d = Vector((math.cos(a) * math.cos(droop), math.sin(a) * math.cos(droop), -math.sin(droop)))
            base = Vector((0, 0, z)) + d * 0.05
            sprig = SPRIGS[rnd.randrange(len(SPRIGS))]
            w = length * rnd.uniform(0.7, 0.9)
            card(base, d, length, w, math.radians(25), sprig)
            card(base, d, length * 0.92, w * 0.9, math.radians(-35), sprig)
        z += rnd.uniform(0.38, 0.55)
    # Crown.
    for roll in (0.0, math.pi / 2):
        card(Vector((0, 0, height - 1.4)), Vector((0, 0, 1)), 1.6, 0.9, roll, SPRIGS[0])

    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(_material("EG_PH_fir_tree_01_twig", (0.2, 0.3, 0.15)))
    mesh.materials.append(_material("EG_PH_fir_tree_01_bark", (0.25, 0.18, 0.12)))
    obj = bpy.data.objects.new(name, mesh)
    _link(obj, "EG_Nature")
    return obj, len(mesh.polygons)


def build_pines():
    result = {}
    for name, seed, h in (("SM_Pine_A", 11, 13.0), ("SM_Pine_B", 23, 10.0), ("SM_Pine_C", 37, 8.0)):
        obj, faces = pine(name, seed, h)
        _export(obj, "Vegetation", name)
        result[name] = faces
    return result


def _import_fbx(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.data.objects if o not in before]


def optimise_scans():
    jobs = [
        ("Environment/PolyHaven/boulder_01/boulder_01.fbx", "SM_Rock_Boulder", 3000),
        ("Environment/PolyHaven/rock_moss_set_01/rock_moss_set_01.fbx", "SM_Rock_MossSet", 5000),
        ("Vegetation/PolyHaven/dead_tree_trunk_02/dead_tree_trunk_02.fbx", "SM_Log_Dead", 2500),
    ]
    result = {}
    for rel, name, target in jobs:
        objs = [o for o in _import_fbx(os.path.join(IMPORTED, rel)) if o.type == "MESH"]
        for o in bpy.context.view_layer.objects:
            o.select_set(False)
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        if len(objs) > 1:
            bpy.ops.object.join()
        obj = bpy.context.view_layer.objects.active
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        mod = obj.modifiers.new("Decimate", "DECIMATE")
        mod.ratio = min(1.0, target / max(1, tris))
        bpy.ops.object.modifier_apply(modifier=mod.name)
        for slot in obj.material_slots:
            if slot.material and not slot.material.name.startswith("EG_PH_"):
                base = slot.material.name.split(".")[0]
                slot.material = _material("EG_PH_" + base, (0.4, 0.4, 0.4))
        obj.name = name
        obj.data.name = name
        _export(obj, "Environment", name)
        result[name] = (tris, sum(len(p.vertices) - 2 for p in obj.data.polygons), [s.material.name for s in obj.material_slots])
    return result
