"""Clothes and gear on the MPFB bodies, in the same flat-faced low-poly style.

Clothes are cut out of the dense body (a shell that follows the skin, pushed out by a little and rumpled with low noise, so the
folds come out as big triangular facets), then collapsed to a few hundred triangles. Gear (goggles, packs, hats ...) is made from
simple low-sided shapes. A garment keeps the body's bone weights (it is cut from the weighted dense body); gear is weighted to
one bone.

Everything is measured from the body itself (ctx(kind)), so the same code fits the swimmer, the old man, the kid, the fat man
and the woman. Formulas work in Unity axes (x right, y up, z forward) and metres, like the game.
"""
import json
import math
import os
import sys
from types import SimpleNamespace

import bmesh
import bpy
from mathutils import Vector, noise

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
sys.path.insert(0, ROOT)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib  # noqa: E402
import body  # noqa: E402
from lib import tube, ellipsoid, box, sweep, material  # noqa: E402

U = lib.to_unity

COLOURS = {
    'Outfit': '#283B8F', 'Shirt': '#F4F1EA', 'Cap': '#FFC743', 'Strap': '#1E2A4A', 'Lens': '#7FD3F0', 'Accent': '#C8553D',
    'Metal': '#BFC6CE', 'Hair': '#3A2A20', 'ShinR': '#EFBC98',
}


def materials():
    for name, col in COLOURS.items():
        material(name, col, 0.7)
    material('Metal', '#BFC6CE', 0.3, 0.8)
    material('Lens', '#7FD3F0', 0.15)


# ------------------------------------------------------------------ measuring a body

def ctx(kind):
    """Sizes and joints of one body, in Unity axes. k: head size (1 = a 0.235 m head), kx: head width, hj: eye height - 0.04k, hz: head
    centre depth. ts, tz, sw: torso height / depth / shoulder width relative to a standard torso (what the gear formulas were made for)."""
    bd = bpy.data.objects[f'LP_{kind}']
    dense = bpy.data.objects[f'LP_{kind}_dense']
    lu = {key: tuple(v) for key, v in json.loads(bd['lm']).items()}
    pts = [v.co for v in dense.data.vertices]
    top = max(p.z for p in pts)
    eye_y = (lu['eye_L'][1] + lu['eye_R'][1]) / 2
    k = 2.2 * (top - eye_y) / 0.235
    head = [p for p in pts if p.z > eye_y - 0.03]
    halfw = max(abs(p.x) for p in head if abs(p.z - eye_y) < 0.03)
    cy = (min(p.y for p in head) + max(p.y for p in head)) / 2
    sh = lu['arm_L'][1]
    chest = [p for p in pts if abs(p.z - (sh - 0.1)) < 0.04 and abs(p.x) < abs(lu['arm_L'][0]) - 0.03]
    depth = max(p.y for p in chest) - min(p.y for p in chest)
    ey = [p for p in pts if abs(p.z - eye_y) < 0.02]
    eye_cy = (min(p.y for p in ey) + max(p.y for p in ey)) / 2
    eye_hd = (max(p.y for p in ey) - min(p.y for p in ey)) / 2
    c = SimpleNamespace(kind=kind, body=bd, dense=dense, lu=lu, top=top, k=k, kx=halfw / 0.083, hj=eye_y - 0.04 * k, hz=-cy,
                        ts=(sh + 0.04 - 0.1) / 0.5, tz=depth / 0.22, sw=abs(lu['arm_L'][0]) / 0.19, eye_y=eye_y, shoulder=sh,
                        hzE=-eye_cy, rxE=halfw + 0.006, rzE=eye_hd + 0.006,  # the head's cross-section at eye height (for bands)
                        back_z=-max(p.y for p in chest), front_z=-min(p.y for p in chest))
    return c


def ray_front(c, x, z):
    """Where a ray along +y (Blender) from in front of the face hits the game body (the faceted one, which sticks out further than the dense
    skin) at (x, z)."""
    hit, loc, nor, _ = c.body.ray_cast(Vector((x, -1.5, z)), Vector((0, 1, 0)), distance=3.0)
    return (loc, nor) if hit else None


