"""
ECLIPSE GENESIS - EG-47 assault rifle v2 (original design, concept art panel 6) + gloved FPS hands, built in Blender.

    exec(open(r"C:/Users/ibarr/EclipseGenesis/ArtSource/Blender/Weapons/eg47_builder.py").read())
    build_eg47(export_fbx=True)

v2: lathe-turned barrel / muzzle brake / buffer tube, boolean cuts (ejection port, M-LOK slots, stock lightening
holes, magazine windows), rounded bevels, fasteners, curved holographic sight hood, and gloved hands with sleeves
(voxel-remeshed so palm, fingers and wrist read as one glove).

Conventions (Docs/BLENDER_UNITY_PIPELINE.md): metres, barrel along +Y, +Z up, right side +X, origin at the top of
the pistol grip. Empties: Socket_Muzzle, Socket_Sight (centre of the holo window, on the eye line), Socket_ShellEject.
Separate objects: "Magazine" (procedural reload), "Arm_R", "Arm_L" (Arm_L moves during the reload).
Materials (Unity: PolyHavenLibrary.WeaponPaints): EG_GunMetal, EG_GunPolymer, EG_GunSteel, EG_GunAccent,
EG_GunGlass, EG_GunReticle, EG_Glove, EG_Sleeve.
"""
import math
import bmesh
import bpy
from mathutils import Matrix, Vector

COLORS = {
    "EG_GunMetal": (0.05, 0.05, 0.055), "EG_GunPolymer": (0.07, 0.07, 0.075), "EG_GunSteel": (0.1, 0.1, 0.105),
    "EG_GunAccent": (0.55, 0.05, 0.04), "EG_GunGlass": (0.4, 0.5, 0.45), "EG_GunReticle": (1.0, 0.1, 0.05),
    "EG_Glove": (0.07, 0.07, 0.065), "EG_Sleeve": (0.2, 0.21, 0.16),
}
COL = None


def _mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*COLORS[name], 1.0)
    m.diffuse_color = (*COLORS[name], 1.0)
    return m


def _link(obj):
    if obj.name not in COL.objects:
        COL.objects.link(obj)
    return obj


def _new_obj(name, bm, mat):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(_mat(mat))
    return _link(bpy.data.objects.new(name, mesh))


def prism_yz(name, pts_yz, x0, x1, mat):
    """Side silhouette (YZ polygon) extruded across X."""
    bm = bmesh.new()
    a = [bm.verts.new((x0, y, z)) for y, z in pts_yz]
    b = [bm.verts.new((x1, y, z)) for y, z in pts_yz]
    bm.faces.new(a[::-1]); bm.faces.new(b)
    for i in range(len(a)):
        j = (i + 1) % len(a)
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _new_obj(name, bm, mat)


def prism_xz(name, pts_xz, y0, y1, mat):
    """Cross-section (XZ polygon) extruded along Y."""
    bm = bmesh.new()
    a = [bm.verts.new((x, y0, z)) for x, z in pts_xz]
    b = [bm.verts.new((x, y1, z)) for x, z in pts_xz]
    bm.faces.new(a[::-1]); bm.faces.new(b)
    for i in range(len(a)):
        j = (i + 1) % len(a)
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _new_obj(name, bm, mat)


def box(name, x0, x1, y0, y1, z0, z1, mat):
    return prism_xz(name, [(x0, z0), (x1, z0), (x1, z1), (x0, z1)], y0, y1, mat)


def lathe(name, profile_ry, mat, x=0.0, z=0.0, sides=24):
    """Surface of revolution around an axis parallel to Y: profile = [(radius, y), ...] from back to front."""
    bm = bmesh.new()
    rings = []
    profile_ry = [(r, y) for r, y in profile_ry if r > 1e-6]   # axis points become flat caps
    for r, y in profile_ry:
        rings.append([bm.verts.new((x + math.cos(k / sides * math.tau) * r, y, z + math.sin(k / sides * math.tau) * r)) for k in range(sides)])
    for i in range(len(rings) - 1):
        for k in range(sides):
            j = (k + 1) % sides
            f = bm.faces.new((rings[i][k], rings[i][j], rings[i + 1][j], rings[i + 1][k]))
            f.smooth = True
    bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _new_obj(name, bm, mat)


