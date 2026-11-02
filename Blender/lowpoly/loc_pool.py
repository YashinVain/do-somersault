"""Location 1, the city swimming pool, as one scene (Docs/ModelBrief.md section 3.1). Output: Models/Locations/Loc_Pool.fbx + previews.
    blender -b --python Blender/lowpoly/loc_pool.py
Origin at the pool's edge; water at y = 0 (not modelled); the block is 64 x 64 m with a soil cut-away 6 m deep.
"""
import math
import os
import sys

import bpy

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


def tree(b, kind, x, z, s=1.0):
    if kind == 0:  # broad leafy tree: a forked trunk and a cluster of crowns
        b.cone('Wood', x, 0, z, 0.32 * s, 0.2 * s, 2.4 * s, 7)
        b.cone('Wood', x + 0.35 * s, 1.8 * s, z, 0.14 * s, 0.07 * s, 1.5 * s, 5)
        b.ico('Leaves', x, 4.0 * s, z, 2.1 * s, 0.85, sub=2)
        b.ico('LeavesDark', x + 1.3 * s, 3.4 * s, z + 0.4 * s, 1.4 * s, 0.85)
        b.ico('Leaves', x - 1.1 * s, 3.6 * s, z - 0.5 * s, 1.3 * s, 0.85)
        b.ico('Leaves', x + 0.2 * s, 5.0 * s, z - 0.3 * s, 1.2 * s, 0.9)
    elif kind == 1:  # pine
        b.cone('Wood', x, 0, z, 0.25 * s, 0.18 * s, 1.6 * s, 6)
        for i, (r, h) in enumerate(((2.2, 2.4), (1.75, 2.2), (1.3, 2.0), (0.85, 1.8))):
            b.cone('LeavesDark', x, (1.3 + i * 1.35) * s, z, r * s, 0.05, h * s, 8)
    else:  # tall slim tree
        b.cone('Wood', x, 0, z, 0.3 * s, 0.2 * s, 3.4 * s, 6)
        b.ico('Leaves', x, 5.4 * s, z, 1.6 * s, 1.55, sub=2)
        b.ico('LeavesDark', x + 0.5 * s, 4.2 * s, z + 0.3 * s, 1.1 * s, 1.1)


def bush(b, x, z, r=0.9, flowers=True):
    b.ico('Bush', x, r * 0.55, z, r, 0.7, sub=1)
    b.ico('Bush', x + r * 0.7, r * 0.4, z + r * 0.3, r * 0.65, 0.7)
    if flowers:
        for i, m in enumerate(('FlowerPink', 'FlowerYellow', 'FlowerWhite', 'FlowerPink')):
            a = i * 1.7
            b.ico(m, x + math.cos(a) * r * 0.75, r * 0.85, z + math.sin(a) * r * 0.75, 0.13, 1.0, sub=0)