def head_surface(c, x, dy):
    return ray_front(c, x * c.kx, c.hj + dy * c.k)


# ------------------------------------------------------------------ cutting clothes out of the dense body

def shell(c, name, region, thick, amp=0.006, freq=14.0, target=300, mat='Outfit', seed=1, cuts=(), hem=None, split=None, flare=None):
    """A garment: the region of the smooth dense body is cut out, wrapped onto the faceted game body with a Shrinkwrap that keeps every
    point `thick` metres outside the real surface (so the skin can never come through), rumpled with a little noise, then collapsed to
    `target` triangles.  region(u, w) -> bool for a face: u its centre in Unity coords, w its bone weights {bone: weight}.
    cuts: heights (or (point, normal) planes) where the dense mesh is sliced first so the edge of the garment is clean.
    flare(u) -> extra metres pushed out afterwards (an untucked shirt widens toward its hem)."""
    src = c.dense
    names = {g.index: g.name for g in src.vertex_groups}
    bm = bmesh.new()
    bm.from_mesh(src.data)
    for cut in cuts:
        co, no = ((0, cut, 0), (0, 1, 0)) if not isinstance(cut, tuple) else cut
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=Vector(lib.to_blender(co)), plane_no=Vector(lib.to_blender(no)))
    dl = bm.verts.layers.deform.active

    def wts(f):
        acc = {}
        for v in f.verts:
            for gi, wt in v[dl].items():
                acc[names[gi]] = acc.get(names[gi], 0.0) + wt / len(f.verts)
        return acc

    kill = [f for f in bm.faces if not region(U(f.calc_center_median()), wts(f))]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    for g in src.vertex_groups:  # same names in the same order: the weights copied with the vertices stay valid
        ob.vertex_groups.new(name=g.name)
    me.materials.append(bpy.data.materials[mat])

    wrap = ob.modifiers.new('Wrap', 'SHRINKWRAP')
    wrap.target = c.body
    wrap.wrap_method = 'TARGET_PROJECT'
    wrap.use_negative_direction = True
    wrap.use_positive_direction = True
    wrap.wrap_mode = 'OUTSIDE_SURFACE'
    wrap.offset = thick
    body.apply_modifier(ob, wrap)

    bm = bmesh.new()
    bm.from_mesh(me)
    bm.normal_update()
    for v in bm.verts:
        u = U(v.co)
        n = noise.noise(Vector((u[0] * freq + seed * 7.1, u[1] * freq + seed * 1.3, u[2] * freq + seed * 3.7)))
        v.co += v.normal * (n * amp + (flare(u) if flare else 0.0))
    bm.to_mesh(me)
    bm.free()
    if len(me.polygons) > target:
        body.decimate_to(ob, target)
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bmesh.ops.dissolve_degenerate(bm, dist=1e-4, edges=bm.edges[:])
    bm.to_mesh(me)
    bm.free()
    body.flat_shade(ob)
    return ob

def from_shapes(name, shapes, mat=None):
    ob = lib.make_object(name, shapes, None, None)
    if mat:
        ob.data.materials.clear()
        ob.data.materials.append(bpy.data.materials[mat])
    body.flat_shade(ob)
    return ob


def weight_to(ob, bone):
    ob.vertex_groups.clear()
    g = ob.vertex_groups.new(name=bone)
    g.add(list(range(len(ob.data.vertices))), 1.0, 'REPLACE')


def skin_to(ob, arm):
    ob.parent = arm
    ob.matrix_parent_inverse = arm.matrix_world.inverted()
    m = ob.modifiers.new('Armature', 'ARMATURE')
    m.object = arm


def part(w, *bones):
    """Total weight of the given bones (both sides when the name has no side)."""
    tot = 0.0
    for b in bones:
        tot += w.get(b, 0.0) + w.get(b + '_L', 0.0) + w.get(b + '_R', 0.0)
    return tot


# ------------------------------------------------------------------ clothes

