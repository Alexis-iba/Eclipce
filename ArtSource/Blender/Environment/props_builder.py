"""
ECLIPSE GENESIS - war-torn street props, built procedurally in Blender.

    exec(open(r"C:/Users/ibarr/EclipseGenesis/ArtSource/Blender/Environment/props_builder.py").read())
    build_all()

Vehicles/SM_Car_Sedan_Burnt, Vehicles/SM_Car_Sedan_Abandoned        lofted sedan body, cabin pillars, wheel arches,
                                                                    bumpers, lights, mirrors, interior; burnt = charred,
                                                                    dented, no glass, sitting on its rims.
EnvironmentProps/SM_Debris_Bricks, _Planks, _Concrete, SM_TrashBags, SM_Debris_Paper
Military/SM_Sandbag_Wall, SM_Concertina, SM_AmmoCrate, SM_Hesco
EnvironmentProps/SM_Sign_* (+ text textures rendered from Blender into Art/Imported/EnvironmentProps/Textures)

Conventions: metres, front +Y, +Z up, origin on the ground. Materials EG_* are remapped in Unity by PolyHavenLibrary;
EG_TEX_<name> materials use Art/Imported/EnvironmentProps/Textures/<name>.png as base colour.
"""
import math
import random
import bmesh
import bpy
from mathutils import Matrix, Vector

PROJECT = r"C:/Users/ibarr/EclipseGenesis"
TEX_DIR = PROJECT + "/Assets/_EclipseGenesis/Art/Imported/EnvironmentProps/Textures/"
COL = None
COLORS = {"EG_CarPaint": (0.18, 0.26, 0.33), "EG_CarBurnt": (0.08, 0.05, 0.04), "EG_Rubber": (0.03, 0.03, 0.03),
          "EG_Chrome": (0.6, 0.6, 0.6), "EG_CarInterior": (0.06, 0.06, 0.06), "EG_Glass": (0.5, 0.6, 0.65),
          "EG_Taillight": (0.5, 0.03, 0.02), "EG_Headlight": (0.8, 0.8, 0.75), "EG_Rust": (0.35, 0.18, 0.1),
          "EG_Brick": (0.5, 0.25, 0.2), "EG_Plank": (0.4, 0.3, 0.2), "EG_Concrete": (0.5, 0.5, 0.48),
          "EG_TrashBag": (0.02, 0.02, 0.025), "EG_Paper": (0.8, 0.78, 0.7), "EG_Sandbag": (0.45, 0.4, 0.3),
          "EG_Metal": (0.4, 0.4, 0.42), "EG_CrateWood": (0.25, 0.27, 0.16), "EG_HescoFabric": (0.5, 0.45, 0.35),
          "EG_SignPost": (0.5, 0.5, 0.52)}


def mat(name, color=None):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    c = color or COLORS.get(name, (0.5, 0.5, 0.5))
    bsdf.inputs["Base Color"].default_value = (*c, 1.0)
    m.diffuse_color = (*c, 1.0)
    return m


