"""Boats, the lighthouse, the treehouse, containers and the penguin.
Run: blender --background --python make_structures.py   -> Assets/_Project/Models/Props/Structures.fbx
Every model is built in the place the scene builder uses it, so it replaces the old primitives one to one:
  Yacht      origin on the waterline, centre of the boat, bow towards +Z (collision boxes unchanged)
  Tug        the same, 10 m long
  Gondola    origin on the waterline, prow towards +Z, 6.5 m
  Container  centred, 2.44 x 2.6 x 6.1 m, doors towards -Z
  Lighthouse origin at the foot of the tower on its rock, tower 17 m, gallery at 17.85 m
  Treehouse  origin at the foot of the tree (x of the trunk, y 0, z of the trunk), hut at 12.35 m
  Penguin    standing, facing +Z, 1.3 m
Material slots are named for what they are; the scene builder gives each slot its own colour.
"""
import math
import os
import sys
import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib
import nature
from lib import tube, sweep, box, ellipsoid, material, Shape
from nature import blob, make_static, empty, export_static, vnoise

lib.reset_scene()
OUT = os.path.join(lib.PROJECT, 'Assets', '_Project', 'Models', 'Props')

for name, col, rough, metal in [
    ('Hull', '#F4F3EE', 0.35, 0.0), ('Stripe', '#1F4E79', 0.4, 0.0), ('Teak', '#B88A5A', 0.7, 0.0), ('Glass', '#24384A', 0.1, 0.0),
    ('Steel', '#8A9097', 0.4, 0.8), ('Black', '#1E2126', 0.5, 0.0), ('White', '#E8E6DE', 0.5, 0.0), ('Red', '#C9413A', 0.5, 0.0),
    ('Gold', '#C69A3C', 0.35, 0.8), ('Seat', '#9B2F36', 0.6, 0.0), ('Rubber', '#16181B', 0.9, 0.0), ('Lamp', '#FFE7A3', 0.2, 0.0),
    ('Stone', '#9A9080', 0.9, 0.0), ('Wood', '#8A5E3B', 0.8, 0.0), ('WoodDark', '#5E3F28', 0.85, 0.0), ('Thatch', '#B59A4E', 0.95, 0.0),
    ('Paint', '#2F5B94', 0.5, 0.0), ('Penguin_Black', '#1C2230', 0.4, 0.0), ('Penguin_White', '#F4F4F0', 0.4, 0.0), ('Penguin_Orange', '#F28C28', 0.5, 0.0),
    ('Eye', '#101318', 0.2, 0.0),
]:
    material(name, col, rough, metal)

models = []


def part(name, shapes, origin=(0, 0, 0)):
    o = make_static(name, shapes, origin=origin)
    models.append(o)
    return o


def hull_shape(length, beam, depth, bow_len, stern_round, mat, bottom=-0.3, sheer_rise=0.25, bow_shape=0.55):
    """A boat hull as a loft along Z: pointed bow, rounded stern, a deck line that rises towards the bow."""
    half = length / 2
    rows = []
    n = 14
    for i in range(n + 1):
        t = -half + length * i / n
        u = (t + half) / length                               # 0 stern .. 1 bow
        # plan: full beam amidships, closing in over the bow length
        bow_k = 1.0
        if t > half - bow_len:
            k = (t - (half - bow_len)) / bow_len
            bow_k = max(0.05, (1 - k) ** (0.62 + 0.5 * bow_shape))
        stern_k = 1.0
        if t < -half + stern_round:
            k = (-half + stern_round - t) / stern_round
            stern_k = max(0.35, 1 - 0.65 * k * k)
        rx = beam / 2 * bow_k * stern_k
        top = depth + sheer_rise * u ** 2                       # the deck rises a little at the bow
        bot = bottom + (0.5 * (1 - bow_k) * depth if t > half - bow_len else 0.0)
        rows.append((t, rx, (top - bot) / 2, 0.0, (top + bot) / 2))
    return tube(rows, 'z', 20, mat=mat, kind='static', closed=True, power=3.2)


