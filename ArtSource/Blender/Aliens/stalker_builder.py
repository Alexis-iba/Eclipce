"""
ECLIPSE GENESIS - Stalker v2: organic, rigged alien (concept art panel 5), built procedurally in Blender.

    exec(open(r"C:/Users/ibarr/EclipseGenesis/ArtSource/Blender/Aliens/stalker_builder.py").read())
    build_stalker(export_fbx=True)

Body: vertex skeleton + Skin modifier + subdivision -> one continuous organic skin, Voronoi/cloud displacement for a
chitinous surface; chitin plates, dorsal spikes, head crest, mandibles, curved claws, glowing red veins. Eyes are a
separate mesh parented to the Head bone (AlienBody hides them on death).
Rig: armature with the bones AlienBody drives (Torso, Neck, Head, Leg_FL/FR/BL/BR, Shin_FL/FR/BL/BR, Tail), automatic
distance-based skin weights. Proportions match the previous Stalker (Prefabs/Aliens/EG_Alien_Stalker), so the AI,
hitboxes and NavMesh agent keep working. Facing +Y (Unity +Z), origin on the ground.
Materials (Unity paints): EG_AlienSkin, EG_AlienPlate, EG_AlienClaw, EG_AlienGlow (emissive red), EG_AlienEye.
"""
import math
import random
import bmesh
import bpy
from mathutils import Matrix, Vector

COLORS = {"EG_AlienSkin": (0.05, 0.04, 0.05), "EG_AlienPlate": (0.02, 0.02, 0.025), "EG_AlienClaw": (0.12, 0.1, 0.1),
          "EG_AlienGlow": (1.0, 0.12, 0.05), "EG_AlienEye": (1.0, 0.15, 0.05)}
COL = None


def _mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*COLORS[name], 1.0)
    if name in ("EG_AlienGlow", "EG_AlienEye"):
        bsdf.inputs["Emission Color"].default_value = (*COLORS[name], 1.0)
        bsdf.inputs["Emission Strength"].default_value = 8.0
    m.diffuse_color = (*COLORS[name], 1.0)
    return m


def _obj(name, bm, mat):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(_mat(mat))
    o = bpy.data.objects.new(name, mesh)
    COL.objects.link(o)
    return o


def _apply(obj, mod):
    with bpy.context.temp_override(object=obj, active_object=obj, selected_objects=[obj], selected_editable_objects=[obj]):
        bpy.ops.object.modifier_apply(modifier=mod.name)


def _join(name, objs):
    with bpy.context.temp_override(active_object=objs[0], selected_editable_objects=objs, selected_objects=objs):
        bpy.ops.object.join()
    objs[0].name = name
    objs[0].data.name = name
    return objs[0]


# Joint positions (Blender: x right, y forward, z up). Same layout as the previous Unity Stalker.
J = {
    "tail_tip": (0, -1.1, 0.5), "tail_mid": (0, -0.85, 0.66), "abdomen": (0, -0.58, 0.76), "pelvis": (0, -0.36, 0.8),
    "mid": (0, -0.04, 0.86), "chest": (0, 0.3, 0.92), "shoulders": (0, 0.5, 0.97), "neck": (0, 0.68, 0.9),
    "head": (0, 0.8, 0.86), "skull": (0, 0.97, 0.84), "snout": (0, 1.16, 0.77),
}
R = {"tail_tip": 0.02, "tail_mid": 0.07, "abdomen": 0.2, "pelvis": 0.2, "mid": 0.22, "chest": 0.3, "shoulders": 0.28,
     "neck": 0.11, "head": 0.14, "skull": 0.16, "snout": 0.075}
LIMBS = {   # hip, knee/elbow, ankle/wrist, foot (crouched, hunched stance)
    "FL": [(-0.26, 0.4, 0.82), (-0.52, 0.52, 0.72), (-0.6, 0.68, 0.26), (-0.64, 0.76, 0.02)],
    "FR": [(0.26, 0.4, 0.82), (0.52, 0.52, 0.72), (0.6, 0.68, 0.26), (0.64, 0.76, 0.02)],
    "BL": [(-0.24, -0.42, 0.74), (-0.5, -0.46, 0.76), (-0.56, -0.72, 0.3), (-0.62, -0.58, 0.02)],
    "BR": [(0.24, -0.42, 0.74), (0.5, -0.46, 0.76), (0.56, -0.72, 0.3), (0.62, -0.58, 0.02)],
}
LIMB_R = [0.14, 0.1, 0.065, 0.045]