class Mesh:
    def __init__(self, name):
        self.name, self.bm, self.mats = name, bmesh.new(), []
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def mi(self, m):
        if m not in self.mats:
            self.mats.append(m)
        return self.mats.index(m)

    def faces_mat(self, faces, m):
        i = self.mi(m)
        for f in faces:
            f.material_index = i

    def box(self, c, s, m, rot=None):
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        mt = Matrix.Translation(Vector(c)) @ (rot or Matrix.Identity(4)) @ Matrix.Diagonal((*s, 1.0))
        bmesh.ops.transform(self.bm, matrix=mt, verts=res["verts"])
        fs = list({f for v in res["verts"] for f in v.link_faces})
        self.faces_mat(fs, m)
        return fs

    def cyl(self, c, r, h, m, axis="Z", seg=16, r2=None):
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=h)
        rot = {"Z": Matrix.Identity(4), "X": Matrix.Rotation(math.pi / 2, 4, "Y"), "Y": Matrix.Rotation(math.pi / 2, 4, "X")}[axis]
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation(Vector(c)) @ rot, verts=res["verts"])
        fs = list({f for v in res["verts"] for f in v.link_faces})
        self.faces_mat(fs, m)
        for f in fs:
            f.smooth = True
        return fs

    def blob(self, c, s, m, sub=2, rot=None):
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=sub, radius=1.0)
        mt = Matrix.Translation(Vector(c)) @ (rot or Matrix.Identity(4)) @ Matrix.Diagonal((*s, 1.0))
        bmesh.ops.transform(self.bm, matrix=mt, verts=res["verts"])
        fs = list({f for v in res["verts"] for f in v.link_faces})
        self.faces_mat(fs, m)
        for f in fs:
            f.smooth = True
        return fs

    def loft(self, sections, m, closed_ends=True):
        """sections: list of rings (lists of Vector) with the same vertex count."""
        rings = [[self.bm.verts.new(p) for p in ring] for ring in sections]
        fs = []
        n = len(rings[0])
        for i in range(len(rings) - 1):
            for k in range(n):
                j = (k + 1) % n
                f = self.bm.faces.new((rings[i][k], rings[i][j], rings[i + 1][j], rings[i + 1][k]))
                f.smooth = True
                fs.append(f)
        if closed_ends:
            fs.append(self.bm.faces.new(rings[0][::-1]))
            fs.append(self.bm.faces.new(rings[-1]))
        self.faces_mat(fs, m)
        return fs

    def finish(self, displace=0.0, bevel=0.0, smooth_box_uv=True):
        bm = self.bm
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        for f in bm.faces:
            n = f.normal
            ax = max(range(3), key=lambda k: abs(n[k]))
            for l in f.loops:
                co = l.vert.co
                l[self.uv].uv = (co.y, co.z) if ax == 0 else (co.x, co.z) if ax == 1 else (co.x, co.y)
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for m in self.mats:
            me.materials.append(mat(m))
        o = bpy.data.objects.new(self.name, me)
        COL.objects.link(o)
        if bevel > 0:
            b = o.modifiers.new("Bevel", "BEVEL")
            b.width, b.segments, b.limit_method = bevel, 2, "ANGLE"
            b.harden_normals = True
            _apply(o, b)
        if displace > 0:
            tex = bpy.data.textures.get("EG_Dent") or bpy.data.textures.new("EG_Dent", "CLOUDS")
            tex.noise_scale = 0.35
            sub = o.modifiers.new("Sub", "SUBSURF")
            sub.levels = 1
            sub.subdivision_type = "SIMPLE"
            _apply(o, sub)
            d = o.modifiers.new("Dent", "DISPLACE")
            d.texture, d.strength, d.texture_coords = tex, displace, "GLOBAL"
            _apply(o, d)
        return o


def _apply(o, mod):
    with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o], selected_editable_objects=[o]):
        bpy.ops.object.modifier_apply(modifier=mod.name)


def _export(objs, category, name):
    exec(open(PROJECT + "/ArtSource/Blender/eg_export_fbx.py").read(), globals())
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    return export(category, name)


# ------------------------------------------------------------------ sedan

def _section(y, half_w, z0, z1, round_r, seg=5):
    """Rounded-rectangle cross-section at station y (XZ plane), counter-clockwise from bottom-left."""
    pts = []
    r = min(round_r, half_w * 0.9, (z1 - z0) * 0.45)
    corners = [(half_w - r, z0 + r, -90), (half_w - r, z1 - r, 0), (-half_w + r, z1 - r, 90), (-half_w + r, z0 + r, 180)]
    for cx, cz, a0 in corners:
        for k in range(seg + 1):
            a = math.radians(a0 + 90 * k / seg)
            pts.append(Vector((cx + math.cos(a) * r, y, cz + math.sin(a) * r)))
    return pts


