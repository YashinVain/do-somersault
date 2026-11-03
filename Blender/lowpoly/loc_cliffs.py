"""Location 3, the cliffs by the sea, as one scene (Docs/ModelBrief.md section 3.3). Output: Models/Locations/Loc_Cliffs.fbx + previews.
    blender -b --python Blender/lowpoly/loc_cliffs.py
Site space like the game's: the block is 90 x 90 m, x -45..45, z -30..60, water at y = 0 (not modelled), sea floor at -6. The cliff wall is the very
same faceted face the game's collision boxes were fitted to (data/cliff_wall.txt, dumped by PrototypeSceneBuilder.DumpCliffWall); the ledges, the house,
the lighthouse, the two sea stacks and the yacht sit where the game's solid boxes are, and only add looks on top.
"""
import bisect
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.normpath(os.path.join(HERE, '..')))
import locbuild  # noqa: E402
import view  # noqa: E402

OUT = os.path.normpath(os.path.join(HERE, '..', '..', 'Assets', '_Project', 'Models', 'Locations', 'Loc_Cliffs.fbx'))
DATA = os.path.join(HERE, 'data', 'cliff_wall.txt')

COLOURS = {
    'Soil': '#5A4330', 'Earth': '#7A5A3E', 'BlockRock': '#5E5A55',
    'RockA': '#C48A56', 'RockB': '#A9713F', 'RockC': '#8E5E36', 'RockPale': '#D8A672', 'RockTop': '#DDB784', 'RockWet': '#6F5238', 'RockWetDark': '#48573F',
    'RockCrack': '#5C3B23', 'RockGrey': '#8B7F72', 'Cave': '#3B2A20',
    'Grass': '#7FAE5A', 'GrassDark': '#5E9A4E', 'GrassLight': '#98C264', 'GrassDry': '#B9B86A', 'Dirt': '#C9A56F', 'Tuft': '#5BAF4A',
    'FlowerWhite': '#FFFFFF', 'FlowerYellow': '#FFD23F', 'FlowerPink': '#F08FA8', 'Vine': '#4F8444',
    'Sand': '#E3CB98', 'SandWet': '#B9A476', 'SandDark': '#CDB680', 'Seabed': '#D9C592', 'SeabedDark': '#B9A878',
    'Kelp': '#4E7F4A', 'Coral': '#E8795E', 'Shell': '#F0E4D0', 'Starfish': '#E58A4E', 'Barnacle': '#C8C3B4', 'Driftwood': '#B79C78',
    'Lime': '#F2EEE4', 'Roof': '#C8643E', 'Shutter': '#3E7CA8', 'Door': '#5B3A28', 'Glass': '#24384A', 'Terracotta': '#C4623F', 'Wood': '#9C6B43',
    'WoodDark': '#6E4A2F', 'Stone': '#B3A58F', 'StoneDark': '#8F8271', 'Rope': '#D9C9A0', 'Net': '#5E7A86', 'Metal': '#8A929A', 'Lantern': '#FFD98A',
    'LHWhite': '#F4F2EC', 'LHRed': '#C9413A', 'LHLamp': '#FFE7A3', 'LHGlass': '#9CC3CF', 'LHDark': '#3A3F47',
    'Hull': '#F4F3EE', 'Stripe': '#1F4E79', 'Teak': '#B88A5A', 'YachtGlass': '#24384A', 'Chrome': '#C9D1D8', 'Fender': '#E04A3A',
    'Gull': '#F4F4F0',
    'Cypress': '#2F5E3C', 'CypressDark': '#26503A', 'Olive': '#8A9A6B', 'OliveDark': '#6B7D55', 'PineUmb': '#3F7A45', 'Bark': '#7E5238',
    'Lavender': '#8E6FB5', 'LavenderDark': '#6F5594', 'Poppy': '#E2503C', 'Agave': '#6FA58A', 'Furrow': '#8E6A44',
}

COLS, ROWS = 24, 9
HALF = 45.0
ZBACK = -30.0
TOP = 22.0
LEFT_BEACH, RIGHT_BEACH = (-45.0, -31.0), (34.0, 45.0)
CRABS = ((36.0, 2.5), (41.0, 1.5), (-36.0, 1.5), (-41.0, 2.5))


def hash01(i):
    return (math.sin(i * 12.9898) * 43758.5453) % 1.0


def face_z(x):   # the same in C#: PrototypeSceneBuilder.CliffFaceZ
    return -3.0 + 1.2 * math.sin(x * 0.15)


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


# ---------------------------------------------------------------- the wall the physics was fitted to
def load_wall():
    pts = [tuple(float(v) for v in line.split()) for line in open(DATA) if line.strip()]
    grid = {}
    for j in range(ROWS):
        for i in range(COLS):
            base = (j * COLS + i) * 6
            grid[(i + 1, j)], grid[(i + 1, j + 1)], grid[(i, j + 1)], grid[(i, j)] = pts[base], pts[base + 1], pts[base + 2], pts[base + 5]
    cap = []
    for i in range(COLS):
        base = COLS * ROWS * 6 + i * 6
        cap.append((pts[base], pts[base + 1], pts[base + 2], pts[base + 5]))   # b0, f0, f1, b1
    return grid, cap


WALL, CAP = load_wall()
WALL_NODES = list(WALL.values())


def wall_z(x, y):
    """Where the rock face stands at x, y (nearest corners of the wall's grid, weighted)."""
    near = sorted(WALL_NODES, key=lambda p: (p[0] - x) ** 2 + ((p[1] - y) * 1.2) ** 2)[:4]
    ws = [1.0 / (0.05 + math.hypot(p[0] - x, (p[1] - y) * 1.2)) for p in near]
    return sum(p[2] * w for p, w in zip(near, ws)) / sum(ws) + relief(x, y)


# ---------------------------------------------------------------- small things
def tuft(b, x, y, z, s=1.0, mat='Tuft', n=4, seed=0):
    for k in range(n):
        a = k * 2.2 + hash01(seed + k) * 3
        r = 0.09 * s
        b.limb(mat, (x + math.cos(a) * r, y - 0.02, z + math.sin(a) * r),
               (x + math.cos(a) * r * 3.0, y + (0.45 + 0.25 * hash01(seed * 3 + k)) * s, z + math.sin(a) * r * 3.0), 0.045 * s, 0.01, 3)


def flowers(b, x, y, z, seed=0, n=3, spread=0.5):
    for k in range(n):
        a = hash01(seed * 5 + k) * 6.28
        d = hash01(seed * 7 + k) * spread
        fx, fz = x + math.cos(a) * d, z + math.sin(a) * d
        b.limb('Tuft', (fx, y, fz), (fx, y + 0.28, fz), 0.015, 0.012, 3)
        b.ico(('FlowerWhite', 'FlowerYellow', 'FlowerPink')[(seed + k) % 3], fx, y + 0.3, fz, 0.07, 0.8, sub=0)


def bush(b, x, y, z, s=1.0, seed=0, mat='GrassDark'):
    for k in range(3):
        a = k * 2.1 + hash01(seed) * 3
        b.ico(mat if k else 'Grass', x + math.cos(a) * 0.35 * s, y + 0.3 * s, z + math.sin(a) * 0.35 * s, 0.5 * s * (0.8 + 0.3 * hash01(seed + k)), 0.75, sub=1,
              jit=0.12, seed=seed + k)
    flowers(b, x, y + 0.5 * s, z, seed, 2, 0.5 * s)


def boulder(b, mat, x, y, z, r, sc=(1.0, 0.7, 1.0), seed=0):
    b.ico(mat, x, y, z, r, 1.0, sub=1, sc=sc, jit=0.16, seed=seed)


def weather_rock(b, x, y, z, r, seed=0, wet=False):
    """A boulder with a lighter top and a darker foot, half sunk."""
    base = 'RockWet' if wet else ('RockB' if seed % 2 else 'RockC')
    boulder(b, base, x, y, z, r, (1.0, 0.7, 1.1), seed)
    if not wet:
        boulder(b, 'RockA' if seed % 3 else 'RockPale', x + 0.05 * r, y + r * 0.32, z, r * 0.7, (1.0, 0.45, 1.0), seed + 4)


# ---------------------------------------------------------------- the block
def block_base(b):
    top = -6.1
    b.box('Soil', 0, top - 0.7, 15, 90, 1.4, 90)
    b.box('Earth', 0, (top - 1.4 - 9.0) / 2, 15, 90, (top - 1.4) - (-9.0), 90)
    b.box('BlockRock', 0, (-9.0 - 16.0) / 2, 15, 90, 7.0, 90)
    b.box('Seabed', 0, -6.05, 15, 90, 0.1, 90)
    # sand ripples and bars on the floor, shells, kelp and coral
    for k in range(34):
        x, z = -42 + 84 * hash01(k * 3.7 + 1), -2 + 58 * hash01(k * 6.1 + 2)
        if z < 4:
            continue
        b.ico('SeabedDark', x, -6.0, z, 1.0, 1.0, sub=1, sc=(2.0 + 3 * hash01(k + 9), 0.07, 0.7 + 0.5 * hash01(k + 4)), jit=0.1, seed=k)
    for k in range(26):
        x, z = -40 + 80 * hash01(k * 2.9 + 11), 3 + 52 * hash01(k * 5.3 + 12)
        weather_rock(b, x, -6.0, z, 0.5 + 0.6 * hash01(k + 3), seed=k + 30, wet=True)
        if k % 2:
            for j in range(4):
                a = hash01(k * 4 + j) * 6.28
                px, pz = x + math.cos(a) * 0.8, z + math.sin(a) * 0.8
                top_y = -6.0 + 1.0 + 1.8 * hash01(k + j)
                b.limb('Kelp', (px, -6.0, pz), (px + 0.3 * math.sin(k + j), top_y, pz + 0.3 * math.cos(k * j)), 0.05, 0.015, 3)
        else:
            for j in range(3):
                a = hash01(k * 9 + j) * 6.28
                b.cone('Coral', x + math.cos(a) * 0.7, -6.0, z + math.sin(a) * 0.7, 0.16, 0.04, 0.5 + 0.4 * hash01(j + k), 5)
    for k in range(40):
        x, z = -40 + 80 * hash01(k * 1.3 + 21), 4 + 50 * hash01(k * 8.3 + 22)
        if k % 3 == 0:
            b.cone('Starfish', x, -6.0, z, 0.3, 0.05, 0.05, 5)
        else:
            b.ico('Shell', x, -5.97, z, 0.12, 0.6, sub=0)