def cut(target, cutter):
    mod = target.modifiers.new("Cut", "BOOLEAN")
    mod.operation = "DIFFERENCE"
    mod.solver = "EXACT"
    mod.object = cutter
    _apply(target, mod)
    bpy.data.objects.remove(cutter, do_unlink=True)


def _apply(obj, mod):
    with bpy.context.temp_override(object=obj, active_object=obj, selected_objects=[obj], selected_editable_objects=[obj]):
        bpy.ops.object.modifier_apply(modifier=mod.name)


def bevel(obj, width, segments=3, angle=40):
    b = obj.modifiers.new("Bevel", "BEVEL")
    b.width = width
    b.segments = segments
    b.limit_method = "ANGLE"
    b.angle_limit = math.radians(angle)
    b.harden_normals = True
    _apply(obj, b)
    return obj


def transform(obj, m):
    obj.data.transform(m)
    obj.data.update()


def rot_x(deg, pivot):
    p = Vector(pivot)
    return Matrix.Translation(p) @ Matrix.Rotation(math.radians(deg), 4, "X") @ Matrix.Translation(-p)


def join(name, objs):
    objs = [o for o in objs if o is not None]
    with bpy.context.temp_override(active_object=objs[0], selected_editable_objects=objs, selected_objects=objs):
        bpy.ops.object.join()
    o = objs[0]
    o.name = name
    o.data.name = name
    return o


def smooth_normals(obj):
    for p in obj.data.polygons:
        p.use_smooth = True
    try:
        mod = obj.modifiers.new("WN", "WEIGHTED_NORMAL")
        mod.keep_sharp = True
        _apply(obj, mod)
    except Exception:
        pass


def capsule(bm, a, b, r0, r1, seg=10, rings=6):
    """Tapered capsule between points a and b, appended to bmesh (used for fingers and arms before remesh)."""
    a, b = Vector(a), Vector(b)
    axis = b - a
    L = axis.length
    ax = axis.normalized()
    ref = Vector((0, 0, 1)) if abs(ax.z) < 0.9 else Vector((1, 0, 0))
    u = ax.cross(ref).normalized()
    v = ax.cross(u).normalized()
    verts = []
    for i in range(rings + 1):
        t = i / rings
        c = a + axis * t
        r = r0 + (r1 - r0) * t
        verts.append([bm.verts.new(c + (u * math.cos(k / seg * math.tau) + v * math.sin(k / seg * math.tau)) * r) for k in range(seg)])
    for i in range(rings):
        for k in range(seg):
            j = (k + 1) % seg
            bm.faces.new((verts[i][k], verts[i][j], verts[i + 1][j], verts[i + 1][k]))
    ca = bm.verts.new(a - ax * r0 * 0.9)
    cb = bm.verts.new(b + ax * r1 * 0.9)
    for k in range(seg):
        j = (k + 1) % seg
        bm.faces.new((verts[0][j], verts[0][k], ca))
        bm.faces.new((verts[-1][k], verts[-1][j], cb))


def blob(bm, center, size, rot=None):
    """Rounded box (palm) as a squashed icosphere."""
    res = bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    m = Matrix.Translation(Vector(center)) @ (rot or Matrix.Identity(4)) @ Matrix.Diagonal((*size, 1.0))
    bmesh.ops.transform(bm, matrix=m, verts=res["verts"])


