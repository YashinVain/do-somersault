"""Trees, palms, rocks, rock pillars and wall ledges for the locations.
Run: blender --background --python make_nature.py   -> Assets/_Project/Models/Props/{Trees,Rocks,Cliffs}.fbx
Every prop is built at the size the scene builder already uses, so it can replace the old primitives one to one.
Material slots are named for what they are (Crown, Bark, Rock0 ...); the scene builder gives each slot its own colour.
"""
import math
import os
import sys
import bpy
import bmesh
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib
import nature
from lib import tube, sweep, material, Shape, to_blender
from nature import blob, blade, make_static, empty, export_static, vnoise, orient_closed

lib.reset_scene()
OUT = os.path.join(lib.PROJECT, 'Assets', '_Project', 'Models', 'Props')

for name, col, rough in [
    ('Crown', '#4E9A47', 0.8), ('Bark', '#7A5636', 0.9), ('Needles', '#2F6B4A', 0.8),
    ('PalmLeaf', '#4F9A4A', 0.7), ('PalmTrunk', '#9B7448', 0.9), ('Coconut', '#6B4A2B', 0.7),
    ('LeafA', '#3E8A3A', 0.8), ('LeafB', '#4FA044', 0.8),
    ('Rock0', '#8E8478', 0.9), ('Rock1', '#756D63', 0.9), ('Rock2', '#5F5850', 0.9), ('Cap', '#5E9A4E', 0.85),
    ('Top', '#8E8478', 0.9), ('Under', '#756D63', 0.9),
]:
    material(name, col, rough)

trees, rocks, cliffs = [], [], []


def trunk(points, radii, mat='Bark', sides=8):
    return sweep(points, radii, radii, ref=(1, 0, 0), sides=sides, mat=mat, kind='static', closed=True)


def flat(shape):
    shape.flat = True
    return shape


# ------------------------------------------------------------------ broadleaf trees (the old Tree: 4.8 m tall, 3 m crown)
def broadleaf(name, seed, h, crown_y, spread, extra=0):
    root = empty(name)
    parts = [trunk([(0, -0.15, 0), (0.02, 0.4, 0), (0.0, h * 0.5, 0.0), (0.04, h * 0.85, 0.02), (0.05, h, 0.03)],
                   [0.34, 0.22, 0.16, 0.11, 0.07]),
             trunk([(0.02, h * 0.55, 0), (0.45 * spread, h * 0.8, 0.12), (0.8 * spread, h * 1.0, 0.2)], [0.09, 0.06, 0.03]),
             trunk([(0.0, h * 0.6, 0), (-0.4 * spread, h * 0.85, -0.12), (-0.7 * spread, h * 1.0, -0.2)], [0.08, 0.055, 0.03])]
    for k in range(3):   # roots
        a = k * 2.1 + seed
        parts.append(blob((math.cos(a) * 0.3, 0.05, math.sin(a) * 0.3), (0.2, 0.16, 0.2), 'Bark', 1, 0.2, seed + k, rot=(0, 0, 0)))
    make_static(name + '_Trunk', parts, parent=root)
    crown = [blob((0, crown_y, 0), (1.35 * spread, 1.1, 1.3 * spread), 'Crown', 1, 0.27, seed),
             blob((0.6 * spread, crown_y + 0.95, -0.25), (0.9 * spread, 0.78, 0.9 * spread), 'Crown', 1, 0.27, seed + 1),
             blob((-0.75 * spread, crown_y - 0.3, 0.45), (0.8 * spread, 0.72, 0.8 * spread), 'Crown', 1, 0.27, seed + 2),
             blob((0.45 * spread, crown_y - 0.55, 0.8 * spread), (0.72 * spread, 0.62, 0.72 * spread), 'Crown', 1, 0.27, seed + 3),
             blob((-0.4 * spread, crown_y + 0.7, -0.6), (0.72 * spread, 0.66, 0.72 * spread), 'Crown', 1, 0.27, seed + 4)]
    if extra:
        crown.append(blob((0.1, crown_y + 1.5, 0.1), (0.7 * spread, 0.6, 0.7 * spread), 'Crown', 1, 0.27, seed + 5))
    make_static(name + '_Crown', crown, parent=root)
    return root