# ---------------------------------------------------------------- the cliff wall
def strata():
    levels = [-6.0]
    k = 0
    while levels[-1] < 24:
        levels.append(levels[-1] + 0.9 + 1.5 * hash01(k * 7.1 + 3))
        k += 1
    return levels


STRATA = strata()
PATTERN = ['RockA', 'RockB', 'RockA', 'RockC', 'RockPale', 'RockB', 'RockA', 'RockC', 'RockPale', 'RockA']


# Relief: hard layers stand out of the face, outcrops swell it like pillows, and a skirt of rubble spreads at the foot. The collision boxes sit
# 0.25 m behind the grid's face, so the relief never goes deeper than 0.15 m behind it.
BUMPS = [(-44 + 88 * hash01(k * 2.3 + 5), 1.5 + 19.5 * hash01(k * 4.7 + 6), 1.0 + 2.2 * hash01(k * 1.9 + 7), 0.7 + 1.5 * hash01(k * 6.1 + 8),
          0.28 + 0.5 * hash01(k * 3.9 + 9)) for k in range(80)]


def relief(x, y):
    k = max(0, min(len(STRATA) - 2, bisect.bisect(STRATA, y) - 1))
    t = (y - STRATA[k]) / (STRATA[k + 1] - STRATA[k])
    hard = hash01(k * 3.3 + 1.0)
    r = (0.3 * hard if hard > 0.45 else -0.06) * max(0.0, math.sin(math.pi * t)) ** 0.6
    r += 0.07 * math.sin(x * 0.7 + y * 0.9)
    for bx, by, sx, sy, amp in BUMPS:
        dx, dy = (x - bx) / sx, (y - by) / sy
        d2 = dx * dx + dy * dy
        if d2 < 9.0:
            r += amp * math.exp(-d2)
    if y < 1.2:
        r += 0.32 * smooth((1.2 - y) / 3.2) * (0.75 + 0.25 * math.sin(x * 0.45 + 1.0))
    return max(-0.15, min(1.25, r))


def wave(x, y, z):
    return y + 0.35 * math.sin(x * 0.21 + y * 0.3) + 0.22 * math.sin(x * 0.53 - y * 0.17 + z)


def wall_material(cx, cz, f):
    if f < -0.9:
        return 'RockWetDark'
    if f < 0.8:
        return 'RockWet'
    return PATTERN[bisect.bisect(STRATA, f) % len(PATTERN)]


def cap_path_z(x):
    return -7.0 + 1.8 * math.sin(x * 0.11 + 1.0)


PALMS = [(-22, -12), (-6, -18), (18, -14), (32, -20)]
CAP_MOUNDS = []
for _k in range(60):
    _x, _z = -42 + 84 * hash01(_k * 1.9 + 41), -27 + 22 * hash01(_k * 2.7 + 42)
    if _z > face_z(_x) - 2.5 or any(math.hypot(_x - px, _z - pz) < 4.5 for px, pz in PALMS) or abs(_z - (-7.0 + 1.8 * math.sin(_x * 0.11 + 1.0))) < 2.4:
        continue
    CAP_MOUNDS.append((_x, _z, 2.2 + 2.4 * hash01(_k + 43), 0.3 + 0.55 * hash01(_k * 3.1 + 44)))


def cap_bump(x, z):
    h = 0.0
    for mx, mz, r, amp in CAP_MOUNDS:
        d = math.hypot(x - mx, z - mz) / r
        if d < 1.0:
            h += amp * (1 - d * d) ** 2.0
    return h


def cap_material(cx, cz, f):
    bump = cap_bump(cx, cz)
    if bump > 0.5:
        return 'RockPale' if hash01(round(cx * 1.3) * 1.7 + round(cz * 1.3)) > 0.5 else 'RockA'
    if bump > 0.3:
        return 'GrassDry'
    if abs(f) < 1.1:
        return 'Dirt'
    if abs(f) < 1.9:
        return 'GrassDry'
    n = hash01(round(cx * 0.9) * 1.7 + round(cz * 0.9) * 3.1)
    return 'GrassLight' if n > 0.72 else 'GrassDark' if n < 0.25 else 'Grass'


def wall(b):
    splits = [(wave, [-0.9, 0.8] + STRATA[1:-1])]
    n = 3          # every cell of the game's grid becomes n x n, pushed in and out by the relief

    def wp(A, B_, C, D, u, v):
        p = [(1 - u) * (1 - v) * A[k] + u * (1 - v) * B_[k] + u * v * C[k] + (1 - u) * v * D[k] for k in range(3)]
        return (p[0], p[1], p[2] + relief(p[0], p[1]))
    for j in range(ROWS):
        for i in range(COLS):
            A, B_, C, D = WALL[(i, j)], WALL[(i + 1, j)], WALL[(i + 1, j + 1)], WALL[(i, j + 1)]
            for p in range(n):
                for q in range(n):
                    a, bb, c, d = (wp(A, B_, C, D, p / n, q / n), wp(A, B_, C, D, (p + 1) / n, q / n),
                                   wp(A, B_, C, D, (p + 1) / n, (q + 1) / n), wp(A, B_, C, D, p / n, (q + 1) / n))
                    for tri in ((bb, c, d), (bb, d, a)):
                        b._terrain_cut([tri[0], tri[2], tri[1]], splits, wall_material)   # the game's triangles face the other way (see Builder.skin)
    cap_split = [(lambda x, y, z: z - cap_path_z(x), [-1.9, -1.1, 1.1, 1.9])]
    rows, halves = 14, 2

    def cap_pt(f0, b0, f1, b1, u, r):
        t = r / rows
        fx = [f0[k] + (f1[k] - f0[k]) * u for k in range(3)]
        bk = [b0[k] + (b1[k] - b0[k]) * u for k in range(3)]
        x, y, z = fx[0] + (bk[0] - fx[0]) * t, fx[1] + (bk[1] - fx[1]) * t, fx[2] + (bk[2] - fx[2]) * t
        if r:
            y += 0.12 * math.sin(x * 0.31 + z * 0.43) * smooth(t * 2.0) + 0.5 * smooth((t - 0.7) / 0.3) * (0.6 + 0.4 * math.sin(x * 0.2))
            y += cap_bump(x, z) * smooth(t * 6.0)
        return (x, y, z)
    for b0, f0, f1, b1 in CAP:
        for h in range(halves):
            u0, u1 = h / halves, (h + 1) / halves
            for r in range(rows):
                l0, r0 = cap_pt(f0, b0, f1, b1, u0, r), cap_pt(f0, b0, f1, b1, u1, r)
                l1, r1 = cap_pt(f0, b0, f1, b1, u0, r + 1), cap_pt(f0, b0, f1, b1, u1, r + 1)
                for tri in ((l1, l0, r0), (l1, r0, r1)):
                    b._terrain_cut([tri[0], tri[2], tri[1]], cap_split, cap_material)
    # the two ends and the back of the block: the same layers, cut straight
    for side in (-1, 1):
        zf = face_z(side * HALF) - 0.5
        y = -6.0
        k = 0
        while y < TOP:
            h = min(STRATA[k + 1] - y, TOP - y) if k + 1 < len(STRATA) else TOP - y
            mat = PATTERN[k % len(PATTERN)]
            b.box(mat, side * (HALF - 0.3), y + h / 2, (zf + ZBACK) / 2, 0.6, h, zf - ZBACK)
            y += h
            k += 1
    y = -6.0
    k = 0
    while y < TOP:
        h = min(STRATA[k + 1] - y, TOP - y)
        b.box(PATTERN[k % len(PATTERN)], 0, y + h / 2, ZBACK + 0.3, 90, h, 0.6)
        y += h
        k += 1
    for side in (-1, 1):          # the grass cap over the ends and the back, so the cut shows rock under a green roof
        b.box('GrassDark', side * (HALF - 0.3), TOP + 0.04, (face_z(side * HALF) - 0.5 + ZBACK) / 2, 0.62, 0.08, face_z(side * HALF) - 0.5 - ZBACK)
    b.box('GrassDark', 0, TOP + 0.04, ZBACK + 0.3, 90, 0.08, 0.62)


