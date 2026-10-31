"""Shared helpers for building the game's models in Blender with scripts.

Everything is described in Unity's axes (x right, y up, z forward; metres) and converted when the Blender objects are made,
so the numbers here are the same numbers the game uses. Run the model scripts with:
    blender --background --python make_diver.py
"""
import math
import os
import bpy
import bmesh
from mathutils import Vector, Matrix

ROOT = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.normpath(os.path.join(ROOT, '..'))
PREVIEWS = os.path.join(ROOT, 'previews')


def to_blender(p):
    """Unity (x, y, z) -> Blender: the character faces -Y in Blender and +Z in Unity."""
    return Vector((-p[0], -p[2], p[1]))


def to_unity(p):
    return Vector((-p[0], p[2], -p[1]))


def clamp(x, a=0.0, b=1.0):
    return max(a, min(b, x))


def ss(a, b, x):
    """Smooth step from a (0) to b (1); works when b < a too."""
    if a == b:
        return 1.0 if x >= a else 0.0
    t = clamp((x - a) / (b - a))
    return t * t * (3 - 2 * t)


# ------------------------------------------------------------------ materials

MATERIALS = {}


def material(name, hex_colour, rough=0.6, metal=0.0, emit=None):
    if name in MATERIALS:
        return MATERIALS[name]
    m = bpy.data.materials.new(name)
    h = hex_colour.lstrip('#')
    c = tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)) + (1.0,)
    lin = tuple(((v + 0.055) / 1.055) ** 2.4 if v > 0.04045 else v / 12.92 for v in c[:3]) + (1.0,)
    m.diffuse_color = lin
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    if bsdf:
        bsdf.inputs['Base Color'].default_value = lin
        bsdf.inputs['Roughness'].default_value = rough
        bsdf.inputs['Metallic'].default_value = metal
        if emit:
            e = tuple(int(emit.lstrip('#')[i:i + 2], 16) / 255 for i in (0, 2, 4)) + (1.0,)
            bsdf.inputs['Emission Color'].default_value = e
            bsdf.inputs['Emission Strength'].default_value = 1.0
    MATERIALS[name] = m
    return m


# ------------------------------------------------------------------ shapes

class Shape:
    def __init__(self, verts, faces, mat, kind, flat=False):
        self.verts = [tuple(v) for v in verts]   # Unity space
        self.faces = faces
        self.mat = mat
        self.kind = kind
        self.flat = flat   # faceted shading, for rocks and foliage

    def moved(self, delta):
        self.verts = [(x + delta[0], y + delta[1], z + delta[2]) for x, y, z in self.verts]
        return self

    def rotated(self, euler_deg, pivot=(0, 0, 0)):
        """Rotate about a pivot by Euler angles (degrees, x then y then z) in Unity axes."""
        ex, ey, ez = (math.radians(a) for a in euler_deg)
        m = Matrix.Rotation(ez, 3, 'Z') @ Matrix.Rotation(ey, 3, 'Y') @ Matrix.Rotation(ex, 3, 'X')
        pv = Vector(pivot)
        self.verts = [tuple(pv + m @ (Vector(v) - pv)) for v in self.verts]
        return self

    def scaled(self, s, pivot=(0, 0, 0)):
        pv = Vector(pivot)
        sv = Vector(s) if hasattr(s, '__len__') else Vector((s, s, s))
        self.verts = [tuple(pv + Vector((sv.x * (v[0] - pv.x), sv.y * (v[1] - pv.y), sv.z * (v[2] - pv.z)))) for v in self.verts]
        return self


def _outward(verts, faces, sides):
    """Reverse the faces if the first side quad points into the tube (the axes swap handedness, so check every time)."""
    a, b, c, d = faces[0]
    p = [to_blender(verts[i]) for i in (a, b, c, d)]
    normal = (p[2] - p[0]).cross(p[3] - p[1])
    ring = Vector((0, 0, 0))
    for i in range(sides):
        ring += to_blender(verts[i])
    ring /= sides
    centre = (p[0] + p[1] + p[2] + p[3]) / 4
    if normal.dot(centre - ring) < 0:
        return [tuple(reversed(f)) for f in faces]
    return faces


def _axis_point(axis, t, a, b):
    if axis == 'y':
        return (a, t, b)
    if axis == 'z':
        return (a, b, t)
    return (t, a, b)  # axis 'x'


