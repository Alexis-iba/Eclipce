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


# Leaf regions of the tree_small_02 leaves atlas (u0, v0, u1, v1): leaf clusters (left) and two leafy twigs (centre).
LEAF_CLUSTERS = [(0.0, 0.0, 0.44, 0.34), (0.0, 0.33, 0.44, 0.67), (0.0, 0.66, 0.44, 1.0),
                 (0.40, 0.30, 0.52, 0.92), (0.55, 0.36, 0.86, 0.96)]


def broadleaf(name, seed, height, crown_radius):
    """Round-crowned deciduous tree: tapered trunk and limbs plus ~150 leaf-cluster cards with spherical normals."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    normals = {}   # loop normals for the foliage (bmesh faces -> per-vertex direction)

    def limb(a, b, r0, r1, sides=7):
        axis = (b - a)
        length = axis.length
        axis.normalize()
        ref = Vector((0, 0, 1)) if abs(axis.z) < 0.9 else Vector((1, 0, 0))
        s1 = axis.cross(ref).normalized()
        s2 = axis.cross(s1).normalized()
        rings = []
        for t, r in ((0.0, r0), (1.0, r1)):
            c = a + axis * length * t
            rings.append([bm.verts.new(c + (s1 * math.cos(k / sides * math.tau) + s2 * math.sin(k / sides * math.tau)) * r)
                          for k in range(sides)])
        for k in range(sides):
            j = (k + 1) % sides
            f = bm.faces.new((rings[0][k], rings[0][j], rings[1][j], rings[1][k]))
            f.material_index = 1
            f.smooth = True
            for loop, (uu, vv) in zip(f.loops, ((k, 0), (k + 1, 0), (k + 1, 1), (k, 1))):
                loop[uv].uv = (uu / sides * (r0 * 6.0), vv * length * 0.5)

    trunk_top = Vector((rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), height * 0.3))
    r_base = 0.22 + height * 0.016
    limb(Vector((0, 0, -0.2)), trunk_top, r_base, r_base * 0.75)
    centre = Vector((0, 0, height - crown_radius * 0.95))
    tips = []
    for i in range(rnd.randint(4, 6)):
        a = i / 5 * math.tau + rnd.uniform(-0.4, 0.4)
        tip = centre + Vector((math.cos(a) * crown_radius * 0.55, math.sin(a) * crown_radius * 0.55, rnd.uniform(-1.2, 1.2)))
        limb(trunk_top, tip, r_base * 0.55, 0.05)
        tips.append(tip)
    limb(trunk_top, centre + Vector((0, 0, crown_radius * 0.7)), r_base * 0.5, 0.04)

    # Leaf cards scattered in a flattened ellipsoid crown, denser on the outside.
    cards = 0
    target = int(150 + crown_radius * 18)
    lobes = tips + [centre + Vector((0, 0, crown_radius * 0.45))]
    while cards < target:
        lobe = lobes[rnd.randrange(len(lobes))]
        d = Vector((rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, 1) * 0.8)).normalized()
        r = crown_radius * 0.55 * (0.5 + 0.5 * rnd.random() ** 0.5)
        p = lobe + Vector((d.x * r, d.y * r, d.z * r * 0.85))
        if p.z < height * 0.36:
            continue
        size = rnd.uniform(1.3, 2.1)
        # Card facing roughly outward, randomly rolled.
        n = (p - centre).normalized()
        t1 = n.cross(Vector((0, 0, 1)))
        if t1.length < 1e-3:
            t1 = Vector((1, 0, 0))
        t1.normalize()
        t2 = n.cross(t1).normalized()
        roll = rnd.uniform(0, math.tau)
        e1 = (t1 * math.cos(roll) + t2 * math.sin(roll)) * size * 0.5
        e2 = (t2 * math.cos(roll) - t1 * math.sin(roll)) * size * 0.5
        tilt = Vector((rnd.uniform(-0.4, 0.4), rnd.uniform(-0.4, 0.4), rnd.uniform(-0.4, 0.4)))
        e2 = (e2 + tilt * size * 0.3)
        corners = [p - e1 - e2, p + e1 - e2, p + e1 + e2, p - e1 + e2]
        vs = [bm.verts.new(c) for c in corners]
        f = bm.faces.new(vs)
        f.material_index = 0
        f.smooth = True
        u0, v0, u1, v1 = LEAF_CLUSTERS[rnd.randrange(len(LEAF_CLUSTERS))]
        for loop, c in zip(f.loops, ((u0, v0), (u1, v0), (u1, v1), (u0, v1))):
            loop[uv].uv = c
        for v in vs:
            normals[v] = (v.co - centre).normalized()
        cards += 1

    mesh = bpy.data.meshes.new(name)
    bm.verts.index_update()
    loop_normals = []
    for f in bm.faces:
        for loop in f.loops:
            loop_normals.append(normals.get(loop.vert))
    bm.to_mesh(mesh)
    bm.free()
    # Spherical normals on foliage (soft, volumetric shading); limbs keep their own.
    custom = [n if n is not None else Vector((0, 0, 0)) for n in loop_normals]
    mesh.normals_split_custom_set([tuple(n) for n in custom])
    mesh.materials.append(_material("EG_PH_tree_small_02_leaves", (0.2, 0.35, 0.15)))
    mesh.materials.append(_material("EG_PH_bark_brown_02", (0.3, 0.22, 0.16)))
    obj = bpy.data.objects.new(name, mesh)
    _link(obj, "EG_Nature")
    return obj, len(mesh.polygons)


def build_broadleaves():
    result = {}
    for name, seed, h, cr in (("SM_Tree_Broadleaf_A", 5, 11.0, 4.2), ("SM_Tree_Broadleaf_B", 17, 9.0, 3.6), ("SM_Tree_Broadleaf_C", 29, 13.0, 5.0)):
        obj, faces = broadleaf(name, seed, h, cr)
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