def wall_detail(b):
    for x, y in ((-26.0, 11.0), (30.0, 14.0)):          # two dark hollows
        b.ico('Cave', x, y, wall_z(x, y) + 0.2, 1.0, 1.0, sub=1, sc=(1.6, 1.0, 0.45))
    # roots, vines and green tufts clinging to the lip and to the ledges
    for k in range(22):
        x = -43 + 86 * hash01(k * 5.7 + 2)
        if 3 < x < 17:
            continue
        zt = wall_z(x, 21.6) + 0.1
        ln = 3 + 6 * hash01(k + 50)
        for j in range(3):
            xx = x + (j - 1) * 0.35
            b.limb('Vine', (xx, 21.8, zt), (xx + 0.2 * math.sin(k + j), 21.8 - ln * (0.6 + 0.4 * hash01(k * 3 + j)), wall_z(xx, 21.8 - ln * 0.7) + 0.2), 0.05, 0.015, 3)
    for k in range(60):
        x = -44 + 88 * hash01(k * 1.9 + 3)
        y = 4 + 17 * hash01(k * 3.7 + 4)
        if 3 < x < 17 and y < 11.8:
            continue
        tuft(b, x, y, wall_z(x, y) + 0.12, 1.0, 'Tuft' if k % 2 else 'GrassLight', 3, seed=k)
    for k in range(40):   # barnacles and weed at the waterline
        x = -44 + 88 * hash01(k * 2.1 + 14)
        b.ico('Barnacle' if k % 2 else 'RockWetDark', x, 0.1 + 0.4 * hash01(k), wall_z(x, 0.2) + 0.15, 0.17, 0.7, sub=0)


def cliff_top(b):
    palms = [(-22, -12), (-6, -18), (18, -14), (32, -20)]
    for k in range(110):
        x = -43 + 86 * hash01(k * 1.7 + 31)
        z = -28 + 25 * hash01(k * 2.9 + 32)
        if z > face_z(x) - 1.2 or any(math.hypot(x - px, z - pz) < 2.6 for px, pz in palms):
            continue
        if abs(z - cap_path_z(x)) < 1.5:
            continue
        r = hash01(k * 5.1)
        if r < 0.3:
            bush(b, x, TOP + cap_bump(x, z), z, 0.9 + 0.5 * hash01(k + 2), seed=k)
        elif r < 0.85:
            tuft(b, x, TOP + cap_bump(x, z), z, 1.2, 'Tuft' if k % 2 else 'GrassLight', 5, seed=k)
            flowers(b, x, TOP + cap_bump(x, z), z, k, 3, 0.7)
        else:
            tuft(b, x, TOP + cap_bump(x, z), z, 1.6, 'GrassDry', 6, seed=k)
    # a little cairn and a signpost on the path
    px = 24.0
    pz = cap_path_z(px)
    for i, r in enumerate((0.7, 0.5, 0.34, 0.2)):
        boulder(b, 'RockGrey', px + 0.03 * i, TOP - 0.05 + i * 0.26, pz + 2.1, r, (1.1, 0.6, 1.1), 200 + i)
    b.box('WoodDark', px - 6, TOP + 0.8, pz + 0.3, 0.12, 1.6, 0.12)
    b.box('Wood', px - 6, TOP + 1.35, pz + 0.3, 0.9, 0.3, 0.07, bevel=0.02)
    b.box('Wood', px - 6.1, TOP + 1.0, pz + 0.3, 0.8, 0.26, 0.07, rot_y=8, bevel=0.02)


# ---------------------------------------------------------------- ledges (collision: PrototypeSceneBuilder.WallLedge)
def ledge(b, name, x, h, reach):
    """A rock shelf growing out of the cliff (collision: four stepped boxes whose top is 3.3 wide, 2.9 beyond the face): one smooth tapering
    bracket through ellipse rings, wide and flat on top, narrowing down into the wall, with a worn pale lip."""
    fz = face_z(x)
    a, d = 1.95, 2.1
    cz0 = fz + reach - d + 0.2
    levels = ((0.0, 1.0, 0.0), (0.4, 1.05, -0.02), (1.1, 0.96, -0.3), (2.0, 0.9, -0.75), (3.0, 0.88, -1.3), (4.0, 0.86, -1.9))
    n = 14
    rings = []
    for k, (dy, sc, dz) in enumerate(levels):
        ring = []
        for i in range(n):
            th = 2 * math.pi * i / n
            j = 1.0 + (0.0 if k == 0 else (hash01(k * 7.3 + i * 1.7 + x) - 0.5) * 0.16)
            jy = 0.0 if k < 2 else (hash01(k * 3.1 + i * 2.3 + x) - 0.5) * 0.3
            ring.append((x + a * sc * math.cos(th) * j, h - dy + jy, cz0 + dz + d * sc * math.sin(th) * j))
        rings.append(ring)
    top = [(x, h, cz0)] * n
    b.skin('RockTop', [rings[0], top], axis=lambda k: (x, h - 3.0, cz0), closed=True)
    for k, mat in enumerate(('RockPale', 'RockA', 'RockB', 'RockC', 'RockB')):
        b.skin(mat, [rings[k], rings[k + 1]], closed=True)
    b.box('Chalk', x, h + 0.015, fz + reach - 0.1, 2.4, 0.02, 0.12)
    for side in (-1, 1):                                                   # rope rails along the two sides, open in front and towards the wall's path
        for q, zz in enumerate((fz - 0.4, fz + 0.5, fz + 1.4)):
            px = x + side * 1.75
            b.limb('WoodDark', (px, h - 0.05, zz), (px, h + 1.05, zz), 0.05, 0.045, 5)
            if q:
                for hy in (0.95, 0.55):
                    b.limb('Rope', (px, h + hy, zz - 0.9), (px, h + hy - 0.05, zz), 0.014, 0.014, 3)
    tuft(b, x - 1.3, h, fz + 0.2, 1.0, 'Tuft', 4, seed=int(x * 3))
    tuft(b, x + 1.2, h, fz + 0.1, 1.2, 'GrassLight', 4, seed=int(x * 5))
    flowers(b, x + 1.3, h, fz + 0.3, int(x) + 2, 3, 0.4)
    b.spot('Spot_' + name, x, h, fz + reach)


def ladder(b, x, y0, y1):
    """A wooden ladder bolted to the face with brackets."""
    for dx in (-0.3, 0.3):
        y = y0
        while y < y1 - 0.01:
            ya = min(y + 1.0, y1)
            b.limb('Wood', (x + dx, y, wall_z(x + dx, y) + 0.3), (x + dx, ya, wall_z(x + dx, ya) + 0.3), 0.05, 0.05, 4)
            y = ya
    y = y0 + 0.3
    while y < y1:
        b.box('Wood', x, y, wall_z(x, y) + 0.3, 0.64, 0.05, 0.06)
        y += 0.38
    y = y0 + 0.8
    while y < y1:
        zw = wall_z(x, y)
        b.box('WoodDark', x - 0.3, y, zw + 0.15, 0.05, 0.06, 0.4)
        b.box('WoodDark', x + 0.3, y, zw + 0.15, 0.05, 0.06, 0.4)
        y += 2.2


def stairs(b, x0, y0, x1, y1, n):
    """Rock-cut steps running along the face from (x0, y0) to (x1, y1): a tread, a riser block under it, posts and a rope on the open side."""
    run = abs(x1 - x0) / n
    rise = (y1 - y0) / n
    for i in range(n + 1):
        t = i / n
        x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        zw = wall_z(x, y)
        b.box('RockTop', x, y - 0.13, zw + 0.5, max(run * 1.1, 0.5), 0.26, 1.5, bevel=0.05)
        b.box('RockB', x, y - 0.26 - abs(rise) * 0.5, zw + 0.25, max(run * 1.1, 0.5), abs(rise) + 0.5, 1.0, bevel=0.04)
        if i % 2 == 0:
            b.limb('WoodDark', (x, y - 0.1, zw + 1.15), (x, y + 1.0, zw + 1.15), 0.045, 0.04, 5)
    for hy in (0.95, 0.5):
        for i in range(n):
            ta, tb = i / n, (i + 1) / n
            xa, ya = x0 + (x1 - x0) * ta, y0 + (y1 - y0) * ta
            xb, yb = x0 + (x1 - x0) * tb, y0 + (y1 - y0) * tb
            b.limb('Rope', (xa, ya + hy, wall_z(xa, ya) + 1.15), (xb, yb + hy, wall_z(xb, yb) + 1.15), 0.014, 0.014, 3)


