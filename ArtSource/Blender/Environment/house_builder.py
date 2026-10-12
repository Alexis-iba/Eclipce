"""
ECLIPSE GENESIS - two-storey wooden house (residential zone), built procedurally in Blender.

Run through Blender MCP (execute_blender_code) or the Text Editor:
    exec(open(r"C:/Users/ibarr/EclipseGenesis/ArtSource/Blender/Environment/house_builder.py").read())
    build_house("SM_House_A", export_fbx=True)

Conventions (Docs/BLENDER_UNITY_PIPELINE.md): metres, +Z up, the front door faces +Y (Unity +Z),
origin at ground level in the centre of the footprint. Every piece is a closed box with planar UVs
in metres, so the Unity materials tile by world size. Materials (remapped in Unity by PolyHavenLibrary):
    EG_Siding     weathered_plank_siding     EG_Trim      white_plaster_02 (painted boards)
    EG_Roof       roof_09                    EG_Concrete  concrete_floor_worn_001
    EG_WoodFloor  wood_floor_worn            EG_Plaster   white_plaster_02 (interior)
Walls have real openings (no booleans): the explorable interior has two floors, a staircase,
partitions with doorways, a porch and back steps. Windows have no glass (abandoned town; keeps
line of fire and avoids invisible-collider issues).
"""
import math
import bmesh
import bpy
from mathutils import Vector

MATS = {
    "EG_Siding": (0.42, 0.47, 0.5),
    "EG_Trim": (0.85, 0.84, 0.8),
    "EG_Roof": (0.18, 0.16, 0.15),
    "EG_Concrete": (0.5, 0.5, 0.48),
    "EG_WoodFloor": (0.45, 0.33, 0.22),
    "EG_Plaster": (0.8, 0.78, 0.74),
}

# Dimensions (metres)
W, D = 9.0, 8.0          # footprint (x, y)
T = 0.2                  # exterior wall thickness
F = 0.45                 # ground floor level (top of foundation)
L2 = F + 3.0             # upper floor level
WTOP = L2 + 2.8          # top of the walls
PITCH = math.radians(35)


class Builder:
    def __init__(self, name):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.name = name
        self.mat_index = {n: i for i, n in enumerate(MATS)}

    def extrude(self, poly, offset, mat, inner_dir=None, inner_mat=None):
        """Closed prism from a planar polygon (list of Vector) extruded by offset."""
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
            f.material_index = self.mat_index[mat]
            if inner_dir is not None and f.normal.dot(inner_dir) > 0.7:
                f.material_index = self.mat_index[inner_mat]
            f.smooth = False
        return faces

    def box(self, x0, x1, y0, y1, z0, z1, mat, inner_dir=None, inner_mat=None):
        if x1 - x0 < 1e-4 or y1 - y0 < 1e-4 or z1 - z0 < 1e-4:
            return
        poly = [Vector((x0, y0, z0)), Vector((x1, y0, z0)), Vector((x1, y1, z0)), Vector((x0, y1, z0))]
        self.extrude(poly, Vector((0, 0, z1 - z0)), mat, inner_dir, inner_mat)

    def finish(self):
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
        mesh = bpy.data.meshes.new(self.name)
        bm.to_mesh(mesh)
        bm.free()
        for m in MATS:
            mesh.materials.append(material(m))
        obj = bpy.data.objects.new(self.name, mesh)
        return obj


def material(name):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (*MATS[name], 1.0)
            bsdf.inputs["Roughness"].default_value = 0.8
    return mat