def tube(profile, axis='y', sides=12, dome0=0, dome1=0, depth0=None, depth1=None,
         mat='Skin', kind='torso', closed=True):
    """A body of rings along an axis. profile rows: (t, rx, rz, ox, oz) or (t, rx, rz).
    dome0/dome1 round the ends with that many extra rings; closed=False leaves the ends open (belts, bands)."""
    rows = [tuple(r) + (0.0,) * (5 - len(r)) for r in profile]
    rings = []
    t0, rx0, rz0, ox0, oz0 = rows[0]
    t1, rx1, rz1, ox1, oz1 = rows[-1]
    sign = 1.0 if t1 >= t0 else -1.0  # direction the profile runs along the axis
    pole0 = pole1 = None
    if closed and dome0 > 0:
        d = depth0 if depth0 else (rx0 + rz0) * 0.5
        for k in range(dome0, 0, -1):
            th = k * (math.pi / 2) / (dome0 + 1)
            rings.append((t0 - sign * d * math.sin(th), rx0 * math.cos(th), rz0 * math.cos(th), ox0, oz0))
        pole0 = (t0 - sign * d, ox0, oz0)
    elif closed:
        pole0 = (t0, ox0, oz0)
    rings += rows
    if closed and dome1 > 0:
        d = depth1 if depth1 else (rx1 + rz1) * 0.5
        for k in range(1, dome1 + 1):
            th = k * (math.pi / 2) / (dome1 + 1)
            rings.append((t1 + sign * d * math.sin(th), rx1 * math.cos(th), rz1 * math.cos(th), ox1, oz1))
        pole1 = (t1 + sign * d, ox1, oz1)
    elif closed:
        pole1 = (t1, ox1, oz1)

    verts, faces = [], []
    for (t, rx, rz, ox, oz) in rings:
        for j in range(sides):
            ang = 2 * math.pi * j / sides
            verts.append(_axis_point(axis, t, ox + rx * math.cos(ang), oz + rz * math.sin(ang)))
    n = len(rings)
    for i in range(n - 1):
        for j in range(sides):
            a, b = i * sides + j, i * sides + (j + 1) % sides
            c, d = (i + 1) * sides + (j + 1) % sides, (i + 1) * sides + j
            faces.append((a, b, c, d))
    if pole0 is not None:
        verts.append(_axis_point(axis, pole0[0], pole0[1], pole0[2]))
        p = len(verts) - 1
        for j in range(sides):
            faces.append((p, (j + 1) % sides, j))
    if pole1 is not None:
        verts.append(_axis_point(axis, pole1[0], pole1[1], pole1[2]))
        p = len(verts) - 1
        base = (n - 1) * sides
        for j in range(sides):
            faces.append((p, base + j, base + (j + 1) % sides))
    return Shape(verts, _outward(verts, faces, sides), mat, kind)


def ellipsoid(center, radii, mat='Skin', kind='head', sides=12, rings=7, rot=None, cut=None):
    """A squashed sphere. rot rotates it about its own centre. cut=(lo, hi) keeps the band between two latitudes
    (degrees, -90 bottom, 90 top): (0, 90) is a dome whose flat bottom is closed."""
    if cut is None:
        lats = [-90 + 180 * (i + 1) / (rings + 1) for i in range(rings)]
    else:
        lo, hi = cut
        lats = [lo + (hi - lo) * i / rings for i in range(rings)]
    prof = [(radii[1] * math.sin(math.radians(l)), radii[0] * math.cos(math.radians(l)), radii[2] * math.cos(math.radians(l))) for l in lats]
    s = tube(prof, 'y', sides, mat=mat, kind=kind)
    top = radii[1] * math.sin(math.radians(90 if cut is None else cut[1]))
    bottom = -radii[1] if cut is None else s.verts[-2][1]
    s.verts[-2] = (s.verts[-2][0], bottom, s.verts[-2][2])
    s.verts[-1] = (s.verts[-1][0], top, s.verts[-1][2])
    if rot:
        s.rotated(rot)
    return s.moved(center)