# ------------------------------------------------------------------ yacht
shapes = [hull_shape(15.4, 3.6, 3.0, 4.2, 1.6, 'Hull', bottom=-0.5)]
shapes.append(hull_shape(15.44, 3.64, 0.55, 4.2, 1.6, 'Stripe', bottom=0.65, sheer_rise=0.0))        # the blue band
shapes.append(box((0, 3.04, -0.3), (3.2, 0.08, 12.0), 0.03, 1, 'Teak', 'static'))
for k in range(9):                                                                                 # deck planks
    shapes.append(box((-1.4 + k * 0.35, 3.1, -0.3), (0.02, 0.02, 11.8), 0.005, 1, 'WoodDark', 'static'))
shapes.append(box((-0.2, 3.85, -1.5), (2.4, 1.6, 5.0), 0.25, 3, 'Hull', 'static'))                  # cabin
shapes.append(box((-0.2, 4.0, -1.5), (2.5, 0.52, 5.1), 0.04, 1, 'Glass', 'static'))                 # windows
shapes.append(box((-0.2, 4.7, -1.5), (2.8, 0.1, 5.4), 0.04, 1, 'Teak', 'static'))                   # roof
shapes.append(box((-0.2, 5.3, -2.6), (1.4, 0.7, 1.8), 0.15, 2, 'Hull', 'static'))                   # flybridge
shapes.append(box((-0.2, 5.4, -1.7), (1.5, 0.45, 0.06), 0.02, 1, 'Glass', 'static'))
shapes.append(tube([(3.0, .055, .055), (11.5, .045, .045), (12.0, .03, .03)], 'y', 8, mat='Steel', kind='static').moved((-0.3, 0, 1.5)))   # mast
shapes.append(box((-0.3, 8.0, 1.5), (0.9, 0.05, 0.05), 0.01, 1, 'Steel', 'static'))
for side in (-1, 1):
    for z in [-5.5 + 0.5 * k for k in range(0, 5)]:
        pass
    for z in [-6.0, -4.5, -3.0, -1.5, 0.0, 1.5, 3.0]:
        shapes.append(ellipsoid((side * 1.83, 2.15, z), (0.05, 0.21, 0.21), 'Glass', 'static', 8, 5))
    shapes.append(sweep([(side * 1.68, 3.62, -6.8), (side * 1.68, 3.62, 3.6), (side * 1.45, 3.62, 5.6)], 0.022, 0.022, ref=(1, 0, 0), sides=6, mat='Steel', kind='static'))
    for z in [-6.5, -5.0, -3.5, -2.0, -0.5, 1.0, 2.5, 4.0]:
        shapes.append(box((side * 1.68, 3.34, z), (0.04, 0.55, 0.04), 0.01, 1, 'Steel', 'static'))
shapes.append(box((0, 3.18, 6.7), (0.5, 0.18, 0.7), 0.05, 1, 'Steel', 'static'))                    # anchor winch
shapes.append(box((0, 1.4, -7.78), (2.0, 0.5, 0.06), 0.02, 1, 'Stripe', 'static'))                  # name plate
for k in range(2):                                                                                   # sun loungers aft
    shapes.append(box((-0.9 + k * 1.2, 3.25, -6.4), (0.7, 0.22, 1.3), 0.08, 2, 'Seat', 'static'))
part('Yacht', shapes)

