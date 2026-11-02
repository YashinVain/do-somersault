"""Location 1, the city swimming pool, as one scene (Docs/ModelBrief.md section 3.1). Output: Models/Locations/Loc_Pool.fbx + previews.
    blender -b --python Blender/lowpoly/loc_pool.py
Origin at the pool's edge; water at y = 0 (not modelled); the block is 64 x 64 m with a soil cut-away 6 m deep.
"""
import math
import os
import sys

import bpy
from mathutils import noise, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.normpath(os.path.join(HERE, '..')))
import locbuild  # noqa: E402
import view  # noqa: E402

OUT = os.path.normpath(os.path.join(HERE, '..', '..', 'Assets', '_Project', 'Models', 'Locations', 'Loc_Pool.fbx'))

COLOURS = {
    'Soil': '#8A6B4A', 'Lawn': '#79BC5A', 'Deck': '#D8C29E', 'Coping': '#F4F1EA', 'Tile': '#6CC4D4', 'PoolLine': '#1F4E9A',
    'Fence': '#E6D9C6', 'Concrete': '#E4DED2', 'PlatformCoral': '#EE6A55', 'Paint_White': '#F4F4F0', 'Metal': '#BFC6CE',
    'Springboard': '#2BA39C', 'ChairRed': '#D9503F', 'LoungerTeal': '#3DB5AD', 'LoungerWhite': '#F4F1EA', 'UmbrellaCoral': '#EE6A55',
    'UmbrellaYellow': '#F2C14E', 'Wood': '#7A5238', 'Leaves': '#4FA34A', 'LeavesDark': '#2F7D3C', 'Duck': '#FFD23F', 'DuckBeak': '#F28C28',
    'DeckLight': '#E0CBA6', 'Grout': '#CDB891', 'TileLine': '#8FD8E4', 'Steel': '#C9D1D8', 'Roof': '#B5543B', 'Plaster': '#F0E4CC', 'Glass': '#8FD3E8',
    'DoorWood': '#8A5A3B', 'Bush': '#3F8F45', 'FlowerPink': '#EE7FA8', 'FlowerYellow': '#FFD23F', 'FlowerWhite': '#FFFFFF', 'Ball': '#E8453C', 'Towel': '#F2C14E',
    'Bench': '#A9703F', 'Bin': '#4C6A7A', 'Lamp': '#FFE9A8',
    'LawnLight': '#8CCB67', 'LawnDark': '#68A94B', 'Stone': '#9A9A94', 'StoneDark': '#7C7C78', 'WoodLight': '#C99A62', 'Eye': '#1A1A1A',
    'DuckWing': '#F2B91E', 'Path': '#CDBB95', 'Gate': '#8A5A3B', 'Dirt': '#C9B48A', 'Tuft': '#5BAF4A', 'Moss': '#6E8F5A',
    'FlagCoral': '#EE6A55', 'FlagYellow': '#F2C14E', 'FlagTeal': '#2BA39C', 'FlagWhite': '#F4F1EA',
}

X0, X1, Z0, Z1 = -32, 32, -26, 38           # the block
PX0, PX1, PZ0, PZ1 = -5, 5, -3, 15           # the pool
DECK_TOP, LAWN_TOP = 0.35, 0.25


def rect_minus_pool(b, mat, x0, x1, z0, z1, y0, y1):
    """A slab over a rectangle with the pool's hole cut out of it (four boxes)."""
    def slab(a0, a1, c0, c1):
        if a1 - a0 > 1e-6 and c1 - c0 > 1e-6:
            b.box(mat, (a0 + a1) / 2, (y0 + y1) / 2, (c0 + c1) / 2, a1 - a0, y1 - y0, c1 - c0)
    slab(x0, PX0, z0, z1)
    slab(PX1, x1, z0, z1)
    slab(PX0, PX1, z0, max(z0, min(z1, PZ0)))
    slab(PX0, PX1, min(z1, max(z0, PZ1)), z1)


def hash01(i):
    return (math.sin(i * 12.9898) * 43758.5453) % 1.0


PADS = [(21.0, 4.0, 4.5, None), (-8.5, 25.5, 6.5, 0.27), (8.5, 25.5, 6.5, 0.27)]   # level places: the picnic table and the two cabins


def path_x(z):
    return 3.2 * math.sin((z - 20.0) * 0.22)