def sedan(name, burnt, seed=1):
    random.seed(seed)
    m = Mesh(name)
    paint = "EG_CarBurnt" if burnt else "EG_CarPaint"
    lift = -0.12 if burnt else 0.0          # burnt cars sit on their rims
    L = 4.5
    # Lower body: stations along Y (rear -> front): (y, half width, bottom, top, rounding).
    lower = [(-2.25, 0.8, 0.36, 0.86, 0.16), (-2.15, 0.88, 0.3, 0.92, 0.14), (-1.6, 0.9, 0.28, 0.95, 0.1),
             (0.0, 0.91, 0.27, 0.93, 0.1), (1.4, 0.9, 0.28, 0.88, 0.1), (2.05, 0.86, 0.3, 0.8, 0.13), (2.25, 0.76, 0.36, 0.72, 0.15)]
    m.loft([_section(y, w, z0 + lift, z1 + lift, r) for y, w, z0, z1, r in lower], paint)
    # Cabin (greenhouse): windshield slope, roof, rear window; pillars are solid, side windows separate panels.
    cabin = [(-1.45, 0.78, 0.9, 0.95), (-0.95, 0.76, 0.9, 1.4), (0.35, 0.76, 0.9, 1.43), (0.95, 0.8, 0.9, 0.97)]
    cab_rings = [_section(y, w, z0 + lift, z1 + lift, 0.08) for y, w, z0, z1 in cabin]
    m.loft(cab_rings, paint)
    # Side windows (glass for the abandoned car, dark holes for the burnt one).
    gl = "EG_CarInterior" if burnt else "EG_Glass"
    for s in (-1, 1):
        for y0, y1 in ((-0.85, -0.05), (0.05, 0.7)):
            m.box((s * 0.792, (y0 + y1) / 2, 1.17 + lift), (0.01, y1 - y0, 0.36), gl)
        m.box((s * 0.79, 0.0, 1.16 + lift), (0.02, 0.08, 0.46), paint)     # B pillar
    m.box((0, 0.65 + 0.61 * 0.014, 1.2 + 0.79 * 0.014 + lift), (1.3, 0.01, 0.7), gl, Matrix.Rotation(math.radians(52), 4, "X"))    # windshield on the slope
    m.box((0, -1.2 - 0.67 * 0.014, 1.175 + 0.74 * 0.014 + lift), (1.26, 0.01, 0.6), gl, Matrix.Rotation(math.radians(-48), 4, "X"))   # rear window
    # Wheel arches (dark recess), wheels.
    for y in (-1.35, 1.35):
        for s in (-1, 1):
            m.cyl((s * 0.86, y, 0.32 + lift), 0.36, 0.06, "EG_CarInterior", axis="X", seg=20)
            if burnt:
                m.cyl((s * 0.8, y, 0.24), 0.2, 0.18, "EG_Rust", axis="X", seg=18)
                m.cyl((s * 0.8, y, 0.24), 0.12, 0.2, "EG_Rust", axis="X", seg=10)
            else:
                m.cyl((s * 0.8, y, 0.32), 0.32, 0.2, "EG_Rubber", axis="X", seg=24)
                m.cyl((s * 0.81, y, 0.32), 0.19, 0.205, "EG_Chrome", axis="X", seg=18)
    # Bumpers, lights, grille, mirrors, plate, handles.
    m.box((0, 2.27, 0.42 + lift), (1.62, 0.12, 0.16), "EG_Rubber" if not burnt else "EG_CarBurnt")
    m.box((0, -2.27, 0.45 + lift), (1.6, 0.12, 0.16), "EG_Rubber" if not burnt else "EG_CarBurnt")
    for s in (-1, 1):
        m.box((s * 0.62, 2.21, 0.68 + lift), (0.3, 0.06, 0.1), "EG_Headlight" if not burnt else "EG_CarInterior")
        m.box((s * 0.66, -2.22, 0.74 + lift), (0.28, 0.05, 0.12), "EG_Taillight" if not burnt else "EG_CarInterior")
        m.box((s * 0.92, 0.55, 1.0 + lift), (0.12, 0.06, 0.08), paint)
        for y in (-0.5, 0.35):
            m.box((s * 0.915, y, 0.86 + lift), (0.02, 0.14, 0.03), "EG_Chrome" if not burnt else "EG_CarBurnt")
    m.box((0, 2.24, 0.6 + lift), (0.7, 0.04, 0.14), "EG_CarInterior")
    m.box((0, -2.26, 0.62 + lift), (0.36, 0.02, 0.12), "EG_Chrome" if not burnt else "EG_Rust")
    # Interior: seats, dashboard, wheel.
    for y in (-0.55, 0.25):
        for s in (-1, 1):
            m.box((s * 0.38, y, 0.62 + lift), (0.5, 0.5, 0.18), "EG_CarInterior")
            m.box((s * 0.38, y - 0.22, 0.95 + lift), (0.48, 0.12, 0.55), "EG_CarInterior", Matrix.Rotation(math.radians(12), 4, "X"))
    m.box((0, 0.78, 0.95 + lift), (1.4, 0.3, 0.12), "EG_CarInterior")
    m.cyl((-0.38, 0.6, 1.02 + lift), 0.18, 0.03, "EG_CarInterior", axis="Y", seg=16)
    o = m.finish(displace=0.035 if burnt else 0.008)
    for p in o.data.polygons:
        p.use_smooth = True
    return o