trees.append(broadleaf('Tree_A', 11, 2.8, 3.55, 1.0))
trees.append(broadleaf('Tree_B', 23, 3.2, 4.1, 0.86, extra=1))
trees.append(broadleaf('Tree_C', 37, 2.5, 3.2, 1.12))


# ------------------------------------------------------------------ pines: layered tiers with ragged skirts
def tier(y0, y1, r, seed, sides=9):
    verts, faces = [], []
    n = sides * 2
    for i in range(n):
        a = 2 * math.pi * i / n
        rr = r * (1.0 if i % 2 == 0 else 0.74) * (1 + 0.08 * vnoise(math.cos(a) * 2, i * 0.7, seed, seed))
        verts.append((math.cos(a) * rr, y0 - (0.0 if i % 2 == 0 else 0.0) + (0.0 if i % 2 == 0 else 0.22), math.sin(a) * rr))
    verts.append((0, y1, 0))     # apex
    verts.append((0, y0 + 0.25, 0))     # underside centre
    apex, under = n, n + 1
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, apex))
        faces.append((j, i, under))
    return orient_closed(Shape(verts, faces, 'Needles', 'static', flat=True))


def pine(name, seed, tiers, trunk_h):
    root = empty(name)
    make_static(name + '_Trunk', [trunk([(0, -0.1, 0), (0, trunk_h * 0.5, 0), (0.02, trunk_h, 0)], [0.24, 0.17, 0.1])], parent=root)
    make_static(name + '_Needles', [tier(*t, seed + i) for i, t in enumerate(tiers)], parent=root)
    return root


trees.append(pine('Pine_A', 5, [(1.7, 3.9, 1.75), (3.0, 5.1, 1.4), (4.1, 6.2, 1.05), (5.0, 7.0, 0.62)], 5.2))
trees.append(pine('Pine_B', 9, [(1.5, 3.2, 1.55), (2.6, 4.4, 1.35), (3.6, 5.4, 1.12), (4.5, 6.3, 0.88), (5.3, 7.1, 0.56)], 5.4))


# ------------------------------------------------------------------ palms
def palm(name, seed, bend, height, leaves=9):
    root = empty(name)
    pts = [(bend * (t ** 1.8) * 1.0, height * t, 0.0) for t in (0, 0.2, 0.4, 0.6, 0.8, 1.0)]
    radii = [0.27, 0.22, 0.19, 0.17, 0.15, 0.12]
    make_static(name + '_Trunk', [trunk(pts, radii, 'PalmTrunk', 9)], parent=root)
    tip = pts[-1]
    crown = []
    for i in range(leaves):
        yaw = i * 360.0 / leaves + (seed * 13) % 17
        crown.append(blade(tip, yaw, 1.9 + 0.35 * vnoise(i, seed, 0, seed), 0.24, droop=0.55 + 0.1 * (i % 3), rise=0.38))
    for i in range(3):
        a = i * 2.1 + seed
        crown.append(blob((tip[0] + math.cos(a) * 0.16, tip[1] - 0.14, tip[2] + math.sin(a) * 0.16), (0.1, 0.1, 0.1), 'Coconut', 1, 0.12, seed + i))
    make_static(name + '_Crown', crown, origin=tip, parent=root)
    # the crown is parented to the root, not the trunk: its origin is the top of the trunk
    return root


trees.append(palm('Palm_A', 4, 1.3, 5.4))
trees.append(palm('Palm_B', 8, -1.1, 6.0, 8))


