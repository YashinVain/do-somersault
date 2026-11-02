"""From the smooth MPFB body to the game body: flat low-poly facets, the game's 13 bones, eyes / brows / mouth.

finish(kind) -> (body, rig, eyes). The dense smooth body stays hidden as LP_<kind>_dense (clothes are cut from it).
"""
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib  # noqa: E402
import mpfb_body  # noqa: E402
import body as old  # noqa: E402  (ctx, decimate_to, flat_shade helpers)
from lib import material  # noqa: E402

SKIN, EYE, IRIS, PUPIL, MOUTH, LIP, HAIR = '#EFBC98', '#F7F5F0', '#4B3426', '#101218', '#6E2F2B', '#C77F73', '#3A2A20'
HEAD_W, HAND_W, FOOT_W, BODY_W = 0.25, 0.6, 0.4, 1.0  # decimation weights: low = keep detail  # decimation weights: low = keep detail


def ensure_materials():
    for n, c in (('Skin', SKIN), ('Face', SKIN), ('EyeWhite', EYE), ('Iris', IRIS), ('Pupil', PUPIL), ('Mouth', MOUTH), ('Lip', LIP), ('Hair', HAIR)):
        material(n, c, 0.8)


def ray_front(ob, x, z):
    """Where a ray along +y (Blender) from in front of the face hits the body at (x, z)."""
    hit, loc, nor, _ = ob.ray_cast(Vector((x, -1.5, z)), Vector((0, 1, 0)), distance=3.0)
    return (loc, nor) if hit else None


def hand_set(ob, lm):
    """Vertices of the hands: the ForeArm group below the wrist (the MPFB fingers and palm fold into ForeArm)."""
    names = {g.index: g.name for g in ob.vertex_groups}
    out = set()
    for v in ob.data.vertices:
        for g in v.groups:
            for s in ('L', 'R'):
                if names.get(g.group) == f'ForeArm_{s}' and g.weight > 0.5 and v.co.z < lm[f'wrist_{s}'].z + 0.03:
                    out.add(v.index)
    return out


def classify(ob, lm):
    """Faces of the head, neck and hands take the Face material; the rest is Skin."""
    head_z = lm['head'].z
    hands = hand_set(ob, lm)
    idx = []
    for pl in ob.data.polygons:
        face = pl.center.z > head_z - 0.03 or sum(1 for i in pl.vertices if i in hands) >= 2
        idx.append(1 if face else 0)
    ob.data.polygons.foreach_set('material_index', idx)


# ---- face ---------------------------------------------------------------------------------------------------------

def _orient_out(verts, faces, pivot):
    """Flip faces whose normal points toward the pivot (for convex pieces)."""
    out = []
    for f in faces:
        pts = [verts[i] for i in f]
        c = sum(pts, Vector()) / len(pts)
        n = (pts[1] - pts[0]).cross(pts[2] - pts[0])
        out.append(tuple(f) if n.dot(c - pivot) >= 0 else tuple(reversed(f)))
    return out


def eyeball(centre, rx, ry, rz):
    """A white ellipsoid (8 x 5 facets) with an iris cone and a pupil cone on its front (-Y). Returns verts, faces, material per face."""
    U, V = 8, 5
    verts = [centre + Vector((0, 0, rz)), centre - Vector((0, 0, rz))]
    for j in range(1, V):
        th = math.pi * j / V
        for i in range(U):
            ph = 2 * math.pi * i / U + math.pi / U
            verts.append(centre + Vector((rx * math.sin(th) * math.cos(ph), ry * math.sin(th) * math.sin(ph), rz * math.cos(th))))
    faces = []
    ring = lambda j, i: 2 + (j - 1) * U + (i % U)
    for i in range(U):
        faces.append((0, ring(1, i + 1), ring(1, i)))
        faces.append((1, ring(V - 1, i), ring(V - 1, i + 1)))
        for j in range(1, V - 1):
            faces.append((ring(j, i), ring(j, i + 1), ring(j + 1, i + 1), ring(j + 1, i)))
    faces = _orient_out(verts, faces, centre)
    mats = [0] * len(faces)

    def cone(radius, lift, mat):
        c = len(verts)
        verts.append(centre + Vector((0, -ry - lift - 0.0006, 0)))
        for i in range(8):
            t = 2 * math.pi * i / 8
            dx, dz = radius * math.cos(t), radius * math.sin(t)
            depth = ry * math.sqrt(max(0.0, 1 - (dx / rx) ** 2 - (dz / rz) ** 2))
            verts.append(centre + Vector((dx, -depth - lift, dz)))
        for i in range(8):
            f = _orient_out(verts, [(c, c + 1 + i, c + 1 + (i + 1) % 8)], centre)[0]
            faces.append(f)
            mats.append(mat)

    r_iris = 0.56 * min(rx, rz)
    cone(r_iris, 0.0004, 1)
    cone(0.5 * r_iris, 0.0009, 2)
    return verts, faces, mats