def body_mesh():
    bm = bmesh.new()
    verts, radii = {}, {}
    order = ["tail_tip", "tail_mid", "abdomen", "pelvis", "mid", "chest", "shoulders", "neck", "head", "skull", "snout"]
    for k in order:
        verts[k] = bm.verts.new(J[k])
        radii[verts[k]] = R[k]
    for a, b in zip(order, order[1:]):
        bm.edges.new((verts[a], verts[b]))
    for side, pts in LIMBS.items():
        prev = verts["chest"] if side[0] == "F" else verts["pelvis"]
        for i, p in enumerate(pts):
            v = bm.verts.new(p)
            radii[v] = LIMB_R[i]
            bm.edges.new((prev, v))
            prev = v
    me = bpy.data.meshes.new("SK_Stalker")
    bm.to_mesh(me)
    rad_by_co = {tuple(round(c, 4) for c in v.co): r for v, r in radii.items()}
    bm.free()
    o = bpy.data.objects.new("SK_Stalker", me)
    COL.objects.link(o)
    skin = o.modifiers.new("Skin", "SKIN")
    skin.use_smooth_shade = True
    skin.branch_smoothing = 0.6
    for i, v in enumerate(me.vertices):
        r = rad_by_co[tuple(round(c, 4) for c in v.co)]
        sv = me.skin_vertices[0].data[i]
        sv.radius = (r, r * (0.85 if r > 0.15 else 1.0))
        sv.use_root = (i == 0)
    _apply(o, skin)
    sub = o.modifiers.new("Sub", "SUBSURF")
    sub.levels = 2
    _apply(o, sub)
    # Chitin cells + organic lumps.
    for kind, size, strength in (("VORONOI", 0.06, 0.04), ("CLOUDS", 0.16, 0.05)):
        tex = bpy.data.textures.get("EG_Stalker_" + kind) or bpy.data.textures.new("EG_Stalker_" + kind, kind)
        tex.noise_scale = size
        d = o.modifiers.new("Disp_" + kind, "DISPLACE")
        d.texture = tex
        d.strength = strength
        d.texture_coords = "GLOBAL"
        _apply(o, d)
    sm = o.modifiers.new("Smooth", "SMOOTH")
    sm.iterations = 1
    sm.factor = 0.3
    _apply(o, sm)
    o.data.materials.append(_mat("EG_AlienSkin"))
    return o


def shell(name, center, size, rot_deg, mat="EG_AlienPlate"):
    bm = bmesh.new()
    res = bmesh.ops.create_uvsphere(bm, u_segments=14, v_segments=8, radius=1.0)
    # Keep the upper half: a curved shell.
    for v in res["verts"]:
        if v.co.z < -0.15:
            v.co.z = -0.15 + (v.co.z + 0.15) * 0.15
    m = Matrix.Translation(Vector(center)) @ Matrix.Rotation(math.radians(rot_deg[0]), 4, "X") @ Matrix.Rotation(math.radians(rot_deg[2]), 4, "Z") @ Matrix.Diagonal((*size, 1.0))
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    o = _obj(name, bm, mat)
    for p in o.data.polygons:
        p.use_smooth = True
    return o


def horn(name, base, direction, length, radius, bend_deg, mat="EG_AlienClaw", segs=8, rings=8):
    """Curved tapered horn/claw from base along direction, bending downward."""
    d = Vector(direction).normalized()
    side = d.cross(Vector((0, 0, 1)))
    if side.length < 1e-3:
        side = Vector((1, 0, 0))
    side.normalize()
    up = side.cross(d).normalized()
    bm = bmesh.new()
    rings_v = []
    p = Vector(base)
    dirv = d.copy()
    step = length / rings
    rot = Matrix.Rotation(math.radians(bend_deg / rings), 3, side)
    for i in range(rings + 1):
        t = i / rings
        r = radius * (1 - t) ** 0.8 + 0.0015
        s = dirv.cross(Vector((0, 0, 1)))
        if s.length < 1e-3:
            s = side
        s.normalize()
        u = s.cross(dirv).normalized()
        rings_v.append([bm.verts.new(p + (s * math.cos(k / segs * math.tau) + u * math.sin(k / segs * math.tau)) * r * (1.0 if k % 2 else 0.8)) for k in range(segs)])
        p = p + dirv * step
        dirv = rot @ dirv
    for i in range(rings):
        for k in range(segs):
            j = (k + 1) % segs
            bm.faces.new((rings_v[i][k], rings_v[i][j], rings_v[i + 1][j], rings_v[i + 1][k]))
    bm.faces.new(rings_v[0][::-1])
    tip = bm.verts.new(p)
    for k in range(segs):
        j = (k + 1) % segs
        bm.faces.new((rings_v[-1][k], rings_v[-1][j], tip))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = _obj(name, bm, mat)
    for poly in o.data.polygons:
        poly.use_smooth = True
    return o