def access(b):
    """How one gets to the places: a path along the face from the house terrace to the lower shelf, ladders up to the upper shelf and to the
    top, steps down from the top to a plank bridge into the lighthouse gallery, rope ladders up the two sea stacks."""
    # the path at 8 m from the terrace (x 3) to the lower shelf (x -6.1)
    x = 3.2
    k = 0
    while x > -6.2:
        zw = wall_z(x, 8.0)
        b.box('RockTop', x, 7.8, zw + 0.4, 1.25, 0.4, 1.7, bevel=0.07)
        b.box('RockB', x, 7.0, zw + 0.2, 1.2, 1.4, 1.2, bevel=0.06)
        if k % 2 == 0:
            b.limb('WoodDark', (x, 7.9, zw + 1.18), (x, 9.0, zw + 1.18), 0.05, 0.045, 5)
        x -= 1.1
        k += 1
    xs = [3.2 - 1.1 * q for q in range(k)]
    for hy in (0.95, 0.5):
        for q in range(0, k - 2, 2):
            b.limb('Rope', (xs[q], 8.0 + hy, wall_z(xs[q], 8.0) + 1.18), (xs[q + 2], 8.0 + hy, wall_z(xs[q + 2], 8.0) + 1.18), 0.014, 0.014, 3)
    # lower shelf (-8, 8) -> upper shelf (-2, 15): a plank, a ladder between them, a plank
    b.box('Wood', -5.75, 7.94, wall_z(-5.75, 8.0) + 0.6, 1.0, 0.1, 1.3)
    ladder(b, -5.0, 7.7, 15.1)
    b.box('Wood', -4.45, 14.96, wall_z(-4.45, 15.0) + 0.6, 1.0, 0.1, 1.3)
    # upper shelf -> the top
    b.box('Wood', 0.3, 14.96, wall_z(0.3, 15.0) + 0.6, 1.0, 0.1, 1.3)
    ladder(b, 0.9, 14.7, 22.5)
    b.limb('WoodDark', (0.5, 21.7, wall_z(0.5, 21.7) + 0.5), (0.5, 23.3, wall_z(0.5, 22.0) - 0.3), 0.05, 0.045, 5)
    b.limb('WoodDark', (1.3, 21.7, wall_z(1.3, 21.7) + 0.5), (1.3, 23.3, wall_z(1.3, 22.0) - 0.3), 0.05, 0.045, 5)
    # steps from the top down to the lighthouse and a bridge onto its gallery
    stairs(b, 21.0, 21.9, 25.6, 18.3, 9)
    zw = wall_z(26.0, 18.0)
    bx0, bz0, bx1, bz1 = 25.9, zw + 0.7, 27.0, -2.7
    mx, mz = (bx0 + bx1) / 2, (bz0 + bz1) / 2
    ang = math.degrees(math.atan2(bx1 - bx0, bz1 - bz0))
    ln = math.hypot(bx1 - bx0, bz1 - bz0)
    b.box('Wood', mx, 17.9, mz, 1.1, 0.12, ln + 0.4, rot_y=-ang + 0.0)
    b.box('WoodDark', mx - 0.45, 17.8, mz, 0.1, 0.16, ln + 0.4, rot_y=-ang)
    b.box('WoodDark', mx + 0.45, 17.8, mz, 0.1, 0.16, ln + 0.4, rot_y=-ang)
    # rope ladders up the stacks, on the sides away from the diving places
    for cx, cz, r0, r1, top, dxz in ((12.0, 30.0, 4.4, 2.8, 7.0, (0.0, 1.0)), (-32.0, 24.0, 5.2, 3.3, 12.0, (-1.0, 0.0))):
        h = top + 6.0
        px, pz = -dxz[1], dxz[0]
        y = top + 0.1
        while y > -0.6:
            rr = (r0 + (r1 - r0) * ((y + 6.0) / h)) * 1.04 + 0.12
            ya = max(y - 1.0, -0.6)
            rb = (r0 + (r1 - r0) * ((ya + 6.0) / h)) * 1.04 + 0.12
            for side in (-0.28, 0.28):
                b.limb('Rope', (cx + dxz[0] * rr + px * side, y, cz + dxz[1] * rr + pz * side), (cx + dxz[0] * rb + px * side, ya, cz + dxz[1] * rb + pz * side), 0.016, 0.016, 3)
            y = ya
        y = top - 0.3
        while y > -0.5:
            rr = (r0 + (r1 - r0) * ((y + 6.0) / h)) * 1.04 + 0.14
            b.box('Wood', cx + dxz[0] * rr, y, cz + dxz[1] * rr, 0.7 if dxz[0] == 0 else 0.06, 0.05, 0.06 if dxz[0] == 0 else 0.7)
            y -= 0.4


# ---------------------------------------------------------------- the fisherman's house
def house(b):
    hx, terrace_top = 10.0, 8.0
    tb = face_z(hx) - 3.0
    tf = -0.2
    # rock terrace: dry stone courses on the front and the sides above the surf, plain rock below the waterline
    b.box('RockC', hx, -3.3, (tf + tb) / 2 - 0.2, 14, 5.4, tf - tb - 0.4)
    k = 0
    y = -0.6
    while y < terrace_top - 0.05:
        ch = 0.7 + 0.55 * hash01(k * 3.1)
        ch = min(ch, terrace_top - y)
        x = hx - 7.0
        j = 0
        while x < hx + 7.0 - 0.1:
            w = min(1.5 + 1.8 * hash01(k * 11.3 + j * 2.7), hx + 7.0 - x)
            mat = ('RockA', 'RockB', 'RockPale', 'RockC', 'RockA')[int(hash01(k * 5.3 + j * 1.7) * 5)]
            if y < 0.5:
                mat = 'RockWet'
            b.box(mat, x + w / 2, y + ch / 2, tf - 0.02 + 0.14 * hash01(k + j * 3), w - 0.06, ch - 0.05, 0.9, bevel=0.14, rot_y=(hash01(k * 2 + j) - 0.5) * 4)
            x += w
            j += 1
        y += ch
        k += 1
    for side in (-1, 1):
        zc = (tf + tb) / 2
        b.box('RockB', hx + side * 7.0, (terrace_top - 6.0) / 2, zc, 0.3, terrace_top + 6.0, tf - tb)
    b.box('GrassDark', hx, terrace_top + 0.05, (tf + tb) / 2, 14, 0.1, tf - tb)
    b.box('Dirt', hx - 2.0, terrace_top + 0.11, 0.0, 3.0, 0.02, 0.9)           # a worn strip in front of the door
    for xx in (hx - 6.3, hx - 5.6, hx + 5.9, hx + 6.5, hx + 5.0):
        tuft(b, xx, terrace_top + 0.1, -0.6 - 0.8 * hash01(xx), 1.2, 'GrassLight', 5, seed=int(xx * 7))
    for xx in (hx - 6.0, hx + 6.2):
        bush(b, xx, terrace_top + 0.1, -1.2, 1.1, seed=int(xx * 3))
    # walls
    houseC = (hx, (terrace_top + 11.2) / 2, -1.4)
    b.box('Lime', *houseC, 8.0, 11.2 - terrace_top, 3.2)
    b.box('Stone', hx, terrace_top + 0.25, -1.4, 8.3, 0.5, 3.5, bevel=0.03)                 # plinth
    for xx in (hx - 4.0, hx + 4.0):                                                          # corner stones
        for q in range(5):
            b.box('Stone', xx, terrace_top + 0.9 + q * 0.62, -1.4, 0.34, 0.5, 3.3 + 0.0, bevel=0.03)
    # roof: red slab with a pale plate, a drip edge; the diving edge stays clear
    b.box('Roof', hx, 11.35, -1.2, 8.8, 0.3, 4.4, bevel=0.03)
    b.box('Lime', hx, 11.51, -1.4, 8.0, 0.04, 3.4)
    b.box('RockTop', hx, 11.51, 0.2, 8.0, 0.04, 0.6)
    for sx in (hx - 4.4, hx + 4.4):
        b.box('Roof', sx, 11.66, -1.2, 0.12, 0.3, 4.4, bevel=0.02)
    b.box('Roof', hx, 11.66, -3.3, 8.8, 0.3, 0.12, bevel=0.02)                                # a parapet on three sides, none towards the sea
    # door with a frame, a step and a lantern
    b.box('Stone', hx - 2.0, terrace_top + 1.05, 0.2, 1.3, 2.1, 0.12, bevel=0.02)
    b.box('Door', hx - 2.0, terrace_top + 1.0, 0.24, 1.0, 2.0, 0.08)
    for q in range(4):
        b.box('WoodDark', hx - 2.0 + (q - 1.5) * 0.24, terrace_top + 1.0, 0.29, 0.05, 1.9, 0.02)
    b.box('Metal', hx - 1.65, terrace_top + 1.0, 0.31, 0.08, 0.08, 0.05)
    b.box('Stone', hx - 2.0, terrace_top + 0.08, 0.55, 1.5, 0.16, 0.5, bevel=0.03)
    b.box('Metal', hx - 3.1, terrace_top + 2.5, 0.3, 0.12, 0.35, 0.12)
    b.ico('Lantern', hx - 3.1, terrace_top + 2.5, 0.32, 0.12, 1.0, sub=0)
    # windows
    for wx in (hx + 0.6, hx + 2.6):
        b.box('Stone', wx, terrace_top + 1.8, 0.2, 1.0, 1.1, 0.12, bevel=0.02)
        b.box('Glass', wx, terrace_top + 1.8, 0.24, 0.8, 0.9, 0.06)
        b.box('Stone', wx, terrace_top + 1.8, 0.28, 0.06, 0.9, 0.04)
        b.box('Stone', wx, terrace_top + 1.8, 0.28, 0.8, 0.06, 0.04)
        for sx in (-0.6, 0.6):
            b.box('Shutter', wx + sx, terrace_top + 1.8, 0.26, 0.36, 0.95, 0.05)
        b.box('Stone', wx, terrace_top + 1.18, 0.34, 1.2, 0.08, 0.3, bevel=0.02)             # sill
        b.box('Terracotta', wx, terrace_top + 1.0, 0.45, 1.0, 0.22, 0.2, bevel=0.02)         # a flower box
        for q in range(3):
            bush(b, wx + (q - 1) * 0.32, terrace_top + 1.12, 0.45, 0.2, seed=int(wx * 10) + q)
    # the side wall: a small window
    b.box('Glass', hx + 4.02, terrace_top + 1.8, -1.4, 0.05, 0.9, 0.8)
    b.box('Shutter', hx + 4.04, terrace_top + 1.8, -0.8, 0.05, 0.95, 0.3)
    b.box('Shutter', hx + 4.04, terrace_top + 1.8, -2.0, 0.05, 0.95, 0.3)
    # chimney, pots, an aerial and a flag on the roof, all well away from the front edge
    b.box('Lime', hx + 3.0, 12.2, -2.4, 0.6, 1.4, 0.6)
    b.box('Stone', hx + 3.0, 12.95, -2.4, 0.8, 0.12, 0.8, bevel=0.02)
    for q in range(4):
        b.box('StoneDark', hx + 3.0, 12.0 + q * 0.3, -2.4, 0.62, 0.05, 0.62)
    for px, pz, s in ((hx - 3.6, -2.8, 1.0), (hx - 2.6, -2.9, 0.7), (hx + 0.6, -3.0, 0.8)):
        b.cone('Terracotta', px, 11.5, pz, 0.34 * s, 0.26 * s, 0.4 * s, 8)
        bush(b, px, 11.85 * 1.0 + 0.0 - 0.0, pz, 0.7 * s, seed=int(px * 9))
    b.limb('Metal', (hx - 0.8, 11.5, -2.8), (hx - 0.8, 13.4, -2.8), 0.025, 0.02, 4)
    for q in range(3):
        b.box('Metal', hx - 0.8, 12.6 + q * 0.3, -2.8, 0.7 - q * 0.15, 0.02, 0.02)
    b.limb('WoodDark', (hx + 3.9, 11.5, -3.1), (hx + 3.9, 14.0, -3.1), 0.035, 0.025, 4)
    b.box('Roof', hx + 4.35, 13.7, -3.1, 0.9, 0.5, 0.02)
    # life round the door: barrels, crates, a net on a pole, lobster pots, a hanging fish rack
    for q, (dx, dz) in enumerate(((1.8, 0.7), (-4.9, 0.4))):
        b.cone('WoodDark', hx + dx, terrace_top + 0.1, dz, 0.4, 0.38, 0.85, 10)
        b.cone('Metal', hx + dx, terrace_top + 0.1 + 0.25, dz, 0.41, 0.41, 0.04, 10)
        b.cone('Metal', hx + dx, terrace_top + 0.1 + 0.62, dz, 0.4, 0.4, 0.04, 10)
    for q, (dx, dz, yaw) in enumerate(((-5.2, 0.9, 12), (-4.4, 1.1, -8))):
        b.box('Wood', hx + dx, terrace_top + 0.4, dz, 0.8, 0.55, 0.6, bevel=0.03, rot_y=yaw)
        b.box('WoodDark', hx + dx, terrace_top + 0.7, dz, 0.84, 0.05, 0.64, rot_y=yaw)
    b.cone('Net', hx + 5.2, terrace_top + 0.1, 0.5, 0.55, 0.4, 0.55, 8)
    b.cone('Net', hx + 5.9, terrace_top + 0.1, -0.4, 0.4, 0.3, 0.45, 8)
    b.limb('WoodDark', (hx + 3.4, terrace_top, 0.65), (hx + 3.4, terrace_top + 2.2, 0.65), 0.05, 0.045, 4)
    b.limb('WoodDark', (hx + 6.2, terrace_top, 0.65), (hx + 6.2, terrace_top + 2.2, 0.65), 0.05, 0.045, 4)
    b.box('WoodDark', hx + 4.8, terrace_top + 2.05, 0.65, 2.9, 0.06, 0.06)
    for q in range(5):
        fx = hx + 3.8 + q * 0.5
        b.ico('Stone', fx, terrace_top + 1.6, 0.65, 0.16, 1.0, sub=0, sc=(0.5, 1.5, 0.5))
        b.limb('Rope', (fx, terrace_top + 2.05, 0.65), (fx, terrace_top + 1.85, 0.65), 0.012, 0.012, 3)
    # the ladder down the terrace front, a rope and a rowing boat tied at its foot
    for dx in (-0.3, 0.3):
        b.limb('Wood', (hx + 5.0 + dx, -0.3, 0.14), (hx + 5.0 + dx, terrace_top + 0.7, 0.14), 0.05, 0.05, 4)
    for q in range(16):
        b.box('Wood', hx + 5.0, 0.2 + q * 0.5, 0.14, 0.6, 0.05, 0.05)
    b.spot('Spot_Roof', hx, 11.5, 1.0)
    # a dinghy by the ladder (not in the line of any dive)
    bx, bz = hx + 7.4, 2.6
    b.box('WoodDark', bx, 0.05, bz, 1.4, 0.5, 3.6, bevel=0.06, rot_y=18)
    b.box('Wood', bx, 0.34, bz, 1.2, 0.08, 3.3, rot_y=18)
    b.box('RockPale', bx, 0.34, bz, 0.8, 0.09, 0.4, rot_y=18)
    b.box('Fender', bx, 0.38, bz, 1.38, 0.05, 0.1, rot_y=18)


