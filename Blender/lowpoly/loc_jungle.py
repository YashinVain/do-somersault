"""Location 7, the jungle lagoon under a waterfall, as one scene (Docs/ModelBrief.md section 3.7). Output: Models/Locations/Loc_Jungle.fbx + previews.
    blender -b --python Blender/lowpoly/loc_jungle.py
Site space like the game's: the block is 90 x 90 m, x -45..45, z -30..60, water at y = 0 (not modelled), lagoon floor at -4.2. The moss wall is the very
same faceted face the game's collision boxes were fitted to (data/jungle_wall.txt, from PrototypeSceneBuilder.DumpJungle); the ground follows the game's
JungleHeight (sand at 1.0 on the beaches, where the collision blocks have their top); the dock, the ledges, the treehouse, the temple, the idol island and the
totem rock stand where the game's solid boxes are. The game keeps its own swaying jungle trees and palms (data/jungle_trees.txt says where) and the mist.
"""
import bisect
import math
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.normpath(os.path.join(HERE, '..')))
import locbuild  # noqa: E402
import view  # noqa: E402

OUT = os.path.normpath(os.path.join(HERE, '..', '..', 'Assets', '_Project', 'Models', 'Locations', 'Loc_Jungle.fbx'))
WALL_DATA = os.path.join(HERE, 'data', 'jungle_wall.txt')
TREE_DATA = os.path.join(HERE, 'data', 'jungle_trees.txt')

COLOURS = {
    'Soil': '#5A4330', 'Earth': '#6C5440', 'BlockRock': '#4E5A48',
    'RockA': '#6A7552', 'RockB': '#58643F', 'RockC': '#788460', 'RockWet': '#3F5A3C', 'RockDark': '#46523A', 'Moss': '#6E9A4A', 'MossLight': '#8DB85A',
    'CapGrass': '#3F8A3A', 'CapDark': '#2F7A33', 'Dirt': '#7A5A3A', 'Sand': '#D9C48E', 'SandWet': '#A99A6E', 'Mud': '#3E5A4A', 'MudLight': '#52705A',
    'River': '#6FD0C8', 'Fall': '#E2F3F7', 'FallBlue': '#8FD0E2', 'FallDeep': '#5FB4CC', 'Foam': '#F4FBFB',
    'TempleA': '#C9C2A2', 'TempleB': '#AEA787', 'TempleLight': '#E4DDBE', 'TempleDark': '#7D7760', 'Jade': '#3FA58F', 'TempleRed': '#B5473A',
    'Wood': '#8A5E3B', 'WoodDark': '#5E3F28', 'WoodLight': '#A87A4C', 'Thatch': '#B59A4E', 'ThatchDark': '#8C7436', 'Bark': '#6B4A32',
    'Stone': '#8D8F7C', 'StoneDark': '#6F715F', 'StoneMoss': '#7B8A64', 'StoneLight': '#A4A690', 'Gold': '#D9B04A', 'Dark': '#1E221C',
    'LeafA': '#2F7D3C', 'LeafB': '#4FA04A', 'LeafC': '#76B24A', 'LeafDark': '#245E30', 'Rope': '#C9B88A', 'Bamboo': '#A8B64F', 'BambooDark': '#7E8F36',
    'FlowerR': '#D9473A', 'FlowerY': '#F2C94C', 'FlowerP': '#C76AA8', 'FlowerO': '#F28C28', 'FlowerW': '#F4F1E8',
    'TotemRed': '#B83A2E', 'TotemTeal': '#2E8B88', 'TotemWhite': '#EDE6D2', 'Lit': '#FFB347',
    'ParrotR': '#D9382E', 'ParrotB': '#2D6FD0', 'ParrotY': '#F2C94C', 'Beak': '#2A2A2A', 'Shell': '#F0E4D0', 'Cloth': '#C9413A', 'Canoe': '#9C6B43',
}

COLS, ROWS = 24, 10
HALF = 45.0
ZBACK = -32.0
TOP = 28.0
FALL_X = 15.0


def hash01(i):
    return (math.sin(i * 12.9898) * 43758.5453) % 1.0


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def face_z(x):   # PrototypeSceneBuilder.JungleFaceZ
    return -4.0 + 1.2 * math.sin(x * 0.14) + 0.5 * math.sin(x * 0.4)


def shore_z(x):  # PrototypeSceneBuilder.JungleShoreZ
    t = max(0.0, min(1.0, (abs(x) - 18.0) / 20.0))
    return -4.0 + 14.0 * smooth(t)


def jungle_noise(x, z):
    return 0.5 * (math.sin(x * 0.15 + 1.3) * math.cos(z * 0.13 + 0.7) * 0.6 + math.sin(x * 0.31 + z * 0.23 + 2.1) * 0.4)


def jungle_height(x, z):
    shore = shore_z(x)
    t = max(0.0, min(1.0, (shore + 2.2 - z) / 3.4))
    land = smooth(t)
    return -4.2 + 5.2 * land + land * jungle_noise(x, z) * 0.2


# ---------------------------------------------------------------- the game's own trees (kept, swaying) and what is placed around them
def load_trees():
    out = []
    for line in open(TREE_DATA):
        p = line.split()
        if len(p) == 5:
            out.append((p[0], float(p[1]), float(p[2]), float(p[3]), float(p[4])))
    return out


TREES = load_trees()
GROUND_TREES = [(x, z, 2.4 * s) for n, x, y, z, s in TREES if y < 5]       # on the beaches
TOP_TREES = [(x, z, 3.2 * s) for n, x, y, z, s in TREES if y > 20]         # on the plateau


def free_of_trees(x, z, margin=0.0, top=False):
    lst = TOP_TREES if top else GROUND_TREES
    return all(math.hypot(x - tx, z - tz) > r + margin for tx, tz, r in lst)


# ---------------------------------------------------------------- the wall the physics was fitted to
def load_wall():
    pts = [tuple(float(v) for v in line.split()) for line in open(WALL_DATA) if line.strip()]
    grid = {}
    for j in range(ROWS):
        for i in range(COLS):
            base = (j * COLS + i) * 6
            grid[(i + 1, j)], grid[(i + 1, j + 1)], grid[(i, j + 1)], grid[(i, j)] = pts[base], pts[base + 1], pts[base + 2], pts[base + 5]
    cap = []
    for i in range(COLS):
        base = COLS * ROWS * 6 + i * 6
        cap.append((pts[base], pts[base + 1], pts[base + 2], pts[base + 5]))
    return grid, cap


WALL, CAP = load_wall()
WALL_NODES = list(WALL.values())


def wall_z(x, y):
    near = sorted(WALL_NODES, key=lambda p: (p[0] - x) ** 2 + ((p[1] - y) * 1.2) ** 2)[:4]
    ws = [1.0 / (0.05 + math.hypot(p[0] - x, (p[1] - y) * 1.2)) for p in near]
    return sum(p[2] * w for p, w in zip(near, ws)) / sum(ws) + relief(x, y)


def strata():
    levels = [-6.0]
    k = 0
    while levels[-1] < 30:
        levels.append(levels[-1] + 0.9 + 1.6 * hash01(k * 7.1 + 3))
        k += 1
    return levels


STRATA = strata()
PATTERN = ['RockA', 'RockB', 'RockC', 'RockA', 'RockB', 'RockDark', 'RockC', 'RockA']
BUMPS = [(-44 + 88 * hash01(k * 2.3 + 5), 1.5 + 25 * hash01(k * 4.7 + 6), 1.0 + 2.4 * hash01(k * 1.9 + 7), 0.8 + 1.8 * hash01(k * 6.1 + 8),
          0.25 + 0.5 * hash01(k * 3.9 + 9)) for k in range(90)]
# stepped cascades on the wall behind the waterfall: the sheet follows the rock and breaks on these
FLANKS = [(FALL_X - 4.2, 16.0, 1.5, 10.0, 1.2), (FALL_X + 4.4, 15.0, 1.5, 10.0, 1.2), (FALL_X - 4.0, 4.0, 1.8, 3.5, 1.1), (FALL_X + 4.2, 5.0, 1.8, 3.5, 1.1)]
CASCADES = [(FALL_X + 0.3, 21.0, 2.6, 0.7, 0.9), (FALL_X - 0.4, 15.5, 2.8, 0.7, 1.0), (FALL_X + 0.2, 10.0, 3.0, 0.8, 1.1), (FALL_X - 0.2, 5.0, 3.2, 0.8, 1.1)]


def relief(x, y):
    k = max(0, min(len(STRATA) - 2, bisect.bisect(STRATA, y) - 1))
    t = (y - STRATA[k]) / (STRATA[k + 1] - STRATA[k])
    hard = hash01(k * 3.3 + 1.0)
    r = (0.28 * hard if hard > 0.45 else -0.06) * max(0.0, math.sin(math.pi * t)) ** 0.6
    r += 0.07 * math.sin(x * 0.7 + y * 0.9)
    for bx, by, sx, sy, amp in BUMPS + CASCADES + FLANKS:
        dx, dy = (x - bx) / sx, (y - by) / sy
        d2 = dx * dx + dy * dy
        if d2 < 9.0:
            r += amp * math.exp(-d2)
    if y < 1.2:
        r += 0.3 * smooth((1.2 - y) / 3.2)
    return max(-0.15, min(1.5, r))


def wave(x, y, z):
    return y + 0.35 * math.sin(x * 0.21 + y * 0.3) + 0.22 * math.sin(x * 0.53 - y * 0.17 + z)


def wall_material(cx, cz, f):
    if f < -0.9:
        return 'RockWet'
    if f < 0.8:
        return 'RockWet' if hash01(round(cx) * 1.3) > 0.4 else 'RockDark'
    # moss grows in broad patches (a smooth field, not confetti), more on the upper parts and round the ledges
    m = math.sin(cx * 0.16 + f * 0.09 + 1.0) * math.sin(f * 0.21 - cx * 0.06 + 0.5) + 0.45 * math.sin(cx * 0.43 + f * 0.37) + 0.012 * f
    base = PATTERN[bisect.bisect(STRATA, f) % len(PATTERN)]
    if m > 0.95:
        return 'MossLight'
    if m > 0.5:
        return 'Moss'
    return base