def vein(name, pts, radius=0.009):
    bm = bmesh.new()
    segs = 6
    rings = []
    for i, p in enumerate(pts):
        p = Vector(p)
        d = (Vector(pts[min(i + 1, len(pts) - 1)]) - Vector(pts[max(i - 1, 0)])).normalized()
        s = d.cross(Vector((0, 0, 1)))
        if s.length < 1e-3:
            s = Vector((1, 0, 0))
        s.normalize()
        u = s.cross(d).normalized()
        rings.append([bm.verts.new(p + (s * math.cos(k / segs * math.tau) + u * math.sin(k / segs * math.tau)) * radius) for k in range(segs)])
    for i in range(len(rings) - 1):
        for k in range(segs):
            j = (k + 1) % segs
            bm.faces.new((rings[i][k], rings[i][j], rings[i + 1][j], rings[i + 1][k]))
    return _obj(name, bm, "EG_AlienGlow")


def armature():
    arm = bpy.data.armatures.new("SK_Stalker_Rig")
    ao = bpy.data.objects.new("SK_Stalker_Rig", arm)
    COL.objects.link(ao)
    bpy.context.view_layer.objects.active = ao
    with bpy.context.temp_override(active_object=ao, object=ao, selected_objects=[ao], selected_editable_objects=[ao]):
        bpy.ops.object.mode_set(mode="EDIT")
        eb = arm.edit_bones

        def bone(name, head, tail, parent=None):
            b = eb.new(name)
            b.head, b.tail = Vector(head), Vector(tail)
            if parent:
                b.parent = eb[parent]
            return b
        bone("Torso", (0, 0, 0.78), (0, 0.3, 0.8))
        bone("Neck", (0, 0.62, 0.92), (0, 0.78, 0.87), "Torso")
        bone("Head", (0, 0.78, 0.87), (0, 1.05, 0.82), "Neck")
        bone("Tail", (0, -0.55, 0.8), (0, -1.12, 0.55), "Torso")
        for side, pts in LIMBS.items():
            bone("Leg_" + side, pts[0], pts[1], "Torso")
            bone("Shin_" + side, pts[1], pts[2], "Leg_" + side)
            bone("Foot_" + side, pts[2], pts[3], "Shin_" + side)
        bpy.ops.object.mode_set(mode="OBJECT")
    return ao


def seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (a + ab * t - p).length


def skin_weights(mesh_obj, arm_obj):
    bones = [(b.name, b.head_local.copy(), b.tail_local.copy()) for b in arm_obj.data.bones]
    groups = {n: mesh_obj.vertex_groups.new(name=n) for n, _, _ in bones}
    for v in mesh_obj.data.vertices:
        p = v.co
        ds = sorted(((seg_dist(p, h, t), n) for n, h, t in bones))[:3]
        ws = [(1.0 / (d ** 4 + 1e-7), n) for d, n in ds]
        total = sum(w for w, _ in ws)
        for w, n in ws:
            if w / total > 0.02:
                groups[n].add([v.index], w / total, "REPLACE")
    mod = mesh_obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm_obj
    mesh_obj.parent = arm_obj