# ------------------------------------------------------------------ tug
shapes = [hull_shape(12.2, 4.2, 2.6, 3.4, 1.4, 'Red', bottom=-0.4, sheer_rise=0.4, bow_shape=0.4)]
shapes.append(hull_shape(12.26, 4.26, 0.7, 3.4, 1.4, 'Black', bottom=-0.4, sheer_rise=0.0, bow_shape=0.4))   # boot topping
shapes.append(box((0, 2.64, 0), (3.9, 0.08, 9.6), 0.03, 1, 'Steel', 'static'))
shapes.append(box((0, 3.7, 1.8), (3.0, 2.2, 3.2), 0.2, 3, 'White', 'static'))                       # wheelhouse
shapes.append(box((0, 3.95, 1.8), (3.06, 0.8, 3.26), 0.03, 1, 'Glass', 'static'))
shapes.append(box((0, 4.9, 1.8), (3.4, 0.14, 3.6), 0.05, 1, 'Steel', 'static'))
shapes.append(tube([(2.8, .5, .5), (5.4, .45, .45), (5.9, .5, .5)], 'y', 14, mat='Black', kind='static', dome1=1, depth1=0.1).moved((0, 0, -2.2)))   # funnel
shapes.append(tube([(5.0, .52, .52), (5.35, .52, .52)], 'y', 14, mat='Red', kind='static', closed=False).moved((0, 0, -2.2)))
shapes.append(tube([(4.95, .03, .03), (7.2, .025, .025)], 'y', 8, mat='Steel', kind='static').moved((0, 0, 2.6)))   # mast
shapes.append(box((0, 3.0, 5.0), (0.7, 0.4, 0.7), 0.1, 2, 'Steel', 'static'))                        # towing bitt
for side in (-1, 1):
    for z in (-3.6, -1.2, 1.2, 3.6):                                                                  # old tyre fenders
        shapes.append(tube([(-0.2, .34, .34), (0.2, .34, .34)], 'x', 14, mat='Rubber', kind='static', closed=False).moved((0, 1.0, z)).rotated((0, 0, 0)).moved((side * 2.2, 0, 0)))
    shapes.append(box((side * 1.85, 3.05, -3.0), (0.06, 0.9, 0.06), 0.01, 1, 'Steel', 'static'))
    shapes.append(sweep([(side * 1.9, 3.3, -4.2), (side * 1.9, 3.3, 3.0)], 0.02, 0.02, ref=(1, 0, 0), sides=6, mat='Steel', kind='static'))
shapes.append(ellipsoid((0, 3.1, -4.1), (0.55, 0.3, 0.6), 'Red', 'static', 10, 5))                    # life raft canister
part('Tug', shapes)

# ------------------------------------------------------------------ gondola
rows = []
for i in range(17):
    t = -3.25 + 6.5 * i / 16
    u = abs(t) / 3.25
    rise = 0.62 * max(0.0, (u - 0.55) / 0.45) ** 2                 # both ends sweep up
    w = 0.55 * (1 - 0.82 * max(0.0, (u - 0.5) / 0.5) ** 1.6)
    rows.append((t, w, 0.22, 0.0, 0.2 + rise))
shapes = [tube(rows, 'z', 14, mat='Black', kind='static', power=3.0)]
shapes.append(box((0, 0.5, 0.4), (0.8, 0.14, 1.2), 0.04, 1, 'Seat', 'static'))
shapes.append(box((0, 0.74, -0.15), (0.8, 0.5, 0.1), 0.04, 1, 'Seat', 'static'))
shapes.append(sweep([(0, 0.2, 3.1), (0, 0.9, 3.45), (0, 1.5, 3.3), (0, 1.55, 3.05)], 0.07, 0.04, ref=(1, 0, 0), sides=8, mat='Steel', kind='static'))   # the iron prow
for k in range(5):
    shapes.append(box((0, 1.1 - k * 0.1, 3.4 - k * 0.0), (0.02, 0.05, 0.28), 0.005, 1, 'Steel', 'static'))
shapes.append(sweep([(0, 0.25, -3.1), (0, 0.9, -3.4), (0, 1.0, -3.2)], 0.07, 0.04, ref=(1, 0, 0), sides=8, mat='Gold', kind='static'))
shapes.append(box((0.45, 0.62, -2.5), (0.12, 0.62, 0.12), 0.03, 1, 'WoodDark', 'static'))           # the rowlock post
shapes.append(sweep([(0.45, 0.8, -2.5), (0.2, 0.7, -2.0), (-0.6, 0.0, 0.0)], 0.03, 0.03, ref=(1, 0, 0), sides=6, mat='Wood', kind='static'))
part('Gondola', shapes)

# ------------------------------------------------------------------ container
shapes = [box((0, 0, 0), (2.4, 2.56, 6.0), 0.04, 1, 'Paint', 'static')]
for k in range(13):                                                                                   # corrugation on both sides
    z = -2.7 + k * 0.45
    for side in (-1, 1):
        shapes.append(box((side * 1.215, 0.0, z), (0.07, 2.4, 0.22), 0.02, 1, 'Paint', 'static'))
    shapes.append(box((0, 1.29, z), (2.2, 0.05, 0.2), 0.015, 1, 'Paint', 'static'))
for sx in (-1, 1):
    for sy in (-1, 1):
        for sz in (-1, 1):
            shapes.append(box((sx * 1.16, sy * 1.26, sz * 2.94), (0.2, 0.2, 0.2), 0.03, 1, 'Steel', 'static'))   # corner castings