# ---------------------------------------------------------------- small things
def tuft(b, x, y, z, s=1.0, mat='LeafB', n=4, seed=0):
    for k in range(n):
        a = k * 2.2 + hash01(seed + k) * 3
        b.limb(mat, (x + math.cos(a) * 0.08 * s, y - 0.02, z + math.sin(a) * 0.08 * s),
               (x + math.cos(a) * 0.5 * s, y + (0.5 + 0.3 * hash01(seed * 3 + k)) * s, z + math.sin(a) * 0.5 * s), 0.06 * s, 0.01, 3)


def fern(b, x, y, z, s=1.0, seed=0):
    """A fern: a fan of long arching fronds."""
    for k in range(9):
        a = k * 0.698 + hash01(seed) * 3
        ln = (1.0 + 0.5 * hash01(seed * 3 + k)) * s
        mid = (x + math.cos(a) * 0.55 * ln, y + 0.55 * ln, z + math.sin(a) * 0.55 * ln)
        end = (x + math.cos(a) * 1.15 * ln, y + 0.25 * ln, z + math.sin(a) * 1.15 * ln)
        b.limb('LeafB' if k % 2 else 'LeafA', (x, y, z), mid, 0.05 * s, 0.045 * s, 3)
        b.limb('LeafB' if k % 2 else 'LeafA', mid, end, 0.045 * s, 0.01, 3)


def banana(b, x, y, z, s=1.0, seed=0):
    """A banana plant: a short stem and big flat leaves on arching ribs."""
    b.limb('LeafC', (x, y, z), (x, y + 1.5 * s, z), 0.12 * s, 0.09 * s, 6)
    for k in range(6):
        a = k * 1.05 + hash01(seed) * 3
        tip = (x + math.cos(a) * 1.7 * s, y + 1.6 * s, z + math.sin(a) * 1.7 * s)
        base = (x, y + 1.5 * s, z)
        mid = (x + math.cos(a) * 0.9 * s, y + 2.2 * s, z + math.sin(a) * 0.9 * s)
        px, pz = -math.sin(a) * 0.4 * s, math.cos(a) * 0.4 * s
        b.skin('LeafA' if k % 2 else 'LeafB', [[base, base], [(mid[0] - px, mid[1], mid[2] - pz), (mid[0] + px, mid[1], mid[2] + pz)],
                                              [(tip[0] - px * 0.5, tip[1], tip[2] - pz * 0.5), (tip[0] + px * 0.5, tip[1], tip[2] + pz * 0.5)]],
               axis=lambda kk: (x, y + 1.0 * s, z), closed=False)
        b.skin('LeafA' if k % 2 else 'LeafB', [[base, base], [(mid[0] - px, mid[1], mid[2] - pz), (mid[0] + px, mid[1], mid[2] + pz)],
                                              [(tip[0] - px * 0.5, tip[1], tip[2] - pz * 0.5), (tip[0] + px * 0.5, tip[1], tip[2] + pz * 0.5)]],
               axis=lambda kk: (x, y + 5.0 * s, z), closed=False)


def heliconia(b, x, y, z, s=1.0, seed=0):
    for k in range(4):
        a = k * 1.6 + seed
        top = (x + math.cos(a) * 0.3 * s, y + (1.2 + 0.4 * hash01(seed + k)) * s, z + math.sin(a) * 0.3 * s)
        b.limb('LeafB', (x, y, z), top, 0.04 * s, 0.03 * s, 4)
        for j in range(3):
            t = 0.55 + 0.15 * j
            b.ico('FlowerR' if (k + j) % 2 else 'FlowerO', x + (top[0] - x) * t, y + (top[1] - y) * t, z + (top[2] - z) * t, 0.12 * s, 0.8, sub=0, jit=0.1, seed=seed + j)
        b.limb('LeafA', top, (top[0] + math.cos(a) * 0.5 * s, top[1] - 0.3 * s, top[2] + math.sin(a) * 0.5 * s), 0.05 * s, 0.01, 3)


def bamboo(b, x, y, z, s=1.0, seed=0):
    for k in range(7):
        a = k * 0.9 + seed
        px, pz = x + math.cos(a) * 0.5 * s, z + math.sin(a) * 0.5 * s
        h = (4.0 + 2.5 * hash01(seed + k)) * s
        lean = (0.3 * math.cos(a * 1.7), 0.3 * math.sin(a * 1.7))
        mat = 'Bamboo' if k % 2 else 'BambooDark'
        b.limb(mat, (px, y, pz), (px + lean[0] * s, y + h, pz + lean[1] * s), 0.07 * s, 0.05 * s, 5)
        for j in range(1, int(h)):
            b.cone('BambooDark', px + lean[0] * s * j / h, y + j, pz + lean[1] * s * j / h, 0.085 * s, 0.085 * s, 0.05, 5)
        for j in range(3):
            yy = y + h * (0.6 + 0.12 * j)
            b.limb('LeafB', (px + lean[0] * s * (yy - y) / h, yy, pz + lean[1] * s * (yy - y) / h), (px + math.cos(a + j) * 0.8 * s, yy - 0.3, pz + math.sin(a + j) * 0.8 * s), 0.03, 0.01, 3)


def vine(b, x, y_top, z, ln, seed=0, mat='LeafA'):
    """A liana hanging from y_top with leaves along it."""
    px, pz = x, z
    prev = (x, y_top, z)
    n = max(2, int(ln / 0.9))
    for k in range(n):
        t = (k + 1) / n
        cur = (x + 0.25 * math.sin(seed + k * 1.3), y_top - ln * t, z + 0.15 * math.cos(seed + k))
        b.limb('Bark', prev, cur, 0.03, 0.025, 3)
        if k % 2 == 0:
            b.ico(mat if k % 4 else 'LeafC', cur[0] + 0.15, cur[1], cur[2] + 0.1, 0.16, 0.5, sub=0, jit=0.1, seed=seed + k)
        prev = cur


def rope(b, p0, p1, sag=0.3, mat='Rope', r=0.03, n=5):
    pts = []
    for i in range(n + 1):
        t = i / n
        pts.append((p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t - sag * 4 * t * (1 - t), p0[2] + (p1[2] - p0[2]) * t))
    for a, c in zip(pts, pts[1:]):
        b.limb(mat, a, c, r, r, 4)


def rail_posts(b, pts, y, h=1.0, ropes=(0.9, 0.5), post='WoodDark', mat='Rope'):
    for (x, z) in pts:
        b.limb(post, (x, y - 0.1, z), (x, y + h, z), 0.05, 0.045, 5)
    for (x0, z0), (x1, z1) in zip(pts, pts[1:]):
        for hy in ropes:
            rope(b, (x0, y + hy, z0), (x1, y + hy, z1), 0.07, mat, 0.015, 3)


def boulder(b, mat, x, y, z, r, sc=(1.0, 0.7, 1.0), seed=0):
    b.ico(mat, x, y, z, r, 1.0, sub=1, sc=sc, jit=0.16, seed=seed)


def parrot(b, x, y, z, mat='ParrotR', yaw=0.0, s=1.0):
    c, s_ = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    b.ico(mat, x, y + 0.14 * s, z, 0.11 * s, 1.0, sub=1, sc=(0.8, 1.3, 0.8))
    b.ico(mat, x + 0.02 * s_, y + 0.32 * s, z + 0.02 * c, 0.07 * s, 1.0, sub=0)
    b.cone('Beak', x + 0.08 * s_, y + 0.31 * s, z + 0.08 * c, 0.03 * s, 0.005, 0.07 * s, 4)
    b.limb('ParrotB' if mat == 'ParrotR' else 'ParrotR', (x, y + 0.1 * s, z), (x - 0.18 * s_, y - 0.18 * s, z - 0.18 * c), 0.03 * s, 0.015 * s, 3)


# ---------------------------------------------------------------- the block, the lagoon floor and the ground
def block_base(b):
    top = -6.1
    b.box('Soil', 0, top - 0.7, 15, 90, 1.4, 90)
    b.box('Earth', 0, (top - 1.4 - 9.0) / 2, 15, 90, (top - 1.4) - (-9.0), 90)
    b.box('BlockRock', 0, (-9.0 - 16.0) / 2, 15, 90, 7.0, 90)
    b.box('Mud', 0, -5.15, 15, 90, 1.9, 90)                          # the lagoon floor, top at -4.2
    for k in range(40):
        x, z = -42 + 84 * hash01(k * 3.7 + 1), -2 + 58 * hash01(k * 6.1 + 2)
        b.ico('MudLight', x, -4.2, z, 1.0, 1.0, sub=1, sc=(2.0 + 3 * hash01(k + 9), 0.07, 0.7 + 0.5 * hash01(k + 4)), jit=0.1, seed=k)
    for k in range(20):
        x, z = -40 + 80 * hash01(k * 2.9 + 11), 3 + 52 * hash01(k * 5.3 + 12)
        boulder(b, 'RockWet', x, -4.25, z, 0.5 + 0.6 * hash01(k + 3), seed=k + 30)
        if k % 2:
            for j in range(4):
                a = hash01(k * 4 + j) * 6.28
                b.limb('LeafA', (x + math.cos(a) * 0.8, -4.2, z + math.sin(a) * 0.8), (x + math.cos(a) * 0.9, -2.6 + 0.5 * hash01(k + j), z + math.sin(a) * 0.9), 0.05, 0.015, 3)


