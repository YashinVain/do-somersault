"""The houses of the Old Town: one model per size the scene builder uses, facade towards +Z, base centred on the origin.
Run: blender --background --python make_houses.py   -> Assets/_Project/Models/Props/Houses.fbx
Each house is 9 m deep and sits in a collision box of the same size, so the model only has to look right inside it.
Material slots: Plaster (tinted per house), Trim, Roof, Glass, Shutter, Door, Leaf, Flower.
"""
import math
import os
import sys
import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib
import nature
from lib import box, material, Shape, tube
from nature import make_static, export_static, orient_closed, blob

lib.reset_scene()
OUT = os.path.join(lib.PROJECT, 'Assets', '_Project', 'Models', 'Props')

for name, col, rough in [
    ('Plaster', '#E8D8B0', 0.9), ('Trim', '#EDE5D4', 0.85), ('Roof', '#B5512F', 0.8), ('Glass', '#2F3F4C', 0.2),
    ('Shutter', '#4F7A6B', 0.8), ('Door', '#7A4E2D', 0.8), ('Leaf', '#4F8F43', 0.8), ('Flower', '#E85F5F', 0.6),
]:
    material(name, col, rough)

DEPTH = 9.0
# every (width, height) the Old Town uses: the near row and the far row
SIZES = [(8, 10), (6, 12), (5, 9), (10, 13), (6, 8), (6, 10), (6, 9), (6.5, 12), (7, 8), (6.5, 9),
         (8, 9), (7, 11), (8, 8), (9, 12), (7, 9), (8, 11), (7, 8), (8, 10), (6, 9)]


def fmt(v):
    return str(v).replace('.', 'p')


def gable(w, h, rise=2.5, over_z=0.5, over_x=0.35, thick=0.2):
    """A closed gable roof: two slopes, two triangular ends and an underside."""
    ez = DEPTH / 2 + over_z
    ex = w / 2 + over_x
    y0 = h + 0.02
    v = [(-ex, y0, -ez), (ex, y0, -ez), (ex, y0, ez), (-ex, y0, ez),            # eave corners
         (-ex, y0 + rise, 0), (ex, y0 + rise, 0),                               # ridge
         (-ex + 0.15, y0 - thick, -ez + 0.1), (ex - 0.15, y0 - thick, -ez + 0.1),
         (ex - 0.15, y0 - thick, ez - 0.1), (-ex + 0.15, y0 - thick, ez - 0.1)]  # underside lip
    faces = [(0, 1, 5, 4), (3, 2, 5, 4), (0, 3, 4), (1, 2, 5), (6, 7, 8, 9)]
    s = orient_closed(Shape(v, faces, 'Roof', 'static', flat=True))
    return s


def window(cx, fy, z0, shutters, planter):
    out = []
    out.append(box((cx, fy, z0 + 0.04), (1.12, 1.72, 0.1), 0.02, 1, 'Trim', 'static'))            # frame
    out.append(box((cx, fy, z0 + 0.09), (0.86, 1.42, 0.05), 0.01, 1, 'Glass', 'static'))          # glass
    out.append(box((cx, fy, z0 + 0.12), (0.06, 1.42, 0.05), 0.01, 1, 'Trim', 'static'))           # mullion
    out.append(box((cx, fy + 0.2, z0 + 0.12), (0.86, 0.05, 0.05), 0.01, 1, 'Trim', 'static'))     # transom
    out.append(box((cx, fy - 0.92, z0 + 0.13), (1.4, 0.12, 0.3), 0.03, 1, 'Trim', 'static'))      # sill
    out.append(box((cx, fy + 0.97, z0 + 0.07), (1.34, 0.15, 0.16), 0.03, 1, 'Trim', 'static'))    # lintel
    if shutters:
        for sx in (-1, 1):
            out.append(box((cx + sx * 0.79, fy, z0 + 0.04), (0.42, 1.66, 0.07), 0.015, 1, 'Shutter', 'static'))
    if planter:
        out.append(box((cx, fy - 1.07, z0 + 0.24), (1.04, 0.2, 0.28), 0.03, 1, 'Door', 'static'))
        for k, dx in enumerate((-0.32, 0.0, 0.32)):
            out.append(blob((cx + dx, fy - 0.9, z0 + 0.25), (0.17, 0.13, 0.15), 'Leaf', 1, 0.2, k + 3))
            out.append(blob((cx + dx * 0.9, fy - 0.78, z0 + 0.3), (0.05, 0.05, 0.05), 'Flower', 0, 0.1, k + 1))
    return out