def build_stalker(export_fbx=False, seed=13):
    global COL
    random.seed(seed)
    COL = bpy.data.collections.get("EG_Aliens") or bpy.data.collections.new("EG_Aliens")
    if COL.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(COL)
    for o in list(COL.objects):
        bpy.data.objects.remove(o, do_unlink=True)

    body = body_mesh()
    extra = []
    # Dorsal chitin plates and spikes.
    def spine_top(y):
        pts = sorted((J[k][1], J[k][2] + R[k] * 0.85) for k in J)
        for (y0, z0), (y1, z1) in zip(pts, pts[1:]):
            if y0 <= y <= y1:
                return z0 + (z1 - z0) * (y - y0) / max(y1 - y0, 1e-6)
        return pts[-1][1]
    for i in range(7):
        y = 0.5 - i * 0.17
        z = spine_top(y) + 0.03
        w = 0.26 - abs(y) * 0.12
        extra.append(shell("Plate", (0, y, z - 0.04), (w + 0.04, 0.12, 0.05), (-14, 0, 0)))
        extra.append(horn("Spike", (0, y - 0.02, z + 0.03), (0, -0.6, 1.0), 0.16 + 0.05 * math.cos(i), 0.026, -25))
    for s in (-1, 1):
        extra.append(shell("ShoulderPlate", (s * 0.22, 0.48, 1.1), (0.14, 0.2, 0.08), (-10, 0, s * 25)))
        extra.append(horn("ShoulderSpike", (s * 0.28, 0.45, 1.14), (s * 0.6, -0.4, 0.8), 0.18, 0.03, -20))
        extra.append(shell("FlankPlate", (s * 0.2, -0.05, 0.78), (0.1, 0.32, 0.12), (0, 0, s * 70)))
        extra.append(shell("ThighPlate", (s * 0.42, 0.42, 0.86), (0.1, 0.16, 0.08), (0, 0, s * 40)))
        extra.append(shell("HaunchPlate", (s * 0.42, -0.46, 0.88), (0.12, 0.17, 0.09), (0, 0, s * 40)))
    # Head: crest, brow ridges, mandibles, teeth.
    extra.append(shell("Crest", (0, 0.9, 0.97), (0.1, 0.22, 0.07), (-18, 0, 0)))
    extra.append(horn("CrestHorn", (0, 0.8, 1.0), (0, -0.7, 0.5), 0.26, 0.035, -30))
    for s in (-1, 1):
        extra.append(shell("Brow", (s * 0.08, 1.04, 0.92), (0.06, 0.1, 0.04), (-20, 0, s * 20)))
        extra.append(horn("Mandible", (s * 0.07, 1.09, 0.73), (s * 0.25, 1.0, -0.35), 0.17, 0.022, 40))
        for k in range(3):
            extra.append(horn("Tooth", (s * (0.02 + 0.015 * k), 1.14 - 0.03 * k, 0.74), (0, 0.3, -1.0), 0.05, 0.007, 10))
    # Claws: three long blades on each front limb, three toes on the back feet.
    for side, pts in LIMBS.items():
        s = -1 if "L" in side else 1
        foot = Vector(pts[3])
        if side[0] == "F":
            for k in (-1, 0, 1):
                extra.append(horn("Claw", foot + Vector((k * 0.03, 0.0, 0.05)), (k * 0.25 + s * 0.1, 1.0, -0.15), 0.36, 0.03, 55))
            extra.append(horn("ElbowSpike", Vector(pts[1]) + Vector((0, -0.02, 0.04)), (s * 0.2, -0.8, 0.6), 0.2, 0.03, -15))
        else:
            for k in (-1, 0, 1):
                extra.append(horn("Toe", foot + Vector((k * 0.035, 0.03, 0.03)), (k * 0.3, 1.0, -0.2), 0.14, 0.022, 45))
            extra.append(horn("HockSpike", Vector(pts[2]) + Vector((0, -0.02, 0.0)), (0, -1.0, 0.4), 0.15, 0.025, -10))
    # Glowing veins: flanks, chest core, along the limbs.
    def spine_at(y):
        pts = sorted((J[k][1], J[k][2], R[k]) for k in J)
        for (y0, z0, r0), (y1, z1, r1) in zip(pts, pts[1:]):
            if y0 <= y <= y1:
                t = (y - y0) / max(y1 - y0, 1e-6)
                return z0 + (z1 - z0) * t, r0 + (r1 - r0) * t
        return pts[-1][1], pts[-1][2]
    for s in (-1, 1):
        flank = []
        for y in (0.42, 0.2, -0.02, -0.24, -0.46, -0.66):
            z, r = spine_at(y)
            flank.append((s * r * 0.97, y, z - r * 0.22))
        extra.append(vein("Vein", flank))
        extra.append(vein("Vein", [LIMBS["F" + ("L" if s < 0 else "R")][1], LIMBS["F" + ("L" if s < 0 else "R")][2]], 0.007))

    body = _join("SK_Stalker", [body] + extra)
    dec = body.modifiers.new("Decimate", "DECIMATE")
    tris = sum(len(p.vertices) - 2 for p in body.data.polygons)
    dec.ratio = min(1.0, 22000 / max(tris, 1))
    _apply(body, dec)

    # Eyes: four glowing slits per side of the skull, separate mesh on the Head bone.
    eb = bmesh.new()
    for s in (-1, 1):
        for (y, z, r, x) in ((1.07, 0.89, 0.024, 0.095), (1.02, 0.86, 0.019, 0.11)):
            bmesh.ops.create_uvsphere(eb, u_segments=10, v_segments=6, radius=r,
                                      matrix=Matrix.Translation((s * x, y, z)) @ Matrix.Diagonal((1.0, 0.7, 0.55, 1.0)))
    eyes = _obj("SK_Stalker_Eyes", eb, "EG_AlienEye")

    rig = armature()
    skin_weights(body, rig)
    eyes.parent = rig
    eyes.parent_type = "BONE"
    eyes.parent_bone = "Head"
    hb = rig.data.bones["Head"]
    eyes.matrix_parent_inverse = (rig.matrix_world @ hb.matrix_local @ Matrix.Translation((0, hb.length, 0))).inverted()

    result = {"triangles": sum(len(p.vertices) - 2 for p in body.data.polygons), "dims": tuple(round(v, 2) for v in body.dimensions)}
    if export_fbx:
        exec(open(r"C:/Users/ibarr/EclipseGenesis/ArtSource/Blender/eg_export_fbx.py").read(), globals())
        for o in bpy.context.view_layer.objects:
            o.select_set(False)
        for o in (rig, body, eyes):
            o.select_set(True)
        bpy.context.view_layer.objects.active = rig
        result["fbx"] = export("Aliens", "SK_Stalker")
    return result