def trunks(c, hem_frac=0.32, mat='Outfit', thick=0.007, name='Trunks', ragged=False, seed=11):
    """Swim shorts: from the waist to part-way down the thighs."""
    lu = c.lu
    waist = lu['pelvis'][1] + 0.05 * c.ts
    thigh, knee = lu['thigh_L'][1], lu['shin_L'][1]
    hem = thigh - hem_frac * (thigh - knee)

    def region(u, w):
        if part(w, 'Arm', 'ForeArm') > 0.3:
            return False
        h = hem + (0.045 * noise.noise(Vector((u[0] * 17, u[2] * 17, seed))) if ragged else 0.0)
        return h < u[1] < waist
    return shell(c, name, region, thick, amp=0.003, freq=11, target=700 if c.kind == 'fat' else 460, mat=mat, seed=seed, cuts=() if ragged else (hem, waist))


def shirt(c, kind='tee', hem_off=-0.085, sleeve=0.12, mat='Shirt', name='Gear_Shirt', ragged=False, seed=21, thick=0.013, target=420):
    """kind: 'tee' (hangs below the waist, untucked), 'top' (cut short, shows the belly), 'polo' (a tee)."""
    lu = c.lu
    sh = c.shoulder
    waist = lu['pelvis'][1] + 0.075 * c.ts
    hem = waist + hem_off if kind != 'top' else waist + 0.17 * c.ts
    top = sh + 0.035 * c.ts  # the collar: one clean horizontal edge round the base of the neck

    def region(u, w):
        h = hem + (0.05 * noise.noise(Vector((u[0] * 15, u[2] * 15, seed))) if ragged else 0.0)
        if u[1] < h or u[1] > top:
            return False
        if part(w, 'Head') > 0.4 or part(w, 'ForeArm') > 0.4 or part(w, 'Thigh', 'Shin', 'Ankle') > 0.5:
            return False
        if part(w, 'Arm') > 0.5:
            return u[1] > sh - sleeve - (0.04 * noise.noise(Vector((u[0] * 15, u[2] * 15, seed + 1))) if ragged else 0.0)
        return True
    # an untucked shirt flares toward the hem
    def flare(u):
        return 0.014 * max(0.0, min(1.0, (hem + 0.14 - u[1]) / 0.14))
    return shell(c, name, region, thick, amp=0.005, freq=9, target=target, mat=mat, seed=seed, cuts=(top,) if ragged else (hem, sh - sleeve, top), flare=flare)


# ------------------------------------------------------------------ the head: hair, cap, beard

def _ear(c, u):
    return abs(u[0]) > 0.062 * c.kx and abs(u[2] - c.hz) < 0.045 * c.k and u[1] < c.hj + 0.085 * c.k


def swim_cap(c):
    k, hj, hz = c.k, c.hj, c.hz

    # the cap's edge is one sloped plane: over the brows at the front, down to the nape at the back
    a = Vector((0, hj + 0.082 * k, hz + 0.10 * k))
    b = Vector((0, hj + 0.010 * k, hz - 0.10 * k))
    n = Vector((0, (a.z - b.z), -(a.y - b.y))).normalized()

    def region(u, w):
        return (Vector(u) - a).dot(n) > 0 and not _ear(c, u)
    return shell(c, 'Gear_SwimCap', region, 0.008 * k, amp=0.002, freq=10, target=170, mat='Cap', seed=2, cuts=((tuple(a), tuple(n)),))


def hair(c, style='short', mat='Hair', seed=3, thick=0.011, target=220):
    """style: 'short', 'sides' (bald top, a fringe round the sides and back), 'long' (down to the shoulders at the back)."""
    k, hj, hz, kx = c.k, c.hj, c.hz, c.kx

    def region(u, w):
        front = u[2] > hz + 0.04 * k
        side = abs(u[0]) > 0.055 * kx
        if style == 'sides':  # a fringe above the ears and round the back, the top stays bald
            return (hj + 0.088 * k < u[1] < hj + 0.112 * k if side else hj + 0.02 * k < u[1] < hj + 0.112 * k) and u[2] < hz - 0.005 * k
        if _ear(c, u):
            return False
        if style == 'long' and not front and (u[2] < hz - 0.02 * k or side):
            return u[1] > hj - 0.13 * k and part(w, 'Head', 'Spine') > 0.0 and (u[2] < hz - 0.03 * k)
        return u[1] > (hj + 0.108 * k if front else hj + 0.088 * k if side else hj + 0.02 * k)
    return shell(c, 'Gear_Hair', region, thick * k, amp=0.012 * k, freq=26, target=target, mat=mat, seed=seed)


