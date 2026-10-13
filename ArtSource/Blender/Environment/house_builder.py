"""
ECLIPSE GENESIS - residential buildings, built procedurally in Blender (v2).

    exec(open(r"C:/Users/ibarr/EclipseGenesis/ArtSource/Blender/Environment/house_builder.py").read())
    build_house("SM_House_A", export_fbx=True)                    # default: 9 x 8 m, two storeys, porch
    build_street_set()                                           # all variants used by the residential street
    build_shop("SM_Shop_A", export_fbx=True)                     # two-storey brick corner shop

Conventions (Docs/BLENDER_UNITY_PIPELINE.md): metres, +Z up, the front faces +Y (Unity +Z), origin at ground
level in the centre of the footprint. Every piece is a closed box with planar UVs in metres. Window glass is a
separate object "<name>_Glass" (Unity strips its collider so it never blocks movement or shots).
Material names are remapped in Unity by PolyHavenLibrary.BlenderMaterials (CC0 Poly Haven scans):
    EG_Siding / EG_Siding_B / EG_Siding_C / EG_Siding_D   lap siding variants      EG_Trim      painted boards
    EG_Roof        asphalt-grey roof tiles                 EG_Brick     chimney / shop walls
    EG_Concrete    foundation, steps                       EG_WoodFloor floors, stairs
    EG_Plaster     interior walls and ceilings             EG_Door      painted door leaves and shutters
    EG_Metal       gutters, storefront frames              EG_Glass     glass (HDRP transparent in Unity)
"""
import math
import random
import bmesh
import bpy
from mathutils import Vector

MATS = {
    "EG_Siding": (0.42, 0.47, 0.5), "EG_Siding_B": (0.35, 0.45, 0.55), "EG_Siding_C": (0.85, 0.84, 0.8),
    "EG_Siding_D": (0.6, 0.55, 0.45), "EG_Trim": (0.85, 0.84, 0.8), "EG_Roof": (0.18, 0.18, 0.2),
    "EG_Concrete": (0.5, 0.5, 0.48), "EG_WoodFloor": (0.45, 0.33, 0.22), "EG_Plaster": (0.8, 0.78, 0.74),
    "EG_Brick": (0.5, 0.25, 0.2), "EG_Door": (0.25, 0.3, 0.35), "EG_Metal": (0.4, 0.4, 0.42), "EG_Glass": (0.6, 0.7, 0.75),
}
PITCH = math.radians(35)


def material(name):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        bsdf.inputs["Base Color"].default_value = (*MATS[name], 1.0)
    mat.diffuse_color = (*MATS[name], 1.0)
    return mat


class Builder:
    def __init__(self, name):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.name = name
        self.used = []

    def idx(self, mat):
        if mat not in self.used:
            self.used.append(mat)
        return self.used.index(mat)

    def extrude(self, poly, offset, mat, inner_dir=None, inner_mat=None):
        bm = self.bm
        a = [bm.verts.new(p) for p in poly]
        b = [bm.verts.new(p + offset) for p in poly]
        faces = [bm.faces.new(a[::-1]), bm.faces.new(b)]
        n = len(poly)
        for i in range(n):
            j = (i + 1) % n
            faces.append(bm.faces.new((a[i], a[j], b[j], b[i])))
        bmesh.ops.recalc_face_normals(bm, faces=faces)
        for f in faces:
            f.material_index = self.idx(mat)
            if inner_dir is not None and f.normal.dot(inner_dir) > 0.7:
                f.material_index = self.idx(inner_mat)
            f.smooth = False
        return faces

    def box(self, x0, x1, y0, y1, z0, z1, mat):
        if x1 - x0 < 1e-4 or y1 - y0 < 1e-4 or z1 - z0 < 1e-4:
            return
        poly = [Vector((x0, y0, z0)), Vector((x1, y0, z0)), Vector((x1, y1, z0)), Vector((x0, y1, z0))]
        self.extrude(poly, Vector((0, 0, z1 - z0)), mat)

    def finish(self, collection):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        for f in bm.faces:
            n = f.normal
            ax = max(range(3), key=lambda k: abs(n[k]))
            for loop in f.loops:
                co = loop.vert.co
                if ax == 2:
                    uv = (co.x, co.y)
                elif ax == 0:
                    uv = (co.y if n.x > 0 else -co.y, co.z)
                else:
                    uv = (-co.x if n.y > 0 else co.x, co.z)
                loop[self.uv].uv = uv
        mesh = bpy.data.meshes.get(self.name) or bpy.data.meshes.new(self.name)
        mesh.clear_geometry()
        mesh.materials.clear()
        bm.to_mesh(mesh)
        bm.free()
        for m in self.used:
            mesh.materials.append(material(m))
        obj = bpy.data.objects.get(self.name)
        if obj is None:
            obj = bpy.data.objects.new(self.name, mesh)
        obj.data = mesh
        if obj.name not in collection.objects:
            collection.objects.link(obj)
        return obj