# ------------------------------------------------------------------ jungle trees: tall, buttressed, with a heavy crown
def jungle(name, seed, h):
    root = empty(name)
    parts = [trunk([(0, -0.2, 0), (0.05, h * 0.3, 0.0), (-0.08, h * 0.65, 0.04), (0.0, h, 0)], [0.55, 0.38, 0.3, 0.24], 'Bark', 9)]
    for k in range(4):
        a = k * 1.6 + seed
        parts.append(blob((math.cos(a) * 0.5, 0.5, math.sin(a) * 0.5), (0.28, 0.75, 0.55), 'Bark', 1, 0.14, seed + k,
                          rot=(0, math.degrees(a), 0)))
    make_static(name + '_Trunk', parts, parent=root)
    crown = []
    for i in range(5):
        a = i * 1.3 + seed * 0.7
        size = 1.9 + 0.5 * vnoise(i, seed, 1, seed)
        crown.append(blob((math.cos(a) * (1.1 if i else 0), h + 0.7 * (i % 3) - 0.3, math.sin(a) * (1.1 if i else 0)),
                          (size, size * 0.72, size), 'LeafA' if i % 2 == 0 else 'LeafB', 1, 0.28, seed + i))
    make_static(name + '_Crown', crown, origin=(0, h, 0), parent=root)
    return root


for name, seed, h in (('Jungle_A', 3, 5.6), ('Jungle_B', 6, 6.8), ('Jungle_C', 9, 8.0)):
    trees.append(jungle(name, seed, h))

# ------------------------------------------------------------------ boulders: 4 shapes, radius 0.5 so the old scales still fit
rocks.append(make_static('Rock_A', [blob((0, 0, 0), (0.5, 0.42, 0.5), 'Rock0', 2, 0.2, 1, flat_bottom=-0.55)]))
rocks.append(make_static('Rock_B', [blob((0, 0, 0), (0.5, 0.46, 0.46), 'Rock0', 2, 0.22, 2, flat_bottom=-0.5),
                                    blob((0.28, -0.16, 0.2), (0.26, 0.22, 0.26), 'Rock0', 1, 0.2, 3)]))
rocks.append(make_static('Rock_C', [blob((0, 0, 0), (0.42, 0.55, 0.42), 'Rock0', 2, 0.2, 4, flat_bottom=-0.6)]))
rocks.append(make_static('Rock_D', [blob((0, 0, 0), (0.55, 0.32, 0.5), 'Rock0', 2, 0.24, 5, flat_bottom=-0.4),
                                    blob((-0.2, 0.12, -0.1), (0.3, 0.22, 0.3), 'Rock0', 1, 0.2, 6)]))


# ------------------------------------------------------------------ rock pillars: layered strata, a turf top; height 1, base radius 1
def pillar(name, seed, taper, sides=10):
    """A weathered rock column: vertical facets with a ragged outline, a few darker bands and a turf top."""
    def radius(y):
        return 1 - (1 - taper) * y

    def segment(y0, y1, mat, rings):
        rows = [(y0 + (y1 - y0) * i / rings, radius(y0 + (y1 - y0) * i / rings), radius(y0 + (y1 - y0) * i / rings)) for i in range(rings + 1)]
        s = tube(rows, 'y', sides, mat=mat, kind='static', closed=False)
        s.flat = True
        out = []
        for (x, y, z) in s.verts:
            n = 1 + 0.15 * vnoise(x * 1.5 + seed, y * 2.6, z * 1.5, seed) + 0.05 * vnoise(x * 4, y * 6, z * 4, seed + 5)
            out.append((x * n, y, z * n))
        s.verts = out
        return s

    shapes = [segment(0.0, 0.3, 'Rock0', 3), segment(0.3, 0.37, 'Rock2', 1), segment(0.37, 0.64, 'Rock1', 3),
              segment(0.64, 0.7, 'Rock2', 1), segment(0.7, 0.99, 'Rock0', 3)]
    cap = tube([(0.99, taper * 1.05, taper * 1.05), (1.02, taper * 1.07, taper * 1.07), (1.035, taper * 0.97, taper * 0.97)], 'y', sides,
               dome1=2, depth1=0.03, mat='Cap', kind='static')
    cap.flat = True
    shapes.append(cap)
    for k in range(4):   # small chips lying against the column, half buried
        a = k * 1.6 + seed
        y = 0.1 + 0.2 * k
        r = radius(y) * 0.98
        shapes.append(blob((math.cos(a) * r, y, math.sin(a) * r), (0.11, 0.055, 0.11), 'Rock0' if k % 2 else 'Rock1', 1, 0.3, seed + k))
    for k in range(5):
        a = k * 1.25 + seed
        shapes.append(blob((math.cos(a) * taper * 0.95, 1.03, math.sin(a) * taper * 0.95), (0.16, 0.05, 0.16), 'Cap', 1, 0.2, seed + k))
    return make_static(name, shapes)