# ------------------------------------------------------------------ debris

def debris():
    out = []
    m = Mesh("SM_Debris_Bricks")
    for i in range(45):
        a, r = random.uniform(0, math.tau), random.uniform(0, 0.9) ** 0.7
        h = max(0.0, 0.35 * (1 - r)) + random.uniform(0, 0.08)
        rot = Matrix.Rotation(random.uniform(0, math.tau), 4, "Z") @ Matrix.Rotation(random.uniform(-0.6, 0.6), 4, "X")
        m.box((math.cos(a) * r, math.sin(a) * r, h + 0.04), (0.22, 0.1, 0.065), "EG_Brick", rot)
    m.blob((0, 0, 0.05), (0.8, 0.7, 0.18), "EG_Concrete", 2)
    out.append(("EnvironmentProps", m.finish(bevel=0.006)))

    m = Mesh("SM_Debris_Planks")
    for i in range(9):
        rot = Matrix.Rotation(random.uniform(0, math.tau), 4, "Z") @ Matrix.Rotation(random.uniform(-0.25, 0.25), 4, "Y")
        m.box((random.uniform(-0.4, 0.4), random.uniform(-0.4, 0.4), 0.03 + i * 0.03), (random.uniform(1.2, 2.4), 0.14, 0.025), "EG_Plank", rot)
    out.append(("EnvironmentProps", m.finish(bevel=0.004)))

    m = Mesh("SM_Debris_Concrete")
    for i in range(8):
        rot = Matrix.Rotation(random.uniform(0, math.tau), 4, "Z") @ Matrix.Rotation(random.uniform(-0.5, 0.5), 4, "X")
        s = random.uniform(0.25, 0.6)
        m.box((random.uniform(-0.6, 0.6), random.uniform(-0.6, 0.6), s * 0.2), (s, s * 0.8, s * 0.35), "EG_Concrete", rot)
    for i in range(4):   # bent rebar
        a = random.uniform(0, math.tau)
        m.cyl((math.cos(a) * 0.3, math.sin(a) * 0.3, 0.25), 0.008, 0.6, "EG_Rust", axis="X" if i % 2 else "Y", seg=6)
    out.append(("EnvironmentProps", m.finish(bevel=0.01, displace=0.02)))

    m = Mesh("SM_TrashBags")
    for i in range(5):
        m.blob((random.uniform(-0.5, 0.5), random.uniform(-0.4, 0.4), 0.22), (0.3, 0.26, 0.25), "EG_TrashBag", 2,
               Matrix.Rotation(random.uniform(0, math.tau), 4, "Z"))
        m.cyl((random.uniform(-0.5, 0.5), random.uniform(-0.4, 0.4), 0.45), 0.04, 0.1, "EG_TrashBag", seg=8, r2=0.0)
    out.append(("EnvironmentProps", m.finish(displace=0.04)))

    m = Mesh("SM_Debris_Paper")
    for i in range(22):
        rot = Matrix.Rotation(random.uniform(0, math.tau), 4, "Z") @ Matrix.Rotation(random.uniform(-0.2, 0.2), 4, "X")
        m.box((random.uniform(-1.5, 1.5), random.uniform(-1.5, 1.5), 0.004), (0.21, 0.3, 0.002), "EG_Paper", rot)
    out.append(("EnvironmentProps", m.finish()))
    return out


# ------------------------------------------------------------------ military