class Wall:
    """Wall in local coords: u along the wall, w through it (0 = outside face), z up."""

    def __init__(self, b, glass, origin, u_dir, n_dir, length, thickness, z0, z1, mat, inner_mat=None):
        self.b, self.g, self.o, self.u, self.n = b, glass, Vector(origin), Vector(u_dir), Vector(n_dir)
        self.L, self.t, self.z0, self.z1 = length, thickness, z0, z1
        self.mat, self.inner_mat = mat, inner_mat
        self.openings = []

    def door(self, u0, u1, z0, z1, leaf="open", glass_panel=False):
        self.openings.append(dict(u0=u0, u1=u1, z0=z0, z1=z1, kind="door", leaf=leaf, glass_panel=glass_panel))

    def window(self, u0, u1, z0, z1, broken=False, shutters=False, storefront=False):
        self.openings.append(dict(u0=u0, u1=u1, z0=z0, z1=z1, kind="window", broken=broken, shutters=shutters, storefront=storefront))

    def p(self, u, w, z):
        return self.o + self.u * u + self.n * w + Vector((0, 0, z))

    def piece(self, u0, u1, w0, w1, z0, z1, mat, inner=True, target=None):
        if u1 - u0 < 1e-4 or w1 - w0 < 1e-4 or z1 - z0 < 1e-4:
            return
        poly = [self.p(u0, w0, z0), self.p(u1, w0, z0), self.p(u1, w1, z0), self.p(u0, w1, z0)]
        (target or self.b).extrude(poly, Vector((0, 0, z1 - z0)), mat,
                                   self.n if (inner and self.inner_mat) else None, self.inner_mat)

    def build(self):
        cuts = sorted({0.0, self.L} | {o["u0"] for o in self.openings} | {o["u1"] for o in self.openings})
        for ua, ub in zip(cuts, cuts[1:]):
            mid = (ua + ub) * 0.5
            holes = sorted((o["z0"], o["z1"]) for o in self.openings if o["u0"] <= mid <= o["u1"])
            z = self.z0
            for h0, h1 in holes:
                self.piece(ua, ub, 0, self.t, z, h0, self.mat)
                z = max(z, h1)
            self.piece(ua, ub, 0, self.t, z, self.z1, self.mat)
        for o in self.openings:
            self.dress(o)

    def dress(self, o):
        u0, u1, z0, z1, t = o["u0"], o["u1"], o["z0"], o["z1"], self.t
        trim = "EG_Metal" if o.get("storefront") else "EG_Trim"
        w = 0.06 if o.get("storefront") else 0.11
        # Exterior casing (and interior casing on partitions).
        sides = [(-0.045, 0.0)] + ([(t, t + 0.03)] if not self.inner_mat else [])
        for s0, s1 in sides:
            self.piece(u0 - w, u0, s0, s1, z0, z1 + w, trim, inner=False)
            self.piece(u1, u1 + w, s0, s1, z0, z1 + w, trim, inner=False)
            self.piece(u0 - w, u1 + w, s0, s1, z1, z1 + w, trim, inner=False)
        if o["kind"] == "window":
            mid_w0, mid_w1 = t * 0.35, t * 0.55
            fr = 0.05
            # Sash frame inside the reveal.
            self.piece(u0, u1, mid_w0, mid_w1, z0, z0 + fr, trim, inner=False)
            self.piece(u0, u1, mid_w0, mid_w1, z1 - fr, z1, trim, inner=False)
            self.piece(u0, u0 + fr, mid_w0, mid_w1, z0, z1, trim, inner=False)
            self.piece(u1 - fr, u1, mid_w0, mid_w1, z0, z1, trim, inner=False)
            panes = []
            if o.get("storefront"):
                n = max(1, round((u1 - u0) / 1.6))
                step = (u1 - u0) / n
                for i in range(1, n):
                    self.piece(u0 + i * step - fr / 2, u0 + i * step + fr / 2, mid_w0, mid_w1, z0, z1, trim, inner=False)
                panes = [(u0 + i * step + fr / 2, u0 + (i + 1) * step - fr / 2, z0 + fr, z1 - fr) for i in range(n)]
            else:
                zm = (z0 + z1) * 0.5
                self.piece(u0, u1, mid_w0 - 0.02, mid_w1 + 0.02, zm - 0.03, zm + 0.03, trim, inner=False)   # meeting rail
                um = (u0 + u1) * 0.5
                self.piece(um - 0.02, um + 0.02, mid_w0, mid_w1, zm, z1, trim, inner=False)                 # upper muntin
                panes = [(u0 + fr, u1 - fr, z0 + fr, zm - 0.03), (u0 + fr, u1 - fr, zm + 0.03, z1 - fr)]
            for i, (a, b_, c, d) in enumerate(panes):
                if o.get("broken") and (i == 0 or random.random() < 0.5):
                    continue
                self.piece(a, b_, t * 0.44, t * 0.46, c, d, "EG_Glass", inner=False, target=self.g)
            if not o.get("storefront") and z0 > self.z0 + 0.05:
                self.piece(u0 - 0.15, u1 + 0.15, -0.1, 0.06, z0 - 0.06, z0, "EG_Trim", inner=False)    # sill
            if o.get("shutters"):
                sw = min(0.5, (u1 - u0) * 0.45)
                self.piece(u0 - w - sw, u0 - w - 0.02, -0.09, -0.05, z0, z1, "EG_Door", inner=False)
                self.piece(u1 + w + 0.02, u1 + w + sw, -0.09, -0.05, z0, z1, "EG_Door", inner=False)
        else:
            width = u1 - u0
            if o["leaf"] == "closed":
                self.piece(u0, u1, t * 0.4, t * 0.4 + 0.05, z0, z1, "EG_Door", inner=False)
                if o.get("glass_panel"):
                    self.piece(u0 + 0.15, u1 - 0.15, t * 0.4 - 0.005, t * 0.4 + 0.055, z0 + 1.1, z1 - 0.2, "EG_Glass", inner=False, target=self.g)
            elif o["leaf"] == "open":
                # Hinged at u0, swung ~100 degrees into the room.
                hinge = self.p(u0, t, z0)
                d = self.n * width
                side = self.u * 0.05
                poly = [hinge, hinge + side, hinge + side + d, hinge + d]
                self.b.extrude(poly, Vector((0, 0, z1 - z0 - 0.02)), "EG_Door")