def beard(c, big=False, seed=5, mat='Hair'):
    """Chin and jaw with sideburns, from just under the mouth down; nothing around the nose and lips."""
    k, hj, hz, kx = c.k, c.hj, c.hz, c.kx
    mouth = hj + 0.040 * k - 0.068 * k

    def region(u, w):
        if u[1] < hj - 0.062 * k or part(w, 'Head') < 0.5 or u[2] < hz - 0.03 * k:
            return False
        if abs(u[0]) > 0.052 * kx:                      # sideburns up to the ears
            return u[1] < hj + 0.02 * k
        return u[1] < mouth - 0.012 * k                 # chin
    return shell(c, 'Gear_Beard', region, (0.016 if big else 0.012) * k, amp=0.006 * k, freq=18, target=170, mat=mat, seed=seed, split=0.05)

def ponytail(c):
    k, hj, hz = c.k, c.hj, c.hz
    S = [sweep([(0, hj + 0.075 * k, hz - 0.09 * k), (0, hj + 0.07 * k, hz - 0.16 * k), (0, hj + 0.0, hz - 0.2 * k), (0, hj - 0.08 * k, hz - 0.2 * k), (0, hj - 0.17 * k, hz - 0.17 * k)],
               [0.03 * k, 0.04 * k, 0.038 * k, 0.03 * k, 0.008 * k], [0.03 * k, 0.04 * k, 0.038 * k, 0.03 * k, 0.008 * k], ref=(1, 0, 0), sides=6, mat='Hair', kind='x'),
         ellipsoid((0, hj + 0.072 * k, hz - 0.095 * k), (0.026 * k, 0.014 * k, 0.026 * k), 'Accent', 'x', 6, 3)]
    return from_shapes('Gear_Ponytail', S)


# ------------------------------------------------------------------ face gear

def _band(c, y0, y1, mat='Strap', grow=0.0, open_deg=45):
    """A strap round the head at eye height, open at the front (so it never crosses the eyes): from open_deg round the back to -open_deg."""
    y = (y0 + y1) / 2
    n = 14
    a0 = math.radians(open_deg)
    pts = []
    for i in range(n + 1):
        th = a0 + (2 * math.pi - 2 * a0) * i / n
        pts.append(((c.rxE + grow) * math.sin(th), y, c.hzE + (c.rzE + grow) * math.cos(th)))
    return sweep(pts, (y1 - y0) / 2, 0.0035, ref=(0, 1, 0), sides=4, mat=mat, kind='x')


def _eye_spot(c, sx, off):
    """In front of one eye (Unity coords): the eye's own x and height, and `off` metres ahead of the most forward point of the brow, cheek
    and nose-side around it (the eye sits in a socket; gear has to clear its rim, not rest on the bottom)."""
    ex, ey = sx * abs(c.lu['eye_L'][0]), c.hj + 0.040 * c.k
    zs = []
    for dx in (-0.022, 0.0, 0.022):
        for dz in (-0.014, 0.0, 0.014):
            r = ray_front(c, ex + dx * c.k, ey + dz * c.k)
            if r:
                zs.append(-r[0].y)
    if not zs:
        return None
    zs.sort()
    return Vector((-ex, ey, zs[int(0.7 * (len(zs) - 1))] + off))


def goggles(c):
    k, hj, hz = c.k, c.hj, c.hz
    S = []
    for sx in (-1, 1):
        p = _eye_spot(c, sx, 0.012)
        if p is not None:
            S.append(ellipsoid((p[0], p[1], p[2]), (0.034 * k, 0.026 * k, 0.012 * k), 'Strap', 'x', 8, 4))
            S.append(ellipsoid((p[0], p[1], p[2] + 0.006 * k), (0.027 * k, 0.019 * k, 0.012 * k), 'Lens', 'x', 8, 4))
    p = _eye_spot(c, 1, 0.012)
    zf = p[2] if p is not None else hz + 0.11 * k
    S.append(box((0, hj + 0.046 * k, zf), (0.02 * k, 0.012 * k, 0.014 * k), 0.0, 1, 'Strap', 'x'))
    S.append(_band(c, hj + 0.032 * k, hj + 0.048 * k))
    return from_shapes('Gear_Goggles', S)


