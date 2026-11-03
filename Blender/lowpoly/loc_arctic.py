"""Location 5, the northern bay, as one scene (Docs/ModelBrief.md section 3.5). Output: Models/Locations/Loc_Arctic.fbx + previews.
    blender -b --python Blender/lowpoly/loc_arctic.py
Site space like the game's: the block is 90 x 90 m, x -45..45, z -30..60, water at y = 0 (not modelled), sea floor at -6. The glacier wall is the very
same faceted face the game's collision boxes were fitted to (data/glacier_wall.txt, from PrototypeSceneBuilder.DumpGlacierWall); the floe, the icebreaker,
the two bergs, the ice tower, the arch and the glacier ledge stand where the game's solid boxes are, and only add looks on top. The penguins are the game's.
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

OUT = os.path.normpath(os.path.join(HERE, '..', '..', 'Assets', '_Project', 'Models', 'Locations', 'Loc_Arctic.fbx'))
DATA = os.path.join(HERE, 'data', 'glacier_wall.txt')

COLOURS = {
    'Soil': '#4A4F58', 'Earth': '#5B6068', 'BlockRock': '#444A52', 'Seabed': '#7FA0AC', 'SeabedDark': '#6C8D99',
    'IceA': '#DCEBF3', 'IceB': '#B3D3E5', 'IceC': '#8DB9D2', 'IceDeep': '#5E9BBF', 'IceWhite': '#EAF4F8', 'IceWet': '#6FB8CE', 'IceUnder': '#7CC4D6',
    'Snow': '#F3F8FA', 'SnowShade': '#D5E4EC', 'SnowBlue': '#C3D8E6', 'Rock': '#6A7078', 'RockDark': '#50565E',
    'PineGreen': '#3D6B52', 'PineGreenDark': '#2F5642', 'PineSnow': '#CFE0DA', 'Trunk': '#8A5B3D',
    'Hull': '#B23A32', 'HullDark': '#222831', 'White': '#EDEBE4', 'WhiteShade': '#C9C7BF', 'Glass': '#27394B', 'Deck': '#6C7680', 'Orange': '#E8742A', 'Grey': '#59616B',
    'Steel': '#9CA5AE', 'Black': '#1E2126', 'Wood': '#9C6B43', 'WoodDark': '#6E4A2F', 'Rope': '#D9C9A0', 'StationWall': '#C9453A', 'StationRoof': '#EDEBE4',
    'Flag': '#E8742A', 'Panel': '#2E4A6B', 'Tent': '#E8742A', 'TentDark': '#B5501A', 'Lit': '#FFD98A', 'Coal': '#1E2126', 'Carrot': '#F28C28', 'Scarf': '#C9413A',
    'Fur': '#8A5B3D', 'Dish': '#D8DEE3', 'RedLight': '#FF5A4A', 'Sled': '#7A5A3E', 'BlueIce': '#5E9BBF',
}

COLS, ROWS = 24, 10
HALF = 45.0
ZBACK = -30.0
TOP = 26.0


def hash01(i):
    return (math.sin(i * 12.9898) * 43758.5453) % 1.0


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def face_z(x):   # the same in C#: PrototypeSceneBuilder.GlacierFaceZ
    return -3.0 + 1.6 * math.sin(x * 0.11 + 1.0) + 0.7 * math.sin(x * 0.31)


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
    while levels[-1] < 28:
        levels.append(levels[-1] + 0.7 + 1.3 * hash01(k * 5.7 + 2))
        k += 1
    return levels


STRATA = strata()
PATTERN = ['IceA', 'IceWhite', 'IceA', 'IceB', 'IceWhite', 'IceA', 'IceWhite', 'IceB', 'IceA', 'IceWhite', 'IceC', 'IceA']
CREVASSES = [(-41 + 82 * hash01(k * 3.7 + 11), 5 + 14 * hash01(k * 2.9 + 12), 0.3 * hash01(k + 14)) for k in range(0)]       # no painted crevasses: the fluting and the bumps carry the face
BUMPS = [(-44 + 88 * hash01(k * 2.3 + 25), 1.5 + 22 * hash01(k * 4.7 + 26), 1.2 + 2.4 * hash01(k * 1.9 + 27), 0.8 + 1.8 * hash01(k * 6.1 + 28),
          0.2 + 0.45 * hash01(k * 3.9 + 29)) for k in range(90)]


def relief(x, y):
    k = max(0, min(len(STRATA) - 2, bisect.bisect(STRATA, y) - 1))
    t = (y - STRATA[k]) / (STRATA[k + 1] - STRATA[k])
    hard = hash01(k * 3.3 + 1.0)
    r = (0.22 * hard if hard > 0.5 else -0.05) * max(0.0, math.sin(math.pi * t)) ** 0.6
    r += 0.1 * math.sin(x * 1.9 + math.sin(y * 0.5) * 2.0)                  # vertical fluting, the way meltwater carves a glacier face
    r += 0.06 * math.sin(x * 0.6 + y * 0.8)
    for bx, by, sx, sy, amp in BUMPS:
        dx, dy = (x - bx) / sx, (y - by) / sy
        d2 = dx * dx + dy * dy
        if d2 < 9.0:
            r += amp * math.exp(-d2)
    if y < 1.2:
        r += 0.35 * smooth((1.2 - y) / 3.2)
    r += 0.5 * math.exp(-((x + 12.0 + 0.5 * math.sin(y * 0.3)) / 0.95) ** 2) * smooth((y - 1.5) / 2.5) * smooth((TOP - 1.2 - y) / 2.5)      # the frozen waterfall
    if y > TOP - 1.4:
        r += 0.45 * smooth((y - (TOP - 1.4)) / 1.4) * (0.6 + 0.4 * math.sin(x * 0.33))     # the snow cornice over the lip
    return max(-0.15, min(1.3, r))


def wave(x, y, z):
    return y + 0.3 * math.sin(x * 0.19 + y * 0.35) + 0.2 * math.sin(x * 0.57 - y * 0.2 + z)


def crevasse_at(cx, y):
    for bx, by, drift in CREVASSES:
        if abs(y - by) < 5.5 and abs(cx - (bx + 0.35 * math.sin(y * 0.45 + bx) + drift * (y - by))) < 0.14:
            return True
    return False


def wall_material(cx, cz, f):
    if f < -0.9:
        return 'IceUnder'
    if f < 0.7:
        return 'IceWet'
    if crevasse_at(cx, f):
        return 'IceDeep'
    return PATTERN[bisect.bisect(STRATA, f) % len(PATTERN)]


# ---------------------------------------------------------------- small things
def tuft(b, x, y, z, s=1.0, mat='Snow', n=3, seed=0):
    for k in range(n):
        a = k * 2.2 + hash01(seed + k) * 3
        b.ico(mat, x + math.cos(a) * 0.15 * s, y + 0.1 * s, z + math.sin(a) * 0.15 * s, 0.22 * s, 0.6, sub=0, jit=0.1, seed=seed + k)


def icicle(b, x, y, z, ln, r=0.08, mat='IceWhite'):
    b.cone(mat, x, y - ln, z, 0.01, r, ln, 5)


def pine(b, x, y, z, s=1.0, seed=0):
    b.cone('Trunk', x, y - 0.1, z, 0.16 * s, 0.1 * s, 1.4 * s, 5)
    for i, (r, h) in enumerate(((1.7, 1.7), (1.35, 1.6), (1.0, 1.5), (0.65, 1.4))):
        yy = y + (0.9 + i * 1.0) * s
        b.cone('PineGreen' if (i + seed) % 2 else 'PineGreenDark', x, yy, z, r * s, r * s * 0.12, h * s, 8)
        b.cone('PineSnow', x, yy + h * s * 0.38, z, r * s * 0.66, r * s * 0.1, h * s * 0.62, 8)


def rope(b, p0, p1, sag=0.3, mat='Rope', r=0.03, n=5):
    pts = []
    for i in range(n + 1):
        t = i / n
        pts.append((p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t - sag * 4 * t * (1 - t), p0[2] + (p1[2] - p0[2]) * t))
    for a, c in zip(pts, pts[1:]):
        b.limb(mat, a, c, r, r, 4)


def faceted(b, mat, cx, cz, y0, y1, r0, r1, sides, jit, seed, rot=0.0, flat_top=True, rings=1):
    """An irregular faceted column: rings of `sides` points, radius easing from r0 at y0 to r1 at y1, every corner pushed about by `jit`; closed at both ends."""
    n = max(2, rings)
    rs = []
    for k in range(n + 1):
        t = k / n
        y = y0 + (y1 - y0) * t
        r = r0 + (r1 - r0) * t
        ring = []
        for i in range(sides):
            a = rot + 2 * math.pi * i / sides
            j = 1.0 + (hash01(seed * 7.3 + k * 3.1 + i * 1.7) - 0.5) * 2 * jit if 0 < k < n or not flat_top else 1.0
            ring.append((cx + math.cos(a) * r * j, y + ((hash01(seed + k * 5.1 + i) - 0.5) * jit * 1.5 if 0 < k < n else 0.0), cz + math.sin(a) * r * j))
        rs.append(ring)
    b.skin(mat, rs, closed=True, cap_a=True, cap_b=True)


def layered(b, cx, cz, y0, y1, r0, r1, sides, seed, mats, jit=0.08, steps=None, flare=0.0, rot=0.0):
    """A faceted column built from slices in different ices: radius easing r0 -> r1, a smooth flare under the water."""
    steps = steps or max(8, int((y1 - y0) / 0.7))
    for k in range(steps):
        f0, f1 = k / steps, (k + 1) / steps
        ya, yb = y0 + (y1 - y0) * f0, y0 + (y1 - y0) * f1
        fa, fb = 1.0 + flare * smooth((-0.4 - ya) / 4.5), 1.0 + flare * smooth((-0.4 - yb) / 4.5)
        ra, rb = (r0 + (r1 - r0) * f0) * fa, (r0 + (r1 - r0) * f1) * fb
        mat = mats[(k + seed) % len(mats)]
        if yb < 0.7 and ya > -1.0:
            mat = 'IceWet'
        elif yb < -1.0:
            mat = 'IceUnder' if k % 2 else 'IceC'
        g = 1.0 + (hash01(seed * 2.1 + k * 1.3) - 0.5) * 2 * jit
        b.cone(mat, cx + 0.15 * (hash01(k + seed) - 0.5), ya, cz + 0.15 * (hash01(k * 2 + seed) - 0.5), ra * g, rb * g * (1 + 0.04 * hash01(k + 9)), yb - ya + 0.02, sides)


# ---------------------------------------------------------------- the block
def block_base(b):
    top = -6.1
    b.box('Soil', 0, top - 0.7, 15, 90, 1.4, 90)
    b.box('Earth', 0, (top - 1.4 - 9.0) / 2, 15, 90, (top - 1.4) - (-9.0), 90)
    b.box('BlockRock', 0, (-9.0 - 16.0) / 2, 15, 90, 7.0, 90)
    b.box('Seabed', 0, -6.05, 15, 90, 0.1, 90)
    for k in range(30):
        x, z = -42 + 84 * hash01(k * 3.7 + 1), -2 + 60 * hash01(k * 6.1 + 2)
        b.ico('SeabedDark', x, -6.0, z, 1.0, 1.0, sub=1, sc=(2.0 + 3 * hash01(k + 9), 0.07, 0.7 + 0.5 * hash01(k + 4)), jit=0.1, seed=k)
    for k in range(14):
        x, z = -40 + 80 * hash01(k * 2.9 + 11), 6 + 50 * hash01(k * 5.3 + 12)
        b.ico('Rock', x, -6.0, z, 0.6 + 0.6 * hash01(k + 3), 0.6, sub=1, jit=0.16, seed=k + 40)


# ---------------------------------------------------------------- the glacier
def wall(b):
    splits = [(wave, [-0.9, 0.7] + STRATA[1:-1])]
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
    # the snow cap running back from the lip, in drifts
    rows, halves = 14, 2
    drifts = [(-42 + 84 * hash01(k * 1.9 + 61), -28 + 24 * hash01(k * 2.7 + 62), 2.5 + 3.0 * hash01(k + 63), 0.3 + 0.7 * hash01(k * 3.1 + 64)) for k in range(34)]

    def drift_h(x, z):
        h = 0.0
        for mx, mz, r, amp in drifts:
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
            y += 0.1 * math.sin(x * 0.27 + z * 0.4) * smooth(t * 2.0) + drift_h(x, z) * smooth(t * 5.0) + 0.6 * smooth((t - 0.7) / 0.3)
        return (x, y, z)

    def cap_material(cx, cz, f):
        v = hash01(round(cx * 1.1) * 1.7 + round(cz * 1.1) * 3.3)
        return 'SnowBlue' if v > 0.8 else 'SnowShade' if v < 0.18 else 'Snow'
    cap_split = [(lambda x, y, z: z, [-100.0])]
    for b0, f0, f1, b1 in CAP:
        for h in range(halves):
            u0, u1 = h / halves, (h + 1) / halves
            for r in range(rows):
                l0, r0 = cap_pt(f0, b0, f1, b1, u0, r), cap_pt(f0, b0, f1, b1, u1, r)
                l1, r1 = cap_pt(f0, b0, f1, b1, u0, r + 1), cap_pt(f0, b0, f1, b1, u1, r + 1)
                for tri in ((l1, l0, r0), (l1, r0, r1)):
                    b._terrain_cut([tri[0], tri[2], tri[1]], cap_split, cap_material)
    # ends and back of the block: the same layers cut straight
    for side in (-1, 1):
        zf = face_z(side * HALF) - 0.5
        y = -6.0
        k = 0
        while y < TOP:
            h = min(STRATA[k + 1] - y, TOP - y)
            b.box(PATTERN[k % len(PATTERN)], side * (HALF - 0.3), y + h / 2, (zf + ZBACK) / 2, 0.6, h, zf - ZBACK)
            y += h
            k += 1
        b.box('Snow', side * (HALF - 0.3), TOP + 0.05, (zf + ZBACK) / 2, 0.62, 0.1, zf - ZBACK)
    y = -6.0
    k = 0
    while y < TOP:
        h = min(STRATA[k + 1] - y, TOP - y)
        b.box(PATTERN[k % len(PATTERN)], 0, y + h / 2, ZBACK + 0.3, 90, h, 0.6)
        y += h
        k += 1
    b.box('Snow', 0, TOP + 0.05, ZBACK + 0.3, 90, 0.1, 0.62)
    return drift_h


def glacier_detail(b):
    # icicles hanging from the lip and the cornice, and snow draped on it
    for k in range(70):
        x = -44 + 88 * hash01(k * 2.7 + 7)
        zt = wall_z(x, TOP - 0.6) + 0.12
        icicle(b, x, TOP - 0.7 - 0.6 * hash01(k), zt, 0.8 + 2.4 * hash01(k + 1), 0.07 + 0.08 * hash01(k + 2), 'IceWhite' if k % 3 else 'IceA')
# ---------------------------------------------------------------- the glacier ledge (collision: WallLedge, 14 m at x = 18)
def ledge(b, x, h, reach):
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
    b.skin('Snow', [rings[0], [(x, h, cz0)] * n], axis=lambda k: (x, h - 3.0, cz0), closed=True)
    for k, mat in enumerate(('IceWhite', 'IceA', 'IceB', 'IceC', 'IceB')):
        b.skin(mat, [rings[k], rings[k + 1]], closed=True)
    for side in (-1, 1):
        for q, zz in enumerate((fz - 0.4, fz + 0.5, fz + 1.4)):
            px = x + side * 1.75
            b.limb('WoodDark', (px, h - 0.05, zz), (px, h + 1.05, zz), 0.05, 0.045, 5)
            if q:
                for hy in (0.95, 0.55):
                    b.limb('Rope', (px, h + hy, zz - 0.9), (px, h + hy - 0.05, zz), 0.014, 0.014, 3)
    for dx in (-1.3, 1.2):
        tuft(b, x + dx, h, fz + 0.2, 1.1, 'Snow', 3, seed=int(x + dx))
    for q in range(5):
        icicle(b, x - 1.5 + q * 0.75, h - 0.5, fz + reach - 0.3 + 0.15 * q, 0.5 + 0.4 * hash01(q + x), 0.05)
    b.spot('Spot_GlacierLedge', x, h, fz + reach)


def ladder(b, x, y0, y1):
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
        for dx in (-0.3, 0.3):
            b.box('WoodDark', x + dx, y, zw + 0.15, 0.05, 0.06, 0.4)
        y += 2.2


def stairs(b, x0, y0, x1, y1, n):
    run = abs(x1 - x0) / n
    rise = (y1 - y0) / n
    for i in range(n + 1):
        t = i / n
        x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        zw = wall_z(x, y)
        b.box('Snow', x, y - 0.13, zw + 0.5, max(run * 1.1, 0.5), 0.26, 1.5, bevel=0.06)
        b.box('IceB', x, y - 0.26 - abs(rise) * 0.5, zw + 0.25, max(run * 1.1, 0.5), abs(rise) + 0.5, 1.0, bevel=0.05)
        if i % 2 == 0:
            b.limb('WoodDark', (x, y - 0.1, zw + 1.15), (x, y + 1.0, zw + 1.15), 0.045, 0.04, 5)
    for hy in (0.95, 0.5):
        for i in range(n):
            ta, tb = i / n, (i + 1) / n
            xa, ya = x0 + (x1 - x0) * ta, y0 + (y1 - y0) * ta
            xb, yb = x0 + (x1 - x0) * tb, y0 + (y1 - y0) * tb
            b.limb('Rope', (xa, ya + hy, wall_z(xa, ya) + 1.15), (xb, yb + hy, wall_z(xb, yb) + 1.15), 0.014, 0.014, 3)


def access(b):
    """Steps cut into the face from the east snow shelf up to the glacier ledge, a ladder from the ledge to the top."""
    stairs(b, 31.5, 1.0, 21.0, 13.9, 26)
    b.box('Wood', 19.6, 13.96, wall_z(19.6, 14.0) + 0.6, 1.0, 0.1, 1.3)
    ladder(b, 20.4, 13.7, 26.6)
    for sx in (20.0, 20.8):
        b.limb('WoodDark', (sx, 25.7, wall_z(sx, 25.7) + 0.5), (sx, 27.3, wall_z(sx, 26.0) - 0.3), 0.05, 0.045, 5)


# ---------------------------------------------------------------- the snow shelves where the penguins live (collision: 12 x 6.7 x 11 boxes at x = +-39)
def shelves(b):
    for side in (-1, 1):
        x0, x1 = (-45.0, -26.0) if side < 0 else (26.0, 45.0)
        mounds = []
        for k in range(16):
            x = side * (35.0 + 9.0 * hash01(k * 2.1 + 51 + side))
            z = -1.0 + 6.5 * hash01(k * 3.3 + 52 + side)
            if abs(abs(x) - 38.5) < 3.8 and 2.0 < z < 7.0:
                continue
            mounds.append((x, z, 1.4 + 1.4 * hash01(k + 53), 0.3 + 0.5 * hash01(k * 2.9 + 54)))

        def bump(x, z, mounds=mounds):
            h = 0.0
            for mx, mz, r, amp in mounds:
                d = math.hypot(x - mx, z - mz) / r
                if d < 1.0:
                    h += amp * (1 - d * d) ** 2.0
            return h

        def hf(x, z, side=side, bump=bump):
            ex = max(0.0, (x + 33.0) if side < 0 else (33.0 - x))
            ez = max(0.0, z - 8.0)
            d = math.hypot(ex, ez) / 6.5
            flat = 1.0 - smooth(d)
            h = -6.0 + 6.7 * flat ** 0.85
            wf = face_z(x)
            if z < wf + 3.5 and flat > 0.99:
                h += 0.5 * smooth((wf + 3.5 - z) / 3.5)
            if flat > 0.97:
                h += bump(x, z)
            return h

        def choose(cx, cz, f, bump=bump):
            if f < -0.4:
                return 'IceUnder'
            if f < 0.15:
                return 'IceWet'
            bp = bump(cx, cz)
            if bp > 0.45:
                return 'IceB'
            if bp > 0.25:
                return 'IceA'
            v = hash01(round(cx * 0.8) * 2.3 + round(cz * 0.8))
            return 'SnowBlue' if v > 0.85 else 'SnowShade' if v < 0.12 else 'Snow'
        b.terrain(x0, x1, -6.0, 13.0, 1.0, hf, choose, splits=[(lambda x, y, z: y, [-0.4, 0.15])])
        xe = -HALF if side < 0 else HALF
        for zz in range(-6, 13):
            h = hf(xe, zz + 0.5)
            b.box('IceB', xe + (0.25 if side < 0 else -0.25), (h - 6.1) / 2, zz + 0.5, 0.5, h + 6.1, 1.0)


# ---------------------------------------------------------------- the ice floe, 0.6 m (collision: 7.4 x 1 x 6 box)
def floe(b):
    fx, fz = -30.0, 2.0
    n = 18
    outline = []
    for i in range(n):
        a = 2 * math.pi * i / n
        c, s_ = math.cos(a), math.sin(a)
        rx, rz = 3.95, 3.25
        # a rounded rectangle, with a little wander in the edge
        px = fx + math.copysign(abs(c) ** 0.45, c) * rx * (1 + (hash01(i * 3.1 + 5) - 0.5) * 0.06)
        pz = fz + math.copysign(abs(s_) ** 0.45, s_) * rz * (1 + (hash01(i * 2.3 + 8) - 0.5) * 0.06)
        outline.append((px, pz))
    top = [(px, 0.6, pz) for px, pz in outline]
    mid = [(fx + (px - fx) * 0.97, 0.1, fz + (pz - fz) * 0.97) for px, pz in outline]
    under = [(fx + (px - fx) * 0.8, -1.2, fz + (pz - fz) * 0.8) for px, pz in outline]
    bot = [(fx + (px - fx) * 0.45, -2.4, fz + (pz - fz) * 0.45) for px, pz in outline]
    b.skin('Snow', [top, [(fx, 0.6, fz)] * n], axis=lambda k: (fx, -3.0, fz), closed=True)
    b.skin('IceA', [top, mid], closed=True)
    b.skin('IceWet', [mid, under], closed=True)
    b.skin('IceUnder', [under, bot], closed=True, cap_b=True)
    for k in range(5):
        a = k * 1.3 + 0.4
        b.ico('Snow', fx + math.cos(a) * 1.0, 0.72, fz + math.sin(a) * 0.9 - 0.4, 0.45, 0.4, sub=1, sc=(1.4, 0.35, 1.1), jit=0.12, seed=k + 3)
    b.spot('Spot_Floe', fx, 0.6, 4.9)


# ---------------------------------------------------------------- the icebreaker (collision: hull 4.8 x 3.4 x 16, house, bridge, bow)
def hull(b, pt, stations, bands, deck_mat, deck_y, keel_fn):
    def half(w, keel, y):
        return w * (0.84 + 0.16 * max(0.0, min(1.0, (y - keel) / (deck_y - keel))) ** 0.7)
    for side in (-1, 1):
        for mat, ya, yb in bands:
            rings = []
            for s, w in stations:
                k = keel_fn(s)
                lo = k if yb is None else max(yb, k)
                ys = [ya, (ya + lo) / 2, lo] if yb is None else [ya, lo]
                rings.append([pt(s, side * half(w, k, max(y, k)), max(y, k)) for y in ys])
            b.skin(mat, rings, axis=lambda i: pt(stations[i][0], 0, (deck_y + keel_fn(stations[i][0])) / 2), closed=False)
    bottom = [[pt(s, half(w, keel_fn(s), keel_fn(s)), keel_fn(s)), pt(s, -half(w, keel_fn(s), keel_fn(s)), keel_fn(s))] for s, w in stations]
    b.skin(bands[-1][0], bottom, axis=lambda i: pt(stations[i][0], 0, deck_y), closed=False)
    deck = [[pt(s, -w, deck_y), pt(s, w, deck_y)] for s, w in stations]
    b.skin(deck_mat, deck, axis=lambda i: pt(stations[i][0], 0, deck_y - 2.0), closed=False)


def icebreaker(b):
    bx, bz = -19.0, 9.0

    def pt(s, lat, y):
        return (bx + lat, y, bz + s)
    stations = [(-7.7, 2.0), (-7.4, 2.25), (-6.0, 2.4), (0.0, 2.4), (4.0, 2.4), (6.0, 2.25), (7.4, 1.75), (8.6, 1.15), (9.6, 0.6), (10.2, 0.15)]
    hull(b, pt, stations, (('Hull', 3.3, 0.5), ('HullDark', 0.5, -0.6), ('Hull', -0.6, None)), 'Deck', 3.3, lambda s: -1.8 if s < 5 else -1.8 + (s - 5) / 5.2 * 1.7)
    for side in (-1, 1):                                                                      # bulwark rail and stanchions, the bow end left open
        for i in range(len(stations) - 3):
            s0, w0 = stations[i]
            s1, w1 = stations[i + 1]
            b.limb('Steel', pt(s0, side * (w0 - 0.05), 4.0), pt(s1, side * (w1 - 0.05), 4.0), 0.035, 0.035, 4)
            b.limb('Steel', pt(s0, side * (w0 - 0.05), 3.65), pt(s1, side * (w1 - 0.05), 3.65), 0.03, 0.03, 4)
        for s in (-7.0, -5.2, -3.4, -1.6, 0.2, 2.0, 3.8, 5.6):
            b.limb('Steel', pt(s, side * 2.35, 3.3), pt(s, side * 2.35, 4.05), 0.03, 0.03, 4)
    # the house: 4.2 x 4.2 x 5.5 from the deck, white, a window band on two floors, a door, stairs
    hx, hz = bx, bz - 3.5
    b.box('White', hx, 5.4, hz, 4.2, 4.2, 5.5, bevel=0.05)
    for fl, wy in ((0, 4.3), (1, 6.1)):
        for q in range(4):
            b.box('Glass', hx + 2.12, wy, hz - 2.0 + q * 1.35, 0.06, 0.8, 0.9)
            b.box('Glass', hx - 2.12, wy, hz - 2.0 + q * 1.35, 0.06, 0.8, 0.9)
        b.box('Glass', hx, wy, hz + 2.78, 0.9, 0.8, 0.06) if fl == 0 else None
    b.box('Grey', hx + 0.9, 4.2, hz + 2.78, 1.0, 1.9, 0.08)
    b.box('WhiteShade', hx, 7.55, hz, 4.4, 0.2, 5.7)
    b.box('White', bx, 8.2, bz - 2.4, 4.4, 1.4, 3.2, bevel=0.05)                              # the bridge, a little wider than the house
    for q in range(5):
        b.box('Glass', bx - 1.7 + q * 0.85, 8.4, bz - 0.78, 0.8, 0.72, 0.05)
    for sx in (-1, 1):
        b.box('White', bx + sx * 2.7, 8.1, bz - 1.6, 1.0, 1.1, 2.0, bevel=0.04)             # wings
        b.box('Glass', bx + sx * 2.7, 8.3, bz - 0.55, 0.8, 0.6, 0.05)
    b.box('WhiteShade', bx, 9.0, bz - 2.4, 4.7, 0.14, 3.5, bevel=0.03)
    b.limb('Steel', (bx, 9.1, bz - 3.0), (bx, 12.2, bz - 3.0), 0.07, 0.05, 5)                 # the mast, a yard and a radar
    b.box('Steel', bx, 11.4, bz - 3.0, 1.8, 0.07, 0.07)
    b.box('Grey', bx + 0.5, 12.1, bz - 3.0, 1.4, 0.1, 0.2)
    b.ico('Orange', bx, 12.35, bz - 3.0, 0.12, 1.0, sub=0)
    # funnel, red with a white band, aft of the bridge
    b.cone('Hull', bx, 7.5, bz - 5.2, 0.62, 0.5, 2.6, 8)
    b.cone('White', bx, 8.8, bz - 5.2, 0.58, 0.55, 0.5, 8)
    b.cone('Hull', bx, 9.35, bz - 5.2, 0.5, 0.5, 0.5, 8)
    b.cone('Black', bx, 10.0, bz - 5.2, 0.5, 0.5, 0.12, 8)
    # two orange lifeboats on davits, a crane on the afterdeck, containers aft, a mast and a winch on the foredeck (the bow tip stays open)
    for sx in (-1, 1):
        b.box('Orange', bx + sx * 2.45, 5.5, hz + 0.2, 0.9, 0.7, 3.2, bevel=0.14)
        b.limb('Steel', (bx + sx * 2.2, 4.4, hz - 1.2), (bx + sx * 2.45, 6.1, hz - 1.2), 0.04, 0.04, 4)
        b.limb('Steel', (bx + sx * 2.2, 4.4, hz + 1.6), (bx + sx * 2.45, 6.1, hz + 1.6), 0.04, 0.04, 4)
    b.limb('Orange', (bx + 1.2, 3.3, bz - 7.0), (bx + 1.2, 6.2, bz - 7.0), 0.18, 0.14, 6)
    b.limb('Orange', (bx + 1.2, 6.2, bz - 7.0), (bx + 1.2, 7.6, bz - 5.2), 0.12, 0.1, 5)
    b.limb('Steel', (bx + 1.2, 7.55, bz - 5.2), (bx + 1.2, 5.0, bz - 5.2), 0.02, 0.02, 3)
    for q in range(2):
        b.box('Orange' if q else 'Hull', bx - 1.0, 3.3 + 0.6 + 1.2 * q, bz - 6.3, 1.4, 1.2, 2.2, bevel=0.03)
    b.limb('Steel', (bx, 3.3, bz + 3.8), (bx, 7.4, bz + 3.8), 0.09, 0.07, 5)
    b.box('Steel', bx, 6.6, bz + 3.8, 1.4, 0.07, 0.07)
    b.box('Grey', bx, 3.6, bz + 5.2, 1.3, 0.6, 0.9, bevel=0.05)
    b.cone('Black', bx, 3.9, bz + 5.2, 0.32, 0.32, 0.3, 8)
    for q in range(4):                                                                       # fenders / a red "ice belt" shows as dents: black patches at the waterline
        b.box('HullDark', bx + 2.4, 0.2, bz - 5 + 3 * q, 0.1, 0.9, 1.6)
        b.box('HullDark', bx - 2.4, 0.2, bz - 5 + 3 * q, 0.1, 0.9, 1.6)
    b.spot('Spot_Icebreaker', bx, 3.3, bz + 8.5)


# ---------------------------------------------------------------- the icebergs, the tower and the arch
def iceberg(b):
    bx, bz = -5.0, 8.0
    # the main body: a faceted column from -7 to the flat top at 8, wider under water, no spurs on the diving side
    layered(b, bx, bz, -7.0, 8.0, 6.7, 4.35, 8, 3, ['IceA', 'IceWhite', 'IceA', 'IceB', 'IceWhite'], jit=0.05, steps=24, flare=0.35, rot=0.3927)
    # a low faceted snow dome: at the diving edge (4.3 m out) it is level with the 8 m mark and rises gently towards the middle
    b.cone('Snow', bx, 7.55, bz, 4.4, 4.0, 0.5, 8)
    b.cone('SnowShade', bx, 7.95, bz, 4.0, 2.4, 0.55, 8)
    b.cone('Snow', bx, 8.4, bz, 2.4, 0.9, 0.35, 8)
    b.spot('Spot_Iceberg', bx, 8.0, bz + 4.3 * 0.924 - 0.15)


def iceberg_small(b):
    bx, bz = 16.0, 28.0
    layered(b, bx, bz, -5.5, 5.5, 4.7, 3.0, 7, 9, ['IceA', 'IceWhite', 'IceB', 'IceA'], jit=0.06, steps=16, flare=0.3, rot=0.2)
    layered(b, bx, bz, 5.5, 8.8, 3.0, 0.25, 7, 13, ['IceWhite', 'IceA', 'IceWhite'], jit=0.05, steps=6, rot=0.2)
    # a small snow shoulder on the glacier side, level with the diving mark at 5.5
    b.cone('Snow', bx, 5.2, bz - 1.4, 2.2, 2.0, 0.34, 8)
    b.cone('IceA', bx, 4.0, bz - 1.4, 2.4, 2.2, 1.3, 8)
    b.spot('Spot_IcebergSmall', bx, 5.5, bz - 2.7)


def ice_tower(b):
    tx, tz = -30.0, 26.0
    layered(b, tx, tz, -6.0, 16.0, 3.9, 2.55, 6, 14, ['IceB', 'IceA', 'IceWhite', 'IceB'], jit=0.05, steps=30, flare=0.4, rot=0.0)
    b.cone('Snow', tx, 15.6, tz, 2.6, 2.4, 0.42, 6)
    b.spot('Spot_IceTower', tx + 2.2, 16.0, tz)


def ice_arch(b):
    # two thick columns and a snow-capped ice bridge between and beyond them
    for px in (27.0, 37.0):
        layered(b, px, 0.5, -6.0, 20.0, 2.9, 2.2, 7, int(px), ['IceA', 'IceWhite', 'IceB', 'IceA'], jit=0.05, steps=34, flare=0.4, rot=0.45)
    # the bridge, rings across x: snow on top at 20, a curved soffit between the columns
    x0, x1, z0, z1 = 24.2, 39.8, -3.0, 4.1
    stations = [x0 + (x1 - x0) * i / 22 for i in range(23)]

    rings = []
    for x in stations:
        bot = 19.35 - 1.5 * smooth((abs(x - 32.0) - 3.4) / 3.0) if abs(x - 32.0) > 3.4 else 19.35
        bot = min(bot, 19.4)
        rings.append([(x, 20.0, z0 + 0.2), (x, 20.0, z1 - 0.1), (x, 19.8, z1 + 0.1), (x, bot + 0.2, z1 + 0.05), (x, bot - 0.1, z1 - 0.8), (x, bot - 0.1, z0 + 0.9), (x, bot + 0.2, z0 - 0.05), (x, 19.8, z0)])
    b.skin('IceA', rings, axis=lambda k: (stations[k], 19.6, 0.5), closed=True, cap_a=True, cap_b=True)
    topr = [[(x, 20.0, z0 + 0.2), (x, 20.0, z1 - 0.1)] for x in stations]
    b.skin('Snow', topr, axis=lambda k: (stations[k], 18.0, 0.5), closed=False)
    for k in range(8):                                                                      # icicles under the soffit, away from the front edge
        x = 25.0 + 14.0 * hash01(k * 1.7 + 9)
        icicle(b, x, 19.0, z0 + 1.2 + 1.2 * hash01(k), 0.6 + 1.0 * hash01(k + 3), 0.06)
    b.spot('Spot_Arch', 32.0, 20.0, 3.9)


# ---------------------------------------------------------------- the top: pines, a polar station, markers
def top_garden(b, drift):
    def ty(x, z):
        return TOP + drift(x, z) * 0.95
    placed = []
    for k in range(60):
        x = -42 + 84 * hash01(k * 2.9 + 71)
        z = -28 + 19 * hash01(k * 3.7 + 72)
        if any(math.hypot(x - px, z - pz) < 3.0 for px, pz in placed):
            continue
        if abs(x - 21) < 4 and z > -12:
            continue
        if math.hypot(x + 8, z + 17) < 8.0:
            continue
        placed.append((x, z))
        pine(b, x, ty(x, z), z, 0.9 + 0.7 * hash01(k + 3), k)
    # the polar station
    sx, sz = -8.0, -17.0
    y = ty(sx, sz)
    b.box('StationWall', sx, y + 1.6, sz, 6.0, 3.2, 3.6, bevel=0.06)
    b.skin('StationRoof', [[(sx - 3.3, y + 3.1, sz - 2.0), (sx + 3.3, y + 3.1, sz - 2.0)], [(sx - 3.3, y + 4.4, sz), (sx + 3.3, y + 4.4, sz)]], axis=lambda k: (sx, y, sz), closed=False)
    b.skin('StationRoof', [[(sx - 3.3, y + 3.1, sz + 2.0), (sx + 3.3, y + 3.1, sz + 2.0)], [(sx - 3.3, y + 4.4, sz), (sx + 3.3, y + 4.4, sz)]], axis=lambda k: (sx, y, sz), closed=False)
    for sgn in (-1, 1):
        b.skin('StationWall', [[(sx + sgn * 3.0, y + 3.2, sz - 1.8), (sx + sgn * 3.0, y + 3.2, sz + 1.8)], [(sx + sgn * 3.0, y + 4.3, sz), (sx + sgn * 3.0, y + 4.3, sz)]],
               axis=lambda k: (sx, y + 1, sz), closed=False)
    for q in range(3):
        b.box('Glass', sx - 1.8 + q * 1.8, y + 1.8, sz + 1.83, 0.9, 0.9, 0.06)
        b.box('White', sx - 1.8 + q * 1.8, y + 2.35, sz + 1.86, 1.1, 0.1, 0.06)
    b.box('Grey', sx + 2.2, y + 1.0, sz + 1.84, 0.9, 2.0, 0.08)
    b.box('WhiteShade', sx + 2.2, y + 0.1, sz + 2.4, 1.4, 0.2, 1.0)
    b.box('Black', sx - 2.0, y + 4.7, sz - 0.5, 0.5, 1.2, 0.5)
    b.limb('Steel', (sx + 2.4, y + 4.0, sz - 0.8), (sx + 2.4, y + 9.0, sz - 0.8), 0.07, 0.05, 5)
    for q in range(3):
        b.box('Steel', sx + 2.4, y + 7.0 + q * 0.8, sz - 0.8, 1.6 - q * 0.4, 0.05, 0.05)
    b.box('Panel', sx - 4.6, y + 1.0, sz - 0.5, 0.15, 1.6, 2.4, rot_y=0)
    b.limb('Steel', (sx - 4.6, y, sz - 1.4), (sx - 4.6, y + 1.2, sz - 1.4), 0.04, 0.04, 4)
    b.box('Wood', sx + 5.0, y + 0.2, sz + 1.2, 2.4, 0.12, 0.7, bevel=0.03)             # a sled, a pair of crates and some drums
    for sgn in (-1, 1):
        b.box('WoodDark', sx + 5.0, y + 0.08, sz + 1.2 + sgn * 0.3, 2.6, 0.06, 0.06)
    b.box('Orange', sx + 4.6, y + 0.5, sz + 1.2, 0.7, 0.5, 0.6, bevel=0.04)
    for q in range(3):
        b.cone('Hull' if q % 2 else 'Orange', sx - 3.8 + 0.7 * q, y, sz + 2.8, 0.28, 0.28, 0.8, 8)
    # a line of orange trail flags from the station towards the glacier ledge ladder
    for q in range(10):
        x = -2.0 + 2.2 * q
        z = -14.0 + 1.0 * q
        yy = ty(x, z)
        b.limb('WoodDark', (x, yy, z), (x, yy + 1.8, z), 0.03, 0.03, 4)
        b.tri('Flag', (x, yy + 1.8, z), (x + 0.6, yy + 1.6, z + 0.1), (x, yy + 1.3, z))
    # a fence of ice posts and rope along the lip, open at the ladder
    x = -43.5
    while x < 43.5:
        if 18.5 < x < 23.5:
            x += 2.4
            continue
        z = wall_z(x, TOP - 0.5) - 1.5
        yy = ty(x, z)
        b.limb('WoodDark', (x, yy - 0.1, z), (x, yy + 1.1, z), 0.055, 0.05, 5)
        xn = x + 2.4
        if xn < 43.5 and not (18.5 < xn < 23.5):
            zn = wall_z(xn, TOP - 0.5) - 1.5
            for hy in (0.9, 0.5):
                rope(b, (x, yy + hy, z), (xn, ty(xn, zn) + hy, zn), 0.08)
        x += 2.4


# ---------------------------------------------------------------- life: tents, crates, flags, a snowman, a lookout, a pond ...
def tent(b, x, y, z, length=2.6, width=2.0, height=1.5, yaw=0.0, mat='Tent'):
    """An A-frame tent, ridge along x (yaw about the vertical): two canvas slopes, two end triangles with a door flap, guy ropes."""
    c, s_ = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))

    def P(dx, dy, dz):
        return (x + dx * c + dz * s_, y + dy, z - dx * s_ + dz * c)
    hl, hw = length / 2, width / 2
    for sgn in (-1, 1):
        b.skin(mat, [[P(-hl, 0.0, sgn * hw), P(hl, 0.0, sgn * hw)], [P(-hl, height, 0), P(hl, height, 0)]], axis=lambda k: P(0, -1.0, 0), closed=False)
    for sgn in (-1, 1):
        b.tri('TentDark' if sgn > 0 else mat, P(sgn * hl, 0.0, -hw), P(sgn * hl, 0.0, hw), P(sgn * hl, height, 0))
    b.tri('Coal', P(hl + 0.01, 0.0, -0.35), P(hl + 0.01, 0.0, 0.35), P(hl + 0.01, height * 0.62, 0))
    for sgn in (-1, 1):
        rope(b, P(-hl, height, 0), P(-hl - 0.9, 0.0, sgn * 0.8), 0.05, 'Rope', 0.015, 3)
        rope(b, P(hl, height, 0), P(hl + 0.9, 0.0, sgn * 0.8), 0.05, 'Rope', 0.015, 3)


def flagpole(b, x, y, z, h=4.0, mat='Flag'):
    b.limb('WoodDark', (x, y - 0.1, z), (x, y + h, z), 0.05, 0.035, 5)
    b.tri(mat, (x, y + h, z), (x + 1.0, y + h - 0.25, z + 0.05), (x, y + h - 0.6, z))
    b.ico('Steel', x, y + h + 0.05, z, 0.06, 1.0, sub=0)


def crate_box(b, x, y, z, s=0.9, yaw=0.0, mat='Wood'):
    b.box(mat, x, y + s / 2, z, s, s, s, bevel=0.03, rot_y=yaw)
    b.box('WoodDark', x, y + s / 2, z, s + 0.02, 0.06, s + 0.02, rot_y=yaw)
    b.box('WoodDark', x, y + s * 0.15, z, s + 0.02, 0.05, s + 0.02, rot_y=yaw)


def drum(b, x, y, z, mat='Orange'):
    b.cone(mat, x, y, z, 0.28, 0.28, 0.8, 8)
    b.cone('Steel', x, y + 0.22, z, 0.29, 0.29, 0.04, 8)
    b.cone('Steel', x, y + 0.54, z, 0.29, 0.29, 0.04, 8)


def lantern(b, x, y, z, h=1.3):
    b.limb('WoodDark', (x, y, z), (x, y + h, z), 0.04, 0.035, 5)
    b.box('Steel', x, y + h + 0.12, z, 0.26, 0.04, 0.26)
    b.box('Lit', x, y + h + 0.0, z, 0.2, 0.22, 0.2)
    b.cone('Steel', x, y + h + 0.14, z, 0.2, 0.02, 0.16, 4)


def snowman(b, x, y, z, s=1.0):
    b.ico('Snow', x, y + 0.45 * s, z, 0.55 * s, 0.9, sub=1, jit=0.04, seed=1)
    b.ico('Snow', x, y + 1.15 * s, z, 0.4 * s, 0.9, sub=1, jit=0.04, seed=2)
    b.ico('Snow', x, y + 1.7 * s, z, 0.28 * s, 0.9, sub=1, jit=0.04, seed=3)
    b.cone('Carrot', x, y + 1.7 * s, z + 0.27 * s, 0.05 * s, 0.01, 0.35 * s, 5)
    for dx in (-0.09, 0.09):
        b.ico('Coal', x + dx * s, y + 1.78 * s, z + 0.24 * s, 0.03 * s, 1.0, sub=0)
    b.cone('Scarf', x, y + 1.43 * s, z, 0.31 * s, 0.31 * s, 0.1 * s, 8)
    for sg in (-1, 1):
        b.limb('Trunk', (x + sg * 0.35 * s, y + 1.2 * s, z), (x + sg * 0.85 * s, y + 1.55 * s, z), 0.025, 0.012, 3)


def rail_posts(b, pts, y, h=1.0, ropes=(0.9, 0.5)):
    """Posts at every point of the polyline `pts` (x, z) with rope between them, standing on y."""
    for (x, z) in pts:
        b.limb('WoodDark', (x, y - 0.15, z), (x, y + h, z), 0.05, 0.045, 5)
    for (x0, z0), (x1, z1) in zip(pts, pts[1:]):
        for hy in ropes:
            rope(b, (x0, y + hy, z0), (x1, y + hy, z1), 0.06, 'Rope', 0.014, 3)


def extras(b, drift):
    # --- on the arch: a camp at the back, a railing round the sides and back, a snowman, a flag, a ladder up to the plateau
    ay = 20.0
    tent(b, 27.6, ay, -0.9, 2.8, 2.1, 1.6, 90.0)
    for q, (cx_, cz_) in enumerate(((37.2, -1.7), (38.2, -1.2))):
        crate_box(b, cx_, ay, cz_, 0.9 - 0.1 * q, 12 * q)
    crate_box(b, 37.6, ay + 0.9, -1.55, 0.7, 25)
    drum(b, 36.3, ay, -2.2)
    drum(b, 36.7, ay, -1.6, 'Hull')
    snowman(b, 34.8, ay, -0.6, 1.0)
    flagpole(b, 30.6, ay, -2.2, 4.6)
    lantern(b, 32.6, ay, -2.2, 1.2)
    back = [(24.8 + 1.7 * q, -2.75) for q in range(10)]
    rail_posts(b, back, ay, 1.0)
    rail_posts(b, [(24.8, -2.75), (24.8, -1.0), (24.8, 0.8), (24.8, 2.6)], ay, 1.0)
    rail_posts(b, [(39.2, -2.75), (39.2, -1.0), (39.2, 0.8), (39.2, 2.6)], ay, 1.0)
    ladder(b, 38.0, 19.8, 26.6)
    for sx in (37.6, 38.4):
        b.limb('WoodDark', (sx, 25.7, wall_z(sx, 25.7) + 0.5), (sx, 27.3, wall_z(sx, 26.0) - 0.3), 0.05, 0.045, 5)
    # --- the main iceberg: a flag, a small tent and a crate on the back half of the dome (the diving edge stays bare)
    def dome(x, z):
        r = math.hypot(x + 5.0, z - 8.0)
        return 8.55 if r < 1.0 else 8.4 - 0.38 * (r - 1.6) if r < 3.4 else 8.0
    tent(b, -6.4, dome(-6.4, 6.0) - 0.15, 6.0, 2.2, 1.7, 1.3, 25.0)
    flagpole(b, -3.2, dome(-3.2, 5.2) - 0.1, 5.2, 3.6)
    crate_box(b, -4.7, dome(-4.7, 4.6) - 0.1, 4.6, 0.7, 15)
    # --- the ice tower: a beacon tripod on the west half of the top and a pennant
    tx, tz = -30.0, 26.0
    for q in range(3):
        a = q * 2.094 + 0.5
        b.limb('WoodDark', (tx - 1.1 + math.cos(a) * 0.5, 16.0, tz + 0.2 + math.sin(a) * 0.5), (tx - 1.1, 18.0, tz + 0.2), 0.04, 0.03, 4)
    b.ico('RedLight', tx - 1.1, 18.15, tz + 0.2, 0.17, 1.0, sub=0)
    flagpole(b, tx - 1.5, 16.0, tz - 0.9, 2.6)
    # --- the floe: an ice-fishing hole, a rod on a tripod and a bucket (away from the penguins and the diving edge)
    fx, fz = -30.0, 2.0
    b.cone('IceDeep', fx + 0.9, 0.6, fz - 0.2, 0.42, 0.42, 0.03, 8)
    b.cone('Coal', fx + 0.9, 0.63, fz - 0.2, 0.3, 0.3, 0.02, 8)
    b.limb('WoodDark', (fx + 0.9, 0.62, fz - 0.7), (fx + 0.9, 1.7, fz - 0.2), 0.025, 0.02, 4)
    b.limb('Rope', (fx + 0.9, 1.7, fz - 0.2), (fx + 0.9, 0.7, fz - 0.2), 0.008, 0.008, 3)
    b.cone('Orange', fx + 1.5, 0.6, fz - 0.4, 0.2, 0.17, 0.3, 8)
    b.cone('Steel', fx + 1.5, 0.88, fz - 0.4, 0.2, 0.2, 0.02, 8)
    # --- the glacier ledge: a post with a lantern, a coiled rope
    lantern(b, 16.6, 14.0, face_z(16.6) + 0.5, 1.3)
    b.cone('Rope', 19.4, 14.0, face_z(19.4) + 0.4, 0.3, 0.26, 0.14, 8)
    # --- the west shelf: an igloo of snow blocks; the east shelf: a small camp (tents, sled, crates, drums); away from the penguins
    ix, iz, ir = -43.2, 1.2, 1.9
    for k in range(6):
        h0, h1 = k * ir / 6.5, (k + 1) * ir / 6.5
        r0, r1 = math.sqrt(max(ir ** 2 - (h0 * 1.1) ** 2, 0.01)), math.sqrt(max(ir ** 2 - (h1 * 1.1) ** 2, 0.01))
        b.cone('Snow' if k % 2 else 'SnowShade', ix, 0.3 + h0 * 1.1, iz, r0, max(r1, 0.2), (h1 - h0) * 1.1 + 0.02, 10)
    b.box('Coal', ix + 1.5, 0.95, iz + 0.9, 0.9, 1.0, 0.9)
    b.cone('Snow', ix + 1.0, 0.4, iz + 0.7, 0.7, 0.6, 0.6, 6)
    tent(b, 43.0, 0.55, -1.2, 2.6, 2.0, 1.5, 0.0)
    b.box('Sled', 41.8, 0.75, 1.6, 2.2, 0.1, 0.6, bevel=0.03, rot_y=15)
    for sg in (-1, 1):
        b.box('WoodDark', 41.8, 0.62, 1.6 + sg * 0.25, 2.3, 0.05, 0.05, rot_y=15)
    crate_box(b, 41.6, 0.82, 1.55, 0.6, 15)
    drum(b, 44.0, 0.6, 0.8)
    drum(b, 44.0, 0.6, 1.5, 'Hull')
    flagpole(b, 40.0, 0.6, -1.9, 3.2)
    # --- the plateau: a lookout tower, a snowmobile and a sledge, a satellite dish, an ice pond with a bench, drifted shrubs, drums by the station
    def ty(x, z):
        return TOP + drift(x, z) * 0.95
    lx, lz = -29.0, -14.0
    y0 = ty(lx, lz)
    for dx in (-1.0, 1.0):
        for dz in (-1.0, 1.0):
            b.limb('WoodDark', (lx + dx * 1.2, y0 - 0.1, lz + dz * 1.2), (lx + dx * 0.9, y0 + 5.0, lz + dz * 0.9), 0.1, 0.08, 5)
    for hy in (1.6, 3.4):
        b.box('WoodDark', lx, y0 + hy, lz, 2.5 - 0.15 * hy, 0.1, 0.1)
        b.box('WoodDark', lx, y0 + hy, lz + 1.1 - 0.06 * hy, 0.1, 0.1, 2.0)
    b.box('Wood', lx, y0 + 5.0, lz, 2.4, 0.16, 2.4, bevel=0.03)
    for sgn in (-1, 1):
        b.limb('WoodDark', (lx + sgn * 1.1, y0 + 5.0, lz - 1.1), (lx + sgn * 1.1, y0 + 6.1, lz - 1.1), 0.045, 0.045, 5)
        b.limb('WoodDark', (lx + sgn * 1.1, y0 + 5.0, lz + 1.1), (lx + sgn * 1.1, y0 + 6.1, lz + 1.1), 0.045, 0.045, 5)
    for hy in (5.6, 6.0):
        b.box('WoodDark', lx, y0 + hy, lz - 1.1, 2.3, 0.05, 0.05)
        b.box('WoodDark', lx, y0 + hy, lz + 1.1, 2.3, 0.05, 0.05)
        b.box('WoodDark', lx - 1.1, y0 + hy, lz, 0.05, 0.05, 2.2)
        b.box('WoodDark', lx + 1.1, y0 + hy, lz, 0.05, 0.05, 2.2)
    b.skin('StationRoof', [[(lx - 1.6, y0 + 7.0, lz - 1.5), (lx + 1.6, y0 + 7.0, lz - 1.5)], [(lx - 1.6, y0 + 7.9, lz), (lx + 1.6, y0 + 7.9, lz)]], axis=lambda k: (lx, y0 + 5, lz), closed=False)
    b.skin('StationRoof', [[(lx - 1.6, y0 + 7.0, lz + 1.5), (lx + 1.6, y0 + 7.0, lz + 1.5)], [(lx - 1.6, y0 + 7.9, lz), (lx + 1.6, y0 + 7.9, lz)]], axis=lambda k: (lx, y0 + 5, lz), closed=False)
    for q in range(4):
        b.limb('WoodDark', (lx + (q % 2) * 1.4 - 0.7, y0 + 5.0, lz + (q // 2) * 1.4 - 0.7), (lx + (q % 2) * 1.2 - 0.6, y0 + 7.0, lz + (q // 2) * 1.2 - 0.6), 0.05, 0.05, 4)
    for k in range(10):                                                                  # a ladder up the lookout
        b.box('WoodDark', lx - 0.3 + 0.0, y0 + 0.4 + k * 0.5, lz + 1.5 - 0.05 * k, 0.5, 0.05, 0.05)
    mx, mz = -1.0, -12.5
    y0 = ty(mx, mz)
    b.box('Hull', mx, y0 + 0.55, mz, 0.9, 0.5, 1.9, bevel=0.1)
    b.box('Black', mx, y0 + 0.9, mz - 0.1, 0.8, 0.2, 0.8, bevel=0.06)
    b.box('Hull', mx, y0 + 0.7, mz + 0.9, 0.6, 0.3, 0.7, bevel=0.1)
    for sgn in (-1, 1):
        b.box('Black', mx + sgn * 0.3, y0 + 0.08, mz + 1.1, 0.12, 0.05, 0.9, rot_y=sgn * 6)
        b.box('Black', mx + sgn * 0.45, y0 + 0.2, mz - 0.7, 0.3, 0.25, 0.5)
    b.limb('Steel', (mx, y0 + 1.0, mz + 0.35), (mx, y0 + 1.25, mz + 0.35), 0.03, 0.03, 4)
    b.box('Steel', mx, y0 + 1.27, mz + 0.35, 0.7, 0.04, 0.04)
    b.box('Sled', mx + 2.0, y0 + 0.15, mz + 0.2, 0.7, 0.1, 1.6, bevel=0.03)
    for sgn in (-1, 1):
        b.box('WoodDark', mx + 2.0 + sgn * 0.3, y0 + 0.05, mz + 0.2, 0.05, 0.05, 1.8)
    b.ico('Orange', mx + 2.0, y0 + 0.35, mz + 0.1, 0.3, 0.6, sub=1, sc=(1.0, 0.7, 1.2), jit=0.1, seed=3)
    dx_, dz_ = -14.2, -15.0
    y0 = ty(dx_, dz_)
    b.limb('Steel', (dx_, y0, dz_), (dx_, y0 + 1.4, dz_), 0.07, 0.06, 5)
    b.cone('Dish', dx_, y0 + 1.3, dz_ + 0.2, 0.15, 1.1, 0.5, 12)
    b.limb('Steel', (dx_, y0 + 1.55, dz_ + 0.3), (dx_ + 0.1, y0 + 2.1, dz_ + 1.0), 0.02, 0.02, 3)
    px_, pz_ = 14.0, -20.0
    y0 = ty(px_, pz_)
    b.cone('SnowShade', px_, y0 - 0.1, pz_, 5.0, 4.6, 0.25, 12)
    b.cone('BlueIce', px_, y0 + 0.13, pz_, 4.3, 4.1, 0.05, 12)
    bench_y = ty(px_ + 0.5, pz_ + 5.4)
    b.box('Wood', px_ + 0.5, bench_y + 0.45, pz_ + 5.4, 1.8, 0.07, 0.5, bevel=0.02)
    b.box('Wood', px_ + 0.5, bench_y + 0.9, pz_ + 5.7, 1.8, 0.4, 0.06, bevel=0.02)
    for sg in (-0.75, 0.75):
        b.box('WoodDark', px_ + 0.5 + sg, bench_y + 0.22, pz_ + 5.4, 0.08, 0.45, 0.44)
    sy = ty(-4.0, -19.5)
    drum(b, -11.2, ty(-11.2, -14.2), -14.2)
    drum(b, -11.8, ty(-11.8, -14.4), -14.4, 'Hull')
    crate_box(b, -12.2, ty(-12.2, -13.4), -13.4, 0.8, 10)
    for k in range(26):                                                                  # snow-capped shrubs among the pines
        x = -42 + 84 * hash01(k * 2.3 + 301)
        z = -28 + 19 * hash01(k * 3.1 + 302)
        if math.hypot(x + 8, z + 17) < 9.0 or abs(x - 21) < 4 or math.hypot(x - lx, z - lz) < 3.5 or math.hypot(x - px_, z - pz_) < 6.0:
            continue
        y = ty(x, z)
        b.ico('PineGreenDark', x, y + 0.25, z, 0.5 + 0.2 * hash01(k), 0.8, sub=1, jit=0.15, seed=k + 200)
        b.ico('Snow', x, y + 0.55, z, 0.4 + 0.15 * hash01(k + 1), 0.55, sub=1, sc=(1.1, 0.5, 1.0), jit=0.12, seed=k + 230)


# ---------------------------------------------------------------- loose floes in the far corner of the sea
def brash(b):
    for k in range(7):
        x = -14 + 54 * hash01(k * 2.9 + 3)
        z = 46 + 12 * hash01(k * 3.7 + 4)
        if z > 58:
            continue
        r = 1.2 + 1.0 * hash01(k + 5)
        b.cone('Snow', x, 0.0, z, r, r * 0.95, 0.12, 7)
        b.cone('IceWet', x, -1.0, z, r * 0.8, r, 1.0, 7)


# ---------------------------------------------------------------- build
def build():
    b = locbuild.Builder()
    for k, v in COLOURS.items():
        b.mat(k, v)
    block_base(b)
    drift = wall(b)
    glacier_detail(b)
    ledge(b, 18.0, 14.0, 2.9)
    access(b)
    shelves(b)
    floe(b)
    icebreaker(b)
    iceberg(b)
    iceberg_small(b)
    ice_tower(b)
    ice_arch(b)
    top_garden(b, drift)
    extras(b, drift)
    brash(b)
    return b


def add_preview_water():
    import bmesh
    me = bpy.data.meshes.new('PreviewWater')
    ob = bpy.data.objects.new('PreviewWater', me)
    bpy.context.scene.collection.objects.link(ob)
    bm = bmesh.new()
    v = [bm.verts.new((x, y, 0.0)) for x, y in ((-HALF, 30.0), (HALF, 30.0), (HALF, -60.0), (-HALF, -60.0))]
    bm.faces.new(v)
    bm.to_mesh(me)
    bm.free()
    m = bpy.data.materials.new('PreviewWater')
    m.use_nodes = True
    for n in m.node_tree.nodes:
        if n.type == 'BSDF_PRINCIPLED':
            n.inputs['Base Color'].default_value = (0.05, 0.3, 0.45, 1)
            n.inputs['Alpha'].default_value = 0.6
            n.inputs['Roughness'].default_value = 0.25
    try:
        m.surface_render_method = 'BLENDED'
    except Exception:
        pass
    me.materials.append(m)


def main():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    b = build()
    objs = b.finish('Loc_Arctic')
    print('LOCATION TRIS', b.tris, 'objects', len(objs))
    b.export(OUT)
    add_preview_water()
    view.shoot('loc_arctic_overview', (0, 8, 12), 150, (0.35, 0.55, 1.0), lens=50, res=(1200, 800))
    view.shoot('loc_arctic_ship', (-19, 4, 9), 28, (0.9, 0.45, 0.7), lens=50, res=(1100, 700))
    view.shoot('loc_arctic_arch', (32, 14, 0), 55, (0.2, 0.3, 1.0), lens=50, res=(1100, 700))
    view.shoot('loc_arctic_top', (0, 26, -16), 70, (0.25, 0.45, 1.0), lens=50, res=(1100, 700))
    view.shoot('loc_arctic_bergs', (-8, 4, 18), 60, (0.3, 0.3, 1.0), lens=50, res=(1100, 700))
    print('LOC DONE')


if __name__ == '__main__':
    main()