def hand(name, palm_center, palm_size, palm_rot, fingers, thumb, wrist, elbow, sleeve_r=0.048):
    """Glove + sleeve. fingers/thumb: lists of joint points; wrist/elbow: forearm axis. Remeshed into one skin."""
    bm = bmesh.new()
    blob(bm, palm_center, palm_size, palm_rot)
    for chain in fingers + [thumb]:
        for i in range(len(chain) - 1):
            r = 0.0105 - i * 0.0012
            capsule(bm, chain[i], chain[i + 1], r, r - 0.001)
    capsule(bm, palm_center, wrist, 0.03, 0.028)
    capsule(bm, wrist, Vector(wrist) + (Vector(elbow) - Vector(wrist)) * 0.18, 0.03, 0.034)
    glove = _new_obj(name + "_Glove", bm, "EG_Glove")
    rm = glove.modifiers.new("Remesh", "REMESH")
    rm.mode = "VOXEL"
    rm.voxel_size = 0.0032
    rm.use_smooth_shade = True
    _apply(glove, rm)
    sm = glove.modifiers.new("Smooth", "SMOOTH")
    sm.iterations = 6
    sm.factor = 0.6
    _apply(glove, sm)
    dec = glove.modifiers.new("Decimate", "DECIMATE")
    dec.ratio = 0.25
    _apply(glove, dec)
    smooth_normals(glove)

    # Sleeve: tapered tube with fabric folds, cuff band over the glove.
    w, e = Vector(wrist), Vector(elbow)
    axis = (e - w)
    L = axis.length
    ax = axis.normalized()
    ref = Vector((0, 0, 1)) if abs(ax.z) < 0.9 else Vector((1, 0, 0))
    u = ax.cross(ref).normalized()
    v = ax.cross(u).normalized()
    bm = bmesh.new()
    seg, rings = 20, 14
    verts = []
    for i in range(rings + 1):
        t = i / rings
        c = w + axis * (0.12 + t * 0.95)
        r = sleeve_r * (1.0 + 0.25 * t)
        ring = []
        for k in range(seg):
            a = k / seg * math.tau
            fold = 1.0 + 0.06 * math.sin(a * 3 + t * 9) * math.sin(t * math.pi * 3 + a)
            ring.append(bm.verts.new(c + (u * math.cos(a) + v * math.sin(a)) * r * fold))
        verts.append(ring)
    for i in range(rings):
        for k in range(seg):
            j = (k + 1) % seg
            f = bm.faces.new((verts[i][k], verts[i][j], verts[i + 1][j], verts[i + 1][k]))
            f.smooth = True
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    sleeve = _new_obj(name + "_Sleeve", bm, "EG_Sleeve")
    cuff = lathe(name + "_Cuff", [(sleeve_r * 0.92, 0.0), (sleeve_r * 1.02, 0.004), (sleeve_r * 1.02, 0.026), (sleeve_r * 0.92, 0.03)], "EG_Glove", sides=20)
    rot = ax.to_track_quat("Y", "Z").to_matrix().to_4x4()
    transform(cuff, Matrix.Translation(w + axis * 0.1) @ rot)
    arm = join(name, [glove, sleeve, cuff])
    return arm