def ground(b):
    """The beaches and the banks: the game's JungleHeight, sand along the water, wet sand and mud below it, jungle floor inland, low mounds of moss-grown rock."""
    mounds = []
    for k in range(60):
        side = -1 if k % 2 else 1
        x = side * (19.0 + 25.0 * hash01(k * 2.1 + 71))
        z = shore_z(x) - 14 * hash01(k * 3.3 + 72) + 3.0
        if z < face_z(x) + 2.5 or not free_of_trees(x, z, 1.5):
            continue
        if abs(x - 30) < 9.0 and z > -4:                    # not the temple's front
            continue
        if abs(x + 29) < 5.0 and z > -5:                    # nor the treehouse
            continue
        mounds.append((x, z, 1.1 + 1.6 * hash01(k + 73), 0.3 + 0.55 * hash01(k * 2.9 + 74)))

    def bump(x, z):
        h = 0.0
        for mx, mz, r, amp in mounds:
            d = math.hypot(x - mx, z - mz) / r
            if d < 1.0:
                h += amp * (1 - d * d) ** 2.0
        return h

    def hf(x, z):
        h = jungle_height(x, z)
        if h > 0.9:
            h += bump(x, z)
        return h

    def skip(cx, cz):
        return all(jungle_height(cx + dx, cz + dz) < -4.1 for dx in (-0.8, 0.8) for dz in (-0.8, 0.8)) and jungle_height(cx, cz) < -4.1

    def choose(cx, cz, f):
        if f < -0.4:
            return 'Mud' if f < -1.5 else 'MudLight'
        d = cz - shore_z(cx)
        bp = bump(cx, cz)
        if f > 0.9 and bp > 0.42:
            return 'RockA' if hash01(round(cx) * 2.3 + round(cz)) > 0.5 else 'RockC'
        if f > 0.9 and bp > 0.24:
            return 'Moss'
        if f < 0.45:
            return 'SandWet'
        if d > -4.5:
            return 'Sand'
        if d > -6.0:
            return 'Dirt'
        v = hash01(round(cx * 0.8) * 2.3 + round(cz * 0.8))
        return 'CapDark' if v > 0.6 else 'CapGrass'
    splits = [(lambda x, y, z: y, [-1.5, -0.4, 0.45]), (lambda x, y, z: z - shore_z(x), [-6.0, -4.5])]
    b.terrain(-45.0, 45.0, -6.0, 48.0, 1.5, hf, choose, skip=skip, splits=splits)
    return hf


# ---------------------------------------------------------------- the wall, its cap with the river, the waterfall
def river_x(z):
    return 17.2 - 0.0 * z + 0.0 if z > -4 else FALL_X + 3.2 * smooth(min(1.0, (-4.0 - z) / 22.0)) * (1.0 + 0.2 * math.sin(z * 0.3)) + 0.4 * math.sin(z * 0.45)


def wall(b):
    splits = [(wave, [-0.9, 0.8] + STRATA[1:-1])]
    n = 3

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
                        b._terrain_cut([tri[0], tri[2], tri[1]], splits, wall_material)
    rows, halves = 14, 2
    mounds = []
    for k in range(70):
        x, z = -42 + 84 * hash01(k * 1.9 + 41), -29 + 22 * hash01(k * 2.7 + 42)
        if z > face_z(x) - 2.0 or not free_of_trees(x, z, 0.8, top=True) or abs(x - river_x(z)) < 2.4:
            continue
        mounds.append((x, z, 1.4 + 2.0 * hash01(k + 43), 0.3 + 0.6 * hash01(k * 3.1 + 44)))

    def cap_bump(x, z):
        h = 0.0
        for mx, mz, r, amp in mounds:
            d = math.hypot(x - mx, z - mz) / r
            if d < 1.0:
                h += amp * (1 - d * d) ** 2.0
        return h

    def cap_pt(f0, b0, f1, b1, u, r):
        t = r / rows
        fx = [f0[k] + (f1[k] - f0[k]) * u for k in range(3)]
        bk = [b0[k] + (b1[k] - b0[k]) * u for k in range(3)]
        x, y, z = fx[0] + (bk[0] - fx[0]) * t, fx[1] + (bk[1] - fx[1]) * t, fx[2] + (bk[2] - fx[2]) * t
        if r:
            y += 0.1 * math.sin(x * 0.27 + z * 0.4) * smooth(t * 2.0) + cap_bump(x, z) * smooth(t * 5.0)
            dr = abs(x - river_x(z))
            if dr < 2.6:
                y -= 0.42 * smooth((2.6 - dr) / 1.6) * smooth((-z - 4.0) / 3.0)                # the river bed sinks a little
        return (x, y, z)

    def cap_material(cx, cz, f):
        dr = abs(cx - river_x(cz))
        wide = 0.9 + 1.5 * smooth((cz + 14.0) / 10.0)
        if dr < wide and cz < face_z(cx) - 1.0:
            return 'River'
        if dr < 1.5 and cz < face_z(cx) - 1.0:
            return 'MudLight'
        bp = cap_bump(cx, cz)
        if bp > 0.6:
            return 'RockA' if hash01(round(cx * 1.3) + round(cz)) > 0.5 else 'RockC'
        if bp > 0.35:
            return 'Moss'
        v = hash01(round(cx * 0.9) * 1.7 + round(cz * 0.9) * 3.1)
        return 'CapDark' if v > 0.55 else 'CapGrass'
    cap_split = [(lambda x, y, z: x - river_x(z), [-1.5, -0.9, 0.9, 1.5])]
    for b0, f0, f1, b1 in CAP:
        for h in range(halves):
            u0, u1 = h / halves, (h + 1) / halves
            for r in range(rows):
                l0, r0 = cap_pt(f0, b0, f1, b1, u0, r), cap_pt(f0, b0, f1, b1, u1, r)
                l1, r1 = cap_pt(f0, b0, f1, b1, u0, r + 1), cap_pt(f0, b0, f1, b1, u1, r + 1)
                for tri in ((l1, l0, r0), (l1, r0, r1)):
                    b._terrain_cut([tri[0], tri[2], tri[1]], cap_split, cap_material)
    for side in (-1, 1):
        zf = face_z(side * HALF) - 0.5
        y = -6.0
        k = 0
        while y < TOP:
            h = min(STRATA[k + 1] - y, TOP - y)
            b.box(PATTERN[k % len(PATTERN)], side * (HALF - 0.3), y + h / 2, (zf + ZBACK) / 2, 0.6, h, zf - ZBACK)
            y += h
            k += 1
        b.box('CapDark', side * (HALF - 0.3), TOP + 0.05, (zf + ZBACK) / 2, 0.62, 0.1, zf - ZBACK)
    y = -6.0
    k = 0
    while y < TOP:
        h = min(STRATA[k + 1] - y, TOP - y)
        b.box(PATTERN[k % len(PATTERN)], 0, y + h / 2, ZBACK + 0.3, 90, h, 0.6)
        y += h
        k += 1
    b.box('CapDark', 0, TOP + 0.05, ZBACK + 0.3, 90, 0.1, 0.62)
    return cap_bump


def waterfall(b):
    """Water falling down a rocky gorge, read from far away on the map: a wide smooth curved sheet in two layers (deep blue under, white over), steps with pools and
    plenty of white water, a broad turquoise plunge pool with a ring of foam and boulders. Few, large shapes, no thin shimmering strips."""
    x0 = FALL_X
    ys = [TOP - 0.2 - 1.5 * i for i in range(19)] + [0.8, 0.3]
    ys = sorted(set(round(y, 2) for y in ys if y > 0.2), reverse=True)

    def sheet(width_top, width_bot, off, mat, nq, bulge=0.0):
        rings = []
        for k, y in enumerate(ys):
            t = 1 - y / TOP
            wd = width_top + (width_bot - width_top) * t
            ring = []
            for q in range(nq):
                u = (q / (nq - 1) - 0.5) * wd
                edge = 1.0 - (abs(u) / (wd / 2)) ** 3
                z = wall_z(x0 + u, y) + off + bulge * math.sin(math.pi * min(1.0, t * 1.15)) * edge + 0.05 * math.sin(y * 0.9 + q * 1.3)
                ring.append((x0 + u, y, z))
            rings.append(ring)
        b.skin(mat, rings, axis=lambda k: (x0, ys[k], wall_z(x0, ys[k]) - 3.0), closed=False)
    sheet(6.2, 7.6, 0.25, 'FallDeep', 9, 0.15)                                       # deep blue body of the water
    sheet(5.0, 6.6, 0.42, 'FallBlue', 7, 0.3)                                        # a paler layer
    sheet(3.4, 4.8, 0.6, 'Fall', 5, 0.4)                                             # white water down the middle, narrower and thicker
    for y in (21.0, 15.5, 10.0, 5.0):                                                # steps: a pool on each with white water breaking over its lip
        zc = wall_z(x0, y) + 0.55
        b.ico('River', x0, y + 0.02, zc, 2.2, 0.5, sub=1, sc=(1.0, 0.12, 0.45), jit=0.06, seed=int(y))
        for k in range(5):
            u = (k / 4 - 0.5) * 5.4
            b.ico('Foam', x0 + u, y - 0.35, wall_z(x0 + u, y - 0.4) + 0.75, 0.55 + 0.25 * hash01(k + y), 0.7, sub=1, sc=(1.2, 0.7, 0.8), jit=0.18, seed=int(y * 3) + k)
    zb = wall_z(x0, 0.4)
    b.ico('River', x0 - 0.3, -0.05, zb + 2.8, 5.2, 0.4, sub=2, sc=(1.0, 0.04, 0.8), jit=0.03, seed=1)      # the plunge pool, brighter than the lagoon
    for k in range(26):                                                              # a broad ring of foam and spray where it lands
        a = k * 0.24
        r = 2.4 + 1.8 * hash01(k + 5)
        b.ico('Foam', x0 - 0.3 + math.cos(a) * r * 1.5, 0.1, zb + 2.8 + math.sin(a) * r * 0.8 + 0.4, 0.5 + 0.35 * hash01(k), 0.25, sub=1, sc=(1.3, 0.35, 1.0), jit=0.2, seed=k + 60)
    for k in range(9):                                                               # a boiling heap of white water right under the fall
        b.ico('Foam', x0 + (hash01(k * 3.3) - 0.5) * 4.6, 0.6 + 1.1 * hash01(k), zb + 0.7 + 1.2 * hash01(k + 9), 0.8 + 0.5 * hash01(k + 2), 0.75, sub=1, jit=0.2, seed=k + 70)
    for k in range(9):
        a = 0.2 + k * 0.38
        boulder(b, 'RockWet' if k % 2 else 'RockB', x0 + math.cos(a) * 5.4 - 0.6, -0.5, zb + 1.2 + math.sin(a) * 3.4 + 0.6, 0.9 + 0.6 * hash01(k + 2), (1.2, 0.7, 1.1), seed=k + 80)
        b.ico('Moss', x0 + math.cos(a) * 5.4 - 0.6, -0.1, zb + 1.2 + math.sin(a) * 3.4 + 0.6, 0.6, 0.4, sub=1, sc=(1.2, 0.5, 1.1), jit=0.15, seed=k + 90)
    zl = face_z(x0) + 0.6                                                            # the river on top widens into the lip: a broad stream between mossy stones
    for k in range(11):
        b.ico('River', x0 - 2.9 + k * 0.58, TOP - 0.05, zl - 0.5, 0.5, 0.4, sub=1, sc=(1.2, 0.25, 1.5), jit=0.1, seed=k)
    for k in range(4):
        boulder(b, 'RockWet' if k % 2 else 'RockB', x0 + (-3.6 if k % 2 else 3.6), TOP + 0.1, zl - 1.5 - k, 0.8, (1.1, 0.7, 1.0), seed=k + 100)
    for sx in (-5.6, 5.8):                                                           # ferns up the sides of the gorge
        for k in range(7):
            y = 2 + 3.2 * k
            fern(b, x0 + sx + 0.3 * math.sin(k), y, wall_z(x0 + sx, y) + 0.2, 0.9 + 0.3 * hash01(k), seed=k + int(sx))


