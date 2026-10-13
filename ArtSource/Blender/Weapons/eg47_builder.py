"""
ECLIPSE GENESIS - EG-47 assault rifle (original design, concept art panel 6), built procedurally in Blender.

    exec(open(r"C:/Users/ibarr/EclipseGenesis/ArtSource/Blender/Weapons/eg47_builder.py").read())
    build_eg47(export_fbx=True)

Conventions (Docs/BLENDER_UNITY_PIPELINE.md): metres, barrel along +Y, +Z up, right side +X, origin at the grip
(top of the pistol grip, under the trigger). Empties: Socket_Muzzle, Socket_Sight (centre of the holographic window,
on the eye line), Socket_ShellEject (ejection port). "Magazine" is a separate object for the procedural reload.
Materials (Unity paints them, see PolyHavenLibrary.WeaponPaints):
    EG_GunMetal   black anodised aluminium     EG_GunPolymer  matte black polymer
    EG_GunSteel   dark parkerised steel        EG_GunAccent   red anodised accents
    EG_GunGlass   sight glass                  EG_GunReticle  emissive red reticle
"""
import math
import bmesh
import bpy
from mathutils import Matrix, Vector

COLORS = {
    "EG_GunMetal": (0.035, 0.036, 0.04), "EG_GunPolymer": (0.05, 0.05, 0.055), "EG_GunSteel": (0.08, 0.08, 0.085),
    "EG_GunAccent": (0.55, 0.05, 0.04), "EG_GunGlass": (0.4, 0.5, 0.45), "EG_GunReticle": (1.0, 0.1, 0.05),
}


def _mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*COLORS[name], 1.0)
    m.diffuse_color = (*COLORS[name], 1.0)
    return m