def house(w, h):
    z0 = DEPTH / 2
    shapes = [box((0, h / 2, 0), (w, h, DEPTH), 0.05, 1, 'Plaster', 'static'),
              box((0, 0.3, 0), (w + 0.22, 0.6, DEPTH + 0.22), 0.04, 1, 'Trim', 'static'),           # plinth
              box((0, h - 0.14, 0), (w + 0.46, 0.3, DEPTH + 0.46), 0.05, 1, 'Trim', 'static'),      # cornice
              gable(w, h)]
    # corner stones
    for sx in (-1, 1):
        shapes.append(box((sx * (w / 2 - 0.12), h / 2, z0 + 0.02), (0.28, h - 0.6, 0.06), 0.02, 1, 'Trim', 'static'))
    # ridge cap and a chimney
    shapes.append(box((0, h + 2.55, 0), (w + 0.8, 0.14, 0.34), 0.04, 1, 'Trim', 'static'))
    shapes.append(box((w * 0.25, h + 2.0, -1.6), (0.75, 2.2, 0.75), 0.04, 1, 'Trim', 'static'))
    shapes.append(box((w * 0.25, h + 3.14, -1.6), (0.95, 0.16, 0.95), 0.03, 1, 'Trim', 'static'))

    variant = int(w * 2 + h)
    shutters = variant % 2 == 0
    planters = variant % 3 != 1
    xs = []
    wx = -w / 2 + 1.6
    while wx < w / 2 - 0.8:
        xs.append(wx)
        wx += 2.4
    fy = 2.5
    floor = 0
    while fy < h - 1.5:
        for i, x in enumerate(xs):
            shapes += window(x, fy, z0, shutters and floor > 0, planters and (i + floor) % 2 == 0)
        fy += 3.0
        floor += 1

    # a door between the first two windows on the ground, with a step
    if len(xs) >= 2:
        dx = (xs[0] + xs[1]) / 2
    else:
        dx = 0.0
    shapes.append(box((dx, 1.2, z0 + 0.05), (1.5, 2.5, 0.1), 0.03, 1, 'Trim', 'static'))
    shapes.append(box((dx, 1.1, z0 + 0.11), (1.1, 2.2, 0.06), 0.02, 1, 'Door', 'static'))
    shapes.append(box((dx, 0.1, z0 + 0.45), (1.8, 0.2, 0.7), 0.03, 1, 'Trim', 'static'))
    shapes.append(box((dx + 0.34, 1.1, z0 + 0.16), (0.08, 0.08, 0.05), 0.015, 1, 'Trim', 'static'))
    return shapes


objs = []
for (w, h) in SIZES:
    objs.append(make_static(f'House_{fmt(w)}_{fmt(h)}', house(w, h)))

export_static(os.path.join(OUT, 'Houses.fbx'), objs)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(lib.ROOT, 'Houses.blend'))

if '--no-preview' not in sys.argv:
    cam = lib.setup_render(900)
    for o in objs:
        o.hide_render = True
    for name, idx in (('houses_a', [0, 1, 3]), ('houses_b', [5, 9, 12])):
        for o in objs:
            o.hide_render = True
        for k, i in enumerate(idx):
            objs[i].hide_render = False
            objs[i].location.x = -(k - 1) * 13.0
        lib.render_views(cam, name, (0, 7.0, 0), 42, [('front', (0.15, 0.1, 1)), ('three_quarter', (0.9, 0.25, 0.9))])
print('DONE', len(objs))