# ---------------------------------------------------------------- the lighthouse (collision: base 7.8 x 6.8, tower 2.8, gallery 4.6)
def lighthouse(b):
    lx, lz = 28.0, -0.6
    # the rock: a heap of boulders from the sea floor up to the plinth
    for k, (dx, dz, r, y, sc) in enumerate(((0, 0, 4.2, -1.0, (1.0, 0.85, 0.9)), (-2.2, 1.0, 2.6, -0.4, (1.0, 0.9, 1.1)), (2.3, -0.5, 2.7, -0.3, (1.1, 0.9, 1.0)),
                                            (0.5, 2.3, 2.2, -0.9, (1.2, 0.8, 1.0)), (-1.8, -2.0, 2.4, -0.5, (1.0, 1.0, 1.0)), (2.8, 1.8, 1.9, -0.8, (1.0, 0.9, 1.0)),
                                            (-3.3, -0.6, 1.8, -0.2, (1.0, 1.1, 1.0)))):
        weather_rock(b, lx + dx, y, lz + dz, r * (1.05), seed=k + 70, wet=False)
        boulder(b, 'RockWet', lx + dx, -0.6, lz + dz, r * 1.0, (1.1, 0.45, 1.1), 90 + k)
    b.cone('RockWet', lx, -3.0, lz, 6.4, 4.2, 3.6, 12)
    b.cone('RockTop', lx, 1.6, lz, 3.6, 3.3, 1.5, 12)                        # the flat of the plinth, top at 3.1
    b.cone('Stone', lx, 3.0, lz, 2.25, 2.1, 0.45, 12)
    b.cone('StoneDark', lx, 3.4, lz, 2.0, 1.95, 0.12, 12)
    for k in range(7):
        a = k * 0.9 + 0.4
        b.ico('Grass', lx + math.cos(a) * 3.0, 3.05, lz + math.sin(a) * 2.6, 0.45, 0.45, sub=0)
        tuft(b, lx + math.cos(a) * 3.0, 3.1, lz + math.sin(a) * 2.6, 1.0, 'Tuft', 3, seed=k + 8)
    # tower bands, white and red, over the 17 m of the physical tower (y 1..18; the visible part starts on the plinth)
    y = 3.4
    k = 0
    while y < 17.65:
        h = min(2.35, 17.65 - y)
        r0 = 1.7 - (y - 1.0) / 17.0 * 0.45
        r1 = 1.7 - (y + h - 1.0) / 17.0 * 0.45
        b.cone('LHRed' if k % 2 else 'LHWhite', lx, y, lz, r0, r1, h, 12)
        y += h
        k += 1
    b.cone('LHDark', lx, 3.4, lz, 1.78, 1.76, 0.18, 12)
    # door, windows and a little balcony ring under the gallery
    tz = lz + 1.66
    b.box('LHDark', lx, 4.3, tz, 0.9, 1.7, 0.2, bevel=0.03)
    b.box('Door', lx, 4.3, tz + 0.02, 0.75, 1.55, 0.12)
    b.box('Stone', lx, 5.25, tz - 0.02, 1.2, 0.18, 0.25, bevel=0.03)
    for wy in (7.4, 10.4, 13.4, 16.0):
        r = 1.7 - (wy - 1.0) / 17.0 * 0.45
        b.box('Glass', lx, wy, lz + r - 0.05, 0.42, 0.85, 0.12)
        b.box('Stone', lx, wy + 0.5, lz + r - 0.02, 0.62, 0.1, 0.16, bevel=0.02)
    b.cone('Stone', lx, 17.45, lz, 1.5, 1.7, 0.3, 12)
    # gallery: red floor with a pale chalk line on the diving edge, corbels under it, a rail round the back
    b.cone('LHRed', lx, 17.7, lz, 2.3, 2.3, 0.3, 16)
    b.cone('LHWhite', lx, 17.99, lz, 2.25, 2.25, 0.03, 16)
    for k in range(10):
        a = k * 0.628
        b.limb('LHDark', (lx + math.cos(a) * 1.3, 17.45, lz + math.sin(a) * 1.3), (lx + math.cos(a) * 2.15, 17.7, lz + math.sin(a) * 2.15), 0.09, 0.07, 4)
    for k in range(11):
        a = math.radians(188 + k * 16.4)
        px, pz = lx + math.cos(a) * 2.2, lz + math.sin(a) * 2.2
        b.limb('LHWhite', (px, 18.0, pz), (px, 19.05, pz), 0.04, 0.04, 4)
    for hy in (18.5, 19.05):
        for k in range(10):
            a0, a1 = math.radians(188 + k * 16.4), math.radians(188 + (k + 1) * 16.4)
            b.limb('LHWhite', (lx + math.cos(a0) * 2.2, hy, lz + math.sin(a0) * 2.2), (lx + math.cos(a1) * 2.2, hy, lz + math.sin(a1) * 2.2), 0.03, 0.03, 4)
    # lamp room: glass drum, the lamp, red mullions, a domed red roof with a ball and a rod
    b.cone('LHGlass', lx, 18.0, lz, 1.0, 1.0, 1.6, 12)
    b.cone('LHLamp', lx, 18.3, lz, 0.42, 0.42, 0.9, 8)
    for k in range(8):
        a = k * 0.785
        b.limb('LHRed', (lx + math.cos(a) * 1.02, 18.0, lz + math.sin(a) * 1.02), (lx + math.cos(a) * 1.02, 19.6, lz + math.sin(a) * 1.02), 0.05, 0.05, 4)
    b.cone('LHRed', lx, 19.55, lz, 1.2, 1.15, 0.12, 12)
    b.cone('LHRed', lx, 19.65, lz, 1.2, 0.12, 1.1, 12)
    b.ico('Metal', lx, 20.8, lz, 0.14, 1.0, sub=0)
    b.limb('Metal', (lx, 20.8, lz), (lx, 21.7, lz), 0.03, 0.01, 4)
    b.spot('Spot_Lighthouse', lx, 18.0, 1.7)


