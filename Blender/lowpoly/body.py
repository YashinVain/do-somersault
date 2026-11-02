"""Low-poly human bodies with flat triangular facets, like the references.

How a body is made: a dense smooth body is lofted from cross-sections (torso, neck, head, arms, legs, feet), melted into one
manifold surface with a voxel remesh, smoothed, then collapsed down to about a thousand triangles. The collapse leaves the
irregular facets of the references, and the head is kept finer than the rest. Hands and the face are built apart from low-poly
parts and snapped onto the surface. Every body gets the same bone names as the game's skeleton, at its own proportions.

Run in Blender (live or headless):  import body; body.build('swimmer')
Bodies: swimmer (adult man), old (the same with broader shoulders and a stoop), kid, fat, woman.
All numbers are Unity metres (x right, y up, z forward, hips at the origin); lib.to_blender maps them into Blender's axes.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
sys.path.insert(0, ROOT)
import lib  # noqa: E402
from lib import tube, ellipsoid, box, Shape, to_blender, densify, material  # noqa: E402
import nature  # noqa: E402

SKILLS = os.path.join(os.path.expanduser('~'), '.claude', 'skills')
sys.path.insert(0, os.path.join(SKILLS, 'blender-character-rigging', 'scripts'))

SKIN, FACE, EYE, PUPIL, MOUTH, HAIR = '#EFBC98', '#EFBC98', '#F7F5F0', '#1A1D26', '#7A3B35', '#3A2A20'

# Proportions of every body. ts: torso height, sw: shoulder width, tz: depth, lk: leg length, lr: leg thickness, ak: arm length,
# ar: arm thickness, head: head size, belly, muscle, hip: pelvis width, hw: how far apart the legs stand, foot: foot size.
PARAMS = {
    'swimmer': dict(ts=1.0, sw=1.0, tz=1.0, lk=1.0, lr=1.0, ak=1.0, ar=1.0, head=1.04, belly=0.0, muscle=1.0, hip=1.0, hw=1.0, foot=1.0, target=900, stoop=0.0),
    'old': dict(ts=0.98, sw=1.08, tz=1.04, lk=0.99, lr=0.96, ak=1.0, ar=0.95, head=1.04, belly=0.15, muscle=0.25, hip=1.02, hw=1.0, foot=1.0, target=900, stoop=0.5),
    'kid': dict(ts=0.9, sw=0.78, tz=0.86, lk=0.76, lr=0.82, ak=0.77, ar=0.8, head=1.24, belly=0.1, muscle=0.0, hip=0.92, hw=0.88, foot=0.78, target=720, stoop=0.0),
    'fat': dict(ts=0.98, sw=1.4, tz=1.55, lk=0.95, lr=1.55, ak=0.97, ar=1.55, head=1.13, belly=1.0, muscle=0.0, hip=1.4, hw=1.45, foot=1.1, target=1000, stoop=0.0),
    'woman': dict(ts=0.97, sw=0.86, tz=0.92, lk=0.96, lr=0.9, ak=0.93, ar=0.8, head=0.98, belly=0.0, muscle=0.3, hip=1.2, hw=1.08, foot=0.88, target=900, stoop=0.0),
}


HEAD_R = (0.092, 0.13, 0.108)   # half width, half height, half depth of the head before the body's own head scale
HEAD_LIFT = 0.025


def gauss(x):
    return math.exp(-x * x)


def xf(shape, pivot, new, s):
    """v' = new + (v - pivot) * s, per axis."""
    shape.verts = [(new[0] + (x - pivot[0]) * s[0], new[1] + (y - pivot[1]) * s[1], new[2] + (z - pivot[2]) * s[2]) for x, y, z in shape.verts]
    return shape


def skeleton(p):
    """Joint positions of this body (Unity metres)."""
    ts, sw, ak, lk, hw = p['ts'], p['sw'], p['ak'], p['lk'], p['hw']
    shoulder = 0.1 + 0.4 * ts
    ax = 0.235 * sw
    lx = 0.10 * hw
    return dict(
        spine=(0, 0.10, 0), head=(0, 0.1 + 0.63 * ts, 0.01 + 0.03 * p['stoop']),
        arm=(ax, shoulder, 0), fore=(ax, shoulder - 0.27 * ak, 0), wrist=(ax, shoulder - 0.51 * ak, 0), hand_end=(ax, shoulder - 0.51 * ak - 0.17 * ak * 1.1, 0),
        thigh=(lx, 0.02, 0), shin=(lx, 0.02 - 0.44 * lk, 0), ankle=(lx, 0.02 - 0.865 * lk, 0), toe=(lx, 0.02 - 0.865 * lk, 0.16 * p['foot']),
        sole=0.02 - 0.895 * lk,
    )