def ground_base(x, z):
    """The terrain height. Flat (0.27) inside the fence and round it, rolling hills and a valley beyond, back down to 0.27 at the edge."""
    dx = max(0.0, abs(x) - 15.0)
    dz = max(0.0, -10.0 - z, z - 23.0)
    d = math.hypot(dx, dz)
    t = min(1.0, d / 9.0)
    blend = t * t * (3 - 2 * t)
    edge = max(0.0, min(X1 - abs(x), z - Z0, Z1 - z))
    e = min(1.0, edge / 7.0)
    edge_f = e * e * (3 - 2 * e)
    n1 = noise.noise(Vector((x * 0.055, z * 0.055, 1.3))) * 0.5 + 0.5
    n2 = noise.noise(Vector((x * 0.16, z * 0.16, 7.1))) * 0.5 + 0.5
    n3 = noise.noise(Vector((x * 0.4, z * 0.4, 2.7))) * 0.5 + 0.5
    return 0.27 + blend * edge_f * (0.2 + 4.0 * n1 * n1 + 0.9 * n2 + 0.25 * n3)


def ground(x, z):
    h = ground_base(x, z)
    for px, pz, r, flat in PADS:
        d = math.hypot(x - px, z - pz)
        t = max(0.0, min(1.0, (r + 3.0 - d) / 3.0))
        t = t * t * (3 - 2 * t)
        hp = flat if flat is not None else ground_base(px, pz)
        h = h * (1 - t) + hp * t
    return h


def clear_of_things(x, z, margin=0.0):
    """Free of the fenced deck, the cabins, the picnic place and the gate path (so trees and bushes do not grow through them)."""
    if abs(x) < 15.5 + margin and -11.0 - margin < z < 23.5 + margin:
        return False
    for cx in (-8.5, 8.5):
        if abs(x - cx) < 5.2 + margin and 22.5 - margin < z < 29.5 + margin:
            return False
    if math.hypot(x - 21.0, z - 4.0) < 4.5 + margin:
        return False
    if z > 20 and abs(x - path_x(z)) < 2.2 + margin:
        return False
    return True


def tree(b, kind, x, z, s=1.0):
    y0 = ground(x, z) - 0.3
    if kind == 0:  # broad leafy tree: a forked trunk and a cluster of crowns
        b.cone('Wood', x, y0, z, 0.32 * s, 0.2 * s, 2.7 * s, 7)
        b.cone('Wood', x + 0.35 * s, y0 + 2.1 * s, z, 0.14 * s, 0.07 * s, 1.5 * s, 5)
        b.ico('Leaves', x, y0 + 4.3 * s, z, 2.1 * s, 0.85, sub=2)
        b.ico('LeavesDark', x + 1.3 * s, y0 + 3.7 * s, z + 0.4 * s, 1.4 * s, 0.85)
        b.ico('Leaves', x - 1.1 * s, y0 + 3.9 * s, z - 0.5 * s, 1.3 * s, 0.85)
        b.ico('Leaves', x + 0.2 * s, y0 + 5.3 * s, z - 0.3 * s, 1.2 * s, 0.9)
    elif kind == 1:  # pine
        b.cone('Wood', x, y0, z, 0.25 * s, 0.18 * s, 1.9 * s, 6)
        for i, (r, h) in enumerate(((2.2, 2.4), (1.75, 2.2), (1.3, 2.0), (0.85, 1.8))):
            b.cone('LeavesDark', x, y0 + (1.6 + i * 1.35) * s, z, r * s, 0.05, h * s, 8)
    else:  # tall slim tree
        b.cone('Wood', x, y0, z, 0.3 * s, 0.2 * s, 3.7 * s, 6)
        b.ico('Leaves', x, y0 + 5.7 * s, z, 1.6 * s, 1.55, sub=2)
        b.ico('LeavesDark', x + 0.5 * s, y0 + 4.5 * s, z + 0.3 * s, 1.1 * s, 1.1)


def bush(b, x, z, r=0.9, flowers=True):
    gy = ground(x, z)
    b.ico('Bush', x, gy + r * 0.45, z, r, 0.7, sub=1)
    b.ico('Bush', x + r * 0.7, gy + r * 0.3, z + r * 0.3, r * 0.65, 0.7)
    if flowers:
        for i, m in enumerate(('FlowerPink', 'FlowerYellow', 'FlowerWhite', 'FlowerPink')):
            a = i * 1.7
            b.ico(m, x + math.cos(a) * r * 0.75, gy + r * 0.75, z + math.sin(a) * r * 0.75, 0.13, 1.0, sub=0)