for sx in (-1, 1):
    shapes.append(box((sx * 1.15, 0, -3.035), (0.09, 2.5, 0.09), 0.02, 1, 'Steel', 'static'))                       # door frame
shapes.append(box((0, 0, -3.04), (2.3, 2.5, 0.05), 0.02, 1, 'Paint', 'static'))
for sx in (-0.75, -0.25, 0.25, 0.75):                                                                  # locking bars
    shapes.append(tube([(-1.1, .025, .025), (1.1, .025, .025)], 'y', 6, mat='Steel', kind='static').moved((sx, 0, -3.1)))
    shapes.append(box((sx, 0.2, -3.1), (0.12, 0.08, 0.04), 0.01, 1, 'Steel', 'static'))
shapes.append(box((0, 0.0, -3.075), (0.02, 2.45, 0.02), 0.005, 1, 'Black', 'static'))                  # the seam between the doors
part('Container', shapes)

# ------------------------------------------------------------------ lighthouse
shapes = []
tower = [(0.9, 1.95, 1.95), (1.8, 1.8, 1.8), (6.0, 1.62, 1.62), (12.0, 1.45, 1.45), (17.0, 1.3, 1.3)]
shapes.append(tube(lib.densify(tower, 0.5), 'y', 24, mat='White', kind='static', closed=False))
def tower_r(y):
    pts = [(0.9, 1.95), (1.8, 1.8), (6.0, 1.62), (12.0, 1.45), (17.0, 1.3)]
    for (a, ra), (b, rb) in zip(pts, pts[1:]):
        if a <= y <= b:
            return ra + (rb - ra) * (y - a) / (b - a)
    return pts[-1][1]


for y0, y1 in ((4.5, 6.3), (9.8, 11.6), (14.2, 15.8)):
    rows = [(y0 + (y1 - y0) * k / 4, tower_r(y0 + (y1 - y0) * k / 4) + 0.04, tower_r(y0 + (y1 - y0) * k / 4) + 0.04) for k in range(5)]
    shapes.append(tube(rows, 'y', 24, mat='Red', kind='static', closed=False))
shapes.append(tube([(0.9, 2.15, 2.15), (1.5, 2.0, 2.0), (1.8, 1.9, 1.9)], 'y', 24, mat='Stone', kind='static', closed=False))   # stone plinth
shapes.append(box((0, 2.9, tower_r(2.9)), (0.95, 1.7, 0.3), 0.06, 2, 'WoodDark', 'static'))                         # door
for y in (5.0, 8.0, 11.0, 14.0):
    a = math.radians(y * 40)
    shapes.append(box((math.sin(a) * tower_r(y), y, math.cos(a) * tower_r(y)), (0.32, 0.6, 0.14), 0.03, 1, 'Glass', 'static', rot=(0, math.degrees(a), 0)))
shapes.append(tube([(17.0, 2.05, 2.05), (17.7, 2.45, 2.45), (17.95, 2.45, 2.45)], 'y', 24, mat='Red', kind='static'))          # gallery floor
for k in range(28):                                                                                      # railing
    a = 2 * math.pi * k / 28
    shapes.append(box((math.cos(a) * 2.38, 18.5, math.sin(a) * 2.38), (0.04, 1.0, 0.04), 0.005, 1, 'White', 'static'))
shapes.append(tube([(18.95, 2.4, 2.4), (19.0, 2.4, 2.4)], 'y', 28, mat='White', kind='static', closed=False))
shapes.append(tube([(18.0, 1.15, 1.15), (19.5, 1.15, 1.15)], 'y', 16, mat='Glass', kind='static', closed=False))                # lantern glass
for k in range(8):
    a = 2 * math.pi * k / 8
    shapes.append(box((math.cos(a) * 1.16, 18.75, math.sin(a) * 1.16), (0.07, 1.5, 0.07), 0.01, 1, 'White', 'static'))