def ribbon(ob, columns, lift=0.002):
    """A strip pressed onto the surface: columns are (x, z_low, z_high); each vertex is snapped by a ray and lifted off the facet a little.
    Returns verts and faces, or None if the strip misses the head."""
    front = Vector((0, -1, 0))
    verts, cols = [], []
    for x, z0, z1 in columns:
        pair = []
        for z in (z0, z1):
            r = ray_front(ob, x, z)
            if not r:
                break
            loc, nor = r
            n = (nor.normalized() + front).normalized()
            pair.append(len(verts))
            verts.append(loc + n * lift)
        else:
            cols.append(pair)
    if len(cols) < 2:
        return None
    faces = []
    for a, b in zip(cols, cols[1:]):
        f = (a[0], b[0], b[1], a[1])
        n = (verts[f[1]] - verts[f[0]]).cross(verts[f[2]] - verts[f[0]])
        faces.append(f if n.dot(front) > 0 else tuple(reversed(f)))
    return verts, faces


def face_parts(ob, lm, kind, k):
    """Eyeballs (a separate object), brows, mouth and lips (part of the body) on the decimated head. k: head size factor."""
    eye_z = (lm['eye_L'].z + lm['eye_R'].z) / 2
    ex = abs(lm['eye_L'].x)
    zs = [v.co.z for v in ob.data.vertices if v.co.z > lm['head'].z - 0.02]
    chin_z = min(zs)
    mouth_z = eye_z - 0.68 * (eye_z - chin_z)

    ev, ef, em = [], [], []
    for sx in (-1, 1):
        ys = []
        for dx, dz in ((0, 0), (0.012, 0), (-0.012, 0), (0, 0.008), (0, -0.008)):
            r = ray_front(ob, sx * ex + dx * k, eye_z + dz * k)
            if r:
                ys.append(r[0].y)
        if not ys:
            continue
        ys.sort()
        y_surface = ys[len(ys) // 2]
        rx, ry, rz = 0.0190 * k, 0.0140 * k, 0.0155 * k
        centre = Vector((sx * ex, y_surface - 0.0045 + ry, eye_z))
        v, f, m = eyeball(centre, rx, ry, rz)
        base = len(ev)
        ev += v
        ef += [tuple(i + base for i in fc) for fc in f]
        em += m
    eye_ob = _make_object(f'LP_{kind}_eyes', ev, ef, em, ['EyeWhite', 'Iris', 'Pupil'])

    fv, ff, fm = [], [], []

    def add(strip, mat):
        if strip:
            v, f = strip
            base = len(fv)
            fv.extend(v)
            ff.extend(tuple(i + base for i in fc) for fc in f)
            fm.extend([mat] * len(f))

    for sx in (-1, 1):  # brows: an arch, thicker toward the nose
        cols = []
        for i in range(7):
            t = i / 6
            x = sx * ex * (0.42 + 1.1 * t)
            zc = eye_z + (0.030 + 0.007 * math.sin(math.pi * min(1.0, t * 1.15))) * k - 0.004 * t * k
            h = (0.0095 - 0.0045 * t) * k
            cols.append((x, zc - h / 2, zc + h / 2))
        add(ribbon(ob, cols, 0.0025), 0)
    cols = []  # mouth line, a little smile
    for i in range(9):
        t = i / 8 * 2 - 1
        x = 0.030 * k * t
        zc = mouth_z + 0.0035 * k * t * t
        h = 0.0052 * k * (1 - 0.45 * t * t)
        cols.append((x, zc - h / 2, zc + h / 2))
    add(ribbon(ob, cols, 0.0022), 1)
    cols = []  # lower lip
    for i in range(7):
        t = i / 6 * 2 - 1
        x = 0.020 * k * t
        zc = mouth_z - 0.0085 * k + 0.0020 * k * t * t
        h = 0.0080 * k * (1 - 0.55 * t * t)
        cols.append((x, zc - h / 2, zc + h / 2))
    add(ribbon(ob, cols, 0.0020), 2)
    face_ob = _make_object(f'LP_{kind}_face', fv, ff, fm, ['Hair', 'Mouth', 'Lip']) if fv else None
    return eye_ob, face_ob


def _make_object(name, verts, faces, mat_idx, mat_names):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], faces)
    for n in mat_names:
        me.materials.append(bpy.data.materials[n])
    me.polygons.foreach_set('material_index', mat_idx)
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    old.flat_shade(ob)
    return ob