def sunglasses(c):
    """Two separate dark lenses with a bridge between them, and thin arms going back to the ears."""
    k, hj, hz = c.k, c.hj, c.hz
    S = []
    zf = hz + 0.11 * k
    for sx in (-1, 1):
        p = _eye_spot(c, sx, 0.012)
        if p is not None:
            zf = p[2]
            S.append(box((p[0], p[1], p[2]), (0.043 * k, 0.030 * k, 0.010 * k), 0.003, 1, 'Strap', 'x'))
            arm_x = sx * (c.rxE - 0.014)
            S.append(box((arm_x, hj + 0.040 * k, 0.5 * (zf + c.hzE)), (0.006 * k, 0.008 * k, abs(zf - c.hzE)), 0.0, 1, 'Strap', 'x'))
            S.append(box((0.5 * (arm_x + p[0] * 1.0), hj + 0.040 * k, zf), (abs(arm_x - p[0]), 0.008 * k, 0.008 * k), 0.0, 1, 'Strap', 'x'))
    S.append(box((0, hj + 0.046 * k, zf), (0.020 * k, 0.007 * k, 0.008 * k), 0.0, 1, 'Strap', 'x'))
    return from_shapes('Gear_Sunglasses', S)

def mask(c):
    k, hj, hz = c.k, c.hj, c.hz
    r = ray_front(c, 0.0, hj + 0.030 * k)
    cc = Vector(lib.to_unity(r[0] + r[1].normalized() * 0.014)) if r else Vector((0, hj + 0.03 * k, hz + 0.115))
    S = [box((cc[0], cc[1], cc[2]), (0.108 * k, 0.062 * k, 0.034 * k), 0.0, 1, 'Strap', 'x'),
         box((cc[0], cc[1], cc[2] + 0.017 * k), (0.088 * k, 0.046 * k, 0.012 * k), 0.0, 1, 'Lens', 'x'),
         ellipsoid((cc[0], cc[1] - 0.04 * k, cc[2] + 0.002), (0.02 * k, 0.014 * k, 0.016 * k), 'Strap', 'x', 8, 4),
         _band(c, hj + 0.026 * k, hj + 0.05 * k)]
    return from_shapes('Gear_Mask', S)


def pirate_hat(c):
    k, hj, hz, kx = c.k, c.hj, c.hz, c.kx
    z = c.hzE
    S = [ellipsoid((0, hj + 0.135 * k, z), (0.105 * kx, 0.05 * k, 0.13 * k), 'Accent', 'x', 10, 5),
         ellipsoid((0, hj + 0.095 * k, z), (0.17 * kx, 0.012 * k, 0.15 * k), 'Accent', 'x', 12, 4)]
    for sx in (-1, 1):
        S.append(ellipsoid((sx * 0.14 * kx, hj + 0.115 * k, z), (0.06 * kx, 0.012 * k, 0.14 * k), 'Accent', 'x', 8, 4, rot=(0, 0, sx * 32)))
    S.append(box((0, hj + 0.15 * k, z + 0.123 * k), (0.045 * k, 0.04 * k, 0.008 * k), 0.0, 1, 'Shirt', 'x'))
    return from_shapes('Gear_PirateHat', S)


def eyepatch(c):
    k, hj, hz = c.k, c.hj, c.hz
    p = _eye_spot(c, -1, 0.008)
    cc = p if p is not None else Vector((-0.036, hj + 0.04 * k, hz + 0.1))
    S = [ellipsoid((cc[0], cc[1], cc[2]), (0.026 * k, 0.024 * k, 0.008 * k), 'Strap', 'x', 8, 4)]
    band = _band(c, hj + 0.036 * k, hj + 0.044 * k)
    band.rotated((-14, 0, -18), (0, hj + 0.04 * k, c.hzE))
    S.append(band)
    return from_shapes('Gear_Eyepatch', S)