class Part:
    """One object made of boxes / prisms / cylinders, bevelled at the end."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []

    def _mi(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def poly_prism(self, pts_xz, y0, y1, mat, matrix=None):
        """Prism along Y from a polygon in the XZ plane."""
        a = [self.bm.verts.new((x, y0, z)) for x, z in pts_xz]
        b = [self.bm.verts.new((x, y1, z)) for x, z in pts_xz]
        faces = [self.bm.faces.new(a[::-1]), self.bm.faces.new(b)]
        n = len(pts_xz)
        for i in range(n):
            j = (i + 1) % n
            faces.append(self.bm.faces.new((a[i], a[j], b[j], b[i])))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        for f in faces:
            f.material_index = self._mi(mat)
        if matrix is not None:
            bmesh.ops.transform(self.bm, matrix=matrix, verts=a + b)
        return faces

    def box(self, x0, x1, y0, y1, z0, z1, mat, matrix=None):
        return self.poly_prism([(x0, z0), (x1, z0), (x1, z1), (x0, z1)], y0, y1, mat, matrix)

    def side_profile(self, pts_yz, x0, x1, mat, matrix=None):
        """Prism along X from a polygon in the YZ plane (side silhouettes: grip, stock, magazine)."""
        a = [self.bm.verts.new((x0, y, z)) for y, z in pts_yz]
        b = [self.bm.verts.new((x1, y, z)) for y, z in pts_yz]
        faces = [self.bm.faces.new(a[::-1]), self.bm.faces.new(b)]
        n = len(pts_yz)
        for i in range(n):
            j = (i + 1) % n
            faces.append(self.bm.faces.new((a[i], a[j], b[j], b[i])))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        for f in faces:
            f.material_index = self._mi(mat)
        if matrix is not None:
            bmesh.ops.transform(self.bm, matrix=matrix, verts=a + b)
        return faces

    def cyl(self, y0, y1, r0, mat, x=0.0, z=0.0, sides=16, r1=None):
        r1 = r0 if r1 is None else r1
        a = [self.bm.verts.new((x + math.cos(t) * r0, y0, z + math.sin(t) * r0)) for t in (k / sides * math.tau for k in range(sides))]
        b = [self.bm.verts.new((x + math.cos(t) * r1, y1, z + math.sin(t) * r1)) for t in (k / sides * math.tau for k in range(sides))]
        faces = [self.bm.faces.new(a[::-1]), self.bm.faces.new(b)]
        for i in range(sides):
            j = (i + 1) % sides
            faces.append(self.bm.faces.new((a[i], a[j], b[j], b[i])))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        for f in faces:
            f.material_index = self._mi(mat)
            f.smooth = True
        return faces

    def finish(self, collection, bevel=0.0012):
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-6)
        mesh = bpy.data.meshes.get(self.name) or bpy.data.meshes.new(self.name)
        mesh.clear_geometry()
        mesh.materials.clear()
        self.bm.to_mesh(mesh)
        self.bm.free()
        for m in self.mats:
            mesh.materials.append(_mat(m))
        obj = bpy.data.objects.get(self.name) or bpy.data.objects.new(self.name, mesh)
        obj.data = mesh
        if obj.name not in collection.objects:
            collection.objects.link(obj)
        for mod in list(obj.modifiers):
            obj.modifiers.remove(mod)
        if bevel > 0:
            bev = obj.modifiers.new("Bevel", "BEVEL")
            bev.width = bevel
            bev.segments = 2
            bev.limit_method = "ANGLE"
            bev.angle_limit = math.radians(35)
            bev.harden_normals = True
            wn = obj.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL")
            wn.keep_sharp = True
        # Box-projected UVs in metres (tiny detail textures in Unity tile cleanly).
        mesh.uv_layers.new(name="UVMap") if not mesh.uv_layers else None
        uvl = mesh.uv_layers[0].data
        for poly in mesh.polygons:
            n = poly.normal
            ax = max(range(3), key=lambda k: abs(n[k]))
            for li in poly.loop_indices:
                co = mesh.vertices[mesh.loops[li].vertex_index].co
                uvl[li].uv = (co.y, co.z) if ax == 0 else (co.x, co.z) if ax == 1 else (co.x, co.y)
        return obj


def _rot_x(deg, pivot):
    return Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(deg), 4, "X") @ Matrix.Translation(-pivot)


def build_eg47(export_fbx=False):
    col = bpy.data.collections.get("EG_Weapons") or bpy.data.collections.new("EG_Weapons")
    if col.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(col)
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)

    W = 0.031          # receiver width
    body = Part("SM_EG47")
    M, P, S, A = "EG_GunMetal", "EG_GunPolymer", "EG_GunSteel", "EG_GunAccent"

    # Lower receiver with magwell flare and trigger guard.
    body.side_profile([(-0.075, 0.018), (0.125, 0.018), (0.125, -0.012), (0.105, -0.034), (0.03, -0.034),
                       (0.0, -0.022), (-0.06, -0.022), (-0.075, -0.005)], -W / 2, W / 2, M)
    body.side_profile([(0.03, -0.034), (0.108, -0.034), (0.104, -0.078), (0.032, -0.078)], -0.0195, 0.0195, M)
    body.side_profile([(0.0, -0.022), (0.072, -0.034), (0.072, -0.038), (0.002, -0.052), (-0.012, -0.05), (-0.012, -0.022)], -0.004, 0.004, M)
    body.box(-0.0035, 0.0035, 0.012, 0.022, -0.04, -0.022, S, matrix=_rot_x(-12, Vector((0, 0.017, -0.03))))   # trigger
    body.box(W / 2, W / 2 + 0.003, 0.07, 0.082, -0.012, 0.0, A)        # magazine release (red)
    body.box(-W / 2 - 0.003, -W / 2, -0.03, -0.012, 0.002, 0.012, S)   # safety selector
    # Upper receiver, forward assist, ejection port and dust cover.
    body.box(-W / 2, W / 2, -0.085, 0.16, 0.018, 0.058, M)
    body.box(W / 2, W / 2 + 0.0012, 0.0, 0.062, 0.028, 0.048, S)       # dust cover (right)
    body.cyl(-0.02, -0.002, 0.007, M, x=W / 2 + 0.006, z=0.045, sides=10)
    body.box(-0.006, 0.006, -0.112, -0.085, 0.05, 0.06, M)              # charging handle
    body.box(-0.014, 0.014, -0.118, -0.108, 0.05, 0.06, A)
    # Top Picatinny rail with cross slots.
    body.box(-0.011, 0.011, -0.09, 0.47, 0.058, 0.066, M)
    y = -0.088
    while y < 0.465:
        body.box(-0.0105, 0.0105, y, y + 0.0052, 0.066, 0.071, M)
        y += 0.01
    # Free-float octagonal handguard with M-LOK slots, end cap and a red accent ring.
    oct_pts = [(math.cos(k / 8 * math.tau + math.pi / 8) * 0.0235, 0.036 + math.sin(k / 8 * math.tau + math.pi / 8) * 0.0235) for k in range(8)]
    body.poly_prism(oct_pts, 0.16, 0.47, M)
    for side in (-1, 1):
        for k in range(5):
            y0 = 0.19 + k * 0.055
            body.box(side * 0.0225 - 0.001, side * 0.0225 + 0.001, y0, y0 + 0.032, 0.03, 0.042, P)
    for k in range(5):
        y0 = 0.19 + k * 0.055
        body.box(-0.006, 0.006, y0, y0 + 0.032, 0.0115, 0.0135, P)
    body.cyl(0.465, 0.475, 0.0215, A, z=0.036, sides=8)
    # Barrel, gas block, muzzle brake with ports.
    body.cyl(0.47, 0.6, 0.0085, S, z=0.036, sides=14)
    body.cyl(0.6, 0.655, 0.0128, S, z=0.036, sides=14)
    for k in range(3):
        y0 = 0.606 + k * 0.016
        body.box(-0.016, 0.016, y0, y0 + 0.008, 0.033, 0.039, M)
    # Pistol grip (angled, finger grooves suggested by the profile).
    body.side_profile([(-0.004, -0.018), (-0.048, -0.018), (-0.075, -0.115), (-0.072, -0.122), (-0.04, -0.124),
                       (-0.03, -0.108), (-0.024, -0.09), (-0.016, -0.07), (-0.008, -0.04)], -0.0155, 0.0155, P)
    body.box(-0.0156, 0.0156, -0.074, -0.04, -0.118, -0.112, A, matrix=_rot_x(-14, Vector((0, -0.057, -0.115))))
    # Buffer tube and skeletal adjustable stock.
    body.cyl(-0.27, -0.085, 0.0145, M, z=0.03, sides=14)
    # Collapsible stock: body around the buffer tube, cheek riser, angled toe and a red butt pad.
    stock = [(-0.17, 0.052), (-0.29, 0.056), (-0.3, 0.048), (-0.3, -0.07), (-0.287, -0.078), (-0.262, -0.07),
             (-0.215, -0.012), (-0.185, 0.004), (-0.17, 0.006)]
    body.side_profile(stock, -0.016, 0.016, P)
    body.side_profile([(-0.2, 0.062), (-0.285, 0.066), (-0.285, 0.052), (-0.2, 0.05)], -0.012, 0.012, P)
    body.side_profile([(-0.215, 0.0), (-0.262, -0.055), (-0.282, -0.055), (-0.282, 0.0)], -0.0168, -0.0158, M)
    body.side_profile([(-0.215, 0.0), (-0.262, -0.055), (-0.282, -0.055), (-0.282, 0.0)], 0.0158, 0.0168, M)
    body.box(-0.0175, 0.0175, -0.306, -0.299, -0.08, 0.058, A)       # red butt pad
    # Angled foregrip.
    body.side_profile([(0.3, 0.012), (0.37, 0.012), (0.355, -0.008), (0.31, -0.03), (0.296, -0.026)], -0.013, 0.013, P)
    # Weapon light on the right rail.
    body.cyl(0.36, 0.43, 0.0105, M, x=0.034, z=0.046, sides=12)
    body.cyl(0.43, 0.436, 0.0125, A, x=0.034, z=0.046, sides=12)
    body.box(0.022, 0.03, 0.375, 0.415, 0.04, 0.05, M)
    obj = body.finish(col)

    # Holographic sight (separate object so its glass/reticle stay readable).
    sight = Part("SM_EG47_HoloSight")
    G, Rt = "EG_GunGlass", "EG_GunReticle"
    sight.box(-0.018, 0.018, 0.0, 0.118, 0.071, 0.08, M)               # base + rail clamp
    sight.box(-0.024, 0.024, 0.075, 0.118, 0.078, 0.096, M)            # battery housing (front)
    sight.box(-0.024, -0.02, 0.012, 0.118, 0.08, 0.14, M)             # hood side walls
    sight.box(0.02, 0.024, 0.012, 0.118, 0.08, 0.14, M)
    sight.box(-0.024, 0.024, 0.012, 0.118, 0.136, 0.142, M)            # hood top
    for y0, y1 in ((0.11, 0.118), (0.012, 0.02)):                       # front / rear window frames (open)
        sight.box(-0.026, 0.026, y0, y1, 0.08, 0.098, M)
        sight.box(-0.026, 0.026, y0, y1, 0.135, 0.143, M)
        sight.box(-0.026, -0.019, y0, y1, 0.098, 0.135, M)
        sight.box(0.019, 0.026, y0, y1, 0.098, 0.135, M)
    sight.box(0.024, 0.029, 0.03, 0.06, 0.1, 0.112, A)                 # brightness buttons (red)
    sight.box(-0.019, 0.019, 0.016, 0.0175, 0.098, 0.135, G)           # rear window
    sight.box(-0.019, 0.019, 0.111, 0.1125, 0.098, 0.135, G)           # front window
    sight.box(-0.0012, 0.0012, 0.1105, 0.1108, 0.1115, 0.1185, Rt)    # reticle dot
    for a in range(12):                                                # reticle ring
        t = a / 12 * math.tau
        x, z = math.cos(t) * 0.0085, 0.115 + math.sin(t) * 0.0085
        sight.box(x - 0.0007, x + 0.0007, 0.1105, 0.1108, z - 0.0007, z + 0.0007, Rt)
    sight_obj = sight.finish(col, bevel=0.0006)
    sight_obj.parent = obj

    # Curved 30-round magazine.
    mag = Part("Magazine")
    # Smooth curved body: front and back edges follow arcs bending forward.
    front, back = [], []
    for k in range(9):
        t = k / 8
        dz = -0.034 - t * 0.17
        bend = 0.042 * t * t
        front.append((0.098 + bend + 0.004 * t, dz))
        back.append((0.038 + bend, dz))
    mag.side_profile(front + back[::-1], -0.012, 0.012, M)
    mag.side_profile([(back[-1][0] - 0.004, -0.204), (front[-1][0] + 0.006, -0.204), (front[-1][0] + 0.006, -0.218), (back[-1][0] - 0.004, -0.218)], -0.0138, 0.0138, P)
    for k in range(1, 8):                                               # stiffening ribs
        t = k / 8
        mag.box(-0.0128, 0.0128, back[k][0] + 0.008, front[k][0] - 0.008, -0.034 - t * 0.17 - 0.002, -0.034 - t * 0.17 + 0.002, M)
    mag_obj = mag.finish(col, bevel=0.0008)
    mag_obj.parent = obj

    # Sockets.
    def empty(name, loc):
        e = bpy.data.objects.get(name) or bpy.data.objects.new(name, None)
        e.empty_display_size = 0.02
        if e.name not in col.objects:
            col.objects.link(e)
        e.parent = obj
        e.location = loc
        return e
    empties = [empty("Socket_Muzzle", (0, 0.657, 0.036)), empty("Socket_Sight", (0, 0.06, 0.1155)),
               empty("Socket_ShellEject", (W / 2 + 0.004, 0.03, 0.04))]

    tris = 0
    for o in (obj, sight_obj, mag_obj):
        deps = o.evaluated_get(bpy.context.evaluated_depsgraph_get())
        tris += sum(len(p.vertices) - 2 for p in deps.data.polygons)
    result = {"triangles": tris, "length": round(obj.dimensions.y, 3), "height": round(obj.dimensions.z, 3)}
    if export_fbx:
        exec(open(r"C:/Users/ibarr/EclipseGenesis/ArtSource/Blender/eg_export_fbx.py").read(), globals())
        for o in bpy.context.view_layer.objects:
            o.select_set(False)
        for o in [obj, sight_obj, mag_obj] + empties:
            o.select_set(True)
        bpy.context.view_layer.objects.active = obj
        result["missing"] = check_weapon_sockets()
        result["fbx"] = export("Weapons", "SM_EG47")
    return result