def build_eg47(export_fbx=False):
    global COL
    COL = bpy.data.collections.get("EG_Weapons") or bpy.data.collections.new("EG_Weapons")
    if COL.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(COL)
    for o in list(COL.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        if m.users == 0:
            bpy.data.meshes.remove(m)

    M, P, S, A = "EG_GunMetal", "EG_GunPolymer", "EG_GunSteel", "EG_GunAccent"
    W = 0.031
    parts = []

    # ---------------- upper receiver: chamfered top edges, ejection port, forward assist, brass deflector
    upper = prism_xz("Upper", [(-W / 2, 0.018), (W / 2, 0.018), (W / 2, 0.05), (W / 2 - 0.006, 0.058), (-W / 2 + 0.006, 0.058), (-W / 2, 0.05)], -0.088, 0.16, M)
    cut(upper, box("c", W / 2 - 0.004, W / 2 + 0.01, 0.0, 0.06, 0.028, 0.048, M))                     # ejection port
    bevel(upper, 0.0015)
    parts.append(upper)
    parts.append(bevel(box("DustCover", W / 2 - 0.0035, W / 2 - 0.0015, 0.004, 0.058, 0.03, 0.046, S), 0.0005, 2))
    parts.append(bevel(prism_yz("BrassDeflector", [(-0.006, 0.03), (-0.024, 0.034), (-0.024, 0.052), (-0.002, 0.05)], W / 2, W / 2 + 0.007, M), 0.001))
    fa = lathe("ForwardAssist", [(0.0, -0.032), (0.0065, -0.031), (0.0065, -0.012), (0.0045, -0.01), (0.0, -0.01)], M, x=W / 2 + 0.006, z=0.042, sides=16)
    transform(fa, rot_x(0, (0, 0, 0)))
    parts.append(fa)
    parts.append(bevel(box("ChargingHandle", -0.006, 0.006, -0.112, -0.086, 0.05, 0.059, M), 0.001))
    parts.append(bevel(prism_yz("ChargingLatch", [(-0.118, 0.05), (-0.108, 0.05), (-0.106, 0.06), (-0.118, 0.062)], -0.016, 0.016, A), 0.0012))

    # ---------------- lower receiver: magwell flare, bolt catch, selector, pins, trigger guard, trigger
    lower = prism_yz("Lower", [(-0.075, 0.018), (0.125, 0.018), (0.125, -0.008), (0.112, -0.03), (0.032, -0.03),
                               (0.006, -0.02), (-0.06, -0.02), (-0.075, -0.004)], -W / 2, W / 2, M)
    bevel(lower, 0.0018)
    parts.append(lower)
    magwell = prism_yz("Magwell", [(0.03, -0.03), (0.112, -0.03), (0.108, -0.072), (0.1, -0.08), (0.034, -0.08), (0.03, -0.072)], -0.0195, 0.0195, M)
    cut(magwell, box("c", -0.0128, 0.0128, 0.036, 0.104, -0.09, -0.02, M))                               # open magwell
    bevel(magwell, 0.0016)
    parts.append(magwell)
    tg = bmesh.new()
    path = [(0.074, -0.03), (0.07, -0.042), (0.05, -0.05), (0.02, -0.05), (0.004, -0.042), (-0.004, -0.03)]
    for i in range(len(path) - 1):
        capsule(tg, (0, path[i][0], path[i][1]), (0, path[i + 1][0], path[i + 1][1]), 0.0034, 0.0034, seg=8, rings=1)
    parts.append(_new_obj("TriggerGuard", tg, M))
    trig = prism_yz("Trigger", [(0.02, -0.02), (0.027, -0.02), (0.026, -0.03), (0.02, -0.042), (0.016, -0.04), (0.02, -0.03)], -0.0032, 0.0032, S)
    parts.append(bevel(trig, 0.0008, 2))
    parts.append(bevel(box("MagRelease", W / 2, W / 2 + 0.003, 0.068, 0.08, -0.012, 0.0, A), 0.0008, 2))
    parts.append(bevel(prism_yz("BoltCatch", [(0.058, -0.004), (0.084, -0.004), (0.084, 0.012), (0.066, 0.012)], -W / 2 - 0.0025, -W / 2, M), 0.0008, 2))
    sel = lathe("Selector", [(0.0, 0.0), (0.006, 0.0), (0.006, 0.002), (0.0, 0.002)], S, sides=16)
    transform(sel, Matrix.Translation((-W / 2 - 0.002, -0.028, 0.004)) @ Matrix.Rotation(math.radians(90), 4, "Z"))
    parts.append(sel)
    parts.append(bevel(box("SelectorLever", -W / 2 - 0.0035, -W / 2 - 0.0015, -0.04, -0.022, 0.002, 0.007, A), 0.0006, 2))
    for y, z in ((-0.06, 0.006), (0.108, 0.006), (0.0, 0.006)):
        pin = lathe("Pin", [(0.0, 0.0), (0.0032, 0.0), (0.0032, 0.0012), (0.0, 0.0012)], S, sides=12)
        transform(pin, Matrix.Translation((W / 2, y, z)) @ Matrix.Rotation(math.radians(-90), 4, "Z"))
        parts.append(pin)

    # ---------------- top rail (Picatinny) with slots
    rail = prism_xz("Rail", [(-0.0105, 0.058), (0.0105, 0.058), (0.0105, 0.064), (0.0075, 0.071), (-0.0075, 0.071), (-0.0105, 0.064)], -0.09, 0.47, M)
    y = -0.086
    while y < 0.465:
        cut(rail, box("c", -0.012, 0.012, y, y + 0.005, 0.066, 0.075, M))
        y += 0.01
    parts.append(bevel(rail, 0.0006, 2))

    # ---------------- free-float handguard: octagon with M-LOK slots, QD cup, end cap
    oct_pts = [(math.cos(k / 8 * math.tau + math.pi / 8) * 0.0245, 0.036 + math.sin(k / 8 * math.tau + math.pi / 8) * 0.0245) for k in range(8)]
    hg = prism_xz("Handguard", oct_pts, 0.16, 0.47, M)
    cut(hg, prism_xz("bore", [(math.cos(k / 8 * math.tau + math.pi / 8) * 0.0205, 0.036 + math.sin(k / 8 * math.tau + math.pi / 8) * 0.0205) for k in range(8)], 0.15, 0.48, M))
    for k in range(5):
        y0 = 0.19 + k * 0.054
        for x0, x1, z0, z1 in ((0.018, 0.03, 0.03, 0.042), (-0.03, -0.018, 0.03, 0.042), (-0.0045, 0.0045, 0.0, 0.016)):
            cut(hg, box("slot", x0, x1, y0, y0 + 0.032, z0, z1, M))
    parts.append(bevel(hg, 0.0012, 2))
    parts.append(lathe("EndCap", [(0.0, 0.468), (0.0215, 0.468), (0.0225, 0.471), (0.0225, 0.477), (0.0, 0.477)], A, z=0.036, sides=8))
    qd = lathe("QDCup", [(0.0, 0.0), (0.0075, 0.0), (0.0075, 0.006), (0.0, 0.006)], S, sides=16)
    transform(qd, Matrix.Translation((-0.025, 0.2, 0.036)) @ Matrix.Rotation(math.radians(90), 4, "Z"))
    parts.append(qd)

    # ---------------- barrel, gas block, muzzle brake with ports
    parts.append(lathe("Barrel", [(0.0085, 0.47), (0.0085, 0.6), (0.0072, 0.6)], S, z=0.036, sides=20))
    brake = lathe("MuzzleBrake", [(0.0, 0.598), (0.0118, 0.598), (0.0128, 0.602), (0.0128, 0.648), (0.0118, 0.656), (0.0, 0.656)], S, z=0.036, sides=24)
    for k in range(3):
        y0 = 0.607 + k * 0.014
        cut(brake, box("port", -0.02, 0.02, y0, y0 + 0.007, 0.032, 0.04, S))
    parts.append(bevel(brake, 0.0006, 2))
    parts.append(bevel(box("GasBlock", -0.012, 0.012, 0.44, 0.465, 0.026, 0.05, S), 0.001))

    # ---------------- pistol grip: rounded polymer with finger groove and red base accent
    grip = prism_yz("Grip", [(-0.004, -0.018), (-0.048, -0.018), (-0.073, -0.11), (-0.071, -0.122), (-0.042, -0.126),
                             (-0.031, -0.112), (-0.026, -0.094), (-0.02, -0.084), (-0.022, -0.072), (-0.012, -0.05), (-0.006, -0.034)], -0.0155, 0.0155, P)
    bevel(grip, 0.006, 4, 30)
    parts.append(grip)
    gb = prism_yz("GripBase", [(-0.071, -0.12), (-0.04, -0.124), (-0.039, -0.13), (-0.073, -0.126)], -0.0145, 0.0145, A)
    parts.append(bevel(gb, 0.002, 2))
    for k in range(6):   # stippling ridges on the backstrap
        z = -0.04 - k * 0.012
        parts.append(bevel(box("Grip_Ridge", -0.012, 0.012, -0.056 + (z + 0.04) * 0.27 - 0.002, -0.056 + (z + 0.04) * 0.27, z - 0.0025, z, P), 0.0005, 1))

    # ---------------- buffer tube + collapsible stock with lightening cuts, cheek riser, ribbed butt pad
    parts.append(lathe("BufferTube", [(0.0, -0.28), (0.0145, -0.28), (0.0155, -0.11), (0.017, -0.105), (0.017, -0.088), (0.0, -0.088)], M, z=0.03, sides=24))
    stock = prism_yz("Stock", [(-0.165, 0.054), (-0.29, 0.058), (-0.3, 0.05), (-0.3, -0.07), (-0.287, -0.079), (-0.262, -0.072),
                               (-0.212, -0.012), (-0.182, 0.004), (-0.165, 0.006)], -0.016, 0.016, P)
    cut(stock, prism_yz("hole", [(-0.215, 0.0), (-0.258, -0.05), (-0.278, -0.052), (-0.278, 0.0)], -0.03, 0.03, P))
    cut(stock, prism_yz("tube", [(-0.17, 0.045), (-0.28, 0.045), (-0.28, 0.015), (-0.17, 0.015)], -0.0156, 0.0156, P))
    bevel(stock, 0.004, 3, 30)
    parts.append(stock)
    parts.append(bevel(prism_yz("CheekRiser", [(-0.2, 0.068), (-0.284, 0.071), (-0.287, 0.055), (-0.2, 0.053)], -0.012, 0.012, P), 0.003, 3, 30))
    pad = box("ButtPad", -0.0175, 0.0175, -0.307, -0.298, -0.082, 0.06, A)
    bevel(pad, 0.003, 3, 30)
    parts.append(pad)
    parts.append(lathe("StockLatch", [(0.0, -0.19), (0.004, -0.19), (0.004, -0.175), (0.0, -0.175)], S, z=0.008, sides=12))

    # ---------------- angled foregrip and weapon light
    fg = prism_yz("Foregrip", [(0.3, 0.012), (0.372, 0.012), (0.358, -0.006), (0.316, -0.032), (0.297, -0.028)], -0.013, 0.013, P)
    parts.append(bevel(fg, 0.004, 3, 30))
    parts.append(lathe("Light", [(0.0, 0.36), (0.0105, 0.36), (0.0105, 0.425), (0.0125, 0.43), (0.0125, 0.438), (0.0, 0.438)], M, x=0.035, z=0.046, sides=20))
    parts.append(lathe("LightLens", [(0.0, 0.4375), (0.0105, 0.4375), (0.0105, 0.439), (0.0, 0.439)], "EG_GunGlass", x=0.035, z=0.046, sides=20))
    parts.append(bevel(box("LightMount", 0.022, 0.03, 0.372, 0.418, 0.038, 0.05, M), 0.001))

    body = join("SM_EG47", parts)
    smooth_normals(body)

    # ---------------- holographic sight: base, curved hood with window, glass, buttons, battery cap
    sp = []
    sp.append(bevel(box("Holo_Base", -0.018, 0.018, 0.0, 0.118, 0.071, 0.08, M), 0.0012))
    sp.append(bevel(box("Holo_Body", -0.022, 0.022, 0.07, 0.118, 0.079, 0.097, M), 0.002))
    arch_out, arch_in = [], []
    for k in range(13):
        a = math.pi * k / 12
        arch_out.append((math.cos(a) * 0.025, 0.112 + math.sin(a) * 0.035))
        arch_in.append((math.cos(a) * 0.02, 0.112 + math.sin(a) * 0.029))
    hood = prism_xz("Holo_Hood", [(-0.025, 0.08), (0.025, 0.08)] + arch_out, 0.012, 0.118, M)
    cut(hood, prism_xz("win", [(-0.02, 0.097), (0.02, 0.097)] + arch_in, 0.0, 0.13, M))
    sp.append(bevel(hood, 0.0012, 2))
    sp.append(box("Holo_GlassRear", -0.0195, 0.0195, 0.02, 0.0215, 0.097, 0.138, "EG_GunGlass"))
    sp.append(box("Holo_GlassFront", -0.0195, 0.0195, 0.108, 0.1095, 0.097, 0.138, "EG_GunGlass"))
    ret = bmesh.new()
    bmesh.ops.create_circle(ret, cap_ends=True, radius=0.0012, segments=12, matrix=Matrix.Translation((0, 0.1075, 0.1155)) @ Matrix.Rotation(math.radians(90), 4, "X"))
    for a in range(24):
        t = a / 24 * math.tau
        res = bmesh.ops.create_circle(ret, cap_ends=True, radius=0.0005, segments=6,
                                      matrix=Matrix.Translation((math.cos(t) * 0.0085, 0.1075, 0.1155 + math.sin(t) * 0.0085)) @ Matrix.Rotation(math.radians(90), 4, "X"))
    sp.append(_new_obj("Holo_Reticle", ret, "EG_GunReticle"))
    for k in range(2):
        sp.append(bevel(box("Holo_Button", 0.022, 0.026, 0.078 + k * 0.016, 0.09 + k * 0.016, 0.084, 0.092, A), 0.0008, 2))
    bc = lathe("Holo_BatteryCap", [(0.0, 0.0), (0.0075, 0.0), (0.0075, 0.009), (0.0, 0.009)], M, sides=20)
    transform(bc, Matrix.Translation((-0.022, 0.095, 0.088)) @ Matrix.Rotation(math.radians(90), 4, "Z"))
    sp.append(bc)
    for y0 in (0.03, 0.08):
        sp.append(bevel(box("Holo_Clamp", 0.016, 0.022, y0, y0 + 0.012, 0.064, 0.074, S), 0.0008, 2))
    sight = join("SM_EG47_HoloSight", sp)
    smooth_normals(sight)
    sight.parent = body

    # ---------------- curved magazine with ribs, witness windows and a flared base plate
    front, back = [], []
    for k in range(10):
        t = k / 9
        dz = -0.03 - t * 0.17
        bend = 0.042 * t * t
        front.append((0.1 + bend + 0.004 * t, dz))
        back.append((0.038 + bend, dz))
    mag = prism_yz("Magazine", front + back[::-1], -0.012, 0.012, M)
    for k in range(3):
        t = 0.25 + k * 0.17
        yb = 0.06 + 0.042 * t * t
        cut(mag, box("win", -0.02, 0.02, yb, yb + 0.006, -0.03 - t * 0.17 - 0.012, -0.03 - t * 0.17, M))
    bevel(mag, 0.0016, 2)
    mp = [mag]
    for k in range(1, 9):
        t = k / 9
        dz = -0.03 - t * 0.17
        mp.append(bevel(box("Rib", -0.0128, 0.0128, back[k][0] + 0.008, front[k][0] - 0.01, dz - 0.0018, dz + 0.0018, M), 0.0006, 1))
    basep = prism_yz("MagBase", [(back[-1][0] - 0.005, -0.198), (front[-1][0] + 0.007, -0.198), (front[-1][0] + 0.007, -0.21), (back[-1][0] - 0.005, -0.214)], -0.0142, 0.0142, P)
    mp.append(bevel(basep, 0.0025, 3))
    magazine = join("Magazine", mp)
    smooth_normals(magazine)
    magazine.parent = body

    # ---------------- gloved hands
    q = lambda deg: Matrix.Rotation(math.radians(deg), 4, "X")
    right = hand("Arm_R",
                 palm_center=(0.02, -0.068, -0.062), palm_size=(0.016, 0.026, 0.048), palm_rot=q(-16),
                 fingers=[[(0.018, -0.03, z), (0.012, -0.004, z - 0.002), (-0.006, 0.004, z - 0.004), (-0.02, -0.006, z - 0.004)]
                          for z in (-0.054, -0.076, -0.098)]
                         + [[(0.022, -0.035, -0.032), (0.026, 0.0, -0.028), (0.026, 0.03, -0.026), (0.025, 0.052, -0.024)]],   # index along the receiver
                 thumb=[(0.004, -0.07, -0.028), (-0.012, -0.05, -0.012), (-0.022, -0.03, -0.006)],
                 wrist=(0.03, -0.11, -0.085), elbow=(0.12, -0.42, -0.22))
    left = hand("Arm_L",
                palm_center=(-0.016, 0.33, -0.015), palm_size=(0.018, 0.028, 0.034), palm_rot=q(20),
                fingers=[[(-0.02, 0.33 + d, -0.004), (-0.004, 0.335 + d, -0.034), (0.016, 0.334 + d, -0.03), (0.022, 0.33 + d, -0.012)]
                         for d in (0.012, -0.006, -0.024)],
                thumb=[(-0.026, 0.31, 0.01), (-0.024, 0.33, 0.032), (-0.016, 0.352, 0.05)],
                wrist=(-0.04, 0.28, -0.05), elbow=(-0.19, -0.04, -0.23))
    right.parent = body
    left.parent = body

    def empty(name, loc):
        e = bpy.data.objects.new(name, None)
        e.empty_display_size = 0.02
        _link(e)
        e.parent = body
        e.location = loc
        return e
    empties = [empty("Socket_Muzzle", (0, 0.658, 0.036)), empty("Socket_Sight", (0, 0.065, 0.1155)),
               empty("Socket_ShellEject", (W / 2 + 0.004, 0.03, 0.04))]

    objs = [body, sight, magazine, right, left]
    tris = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs}
    result = {"triangles": tris, "total": sum(tris.values()), "length": round(body.dimensions.y, 3)}
    if export_fbx:
        exec(open(r"C:/Users/ibarr/EclipseGenesis/ArtSource/Blender/eg_export_fbx.py").read(), globals())
        for o in bpy.context.view_layer.objects:
            o.select_set(False)
        for o in objs + empties:
            o.select_set(True)
        bpy.context.view_layer.objects.active = body
        result["missing"] = check_weapon_sockets()
        result["fbx"] = export("Weapons", "SM_EG47")
    return result