def bones_for(p):
    j = skeleton(p)
    B = lib.Bone
    bones = [B('Hips', (0, 0, 0), (0, 0.1, 0)), B('Spine', j['spine'], j['head'], 'Hips'),
             B('Head', j['head'], (0, j['head'][1] + 0.16 * p['head'], j['head'][2]), 'Spine')]
    for side, sx in (('L', -1), ('R', 1)):
        def at(key):
            x, y, z = j[key]
            return (sx * x, y, z)
        bones += [B(f'Arm_{side}', at('arm'), at('fore'), 'Spine'), B(f'ForeArm_{side}', at('fore'), at('wrist'), f'Arm_{side}'),
                  B(f'Thigh_{side}', at('thigh'), at('shin'), 'Hips'), B(f'Shin_{side}', at('shin'), at('ankle'), f'Thigh_{side}'),
                  B(f'Ankle_{side}', at('ankle'), at('toe'), f'Shin_{side}')]
    return bones


# ------------------------------------------------------------------ the dense source body

# The head as a stack of cross-sections: (height above the head joint, half width, how far the face reaches forward, how far
# the skull reaches back). The face is flatter and the skull rounder; the jaw narrows to a chin.
HEAD_RINGS = [(-0.085, .016, .058, .020), (-0.076, .038, .078, .038), (-0.058, .060, .090, .060), (-0.038, .073, .098, .083),
              (-0.014, .078, .103, .094), (0.010, .080, .106, .100), (0.034, .081, .104, .103), (0.066, .079, .094, .106),
              (0.100, .073, .080, .104), (0.130, .062, .058, .094), (0.152, .042, .034, .074), (0.165, .016, .010, .036)]


def head_shapes(p, j):
    hj = j['head'][1]
    k = p['head']
    kx = k * (1 + 0.2 * (p['sw'] - 1))
    rows = [(hj + dy, w, (df + db) / 2, 0, (df - db) / 2) for dy, w, df, db in HEAD_RINGS]
    h = tube(densify(rows, 0.025), 'y', 22, dome0=2, dome1=2, depth0=0.01, depth1=0.014, mat='Skin', kind='x', power=2.5)
    pivot = (0, hj, 0)
    new = (0, hj, j['head'][2] - 0.01)
    S = [xf(h, pivot, new, (kx, k, k))]
    feats = [((0, 0.062, 0.100), (0.066, 0.011, 0.016)),                       # brow ridge
             ((0.060, 0.008, 0.078), (0.021, 0.019, 0.026)),                   # cheekbones
             ((-0.060, 0.008, 0.078), (0.021, 0.019, 0.026)),
             ((0, -0.075, 0.082), (0.026, 0.016, 0.021)),                      # chin
             ((0, 0.024, 0.106), (0.014, 0.034, 0.02))]                        # nose root, merges with the face
    for c, r in feats:
        e = ellipsoid((0, 0, 0), r, 'Skin', 'x', 10, 6)
        S.append(xf(e, (0, 0, 0), (c[0] * kx, hj + c[1] * k, new[2] + c[2] * k), (kx, k, k)))
    return S