def rock(b, x, z, r, seed=0):
    gy = ground(x, z)
    m = 'Stone' if hash01(seed) > 0.4 else 'StoneDark'
    b.ico(m, x, gy + r * 0.25, z, r, 0.6, sub=1, sc=(1.0 + 0.4 * hash01(seed + 1), 1.0, 1.0 + 0.4 * hash01(seed + 2)))


def stump(b, x, z, r=0.4):
    gy = ground(x, z)
    b.cone('Wood', x, gy - 0.1, z, r * 1.15, r, 0.55, 7)
    b.cone('WoodLight', x, gy + 0.44, z, r * 0.98, r * 0.98, 0.04, 7)


def cabin(b, x, z, w=7.0, d=4.0, h=3.2):
    """A changing-room building with a pitched roof, door and windows (seen from the pool, so the doors face -Z)."""
    gy = ground(x, z)
    b.box('Plaster', x, gy + (h - 0.6) / 2, z, w, h + 0.6, d, bevel=0.03)
    for i in range(6):   # pitched roof: stacked slabs on both slopes
        t = i / 5
        b.box('Roof', x, gy + h + 0.12 + t * 1.0, z - (d / 2 + 0.3) * (1 - t), w + 0.7, 0.18, (d / 2 + 0.3) * (1 - t) * 0.9 + 0.2)
        b.box('Roof', x, gy + h + 0.12 + t * 1.0, z + (d / 2 + 0.3) * (1 - t), w + 0.7, 0.18, (d / 2 + 0.3) * (1 - t) * 0.9 + 0.2)
    for k in (-2.4, 0.0, 2.4):
        b.box('DoorWood', x + k, gy + 1.1, z - d / 2 - 0.03, 0.9, 2.2, 0.08)
    for k in (-1.2, 1.2):
        b.box('Glass', x + k, gy + 2.0, z - d / 2 - 0.03, 0.7, 0.6, 0.06)
        b.box('Paint_White', x + k, gy + 2.0, z - d / 2 - 0.05, 0.82, 0.72, 0.03)
    for k in (-3.2, 3.2):   # steps in front of the outer doors
        b.box('StoneDark', x + k * 0.5, gy + 0.05, z - d / 2 - 0.45, 2.6, 0.12, 0.7)


def duck(b, x, z, s=1.0, yaw=0.0, y=0.08):
    """A proper rubber duck floating on the water: egg body, head, flat beak, eyes, wings and a lifted tail."""
    c, sn = math.cos(yaw), math.sin(yaw)

    def P(dx, dy, dz):
        return (x + dx * c + dz * sn, y + dy, z - dx * sn + dz * c)
    b.ico('Duck', *P(0, 0.0, 0), 0.5 * s, 1.0, sub=2, sc=(0.85, 0.78, 1.18))
    b.ico('Duck', *P(0, 0.60 * s, 0.28 * s), 0.31 * s, 1.0, sub=1)
    b.ico('Duck', *P(0, 0.30 * s, -0.55 * s), 0.2 * s, 1.0, sub=1, sc=(0.7, 1.0, 0.8))
    b.box('DuckBeak', *P(0, 0.54 * s, 0.6 * s), 0.2 * s, 0.07 * s, 0.22 * s, bevel=0.02, rot_y=math.degrees(yaw))
    for k in (-1, 1):
        b.ico('Eye', *P(k * 0.14 * s, 0.68 * s, 0.5 * s), 0.045 * s, 1.0, sub=0)
        b.ico('DuckWing', *P(k * 0.4 * s, 0.1 * s, -0.02 * s), 0.4 * s, 1.0, sub=1, sc=(0.18, 0.55, 0.75))


def lamp(b, x, z):
    b.cone('Metal', x, DECK_TOP, z, 0.07, 0.05, 3.2, 6)
    b.box('Metal', x, DECK_TOP + 3.25, z, 0.5, 0.06, 0.06)
    b.ico('Lamp', x, DECK_TOP + 3.35, z, 0.2, 0.8, sub=1)


def bench(b, x, z, along_x=True):
    sx, sz = (2.0, 0.55) if along_x else (0.55, 2.0)
    b.box('Bench', x, DECK_TOP + 0.45, z, sx, 0.1, sz, bevel=0.02)
    b.box('Bench', x, DECK_TOP + 0.8, z + (0.26 if along_x else 0), sx if along_x else 0.1, 0.45 if along_x else 0.45, 0.1 if along_x else sz, bevel=0.02)
    for k in (-0.8, 0.8):
        b.box('Metal', x + (k if along_x else 0), DECK_TOP + 0.22, z + (0 if along_x else k), 0.08, 0.45, 0.08)