def sweep(points, ru, rv=None, ref=(0, 0, 1), sides=8, mat='Strap', kind='head', closed=True, loop=False):
    """A tube along a path. ru/rv are the radii (numbers or per-point lists) along the reference direction and across it."""
    pts = [Vector(p) for p in points]
    n = len(pts)
    ru = ru if isinstance(ru, (list, tuple)) else [ru] * n
    rv = rv if rv is not None else ru
    rv = rv if isinstance(rv, (list, tuple)) else [rv] * n
    refv = Vector(ref)
    verts, faces = [], []
    for i, p in enumerate(pts):
        tan = ((pts[(i + 1) % n] - pts[(i - 1) % n]) if loop else (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)])).normalized()
        u = refv - tan * refv.dot(tan)
        if u.length < 1e-4:
            u = Vector((1, 0, 0)) - tan * tan.x
        u.normalize()
        v = tan.cross(u).normalized()
        for j in range(sides):
            ang = 2 * math.pi * j / sides
            verts.append(tuple(p + u * (ru[i] * math.cos(ang)) + v * (rv[i] * math.sin(ang))))
    for i in range(n if loop else n - 1):
        i2 = (i + 1) % n
        for j in range(sides):
            a, b = i * sides + j, i * sides + (j + 1) % sides
            c, d = i2 * sides + (j + 1) % sides, i2 * sides + j
            faces.append((a, b, c, d))
    if closed and not loop:
        verts.append(tuple(pts[0]))
        p0 = len(verts) - 1
        for j in range(sides):
            faces.append((p0, (j + 1) % sides, j))
        verts.append(tuple(pts[-1]))
        p1 = len(verts) - 1
        base = (n - 1) * sides
        for j in range(sides):
            faces.append((p1, base + j, base + (j + 1) % sides))
    return Shape(verts, _outward(verts, faces, sides), mat, kind)


def box(center, size, bevel=0.02, segments=2, mat='Accent', kind='spine', rot=None):
    """A rounded box (Unity axes). Built in Blender space so the bevel and the normals come out right."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    sx, sy, sz = size
    for v in bm.verts:
        v.co = Vector((v.co.x * sx, v.co.y * sz, v.co.z * sy))  # Unity y (up) is Blender z
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=segments, profile=0.5, affect='EDGES')
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.verts.ensure_lookup_table()
    verts = [tuple(to_unity(v.co)) for v in bm.verts]
    faces = [tuple(v.index for v in f.verts) for f in bm.faces]
    bm.free()
    s = Shape(verts, faces, mat, kind)
    if rot:
        s.rotated(rot)
    return s.moved(center)


# ------------------------------------------------------------------ bones and weights

class Bone:
    def __init__(self, name, head, tail, parent=None):
        self.name, self.head, self.tail, self.parent = name, head, tail, parent


def build_armature(name, bones):
    arm_data = bpy.data.armatures.new(name)
    arm = bpy.data.objects.new(name, arm_data)
    bpy.context.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    made = {}
    for b in bones:
        eb = arm_data.edit_bones.new(b.name)
        eb.head = to_blender(b.head)
        eb.tail = to_blender(b.tail)
        if (eb.tail - eb.head).length < 0.02:
            eb.tail = eb.head + Vector((0, 0, 0.02))
        made[b.name] = eb
    for b in bones:
        if b.parent:
            made[b.name].parent = made[b.parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    return arm


def make_object(name, shapes, armature, weight_fn, fat_fn=None):
    """Merge shapes into one skinned mesh object. weight_fn(kind, unity_point) -> {bone: weight}."""
    verts, faces, mat_names, face_mats, kinds, face_flat = [], [], [], [], [], []
    for s in shapes:
        if s.mat not in mat_names:
            mat_names.append(s.mat)
        mi = mat_names.index(s.mat)
        off = len(verts)
        verts += s.verts
        kinds += [s.kind] * len(s.verts)
        for f in s.faces:
            faces.append(tuple(i + off for i in f))
            face_mats.append(mi)
            face_flat.append(s.flat)
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([tuple(to_blender(v)) for v in verts], [], faces)
    mesh.update()
    for nme in mat_names:
        mesh.materials.append(MATERIALS[nme])
    for p, mi, fl in zip(mesh.polygons, face_mats, face_flat):
        p.material_index = mi
        p.use_smooth = not fl
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)

    if armature is None:
        return obj

    groups = {}
    for i, (v, k) in enumerate(zip(verts, kinds)):
        w = {b: x for b, x in weight_fn(k, v).items() if x > 0.001}
        top = sorted(w.items(), key=lambda kv: -kv[1])[:4]
        tot = sum(x for _, x in top) or 1.0
        for b, x in top:
            if b not in groups:
                groups[b] = obj.vertex_groups.new(name=b)
            groups[b].add([i], x / tot, 'REPLACE')

    if fat_fn is not None:
        obj.shape_key_add(name='Basis', from_mix=False)
        key = obj.shape_key_add(name='Fat', from_mix=False)
        for i, (v, k) in enumerate(zip(verts, kinds)):
            key.data[i].co = to_blender(fat_fn(k, v))

    mod = obj.modifiers.new('Armature', 'ARMATURE')
    mod.object = armature
    obj.parent = armature
    return obj


# ------------------------------------------------------------------ export and preview

def export_fbx(path, objects):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        add_leaf_bones=False, bake_anim=False, mesh_smooth_type='FACE', use_armature_deform_only=False,
        primary_bone_axis='Y', secondary_bone_axis='X', path_mode='COPY', embed_textures=False)


def setup_render(res=700):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'MATERIAL'
    sc.display.shading.show_cavity = True
    sc.display.shading.background_type = 'VIEWPORT'
    sc.display.shading.background_color = (0.62, 0.72, 0.82)
    sc.view_settings.view_transform = 'Standard'
    sc.render.resolution_x = sc.render.resolution_y = res
    sc.render.film_transparent = False
    world = bpy.data.worlds.new('W') if not bpy.context.scene.world else bpy.context.scene.world
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get('Background')
    if bg:
        bg.inputs[0].default_value = (0.62, 0.72, 0.82, 1)
        bg.inputs[1].default_value = 1.0
    sun = bpy.data.lights.new('Sun', 'SUN')
    sun.energy = 3.5
    so = bpy.data.objects.new('Sun', sun)
    bpy.context.collection.objects.link(so)
    so.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
    fill = bpy.data.lights.new('Fill', 'SUN')
    fill.energy = 1.2
    fo = bpy.data.objects.new('Fill', fill)
    bpy.context.collection.objects.link(fo)
    fo.rotation_euler = (math.radians(70), 0, math.radians(150))
    cam = bpy.data.cameras.new('Cam')
    co = bpy.data.objects.new('Cam', cam)
    bpy.context.collection.objects.link(co)
    sc.camera = co
    return co


def render_views(cam, name, target, distance, views, ortho=None):
    """views: list of (suffix, (dx, dy, dz) camera direction in Unity axes)."""
    os.makedirs(PREVIEWS, exist_ok=True)
    sc = bpy.context.scene
    tgt = to_blender(target)
    cam.data.lens = 70
    if ortho:
        cam.data.type = 'ORTHO'
        cam.data.ortho_scale = ortho
    for suffix, d in views:
        dv = to_blender(d).normalized()
        cam.location = tgt + dv * distance
        look = (tgt - cam.location).normalized()
        cam.rotation_euler = look.to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(PREVIEWS, f'{name}_{suffix}.png')
        bpy.ops.render.render(write_still=True)


def reset_scene():
    """Blender's factory scene has a cube, a light and a camera: remove them (and anything else) before building."""
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.armatures, bpy.data.cameras, bpy.data.lights):
        for d in list(coll):
            coll.remove(d)
    MATERIALS.clear()