class Wall:
    """Exterior or interior wall in local coords: u along the wall, w through it (0 = outside face), z up."""

    def __init__(self, b, origin, u_dir, n_dir, length, thickness, z0, z1, mat, inner_mat=None):
        self.b, self.o, self.u, self.n = b, Vector(origin), Vector(u_dir), Vector(n_dir)
        self.L, self.t, self.z0, self.z1 = length, thickness, z0, z1
        self.mat, self.inner_mat = mat, inner_mat
        self.openings = []

    def opening(self, u0, u1, z0, z1, frame=True, sill=True):
        self.openings.append((u0, u1, z0, z1, frame, sill))
        return self

    def piece(self, u0, u1, w0, w1, z0, z1, mat, inner=True):
        if u1 - u0 < 1e-4 or w1 - w0 < 1e-4 or z1 - z0 < 1e-4:
            return
        base = self.o + Vector((0, 0, z0))
        p = [base + self.u * u0 + self.n * w0, base + self.u * u1 + self.n * w0,
             base + self.u * u1 + self.n * w1, base + self.u * u0 + self.n * w1]
        self.b.extrude(p, Vector((0, 0, z1 - z0)), mat,
                       self.n if (inner and self.inner_mat) else None, self.inner_mat)

    def build(self):
        cuts = sorted({0.0, self.L} | {o[0] for o in self.openings} | {o[1] for o in self.openings})
        for ua, ub in zip(cuts, cuts[1:]):
            mid = (ua + ub) * 0.5
            holes = sorted((o[2], o[3]) for o in self.openings if o[0] <= mid <= o[1])
            z = self.z0
            for h0, h1 in holes:
                self.piece(ua, ub, 0, self.t, z, h0, self.mat)
                z = max(z, h1)
            self.piece(ua, ub, 0, self.t, z, self.z1, self.mat)
        for u0, u1, z0, z1, frame, sill in self.openings:
            if not frame:
                continue
            # Painted casing on the outside face (and inside for interior doors).
            w = 0.1
            for side in ((-0.045, 0.0),) + (((self.t, self.t + 0.03),) if not self.inner_mat else ()):
                self.piece(u0 - w, u0, side[0], side[1], z0, z1 + w, "EG_Trim", inner=False)
                self.piece(u1, u1 + w, side[0], side[1], z0, z1 + w, "EG_Trim", inner=False)
                self.piece(u0 - w, u1 + w, side[0], side[1], z1, z1 + w, "EG_Trim", inner=False)
            if sill and z0 > self.z0 + 0.05:
                self.piece(u0 - 0.14, u1 + 0.14, -0.09, 0.05, z0 - 0.05, z0, "EG_Trim", inner=False)