# ------------------------------------------------------------------ body gear

def backpack(c):
    ts, tz, sw = c.ts, c.tz, c.sw
    bz = c.back_z - 0.06
    S = [box((0, 0.1 + 0.2 * ts, bz), (0.27 * sw, 0.3 * ts, 0.13), 0.0, 1, 'Accent', 'x'),
         box((0, 0.1 + 0.07 * ts, bz - 0.04), (0.2 * sw, 0.1 * ts, 0.06), 0.0, 1, 'Accent', 'x')]
    for sx in (-1, 1):  # the straps go over the shoulders and down the chest, outside the shirt (1.3 cm thick)
        S.append(sweep([(sx * 0.09 * sw, 0.1 + 0.34 * ts, c.back_z + 0.01), (sx * 0.1 * sw, 0.1 + 0.45 * ts, c.back_z * 0.5), (sx * 0.1 * sw, 0.1 + 0.52 * ts, 0.0),
                        (sx * 0.1 * sw, 0.1 + 0.49 * ts, c.front_z * 0.6)], 0.017, 0.007, ref=(1, 0, 0), sides=4, mat='Strap', kind='x'))
    return from_shapes('Gear_Backpack', S)


def tank(c):
    ts, tz, sw = c.ts, c.tz, c.sw
    bz = c.back_z - 0.06
    S = []
    for sx in (-1, 1):
        t = tube([(0.0, .062, .062), (0.12, .066, .066), (0.3, .066, .066), (0.38, .05, .05)], 'y', 8, dome0=2, dome1=2, depth0=0.05, depth1=0.04, mat='Accent', kind='x')
        t.verts = [(x + sx * 0.08 * sw, y * ts + 0.04, z + bz) for x, y, z in t.verts]
        S.append(t)
        S.append(sweep([(sx * 0.1 * sw, 0.1 + 0.34 * ts, c.back_z + 0.01), (sx * 0.11 * sw, 0.1 + 0.46 * ts, c.back_z * 0.5), (sx * 0.11 * sw, 0.1 + 0.52 * ts, 0.0),
                        (sx * 0.12 * sw, 0.1 + 0.4 * ts, c.front_z - 0.02), (sx * 0.12 * sw, 0.1 + 0.2 * ts, c.front_z - 0.01)], 0.02, 0.008, ref=(1, 0, 0), sides=4, mat='Strap', kind='x'))
    S.append(box((0, 0.1 + 0.42 * ts, bz - 0.02), (0.07, 0.06, 0.06), 0.0, 1, 'Metal', 'x'))
    S.append(sweep([(0.04, 0.1 + 0.46 * ts, bz - 0.02), (0.12 * sw, 0.1 + 0.62 * ts, c.back_z * 0.6), (0.06, 0.1 + 0.72 * ts, c.hzE - 0.04)], 0.012, 0.012, ref=(1, 0, 0), sides=4, mat='Metal', kind='x'))
    return from_shapes('Gear_Tank', S)


def fins(c):
    S = []
    ax, ay = abs(c.lu['ankle_R'][0]), c.lu['ankle_R'][1]
    for sx in (-1, 1):
        f = tube([(-0.05, .044, .02), (0.0, .054, .016), (0.15, .066, .012), (0.3, .09, .008), (0.4, .112, .006)], 'z', 6, dome1=1, depth1=0.01, mat='Accent', kind='x', power=2.5)
        f.verts = [(x + sx * ax, y + ay - 0.035, z) for x, y, z in f.verts]
        S.append(f)
    return from_shapes('Gear_Tank_Mask_Fins', S)


def girth(c, y):
    """Torso half-width and depth range (Unity z) at height y, from the Spine / Hips vertices of the dense body."""
    names = {g.index: g.name for g in c.dense.vertex_groups}
    xs, zs = [], []
    for v in c.dense.data.vertices:
        if abs(v.co.z - y) < 0.03 and sum(g.weight for g in v.groups if names[g.group] in ('Spine', 'Hips')) > 0.6:
            xs.append(abs(v.co.x))
            zs.append(-v.co.y)
    return max(xs), min(zs), max(zs)