def military():
    out = []
    m = Mesh("SM_Sandbag_Wall")
    for row in range(4):
        for k in range(6):
            x = -1.5 + k * 0.58 + (0.29 if row % 2 else 0) + random.uniform(-0.03, 0.03)
            if x > 1.6:
                continue
            m.blob((x, random.uniform(-0.03, 0.03), 0.09 + row * 0.16), (0.3, 0.2, 0.09), "EG_Sandbag", 2,
                   Matrix.Rotation(random.uniform(-0.08, 0.08), 4, "Z"))
    out.append(("Military", m.finish(displace=0.012)))

    m = Mesh("SM_Concertina")
    turns, length, radius = 26, 6.0, 0.45
    pts = []
    for i in range(turns * 16 + 1):
        t = i / 16
        a = t * math.tau
        pts.append(Vector((-length / 2 + length * t / turns, math.cos(a) * radius, radius + math.sin(a) * radius * 0.95)))
    rings = []
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        s = d.cross(Vector((0, 0, 1))).normalized() if abs(d.z) < 0.99 else Vector((1, 0, 0))
        u = s.cross(d).normalized()
        rings.append([p + (s * math.cos(k / 4 * math.tau) + u * math.sin(k / 4 * math.tau)) * 0.004 for k in range(4)])
    m.loft(rings, "EG_Metal", closed_ends=False)
    for i in range(0, len(pts), 5):   # razor barbs
        m.box(pts[i], (0.03, 0.006, 0.006), "EG_Metal", Matrix.Rotation(i * 0.7, 4, "Z"))
    for x in (-2.5, 0.0, 2.5):
        m.cyl((x, 0.0, 0.5), 0.02, 1.0, "EG_Rust", seg=8)
    out.append(("Military", m.finish()))

    m = Mesh("SM_AmmoCrate")
    for k, (c, s) in enumerate((((0, 0, 0.18), (0.9, 0.45, 0.36)), ((0.15, 0.02, 0.5), (0.9, 0.45, 0.28)))):
        m.box(c, s, "EG_CrateWood", Matrix.Rotation(0.2 * k, 4, "Z"))
        for dx in (-0.35, 0.35):
            m.box((c[0] + dx, c[1], c[2]), (0.05, s[1] + 0.01, s[2] + 0.01), "EG_Metal", Matrix.Rotation(0.2 * k, 4, "Z"))
    out.append(("Military", m.finish(bevel=0.008)))

    m = Mesh("SM_Hesco")
    for k in range(3):
        x = -1.1 + k * 1.1
        m.box((x, 0, 0.6), (1.06, 1.06, 1.2), "EG_HescoFabric")
        m.blob((x, 0, 1.2), (0.5, 0.5, 0.08), "EG_Sandbag", 2)
        for gx in (-0.53, 0.53):
            for gz in range(5):
                m.box((x + gx * 0.999, 0, 0.12 + gz * 0.24), (0.012, 1.08, 0.012), "EG_Metal")
        for gy in (-0.54, 0.54):
            for gx in range(5):
                m.box((x - 0.44 + gx * 0.22, gy, 0.6), (0.012, 0.012, 1.22), "EG_Metal")
    out.append(("Military", m.finish(displace=0.015)))
    return out


# ------------------------------------------------------------------ signs

SIGNS = {
    # name: (background, text colour, lines, shape)
    "Stop": ((0.72, 0.06, 0.05), (1, 1, 1), ["ALTO"], "octagon"),
    "Evacuated": ((0.95, 0.45, 0.05), (0.05, 0.05, 0.05), ["ZONA", "EVACUADA"], "rect"),
    "Quarantine": ((0.95, 0.8, 0.05), (0.05, 0.05, 0.05), ["PELIGRO", "CUARENTENA", "GENESIS"], "rect"),
    "Checkpoint": ((0.92, 0.92, 0.9), (0.7, 0.05, 0.05), ["CHECKPOINT", "MILITAR", "ALTO TOTAL"], "rect"),
    "StreetName": ((0.06, 0.35, 0.18), (1, 1, 1), ["ELM ST"], "street"),
}