def cabin(b, x, z, w=7.0, d=4.0, h=3.2):
    """A changing-room building with a pitched roof, door and windows (seen from the pool, so the doors face -Z)."""
    b.box('Plaster', x, h / 2, z, w, h, d, bevel=0.03)
    # pitched roof: two sloped slabs built from stacked boxes
    for i in range(6):
        t = i / 5
        b.box('Roof', x, h + 0.12 + t * 1.0, z - (d / 2 + 0.3) * (1 - t) + 0.0, w + 0.7, 0.18, (d / 2 + 0.3) * (1 - t) * 0.9 + 0.2)
        b.box('Roof', x, h + 0.12 + t * 1.0, z + (d / 2 + 0.3) * (1 - t), w + 0.7, 0.18, (d / 2 + 0.3) * (1 - t) * 0.9 + 0.2)
    for k in (-2.4, 0.0, 2.4):
        b.box('DoorWood', x + k, 1.1, z - d / 2 - 0.03, 0.9, 2.2, 0.08)
    for k in (-1.2, 1.2):
        b.box('Glass', x + k, 2.0, z - d / 2 - 0.03, 0.7, 0.6, 0.06)
        b.box('Paint_White', x + k, 2.0, z - d / 2 - 0.05, 0.82, 0.72, 0.03)


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
    for cx, cz in ((-27, -20), (27, -20), (-27, 33), (27, 33), (-22, 8), (22, 8)):   # soft hills
        b.ico('Lawn', cx, 0.1, cz, 5.5, 0.28, sub=2)

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
    for z in (-8, 20):
        b.box('Fence', 0, fy, z, 24.3, 1.2, 0.18)
    for x in (-12, 12):
        b.box('Fence', x, fy, 6, 0.18, 1.2, 28.3)
    for x in range(-12, 13, 4):
        for z in (-8, 20):
            b.box('Fence', x, fy + 0.15, z, 0.3, 1.5, 0.3)

    for z in (-8, 20):                                                                  # a cap rail along the fence
        b.box('Fence', 0, fy + 0.66, z, 24.6, 0.1, 0.3, bevel=0.02)
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
    for x, m in ((-10.5, 'UmbrellaCoral'), (0.0, 'UmbrellaYellow'), (10.5, 'UmbrellaCoral')):
        b.box('Metal', x, DECK_TOP + 1.2, 18.8, 0.06, 2.4, 0.06)
        b.cone(m, x, DECK_TOP + 2.3, 18.8, 1.5, 0.05, 0.6, 8)
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
    b.ico('Duck', -2.2, 0.15, 7.5, 0.55, 0.7)                                                           # inflatable duck ring
    b.ico('Duck', -2.2, 0.75, 8.0, 0.3, 1.0)
    b.cone('DuckBeak', -2.2, 0.72, 8.38, 0.1, 0.03, 0.2, 5)
    for x, hgt in ((-2.6, 7.5), (2.6, 5.0), (0.0, 10.0)):                               # height plates on the columns
        b.box('Paint_White', x, hgt - 1.2, -3.37, 0.7, 0.45, 0.04)
        b.box('PlatformCoral', x, hgt - 1.2, -3.35, 0.5, 0.25, 0.02)
    for cxx in (-8.5, 8.5):                                                              # changing rooms behind the fence
        cabin(b, cxx, 25.5)
    for bx, bz, ax in ((-10.6, 3.0, False), (-10.6, 11.0, False), (10.6, 3.0, False), (10.6, 8.0, False), (0.0, 18.8, True)):
        bench(b, bx, bz, ax)
    for lx, lz in ((-11.2, -6.8), (11.2, -6.8), (-11.2, 18.8), (11.2, 18.8)):
        lamp(b, lx, lz)
    for bx in range(-12, 13, 3):
        bush(b, bx, -9.6, 0.8)
        bush(b, bx + 1.2, 21.7, 0.8)
    for bz in range(-6, 20, 4):
        bush(b, -13.5, bz, 0.75)
        bush(b, 13.5, bz + 1, 0.75, flowers=False)
    b.ico('Ball', -8.2, DECK_TOP + 0.3, 11.5, 0.3, 1.0)                                  # a beach ball and some towels
    b.ico('Paint_White', -8.2, DECK_TOP + 0.3, 11.5, 0.31, 1.0, sub=0)
    for tx, tz, tm in ((-9.5, 17.0, 'Towel'), (7.0, 17.0, 'FlowerPink'), (-4.0, 14.8, 'Towel')):
        b.box(tm, tx, DECK_TOP + 0.37, tz, 0.55, 0.04, 1.2)
    kinds = 0
    for x in range(-28, 29, 8):
        tree(b, kinds % 3, x + (kinds % 2) * 2, 34 - (kinds % 2) * 3, 0.9 + 0.15 * (kinds % 3))
        kinds += 1
    for z in (-18, -8, 2, 12, 22, 30):
        tree(b, kinds % 3, -29 + (kinds % 2) * 2, z, 1.0)
        tree(b, (kinds + 1) % 3, 29 - (kinds % 2) * 2, z + 3, 1.0)
        kinds += 1
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