def wall_life(b, cap_bump):
    # vines and roots hanging from the plateau lip, moss patches, ferns and bromeliads on the ledges of the wall, hanging orchids
    for k in range(44):
        x = -43 + 86 * hash01(k * 2.7 + 11)
        if abs(x - FALL_X) < 3.0:
            continue
        vine(b, x, TOP - 0.3, wall_z(x, TOP - 1.0) + 0.3, 5 + 9 * hash01(k + 3), seed=k)
    for k in range(70):
        x = -44 + 88 * hash01(k * 1.9 + 3)
        y = 2 + 24 * hash01(k * 3.7 + 4)
        if abs(x - FALL_X) < 3.0 and y > 0:
            continue
        zz = wall_z(x, y)
        if k % 3 == 0:
            fern(b, x, y, zz + 0.1, 0.7 + 0.4 * hash01(k), seed=k)
        elif k % 3 == 1:
            b.ico('LeafA', x, y + 0.2, zz + 0.12, 0.4, 0.6, sub=0, sc=(1.4, 0.7, 0.7), jit=0.12, seed=k)
            b.ico(('FlowerR', 'FlowerY', 'FlowerP', 'FlowerO')[k % 4], x, y + 0.45, zz + 0.2, 0.1, 1.0, sub=0)
        else:
            tuft(b, x, y, zz + 0.12, 1.0, 'LeafC', 4, seed=k)


# ---------------------------------------------------------------- ledges (collision: WallLedge, 6 m at x = -3 and 24 m at x = 7.5)
def ledge(b, name, x, h, reach):
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
    b.skin('MossLight', [rings[0], [(x, h, cz0)] * n], axis=lambda k: (x, h - 3.0, cz0), closed=True)
    for k, mat in enumerate(('Moss', 'RockA', 'RockB', 'RockC', 'RockB')):
        b.skin(mat, [rings[k], rings[k + 1]], closed=True)
    for side in (-1, 1):                                                                       # bamboo rails along the sides
        for q, zz in enumerate((fz - 0.4, fz + 0.5, fz + 1.4)):
            px = x + side * 1.75
            b.limb('Bamboo', (px, h - 0.05, zz), (px, h + 1.05, zz), 0.05, 0.045, 5)
            if q:
                for hy in (0.95, 0.55):
                    b.limb('Bamboo', (px, h + hy, zz - 0.9), (px, h + hy - 0.02, zz), 0.03, 0.03, 4)
    for dx in (-1.3, 1.2):
        fern(b, x + dx, h, fz + 0.3, 0.8, seed=int(x + dx))
    flowers_at = ((x + 1.0, fz + 0.2, 'FlowerR'), (x - 0.9, fz + 0.15, 'FlowerY'))
    for fx_, fz_, m in flowers_at:
        b.limb('LeafB', (fx_, h, fz_), (fx_, h + 0.35, fz_), 0.015, 0.012, 3)
        b.ico(m, fx_, h + 0.38, fz_, 0.08, 0.8, sub=0)
    b.spot('Spot_' + name, x, h, fz + reach)


def ladder(b, x, y0, y1):
    for dx in (-0.3, 0.3):
        y = y0
        while y < y1 - 0.01:
            ya = min(y + 1.0, y1)
            b.limb('Bamboo', (x + dx, y, wall_z(x + dx, y) + 0.3), (x + dx, ya, wall_z(x + dx, ya) + 0.3), 0.045, 0.045, 4)
            y = ya
    y = y0 + 0.3
    while y < y1:
        b.limb('BambooDark', (x - 0.3, y, wall_z(x - 0.3, y) + 0.3), (x + 0.3, y, wall_z(x + 0.3, y) + 0.3), 0.03, 0.03, 4)
        y += 0.38


def stairs(b, x0, y0, x1, y1, n):
    run = abs(x1 - x0) / n
    rise = (y1 - y0) / n
    for i in range(n + 1):
        t = i / n
        x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        zw = wall_z(x, y)
        b.box('WoodLight', x, y - 0.1, zw + 0.5, max(run * 1.1, 0.5), 0.16, 1.4, bevel=0.03)
        b.box('Stone', x, y - 0.22 - abs(rise) * 0.5, zw + 0.25, max(run * 1.1, 0.5), abs(rise) + 0.4, 1.0, bevel=0.04)
        if i % 2 == 0:
            b.limb('Bamboo', (x, y - 0.1, zw + 1.15), (x, y + 1.0, zw + 1.15), 0.04, 0.035, 5)
    for hy in (0.95, 0.5):
        for i in range(n):
            ta, tb = i / n, (i + 1) / n
            xa, ya = x0 + (x1 - x0) * ta, y0 + (y1 - y0) * ta
            xb, yb = x0 + (x1 - x0) * tb, y0 + (y1 - y0) * tb
            b.limb('Rope', (xa, ya + hy, wall_z(xa, ya) + 1.15), (xb, yb + hy, wall_z(xb, yb) + 1.15), 0.014, 0.014, 3)


def access(b):
    """From the dock along the foot of the wall by stone steps to the ledge at 6 m; ladders and landings hung on ropes up to the lip of the falls."""
    stairs(b, -10.4, 1.3, -4.9, 5.9, 14)
    def landing(x, y):
        zw = wall_z(x, y)
        b.box('WoodLight', x, y - 0.08, zw + 0.7, 2.0, 0.14, 1.6, bevel=0.03)
        for dx in (-0.85, 0.85):
            b.limb('Rope', (x + dx, y - 0.1, zw + 1.3), (x + dx * 0.5, y + 2.4, zw + 0.15), 0.025, 0.02, 3)
            b.limb('Bamboo', (x + dx, y, zw + 1.45), (x + dx, y + 1.0, zw + 1.45), 0.04, 0.035, 5)
        b.limb('Rope', (x - 0.85, y + 0.9, zw + 1.45), (x + 0.85, y + 0.9, zw + 1.45), 0.015, 0.015, 3)
    ladder(b, -1.0, 5.9, 11.0)
    landing(-1.0, 11.0)
    ladder(b, 2.4, 11.0, 16.4)
    landing(2.4, 16.4)
    ladder(b, 5.6, 16.4, 21.4)
    landing(5.6, 21.4)
    stairs(b, 6.0, 21.5, 6.2, 23.9, 6)
    # to the top of the plateau from the falls ledge
    ladder(b, 9.6, 24.0, 28.6)


# ---------------------------------------------------------------- the dock (collision: 3.4 wide, z -3.4..1 at top 1.2) and the canoes
def canoe(b, x, z, yaw, length=4.2, mat='Canoe'):
    c, s_ = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))

    def P(u, y, v):
        return (x + u * c + v * s_, y, z - u * s_ + v * c)
    half = length / 2
    stations = [-half, -half * 0.88, -half * 0.6, -half * 0.25, half * 0.25, half * 0.6, half * 0.88, half]

    def prof(v):
        t = abs(v) / half
        return 0.42 * (1 - t ** 2.0) ** 0.5 + 0.03, 0.34 + 0.38 * t ** 2.2
    rings, inner = [], []
    for v in stations:
        w, sheer = prof(v)
        keel = -0.12 + 0.3 * (abs(v) / half) ** 3
        rings.append([P(-w, sheer, v), P(-w * 0.9, 0.1, v), P(0, keel, v), P(w * 0.9, 0.1, v), P(w, sheer, v)])
        wi = w - 0.06
        inner.append([P(-wi, sheer - 0.02, v), P(-wi * 0.85, 0.14, v), P(0, keel + 0.08, v), P(wi * 0.85, 0.14, v), P(wi, sheer - 0.02, v)])
    b.skin(mat, rings, axis=lambda k: P(0, 0.3, stations[k]), closed=False)
    b.skin('WoodDark', inner, axis=lambda k: P(0, 0.3, stations[k]), closed=False, flip=True)
    for sgn in (-1, 1):
        for i in range(len(stations) - 1):
            v0, v1 = stations[i], stations[i + 1]
            b.limb('WoodDark', P(sgn * prof(v0)[0], prof(v0)[1] + 0.01, v0), P(sgn * prof(v1)[0], prof(v1)[1] + 0.01, v1), 0.03, 0.03, 4)
    for v in (-half * 0.4, half * 0.35):
        w, _ = prof(v)
        px, py, pz = P(0, 0.3, v)
        b.box('Wood', px, py, pz, 2 * w * 0.95, 0.05, 0.22, rot_y=yaw)
    for side, col in ((-1, 'TotemRed'), (1, 'TotemTeal')):                                       # painted prow and stern
        px, py, pz = P(0, 0.62, side * (half - 0.1))
        b.box(col, px, py, pz, 0.16, 0.22, 0.16, rot_y=yaw)
    px, py, pz = P(0.3, 0.45, half * 0.1)
    qx, qy, qz = P(-0.9, 0.2, half * 0.1 + 0.2)
    b.limb('Wood', (px, py + 0.5, pz), (qx, qy, qz), 0.025, 0.04, 4)