shapes.append(ellipsoid((0, 18.75, 0), (0.55, 0.6, 0.55), 'Lamp', 'static', 12, 6))
shapes.append(tube([(19.5, 1.3, 1.3), (19.7, 1.35, 1.35)], 'y', 24, mat='Red', kind='static'))
shapes.append(ellipsoid((0, 19.7, 0), (1.35, 1.0, 1.35), 'Red', 'static', 24, 8, cut=(0, 90)))                                    # dome
shapes.append(ellipsoid((0, 20.9, 0), (0.18, 0.18, 0.18), 'Steel', 'static', 8, 5))
shapes.append(tube([(20.9, .03, .03), (21.9, .02, .02)], 'y', 6, mat='Steel', kind='static'))
shapes.append(box((0.35, 21.55, 0), (0.7, 0.05, 0.05), 0.01, 1, 'Steel', 'static'))
part('Lighthouse', shapes)

# ------------------------------------------------------------------ treehouse (hut, platform, posts and ladder; the tree stays as it is)
shapes = []
fy = 10.85
shapes.append(box((0, fy, 2.0), (5.4, 0.3, 4.6), 0.04, 1, 'Wood', 'static'))
for k in range(11):
    shapes.append(box((-2.5 + k * 0.5, fy + 0.16, 2.0), (0.46, 0.03, 4.55), 0.005, 1, 'WoodDark', 'static'))
for px in (-2.2, 2.2):
    shapes.append(tube([(1.0, .17, .17), (10.7, .15, .15)], 'y', 8, mat='WoodDark', kind='static').moved((px, 0, 4.1)))
    shapes.append(box((px, 3.0, 4.1), (0.08, 0.08, 0.7), 0.01, 1, 'WoodDark', 'static'))
shapes.append(box((0, 12.4, 0.8), (3.6, 2.8, 2.4), 0.06, 1, 'Wood', 'static'))                           # hut
for k in range(9):
    shapes.append(box((0, 12.4 - 1.3 + k * 0.32, 2.02), (3.62, 0.04, 0.04), 0.005, 1, 'WoodDark', 'static'))
shapes.append(box((0, 11.95, 2.03), (0.9, 1.8, 0.07), 0.03, 1, 'WoodDark', 'static'))                    # door
shapes.append(box((1.2, 12.6, 2.03), (0.7, 0.7, 0.07), 0.02, 1, 'Glass', 'static'))
shapes.append(box((1.2, 12.6, 2.06), (0.78, 0.78, 0.04), 0.02, 1, 'WoodDark', 'static'))
for r in range(5):                                                                                      # thatched pyramid roof in layers
    s = 1.0 - r * 0.18
    shapes.append(box((0, 13.95 + r * 0.42, 0.8), (4.9 * s, 0.45, 3.7 * s), 0.12, 2, 'Thatch', 'static', rot=(0, 0, 0)))
shapes.append(blob((0, 16.2, 0.8), (0.25, 0.3, 0.25), 'Thatch', 1, 0.15, 3))
for side in (-1, 1):
    shapes.append(sweep([(side * 2.6, 11.5, 0.0), (side * 2.6, 11.5, 4.1)], 0.03, 0.03, ref=(1, 0, 0), sides=6, mat='WoodDark', kind='static'))
    shapes.append(tube([(11.0, .045, .045), (11.9, .045, .045)], 'y', 6, mat='WoodDark', kind='static').moved((side * 2.6, 0, 4.1)))
for k in range(20):                                                                                     # rope ladder to the ground
    shapes.append(box((1.0, 1.4 + k * 0.47, 0.9), (0.7, 0.05, 0.05), 0.01, 1, 'Wood', 'static'))
shapes.append(sweep([(0.68, 1.4, 0.9), (0.68, 10.8, 0.9)], 0.015, 0.015, ref=(1, 0, 0), sides=5, mat='WoodDark', kind='static'))
shapes.append(sweep([(1.32, 1.4, 0.9), (1.32, 10.8, 0.9)], 0.015, 0.015, ref=(1, 0, 0), sides=5, mat='WoodDark', kind='static'))
part('Treehouse', shapes)