# ---------------------------------------------------------------- sea stacks (collision: tapered boxes, RockPillar)
def stack(b, name, cx, cz, top_y, r0, r1, seed, spot):
    bottom = -6.0
    h = top_y - bottom
    steps = max(14, int(h / 0.6))
    for k in range(steps):
        f0, f1 = k / steps, (k + 1) / steps
        y0, y1 = bottom + h * f0, bottom + h * f1
        ra, rb = r0 + (r1 - r0) * f0, r0 + (r1 - r0) * f1
        # a smooth flare below the surface: the stack widens like a cone into the sea floor, nothing sticks out
        fa, fb = 1.0 + 0.45 * smooth((-0.4 - y0) / 4.5), 1.0 + 0.45 * smooth((-0.4 - y1) / 4.5)
        j = hash01(seed * 3.3 + k * 1.7)
        mat = PATTERN[(k + seed) % len(PATTERN)]
        if y1 < 0.9 and y0 > -1.1:
            mat = 'RockWet'
        elif y1 < -1.1:
            mat = 'RockWetDark' if k % 2 else 'RockC'
        grow = 0.98 + 0.08 * hash01(k * 1.9 + seed)
        b.cone(mat, cx + 0.3 * (j - 0.5), y0, cz + 0.3 * (hash01(k + seed) - 0.5), ra * fa * grow, rb * fb * grow * (0.98 + 0.06 * j), y1 - y0 + 0.02, 9)
        if k % 3 == 1 and y0 > 0.5:   # a lip of harder rock
            b.cone('RockPale', cx, y1 - 0.12, cz, rb * 1.08, rb * 1.06, 0.22, 9)
    for k in range(12):    # a few barnacles at the waterline
        a = k * 0.5236 + 0.2
        rr = r0 + (r1 - r0) * ((0.0 - bottom) / h)
        if k % 2:
            b.ico('Barnacle', cx + math.cos(a + 0.2) * rr * 1.04, 0.3, cz + math.sin(a + 0.2) * rr * 1.04, 0.12, 0.7, sub=0)
    # turf cap and what grows on it
    b.cone('GrassDark', cx, top_y - 0.35, cz, r1 * 1.05, r1 * 1.0, 0.4, 9)
    b.ico('Grass', cx, top_y - 0.05, cz, r1 * 0.9, 0.2, sub=1, jit=0.05, seed=seed)
    for k in range(7):
        a = k * 0.9 + seed
        tuft(b, cx + math.cos(a) * r1 * 0.7, top_y + 0.02, cz + math.sin(a) * r1 * 0.7, 1.3, 'Tuft' if k % 2 else 'GrassLight', 4, seed=k + seed)
    flowers(b, cx - r1 * 0.3, top_y, cz + r1 * 0.3, seed, 4, 0.8)
    b.spot(name, *spot)


# ---------------------------------------------------------------- the yacht (collision: hull 3.6 x 3.3 x 12.4, cabin, bow)
def yacht(b):
    yx, yz = -24.0, 6.0          # y0 of the game's code
    deck, keel_mid = 3.0, -0.55
    stations = [(-6.0, 1.45), (-5.6, 1.62), (-4.0, 1.78), (-1.0, 1.8), (2.0, 1.8), (4.5, 1.72), (6.0, 1.48), (7.0, 0.98), (7.5, 0.55), (7.8, 0.1)]
    ylevels = [deck, 1.45, 1.15, 0.5, -0.1, keel_mid]

    def keel(z):
        return keel_mid if z < 3.0 else keel_mid + (z - 3.0) / 4.8 * 1.9

    def half(z, w, y):
        k = keel(z)
        t = (y - k) / (deck - k)
        return w * (0.66 + 0.34 * max(0.0, min(1.0, t)) ** 0.7)

    def ring(z, w, y0, y1, side):
        out = []
        for y in ylevels:
            if y1 - 1e-6 <= y <= y0 + 1e-6 or True:
                pass
        return out
    segments = (('Hull', deck, 1.45), ('Stripe', 1.45, 1.15), ('Hull', 1.15, None))
    for side in (-1, 1):
        for mat, ya, yb in segments:
            rings = []
            for (z, w) in stations:
                k = keel(z)
                lo = k if yb is None else yb
                ys = [ya, (ya + lo) / 2, lo] if yb is None else [ya, yb]
                ys = [max(y, k) for y in ys]
                grow = 1.012 if mat == 'Stripe' else 1.0
                rings.append([(yx + side * half(z, w, y) * grow, y, yz + z) for y in ys])
            b.skin(mat, rings, axis=lambda k_, side=side: (yx, 1.2, yz + stations[k_][0]), closed=False)
    bottom_rings = [[(yx + half(z, w, keel(z)), keel(z), yz + z), (yx - half(z, w, keel(z)), keel(z), yz + z)] for (z, w) in stations]
    b.skin('Hull', bottom_rings, axis=lambda k_: (yx, 2.0, yz + stations[k_][0]), closed=False)
    deck_rings = [[(yx - w, deck, yz + z), (yx + w, deck, yz + z)] for (z, w) in stations]
    b.skin('Teak', deck_rings, axis=lambda k_: (yx, deck - 1.0, yz + stations[k_][0]), closed=False)
    # transom
    b.box('Hull', yx, 1.2, yz - 6.0, 2.9, 3.0, 0.1)
    b.box('Stripe', yx, 1.3, yz - 6.06, 2.92, 0.3, 0.06)
    # a toe rail, deck planks, portholes
    for side in (-1, 1):
        for i, (z0, w0) in enumerate(stations[:-1]):
            z1, w1 = stations[i + 1]
            ma = (w0 + w1) / 2 - 0.05
            b.limb('Teak', (yx + side * (w0 - 0.04), deck + 0.08, yz + z0), (yx + side * (w1 - 0.04), deck + 0.08, yz + z1), 0.06, 0.06, 4)
        for z in range(-5, 4):
            b.box('YachtGlass', yx + side * 1.815, 2.2, yz + z + 0.5, 0.05, 0.4, 0.4)
    for q in range(-9, 11):
        zz = -5.7 + q * 0.5
        if zz > 7.0:
            break
        w = 1.6 if zz < 4.5 else max(0.3, 1.5 - (zz - 4.5) * 0.55)
        b.box('WoodDark', yx, deck + 0.012, yz + zz, w * 2 - 0.2, 0.012, 0.025)
    # cabin: lower block, glazed band, roof overhanging, a hatch in front of it
    b.box('Hull', yx - 0.2, 3.8, yz - 1.5, 2.4, 1.6, 5.0, bevel=0.07)
    b.box('YachtGlass', yx - 0.2, 4.0, yz - 1.5, 2.46, 0.5, 4.2)
    for q in range(5):
        b.box('Chrome', yx - 0.2, 4.0, yz - 3.4 + q * 1.0, 2.5, 0.52, 0.05)
    b.box('Stripe', yx - 0.2, 4.62, yz - 1.5, 2.7, 0.14, 5.4, bevel=0.04)
    b.box('Teak', yx - 0.2, 4.45, yz + 1.2, 1.2, 0.18, 0.7, bevel=0.03)
    b.box('Chrome', yx - 0.2, 4.56, yz + 1.2, 1.0, 0.02, 0.5)
    # mast with spreaders and a boom, rigging lines, cockpit seating and a wheel
    b.limb('Chrome', (yx - 0.3, 3.0, yz + 1.5), (yx - 0.3, 12.0, yz + 1.5), 0.1, 0.06, 6)
    b.box('Chrome', yx - 0.3, 9.0, yz + 1.5, 1.6, 0.05, 0.06)
    b.box('Chrome', yx - 0.3, 6.5, yz + 1.5, 2.2, 0.05, 0.06)
    b.limb('Chrome', (yx - 0.3, 5.2, yz + 0.6), (yx - 0.3, 5.2, yz - 4.2), 0.05, 0.04, 5)
    for sx in (-1.0, 1.0):
        b.limb('Rope', (yx - 0.3, 11.8, yz + 1.5), (yx + sx * 1.7, 3.1, yz + 1.0), 0.012, 0.012, 3)
        b.limb('Rope', (yx - 0.3, 11.8, yz + 1.5), (yx + sx * 1.0, 3.1, yz + 7.0), 0.012, 0.012, 3)
    b.limb('Rope', (yx - 0.3, 11.8, yz + 1.5), (yx - 0.3, 3.2, yz - 5.7), 0.012, 0.012, 3)
    b.box('Teak', yx, 3.25, yz - 5.1, 2.6, 0.1, 0.5)
    b.box('Teak', yx - 1.3, 3.3, yz - 4.5, 0.5, 0.5, 1.4, bevel=0.04)
    b.box('Teak', yx + 1.3, 3.3, yz - 4.5, 0.5, 0.5, 1.4, bevel=0.04)
    b.cone('Chrome', yx, 3.05, yz - 4.2, 0.1, 0.07, 0.8, 6)
    b.cone('WoodDark', yx, 3.85, yz - 4.2, 0.36, 0.36, 0.06, 10)
    # rails and stanchions along the sides (the bow stays open: it is the diving place)
    for side in (-1, 1):
        b.box('Chrome', yx + side * 1.65, 3.45, yz - 0.2, 0.04, 0.04, 10.0)
        b.box('Chrome', yx + side * 1.65, 3.25, yz - 0.2, 0.03, 0.03, 10.0)
        for z in (-5.0, -3.2, -1.4, 0.4, 2.2, 4.0):
            b.box('Chrome', yx + side * 1.65, 3.22, yz + z, 0.04, 0.45, 0.04)
    # fenders, a life ring, an anchor and a bow cleat
    for z in (-3.0, 0.0, 3.0):
        b.cone('Fender', yx - 1.78, 1.9, yz + z, 0.14, 0.14, 0.7, 6)
        b.limb('Rope', (yx - 1.78, 2.9, yz + z), (yx - 1.78, 2.55, yz + z), 0.015, 0.015, 3)
    b.cone('Fender', yx + 1.5, 4.0, yz - 5.9, 0.4, 0.4, 0.12, 12)
    b.cone('Hull', yx + 1.5, 4.01, yz - 5.9, 0.43, 0.43, 0.05, 12)
    b.limb('Metal', (yx, 3.0, yz + 6.2), (yx, 3.3, yz + 6.9), 0.05, 0.04, 4)
    b.box('Chrome', yx, 3.06, yz + 5.9, 0.4, 0.05, 0.09)
    b.spot('Spot_Yacht', yx, 3.0, yz + 7.2)