def source_shapes(p):
    j = skeleton(p)
    ts, sw, tz, lk, lr, ak, ar = p['ts'], p['sw'], p['tz'], p['lk'], p['lr'], p['ak'], p['ar']
    belly, hip, muscle, hw = p['belly'], p['hip'], p['muscle'], p['hw']
    shapes = []

    # Torso: a pelvis, a waist that tucks in, a chest that opens out and shoulders that slope down from the neck.
    rows = []
    for (y, rx, rz, ox, oz) in [(-0.04, .135, .095, 0, -0.005), (0.02, .165, .108, 0, -0.008), (0.10, .155, .104, 0, -0.002),
                                (0.20, .138, .093, 0, 0.005), (0.30, .150, .098, 0, 0.012), (0.40, .170, .112, 0, 0.018),
                                (0.47, .178, .108, 0, 0.012), (0.52, .190, .098, 0, 0.0), (0.555, .145, .085, 0, -0.004), (0.585, .075, .068, 0, -0.004)]:
        k_hip = 1 + (hip - 1) * gauss((y - 0.02) / 0.14)
        k_belly = 1 + 0.55 * belly * gauss((y - 0.17) / 0.17)
        rows.append((y, rx * k_hip * (1 + 0.1 * belly * gauss((y - 0.17) / 0.17)), rz * k_belly, ox, oz + 0.07 * belly * gauss((y - 0.17) / 0.14)))
    t = tube(densify(rows, 0.05), 'y', 24, dome0=2, dome1=2, depth0=0.04, depth1=0.03, mat='Skin', kind='x', power=2.3)
    shapes.append(xf(t, (0, 0, 0), (0, 0, 0), (sw, ts, tz)))

    # Neck, a little forward of the spine.
    n = tube([(0.56, .062, .066, 0, 0.004), (0.62, .053, .058, 0, 0.01), (0.69, .05, .055, 0, 0.012)], 'y', 12, mat='Skin', kind='x', power=2.0)
    shapes.append(xf(n, (0, 0, 0), (0, 0, 0.01 * p['stoop']), (1 + 0.5 * (sw - 1), ts, 1 + 0.4 * (tz - 1))))

    shapes += head_shapes(p, j)

    for sx in (-1, 1):
        ax0 = 0.235
        arm_rows = densify([(0.512, .036, .042), (0.48, .059, .058), (0.42, .054, .056), (0.33, .047, .05), (0.25, .043, .045),
                            (0.15, .048, .046), (0.05, .037, .036), (-0.01, .031, .029)], 0.045)
        a = tube(arm_rows, 'y', 16, dome0=2, dome1=0, depth0=0.03, depth1=0.0, mat='Skin', kind='x', power=2.2)
        a.verts = [(x + sx * ax0, y, z) for x, y, z in a.verts]
        shapes.append(xf(a, (sx * ax0, 0.5, 0), (sx * j['arm'][0], j['arm'][1], 0), (ar, ak, ar)))

        leg_rows = densify([(0.04, .092, .098), (-0.05, .092, .098), (-0.12, .087, .093), (-0.22, .075, .082), (-0.32, .062, .07),
                            (-0.39, .055, .062), (-0.43, .053, .058), (-0.49, .054, .064, 0, -0.012), (-0.57, .056, .07, 0, -0.02),
                            (-0.65, .05, .062, 0, -0.015), (-0.74, .04, .047, 0, -0.008), (-0.81, .034, .04), (-0.85, .034, .038)], 0.045)
        lg = tube(leg_rows, 'y', 16, dome0=2, dome1=1, depth0=0.05, depth1=0.02, mat='Skin', kind='x', power=2.2)
        lg.verts = [(x + sx * 0.10, y, z) for x, y, z in lg.verts]
        shapes.append(xf(lg, (sx * 0.10, 0.02, 0), (sx * j['thigh'][0], 0.02, 0), (lr, lk, lr)))
        kc = ellipsoid((0, 0, 0), (0.034, 0.036, 0.022), 'Skin', 'x', 10, 6)                                  # kneecap
        shapes.append(xf(kc, (0, 0, 0), (sx * j['thigh'][0], 0.02 - 0.45 * lk, 0.052 * lr), (lr, 1, lr)))

        ft = tube([(-0.075, .038, .031), (-0.03, .042, .034), (0.05, .046, .028), (0.12, .049, .023), (0.16, .04, .017)], 'z', 12,
                  dome0=2, dome1=2, depth0=0.03, depth1=0.022, mat='Skin', kind='x', power=2.4)
        ft.verts = [(x + sx * 0.10, y - 0.855, z) for x, y, z in ft.verts]
        shapes.append(xf(ft, (sx * 0.10, -0.855, 0), (sx * j['thigh'][0], j['ankle'][1] - 0.01, 0), (p['foot'] * lr ** 0.4, p['foot'], p['foot'])))

    # The chest and back of an athlete: pectorals and lats as soft planes, a little abdomen.
    if muscle > 0.2:
        for sx in (-1, 1):
            for (c, r, rot) in [((sx * 0.085, 0.405, 0.092), (0.082, 0.052, 0.03), (-8, 0, sx * 6)),
                                ((sx * 0.125, 0.30, -0.07), (0.058, 0.13, 0.04), (0, 0, sx * 8)),
                                ((sx * 0.036, 0.27, 0.098), (0.034, 0.1, 0.014), (0, 0, 0))]:
                e = ellipsoid((0, 0, 0), tuple(v * (0.6 + 0.4 * muscle) for v in r), 'Skin', 'x', 12, 7, rot=rot)
                shapes.append(xf(e, (0, 0, 0), (c[0] * sw, 0.1 + (c[1] - 0.1) * ts, c[2] * tz), (1, 1, 1)))
    return shapes