# ------------------------------------------------------------------ penguin
shapes = [ellipsoid((0, 0.5, 0), (0.28, 0.5, 0.24), 'Penguin_Black', 'static', 14, 9),
          ellipsoid((0, 0.46, 0.06), (0.21, 0.4, 0.2), 'Penguin_White', 'static', 14, 9),
          ellipsoid((0, 1.0, 0.03), (0.17, 0.17, 0.17), 'Penguin_Black', 'static', 12, 7),
          ellipsoid((0.08, 1.03, 0.14), (0.05, 0.05, 0.03), 'Penguin_White', 'static', 8, 5),
          ellipsoid((-0.08, 1.03, 0.14), (0.05, 0.05, 0.03), 'Penguin_White', 'static', 8, 5),
          ellipsoid((0.08, 1.03, 0.165), (0.022, 0.025, 0.015), 'Eye', 'static', 6, 4),
          ellipsoid((-0.08, 1.03, 0.165), (0.022, 0.025, 0.015), 'Eye', 'static', 6, 4),
          ellipsoid((0, 0.98, 0.2), (0.045, 0.03, 0.09), 'Penguin_Orange', 'static', 8, 5, rot=(-6, 0, 0))]
for s in (-1, 1):
    shapes.append(ellipsoid((s * 0.31, 0.55, 0), (0.05, 0.27, 0.13), 'Penguin_Black', 'static', 8, 6, rot=(0, 0, s * -16)))
    shapes.append(ellipsoid((s * 0.1, 0.03, 0.1), (0.1, 0.03, 0.15), 'Penguin_Orange', 'static', 8, 4))
part('Penguin', shapes)

# ------------------------------------------------------------------ ice: faceted chunks, radius 0.5 like the boulders
material('Ice', '#B3D3E5', 0.2, 0.0)
for i, (radii, seed) in enumerate([((0.5, 0.55, 0.45), 3), ((0.55, 0.42, 0.5), 8), ((0.42, 0.62, 0.42), 14)]):
    chunk = [blob((0, 0, 0), radii, 'Ice', 1, 0.34, seed, freq=1.2, flat_bottom=-0.55),
             blob((radii[0] * 0.55, -0.12, radii[2] * 0.4), (radii[0] * 0.5, radii[1] * 0.55, radii[2] * 0.5), 'Ice', 1, 0.3, seed + 5, flat_bottom=-0.5)]
    part('Ice_' + 'ABC'[i], chunk)

# ------------------------------------------------------------------ derrick: a lattice tower on a 6.4 m square, 32 m tall, light on top
material('DerrickRed', '#C2412F', 0.5, 0.3)
shapes = []
def leg(sx, sz):
    pts = [(sx * 3.2, 0.0, sz * 3.2), (sx * 0.8, 32.0, sz * 0.8)]
    shapes.append(sweep(pts, 0.17, 0.17, ref=(1, 0, 0), sides=6, mat='DerrickRed', kind='static'))
for sx in (-1, 1):
    for sz in (-1, 1):
        leg(sx, sz)
levels = [0.0, 6.4, 12.8, 19.2, 25.6, 32.0]
def at(y):
    k = 3.2 - (y / 32.0) * 2.4
    return k
for a, b in zip(levels, levels[1:]):
    ka, kb = at(a), at(b)
    for s in (-1, 1):
        shapes.append(sweep([(-kb, b, s * kb), (kb, b, s * kb)], 0.09, 0.09, ref=(0, 1, 0), sides=5, mat='DerrickRed', kind='static'))
        shapes.append(sweep([(s * kb, b, -kb), (s * kb, b, kb)], 0.09, 0.09, ref=(0, 1, 0), sides=5, mat='DerrickRed', kind='static'))
        # diagonals on the four faces
        shapes.append(sweep([(-ka, a, s * ka), (kb, b, s * kb)], 0.06, 0.06, ref=(0, 1, 0), sides=5, mat='Steel', kind='static'))
        shapes.append(sweep([(ka, a, s * ka), (-kb, b, s * kb)], 0.06, 0.06, ref=(0, 1, 0), sides=5, mat='Steel', kind='static'))
        shapes.append(sweep([(s * ka, a, -ka), (s * kb, b, kb)], 0.06, 0.06, ref=(0, 1, 0), sides=5, mat='Steel', kind='static'))
        shapes.append(sweep([(s * ka, a, ka), (s * kb, b, -kb)], 0.06, 0.06, ref=(0, 1, 0), sides=5, mat='Steel', kind='static'))
for y in (12.8, 25.6):                                                    # working platforms
    k = at(y) + 0.5
    shapes.append(box((0, y, 0), (k * 2, 0.15, k * 2), 0.03, 1, 'Steel', 'static'))