def swim_ring(c):
    y = c.lu['pelvis'][1] + 0.14 * c.ts
    hw, z0, z1 = girth(c, y)
    rx, rz, zc = hw + 0.075, (z1 - z0) / 2 + 0.075, (z0 + z1) / 2
    pts = [(rx * math.cos(2 * math.pi * i / 14), y, zc + rz * math.sin(2 * math.pi * i / 14)) for i in range(14)]
    S = [sweep(pts, 0.085, 0.085, ref=(0, 1, 0), sides=6, mat='Accent', kind='x', loop=True)]
    return from_shapes('Gear_SwimRing', S)


def antenna(c):
    k, hj, hz = c.k, c.hj, c.hz
    S = [tube([(hj + 0.17 * k, .008, .008), (hj + 0.26 * k, .006, .006)], 'y', 6, mat='Metal', kind='x'),
         ellipsoid((0, hj + 0.275 * k, c.hzE), (0.022 * k, 0.022 * k, 0.022 * k), 'Accent', 'x', 8, 4)]
    return from_shapes('Gear_Antenna', S)


def blade(c):
    """A running blade for the right leg: socket under the knee, a curved spring to the toe."""
    lx, kn, an = c.lu['shin_R'][0], c.lu['shin_R'][1], c.lu['ankle_R'][1]
    z = c.lu['shin_R'][2]
    S = [tube([(kn + 0.02, .062, .066), (kn - 0.06, .056, .056), (kn - 0.12, .044, .044)], 'y', 8, dome1=1, depth1=0.02, mat='Metal', kind='x', power=2.4)]
    S[0].verts = [(x + lx, y, zz + z) for x, y, zz in S[0].verts]
    S.append(sweep([(lx, kn - 0.1, z), (lx, kn - 0.3, z - 0.045), (lx, kn - 0.46, z - 0.05), (lx, an + 0.02, z - 0.01), (lx, an - 0.03, z + 0.08), (lx, an - 0.035, z + 0.18)],
                   0.022, 0.0065, ref=(1, 0, 0), sides=4, mat='Metal', kind='x'))
    return from_shapes('Gear_Blade', S)