# ------------------------------------------------------------------ hands

def rbox(center, size, rot=(0, 0, 0), pivot=None):
    s = box(center, size, 0.0, 1, 'Face', 'x')
    if any(rot):
        s.rotated(rot, pivot if pivot is not None else center)
    return s


def hand_shapes(p, sx):
    """A flat-faced low-poly hand hanging at the side: palm, four fingers in two segments, a thumb to the front."""
    j = skeleton(p)
    k = p['ak'] * 1.1
    wx, wy = sx * j['wrist'][0], j['wrist'][1] + 0.012
    th = 0.5 * (0.8 + 0.2 * p['ar'])
    palm = tube([(wy + 0.02, 0.02 * k * th, 0.03 * k), (wy - 0.045 * k, 0.0165 * k * th, 0.038 * k), (wy - 0.098 * k, 0.0145 * k * th, 0.043 * k)], 'y', 8,
                mat='Face', kind='x', power=3.0)
    palm.verts = [(x + wx, y, z) for x, y, z in palm.verts]
    S = [palm]
    for z, ln in [(-0.03, 0.8), (-0.011, 0.95), (0.008, 0.9), (0.027, 0.72)]:
        seg1, seg2 = 0.045 * ln * k, 0.036 * ln * k
        top = wy - 0.095 * k
        S.append(rbox((wx, top - seg1 / 2, z * k), (0.026 * k, seg1, 0.021 * k)))
        knuckle = (wx, top - seg1, z * k)
        S.append(rbox((wx, top - seg1 - seg2 / 2, z * k), (0.024 * k, seg2, 0.02 * k), (0, 0, -sx * 24), knuckle))   # curled towards the thigh
    S.append(rbox((wx - sx * 0.004, wy - 0.075 * k, 0.05 * k), (0.026 * k, 0.042 * k, 0.025 * k), (-18, 0, 0)))
    S.append(rbox((wx - sx * 0.004, wy - 0.108 * k, 0.064 * k), (0.024 * k, 0.036 * k, 0.023 * k), (-34, 0, 0)))
    return S


# ------------------------------------------------------------------ building

def mesh_from_shapes(name, shapes):
    return lib.make_object(name, shapes, None, None)


def ctx(ob):
    return bpy.context.temp_override(object=ob, active_object=ob, selected_objects=[ob], selected_editable_objects=[ob])


def apply_modifier(ob, mod):
    with ctx(ob):
        bpy.ops.object.modifier_apply(modifier=mod.name)


def polys(ob):
    return len(ob.data.polygons)


def tri_count(ob):
    return sum(max(1, len(p.vertices) - 2) for p in ob.data.polygons)


def decimate_to(ob, target, keep_group=None, keep=1.0):
    for _ in range(3):
        n = tri_count(ob)
        if n <= target * 1.05:
            break
        mod = ob.modifiers.new('Dec', 'DECIMATE')
        mod.decimate_type = 'COLLAPSE'
        mod.ratio = max(0.01, min(1.0, target / n))
        mod.use_collapse_triangulate = True
        if keep_group is not None and not ob.vertex_groups.get(keep_group) is None:
            mod.vertex_group = keep_group
            mod.vertex_group_factor = keep
        apply_modifier(ob, mod)


def flat_shade(ob):
    n = len(ob.data.polygons)
    ob.data.polygons.foreach_set('use_smooth', [False] * n)
    ob.data.update()