def dock(b):
    dx, top = -12.0, 1.2
    z = -3.4
    k = 0
    while z < 1.0 - 0.05:
        b.box('WoodLight' if k % 3 == 0 else 'Wood', dx, top - 0.03, z + 0.13, 3.4, 0.06, 0.26)
        z += 0.3
        k += 1
    for sx in (-1.5, 1.5):
        b.box('WoodDark', dx + sx, top - 0.15, -1.2, 0.18, 0.2, 4.4)                           # stringers
    for px in (dx - 1.5, dx + 1.5):
        for pz in (-2.8, 0.4):
            b.limb('WoodDark', (px, -4.2, pz), (px, top + 0.35, pz), 0.12, 0.1, 6)
            b.cone('Rope', px, top + 0.2, pz, 0.12, 0.1, 0.1, 6)
    for side in (-1, 1):                                                                       # posts and ropes along the sides, open at the end for the dive
        pts = [(dx + side * 1.6, -3.2 + 1.3 * q) for q in range(4)]
        rail_posts(b, pts, top, 1.0)
    b.limb('WoodDark', (dx + 1.4, top, -3.1), (dx + 1.4, top + 2.2, -3.1), 0.05, 0.045, 5)    # a lantern post
    b.box('Lit', dx + 1.4, top + 2.05, -3.1, 0.2, 0.26, 0.2)
    b.cone('Thatch', dx + 1.4, top + 2.2, -3.1, 0.22, 0.02, 0.2, 6)
    b.cone('WoodDark', dx - 1.2, top, -3.0, 0.35, 0.3, 0.8, 8)                                 # barrels and baskets
    b.cone('WoodDark', dx - 0.6, top, -3.1, 0.3, 0.28, 0.7, 8)
    for q in range(3):
        b.ico('LeafC', dx - 1.1 + q * 0.2, top + 0.85, -3.0, 0.14, 1.0, sub=0)
    b.cone('Rope', dx + 1.0, top, -2.0, 0.3, 0.26, 0.2, 8)
    b.box('Cloth', dx - 0.9, top + 0.1, -1.6, 0.9, 0.06, 0.6, rot_y=15)                         # a net spread to dry
    canoe(b, dx + 3.2, -1.0, 98.0, 4.2)
    rope(b, (dx + 1.7, top + 0.2, -0.7), (dx + 3.0, 0.5, -2.5), 0.2, 'Rope', 0.025, 4)
    canoe(b, dx - 4.0, -2.4, 78.0, 3.6, 'WoodLight')
    boulder(b, 'RockB', dx, 0.4, -4.0, 1.5, (1.8, 0.9, 1.0), 5)                                 # a rock landing where the dock meets the wall
    b.spot('Spot_Dock', dx, top, 1.0)


# ---------------------------------------------------------------- the treehouse (collision: trunk 2 x 20 x 2, floor 5.4 x 0.3 x 4.6 at 10.85, posts, hut)
def treehouse(b):
    tx, tz = -29.0, -2.8
    # the trunk: stations from the roots to the crown, faceted, with buttress roots
    rings = []
    ys = [0.2 + 1.6 * i for i in range(13)] + [20.4]
    for k, y in enumerate(ys):
        t = (y - 0.2) / 20.2
        r = 1.25 - 0.45 * t + 1.1 * (1 - t) ** 6
        ring = []
        for i in range(10):
            a = 2 * math.pi * i / 10
            j = 1.0 + (hash01(k * 3.3 + i * 1.7) - 0.5) * 0.14
            ring.append((tx + math.cos(a) * r * j, y, tz + math.sin(a) * r * j))
        rings.append(ring)
    b.skin('Bark', rings, closed=True, cap_a=True, cap_b=True)
    for k in range(5):
        a = k * 1.26 + 0.4
        b.limb('Bark', (tx + math.cos(a) * 0.7, 3.0, tz + math.sin(a) * 0.7), (tx + math.cos(a) * 2.6, -0.1, tz + math.sin(a) * 2.6), 0.28, 0.1, 6)
        b.limb('Bark', (tx + math.cos(a) * 0.5, 4.5, tz + math.sin(a) * 0.5), (tx + math.cos(a) * 1.6, 1.4, tz + math.sin(a) * 1.6), 0.18, 0.1, 5)
    for k in range(5):
        vine(b, tx + math.cos(k * 1.7) * 1.2, 19.5 - k, tz + math.sin(k * 1.7) * 1.2, 8 + 3 * k, seed=k + 5)
    # the crown: big rounded masses of leaves, a few branches
    for k, (dx, dy, dz, r) in enumerate(((0, 21.4, 0, 4.3), (2.6, 20.8, 1.6, 3.2), (-2.8, 21.0, -1.2, 3.4), (0.8, 23.0, -0.6, 3.0), (-1.2, 21.2, 2.6, 3.0), (2.2, 21.8, -2.4, 3.0),
                                         (-3.2, 22.4, 1.2, 2.6))):
        b.ico('LeafB' if k % 2 else 'LeafA', tx + dx, dy, tz + dz, r, 0.8, sub=2, jit=0.12, seed=k + 3)
    for a in (0.5, 2.3, 4.1):
        b.limb('Bark', (tx, 17.5, tz), (tx + math.cos(a) * 3.0, 20.4, tz + math.sin(a) * 3.0), 0.25, 0.14, 6)
    # the platform: planks on beams, two stilts to the ground with braces, beams to the trunk
    py = 10.85
    fz_c = -0.6
    for q in range(23):
        b.box('WoodLight' if q % 3 == 0 else 'Wood', tx, py, fz_c - 2.3 + 0.2 + q * 0.2, 5.4, 0.28, 0.19)
    for dz in (-2.1, 0.0, 2.0):
        b.box('WoodDark', tx, py - 0.28, fz_c + dz, 5.6, 0.22, 0.22)
    for sx in (-1, 1):
        b.box('WoodDark', tx + sx * 2.75, py - 0.28, fz_c, 0.22, 0.22, 4.8)
    for px in (tx - 2.2, tx + 2.2):
        b.limb('WoodDark', (px, 0.9, 1.5), (px, py - 0.15, 1.5), 0.2, 0.17, 7)
        b.limb('WoodDark', (px, 4.5, 1.5), (px + (-1 if px < tx else 1) * 1.6, 1.0, 2.4), 0.07, 0.06, 4)
        b.limb('WoodDark', (px, 7.5, 1.5), (px + (1 if px < tx else -1) * 1.8, 4.2, 1.5), 0.07, 0.06, 4)
        b.cone('Stone', px, 0.7, 1.5, 0.4, 0.3, 0.3, 6)
    for dz in (-1.5, 1.5):
        b.limb('WoodDark', (tx, py - 0.4, tz + 1.3 + dz * 0.2), (tx + (2.4 if dz > 0 else -2.4), py - 0.3, fz_c), 0.08, 0.08, 4)
    # rope rails: sides and the back, open at the front where one dives; bamboo posts
    posts = []
    for sx in (-1, 1):
        for q in range(5):
            zz = fz_c - 2.1 + q * 1.0
            posts.append((tx + sx * 2.6, zz))
            b.limb('Bamboo', (tx + sx * 2.6, py, zz), (tx + sx * 2.6, py + 1.1, zz), 0.045, 0.04, 5)
    for sx in (-1, 1):
        for hy in (1.0, 0.55):
            rope(b, (tx + sx * 2.6, py + hy, fz_c - 2.1), (tx + sx * 2.6, py + hy, fz_c + 1.9), 0.06, 'Rope', 0.016, 4)
    for q in range(6):
        b.limb('Bamboo', (tx - 2.6 + q * 1.04, py, fz_c - 2.2), (tx - 2.6 + q * 1.04, py + 1.1, fz_c - 2.2), 0.045, 0.04, 5)
    for hy in (1.0, 0.55):
        rope(b, (tx - 2.6, py + hy, fz_c - 2.2), (tx + 2.6, py + hy, fz_c - 2.2), 0.08, 'Rope', 0.016, 4)
    # the hut: bamboo walls, a door, a round window, a hipped thatched roof, a lantern; hammock inside the door
    hx, hz = tx, -1.8
    b.box('Wood', hx, 12.35, hz, 3.6, 2.8, 2.4, bevel=0.03)
    for q in range(18):
        b.box('WoodDark', hx - 1.7 + q * 0.2, 12.35, hz + 1.22, 0.04, 2.7, 0.03)
    b.box('WoodDark', hx - 0.8, 11.85, hz + 1.23, 0.95, 1.8, 0.06)
    b.box('Dark', hx - 0.8, 11.8, hz + 1.26, 0.8, 1.65, 0.03)
    b.limb('Dark', (hx + 0.9, 12.6, hz + 1.22), (hx + 0.9, 12.6, hz + 1.28), 0.38, 0.38, 10)
    b.limb('WoodLight', (hx + 0.9, 12.6, hz + 1.2), (hx + 0.9, 12.6, hz + 1.25), 0.46, 0.46, 10)
    b.limb('Dark', (hx + 0.9, 12.6, hz + 1.24), (hx + 0.9, 12.6, hz + 1.3), 0.34, 0.34, 10)
    ry = 13.75
    for dz, mat in ((1.95, 'Thatch'), (-1.95, 'Thatch')):
        b.skin(mat, [[(hx - 2.7, ry - 0.1, hz + dz), (hx + 2.7, ry - 0.1, hz + dz)], [(hx - 0.4, ry + 2.3, hz), (hx + 0.4, ry + 2.3, hz)]], axis=lambda k: (hx, ry - 2.0, hz), closed=False)
    for sx in (-1, 1):
        b.skin('Thatch', [[(hx + sx * 2.7, ry - 0.1, hz - 1.95), (hx + sx * 2.7, ry - 0.1, hz + 1.95)], [(hx + sx * 0.4, ry + 2.3, hz), (hx + sx * 0.4, ry + 2.3, hz)]], axis=lambda k: (hx, ry - 2.0, hz), closed=False)
    for q in range(1, 6):
        t = q / 6
        for dz in (1.95, -1.95):
            b.limb('ThatchDark', (hx - 2.7 + 2.3 * t, ry - 0.1 + 2.4 * t + 0.04, hz + dz * (1 - t)), (hx + 2.7 - 2.3 * t, ry - 0.1 + 2.4 * t + 0.04, hz + dz * (1 - t)), 0.04, 0.04, 3)
    b.limb('Bamboo', (hx - 0.6, ry + 2.35, hz), (hx + 0.6, ry + 2.35, hz), 0.06, 0.06, 5)
    b.limb('WoodDark', (hx + 1.6, 11.0, hz + 1.5), (hx + 1.6, 11.9, hz + 1.5), 0.04, 0.04, 4)
    b.box('Lit', hx + 1.6, 11.85, hz + 1.5, 0.18, 0.24, 0.18)
    rope(b, (hx - 2.0, 11.9, hz + 2.1), (hx + 0.3, 11.9, hz + 2.1), 0.45, 'Rope', 0.03, 5)
    parrot(b, hx + 2.6, py + 1.1, fz_c - 1.0, 'ParrotR', 40)
    parrot(b, hx - 2.6, py + 1.1, fz_c + 0.2, 'ParrotB', -50)
    # a rope ladder with wooden rungs down the side to the beach
    for dz in (-0.25, 0.25):
        b.limb('Rope', (tx + 3.0, py, fz_c + 1.4 + dz), (tx + 3.0, 1.0, fz_c + 1.7 + dz), 0.02, 0.02, 3)
    q = 0
    while 1.3 + q * 0.42 < py - 0.3:
        y = 1.3 + q * 0.42
        t = (y - 1.0) / (py - 1.0)
        b.box('Wood', tx + 3.0, y, fz_c + 1.7 - 0.3 * t, 0.04, 0.05, 0.55)
        q += 1
    b.spot('Spot_Treehouse', tx, 11.0, 1.7)