def build_house(name="SM_House_A", export_fbx=False, collection_name="EG_House"):
    b = Builder(name)
    hx, hy = W / 2, D / 2
    ix, iy = hx - T, hy - T          # interior half extents

    # Foundation and ground floor.
    b.box(-hx - 0.05, hx + 0.05, -hy - 0.05, hy + 0.05, 0.0, F - 0.03, "EG_Concrete")
    b.box(-ix, ix, -iy, iy, F - 0.03, F, "EG_WoodFloor")

    # Exterior walls: siding outside, plaster inside.
    door_z, win_lo, win_hi = F + 2.25, 0.95, 2.3
    front = Wall(b, (-hx, hy, 0), (1, 0, 0), (0, -1, 0), W, T, F, WTOP, "EG_Siding", "EG_Plaster")
    front.opening(3.95, 5.05, F, door_z, sill=False)
    for u in (1.0, 6.6):
        front.opening(u, u + 1.4, F + win_lo, F + win_hi)
    for u in (1.0, 3.9, 6.6):
        front.opening(u, u + (1.2 if u == 3.9 else 1.4), L2 + win_lo, L2 + win_hi)

    back = Wall(b, (hx, -hy, 0), (-1, 0, 0), (0, 1, 0), W, T, F, WTOP, "EG_Siding", "EG_Plaster")
    back.opening(6.0, 7.0, F, door_z, sill=False)
    back.opening(3.2, 4.4, F + win_lo, F + win_hi)
    for u in (1.5, 6.1):
        back.opening(u, u + 1.4, L2 + win_lo, L2 + win_hi)

    left = Wall(b, (-hx, -iy, 0), (0, 1, 0), (1, 0, 0), D - 2 * T, T, F, WTOP, "EG_Siding", "EG_Plaster")
    right = Wall(b, (hx, iy, 0), (0, -1, 0), (-1, 0, 0), D - 2 * T, T, F, WTOP, "EG_Siding", "EG_Plaster")
    for u in (1.2, 5.0):
        left.opening(u, u + 1.2, F + win_lo, F + win_hi)
        left.opening(u, u + 1.2, L2 + win_lo, L2 + win_hi)
    right.opening(1.0, 2.2, F + win_lo, F + win_hi)
    right.opening(1.0, 2.2, L2 + win_lo, L2 + win_hi)
    right.opening(4.5, 5.7, L2 + win_lo, L2 + win_hi)
    for wall in (front, back, left, right):
        wall.build()

    # Upper floor slab with the stairwell, and the ceiling.
    hole_x, hole_y = 2.7, 0.82
    b.box(-ix, hole_x, -iy, iy, L2 - 0.25, L2 - 0.02, "EG_Plaster")
    b.box(hole_x, ix, hole_y, iy, L2 - 0.25, L2 - 0.02, "EG_Plaster")
    b.box(-ix, hole_x, -iy, iy, L2 - 0.02, L2, "EG_WoodFloor")
    b.box(hole_x, ix, hole_y, iy, L2 - 0.02, L2, "EG_WoodFloor")
    b.box(-ix, ix, -iy, iy, WTOP - 0.12, WTOP, "EG_Plaster")

    # Staircase along the right wall, climbing towards +Y (17 risers of ~0.176 m, 0.26 m treads).
    steps, tread = 17, 0.26
    rise = (L2 - F) / steps
    y0 = hole_y - steps * tread
    for i in range(steps):
        b.box(hole_x + 0.05, ix, y0 + i * tread, y0 + (i + 1) * tread, F, F + (i + 1) * rise, "EG_WoodFloor")

    # Stairwell railing on the upper floor.
    for y in (-iy + 0.05, -1.5, 0.0, hole_y - 0.05):
        b.box(hole_x - 0.05, hole_x + 0.05, y - 0.04, y + 0.04, L2, L2 + 1.0, "EG_Trim")
    b.box(hole_x - 0.06, hole_x + 0.06, -iy, hole_y, L2 + 0.95, L2 + 1.05, "EG_Trim")

    # Interior partitions with doorways (living room | hall + kitchen; two bedrooms upstairs).
    for z0, z1 in ((F, L2 - 0.25), (L2, WTOP - 0.12)):
        part = Wall(b, (-1.06, -iy, 0), (0, 1, 0), (1, 0, 0), D - 2 * T, 0.12, z0, z1, "EG_Plaster")
        part.opening(4.6, 5.6, z0, z0 + 2.15, frame=True, sill=False)
        part.build()
    upper = Wall(b, (-ix, -0.06, 0), (1, 0, 0), (0, 1, 0), ix - 1.06 + ix, 0.12, L2, WTOP - 0.12, "EG_Plaster")
    upper.L = (-1.06) - (-ix)
    upper.opening(1.2, 2.1, L2, L2 + 2.15, frame=True, sill=False)
    upper.build()

    # Corner boards and the band between floors.
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box(sx * hx - 0.08, sx * hx + 0.08, sy * hy - 0.08, sy * hy + 0.08, F - 0.03, WTOP, "EG_Trim")
    band_z = (L2 - 0.32, L2 - 0.14)
    b.box(-hx - 0.04, hx + 0.04, hy, hy + 0.04, *band_z, "EG_Trim")
    b.box(-hx - 0.04, hx + 0.04, -hy - 0.04, -hy, *band_z, "EG_Trim")
    b.box(-hx - 0.04, -hx, -hy, hy, *band_z, "EG_Trim")
    b.box(hx, hx + 0.04, -hy, hy, *band_z, "EG_Trim")

    # Gable roof (ridge along X) with overhangs, gable walls and fascia.
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
        # Fascia board under the eave.
        b.box(-hx - over_side, hx + over_side, y_eave - 0.03 if s > 0 else y_eave, y_eave if s > 0 else y_eave + 0.03,
              z_eave - 0.2, z_eave + 0.02, "EG_Trim")
    for sx in (-1, 1):
        x_out = sx * hx
        tri = [Vector((x_out, -hy, WTOP)), Vector((x_out, hy, WTOP)), Vector((x_out, 0, ridge_z))]
        b.extrude(tri, Vector((-sx * T, 0, 0)), "EG_Siding")
        # Small attic vent.
        b.box(min(x_out, x_out + sx * 0.05), max(x_out, x_out + sx * 0.05), -0.35, 0.35, ridge_z - 1.6, ridge_z - 1.0, "EG_Trim")
    b.box(-hx - over_side, hx + over_side, -0.08, 0.08, ridge_z + thick * 0.6, ridge_z + thick + 0.08, "EG_Roof")

    # Front porch: deck, posts, railing, shed roof and steps.
    px, py0, py1 = 3.6, hy, hy + 2.4
    b.box(-px, px, py0, py1, F - 0.2, F, "EG_WoodFloor")
    b.box(-px + 0.1, px - 0.1, py0, py1 - 0.1, 0.0, F - 0.2, "EG_Concrete")
    post_x = (-px + 0.1, -1.0, 1.0, px - 0.1)
    for x in post_x:
        b.box(x - 0.08, x + 0.08, py1 - 0.22, py1 - 0.06, F, F + 2.62, "EG_Trim")
    b.box(-px, px, py1 - 0.24, py1 - 0.04, F + 2.45, F + 2.62, "EG_Trim")
    for xa, xb in ((-px + 0.18, -1.08), (1.08, px - 0.18)):
        b.box(xa, xb, py1 - 0.18, py1 - 0.1, F + 0.88, F + 0.96, "EG_Trim")
        b.box(xa, xb, py1 - 0.18, py1 - 0.1, F + 0.1, F + 0.16, "EG_Trim")
        n = int((xb - xa) / 0.22)
        for i in range(1, n):
            x = xa + i * (xb - xa) / n
            b.box(x - 0.025, x + 0.025, py1 - 0.16, py1 - 0.12, F + 0.16, F + 0.88, "EG_Trim")
    roof_poly = [Vector((-px - 0.2, py0, F + 2.95)), Vector((px + 0.2, py0, F + 2.95)),
                 Vector((px + 0.2, py1 + 0.35, F + 2.62)), Vector((-px - 0.2, py1 + 0.35, F + 2.62))]
    b.extrude(roof_poly, Vector((0, 0, 0.1)), "EG_Roof")
    for k, top in enumerate((F - 0.15, F - 0.3)):
        b.box(-0.95, 0.95, py1 + k * 0.3, py1 + (k + 1) * 0.3, 0.0, top, "EG_Concrete")
    # Back steps.
    bx = hx - 6.5   # centre of the back door (u 6.0..7.0 from +X)
    for k, top in enumerate((F - 0.15, F - 0.3)):
        b.box(bx - 0.65, bx + 0.65, -hy - (k + 1) * 0.3, -hy - k * 0.3, 0.0, top, "EG_Concrete")

    obj = b.finish()
    col = bpy.data.collections.get(collection_name) or bpy.data.collections.new(collection_name)
    if col.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(col)
    old = bpy.data.objects.get(name)
    if old is not None and old is not obj:
        bpy.data.objects.remove(old, do_unlink=True)
        obj.name = name
    col.objects.link(obj)

    result = {"name": obj.name, "verts": len(obj.data.vertices), "faces": len(obj.data.polygons),
              "dims": tuple(round(v, 2) for v in obj.dimensions)}
    if export_fbx:
        exec(open(r"C:/Users/ibarr/EclipseGenesis/ArtSource/Blender/eg_export_fbx.py").read(), globals())
        for o in bpy.context.view_layer.objects:
            o.select_set(False)
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        result["fbx"] = export("Environment", name)
    return result