def render_sign_texture(name, bg, fg, lines):
    """Renders the sign face (text on colour) into a PNG with an orthographic camera in a temporary scene."""
    import os
    os.makedirs(TEX_DIR, exist_ok=True)
    prev = bpy.context.window.scene
    sc = bpy.data.scenes.new("EG_SignRender")
    bpy.context.window.scene = sc
    try:
        try:
            sc.render.engine = "BLENDER_WORKBENCH"
        except TypeError:
            pass
        sc.display.shading.light = "FLAT"
        sc.display.shading.color_type = "MATERIAL"
        sc.render.resolution_x = sc.render.resolution_y = 512
        sc.view_settings.view_transform = "Standard"
        bgm = bpy.data.meshes.new("bg")
        bm = bmesh.new()
        bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=1.0)
        bm.to_mesh(bgm); bm.free()
        bgm.materials.append(mat("EG_SignBG_" + name, bg))
        bgo = bpy.data.objects.new("bg", bgm)
        sc.collection.objects.link(bgo)
        n = len(lines)
        for i, line in enumerate(lines):
            cu = bpy.data.curves.new("txt", "FONT")
            cu.body = line
            cu.align_x, cu.align_y = "CENTER", "CENTER"
            spacing = 1.5 / max(n, 1)
            cu.size = min(spacing * 0.75, 2.6 / max(len(line), 1))
            cu.extrude = 0.0
            to = bpy.data.objects.new("txt", cu)
            to.location = (0, ((n - 1) / 2 - i) * spacing, 0.01)
            cu.materials.append(mat("EG_SignFG_" + name, fg))
            sc.collection.objects.link(to)
        cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = 2.0
        cam.location = (0, 0, 3)
        sc.collection.objects.link(cam)
        sc.camera = cam
        sc.render.filepath = TEX_DIR + name + ".png"
        bpy.ops.render.render(write_still=True)
    finally:
        bpy.context.window.scene = prev
        for o in list(sc.collection.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.scenes.remove(sc)
    return TEX_DIR + name + ".png"


def sign(name, shape):
    m = Mesh("SM_Sign_" + name)
    face = "EG_TEX_Sign_" + name
    post_h = 2.4 if shape != "street" else 2.9
    m.cyl((0, 0, post_h / 2), 0.03, post_h, "EG_SignPost", seg=10)
    if shape == "octagon":
        r = 0.38
        ring = [Vector((math.cos(k / 8 * math.tau + math.pi / 8) * r, 0.035, post_h - 0.1 + math.sin(k / 8 * math.tau + math.pi / 8) * r)) for k in range(8)]
        back = [p + Vector((0, -0.02, 0)) for p in ring]
        m.loft([back, ring], "EG_SignPost")
    elif shape == "street":
        m.box((0, 0, post_h - 0.05), (0.9, 0.025, 0.22), "EG_SignPost")
        fs = m.box((0, 0.014, post_h - 0.05), (0.88, 0.006, 0.2), face)
        cap = None
    else:
        m.box((0, 0.0, post_h - 0.35), (0.72, 0.025, 0.72), "EG_SignPost")
        fs = m.box((0, 0.014, post_h - 0.35), (0.7, 0.006, 0.7), face)
        cap = None
    o = m.finish()
    # Planar UVs on the +Y face spanning the sign plate (0..1).
    me = o.data
    uvl = me.uv_layers[0].data
    xs = [v.co.x for v in me.vertices if v.co.y > 0.01]
    zs = [v.co.z for v in me.vertices if v.co.y > 0.01 and v.co.z > 1.2]
    if xs and zs:
        x0, x1, z0, z1 = min(xs), max(xs), min(zs), max(zs)
        for p in me.polygons:
            if p.normal.y > 0.9 and p.center.z > 1.2:
                if shape == "octagon":
                    p.material_index = me.materials.find(face) if face in me.materials else p.material_index
                for li in p.loop_indices:
                    co = me.vertices[me.loops[li].vertex_index].co
                    uvl[li].uv = ((co.x - x0) / max(x1 - x0, 1e-4), (co.z - z0) / max(z1 - z0, 1e-4))
    if shape == "octagon" and face not in me.materials:
        me.materials.append(mat(face))
        idx = me.materials.find(face)
        for p in me.polygons:
            if p.normal.y > 0.9 and p.center.z > 1.2:
                p.material_index = idx
    return o


def build_all():
    global COL
    random.seed(4)
    COL = bpy.data.collections.get("EG_Props") or bpy.data.collections.new("EG_Props")
    if COL.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(COL)
    for o in list(COL.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    result = {}
    for nm, burnt in (("SM_Car_Sedan_Burnt", True), ("SM_Car_Sedan_Abandoned", False)):
        o = sedan(nm, burnt)
        _export([o], "Vehicles", nm)
        result[nm] = len(o.data.polygons)
    for cat, o in debris() + military():
        _export([o], cat, o.name)
        result[o.name] = len(o.data.polygons)
    for name, (bg, fg, lines, shape) in SIGNS.items():
        render_sign_texture("Sign_" + name, bg, fg, lines)
        o = sign(name, shape)
        _export([o], "EnvironmentProps", o.name)
        result[o.name] = len(o.data.polygons)
    return result