# ---------------------------------------------------------------- the temple (collision: tiers 14/11/8.2/5.6, shrine 4.2 x 2.8 x 2.2)
def chac_mask(b, x, y, z, s=1.0, mat='TempleLight'):
    """A big carved mask on a tier face: a block, jade eyes, a nose, a wide mouth with fangs, ear plugs, a crest."""
    b.box(mat, x, y, z, 1.3 * s, 1.5 * s, 0.4 * s, bevel=0.05)
    b.box('TempleDark', x, y + 0.82 * s, z + 0.02, 1.5 * s, 0.22 * s, 0.4 * s, bevel=0.03)
    for sg in (-1, 1):
        b.box('Jade', x + sg * 0.34 * s, y + 0.3 * s, z + 0.22 * s, 0.34 * s, 0.26 * s, 0.06)
        b.box('Dark', x + sg * 0.34 * s, y + 0.3 * s, z + 0.26 * s, 0.14 * s, 0.14 * s, 0.04)
        b.limb('Jade', (x + sg * 0.74 * s, y + 0.1 * s, z + 0.05), (x + sg * 0.82 * s, y + 0.1 * s, z + 0.05), 0.2 * s, 0.2 * s, 8)
        b.box('TempleLight', x + sg * 0.22 * s, y - 0.5 * s, z + 0.24 * s, 0.14 * s, 0.34 * s, 0.1)
    b.box('TempleB', x, y - 0.02 * s, z + 0.3 * s, 0.34 * s, 0.5 * s, 0.2 * s, bevel=0.03)
    b.box('Dark', x, y - 0.42 * s, z + 0.23 * s, 0.9 * s, 0.22 * s, 0.06)
    for k in range(4):
        b.box('TempleLight', x - 0.3 * s + k * 0.2 * s, y - 0.42 * s, z + 0.27 * s, 0.1 * s, 0.14 * s, 0.05)
    b.box('Gold', x, y + 0.98 * s, z + 0.1, 1.0 * s, 0.1 * s, 0.3 * s)


def banner(b, x, y, z, ln=2.2):
    """A red cloth hanging from a gold bar down a tier face."""
    b.box('Gold', x, y, z + 0.14, 0.9, 0.08, 0.1)
    b.box('TempleRed', x, y - ln / 2, z + 0.12, 0.7, ln, 0.04)
    b.box('Gold', x, y - 0.35, z + 0.15, 0.7, 0.08, 0.02)
    b.tri('Gold', (x - 0.12, y - ln + 0.0, z + 0.15), (x + 0.12, y - ln, z + 0.15), (x, y - ln - 0.3, z + 0.15))
    b.box('Jade', x, y - ln / 2 - 0.1, z + 0.15, 0.18, 0.5, 0.02)


def temple(b):
    tmx = 30.0
    tiers = [(14.0, 12.0, 4.5), (11.0, 10.0, 3.3), (8.2, 8.0, 2.1), (5.6, 6.0, 0.9)]
    y0, tier_h = 1.2, 3.7
    # the foundation: a block of dressed stone from the lagoon floor up to the first tier, wider than the tier, with a landing and steps down into the water
    b.box('TempleDark', tmx, (y0 - 4.3) / 2, -0.6, 15.2, y0 + 4.3, 13.6)
    b.box('TempleB', tmx, y0 - 0.55, -0.6, 14.8, 1.1, 13.4, bevel=0.05)
    for q in range(int(15.2 / 1.9) + 1):
        b.box('TempleA', tmx - 7.6 + q * 1.9, y0 - 0.55, 6.15, 0.1, 1.1, 0.06)
    for sx in (-1, 1):
        for q in range(int(13.6 / 1.9) + 1):
            b.box('TempleA', tmx + sx * 7.65, y0 - 0.55, -7.4 + q * 1.9, 0.06, 1.1, 0.1)
    for k in range(30):                                                              # weed and wet stone at the waterline
        a = k * 0.21
        b.ico('Moss' if k % 2 else 'RockWet', tmx + (hash01(k * 2.7) - 0.5) * 15.0, -0.2 - 0.3 * hash01(k), 6.2, 0.4 + 0.25 * hash01(k + 3), 0.4, sub=0, sc=(1.8, 0.8, 0.6))
    for q in range(5):                                                               # steps from the foundation down into the lagoon, in front of the main stairs
        b.box('TempleLight', tmx, 1.1 - q * 0.38, 6.15 + 0.4 + q * 0.5, 3.4, 0.2, 0.55, bevel=0.02)
        b.box('TempleB', tmx, 0.55 - q * 0.38, 6.15 + 0.35 + q * 0.5, 3.3, 1.2, 0.5)
    for sx in (-1, 1):
        for pz in (6.4, 7.9):
            b.limb('WoodDark', (tmx + sx * 2.3, -4.0, pz), (tmx + sx * 2.3, 1.6, pz), 0.11, 0.09, 6)
            b.cone('Gold', tmx + sx * 2.3, 1.5, pz, 0.14, 0.1, 0.12, 6)
        rope(b, (tmx + sx * 2.3, 1.45, 6.4), (tmx + sx * 2.3, 1.45, 7.9), 0.1, 'Rope', 0.02, 4)
    for i, (w, depth, front) in enumerate(tiers):
        yb = y0 + tier_h * i
        zc = front - depth / 2
        b.box('TempleA' if i % 2 == 0 else 'TempleB', tmx, yb + tier_h / 2, zc, w, tier_h, depth)
        b.box('TempleLight', tmx, yb + tier_h - 0.15, front + 0.12, w + 0.4, 0.32, 0.55, bevel=0.04)       # a deep cornice
        b.box('TempleDark', tmx, yb + 0.28, front + 0.08, w + 0.2, 0.55, 0.2)                             # a heavy plinth
        # a row of pilasters with recessed panels between them, a jade glyph in each panel
        n = max(3, int(w / 1.7))
        pw = w / n
        for q in range(n + 1):
            px_ = tmx - w / 2 + q * pw
            b.box('TempleLight', px_, yb + 1.9, front + 0.07, 0.34, 2.7, 0.14, bevel=0.03)
        for q in range(n):
            px_ = tmx - w / 2 + (q + 0.5) * pw
            if abs(px_ - tmx) < 1.7:
                continue
            b.box('TempleDark', px_, yb + 1.9, front + 0.05, pw - 0.5, 2.1, 0.07)
            b.box('Jade', px_, yb + 1.95, front + 0.1, 0.34, 0.34, 0.04)
            b.box('Jade', px_ + 0.28, yb + 1.6, front + 0.1, 0.18, 0.18, 0.04)
            b.box('Jade', px_ - 0.28, yb + 2.3, front + 0.1, 0.18, 0.18, 0.04)
        for sx in (-1, 1):
            for q in range(int(depth / 1.2)):
                b.box('TempleDark', tmx + sx * (w / 2 + 0.04), yb + 1.9, front - 0.8 - q * 1.2, 0.08, 2.0, 0.8)
                b.box('Jade', tmx + sx * (w / 2 + 0.08), yb + 1.95, front - 0.8 - q * 1.2, 0.04, 0.3, 0.3)
        b.box('Gold', tmx, yb + tier_h - 0.01, front + 0.4, w + 0.5, 0.04, 0.12)                          # the gold rim on the cornice
        for sx in (-1, 1):                                                                                # a big mask on each side of the stairs, banners at the corners, braziers on the corners above
            chac_mask(b, tmx + sx * min(w / 2 - 1.5, 3.4), yb + 1.9, front + 0.2, 0.9 + 0.05 * i)
            banner(b, tmx + sx * (w / 2 - 0.55), yb + tier_h - 0.35, front + 0.3, 2.2)
            bx_ = tmx + sx * (w / 2 - 0.5)
            b.cone('TempleDark', bx_, yb + tier_h, front - 0.5, 0.38, 0.28, 0.75, 8)
            b.cone('TempleLight', bx_, yb + tier_h + 0.7, front - 0.5, 0.46, 0.46, 0.14, 8)
            b.cone('Lit', bx_, yb + tier_h + 0.82, front - 0.5, 0.28, 0.04, 0.6, 6)
        # the stairs: a wedge in the step-back, light stone with dark risers, flanked by serpent balustrades
        nstep = 10
        gap = 1.2
        for k in range(nstep):
            depth_k = gap * (1 - k / nstep) + 0.18
            b.box('TempleLight', tmx, yb + 0.37 * (k + 1) - 0.1, front + depth_k / 2, 3.0, 0.2, depth_k, bevel=0.02)
            b.box('TempleDark', tmx, yb + 0.37 * (k + 1) - 0.3, front + depth_k / 2 - 0.05, 2.9, 0.18, depth_k - 0.05)
        for sx in (-1, 1):
            b.box('TempleB', tmx + sx * 1.75, yb + 1.0, front + 0.6, 0.45, 2.0, 1.2, bevel=0.05)
            b.box('TempleLight', tmx + sx * 1.75, yb + 2.05, front + 0.6, 0.55, 0.12, 1.3)
            if i == 0:                                                                                    # a serpent head at the foot of the main stairs
                b.ico('TempleLight', tmx + sx * 1.75, yb + 0.55, front + 1.45, 0.5, 1.0, sub=1, sc=(0.8, 0.8, 1.4), jit=0.06, seed=3 + int(sx))
                b.box('Jade', tmx + sx * 1.75 - 0.15 * sx, yb + 0.75, front + 1.78, 0.14, 0.12, 0.05)
                b.box('Dark', tmx + sx * 1.75, yb + 0.35, front + 1.9, 0.3, 0.1, 0.05)
    # the shrine: a jade-framed door, a stepped roof comb in two stones, gold, red flags
    sy = y0 + tier_h * 4
    b.box('TempleB', tmx, sy + 1.4, -1.9, 4.2, 2.8, 2.2, bevel=0.04)
    b.box('Dark', tmx, sy + 1.0, -0.78, 1.1, 2.0, 0.08)
    b.box('Jade', tmx, sy + 1.1, -0.83, 1.5, 2.3, 0.06)
    b.box('Gold', tmx, sy + 2.25, -0.76, 1.5, 0.12, 0.1)
    for sx in (-1, 1):
        b.box('TempleLight', tmx + sx * 1.6, sy + 1.4, -0.8, 0.5, 2.4, 0.4, bevel=0.04)
        b.box('Gold', tmx + sx * 1.6, sy + 2.4, -0.58, 0.4, 0.12, 0.06)
        chac_mask(b, tmx + sx * 1.6, sy + 1.2, -0.45, 0.4)
    for q in range(5):
        b.box('TempleA' if q % 2 else 'TempleLight', tmx, sy + 2.95 + q * 0.38, -1.9, 3.4 - q * 0.6, 0.38, 1.9 - q * 0.32)
    b.box('Jade', tmx, sy + 3.3, -0.94, 1.8, 0.5, 0.04)
    b.cone('Gold', tmx, sy + 4.85, -1.9, 0.35, 0.05, 0.8, 6)
    for sx in (-1, 1):
        b.limb('WoodDark', (tmx + sx * 2.3, sy, -0.4), (tmx + sx * 2.3, sy + 3.2, -0.4), 0.05, 0.04, 5)
        b.tri('TempleRed', (tmx + sx * 2.3, sy + 3.2, -0.4), (tmx + sx * 2.3 + sx * 0.9, sy + 2.95, -0.4), (tmx + sx * 2.3, sy + 2.5, -0.4))
        b.ico('Gold', tmx + sx * 2.3, sy + 3.25, -0.4, 0.07, 1.0, sub=0)
    b.box('Gold', tmx, sy + 0.015, 0.78, 5.6, 0.01, 0.14)                                                   # the gold edge at the diving place
    # a few lianas on the corners and moss on the corners of the steps
    for sx in (-1, 1):
        for k in range(2):
            vine(b, tmx + sx * (7.0 - 0.3 * k), y0 + tier_h - 0.2, 4.3 - 0.9 * k, 3.0 + 0.6 * k, seed=k + int(sx) * 3)
    for k in range(8):
        x = tmx + (7.2 - 1.4 * k) * (1 if k % 2 else -1)
        i = k % 3
        w, depth, front = tiers[i]
        if abs(x - tmx) > w / 2 - 0.4:
            continue
        b.box('Moss', x, y0 + tier_h * (i + 1) + 0.01, front - 0.5, 0.8, 0.025, 0.6)
    # torches along the foot, so the way to the stairs is lit
    for sx in (-5.8, -4.0, 4.0, 5.8):
        b.limb('WoodDark', (tmx + sx, 1.2, 5.7), (tmx + sx, 2.9, 5.7), 0.06, 0.05, 5)
        b.cone('Lit', tmx + sx, 2.85, 5.7, 0.16, 0.02, 0.5, 5)
    b.spot('Spot_Temple', tmx, 16.0, 0.85)