def densify(rows, step=0.04):
    """Smooth a profile (rows of t, rx, rz, ox, oz) into evenly spaced rings with Catmull-Rom curves, so limbs bend cleanly."""
    rows = [tuple(r) + (0.0,) * (5 - len(r)) for r in rows]
    out = []
    n = len(rows)
    for i in range(n - 1):
        p0, p1, p2, p3 = rows[max(i - 1, 0)], rows[i], rows[i + 1], rows[min(i + 2, n - 1)]
        count = max(1, int(round(abs(p2[0] - p1[0]) / step)))
        for k in range(count):
            u = k / count
            row = []
            for c in range(5):
                a, b, cc, d = p0[c], p1[c], p2[c], p3[c]
                row.append(0.5 * ((2 * b) + (-a + cc) * u + (2 * a - 5 * b + 4 * cc - d) * u * u + (-a + 3 * b - 3 * cc + d) * u * u * u))
            out.append(tuple(row))
    out.append(rows[-1])
    return out


def smooth_head(center, rx, ry, rz, rings=13, jaw=0.14, chin=0.014, sides=16, mat='Face', kind='head'):
    """A head: an ellipsoid with a narrower jaw and the chin pushed forward, with exact poles."""
    prof = []
    for i in range(rings):
        lat = math.radians(-90 + 180 * (i + 1) / (rings + 1))
        u = math.sin(lat)
        low = max(0.0, -u)
        prof.append((ry * u, rx * math.cos(lat) * (1 - jaw * low), rz * math.cos(lat) * (1 - 0.05 * low), 0.0, chin * low))
    s = tube(prof, 'y', sides, mat=mat, kind=kind)
    s.verts[-2] = (0.0, -ry, chin)
    s.verts[-1] = (0.0, ry, 0.0)
    return s.moved(center)