def _collection(name):
    col = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if col.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(col)
    return col


def _export(objs, category, name):
    exec(open(r"C:/Users/ibarr/EclipseGenesis/ArtSource/Blender/eg_export_fbx.py").read(), globals())
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    return export(category, name)


def build_house(name="SM_House_A", W=9.0, D=8.0, floors=2, porch=True, chimney=True, siding="EG_Siding",
                broken=0.25, shutters=False, door_leaf="open", seed=1, export_fbx=False, category="Buildings"):
    random.seed(seed)
    b, g = Builder(name), Builder(name + "_Glass")
    T, F = 0.2, 0.45
    L2 = F + 3.0
    WTOP = F + (5.8 if floors == 2 else 3.0)
    hx, hy = W / 2, D / 2
    ix, iy = hx - T, hy - T

    b.box(-hx - 0.05, hx + 0.05, -hy - 0.05, hy + 0.05, 0.0, F - 0.03, "EG_Concrete")
    b.box(-ix, ix, -iy, iy, F - 0.03, F, "EG_WoodFloor")
    # Foundation vents.
    for x in (-hx * 0.5, hx * 0.5):
        b.box(x - 0.2, x + 0.2, hy + 0.05, hy + 0.07, 0.12, 0.3, "EG_Metal")

    win_lo, win_hi = 0.95, 2.3
    lv = [F] + ([L2] if floors == 2 else [])
    R = lambda: random.random() < broken

    front = Wall(b, g, (-hx, hy, 0), (1, 0, 0), (0, -1, 0), W, T, F, WTOP, siding, "EG_Plaster")
    front.door(W / 2 - 0.55, W / 2 + 0.55, F, F + 2.25, leaf=door_leaf, glass_panel=True)
    for u in (W * 0.11, W - W * 0.11 - 1.4):
        front.window(u, u + 1.4, F + win_lo, F + win_hi, broken=R(), shutters=shutters)
    if floors == 2:
        for u in (W * 0.11, W / 2 - 0.6, W - W * 0.11 - 1.4):
            front.window(u, u + (1.2 if abs(u - (W / 2 - 0.6)) < 0.01 else 1.4), L2 + win_lo, L2 + win_hi, broken=R(), shutters=shutters)

    back = Wall(b, g, (hx, -hy, 0), (-1, 0, 0), (0, 1, 0), W, T, F, WTOP, siding, "EG_Plaster")
    back.door(W - 3.0, W - 2.0, F, F + 2.25, leaf="closed")
    back.window(W * 0.36, W * 0.36 + 1.2, F + win_lo, F + win_hi, broken=R())
    if floors == 2:
        for u in (W * 0.17, W - W * 0.17 - 1.4):
            back.window(u, u + 1.4, L2 + win_lo, L2 + win_hi, broken=R())

    side_len = D - 2 * T
    left = Wall(b, g, (-hx, -iy, 0), (0, 1, 0), (1, 0, 0), side_len, T, F, WTOP, siding, "EG_Plaster")
    right = Wall(b, g, (hx, iy, 0), (0, -1, 0), (-1, 0, 0), side_len, T, F, WTOP, siding, "EG_Plaster")
    for z in lv:
        for u in (side_len * 0.16, side_len * 0.66):
            left.window(u, u + 1.2, z + win_lo, z + win_hi, broken=R())
    right.window(side_len * 0.13, side_len * 0.13 + 1.2, F + win_lo, F + win_hi, broken=R())
    if floors == 2:
        right.window(side_len * 0.13, side_len * 0.13 + 1.2, L2 + win_lo, L2 + win_hi, broken=R())
        right.window(side_len * 0.6, side_len * 0.6 + 1.2, L2 + win_lo, L2 + win_hi, broken=R())
    for wall in (front, back, left, right):
        wall.build()

    hole_x, hole_y = hx - 1.8, hy - 3.2
    if floors == 2:
        b.box(-ix, hole_x, -iy, iy, L2 - 0.25, L2 - 0.02, "EG_Plaster")
        b.box(hole_x, ix, hole_y, iy, L2 - 0.25, L2 - 0.02, "EG_Plaster")
        b.box(-ix, hole_x, -iy, iy, L2 - 0.02, L2, "EG_WoodFloor")
        b.box(hole_x, ix, hole_y, iy, L2 - 0.02, L2, "EG_WoodFloor")
        steps, tread = 17, 0.26
        rise = (L2 - F) / steps
        y0 = hole_y - steps * tread
        for i in range(steps):
            b.box(hole_x + 0.05, ix, y0 + i * tread, y0 + (i + 1) * tread, F, F + (i + 1) * rise, "EG_WoodFloor")
        for y in (-iy + 0.05, (y0 + hole_y) / 2, hole_y - 0.05):
            b.box(hole_x - 0.05, hole_x + 0.05, y - 0.04, y + 0.04, L2, L2 + 1.0, "EG_Trim")
        b.box(hole_x - 0.06, hole_x + 0.06, -iy, hole_y, L2 + 0.95, L2 + 1.05, "EG_Trim")
    b.box(-ix, ix, -iy, iy, WTOP - 0.12, WTOP, "EG_Plaster")

    # Partitions with doorways.
    px = -W * 0.118
    for z0, z1 in ((F, (L2 - 0.25) if floors == 2 else WTOP - 0.12),) + (((L2, WTOP - 0.12),) if floors == 2 else ()):
        part = Wall(b, g, (px - 0.06, -iy, 0), (0, 1, 0), (1, 0, 0), side_len, 0.12, z0, z1, "EG_Plaster")
        part.door(side_len * 0.6, side_len * 0.6 + 1.0, z0, z0 + 2.15, leaf="none")
        part.build()
    # Baseboards along the exterior walls.
    for z in lv:
        b.box(-ix, ix, iy - 0.02, iy, z, z + 0.1, "EG_Trim")
        b.box(-ix, ix, -iy, -iy + 0.02, z, z + 0.1, "EG_Trim")

    # Corner boards, band board, gutters, downspouts.
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box(sx * hx - 0.08, sx * hx + 0.08, sy * hy - 0.08, sy * hy + 0.08, F - 0.03, WTOP, "EG_Trim")
    if floors == 2:
        bz = (L2 - 0.32, L2 - 0.14)
        b.box(-hx - 0.04, hx + 0.04, hy, hy + 0.04, *bz, "EG_Trim")
        b.box(-hx - 0.04, hx + 0.04, -hy - 0.04, -hy, *bz, "EG_Trim")
        b.box(-hx - 0.04, -hx, -hy, hy, *bz, "EG_Trim")
        b.box(hx, hx + 0.04, -hy, hy, *bz, "EG_Trim")

    # Gable roof with overhangs, fascia, gutters and ridge cap.
    over_side, over_eave, thick = 0.35, 0.55, 0.14
    tanp = math.tan(PITCH)
    ridge_z = WTOP + hy * tanp
    for s in (1, -1):
        y_eave = s * (hy + over_eave)
        z_eave = WTOP - over_eave * tanp
        normal = Vector((0, s * math.sin(PITCH), math.cos(PITCH)))
        poly = [Vector((-hx - over_side, 0, ridge_z)), Vector((hx + over_side, 0, ridge_z)),
                Vector((hx + over_side, y_eave, z_eave)), Vector((-hx - over_side, y_eave, z_eave))]
        b.extrude(poly, normal * thick, "EG_Roof")
        y0f, y1f = (y_eave - 0.03, y_eave) if s > 0 else (y_eave, y_eave + 0.03)
        b.box(-hx - over_side, hx + over_side, y0f, y1f, z_eave - 0.2, z_eave + 0.02, "EG_Trim")
        gy0, gy1 = (y_eave, y_eave + 0.13) if s > 0 else (y_eave - 0.13, y_eave)
        b.box(-hx - over_side, hx + over_side, gy0, gy1, z_eave - 0.2, z_eave - 0.08, "EG_Metal")
        b.box(hx + 0.12, hx + 0.2, gy0 + 0.02, gy0 + 0.1, 0.1, z_eave - 0.15, "EG_Metal")       # downspout
        # Soffit.
        b.box(-hx - over_side, hx + over_side, min(s * hy, y_eave), max(s * hy, y_eave), z_eave - 0.02, z_eave, "EG_Trim")
    for sx in (-1, 1):
        x_out = sx * hx
        tri = [Vector((x_out, -hy, WTOP)), Vector((x_out, hy, WTOP)), Vector((x_out, 0, ridge_z))]
        b.extrude(tri, Vector((-sx * T, 0, 0)), siding)
        b.box(min(x_out, x_out + sx * 0.05), max(x_out, x_out + sx * 0.05), -0.35, 0.35, ridge_z - 1.6, ridge_z - 1.0, "EG_Trim")
    b.box(-hx - over_side, hx + over_side, -0.1, 0.1, ridge_z + thick * 0.6, ridge_z + thick + 0.06, "EG_Roof")

    if chimney:
        cx0 = -hx - 0.75
        b.box(cx0, -hx - 0.02, -0.65, 0.55, 0.0, WTOP - 0.6, "EG_Brick")
        b.box(cx0 + 0.15, -hx + 0.45, -0.45, 0.35, WTOP - 0.6, ridge_z + 0.9, "EG_Brick")
        b.box(cx0 + 0.08, -hx + 0.52, -0.52, 0.42, ridge_z + 0.9, ridge_z + 1.0, "EG_Concrete")

    if porch:
        px_, py0, py1 = min(3.6, hx - 0.6), hy, hy + 2.4
        b.box(-px_, px_, py0, py1, F - 0.2, F, "EG_WoodFloor")
        b.box(-px_ + 0.1, px_ - 0.1, py0, py1 - 0.1, 0.0, F - 0.2, "EG_Concrete")
        posts = (-px_ + 0.1, -1.0, 1.0, px_ - 0.1)
        for x in posts:
            b.box(x - 0.09, x + 0.09, py1 - 0.23, py1 - 0.05, F, F + 2.62, "EG_Trim")
            b.box(x - 0.13, x + 0.13, py1 - 0.27, py1 - 0.01, F, F + 0.12, "EG_Trim")
        b.box(-px_, px_, py1 - 0.25, py1 - 0.03, F + 2.45, F + 2.62, "EG_Trim")
        for xa, xb in ((-px_ + 0.19, -1.09), (1.09, px_ - 0.19)):
            b.box(xa, xb, py1 - 0.18, py1 - 0.1, F + 0.88, F + 0.96, "EG_Trim")
            b.box(xa, xb, py1 - 0.18, py1 - 0.1, F + 0.1, F + 0.16, "EG_Trim")
            n = int((xb - xa) / 0.16)
            for i in range(1, n):
                x = xa + i * (xb - xa) / n
                b.box(x - 0.022, x + 0.022, py1 - 0.16, py1 - 0.12, F + 0.16, F + 0.88, "EG_Trim")
        roof = [Vector((-px_ - 0.2, py0, F + 2.95)), Vector((px_ + 0.2, py0, F + 2.95)),
                Vector((px_ + 0.2, py1 + 0.35, F + 2.62)), Vector((-px_ - 0.2, py1 + 0.35, F + 2.62))]
        b.extrude(roof, Vector((0, 0, 0.1)), "EG_Roof")
        for k, top in enumerate((F - 0.15, F - 0.3)):
            b.box(-0.95, 0.95, py1 + k * 0.3, py1 + (k + 1) * 0.3, 0.0, top, "EG_Concrete")
    else:
        b.box(-0.9, 0.9, hy, hy + 0.9, 0.0, F - 0.02, "EG_Concrete")       # stoop
        b.box(-0.9, 0.9, hy + 0.9, hy + 1.2, 0.0, F - 0.2, "EG_Concrete")
        b.box(-0.85, 0.85, hy, hy + 1.0, F + 2.5, F + 2.6, "EG_Trim")      # door canopy
    bx = -hx + 2.5   # centre of the back door (u = W-3 .. W-2 measured from +X)
    for k, top in enumerate((F - 0.15, F - 0.3)):
        b.box(bx - 0.65, bx + 0.65, -hy - (k + 1) * 0.3, -hy - k * 0.3, 0.0, top, "EG_Concrete")

    col = _collection("EG_Buildings")
    obj = b.finish(col)
    glass = g.finish(col)
    glass.parent = obj
    result = {"name": name, "faces": len(obj.data.polygons), "glass_faces": len(glass.data.polygons),
              "dims": tuple(round(v, 2) for v in obj.dimensions)}
    if export_fbx:
        result["fbx"] = _export([obj, glass], category, name)
    return result