def nogear_blade(c):
    """The real right lower leg (knee down), cut from the game body so it can be hidden when the blade is worn. Returns the leg object;
    the body loses those faces."""
    knee = c.lu['shin_R'][1]
    bd = c.body
    bm = bmesh.new()
    bm.from_mesh(bd.data)
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=Vector(lib.to_blender((0, knee, 0))), plane_no=Vector((0, 0, 1)))
    bm.faces.index_update()
    leg = [f for f in bm.faces if (lambda u: u[0] > 0.0 and u[1] < knee + 0.001)(U(f.calc_center_median()))]
    # the leg piece: a copy with everything else removed
    bm_leg = bm.copy()
    bm_leg.faces.ensure_lookup_table()
    leg_idx = {f.index for f in leg}
    bmesh.ops.delete(bm_leg, geom=[f for f in bm_leg.faces if f.index not in leg_idx], context='FACES')
    bmesh.ops.delete(bm_leg, geom=[v for v in bm_leg.verts if not v.link_faces], context='VERTS')
    bmesh.ops.delete(bm, geom=leg, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(bd.data)
    bm.free()
    me = bpy.data.meshes.new('NoGear_Blade')
    bm_leg.to_mesh(me)
    bm_leg.free()
    ob = bpy.data.objects.new('NoGear_Blade', me)
    bpy.context.scene.collection.objects.link(ob)
    for g in bd.vertex_groups:
        ob.vertex_groups.new(name=g.name)
    for m in bd.data.materials:
        me.materials.append(m)
    body.flat_shade(ob)
    return ob


# ------------------------------------------------------------------ one body, fully dressed

# what each body's FBX carries (the game switches the objects on per hero). Names are the final ones.
KITS = {
    'swimmer': ['Trunks', 'Gear_SwimCap', 'Gear_Goggles', 'Gear_Hair', 'Gear_Shirt', 'Gear_Beard', 'Gear_PirateHat', 'Gear_Eyepatch', 'Gear_Mask', 'Gear_Tank',
                'Gear_Tank_Mask_Fins', 'Gear_Blade', 'NoGear_Blade'],
    'kid': ['Trunks', 'Gear_Hair', 'Gear_Shirt', 'Gear_Backpack'],
    'fat': ['Trunks', 'Gear_SwimRing', 'Gear_Sunglasses'],
    'old': ['Trunks', 'Gear_Hair', 'Gear_Beard', 'Gear_Shirt'],
    'woman': ['Trunks', 'Gear_Hair', 'Gear_Ponytail', 'Gear_Shirt'],
}
HEAD_GEAR = {'Gear_SwimCap', 'Gear_Goggles', 'Gear_PirateHat', 'Gear_Eyepatch', 'Gear_Mask', 'Gear_Ponytail', 'Gear_Sunglasses', 'Gear_Antenna'}
BODY_GEAR = {'Gear_Backpack': 'Spine', 'Gear_Tank': 'Spine', 'Gear_SwimRing': 'Hips'}


def make_item(c, name):
    k = c.kind
    if name == 'Trunks':
        return trunks(c, hem_frac=0.45 if k == 'fat' else 0.40 if k in ('kid', 'old') else 0.32, thick=0.024 if k == 'fat' else 0.011)
    if name == 'Gear_Shirt':
        if k == 'woman':
            return shirt(c, 'top', sleeve=0.0, thick=0.011)
        if k == 'swimmer':
            return shirt(c, 'tee', ragged=True, thick=0.013, target=380)  # the zombie's and the pirate's rags
        return shirt(c, 'tee', sleeve=0.11 if k == 'kid' else 0.13)
    if name == 'Gear_Hair':
        return hair(c, 'sides' if k == 'old' else 'short')
    if name == 'Gear_Beard':
        return beard(c, big=(k == 'swimmer'))
    one = {'Gear_SwimCap': swim_cap, 'Gear_Goggles': goggles, 'Gear_PirateHat': pirate_hat, 'Gear_Eyepatch': eyepatch, 'Gear_Mask': mask,
           'Gear_Tank': tank, 'Gear_Tank_Mask_Fins': fins, 'Gear_Blade': blade, 'Gear_Backpack': backpack, 'Gear_SwimRing': swim_ring,
           'Gear_Sunglasses': sunglasses, 'Gear_Ponytail': ponytail, 'Gear_Antenna': antenna}
    return one[name](c)


def dress(kind):
    """Build every item of the body's kit, skin it to the body's rig, name it '<kind>.<final name>'. Returns {final name: object}."""
    materials()
    for ob in [o for o in bpy.data.objects if o.name.startswith(f'{kind}.')]:
        bpy.data.objects.remove(ob, do_unlink=True)
    c = ctx(kind)
    rig = bpy.data.objects[f'LP_{kind}_rig']
    out = {}
    for name in KITS[kind]:
        if name == 'NoGear_Blade':
            ob = nogear_blade(c)
            ob.name = 'NoGear_Blade'
        else:
            ob = make_item(c, name)
        if name in HEAD_GEAR:
            weight_to(ob, 'Head')
        elif name in BODY_GEAR:
            weight_to(ob, BODY_GEAR[name])
        elif name == 'Gear_Hair' or name == 'Gear_Beard':
            pass  # cut from the body: keeps its weights
        elif name == 'Gear_Blade':
            weight_to(ob, 'Shin_R')
        elif name == 'Gear_Tank_Mask_Fins':  # one fin per foot: Unity +x is the right leg, Blender -x
            ob.vertex_groups.clear()
            gl, gr = ob.vertex_groups.new(name='Ankle_L'), ob.vertex_groups.new(name='Ankle_R')
            for v in ob.data.vertices:
                (gr if v.co.x < 0 else gl).add([v.index], 1.0, 'REPLACE')
        skin_to(ob, rig)
        ob['base'] = name
        ob.name = f'{kind}.{name}'
        out[name] = ob
    return out