cliffs.append(pillar('Pillar_A', 3, 0.64))
cliffs.append(pillar('Pillar_B', 7, 0.76))


# ------------------------------------------------------------------ the wall ledge: four stone slabs stepping in as they go down
def slab(center, size, seed, mat):
    """A rock slab: flat on top, with a ragged outline and rounded edges below."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=3, use_grid_fill=True)
    sx, sy, sz = size
    verts = []
    for v in bm.verts:
        x, y, z = v.co.x * sx, v.co.z * sy, -v.co.y * sz       # cube z (Blender up) is the slab's height
        top = v.co.z > 0.499
        n = vnoise(x * 1.3 + seed, y * 1.1, z * 1.3, seed)
        if not top:
            k = 1 + 0.08 * n
            x, z = x * k, z * k
            y += 0.12 * vnoise(x, z, y, seed + 3) * sy * (1 if y < 0 else 0.2)
        else:
            x, z = x * (1 + 0.05 * n), z * (1 + 0.05 * n)
        verts.append((x, y, z))
    faces = [tuple(v.index for v in f.verts) for f in bm.faces]
    bm.free()
    s = orient_closed(Shape(verts, faces, mat, 'static', flat=True))
    return s.moved(center)


def ledge(name, seed):
    steps = [(3.3, 1.0, 1.3), (2.9, 0.76, 1.5), (2.4, 0.5, 1.8), (1.9, 0.28, 2.0)]
    reach = 2.9
    y = 0.0
    shapes = []
    for i, (w, k, t) in enumerate(steps):
        r = reach * k
        center = (0.1 if i % 2 == 0 else -0.1, y - t * 0.5, r * 0.5 - 0.8)
        shapes.append(slab(center, (w, t, r + 1.6), seed + i, 'Top' if i % 2 == 0 else 'Under'))
        y -= t - 0.1
    return make_static(name, shapes)


cliffs.append(ledge('Ledge_A', 5))
cliffs.append(ledge('Ledge_B', 12))

# ------------------------------------------------------------------ export and previews
export_static(os.path.join(OUT, 'Trees.fbx'), trees + [o for o in bpy.data.objects if o.parent in trees])
export_static(os.path.join(OUT, 'Rocks.fbx'), rocks)
export_static(os.path.join(OUT, 'Cliffs.fbx'), cliffs)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(lib.ROOT, 'Nature.blend'))

if '--no-preview' not in sys.argv:
    cam = lib.setup_render(900)

    def show(objs):
        keep = set()
        for o in objs:
            keep.add(o)
            keep.update(o.children_recursive)
        for o in bpy.data.objects:
            if o.type in ('MESH', 'EMPTY'):
                o.hide_render = o not in keep

    def gallery(name, objs, spacing, target_y, dist, views=None):
        show(objs)
        for i, o in enumerate(objs):
            o.location.x = -i * spacing + (len(objs) - 1) * spacing / 2   # Blender x is Unity -x
        lib.render_views(cam, name, (0, target_y, 0), dist, views or [('front', (0.1, 0.25, 1)), ('side', (0.8, 0.45, 0.5))])

    gallery('nature_broadleaf', trees[0:3], 5.0, 2.6, 15)
    gallery('nature_pines', trees[3:5], 5.0, 3.4, 15)
    gallery('nature_palms', trees[5:7], 5.5, 3.4, 15)
    gallery('nature_jungle', trees[7:10], 7.0, 4.5, 24)
    gallery('nature_rocks', rocks, 1.4, 0.05, 5.5)
    gallery('nature_pillars', cliffs[:2], 3.6, 0.5, 8.5)
    gallery('nature_ledges', cliffs[2:], 4.5, -3.0, 13)
print('DONE')