# ---------------------------------------------------------------- the idol island (rock pillar -5..9, r 4.6 -> 3.2, at z = 34) and the totem rock (-3.5..4.1)
def stack(b, cx, cz, bottom, top, r0, r1, seed, mats):
    h = top - bottom
    steps = max(10, int(h / 0.7))
    for k in range(steps):
        f0, f1 = k / steps, (k + 1) / steps
        y0, y1 = bottom + h * f0, bottom + h * f1
        ra, rb = r0 + (r1 - r0) * f0, r0 + (r1 - r0) * f1
        fa, fb = 1.0 + 0.4 * smooth((-0.4 - y0) / 4.0), 1.0 + 0.4 * smooth((-0.4 - y1) / 4.0)
        j = hash01(seed * 3.3 + k * 1.7)
        mat = mats[(k + seed) % len(mats)]
        if y1 < 0.9 and y0 > -1.1:
            mat = 'RockWet'
        elif y1 < -1.1:
            mat = 'RockDark'
        g = 0.98 + 0.08 * hash01(k * 1.9 + seed)
        b.cone(mat, cx + 0.3 * (j - 0.5), y0, cz + 0.3 * (hash01(k + seed) - 0.5), ra * fa * g, rb * fb * g * (0.98 + 0.06 * j), y1 - y0 + 0.02, 9)
    b.cone('CapDark', cx, top - 0.35, cz, r1 * 1.05, r1 * 1.0, 0.4, 9)
    b.ico('CapGrass', cx, top - 0.05, cz, r1 * 0.95, 0.2, sub=1, jit=0.05, seed=seed)
    for k in range(7):                                                                                 # hanging moss and roots over the edge
        a = k * 0.9 + seed
        vine(b, cx + math.cos(a) * r1 * 0.98, top - 0.2, cz + math.sin(a) * r1 * 0.98, 2.0 + 2.0 * hash01(k + seed), seed=k + seed)


def idol_island(b):
    cx, cz = 0.0, 34.0
    stack(b, cx, cz, -5.0, 9.0, 4.6, 3.2, 93, ['RockB', 'RockC', 'RockA', 'RockB'])
    # the idol: a stone block 1.7 x 2.6 x 1.4 at (0, 10.2, 35.4), its face towards the falls (-z): brow, eyes, nose, mouth with teeth, ear discs, a stepped headdress
    ix, iy, iz = 0.0, 10.2, 35.4
    fz = iz - 0.7
    b.box('Stone', ix, iy, iz, 1.7, 2.6, 1.4, bevel=0.06)
    b.box('StoneLight', ix, iy - 1.5, iz, 2.2, 0.35, 1.8, bevel=0.05)                                    # a plinth
    b.box('StoneDark', ix, iy + 1.0, fz - 0.06, 1.9, 0.34, 0.2, bevel=0.03)                            # the heavy brow
    for sx in (-1, 1):
        b.box('Dark', ix + sx * 0.42, iy + 0.55, fz - 0.03, 0.34, 0.24, 0.1)
        b.box('Lit', ix + sx * 0.42, iy + 0.55, fz - 0.09, 0.12, 0.12, 0.04)
        b.box('StoneLight', ix + sx * 0.98, iy + 0.5, iz, 0.12, 0.6, 0.6)
        b.limb('StoneDark', (ix + sx * 0.88, iy + 0.5, iz), (ix + sx * 1.06, iy + 0.5, iz), 0.32, 0.32, 10)
    b.box('StoneDark', ix, iy + 0.1, fz - 0.1, 0.4, 0.8, 0.3, bevel=0.04)                              # the nose
    b.box('Dark', ix, iy - 0.55, fz - 0.03, 1.0, 0.3, 0.1)
    for q in range(5):
        b.box('StoneLight', ix - 0.4 + q * 0.2, iy - 0.55, fz - 0.07, 0.12, 0.18, 0.05)
    for q in range(4):                                                                                  # the headdress
        b.box('StoneMoss' if q % 2 else 'StoneDark', ix, iy + 1.5 + q * 0.28, iz, 1.9 - q * 0.36, 0.28, 1.5 - q * 0.2, bevel=0.03)
    b.box('Gold', ix, iy + 1.5, fz - 0.05, 1.8, 0.1, 0.1)
    for sx in (-1, 1):                                                                                  # torches either side
        tx_ = ix + sx * 1.9
        b.limb('WoodDark', (tx_, 9.0, iz - 0.4), (tx_, 10.4, iz - 0.4), 0.06, 0.05, 5)
        b.cone('Lit', tx_, 10.35, iz - 0.4, 0.14, 0.02, 0.4, 5)
    fern(b, ix - 2.0, 9.0, 32.6, 1.0, seed=3)
    fern(b, ix + 2.2, 9.0, 33.0, 1.0, seed=4)
    for q, (dx, dz) in enumerate(((-1.4, 31.9), (1.5, 32.2), (0.3, 31.6))):
        b.ico(('FlowerR', 'FlowerY', 'FlowerP')[q], cx + dx, 9.25, dz, 0.1, 0.8, sub=0)
    # roots and lilies at the water, a mooring ring and a bit of rope
    for k in range(7):
        a = k * 0.9
        b.limb('Bark', (cx + math.cos(a) * 4.4, 0.0, cz + math.sin(a) * 4.4), (cx + math.cos(a) * 4.9, -1.5, cz + math.sin(a) * 4.9), 0.18, 0.08, 5)
    b.spot('Spot_Idol', cx, 9.0, cz - 2.7)