# ---------------------------------------------------------------- beaches
BEACH_MOUNDS = []
for _k in range(40):
    _side = -1 if _k % 2 else 1
    _x, _z = _side * (32.5 + 11 * hash01(_k * 2.1 + 51)), -1.0 + 5.8 * hash01(_k * 3.3 + 52)
    if any(math.hypot(_x - cx, _z - cz) < 3.2 for cx, cz in CRABS) or any(math.hypot(_x - px, _z - pz) < 2.6 for px, pz in ((-40, -1), (-34, 4), (38, -1.5), (42, 4.5))):
        continue
    if (_side < 0 and _x > -32.5) or (_side > 0 and _x < 35.5):
        continue
    BEACH_MOUNDS.append((_x, _z, 1.4 + 1.4 * hash01(_k + 53), 0.3 + 0.5 * hash01(_k * 2.9 + 54)))


def beach_bump(x, z):
    h = 0.0
    for mx, mz, r, amp in BEACH_MOUNDS:
        d = math.hypot(x - mx, z - mz) / r
        if d < 1.0:
            h += amp * (1 - d * d) ** 2.0
    return h


def beach_height(x, z, lo, hi):
    """Sand at +0.35 in front of the cliff, tailing off into the sea; dunes against the wall, rock mounds breaking through the sand."""
    if hi < 0:   # the left beach: flat for x < hi, falling towards +x
        ex = max(0.0, x - hi)
    else:
        ex = max(0.0, lo - x)
    ez = max(0.0, z - 5.6)
    d = math.hypot(ex, ez) / 6.5
    s = 1.0 - smooth(d)
    h = -6.0 + 6.35 * s ** 0.85
    wf = face_z(x)
    if z < wf + 3.5 and s > 0.99:
        h += 0.55 * smooth((wf + 3.5 - z) / 3.5) + 0.12 * math.sin(x * 0.9) * smooth((wf + 3.0 - z) / 3.0)
    if s > 0.97:
        h += beach_bump(x, z)
    return h


def beaches(b):
    for lo, hi in (LEFT_BEACH, RIGHT_BEACH):
        x0, x1 = (-45.0, -22.0) if hi < 0 else (22.0, 45.0)
        step = 1.0

        def hf(x, z, lo=lo, hi=hi):
            return beach_height(x, z, lo, hi)

        def choose(cx, cz, f):
            if f < -0.35:
                return 'Seabed'
            bump = beach_bump(cx, cz)
            if bump > 0.5:
                return 'RockA' if hash01(round(cx) * 2.3 + round(cz)) > 0.5 else 'RockB'
            if bump > 0.28:
                return 'RockPale'
            if f < 0.18:
                return 'SandWet'
            if f > 0.7 and hash01(round(cx * 0.7) * 2.3 + round(cz * 0.7)) > 0.6:
                return 'SandDark'
            return 'Sand'
        b.terrain(x0, x1, -6.0, 13.0, step, hf, choose, splits=[(lambda x, y, z: y, [-0.35, 0.18])])
        # soil cut on the outer side of the block, under the sand
        xe = -HALF if hi < 0 else HALF
        for zz in range(-6, 13):
            h = hf(xe, zz + 0.5)
            b.box('SandDark', xe + (0.25 if hi < 0 else -0.25), (h - 6.1) / 2, zz + 0.5, 0.5, h + 6.1, 1.0)
    # beach life: shells, driftwood, starfish, sea-oats on the dunes, rocks (the crabs are the game's; keep a clear patch round them)
    for k in range(70):
        side = -1 if k % 2 else 1
        x = side * (31.5 + 13 * hash01(k * 1.9 + 1))
        z = -1.5 + 6.8 * hash01(k * 2.7 + 2)
        if (side < 0 and x > -31.0) or (side > 0 and x < 34.0):
            continue
        if any(math.hypot(x - cx, z - cz) < 1.6 for cx, cz in CRABS):
            continue
        y = beach_height(x, z, *(LEFT_BEACH if side < 0 else RIGHT_BEACH))
        r = hash01(k * 3.1)
        if r < 0.3:
            b.ico('Shell', x, y + 0.02, z, 0.1 + 0.05 * hash01(k), 0.5, sub=0)
        elif r < 0.42:
            b.cone('Starfish', x, y, z, 0.28, 0.04, 0.05, 5)
        elif r < 0.55:
            b.box('Driftwood', x, y + 0.1, z, 1.2 + 1.4 * hash01(k), 0.2, 0.2, rot_y=360 * hash01(k + 5), bevel=0.04)
        elif r < 0.75 and z < face_z(x) + 3.5:
            tuft(b, x, y, z, 1.7, 'GrassDry', 6, seed=k)
        else:
            tuft(b, x, y, z, 1.2, 'GrassDry', 4, seed=k)
    # a beach umbrella and a towel on the left one, a pair of footprints would be too much
    ux, uz = -38.5, 5.4
    b.limb('WoodDark', (ux, 0.3, uz), (ux + 0.1, 2.5, uz), 0.04, 0.035, 5)
    b.cone('Roof', ux + 0.1, 2.2, uz, 1.5, 0.05, 0.5, 8)
    for q in range(4):
        a = q * 1.5708
    b.box('Shutter', ux - 0.5, 0.36, uz - 1.8, 1.0, 0.03, 1.8, rot_y=14)
    b.box('Lime', ux - 0.5, 0.38, uz - 1.8, 1.0, 0.02, 0.35, rot_y=14)


# ---------------------------------------------------------------- the sea rocks (the game's SeaRock x3) and a few more
def sea_rocks(b):
    """Two gentle islets far in the corners (nobody dives there): a low smooth dome with a wide drowned foot, grass on top."""
    for k, (x, z, r) in enumerate(((-40.0, 52.0, 3.0), (39.0, 54.0, 2.6))):
        b.cone('RockWet', x, -3.5, z, r * 2.3, r * 1.15, 3.6, 11)
        b.cone('RockC', x, -0.2, z, r * 1.15, r * 0.9, 0.9, 11)
        b.cone('RockB', x, 0.6, z, r * 0.95, r * 0.7, 0.7, 11)
        b.ico('GrassDark', x, 1.2, z, r * 0.75, 0.3, sub=1, jit=0.06, seed=k)
        tuft(b, x + 0.4, 1.3, z - 0.3, 1.2, 'Tuft', 4, seed=k)
        flowers(b, x - 0.5, 1.3, z + 0.2, k + 3, 3, 0.6)


# ---------------------------------------------------------------- what grows on the top
def top_y(x, z):
    return TOP + cap_bump(x, z) * 0.9


def clear_of_palms(x, z, r=3.4):
    return all(math.hypot(x - px, z - pz) > r for px, pz in PALMS)


def cypress(b, x, z, s=1.0):
    y = top_y(x, z)
    b.cone('Bark', x, y - 0.1, z, 0.12 * s, 0.1 * s, 0.7 * s, 5)
    b.cone('Cypress', x, y + 0.4 * s, z, 0.62 * s, 0.08 * s, 4.6 * s, 8)
    b.cone('CypressDark', x + 0.12 * s, y + 0.9 * s, z - 0.1 * s, 0.4 * s, 0.07 * s, 3.2 * s, 7)


def umbrella_pine(b, x, z, s=1.0):
    y = top_y(x, z)
    b.limb('Bark', (x, y - 0.1, z), (x + 0.35 * s, y + 3.6 * s, z + 0.15 * s), 0.22 * s, 0.14 * s, 6)
    b.limb('Bark', (x + 0.35 * s, y + 3.4 * s, z + 0.15 * s), (x - 0.5 * s, y + 4.6 * s, z - 0.2 * s), 0.12 * s, 0.08 * s, 5)
    b.ico('PineUmb', x + 0.3 * s, y + 4.3 * s, z + 0.1 * s, 1.0 * s, 1.0, sub=1, sc=(2.7 * s, 0.5, 2.5 * s), jit=0.1, seed=int(x * 3 + z))
    b.ico('Cypress', x - 0.7 * s, y + 4.9 * s, z - 0.3 * s, 1.0 * s, 1.0, sub=1, sc=(1.6 * s, 0.5, 1.5 * s), jit=0.1, seed=int(x * 5 + z))