def build():
    b = locbuild.Builder()
    for k, v in COLOURS.items():
        b.mat(k, v)

    # --- the block: soil cut-away with the pool pit, lawn and deck on top
    rect_minus_pool(b, 'Soil', X0, X1, Z0, Z1, -6, 0)
    b.box('Soil', 0, -4.6, 6, 10, 2.8, 18)                                   # under the pool floor (-6 to -3.2)
    rect_minus_pool(b, 'Lawn', X0, X1, Z0, Z1, 0, LAWN_TOP)
    rect_minus_pool(b, 'Deck', -12, 12, -8, 20, 0, DECK_TOP)
    def lawn_mat(cx, cz, y):   # soft patches of lighter and darker grass, a dirt path from the gate, moss on the steep bits
        if cz > 20.5 and abs(cx - path_x(cz)) < 1.15:
            return 'Dirt'
        slope = abs(ground(cx + 1.0, cz) - ground(cx - 1.0, cz)) + abs(ground(cx, cz + 1.0) - ground(cx, cz - 1.0))
        n = noise.noise(Vector((cx * 0.11, cz * 0.11, 4.2)))
        if slope > 2.1:
            return 'Moss'
        return 'LawnLight' if (n > 0.18 or y > 2.6) else 'LawnDark' if n < -0.22 else 'Lawn'

    def jitter(x, z):          # the grid points move a little, except in the flat middle and at the block's edge
        if abs(x) <= 16.0 and -12.0 <= z <= 24.0 or x in (X0, X1) or z in (Z0, Z1):
            return 0.0, 0.0
        return (hash01(x * 31.7 + z * 7.3) - 0.5) * 0.7, (hash01(x * 12.9 + z * 53.1) - 0.5) * 0.7
    b.terrain(X0, X1, Z0, Z1, 1.0, ground, lawn_mat, skip=lambda cx, cz: PX0 <= cx <= PX1 and PZ0 <= cz <= PZ1, jitter=jitter)

    # --- paving: light slabs with a grout gap between them, none over the pool
    for ix in range(-6, 6):
        for iz in range(-4, 10):
            cx, cz = ix * 2 + 1, iz * 2 + 1
            if PX0 - 1.0 < cx < PX1 + 1.0 and PZ0 - 1.0 < cz < PZ1 + 1.0:
                continue
            if (ix + iz) % 2 == 0:
                b.box('DeckLight', cx, DECK_TOP + 0.01, cz, 1.88, 0.02, 1.88)
            else:
                b.box('Grout', cx, DECK_TOP + 0.008, cz, 1.88, 0.016, 1.88)

    # --- pool: tiled walls and floor, lane lines, white coping
    b.box('Tile', 0, -3.1, 6, 10, 0.2, 18)
    for wx in (PX0 + 0.1, PX1 - 0.1):   # the lining sits inside the pit, never in the same plane as the soil
        b.box('Tile', wx, (-3.0 + DECK_TOP) / 2, 6, 0.2, 3.0 + DECK_TOP, 18)
    for wz in (PZ0 + 0.1, PZ1 - 0.1):
        b.box('Tile', 0, (-3.0 + DECK_TOP) / 2, wz, 9.6, 3.0 + DECK_TOP, 0.2)
    for lx in (-3, 0, 3):
        b.box('PoolLine', lx, -2.98, 6, 0.18, 0.04, 17.4)
        b.box('PoolLine', lx, -2.98, 14.2, 0.9, 0.04, 0.18)
    for ty in (-2.5, -1.5, -0.5):                                                       # tile joints on the walls and the floor
        for wx in (PX0 + 0.21, PX1 - 0.21):
            b.box('TileLine', wx, ty, 6, 0.02, 0.05, 17.6)
        for wz in (PZ0 + 0.21, PZ1 - 0.21):
            b.box('TileLine', 0, ty, wz, 9.4, 0.05, 0.02)
    for fz in range(-1, 15, 2):
        b.box('TileLine', 0, -2.985, fz, 9.6, 0.02, 0.05)
    for lz in (3.0, 10.0):                                                              # steel pool ladders on the right wall
        for k in (-0.3, 0.3):
            b.box('Steel', PX1 + 0.45, DECK_TOP + 0.55, lz + k, 0.06, 1.1, 0.06)
            b.box('Steel', PX1 - 0.5, DECK_TOP + 1.05, lz + k, 1.0, 0.06, 0.06)
            b.box('Steel', PX1 - 0.95, -0.45, lz + k, 0.06, 2.9, 0.06)
        for ry in (-1.4, -0.9, -0.4):
            b.box('Steel', PX1 - 0.95, ry, lz, 0.05, 0.05, 0.6)
    c = 0.35
    b.box('Coping', 0, DECK_TOP + 0.035, PZ0 - c / 2, 10 + 2 * c, 0.07, c)
    b.box('Coping', 0, DECK_TOP + 0.035, PZ1 + c / 2, 10 + 2 * c, 0.07, c)
    b.box('Coping', PX0 - c / 2, DECK_TOP + 0.035, 6, c, 0.07, 18)
    b.box('Coping', PX1 + c / 2, DECK_TOP + 0.035, 6, c, 0.07, 18)

    # --- fence round the deck, 1.2 m, with a gap in front for the camera's view
    fy = DECK_TOP + 0.6
    b.box('Fence', 0, fy, -8, 24.3, 1.2, 0.18)
    for gx in (-1, 1):   # the back fence has a gate gap in the middle
        b.box('Fence', gx * 6.925, fy, 20, 10.85, 1.2, 0.18)
        b.box('Gate', gx * 1.3, fy + 0.4, 20, 0.28, 2.0, 0.28, bevel=0.03)
    for x in (-12, 12):
        b.box('Fence', x, fy, 6, 0.18, 1.2, 28.3)
    for x in range(-12, 13, 4):
        for z in (-8, 20):
            if z == 20 and abs(x) < 3:
                continue
            b.box('Fence', x, fy + 0.15, z, 0.3, 1.5, 0.3)

    b.box('Fence', 0, fy + 0.66, -8, 24.6, 0.1, 0.3, bevel=0.02)
    for gx in (-1, 1):
        b.box('Fence', gx * 6.925, fy + 0.66, 20, 10.85, 0.1, 0.3, bevel=0.02)
    for x in (-12, 12):
        b.box('Fence', x, fy + 0.66, 6, 0.3, 0.1, 28.6, bevel=0.02)

    # --- dive tower: three 1.4 m columns, coral platforms, rails, ladder
    cz = -4.1
    towers = [(-2.6, 7.5), (2.6, 5.0), (0.0, 10.0)]
    for x, h in towers:
        b.box('Concrete', x, DECK_TOP + (h - DECK_TOP) / 2, cz, 1.4, h - DECK_TOP, 1.4, bevel=0.03)
    b.box('Concrete', 0, 4.1, cz, 4.0, 0.3, 1.4)                          # a tie between the columns
    plats = [('Platform3', -2.6, 3.0, -0.9), ('Platform5', 2.6, 5.0, -0.4), ('Platform75', -2.6, 7.5, 0.0), ('Platform10', 0.0, 10.0, 0.3)]
    for name, x, h, sz in plats:
        edge = sz + 0.5
        zc, ln = (-4.8 + edge) / 2, edge + 4.8
        b.box('PlatformCoral', x, h - 0.125, zc, 2.0, 0.25, ln, bevel=0.02)
        b.box('Paint_White', x, h - 0.02, edge - 0.15, 2.0, 0.05, 0.3)       # the white stripe on the edge
        rail_end = edge - 1.0
        for sx in (-1, 1):                                                  # side rails: posts, a top rail and a middle rail
            for pz in (-4.75, (rail_end - 4.75) / 2, rail_end):
                b.box('Paint_White', x + sx * 0.95, h + 0.5, pz, 0.07, 1.0, 0.07)
            b.box('Paint_White', x + sx * 0.95, h + 1.0, (rail_end - 4.75) / 2, 0.08, 0.08, rail_end + 4.75)
            b.box('Paint_White', x + sx * 0.95, h + 0.55, (rail_end - 4.75) / 2, 0.05, 0.05, rail_end + 4.75)
        for px in (-0.95, 0.0, 0.95):                                       # back rail
            b.box('Paint_White', x + px, h + 0.5, -4.75, 0.07, 1.0, 0.07)
        b.box('Paint_White', x, h + 1.0, -4.75, 2.0, 0.08, 0.08)
        b.box('Paint_White', x, h + 0.55, -4.75, 2.0, 0.05, 0.05)
        b.box('Paint_White', x, h - 0.45, -2.2, 0.12, 0.5, 4.6)             # a beam under the slab
        b.spot(f'Spot_{name}', x, h, sz)
    for sx in (-1, 1):                                                     # ladder on the back of the centre column
        b.box('Metal', 0.35 * sx, 5.2, cz - 0.82, 0.07, 9.7, 0.07)
    for i in range(24):
        b.box('Metal', 0, 0.8 + i * 0.4, cz - 0.82, 0.7, 0.05, 0.05)

    # --- ground-level jump places
    b.box('PlatformCoral', 3.9, (DECK_TOP + 0.8) / 2, -3.0, 0.7, 0.8 - DECK_TOP, 0.6, bevel=0.03)        # start block
    b.spot('Spot_StartBlock', 3.9, 0.8, -3)
    b.box('Springboard', -6.0, 0.97, 6, 3.4, 0.06, 0.8, bevel=0.02)                                    # springboard, left
    b.box('Paint_White', -7.6, (DECK_TOP + 0.9) / 2, 6, 0.9, 0.9 - DECK_TOP, 1.0, bevel=0.03)
    b.spot('Spot_Springboard', -4.3, 1.0, 6)
    ch, sx0 = 2.2, 6.4                                                                                 # lifeguard chair, right
    for lx in (sx0 - 0.6, sx0 + 0.6):
        for lz in (11.5, 12.5):
            b.box('ChairRed', lx, (DECK_TOP + ch) / 2, lz, 0.14, ch - DECK_TOP, 0.14)
    b.box('ChairRed', sx0, ch + 0.08, 12, 1.5, 0.16, 1.3, bevel=0.02)
    b.box('ChairRed', sx0 + 0.7, ch + 0.8, 12, 0.14, 1.3, 1.3, bevel=0.02)
    for lz in (11.5, 12.5):
        b.box('Metal', sx0 + 0.65, ch + 2.2, lz, 0.08, 1.7, 0.08)
    b.box('ChairRed', sx0, ch + 3.1, 12, 2.2, 0.15, 1.9, bevel=0.03)
    b.spot('Spot_LifeguardChair', 5.5, 2.36, 12)

    # --- decor: loungers, umbrellas, bunting, duck, trees
    for i, x in enumerate((-9.5, -7.0, 7.0, 9.5)):
        m = 'LoungerTeal' if i % 2 == 0 else 'LoungerWhite'
        b.box(m, x, DECK_TOP + 0.25, 17.0, 0.75, 0.2, 1.9, bevel=0.02)
        b.box(m, x, DECK_TOP + 0.55, 18.1, 0.75, 0.2, 0.7, bevel=0.02, rot_y=0)
        for lx in (-0.3, 0.3):
            b.box('Metal', x + lx, DECK_TOP + 0.12, 16.3, 0.06, 0.24, 0.06)
            b.box('Metal', x + lx, DECK_TOP + 0.12, 17.7, 0.06, 0.24, 0.06)
    for x, m in ((-10.2, 'UmbrellaCoral'), (-3.5, 'UmbrellaYellow'), (4.5, 'UmbrellaYellow'), (10.2, 'UmbrellaCoral')):
        b.box('Metal', x, DECK_TOP + 1.2, 16.6, 0.06, 2.4, 0.06)
        b.cone(m, x, DECK_TOP + 2.3, 16.6, 1.35, 0.05, 0.55, 8)
    for mx in (-8.0, 8.0):                                                                              # bunting across the pool
        b.box('Metal', mx, DECK_TOP + 2.0, 17, 0.12, 4.0, 0.12)
    n = 11
    for i in range(n):
        t0, t1 = i / n, (i + 1) / n
        x0, x1 = -8.0 + 16 * t0, -8.0 + 16 * t1
        y0 = 4.2 - 1.3 * math.sin(math.pi * t0)
        y1 = 4.2 - 1.3 * math.sin(math.pi * t1)
        mat = ('FlagCoral', 'FlagYellow', 'FlagTeal', 'FlagWhite')[i % 4]
        b.tri(mat, (x0 + 0.05, y0, 17), (x1 - 0.05, y1, 17), ((x0 + x1) / 2, (y0 + y1) / 2 - 0.9, 17))
    duck(b, -2.2, 8.0, 1.2, yaw=0.35)                                                                   # a mother duck and her ducklings
    duck(b, -3.3, 6.7, 0.55, yaw=0.4)
    duck(b, -4.0, 7.5, 0.5, yaw=0.2)
    duck(b, -3.0, 5.7, 0.5, yaw=0.5)
    duck(b, 3.3, 3.2, 0.9, yaw=-0.9)                                                                    # and one on its own
    for x, hgt in ((-2.6, 7.5), (2.6, 5.0), (0.0, 10.0)):                               # height plates on the columns
        b.box('Paint_White', x, hgt - 1.2, -3.37, 0.7, 0.45, 0.04)
        b.box('PlatformCoral', x, hgt - 1.2, -3.35, 0.5, 0.25, 0.02)
    for cxx in (-8.5, 8.5):                                                              # changing rooms behind the fence
        cabin(b, cxx, 25.5)
    for bx, bz, ax in ((-10.6, 3.0, False), (-10.6, 11.0, False), (10.6, 3.0, False), (10.6, 8.0, False), (4.5, 18.9, True)):
        bench(b, bx, bz, ax)
    for lx, lz in ((-11.2, -6.8), (11.2, -6.8), (-11.2, 18.8), (11.2, 18.8)):
        lamp(b, lx, lz)
    b.ico('Ball', -8.2, DECK_TOP + 0.3, 11.5, 0.3, 1.0)                                  # a beach ball and some towels
    b.ico('Paint_White', -8.2, DECK_TOP + 0.3, 11.5, 0.31, 1.0, sub=0)
    for tx, tz, tm in ((-9.5, 17.0, 'Towel'), (7.0, 17.0, 'FlowerPink'), (-4.0, 14.8, 'Towel')):
        b.box(tm, tx, DECK_TOP + 0.37, tz, 0.55, 0.04, 1.2)
    # --- a picnic place on its own level pad
    px, pz = 21.0, 4.0
    gy = ground(px, pz)
    b.box('Bench', px, gy + 0.8, pz, 2.4, 0.09, 1.0, bevel=0.02)
    for k in (-1.0, 1.0):
        b.box('Bench', px + k, gy + 0.35, pz, 0.09, 0.9, 0.9)
        for off in (0.85, -0.85):
            b.box('Bench', px, gy + 0.42, pz + off, 2.4, 0.08, 0.4, bevel=0.02)
            b.box('Metal', px + k * 1.1, gy + 0.2, pz + off, 0.07, 0.65, 0.07)
    b.box('Metal', px, gy + 1.55, pz, 0.06, 1.6, 0.06)
    b.cone('UmbrellaYellow', px, gy + 2.3, pz, 1.5, 0.05, 0.6, 8)

    # --- bushes in loose clusters (not rows), more at the groves' edges
    k = 0
    for i in range(70):
        x = -28 + 56 * hash01(i * 3.1 + 1)
        z = -22 + 60 * hash01(i * 5.3 + 2)
        if not clear_of_things(x, z, 1.0) or (z < -12 and abs(x) < 4):
            continue
        for j in range(2 + int(hash01(i + 9) * 3)):
            bx_, bz_ = x + (hash01(i * 7 + j) - 0.5) * 2.4, z + (hash01(i * 11 + j) - 0.5) * 2.4
            if clear_of_things(bx_, bz_, 0.5):
                bush(b, bx_, bz_, 0.45 + 0.7 * hash01(i * 13 + j), flowers=hash01(i * 17 + j) > 0.45)
                k += 1
    # round the fence, a few close bushes at uneven spacing
    for i in range(34):
        side = i % 4
        t = hash01(i * 9.7 + 3)
        if side == 0:
            x, z = -12.8 - 1.0 * hash01(i), -8 + 28 * t
        elif side == 1:
            x, z = 12.8 + 1.0 * hash01(i), -8 + 28 * t
        elif side == 2:
            x, z = -12 + 24 * t, -9.3 - 1.0 * hash01(i)
        else:
            x, z = -12 + 24 * t, 21.3 + 0.8 * hash01(i)
            if abs(x - path_x(z)) < 2.2:
                continue
        bush(b, x, z, 0.5 + 0.5 * hash01(i + 40), flowers=hash01(i + 2) > 0.5)

    # --- grass tufts, rocks, stumps, fallen logs and flower meadows across the meadow
    for i in range(260):
        x = -30 + 60 * hash01(i * 2.3 + 5)
        z = -24 + 62 * hash01(i * 4.1 + 6)
        if not clear_of_things(x, z, 0.4):
            continue
        gy = ground(x, z)
        for j in range(3):
            a_ = j * 2.1 + hash01(i) * 3
            b.cone('Tuft', x + math.cos(a_) * 0.12, gy - 0.02, z + math.sin(a_) * 0.12, 0.11, 0.015, 0.4 + 0.3 * hash01(i + j), 4)
    n = 0
    for i in range(22):
        x, z = -29 + 58 * hash01(i * 6.7 + 8), -22 + 58 * hash01(i * 3.3 + 9)
        if not clear_of_things(x, z, 1.0):
            continue
        for j in range(1 + int(hash01(i) * 3)):
            rock(b, x + 1.4 * j * (hash01(i + j) - 0.5), z + 1.2 * j * (hash01(i * 3 + j) - 0.5), 0.5 + 1.1 * hash01(i + j * 5), n)
            n += 1
    for i in range(9):
        x, z = -29 + 58 * hash01(i * 8.1 + 11), -20 + 56 * hash01(i * 2.9 + 12)
        if not clear_of_things(x, z, 1.5):
            continue
        if i % 2 == 0:
            stump(b, x, z, 0.35 + 0.15 * hash01(i))
        else:
            gy = ground(x, z)
            b.box('Wood', x, gy + 0.18, z, 2.4 + hash01(i), 0.42, 0.42, bevel=0.04, rot_y=360 * hash01(i + 4))
            b.box('WoodLight', x, gy + 0.18, z, 0.06, 0.34, 0.34, rot_y=0)
    for i in range(10):
        fx, fz = -28 + 56 * hash01(i * 5.9 + 14), -20 + 56 * hash01(i * 7.7 + 15)
        if not clear_of_things(fx, fz, 1.0):
            continue
        for j in range(10):
            a_, rr = j * 2.4, 0.4 + 1.8 * hash01(i * 10 + j)
            b.ico(('FlowerPink', 'FlowerYellow', 'FlowerWhite')[j % 3], fx + math.cos(a_) * rr, ground(fx, fz) + 0.15, fz + math.sin(a_) * rr, 0.13, 1.0, sub=0)

    # --- the forest in groves of different size and density, with clearings between; only low things in front of the camera
    GROVES = [(-25, 34, 9.5, 17), (-8, 38, 6, 9), (10, 37, 7, 11), (26, 33, 9, 15), (-31, 16, 6, 8), (30, 19, 7, 9), (-30, -6, 6, 6), (31, -4, 6, 7),
              (0, 31, 3.5, 3), (-19, 26, 4, 4), (19, 30, 4, 5)]
    n = 0
    for cx, cz, rad, cnt in GROVES:
        for i in range(cnt):
            ang, rr = hash01(n * 1.7 + 3) * 6.283, rad * math.sqrt(hash01(n * 2.9 + 4))
            x, z = cx + math.cos(ang) * rr, cz + math.sin(ang) * rr
            n += 1
            if not clear_of_things(x, z, 0.8) or abs(x) > 31 or z > 38 or z < -23:
                continue
            u = hash01(n * 5.3 + 7)
            kind = 1 if u < 0.45 else (0 if u < 0.8 else 2)
            tree(b, kind, x, z, 0.75 + 0.75 * hash01(n * 3.7 + 8))
    for i in range(16):   # a few lone trees in the clearings (never tall near the sides of the pool)
        x, z = -30 + 60 * hash01(i * 9.1 + 20), -10 + 48 * hash01(i * 4.7 + 21)
        if not clear_of_things(x, z, 2.5) or (abs(x) < 26 and z < 26):
            continue
        tree(b, int(hash01(i + 30) * 3), x, z, 0.75 + 0.5 * hash01(i + 31))
    return b


def main():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    b = build()
    objs = b.finish('Loc_Pool')
    print('LOCATION TRIS', b.tris, 'objects', len(objs), 'materials', sorted(b.bms))
    b.export(OUT)
    # review pictures: the map's overview, the view from the high platform, and the camera's side view
    view.shoot('loc_pool_overview', (0, 0, 6), 75, (0.55, 0.75, -0.9), lens=50, res=(1100, 800))
    view.shoot('loc_pool_side', (0, 4, 6), 48, (1, 0.25, 0.05), lens=50, res=(1100, 700))
    view.shoot('loc_pool_back', (0, 5, 4), 34, (-0.35, 0.3, -1), lens=50, res=(1100, 700))
    print('LOC DONE')


if __name__ == '__main__':
    main()