def slot_index(ob, mat):
    for i, s in enumerate(ob.material_slots):
        if s.material == mat:
            return i
    ob.data.materials.append(mat)
    return len(ob.material_slots) - 1


def surface(ob, x, y, from_front=True):
    """Where a ray along -z (Unity) hits the body at (x, y). Returns (point, normal) in Blender coordinates or None."""
    o = to_blender((x, y, 0.8))
    d = to_blender((0, 0, -1)) - to_blender((0, 0, 0))
    hit, loc, nor, _ = ob.ray_cast(Vector(o), Vector(d), distance=2.0)
    return (loc, nor) if hit else None


def surface_side(ob, x, y, z, side):
    o = to_blender((side * 0.5, y, z))
    d = to_blender((-side, 0, 0)) - to_blender((0, 0, 0))
    hit, loc, nor, _ = ob.ray_cast(Vector(o), Vector(d), distance=2.0)
    return (loc, nor) if hit else None


def slab(center, right, up, nor, w, h, d, mat):
    """A thin box in the surface's own frame: w along right, h along up, d along the normal. Returns a Shape in Unity coords."""
    pts = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (0, 1):
                pts.append(center + right * (sx * w / 2) + up * (sy * h / 2) + nor * (sz * d))
    # indexes: (sx,sy,sz) -> 4*(sx>0) + 2*(sy>0) + sz
    faces = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    verts = [tuple(lib.to_unity(v)) for v in pts]
    return nature.orient_closed(Shape(verts, faces, mat, 'x', flat=True))


def frame(nor):
    nor = nor.normalized()
    up = Vector((0, 0, 1))
    right = up.cross(nor).normalized()
    up2 = nor.cross(right).normalized()
    return right, up2, nor


def face_parts(body, p):
    """Eyes, brows, nose, mouth and ears, snapped to the head's actual surface. Offsets are metres from the head joint, scaled with the head."""
    j = skeleton(p)
    k = p['head']
    kx = k * (1 + 0.2 * (p['sw'] - 1))
    hj = j['head'][1]
    S = []

    def at(x, dy):
        return surface(body, x * kx, hj + dy * k)

    for sx in (-1, 1):
        r = at(sx * 0.036, 0.040)
        if r:
            loc, nor = r
            right, up, n = frame(nor)
            S.append(slab(loc + n * 0.001, right, up, n, 0.026 * k, 0.0105 * k, 0.004, 'EyeWhite'))
            S.append(slab(loc + n * 0.003, right, up, n, 0.0092 * k, 0.0092 * k, 0.003, 'Pupil'))
        r = at(sx * 0.038, 0.066)
        if r:
            loc, nor = r
            right, up, n = frame(nor)
            S.append(slab(loc + n * 0.001 + up * (0.003 * -sx), right, up, n, 0.034 * k, 0.0055 * k, 0.005, 'Hair'))
    r = at(0, -0.050)
    if r:
        loc, nor = r
        right, up, n = frame(nor)
        S.append(slab(loc + n * 0.0005, right, up, n, 0.036 * k, 0.0055 * k, 0.004, 'Mouth'))
    # nose: a four-sided pyramid with the tip forward
    top, base = at(0, 0.052), at(0, -0.012)
    left, rightp = at(-0.018, -0.008), at(0.018, -0.008)
    if top and base and left and rightp:
        T = top[0] + top[1] * 0.004
        L = left[0] + left[1] * 0.005
        R = rightp[0] + rightp[1] * 0.005
        B = base[0] + base[1] * 0.004
        fwd = Vector(to_blender((0, 0, 1))) - Vector(to_blender((0, 0, 0)))
        P = (T + B) * 0.5 + fwd * 0.024 * k - Vector((0, 0, 0.003))
        pts = [T, L, R, B, P]
        centre = Vector(to_blender((0, hj, j['head'][2])))
        faces = []
        for f in [(0, 1, 4), (0, 4, 2), (1, 3, 4), (4, 3, 2)]:
            a, b2, c = pts[f[0]], pts[f[1]], pts[f[2]]
            nrm = (b2 - a).cross(c - a)
            faces.append(f if nrm.dot((a + b2 + c) / 3 - centre) > 0 else (f[2], f[1], f[0]))
        verts = [tuple(lib.to_unity(v)) for v in pts]
        S.append(Shape(verts, faces, 'Face', 'x', flat=True))
    for sx in (-1, 1):
        r = surface_side(body, 0, hj, j['head'][2] - 0.01, sx)
        if r:
            loc, nor = r
            right, up, n = frame(nor)
            S.append(slab(loc - n * 0.004, right, up, n, 0.026 * k, 0.05 * k, 0.012, 'Face'))
    return S