def orient_outward(ob, ref):
    """Every face looks the way the smooth original does (ref, the dense body, has correct outward normals). recalc_face_normals gets
    patches of this open, non-convex mesh backwards, and the clothes, which are pushed along the normals, then end up inside the body."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.normal_update()
    flip = []
    for f in bm.faces:
        ok, loc, nor, _ = ref.closest_point_on_mesh(f.calc_center_median())
        if ok and f.normal.dot(nor) < 0:
            flip.append(f)
    bmesh.ops.reverse_faces(bm, faces=flip)
    bm.to_mesh(ob.data)
    bm.free()

# ---- the whole body -----------------------------------------------------------------------------------------------

def finish(kind):
    ensure_materials()
    spec = mpfb_body.BODIES[kind]
    human, lm = mpfb_body.build(kind)
    human.name = f'LP_{kind}'
    human.data.name = f'LP_{kind}'

    # the dense body, hidden, for cutting clothes
    dense = human.copy()
    dense.data = human.data.copy()
    dense.name = f'LP_{kind}_dense'
    bpy.context.scene.collection.objects.link(dense)
    dense.hide_set(True)
    dense.hide_render = True

    # What the decimation must keep: the head, hands and feet. In Blender's collapse decimate a LOW vertex weight protects the vertex
    # (weight 0 is never collapsed), so they get small weights and the rest of the body 1.0.
    head_z = lm['head'].z
    hands = hand_set(human, lm)
    g = human.vertex_groups.new(name='Keep')
    for v in human.data.vertices:
        w = BODY_W
        if v.co.z > head_z - 0.03:
            w = HEAD_W
        elif v.index in hands:
            w = HAND_W
        else:
            for s in ('L', 'R'):
                a, t = lm[f'ankle_{s}'], lm[f'toe_{s}']
                if v.co.z < a.z + 0.04 and (v.co.xy - a.xy).length < 0.25:
                    # heel and instep keep detail; the toes collapse into one clean wedge instead of ragged claws
                    w = BODY_W if v.co.y < (a.y + t.y) / 2 - 0.02 else FOOT_W
        g.add([v.index], w, 'REPLACE')
    human.data.materials.clear()
    human.data.materials.append(bpy.data.materials['Skin'])
    human.data.materials.append(bpy.data.materials['Face'])
    classify(human, lm)
    old.decimate_to(human, spec['target'], 'Keep', 1.0)
    human.vertex_groups.remove(human.vertex_groups['Keep'])
    classify(human, lm)

    # triangles, outward normals (done before the open face strips are joined)
    bm = bmesh.new()
    bm.from_mesh(human.data)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(human.data)
    bm.free()
    orient_outward(human, dense)
    old.flat_shade(human)

    # eyes, brows, mouth
    k = spec['tall'] / 1.77 if kind != 'kid' else 0.9
    eye_ob, face_ob = face_parts(human, lm, kind, max(0.85, k) * (1.15 if kind == 'kid' else 1.0))
    if face_ob:
        n0 = len(human.data.vertices)
        with bpy.context.temp_override(object=human, active_object=human, selected_objects=[human, face_ob], selected_editable_objects=[human, face_ob]):
            bpy.ops.object.join()
        human.vertex_groups['Head'].add(list(range(n0, len(human.data.vertices))), 1.0, 'REPLACE')
    old.flat_shade(human)

    rig = mpfb_body.make_rig(kind, lm)
    human.parent = rig
    human.matrix_parent_inverse = rig.matrix_world.inverted()
    mod = human.modifiers.new('Armature', 'ARMATURE')
    mod.object = rig
    # the eyes ride on the head bone
    eye_ob.vertex_groups.new(name='Head').add(list(range(len(eye_ob.data.vertices))), 1.0, 'REPLACE')
    eye_ob.parent = rig
    eye_ob.matrix_parent_inverse = rig.matrix_world.inverted()
    emod = eye_ob.modifiers.new('Armature', 'ARMATURE')
    emod.object = rig
    human['dense'] = dense.name
    human['eyes'] = eye_ob.name
    return human, rig, eye_ob