def totem_rock(b):
    cx, cz = -24.0, 30.0
    stack(b, cx, cz, -3.5, 4.1, 3.4, 2.6, 94, ['RockC', 'RockB', 'RockA'])
    px = cx - 1.1
    # the pole: 0.9 square, 5.8 high from 4.1: three stacked carved figures, painted
    b.box('Wood', px, 7.0, cz, 0.9, 5.8, 0.9, bevel=0.05)
    figures = ((4.9, 'TotemRed', 'bear'), (6.3, 'TotemTeal', 'frog'), (7.7, 'TotemWhite', 'eagle'))
    for yc, col, kind in figures:
        b.box(col, px + 0.47, yc, cz, 0.1, 1.15, 0.86)
        b.box('Dark', px + 0.54, yc + 0.18, cz - 0.22, 0.04, 0.2, 0.22)
        b.box('Dark', px + 0.54, yc + 0.18, cz + 0.22, 0.04, 0.2, 0.22)
        b.box('TotemWhite' if col != 'TotemWhite' else 'TotemRed', px + 0.56, yc + 0.18, cz - 0.22, 0.03, 0.08, 0.08)
        b.box('TotemWhite' if col != 'TotemWhite' else 'TotemRed', px + 0.56, yc + 0.18, cz + 0.22, 0.03, 0.08, 0.08)
        if kind == 'bear':
            b.box('Dark', px + 0.6, yc - 0.15, cz, 0.12, 0.22, 0.5)
            for sg in (-1, 1):
                b.box(col, px + 0.55, yc + 0.62, cz + sg * 0.3, 0.1, 0.22, 0.2)
        elif kind == 'frog':
            b.box('Dark', px + 0.58, yc - 0.2, cz, 0.08, 0.1, 0.62)
            for sg in (-1, 1):
                b.box('TotemRed', px + 0.58, yc - 0.45, cz + sg * 0.55, 0.12, 0.3, 0.2)
        else:
            b.box('Gold', px + 0.62, yc - 0.12, cz, 0.26, 0.28, 0.2)
            b.box('Gold', px + 0.5, yc - 0.1, cz, 0.12, 0.12, 0.2)
    for sg in (-1, 1):                                                                                  # wings of the eagle
        b.box('TotemTeal', px + 0.3, 7.55, cz + sg * 0.95, 0.5, 0.8, 0.12, bevel=0.03)
        b.box('TotemRed', px + 0.3, 7.15, cz + sg * 1.2, 0.5, 0.3, 0.12, bevel=0.03)
    b.cone('Gold', px, 9.9, cz, 0.3, 0.05, 0.45, 6)
    for k in range(4):                                                                                  # feathers and offerings at the foot
        b.ico(('FlowerR', 'FlowerY', 'FlowerP', 'FlowerO')[k], cx - 0.3 + 0.5 * k, 4.3, cz + 1.2 - 0.4 * k, 0.1, 0.8, sub=0)
    b.cone('WoodDark', cx + 0.4, 4.1, cz + 1.5, 0.3, 0.26, 0.25, 8)
    b.spot('Spot_Totem', cx + 2.2, 4.1, cz)


# ---------------------------------------------------------------- the beach life and the flora of the banks
def beach_life(b, hf):
    for k in range(90):
        side = -1 if k % 2 else 1
        x = side * (19.0 + 25.0 * hash01(k * 1.9 + 1))
        z = shore_z(x) - 10.0 * hash01(k * 2.7 + 2) + 1.5
        if z < face_z(x) + 2.0 or not free_of_trees(x, z, 0.8):
            continue
        if abs(x - 30) < 9.0 and z > -4.0:
            continue
        if abs(x + 29) < 5.0 and z > -5.5:
            continue
        y = hf(x, z)
        if y < 0.6:
            continue
        r = hash01(k * 3.1)
        if r < 0.12:
            banana(b, x, y, z, 0.8 + 0.4 * hash01(k), seed=k)
        elif r < 0.26:
            heliconia(b, x, y, z, 0.9 + 0.5 * hash01(k), seed=k)
        elif r < 0.4:
            bamboo(b, x, y, z, 0.8 + 0.3 * hash01(k), seed=k)
        elif r < 0.62:
            fern(b, x, y, z, 0.8 + 0.5 * hash01(k), seed=k)
        elif r < 0.74:
            b.ico('Shell', x, y + 0.04, z, 0.1, 0.5, sub=0)
        elif r < 0.84:
            b.box('Wood', x, y + 0.14, z, 1.6 + 1.6 * hash01(k), 0.28, 0.28, rot_y=360 * hash01(k + 5), bevel=0.05)
        else:
            tuft(b, x, y, z, 1.2, 'LeafC', 5, seed=k)
    # a fire pit with logs on the west beach and a pulled-up canoe, a hammock between two stakes on the east one
    fx, fz = -33.0, 6.0
    y = hf(fx, fz)
    for q in range(8):
        a = q * 0.785
        b.ico('Stone', fx + math.cos(a) * 0.75, y + 0.12, fz + math.sin(a) * 0.75, 0.2, 0.7, sub=0)
    b.cone('Lit', fx, y, fz, 0.35, 0.05, 0.5, 6)
    for a in (0.3, 2.4, 4.4):
        b.box('Wood', fx + math.cos(a) * 1.7, y + 0.2, fz + math.sin(a) * 1.7, 1.5, 0.35, 0.35, rot_y=math.degrees(-a) + 90, bevel=0.04)
    canoe(b, -37.5, 8.6, 20.0, 4.0, 'WoodLight')
    hx, hz = 38.0, 5.0
    y = hf(hx, hz)
    for sg in (-1, 1):
        b.limb('WoodDark', (hx + sg * 2.2, y - 0.1, hz), (hx + sg * 2.2, y + 1.8, hz), 0.07, 0.06, 5)
    rope(b, (hx - 2.2, y + 1.6, hz), (hx + 2.2, y + 1.6, hz), 0.8, 'Cloth', 0.09, 6)


def jungle_edges(b, hf):
    """Mangrove roots in the shallows and lilies on the lagoon along both banks, reeds in clumps."""
    for k in range(36):
        side = -1 if k % 2 else 1
        x = side * (19.5 + 24.0 * hash01(k * 2.7 + 61))
        z = shore_z(x) + 0.8 + 2.0 * hash01(k * 3.9 + 62)
        if free_of_trees(x, z, 0.6) is False:
            continue
        y = hf(x, z)
        if y > 0.2:
            continue
        for j in range(5):
            a = j * 1.3 + k
            b.limb('Bark', (x + math.cos(a) * 0.2, 0.6, z + math.sin(a) * 0.2), (x + math.cos(a) * 0.8, max(y, -1.2), z + math.sin(a) * 0.8), 0.07, 0.04, 4)
        b.limb('Bark', (x, 0.7, z), (x, 0.9, z), 0.12, 0.1, 5)
        b.ico('LeafA', x, 1.0, z, 0.5, 0.5, sub=1, jit=0.15, seed=k)
    for k in range(34):
        x, z = -40 + 80 * hash01(k * 2.1 + 81), 2 + 40 * hash01(k * 5.1 + 82)
        if hf(x, z) > -0.8:
            continue
        b.cone('LeafA', x, 0.0, z, 0.28, 0.26, 0.03, 8)
        if k % 3 == 0:
            b.ico('FlowerP' if k % 2 else 'FlowerW', x, 0.08, z, 0.1, 0.8, sub=0)


# ---------------------------------------------------------------- build
def build():
    b = locbuild.Builder()
    for k, v in COLOURS.items():
        b.mat(k, v)
    block_base(b)
    hf = ground(b)
    cap_bump = wall(b)
    waterfall(b)
    wall_life(b, cap_bump)
    ledge(b, 'RockLedge', -3.0, 6.0, 2.9)
    ledge(b, 'FallsLedge', 7.5, 24.0, 2.9)
    access(b)
    dock(b)
    treehouse(b)
    temple(b)
    idol_island(b)
    totem_rock(b)
    beach_life(b, hf)
    jungle_edges(b, hf)
    return b


def add_preview_water():
    import bmesh
    me = bpy.data.meshes.new('PreviewWater')
    ob = bpy.data.objects.new('PreviewWater', me)
    bpy.context.scene.collection.objects.link(ob)
    bm = bmesh.new()
    v = [bm.verts.new((x, y, 0.0)) for x, y in ((-HALF, 32.0), (HALF, 32.0), (HALF, -60.0), (-HALF, -60.0))]
    bm.faces.new(v)
    bm.to_mesh(me)
    bm.free()
    m = bpy.data.materials.new('PreviewWater')
    m.use_nodes = True
    for n in m.node_tree.nodes:
        if n.type == 'BSDF_PRINCIPLED':
            n.inputs['Base Color'].default_value = (0.03, 0.55, 0.52, 1)
            n.inputs['Alpha'].default_value = 0.62
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
    objs = b.finish('Loc_Jungle')
    print('LOCATION TRIS', b.tris, 'objects', len(objs))
    b.export(OUT)
    add_preview_water()
    view.shoot('loc_jungle_overview', (0, 8, 10), 150, (0.35, 0.55, 1.0), lens=50, res=(1200, 800))
    view.shoot('loc_jungle_falls', (15, 14, 0), 55, (0.1, 0.1, 1.0), lens=50, res=(1100, 700))
    view.shoot('loc_jungle_tree', (-29, 10, 0), 40, (0.5, 0.2, 1.0), lens=50, res=(1100, 700))
    view.shoot('loc_jungle_temple', (30, 8, 2), 40, (-0.3, 0.2, 1.0), lens=50, res=(1100, 700))
    view.shoot('loc_jungle_dock', (-12, 4, -1), 30, (0.3, 0.3, 1.0), lens=50, res=(1100, 700))
    view.shoot('loc_jungle_islands', (-10, 4, 32), 60, (0.1, 0.3, 1.0), lens=50, res=(1100, 700))
    print('LOC DONE')


if __name__ == '__main__':
    main()