def build(kind='swimmer', offset=(0.0, 0.0, 0.0), keep_source=False):
    p = PARAMS[kind]
    sc = bpy.context.scene
    for mname, col in (('Skin', SKIN), ('Face', FACE), ('EyeWhite', EYE), ('Pupil', PUPIL), ('Mouth', MOUTH), ('Hair', HAIR)):
        material(mname, col, 0.8)

    # dense source -> one surface
    shapes = source_shapes(p)
    src = lib.make_object(f'LP_{kind}_src', shapes, None, None)
    src.data.use_auto_texspace = True
    src.data.remesh_voxel_size = 0.0085
    with ctx(src):
        bpy.ops.object.voxel_remesh()
    sm = src.modifiers.new('Smooth', 'SMOOTH')
    sm.factor = 0.55
    sm.iterations = 10
    apply_modifier(src, sm)

    # the dense body stays (hidden): clothes are cut from it
    dense = src.copy()
    dense.data = src.data.copy()
    dense.name = f'LP_{kind}_dense'
    bpy.context.scene.collection.objects.link(dense)
    dense.hide_set(True)
    dense.hide_render = True

    # the head is kept finer than the limbs
    j = skeleton(p)
    neck_y = j['head'][1] - 0.05 * p['head']
    g = src.vertex_groups.new(name='Keep')
    idx = [v.index for v in src.data.vertices if lib.to_unity(v.co)[1] > neck_y]
    low = [v.index for v in src.data.vertices if lib.to_unity(v.co)[1] <= neck_y]
    g.add(idx, 1.0, 'REPLACE')
    g.add(low, 0.35, 'REPLACE')
    decimate_to(src, p['target'], 'Keep', 1.0)
    src.vertex_groups.clear()
    src.name = f'LP_{kind}'
    src.data.name = f'LP_{kind}'
    src.data.materials.clear()
    src.data.materials.append(bpy.data.materials['Skin'])
    src.data.materials.append(bpy.data.materials['Face'])
    flat_shade(src)

    # head and neck faces take the Face material
    me = src.data
    mi = [0] * len(me.polygons)
    for pl in me.polygons:
        yy = lib.to_unity(pl.center)[1]
        if yy > neck_y + 0.0:
            mi[pl.index] = 1
    me.polygons.foreach_set('material_index', mi)

    # face and hands as separate low-poly parts, then joined
    parts = face_parts(src, p)
    eyes = [s for s in parts if s.mat in ('EyeWhite', 'Pupil')]
    extra = [s for s in parts if s.mat not in ('EyeWhite', 'Pupil')] + hand_shapes(p, -1) + hand_shapes(p, 1)
    extras = lib.make_object(f'LP_{kind}_extra', extra, None, None)
    flat_shade(extras)
    with bpy.context.temp_override(object=src, active_object=src, selected_objects=[src, extras], selected_editable_objects=[src, extras]):
        bpy.ops.object.join()
    # triangulate everything for a count that means something
    bm = bmesh.new()
    bm.from_mesh(src.data)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(src.data)
    bm.free()
    flat_shade(src)
    src.location = Vector(offset)
    eye_ob = lib.make_object(f'LP_{kind}_eyes', eyes, None, None)
    flat_shade(eye_ob)
    src['dense'] = dense.name
    src['eyes'] = eye_ob.name
    return src


def rig(body, kind):
    """Build the skeleton at this body's proportions, bind with automatic weights, then tidy the weights."""
    p = PARAMS[kind]
    arm = lib.build_armature(f'LP_{kind}_rig', bones_for(p))
    arm.location = body.location
    import importlib
    import bind_mesh
    importlib.reload(bind_mesh)
    res = bind_mesh.run(armature=arm.name, meshes=[body.name], method='auto')
    return arm, res