shapes.append(box((0, 32.5, 0), (2.0, 1.4, 2.0), 0.15, 2, 'DerrickRed', 'static'))                      # crown block
shapes.append(box((0, 34.0, 0), (0.7, 1.6, 0.7), 0.1, 1, 'Steel', 'static'))
shapes.append(ellipsoid((0, 33.2, 0), (0.7, 0.7, 0.7), 'Lamp', 'static', 10, 6))
part('Derrick', shapes)

# ------------------------------------------------------------------ warehouses: corrugated walls, roller doors, a low pitched roof
material('WallSteel', '#8A8F96', 0.5, 0.2)
material('RoofSteel', '#3D4249', 0.5, 0.3)
for width in (22, 16, 18):
    w = float(width)
    shapes = [box((0, 5.0, 0), (w, 10.0, 10.0), 0.05, 1, 'WallSteel', 'static')]
    k = 0
    x = -w / 2 + 0.3
    while x < w / 2 - 0.2:                                                 # corrugation on the front
        shapes.append(box((x, 5.0, 5.04), (0.14, 9.6, 0.06), 0.015, 1, 'WallSteel', 'static'))
        x += 0.42
    shapes.append(box((0, 0.35, 5.06), (w, 0.7, 0.1), 0.02, 1, 'Steel', 'static'))                        # concrete kerb
    shapes.append(box((0, 10.0, 0), (w + 0.4, 0.3, 10.4), 0.03, 1, 'Steel', 'static'))                   # eaves
    shapes.append(box((0, 10.7, -2.6), (w + 0.5, 0.18, 5.6), 0.03, 1, 'RoofSteel', 'static', rot=(14, 0, 0)))
    shapes.append(box((0, 10.7, 2.6), (w + 0.5, 0.18, 5.6), 0.03, 1, 'RoofSteel', 'static', rot=(-14, 0, 0)))
    for sx in (-1, 1):                                                     # closed gable ends
        gab = Shape([(sx * w / 2, 10.0, -5.0), (sx * w / 2, 10.0, 5.0), (sx * w / 2, 11.3, 0.0)], [(0, 1, 2), (2, 1, 0)], 'WallSteel', 'static')
        shapes.append(lib_shape := gab)
    for dx in (-w / 4, w / 4):                                             # roller doors
        shapes.append(box((dx, 2.5, 5.1), (4.0, 5.0, 0.1), 0.03, 1, 'Steel', 'static'))
        for r in range(9):
            shapes.append(box((dx, 0.5 + r * 0.55, 5.17), (3.9, 0.04, 0.05), 0.01, 1, 'WallSteel', 'static'))
    shapes.append(box((0, 11.2, 0), (w * 0.6, 0.5, 1.2), 0.1, 2, 'Steel', 'static'))                      # ridge vent
    part('Warehouse_' + str(width), shapes)

export_static(os.path.join(OUT, 'Structures.fbx'), models)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(lib.ROOT, 'Structures.blend'))

if '--no-preview' not in sys.argv:
    cam = lib.setup_render(900)

    def shot(name, objs, target, dist, views, spacing=0.0):
        for o in bpy.data.objects:
            if o.type == 'MESH':
                o.hide_render = o not in objs
        for i, o in enumerate(objs):
            o.location.x = -i * spacing
        lib.render_views(cam, name, target, dist, views)

    byname = {o.name: o for o in models}
    shot('struct_yacht', [byname['Yacht']], (0, 3, 0), 26, [('side', (1, 0.2, 0.3)), ('bow', (0.6, 0.35, 1))])
    shot('struct_tug', [byname['Tug']], (0, 2.8, 0), 22, [('side', (1, 0.2, 0.3)), ('bow', (0.6, 0.35, 1))])
    shot('struct_lighthouse', [byname['Lighthouse']], (0, 11, 0), 46, [('front', (0.3, 0.1, 1))])
    shot('struct_small', [byname['Gondola'], byname['Container'], byname['Penguin']], (-3.6, 1.0, 0), 20, [('front', (0.4, 0.4, 1))], spacing=5.0)
    shot('struct_treehouse', [byname['Treehouse']], (0, 8, 2), 36, [('front', (0.5, 0.2, 1))])
print('DONE', len(models))