def build_shop(name="SM_Shop_A", W=14.0, D=12.0, export_fbx=False, category="Buildings", seed=3):
    """Two-storey brick shop: storefront glazing, recessed glass door, awning, sign band, flat roof with parapet."""
    random.seed(seed)
    b, g = Builder(name), Builder(name + "_Glass")
    T, F = 0.3, 0.15
    L2 = F + 4.2
    TOP = L2 + 3.2
    hx, hy = W / 2, D / 2
    ix, iy = hx - T, hy - T
    b.box(-hx - 0.05, hx + 0.05, -hy - 0.05, hy + 0.05, 0.0, F, "EG_Concrete")
    b.box(-ix, ix, -iy, iy, F, F + 0.02, "EG_WoodFloor")

    front = Wall(b, g, (-hx, hy, 0), (1, 0, 0), (0, -1, 0), W, T, F, TOP, "EG_Brick", "EG_Plaster")
    front.window(0.8, W / 2 - 1.0, F + 0.6, F + 3.2, storefront=True, broken=True)
    front.window(W / 2 + 1.0, W - 0.8, F + 0.6, F + 3.2, storefront=True)
    front.door(W / 2 - 0.9, W / 2 + 0.9, F, F + 2.6, leaf="none")
    for u in (1.2, 4.2, W - 5.6, W - 2.6):
        front.window(u, u + 1.4, L2 + 0.9, L2 + 2.5, broken=random.random() < 0.4)
    back = Wall(b, g, (hx, -hy, 0), (-1, 0, 0), (0, 1, 0), W, T, F, TOP, "EG_Brick", "EG_Plaster")
    back.door(2.0, 3.1, F, F + 2.3, leaf="closed")
    for u in (5.0, 9.0):
        back.window(u, u + 1.2, L2 + 0.9, L2 + 2.3)
    sl = D - 2 * T
    left = Wall(b, g, (-hx, -iy, 0), (0, 1, 0), (1, 0, 0), sl, T, F, TOP, "EG_Brick", "EG_Plaster")
    right = Wall(b, g, (hx, iy, 0), (0, -1, 0), (-1, 0, 0), sl, T, F, TOP, "EG_Brick", "EG_Plaster")
    for u in (2.0, sl - 3.4):
        left.window(u, u + 1.4, F + 1.0, F + 2.9, broken=True)
        left.window(u, u + 1.4, L2 + 0.9, L2 + 2.5)
        right.window(u, u + 1.4, L2 + 0.9, L2 + 2.5)
    for wall in (front, back, left, right):
        wall.build()
    b.box(-ix, ix, -iy, iy, L2 - 0.3, L2, "EG_Plaster")
    b.box(-ix, ix, -iy, iy, TOP - 0.15, TOP, "EG_Plaster")
    # Flat roof, parapet with coping, sign band and awning.
    b.box(-hx, hx, -hy, hy, TOP, TOP + 0.1, "EG_Roof")
    for x0, x1, y0, y1 in ((-hx, hx, hy - 0.3, hy), (-hx, hx, -hy, -hy + 0.3), (-hx, -hx + 0.3, -hy, hy), (hx - 0.3, hx, -hy, hy)):
        b.box(x0, x1, y0, y1, TOP, TOP + 1.0, "EG_Brick")
        b.box(x0 - 0.05, x1 + 0.05, y0 - 0.05, y1 + 0.05, TOP + 1.0, TOP + 1.1, "EG_Concrete")
    b.box(-hx + 0.4, hx - 0.4, hy, hy + 0.12, F + 3.45, F + 4.05, "EG_Door")               # sign band
    b.box(-hx + 0.3, hx - 0.3, hy + 0.12, hy + 0.16, F + 3.4, F + 4.1, "EG_Trim")
    aw = [Vector((-hx + 0.6, hy, F + 3.3)), Vector((hx - 0.6, hy, F + 3.3)),
          Vector((hx - 0.6, hy + 1.6, F + 2.85)), Vector((-hx + 0.6, hy + 1.6, F + 2.85))]
    b.extrude(aw, Vector((0, 0, 0.05)), "EG_Metal")
    for x in (-hx + 1.2, hx - 1.2):
        b.box(x - 0.03, x + 0.03, hy, hy + 1.6, F + 3.25, F + 3.3, "EG_Metal")
    # Pilasters and a stone base course.
    for u in (-hx, -0.95, 0.95, hx):
        b.box(u - 0.2, u + 0.2, hy, hy + 0.08, F, L2 - 0.4, "EG_Brick")
    b.box(-hx - 0.04, hx + 0.04, hy, hy + 0.06, 0.0, F + 0.6, "EG_Concrete")
    # Shop counter and shelves stay in Unity (props); rear stair is not modelled (upper floor not accessible).

    col = _collection("EG_Buildings")
    obj = b.finish(col)
    glass = g.finish(col)
    glass.parent = obj
    result = {"name": name, "faces": len(obj.data.polygons), "glass_faces": len(glass.data.polygons),
              "dims": tuple(round(v, 2) for v in obj.dimensions)}
    if export_fbx:
        result["fbx"] = _export([obj, glass], category, name)
    return result


def build_street_set():
    """Variants used by the residential street (Eclipse_Genesis_OpenWorld_Realistic)."""
    out = []
    out.append(build_house("SM_House_A", 9.0, 8.0, 2, True, True, "EG_Siding", 0.25, False, "open", 1, True))
    out.append(build_house("SM_House_B", 8.0, 7.6, 1, True, True, "EG_Siding_B", 0.35, True, "closed", 2, True))
    out.append(build_house("SM_House_C", 10.0, 8.4, 2, False, True, "EG_Siding_C", 0.3, True, "closed", 3, True))
    out.append(build_house("SM_House_D", 8.6, 8.0, 2, True, False, "EG_Siding_D", 0.5, False, "closed", 4, True))
    out.append(build_shop("SM_Shop_A", export_fbx=True))
    return out