def olive(b, x, z, s=1.0):
    y = top_y(x, z)
    b.limb('Bark', (x, y - 0.1, z), (x - 0.4 * s, y + 1.2 * s, z), 0.28 * s, 0.2 * s, 6)
    b.limb('Bark', (x - 0.4 * s, y + 1.1 * s, z), (x - 1.1 * s, y + 2.0 * s, z + 0.2 * s), 0.18 * s, 0.1 * s, 5)
    b.limb('Bark', (x - 0.4 * s, y + 1.1 * s, z), (x + 0.7 * s, y + 2.2 * s, z - 0.3 * s), 0.17 * s, 0.1 * s, 5)
    for q, (dx, dy, dz, rr) in enumerate(((-1.1, 2.3, 0.2, 1.1), (0.7, 2.5, -0.3, 1.2), (-0.2, 2.9, 0.1, 1.0), (-1.5, 1.8, -0.5, 0.8))):
        b.ico('Olive' if q % 2 else 'OliveDark', x + dx * s, y + dy * s, z + dz * s, rr * s, 0.8, sub=1, jit=0.14, seed=int(x * 7 + q))


def agave(b, x, z, s=1.0):
    y = top_y(x, z)
    for q in range(9):
        a = q * 0.698 + x
        tilt = 0.55 + 0.25 * hash01(q + x)
        b.limb('Agave', (x, y + 0.1, z), (x + math.cos(a) * 0.85 * s, y + 0.15 + tilt * s * 0.9, z + math.sin(a) * 0.85 * s), 0.1 * s, 0.015, 3)


def lavender_field(b, x0, x1, z0, z1):
    zr = z0
    row = 0
    while zr < z1:
        for xx in [x0 + 0.55 * q for q in range(int((x1 - x0) / 0.55))]:
            if hash01(xx * 3.1 + row * 7.7) < 0.12:
                continue
            y = top_y(xx, zr)
            b.cone('LavenderDark' if (int(xx * 2) + row) % 3 == 0 else 'Lavender', xx + 0.1 * math.sin(xx * 5), y, zr, 0.22, 0.04, 0.55 + 0.25 * hash01(xx + row), 5)
        zr += 1.15
        row += 1


def poppy_meadow(b, x0, x1, z0, z1, n):
    for k in range(n):
        x, z = x0 + (x1 - x0) * hash01(k * 2.3 + 71), z0 + (z1 - z0) * hash01(k * 3.7 + 72)
        if not clear_of_palms(x, z, 3.0) or abs(z - cap_path_z(x)) < 2.0:
            continue
        y = top_y(x, z)
        tuft(b, x, y, z, 1.1, 'Tuft', 3, seed=k)
        b.limb('Tuft', (x, y, z), (x, y + 0.4, z), 0.015, 0.012, 3)
        b.ico('Poppy' if k % 4 else 'FlowerYellow', x, y + 0.42, z, 0.09, 0.8, sub=0)


def fence(b, gaps):
    """A weathered wooden fence with two rails along the rim, with openings where the ladders and the steps come up."""
    x = -43.5
    while x < 43.5:
        if any(g0 < x < g1 for g0, g1 in gaps):
            x += 2.4
            continue
        z = wall_z(x, 21.0) - 1.3
        y = TOP - 0.15
        b.limb('WoodDark', (x, y, z), (x, y + 1.15, z), 0.06, 0.055, 5)
        xn = x + 2.4
        if not any(g0 < xn < g1 for g0, g1 in gaps) and xn < 43.5:
            zn = wall_z(xn, 21.0) - 1.3
            for hy in (0.95, 0.5):
                b.limb('Wood', (x, y + hy, z), (xn, y + hy, zn), 0.03, 0.03, 4)
        x += 2.4


def bench(b, x, z):
    y = top_y(x, z)
    b.box('Wood', x, y + 0.45, z, 1.8, 0.07, 0.5, bevel=0.02)
    b.box('Wood', x, y + 0.9, z - 0.3, 1.8, 0.4, 0.06, bevel=0.02)
    for dx in (-0.75, 0.75):
        b.box('WoodDark', x + dx, y + 0.22, z, 0.08, 0.45, 0.44)


def cliff_garden(b):
    # a row of cypresses along the back, umbrella pines and olives scattered, lavender rows, a poppy meadow, agaves on the rim, a fence and a bench
    for q in range(16):
        x = -42 + 5.4 * q + 2.0 * hash01(q * 1.7)
        z = -28.4 + 1.2 * hash01(q * 2.9)
        if clear_of_palms(x, z):
            cypress(b, x, z, 0.9 + 0.5 * hash01(q + 7))
    for x, z, s in ((-38, -22, 1.1), (-30, -26, 1.0), (-12, -24, 1.2), (4, -23, 1.1), (22, -25, 1.0), (38, -24, 1.2), (40, -15, 1.0), (-41, -12, 1.0)):
        if clear_of_palms(x, z, 4.0):
            umbrella_pine(b, x, z, s)
    for x, z, s in ((-16, -14, 1.1), (-1, -13, 1.0), (10, -17, 1.2), (26, -12, 1.0), (-33, -9.5, 1.1), (41, -8, 1.0)):
        if clear_of_palms(x, z, 3.6):
            olive(b, x, z, s)
    lavender_field(b, -41.0, -29.0, -27.0, -17.5)
    lavender_field(b, 12.0, 24.0, -27.5, -22.0)
    poppy_meadow(b, -14, 8, -26, -16, 90)
    poppy_meadow(b, 26, 42, -18, -4, 60)
    for x in (-25.0, -10.5, 7.0, 15.5, 36.0, -39.0):
        agave(b, x, wall_z(x, 21.0) - 2.4, 1.0 + 0.3 * hash01(x))
    fence(b, [(-1.6, 3.0), (19.5, 25.5), (-45.0, -44.0)])
    bench(b, -17.0, wall_z(-17.0, 21.0) - 3.2)
    b.box('WoodDark', 6.0, TOP + 0.9, wall_z(6.0, 21.0) - 3.4, 0.12, 1.8, 0.12)       # a signpost where the path meets the rim
    b.box('Wood', 6.0, TOP + 1.5, wall_z(6.0, 21.0) - 3.4, 1.1, 0.3, 0.07, bevel=0.02)


# ---------------------------------------------------------------- build
def build():
    b = locbuild.Builder()
    for k, v in COLOURS.items():
        b.mat(k, v)
    b.mat('Chalk', '#F2EFE6')
    b.mat('Paint_White', '#F2EFE6')
    block_base(b)
    wall(b)
    wall_detail(b)
    cliff_top(b)
    beaches(b)
    ledge(b, 'Ledge_8', -8.0, 8.0, 2.9)
    ledge(b, 'Ledge_15', -2.0, 15.0, 2.9)
    access(b)
    cliff_garden(b)
    house(b)
    lighthouse(b)
    stack(b, 'Spot_StackA', 12.0, 30.0, 7.0, 4.4, 2.8, 71, (12.0, 7.0, 27.45))
    stack(b, 'Spot_StackB', -32.0, 24.0, 12.0, 5.2, 3.3, 72, (-28.9, 12.0, 24.0))
    yacht(b)
    sea_rocks(b)
    return b


def add_preview_water():
    me = bpy.data.meshes.new('PreviewWater')
    ob = bpy.data.objects.new('PreviewWater', me)
    bpy.context.scene.collection.objects.link(ob)
    import bmesh
    bm = bmesh.new()
    v = [bm.verts.new(p) for p in ((-HALF, HALF, 0.0), (HALF, HALF, 0.0), (HALF, -HALF - 30, 0.0), (-HALF, -HALF - 30, 0.0))]
    # Blender y is -Unity z: the block runs from Unity z = -30 to 60, i.e. Blender y = 30 .. -60
    for q, p in enumerate(((-HALF, 30.0), (HALF, 30.0), (HALF, -60.0), (-HALF, -60.0))):
        v[q].co = (p[0], p[1], 0.0)
    bm.faces.new(v)
    bm.to_mesh(me)
    bm.free()
    m = bpy.data.materials.new('PreviewWater')
    m.use_nodes = True
    for n in m.node_tree.nodes:
        if n.type == 'BSDF_PRINCIPLED':
            n.inputs['Base Color'].default_value = (0.02, 0.35, 0.42, 1)
            n.inputs['Alpha'].default_value = 0.55
            n.inputs['Roughness'].default_value = 0.2
    try:
        m.surface_render_method = 'BLENDED'
    except Exception:
        pass
    me.materials.append(m)


def main():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    b = build()
    objs = b.finish('Loc_Cliffs')
    print('LOCATION TRIS', b.tris, 'objects', len(objs))
    b.export(OUT)
    add_preview_water()
    view.shoot('loc_cliffs_overview', (0, 6, 12), 150, (0.35, 0.55, 1.0), lens=50, res=(1200, 800))
    view.shoot('loc_cliffs_house', (10, 9, 0), 26, (0.3, 0.35, 1.0), lens=50, res=(1100, 700))
    view.shoot('loc_cliffs_lighthouse', (28, 10, 0), 34, (0.3, 0.2, 1.0), lens=50, res=(1100, 700))
    view.shoot('loc_cliffs_yacht', (-24, 3, 6), 24, (0.9, 0.5, 0.8), lens=50, res=(1100, 700))
    view.shoot('loc_cliffs_stacks', (-10, 6, 28), 70, (0.1, 0.3, 1.0), lens=50, res=(1100, 700))
    view.shoot('loc_cliffs_ledges', (-3, 11, -3), 30, (0.15, 0.15, 1.0), lens=50, res=(1100, 700))
    view.shoot('loc_cliffs_top', (0, 22, -16), 70, (0.25, 0.45, 1.0), lens=50, res=(1100, 700))
    view.shoot('loc_cliffs_beach', (-38, 1, 2), 26, (1.0, 0.35, 0.5), lens=50, res=(1100, 700))
    print('LOC DONE')


if __name__ == '__main__':
    main()
