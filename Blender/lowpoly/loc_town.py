"""Location 6, the old town on a canal, as one scene (Docs/ModelBrief.md section 3.6). Output: Models/Locations/Loc_Town.fbx + previews.
    blender -b --python Blender/lowpoly/loc_town.py
Site space like the game's: the block is 90 x 90 m, x -45..45, z -30..60, water at y = 0 (not modelled), canal floor at -6. The near quay is z < -3
(top 0.9), the far quay z > 48. Houses, the balconies, the footbridge, the bell tower, the cathedral and the clock tower stand exactly where the game's
solid boxes are; the gondolas are the game's own.
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

OUT = os.path.normpath(os.path.join(HERE, '..', '..', 'Assets', '_Project', 'Models', 'Locations', 'Loc_Town.fbx'))

COLOURS = {
    'Soil': '#5A4838', 'Earth': '#6C5440', 'BlockRock': '#58524C', 'Canal': '#6F8F86', 'CanalDark': '#58766F', 'Moss': '#5A7A4A', 'Slime': '#4C6F4A',
    'Stone': '#C9BEAA', 'StoneDark': '#9A9080', 'StoneLight': '#DDD3C0', 'Brick': '#B3654A', 'Roof': '#B5512F', 'RoofDark': '#9A4426',
    'Glass': '#2B3A48', 'Wood': '#7A5233', 'WoodDark': '#5A3D26', 'Trim': '#EDE5D4', 'Shutter': '#4F7A6B', 'ShutterB': '#7A5A8A', 'Door': '#7A4E2D',
    'Leaf': '#4F8F43', 'LeafDark': '#3C7A38', 'Flower': '#E85F5F', 'FlowerYellow': '#F0C43A', 'FlowerWhite': '#F4F1E8', 'FlowerPink': '#E88FB0',
    'Iron': '#2D3138', 'Dome': '#7BA8A0', 'DomeRib': '#5E8C85', 'Gold': '#D9B04A', 'ClockFace': '#F4F1E8', 'Bell': '#B8873A', 'Terracotta': '#C4623F',
    'AwnRed': '#C9413A', 'AwnWhite': '#F4F1E8', 'AwnGreen': '#4E7A4A', 'Lit': '#FFD98A', 'Rope': '#D9C9A0', 'Cloth': '#F4F1E8', 'ClothB': '#6A9BC4',
    'Sand': '#E8C99B', 'Salmon': '#E7A58D', 'Cream': '#EDE1B4', 'Mint': '#9CC7B8', 'Mauve': '#C9A3B8', 'Yellow': '#F0D58A',
    'Terra': '#D9895F', 'Ochre': '#D9A441', 'Peach': '#F2C6A0', 'Sky': '#A9C9DC', 'Olive': '#B5B77A', 'Rose': '#D99AA3', 'Tank': '#8A929A', 'Fruit': '#E8742A', 'FruitG': '#8FB04A',
    'PoleRed': '#C9413A', 'PoleWhite': '#F4F1E8', 'BoatRed': '#B23A32', 'BoatWhite': '#EDEBE4',
}
PLASTER = ['Sand', 'Salmon', 'Cream', 'Mint', 'Mauve', 'Yellow', 'Terra', 'Ochre', 'Peach', 'Sky', 'Olive', 'Rose']

QUAY_TOP = 0.9
HALF = 45.0


def hash01(i):
    return (math.sin(i * 12.9898) * 43758.5453) % 1.0


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def rope(b, p0, p1, sag=0.3, mat='Rope', r=0.03, n=5):
    pts = []
    for i in range(n + 1):
        t = i / n
        pts.append((p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t - sag * 4 * t * (1 - t), p0[2] + (p1[2] - p0[2]) * t))
    for a, c in zip(pts, pts[1:]):
        b.limb(mat, a, c, r, r, 4)


def flowers(b, x, y, z, seed=0, n=3, spread=0.35):
    for k in range(n):
        a = hash01(seed * 5 + k) * 6.28
        d = hash01(seed * 7 + k) * spread
        b.ico(('Flower', 'FlowerYellow', 'FlowerWhite', 'FlowerPink')[(seed + k) % 4], x + math.cos(a) * d, y + 0.08, z + math.sin(a) * d, 0.08, 0.8, sub=0)


def planter(b, x, y, z, w=0.9, seed=0, depth=0.3):
    b.box('Terracotta', x, y + 0.14, z, w, 0.28, depth, bevel=0.02)
    for k in range(2):
        b.ico('Leaf' if k % 2 else 'LeafDark', x - w * 0.22 + k * w * 0.44, y + 0.38, z, 0.2, 0.8, sub=0, jit=0.1, seed=seed + k)
    flowers(b, x, y + 0.4, z, seed, 2, w * 0.4)


def pot(b, x, y, z, s=1.0, seed=0):
    b.cone('Terracotta', x, y, z, 0.2 * s, 0.14 * s, 0.34 * s, 8)
    b.ico('Leaf', x, y + 0.5 * s, z, 0.28 * s, 0.9, sub=1, jit=0.15, seed=seed)
    flowers(b, x, y + 0.6 * s, z, seed, 3, 0.2 * s)


def lamp(b, x, z, o=1.0, y=QUAY_TOP):
    """A wrought-iron street lamp with a curled arm over the canal (o = +1 leans to +z)."""
    b.cone('Iron', x, y, z, 0.16, 0.1, 0.5, 6)
    b.limb('Iron', (x, y + 0.4, z), (x, y + 3.6, z), 0.06, 0.045, 6)
    b.limb('Iron', (x, y + 3.5, z), (x, y + 3.8, z + 0.35 * o), 0.04, 0.035, 5)
    b.limb('Iron', (x, y + 3.8, z + 0.35 * o), (x, y + 3.7, z + 0.7 * o), 0.035, 0.03, 5)
    b.box('Iron', x, y + 3.5, z + 0.7 * o, 0.3, 0.05, 0.3)
    b.box('Lit', x, y + 3.32, z + 0.7 * o, 0.22, 0.34, 0.22)
    b.cone('Iron', x, y + 3.5, z + 0.7 * o, 0.2, 0.02, 0.18, 4)


def bollard(b, x, z, y=QUAY_TOP):
    b.cone('StoneDark', x, y, z, 0.28, 0.22, 0.5, 8)
    b.cone('StoneLight', x, y + 0.48, z, 0.3, 0.3, 0.08, 8)
    b.ico('StoneDark', x, y + 0.6, z, 0.2, 0.7, sub=1, jit=0.04, seed=int(x * 3))


# ---------------------------------------------------------------- the block and the canal
def block_base(b):
    top = -6.1
    b.box('Soil', 0, top - 0.7, 15, 90, 1.4, 90)
    b.box('Earth', 0, (top - 1.4 - 9.0) / 2, 15, 90, (top - 1.4) - (-9.0), 90)
    b.box('BlockRock', 0, (-9.0 - 16.0) / 2, 15, 90, 7.0, 90)
    b.box('Canal', 0, -6.05, 15, 90, 0.1, 90)
    for k in range(34):
        x, z = -42 + 84 * hash01(k * 3.7 + 1), -2 + 52 * hash01(k * 6.1 + 2)
        b.ico('CanalDark', x, -6.0, z, 1.0, 1.0, sub=1, sc=(2.0 + 3 * hash01(k + 9), 0.07, 0.7 + 0.5 * hash01(k + 4)), jit=0.1, seed=k)
    for k in range(18):                                        # cobbles and fallen bricks on the canal floor
        x, z = -40 + 80 * hash01(k * 2.9 + 11), 0 + 46 * hash01(k * 5.3 + 12)
        b.ico('StoneDark' if k % 2 else 'Brick', x, -6.0, z, 0.3 + 0.25 * hash01(k), 0.6, sub=1, jit=0.15, seed=k + 40)
    for k in range(10):                                        # weed
        x, z = -38 + 76 * hash01(k * 4.1 + 21), 2 + 44 * hash01(k * 3.3 + 22)
        for j in range(4):
            a = j * 1.6
            b.limb('Moss', (x + math.cos(a) * 0.3, -6.0, z + math.sin(a) * 0.3), (x + math.cos(a) * 0.5, -4.6 + 0.4 * hash01(k + j), z + math.sin(a) * 0.5), 0.05, 0.015, 3)


def quay(b, far):
    """The stone embankment: paved top with a worn cobble pattern, a darker coping, block joints and weed on the canal face, bollards, rings."""
    if not far:
        z0, z1, edge = -30.0, -3.0, -3.0
        o = 1.0          # towards the canal
    else:
        z0, z1, edge = 48.0, 60.0, 48.0
        o = -1.0
    b.box('Stone', 0, (QUAY_TOP - 6.0) / 2, (z0 + z1) / 2, 90, QUAY_TOP + 6.0, z1 - z0)
    # cobbles on the promenade in front of the houses
    pz0, pz1 = (-4.2, -3.0) if not far else (48.0, 51.0)
    nrow = max(2, int((pz1 - pz0) / 0.6))
    for r in range(nrow):
        zc = pz0 + (r + 0.5) * (pz1 - pz0) / nrow
        x = -44.8 + 0.3 * (r % 2)
        k = 0
        while x < 44.8:
            w = 0.5 + 0.35 * hash01(k * 3.7 + r * 11 + (5 if far else 0))
            tone = hash01(k * 1.3 + r * 7.7)
            b.box('StoneLight' if tone > 0.7 else ('StoneDark' if tone < 0.22 else 'Stone'), x + w / 2, QUAY_TOP + 0.006, zc, w - 0.04, 0.012, (pz1 - pz0) / nrow - 0.04)
            x += w
            k += 1
    b.box('StoneDark', 0, QUAY_TOP - 0.04, edge - 0.04 * o + (0.0), 90, 0.12, 0.42)
    b.box('StoneLight', 0, QUAY_TOP + 0.015, edge - 0.15 * o, 90, 0.03, 0.14)
    for x in range(-45, 46, 3):                                                        # joints in the face
        b.box('CanalDark', x, (QUAY_TOP - 6.0) / 2, edge + 0.02 * o, 0.05, QUAY_TOP + 6.0, 0.05)
    for y in (0.3, -0.7, -1.9, -3.4):
        b.box('CanalDark', 0, y, edge + 0.02 * o, 90, 0.05, 0.05)
    for k in range(70):                                                                # green slime at the waterline
        x = -44 + 88 * hash01(k * 2.3 + (3 if far else 8))
        b.ico('Slime', x, -0.3 - 0.3 * hash01(k), edge + 0.04 * o, 0.45, 0.4, sub=0, sc=(2.2, 1.0, 0.5))
    for x in range(-43, 44, 7):
        z = edge - 0.5 * o
        bollard(b, x, z)
        b.box('Iron', x, QUAY_TOP - 0.6, edge + 0.03 * o, 0.32, 0.32, 0.06)
        b.box('Slime', x, QUAY_TOP - 0.6, edge + 0.07 * o, 0.18, 0.18, 0.04)
    for x in (-40, -14, 14, 38):
        for dx in (-0.25, 0.25):
            b.limb('Iron', (x + dx, QUAY_TOP, edge + 0.08 * o), (x + dx, -2.0, edge + 0.08 * o), 0.03, 0.03, 4)
        yy = QUAY_TOP - 0.3
        while yy > -2.0:
            b.box('Iron', x, yy, edge + 0.08 * o, 0.5, 0.04, 0.04)
            yy -= 0.35


# ---------------------------------------------------------------- houses
# ---------------------------------------------------------------- houses: six kinds of facade, five kinds of roof, backs and sides that are not blank
def disc(b, mat, x, y, z, r, o, t=0.05):
    b.limb(mat, (x, y, z), (x, y, z + t * o), r, r, 12)


def window(b, x, y, zf, o, w=0.9, h=1.4, shut='Shutter', flowerbox=False, seed=0, kind='plain'):
    """A window on the wall at z = zf, looking towards o. kind: plain, round (semicircle head), noble (tall, with a little pediment), small (no shutters)."""
    zz = zf
    round_top = kind == 'round'
    b.box('Trim', x, y, zz + 0.04 * o, w + 0.28, h + 0.28, 0.08)
    b.box('Glass', x, y, zz + 0.09 * o, w, h, 0.05)
    if round_top:
        disc(b, 'Trim', x, y + h / 2, zz + 0.04 * o, (w + 0.28) / 2, o, 0.08)
        disc(b, 'Glass', x, y + h / 2, zz + 0.09 * o, w / 2, o, 0.05)
    b.box('Trim', x, y, zz + 0.13 * o, 0.05, h + (w / 2 if round_top else 0.0), 0.04)
    b.box('Trim', x, y + 0.1, zz + 0.13 * o, w, 0.05, 0.04)
    if kind != 'small':
        for sx in (-1, 1):
            b.box(shut, x + sx * (w / 2 + 0.2), y + (0.12 if round_top else 0.0), zz + 0.1 * o, 0.38, h + (0.35 if round_top else 0.1), 0.06)
    b.box('StoneLight', x, y - h / 2 - 0.2, zz + 0.16 * o, w + 0.55, 0.1, 0.3)
    if kind == 'noble':
        b.tri('StoneLight', (x - w / 2 - 0.3, y + h / 2 + 0.2, zz + 0.1 * o), (x + w / 2 + 0.3, y + h / 2 + 0.2, zz + 0.1 * o), (x, y + h / 2 + 0.7, zz + 0.1 * o))
        b.box('StoneLight', x, y + h / 2 + 0.17, zz + 0.12 * o, w + 0.7, 0.1, 0.14)
        for sx in (-1, 1):
            b.limb('StoneLight', (x + sx * (w / 2 + 0.2), y - h / 2, zz + 0.12 * o), (x + sx * (w / 2 + 0.2), y + h / 2 + 0.1, zz + 0.12 * o), 0.07, 0.07, 6)
    if flowerbox:
        planter(b, x, y - h / 2 - 0.2 + 0.05, zz + 0.38 * o, w + 0.1, seed)


def balconcino(b, x, y, zf, o, w=1.7, seed=0):
    """A small wrought-iron balcony in front of a tall window, on two stone brackets."""
    b.box('StoneLight', x, y - 0.06, zf + 0.45 * o, w, 0.12, 0.9, bevel=0.02)
    for sx in (-1, 1):
        b.box('StoneDark', x + sx * (w / 2 - 0.25), y - 0.3, zf + 0.25 * o, 0.18, 0.4, 0.4)
    for q in range(int(w / 0.22) + 1):
        px = x - w / 2 + 0.03 + q * 0.22
        b.limb('Iron', (px, y, zf + 0.88 * o), (px, y + 0.9, zf + 0.88 * o), 0.017, 0.017, 4)
    for sx in (-1, 1):
        for q in range(4):
            b.limb('Iron', (x + sx * (w / 2 - 0.03), y, zf + (0.1 + q * 0.26) * o), (x + sx * (w / 2 - 0.03), y + 0.9, zf + (0.1 + q * 0.26) * o), 0.017, 0.017, 4)
    b.box('Iron', x, y + 0.9, zf + 0.88 * o, w, 0.04, 0.04)
    for sx in (-1, 1):
        b.box('Iron', x + sx * (w / 2 - 0.03), y + 0.9, zf + 0.5 * o, 0.04, 0.04, 0.8)
    pot(b, x - w / 2 + 0.3, y, zf + 0.5 * o, 0.7, seed)


def archway(b, x, zf, o, w=2.2, h=3.0, seed=0):
    """A passage through the ground floor, an arch with a dark way in and a courtyard glimpsed at the end; steps and a lamp."""
    y0 = QUAY_TOP
    b.box('Black', x, y0 + h / 2, zf + 0.06 * o, w, h, 0.06)
    disc(b, 'Black', x, y0 + h, zf + 0.06 * o, w / 2, o, 0.06)
    b.box('StoneLight', x - w / 2 - 0.18, y0 + h / 2, zf + 0.08 * o, 0.3, h, 0.12)
    b.box('StoneLight', x + w / 2 + 0.18, y0 + h / 2, zf + 0.08 * o, 0.3, h, 0.12)
    for q in range(11):                                                                           # the voussoirs of the arch ring
        a = math.pi * q / 10
        b.box('StoneLight' if q % 2 else 'Stone', x - math.cos(a) * (w / 2 + 0.18), y0 + h + math.sin(a) * (w / 2 + 0.18), zf + 0.08 * o, 0.3, 0.3, 0.12, rot_y=0)
    b.box('Leaf', x, y0 + 0.8, zf + 0.1 * o, 1.0, 1.2, 0.02)                                       # a courtyard tree and a bit of light in the dark
    b.box('Lit', x, y0 + 1.9, zf + 0.1 * o, 0.9, 1.4, 0.02)
    b.box('StoneLight', x, y0 + 0.05, zf + 0.35 * o, w + 0.4, 0.1, 0.6)
    lamp_x = x + w / 2 + 0.7
    b.box('Iron', lamp_x, y0 + 2.9, zf + 0.25 * o, 0.1, 0.1, 0.4)
    b.box('Lit', lamp_x, y0 + 2.7, zf + 0.5 * o, 0.18, 0.26, 0.18)


def shopfront(b, x, w, zf, o, seed):
    """A shop: a wide window with a shutter box above and a hand-painted sign."""
    y0 = QUAY_TOP
    b.box('WoodDark', x, y0 + 1.5, zf + 0.06 * o, w, 2.7, 0.1)
    b.box('Glass', x, y0 + 1.25, zf + 0.12 * o, w - 0.5, 1.7, 0.05)
    for q in range(int((w - 0.5) / 0.7)):
        b.box('WoodDark', x - (w - 0.5) / 2 + 0.35 + q * 0.7, y0 + 1.25, zf + 0.15 * o, 0.05, 1.7, 0.03)
    b.box(('AwnRed', 'AwnGreen', 'Terracotta', 'ClothB')[seed % 4], x, y0 + 2.65, zf + 0.14 * o, w, 0.5, 0.1)
    b.box('Cloth', x, y0 + 2.65, zf + 0.2 * o, w * 0.5, 0.22, 0.03)


def awning_run(b, x, w, y, zf, o, seed):
    n = max(4, int(w / 0.45))
    for q in range(n):
        xa = x - w / 2 + q * w / n
        mat = 'AwnRed' if q % 2 else 'AwnWhite'
        b.skin(mat, [[(xa, y + 0.4, zf + 0.05 * o), (xa + w / n, y + 0.4, zf + 0.05 * o)], [(xa, y - 0.15, zf + 1.0 * o), (xa + w / n, y - 0.15, zf + 1.0 * o)]],
               axis=lambda k: (x, y - 1.0, zf), closed=False)
        b.box(mat, xa + w / (2 * n), y - 0.2, zf + 1.02 * o, w / n, 0.22, 0.03)


def roof_gable(b, x, w, yb, zf, o, depth, rise, ox, col, seed):
    zf_roof = zf + 0.5 * o
    zb_roof = zf - o * (depth + 0.5)
    ridge_z = (zf_roof + zb_roof) / 2
    for zz in (zf_roof, zb_roof):
        b.skin('Roof', [[(x - w / 2 - ox, yb, zz), (x + w / 2 + ox, yb, zz)], [(x - w / 2 - ox, yb + rise, ridge_z), (x + w / 2 + ox, yb + rise, ridge_z)]],
               axis=lambda k: (x, yb - 3.0, ridge_z), closed=False)
    for sx in (-1, 1):
        xg = x + sx * (w / 2 + 0.02)
        b.skin(col, [[(xg, yb, zf), (xg, yb, zf - o * depth)], [(xg, yb + rise * 0.84, ridge_z), (xg, yb + rise * 0.84, ridge_z)]], axis=lambda k: (x, yb - 2.0, ridge_z), closed=False)
    b.box('RoofDark', x, yb + rise + 0.04, ridge_z, w + 0.7, 0.12, 0.3)
    for q in range(1, 5):
        t = q / 5
        for zz in (zf_roof, zb_roof):
            b.box('RoofDark', x, yb + rise * t + 0.03, zz + (ridge_z - zz) * t, w + 0.4, 0.04, 0.05)
    b.box('Trim', x, yb + 0.05, zf + 0.35 * o, w + 0.6, 0.14, 0.14)
    return ridge_z


def roof_hip(b, x, w, yb, zf, o, depth, rise, ox, col, seed):
    zf_roof = zf + 0.5 * o
    zb_roof = zf - o * (depth + 0.5)
    ridge_z = (zf_roof + zb_roof) / 2
    a = min(w / 2 + ox, abs(zb_roof - zf_roof) / 2 - 0.2)
    za, zb = zf_roof - o * a, zb_roof + o * a
    lx, rx = x - w / 2 - ox, x + w / 2 + ox
    b.skin('Roof', [[(lx, yb, zf_roof), (lx, yb, zb_roof)], [(x, yb + rise, za), (x, yb + rise, zb)]], axis=lambda k: (x, yb - 3.0, ridge_z), closed=False)
    b.skin('Roof', [[(rx, yb, zf_roof), (rx, yb, zb_roof)], [(x, yb + rise, za), (x, yb + rise, zb)]], axis=lambda k: (x, yb - 3.0, ridge_z), closed=False)
    b.skin('Roof', [[(lx, yb, zf_roof), (rx, yb, zf_roof)], [(x, yb + rise, za), (x, yb + rise, za)]], axis=lambda k: (x, yb - 3.0, ridge_z), closed=False)
    b.skin('Roof', [[(lx, yb, zb_roof), (rx, yb, zb_roof)], [(x, yb + rise, zb), (x, yb + rise, zb)]], axis=lambda k: (x, yb - 3.0, ridge_z), closed=False)
    b.limb('RoofDark', (x, yb + rise + 0.03, za), (x, yb + rise + 0.03, zb), 0.07, 0.07, 4)
    for q in range(1, 4):
        t = q / 4
        for zz, zr in ((zf_roof, za), (zb_roof, zb)):
            b.box('RoofDark', x, yb + rise * t + 0.03, zz + (zr - zz) * t, (w + 2 * ox) * (1 - t * 0.8), 0.04, 0.05)
    b.box('Trim', x, yb + 0.05, zf + 0.35 * o, w + 0.6, 0.14, 0.14)
    return ridge_z


def roof_mansard(b, x, w, yb, zf, o, depth, rise, ox, col, seed):
    zf_roof = zf + 0.5 * o
    zb_roof = zf - o * (depth + 0.5)
    ridge_z = (zf_roof + zb_roof) / 2
    ins = 1.3
    zf2, zb2 = zf_roof - o * ins, zb_roof + o * ins
    h1 = rise * 0.62
    for zz, z2 in ((zf_roof, zf2), (zb_roof, zb2)):
        b.skin('RoofDark', [[(x - w / 2 - ox, yb, zz), (x + w / 2 + ox, yb, zz)], [(x - w / 2 - ox, yb + h1, z2), (x + w / 2 + ox, yb + h1, z2)]],
               axis=lambda k: (x, yb - 3.0, ridge_z), closed=False)
        b.skin('Roof', [[(x - w / 2 - ox, yb + h1, z2), (x + w / 2 + ox, yb + h1, z2)], [(x - w / 2 - ox, yb + rise, ridge_z), (x + w / 2 + ox, yb + rise, ridge_z)]],
               axis=lambda k: (x, yb - 3.0, ridge_z), closed=False)
    for sx in (-1, 1):
        xg = x + sx * (w / 2 + 0.02)
        b.skin(col, [[(xg, yb, zf), (xg, yb, zf - o * depth)], [(xg, yb + h1, zf2 - o * 0.3), (xg, yb + h1, zb2 + o * 0.3)]], axis=lambda k: (x, yb - 2.0, ridge_z), closed=False)
        b.skin(col, [[(xg, yb + h1, zf2 - o * 0.3), (xg, yb + h1, zb2 + o * 0.3)], [(xg, yb + rise * 0.9, ridge_z), (xg, yb + rise * 0.9, ridge_z)]], axis=lambda k: (x, yb - 2.0, ridge_z), closed=False)
    b.box('RoofDark', x, yb + rise + 0.04, ridge_z, w + 0.7, 0.12, 0.3)
    for k in range(max(1, int(w // 2.4))):                                                      # round dormers in the steep part
        dx_ = x - w / 2 + (k + 0.5) * w / max(1, int(w // 2.4))
        zz = zf_roof + (zf2 - zf_roof) * 0.5
        b.box(col, dx_, yb + h1 * 0.45, zz, 1.1, 0.9, 0.5)
        disc(b, col, dx_, yb + h1 * 0.45 + 0.45, zz + 0.25 * o, 0.55, o, 0.0)
        b.box('Glass', dx_, yb + h1 * 0.45, zz + 0.27 * o, 0.7, 0.7, 0.03)
        b.box('Trim', dx_, yb + h1 * 0.45 + 0.4, zz + 0.25 * o, 0.9, 0.08, 0.1)
    b.box('Trim', x, yb + 0.05, zf + 0.35 * o, w + 0.6, 0.14, 0.14)
    return ridge_z


def roof_terrace(b, x, w, yb, zf, o, depth, col, seed):
    """A flat roof used as a terrace: a parapet, a plank floor, a pergola with a vine, chairs, pots, a washing line, a water tank."""
    zc = zf - o * depth / 2
    b.box('Trim', x, yb + 0.1, zc, w + 0.5, 0.2, depth + 0.5, bevel=0.03)
    b.box('Pallet', x, yb + 0.23, zc, w - 0.4, 0.06, depth - 0.4) if 'Pallet' in COLOURS else b.box('Wood', x, yb + 0.23, zc, w - 0.4, 0.06, depth - 0.4)
    for sx in (-1, 1):
        b.box(col, x + sx * (w / 2 + 0.2), yb + 0.55, zc, 0.2, 0.9, depth + 0.5)
    b.box(col, x, yb + 0.55, zf + 0.2 * o, w + 0.5, 0.9, 0.2)
    b.box(col, x, yb + 0.55, zf - o * (depth + 0.2), w + 0.5, 0.9, 0.2)
    b.box('Trim', x, yb + 1.03, zf + 0.2 * o, w + 0.6, 0.08, 0.3)
    pz = zf - o * 2.4
    for sx in (-1, 1):
        for sz in (-1, 1):
            b.limb('Wood', (x + sx * (w / 2 - 0.5), yb + 0.2, pz + sz * 1.6), (x + sx * (w / 2 - 0.5), yb + 2.5, pz + sz * 1.6), 0.07, 0.07, 5)
    for q in range(7):
        b.box('Wood', x, yb + 2.5, pz - 1.6 + q * 0.55, w - 0.6, 0.06, 0.1)
    for q in range(4):
        b.box('Wood', x - w / 2 + 0.7 + q * (w - 1.4) / 3, yb + 2.56, pz, 0.08, 0.06, 3.4)
    for k in range(5):
        b.ico('Leaf' if k % 2 else 'LeafDark', x - w / 2 + 0.7 + k * (w - 1.4) / 4, yb + 2.7, pz + (k % 2 - 0.5) * 1.8, 0.28, 0.5, sub=0, jit=0.1, seed=seed + k)
    b.cone('Wood', x - 0.5, yb + 0.26, pz + 0.3, 0.45, 0.45, 0.05, 8)
    for sg in (-1, 1):
        b.box('Wood', x - 0.5 + sg * 0.7, yb + 0.5, pz + 0.3, 0.4, 0.05, 0.4)
        b.box('Wood', x - 0.5 + sg * 0.9, yb + 0.8, pz + 0.3, 0.05, 0.6, 0.4)
    for k in range(3):
        pot(b, x + w / 2 - 0.9, yb + 0.26, zf - o * (0.8 + k * 0.7), 0.9, seed + k)
    b.cone('Tank', x + w / 2 - 1.1, yb + 0.26, zf - o * (depth - 1.3), 0.6, 0.6, 1.1, 8)
    b.box('Iron', x + w / 2 - 1.1, yb + 1.4, zf - o * (depth - 1.3), 0.2, 0.05, 0.2)
    rope(b, (x - w / 2 + 0.3, yb + 1.6, zf - o * (depth - 0.9)), (x + w / 2 - 1.6, yb + 1.6, zf - o * (depth - 0.9)), 0.12, 'Rope', 0.012, 5)
    for k in range(4):
        b.box(('Cloth', 'ClothB', 'Flower', 'Cloth')[k], x - w / 2 + 0.9 + k * 0.7, yb + 1.3, zf - o * (depth - 0.9), 0.4, 0.5, 0.02)
    return zc


def roof_extras(b, x, w, yb, zf, o, depth, ridge_z, seed, style):
    """Chimneys, an antenna, a weathervane."""
    cx_ = x + (w * 0.25 if seed % 2 else -w * 0.25)
    b.box('Brick', cx_, yb + 2.3, ridge_z - 1.0 * o, 0.7, 1.4, 0.7)
    b.box('StoneDark', cx_, yb + 3.05, ridge_z - 1.0 * o, 0.9, 0.12, 0.9)
    for q in range(3):
        b.box('StoneDark', cx_, yb + 1.9 + q * 0.5, ridge_z - 1.0 * o, 0.72, 0.04, 0.72)
    if seed % 3 == 1:
        b.cone('Brick', x - cx_ + x, yb + 2.2, ridge_z + 1.0 * o, 0.28, 0.24, 1.1, 6)
        b.cone('StoneDark', x - cx_ + x, yb + 3.3, ridge_z + 1.0 * o, 0.34, 0.1, 0.18, 6)
    if seed % 4 == 2:
        b.limb('Iron', (x, yb + 2.5, ridge_z), (x, yb + 4.2, ridge_z), 0.03, 0.025, 4)
        b.box('Iron', x, yb + 4.0, ridge_z, 0.9, 0.03, 0.03)
        b.box('Iron', x, yb + 3.8, ridge_z, 0.6, 0.03, 0.03)


def turret(b, x, y0, h, zc, r=0.95, col='Cream', seed=0):
    """A round corner turret with little windows and a conical roof."""
    b.cone(col, x, y0, zc, r, r, h, 12)
    b.cone('StoneLight', x, y0 + h - 0.2, zc, r + 0.12, r + 0.12, 0.2, 12)
    for q in range(int(h // 3.0)):
        for a in (-0.6, 0.0, 0.6):
            b.box('Glass', x + math.sin(a) * (r + 0.01), y0 + 1.5 + q * 3.0, zc + math.cos(a) * (r + 0.01) * 1.0, 0.4, 0.8, 0.05, rot_y=math.degrees(a)) if abs(a) < 0.1 else None
    b.cone('Roof', x, y0 + h, zc, r + 0.35, 0.05, 2.4, 12)
    b.ico('Gold', x, y0 + h + 2.45, zc, 0.12, 1.0, sub=0)


def house(b, x, w, h, ci, zf, o, seed, special=None, depth=9.0, style=None, back=False, lite=False):
    """One house, its front at z = zf looking towards o (+1 near bank, -1 far bank). special: x of a balcony door to leave free."""
    style = style if style is not None else (seed * 5 + int(abs(x) * 3)) % 6
    if special is not None:
        style = 2
    col = PLASTER[ci % len(PLASTER)]
    y0 = QUAY_TOP
    zc = zf - o * depth / 2
    b.box(col, x, y0 + h / 2, zc, w, h, depth)
    base = 1.2
    b.box('StoneDark', x, y0 + base / 2, zf + 0.03 * o, w + 0.1, base, 0.12, bevel=0.02)               # a stone plinth, rusticated on the grander houses
    if style in (2, 3):
        for q in range(int((w) / 1.2)):
            b.box('Stone', x - w / 2 + 0.6 + q * 1.2, y0 + 1.8, zf + 0.05 * o, 1.1, 0.5, 0.1) if q % 2 == 0 else None
    for sx in (-1, 1):                                                                            # quoins, in a different rhythm on each kind
        step = 1.3 if style != 3 else 1.8
        for q in range(int(h / step)):
            b.box('Trim', x + sx * (w / 2 - 0.12 + (0.04 if q % 2 else 0.0)), y0 + 0.5 + q * step + step / 2, zf + 0.04 * o, 0.28 + (0.1 if q % 2 else 0.0), step * 0.5, 0.1)
    floors = max(2, int((h - 1.0) // 3.0))
    fh = (h - 1.2) / floors
    for f in range(1, floors):
        b.box('Trim', x, y0 + 1.2 + f * fh - 0.1, zf + 0.06 * o, w + 0.1, 0.16 if style != 2 else 0.24, 0.14 if style != 2 else 0.2)
    cols = max(1, int((w - 1.2) // (2.3 if style != 3 else 2.0)))
    door_c = cols // 2 if cols > 1 else 0
    for f in range(floors):
        yc = y0 + 1.2 + f * fh + fh * 0.5 + (0.15 if f else 0.0)
        for c in range(cols):
            wx = x - w / 2 + (c + 0.5) * w / cols
            if f == 0 and c == door_c:
                continue
            if special is not None and abs(wx - special) < 1.5 and abs(yc - special_y(special)) < 2.2:
                continue
            if f == 0 and style == 1:
                continue                                                                          # an arcade or shop fills the ground floor
            kind = 'plain'
            if style == 1 and f >= 1:
                kind = 'round'
            elif style == 2 and f == 1:
                kind = 'noble'
            elif style == 5 and f == floors - 1:
                kind = 'small'
            elif style == 3 and f >= 1:
                kind = 'round' if (c + f) % 2 else 'plain'
            hh = 1.35 if f else 1.15
            if kind == 'noble':
                hh = 1.8
            if kind == 'round':
                hh = 1.1
            window(b, wx, yc, zf, o, 0.85, hh, 'Shutter' if (c + f + seed) % 3 else 'ShutterB', flowerbox=(not lite and f > 0 and kind != 'noble' and (c + f + seed) % 2 == 0), seed=seed + c + f * 3, kind='small' if (lite and kind == 'plain') else kind)
            if style in (1, 2) and f == 1 and c % 2 == 0 and special is None and not lite:
                balconcino(b, wx, yc - 0.55, zf, o, 1.9 if kind == 'noble' else 1.6, seed + c)
    # the ground floor: a door with a stone surround, a shop front, an arcade or a passage
    dx = x - w / 2 + (door_c + 0.5) * w / cols if cols > 1 else x
    if style == 1:
        if w >= 6.0:
            shopfront(b, dx - w * 0.28, min(2.6, w * 0.35), zf, o, seed)
        # an arcade: a row of slender columns carrying arches along the front, one metre deep
        for q in range(int(w / 1.5) + 1):
            px = x - w / 2 + q * w / int(w / 1.5)
            b.cone('StoneLight', px, y0, zf + 0.9 * o, 0.18, 0.15, 3.0, 8)
        for q in range(int(w / 1.5)):
            px = x - w / 2 + (q + 0.5) * w / int(w / 1.5)
            rr = w / int(w / 1.5) / 2
            disc(b, 'StoneLight', px, y0 + 3.0, zf + 0.9 * o, rr, o, 0.2)
            disc(b, 'Black', px, y0 + 3.0, zf + 1.1 * o, rr - 0.16, o, 0.04)
            b.box('Black', px, y0 + 1.5, zf + 1.1 * o, rr * 2 - 0.5, 3.0, 0.04)
        b.box('Stone', x, y0 + 3.55, zf + 0.9 * o, w + 0.2, 0.3, 0.3)
        b.box('StoneLight', x, y0 + 3.75, zf + 0.9 * o, w + 0.3, 0.1, 0.4)
    elif special is None and w >= 6.0 and seed % 4 == 3:
        archway(b, dx, zf, o, 2.0, 2.9, seed)
    if not (style == 1 or (special is None and w >= 6.0 and seed % 4 == 3)):
        b.box('StoneLight', dx, y0 + 1.35, zf + 0.06 * o, 1.5, 2.5, 0.12)
        if style in (1, 2):
            pass
        b.box('Door', dx, y0 + 1.2, zf + 0.13 * o, 1.0, 2.2, 0.08)
        for q in range(3):
            b.box('WoodDark', dx - 0.33 + q * 0.33, y0 + 1.2, zf + 0.18 * o, 0.04, 2.2, 0.02)
        b.box('Iron', dx + 0.35, y0 + 1.2, zf + 0.2 * o, 0.1, 0.05, 0.05)
        b.box('StoneLight', dx, y0 + 2.5, zf + 0.09 * o, 1.4, 0.16, 0.2)
        b.box('StoneLight', dx, y0 + 0.06, zf + 0.35 * o, 1.4, 0.12, 0.45)                       # the doorstep
        b.box('Iron', dx + 1.05, y0 + 2.4, zf + 0.25 * o, 0.1, 0.1, 0.4)
        b.box('Lit', dx + 1.05, y0 + 2.25, zf + 0.5 * o, 0.18, 0.24, 0.18)
        if seed % 2 == 0:
            pot(b, dx - 1.0, y0, zf + 0.45 * o, 1.0, seed)
    # a hanging shop sign or a banner on some
    if seed % 5 == 1 and special is None and not lite:
        b.box('Iron', x - w / 2 + 0.6, y0 + 3.4, zf + 0.5 * o, 0.04, 0.04, 0.9)
        b.box('Terracotta', x - w / 2 + 0.6, y0 + 3.1, zf + 0.9 * o, 0.04, 0.45, 0.45)
    # patches of bare brick where the plaster has fallen, a drainpipe at one edge, a couple of wall lamps
    for k in range(0 if lite else 2):
        px = x - w / 2 + 0.8 + (w - 1.6) * hash01(seed * 3.3 + k)
        py = y0 + 2.2 + (h - 3.5) * hash01(seed * 5.1 + k)
        b.box('Brick', px, py, zf + 0.025 * o, 0.7 + 0.5 * hash01(k + seed), 0.5 + 0.4 * hash01(k * 2 + seed), 0.02)
    b.limb('StoneDark', (x + w / 2 - 0.35, y0 + h, zf + 0.12 * o), (x + w / 2 - 0.35, y0 + 0.2, zf + 0.12 * o), 0.05, 0.05, 6)
    # the roof by style
    rise = 2.6
    yb = y0 + h - 0.02
    if style == 0 or style == 3:
        ridge_z = roof_gable(b, x, w, yb, zf, o, depth, rise, 0.25, col, seed)
    elif style == 1:
        ridge_z = roof_hip(b, x, w, yb, zf, o, depth, rise * 0.9, 0.3, col, seed)
    elif style == 2:
        ridge_z = roof_gable(b, x, w, yb, zf, o, depth, rise * 0.8, 0.45, col, seed)
        if w > 6.2:
            for k in range(2):
                dxm = x + (-1 + 2 * k) * w * 0.22
                zr = zf + 0.5 * o + (ridge_z - zf - 0.5 * o) * 0.3
                b.box(col, dxm, yb + 0.9, zr, 1.4, 1.2, 0.9)
                b.skin('Roof', [[(dxm - 0.85, yb + 1.5, zr + 0.5 * o), (dxm + 0.85, yb + 1.5, zr + 0.5 * o)], [(dxm - 0.85, yb + 2.1, zr - 0.2 * o), (dxm + 0.85, yb + 2.1, zr - 0.2 * o)]],
                       axis=lambda k: (dxm, yb, zr - 3 * o), closed=False)
                b.box('Glass', dxm, yb + 0.9, zr + 0.46 * o, 0.7, 0.7, 0.04)
    elif style == 4:
        ridge_z = roof_terrace(b, x, w, yb, zf, o, depth, col, seed)
    else:
        ridge_z = roof_mansard(b, x, w, yb, zf, o, depth, rise * 1.15, 0.3, col, seed)
    if style != 4:
        roof_extras(b, x, w, yb, zf, o, depth, ridge_z, seed, style)
    if style == 3 and special is None and w >= 5.0:                                                 # a corner turret with a cone, on the side nearest the middle of the block
        tx_ = x + (w / 2 - 0.1) * (1 if x < 0 else -1)
        turret(b, tx_, y0 + 0.6, h + 1.5, zf + 0.15 * o, 0.9, col, seed)
    # the back wall: the same care, windows in rows, shutters, pipes, washing, so it reads from either side
    if back:
        back_wall(b, x, w, h, col, zf - o * depth, -o, seed)


def back_wall(b, x, w, h, col, zb, ob, seed):
    """Windows, shutters and washing on the wall behind the house (outward direction ob)."""
    y0 = QUAY_TOP
    floors = max(2, int((h - 1.0) // 3.0))
    fh = (h - 1.2) / floors
    cols = max(1, int((w - 1.0) // 2.6))
    for f in range(floors):
        yc = y0 + 1.5 + f * fh + fh * 0.5
        for c in range(cols):
            wx = x - w / 2 + (c + 0.5) * w / cols + 0.2 * math.sin(seed + c + f)
            window(b, wx, yc, zb, ob, 0.8, 1.2, 'Shutter' if (c + f + seed) % 2 else 'ShutterB', flowerbox=(f > 0 and (c + seed) % 2), seed=seed + c + f, kind='small' if f == floors - 1 else 'plain')
    b.limb('StoneDark', (x - w / 2 + 0.35, y0 + h, zb + 0.12 * ob), (x - w / 2 + 0.35, y0 + 0.2, zb + 0.12 * ob), 0.05, 0.05, 6)
    if seed % 3 == 0 and w >= 5.0:                                                                    # washing between two windows
        yl = y0 + 1.2 + fh * 1.6
        rope(b, (x - w / 2 + 0.7, yl, zb + 0.3 * ob), (x + w / 2 - 0.7, yl - 0.1, zb + 0.3 * ob), 0.15, 'Rope', 0.012, 5)
        for k in range(5):
            b.box(('Cloth', 'ClothB', 'Flower', 'Cloth', 'FlowerYellow')[k], x - w / 2 + 1.1 + k * (w - 2.4) / 4, yl - 0.35, zb + 0.3 * ob, 0.4, 0.55, 0.02)
    b.box('Door', x, y0 + 1.0, zb + 0.06 * ob, 1.0, 2.0, 0.08)
    b.box('StoneLight', x, y0 + 1.1, zb + 0.03 * ob, 1.4, 2.3, 0.06)


def special_y(sx):
    return 10.0 if sx == -20 else 8.0


NEAR = [(-40.0, 8.0, 10.0, 0), (-33.0, 6.0, 12.0, 1), (-27.5, 5.0, 9.0, 8), (-20.0, 10.0, 13.0, 3), (-12.0, 6.0, 8.0, 6), (-6.0, 6.0, 10.0, 5),
        (0.0, 6.0, 9.0, 7), (6.0, 6.5, 12.0, 9), (12.5, 7.0, 8.0, 2), (26.0, 6.5, 9.0, 10)]
FAR = [(-40.0, 8.0, 9.0, 3), (-31.0, 7.0, 11.0, 6), (-23.0, 8.0, 8.0, 5), (-14.0, 9.0, 12.0, 1), (-5.0, 7.0, 9.0, 8), (4.0, 8.0, 11.0, 4), (13.0, 7.0, 8.0, 11),
       (33.0, 8.0, 10.0, 9), (41.0, 6.0, 9.0, 7)]
NEAR_STYLES = [0, 0, 3, 2, 5, 0, 4, 3, 5, 2]   # no arcades on the near bank: its promenade is only 1.2 m wide
FAR_STYLES = [1, 3, 4, 2, 0, 5, 1, 3, 4]


def side_windows(b, x_edge, sgn, y_low, y_high, zf, o, depth, seed):
    """Windows on the side wall of a house where it stands higher than its neighbour."""
    y = y_low + 1.2
    row = 0
    while y < y_high - 1.0:
        for q in range(int(depth // 3.0)):
            zz = zf - o * (1.6 + q * 3.0)
            xx = x_edge
            b.box('Trim', xx + sgn * 0.04, y, zz, 0.08, 1.5, 1.1)
            b.box('Glass', xx + sgn * 0.09, y, zz, 0.05, 1.2, 0.8)
            b.box('Shutter', xx + sgn * 0.1, y, zz - 0.62, 0.06, 1.3, 0.34)
            b.box('Shutter', xx + sgn * 0.1, y, zz + 0.62, 0.06, 1.3, 0.34)
        y += 3.0
        row += 1


def town_rows(b):
    for k, ((x, w, h, c), st) in enumerate(zip(NEAR, NEAR_STYLES)):
        house(b, x, w, h, c, -4.2, 1.0, k, special=-20 if x == -20 else None, style=st)
    for k, ((x, w, h, c), st) in enumerate(zip(FAR, FAR_STYLES)):
        house(b, x, w, h, c, 51.0, -1.0, k + 20, special=-14 if x == -14 else None, style=st, back=True)
    # side walls that rise above a lower neighbour get windows too
    for rowlist, zf, o, seed0 in ((NEAR, -4.2, 1.0, 0), (FAR, 51.0, -1.0, 20)):
        for k, (x, w, h, c) in enumerate(rowlist):
            for sgn in (-1, 1):
                nb = None
                for (x2, w2, h2, c2) in rowlist:
                    if x2 != x and abs((x2 - x) - sgn * (w / 2 + w2 / 2)) < 1.6:
                        nb = h2
                if nb is None:
                    nb = 0.0
                if h - nb > 3.6:
                    side_windows(b, x + sgn * (w / 2), sgn, QUAY_TOP + nb, QUAY_TOP + h, zf, o, 9.0, seed0 + k)
    # a second row of taller houses behind the near one, with fronts looking at the canal over the roofs: windows, balconies, roofs and all
    for k in range(13):
        x = -43 + 7.2 * k + 1.5 * hash01(k)
        w = 6.4
        h = 14 + 8 * hash01(k * 2.3 + 1)
        if abs(x - 36) < 8.5 and h < 16:
            h = 17.0
        house(b, x, w, h, (k * 2 + 1) % len(PLASTER), -20.4, 1.0, 40 + k, style=[0, 5, 2, 1, 3, 5, 0, 2, 3, 1, 0, 5, 2][k], depth=9.0, lite=True)
    # the far bank has no second row (the block ends at z = 60), but its houses are taller and different from each other


# ---------------------------------------------------------------- balconies (collision: slab 4 x 0.25 x 2.4, 3.6 for the towers)
def balcony(b, bx, y, zf, o, width=4.0, depth=2.4, gap=(-0.3, 0.3), door=True, name=None, seed=0):
    """A stone balcony on corbels with an iron balustrade, open in front at the diving place."""
    zc = zf + o * depth / 2
    b.box('StoneLight', bx, y - 0.125, zc, width, 0.25, depth, bevel=0.03)
    b.box('Stone', bx, y - 0.3, zc - 0.1 * o, width - 0.3, 0.1, depth - 0.3)
    for sx in (-1, 1):                                                                         # corbels
        for q in range(3):
            b.box('StoneDark', bx + sx * (width / 2 - 0.45), y - 0.55 - q * 0.28, zf + o * (0.25 + (2 - q) * 0.25), 0.3, 0.26, 0.5 - 0.1 * q, bevel=0.02)
    # the balustrade: posts with scrolls round the sides and across the front on either side of the gap
    fz = zf + o * (depth - 0.05)
    xs = [bx - width / 2 + 0.05 + q * 0.4 for q in range(int(width / 0.4) + 1)]
    for px in xs:
        if gap[0] < px - bx < gap[1]:
            continue
        b.limb('Iron', (px, y, fz), (px, y + 0.95, fz), 0.022, 0.022, 4)
    for hy in (0.95, 0.15):
        left_end = bx + gap[0]
        right_start = bx + gap[1]
        b.box('Iron', (bx - width / 2 + left_end) / 2, y + hy, fz, left_end - (bx - width / 2), 0.04, 0.04)
        b.box('Iron', (right_start + bx + width / 2) / 2, y + hy, fz, (bx + width / 2) - right_start, 0.04, 0.04)
    for sx in (-1, 1):
        px = bx + sx * (width / 2 - 0.05)
        for q in range(int(depth / 0.4) + 1):
            zz = zf + o * (q * 0.4)
            b.limb('Iron', (px, y, zz), (px, y + 0.95, zz), 0.022, 0.022, 4)
        b.box('Iron', px, y + 0.95, zf + o * depth / 2, 0.05, 0.05, depth)
        b.box('Iron', px, y + 0.15, zf + o * depth / 2, 0.04, 0.04, depth)
    for sx in (-1, 1):                                                                         # scroll rosettes on the front
        b.cone('Iron', bx + sx * 1.1, y + 0.45, fz + 0.02 * o, 0.18, 0.18, 0.04, 8)
    if door:
        b.box('StoneLight', bx, y + 1.15, zf + 0.06 * o, 1.5, 2.4, 0.12)
        b.box('Door', bx, y + 1.05, zf + 0.12 * o, 1.1, 2.1, 0.08)
        for q in range(2):
            b.box('Glass', bx - 0.27 + q * 0.54, y + 1.45, zf + 0.17 * o, 0.38, 0.8, 0.03)
    planter(b, bx + 1.5, y, zf + o * (depth - 0.3), 0.95, seed)
    pot(b, bx - 1.6, y, zf + o * 0.6, 0.9, seed + 3)
    if name:
        b.spot(name, bx, y, zf + o * depth)


# ---------------------------------------------------------------- the footbridge (collision: piers, arch, deck 14 x 0.4 x 3.2 at y 4.8, steps up both sides)
def footbridge(b):
    z0, z1 = -0.1, 3.1
    zc = (z0 + z1) / 2
    for px in (-10.5, 10.5):
        b.box('Stone', px, (QUAY_TOP - 6.0) / 2, 0.0, 5.0, QUAY_TOP + 6.0, 7.0)
        b.box('StoneDark', px, QUAY_TOP - 0.04, 0.0, 5.1, 0.12, 7.1)
    # the arch under the deck: a solid between the deck and a segmental soffit (x -8..8, springing at -1.3, crown at 4.0), built as one skin
    stations = [-8.0 + 16.0 * i / 28 for i in range(29)]
    rings = []
    for x in stations:
        soffit = -1.3 + 5.3 * math.sqrt(max(0.0, 1.0 - (x / 8.0) ** 2))
        rings.append([(x, 4.62, z0), (x, 4.62, z1), (x, soffit, z1), (x, soffit, z0)])
    b.skin('Stone', rings, axis=lambda k: (stations[k], 1.5, zc), closed=True, cap_a=True, cap_b=True)
    for i in range(0, 28, 2):                                                                     # voussoir joints on both faces
        x = stations[i]
        soffit = -1.3 + 5.3 * math.sqrt(max(0.0, 1.0 - (x / 8.0) ** 2))
        for zz in (z0 - 0.02, z1 + 0.02):
            b.limb('StoneDark', (x, soffit, zz), (x, min(soffit + 0.9, 4.6), zz), 0.03, 0.03, 3)
    for x in stations[::2]:
        soffit = -1.3 + 5.3 * math.sqrt(max(0.0, 1.0 - (x / 8.0) ** 2))
        b.box('StoneLight', x, soffit + 0.05, z1 + 0.04, 0.6, 0.14, 0.14)
    b.box('StoneLight', 0, 4.8, zc, 14.0, 0.4, 3.2, bevel=0.03)
    b.box('Stone', 0, 4.58, zc, 14.4, 0.14, 3.3)                                                # the string course under the deck
    b.box('StoneDark', 0, 3.3, z1 + 0.04, 0.9, 1.1, 0.16, bevel=0.03)
    # steps up the ramps from the quay to the deck, with solid cheeks
    for s in (-1, 1):
        for k in range(9):
            x_a = 12.6 - k * 0.6
            top = QUAY_TOP + (5.0 - QUAY_TOP) * (k + 1) / 9
            b.box('StoneLight', s * x_a, top - 0.06, zc, 0.64, 0.12, 3.2, bevel=0.02)
            b.box('Stone', s * x_a, (QUAY_TOP + top) / 2 - 0.1, zc, 0.64, top - QUAY_TOP, 3.0)
    # balustrades: back wall the full length, the front in two parts with an opening at the middle
    for xs0, xs1 in ((-7.0, 7.0),):
        b.box('Stone', 0, 5.3, z0 + 0.15, 14.0, 0.6, 0.3, bevel=0.03)
        b.box('StoneLight', 0, 5.63, z0 + 0.15, 14.2, 0.1, 0.4)
    for xa, xb in ((-7.0, -2.0), (2.0, 7.0)):
        xm = (xa + xb) / 2
        b.box('StoneLight', xm, 5.63, z1 - 0.15, xb - xa + 0.2, 0.1, 0.4)
        n_ = int((xb - xa) / 0.5)
        for q in range(n_ + 1):
            px = xa + q * (xb - xa) / n_
            b.cone('Stone', px, 5.0, z1 - 0.15, 0.1, 0.06, 0.6, 8)
        b.box('Stone', xm, 5.2, z1 - 0.15, xb - xa, 0.1, 0.3)
    for s in (-1, 1):                                                                            # the cheeks of the stairs carry a sloping parapet
        for k in range(9):
            x_a = 12.6 - k * 0.6
            top = QUAY_TOP + (5.0 - QUAY_TOP) * (k + 1) / 9
            b.box('Stone', s * x_a, top + 0.35, z0 + 0.15, 0.64, 0.7, 0.3)
            b.box('Stone', s * x_a, top + 0.35, z1 - 0.15, 0.64, 0.7, 0.3)
    for px in (-6.9, -2.1, 2.1, 6.9):                                                            # urn finials
        b.cone('StoneLight', px, 5.7, z1 - 0.15, 0.2, 0.12, 0.35, 8)
        b.ico('Leaf', px, 6.2, z1 - 0.15, 0.28, 0.9, sub=1, jit=0.12, seed=int(px * 5))
    b.ico('Leaf', 0.0, 6.0, z0 + 0.15, 0.3, 0.9, sub=1, jit=0.1, seed=7)
    for sx in (-6.0, 6.0):                                                                       # lamps on the deck, over the back wall
        b.limb('Iron', (sx, 5.7, z0 + 0.15), (sx, 7.6, z0 + 0.15), 0.05, 0.04, 5)
        b.box('Lit', sx, 7.8, z0 + 0.15, 0.24, 0.36, 0.24)
        b.cone('Iron', sx, 8.0, z0 + 0.15, 0.22, 0.02, 0.2, 4)
    # the two piers are landings with something on them: a fountain and a bench on the west one, a gelato cart under a tree on the east one
    bench_z = -1.9
    b.cone('StoneLight', -11.0, QUAY_TOP, bench_z, 1.1, 1.0, 0.5, 10)
    b.cone('Stone', -11.0, QUAY_TOP + 0.5, bench_z, 0.9, 0.85, 0.3, 10)
    b.cone('StoneLight', -11.0, QUAY_TOP + 0.8, bench_z, 0.12, 0.2, 1.0, 8)
    b.cone('Stone', -11.0, QUAY_TOP + 1.8, bench_z, 0.5, 0.3, 0.14, 10)
    b.cone('CanalDark', -11.0, QUAY_TOP + 0.78, bench_z, 0.8, 0.8, 0.04, 10)
    for sg in (-1, 1):
        b.box('Wood', -11.0 + sg * 1.7, QUAY_TOP + 0.45, bench_z, 0.45, 0.06, 1.4, bevel=0.02)
        b.box('Wood', -11.0 + sg * 1.9, QUAY_TOP + 0.85, bench_z, 0.06, 0.4, 1.4, bevel=0.02)
        for dz in (-0.5, 0.5):
            b.box('Iron', -11.0 + sg * 1.7, QUAY_TOP + 0.22, bench_z + dz, 0.4, 0.45, 0.06)
    lamp(b, -12.4, -2.6, 1.0)
    tree(b, 11.8, -2.0, 1.0, 5)
    b.box('Cloth', 9.3, QUAY_TOP + 0.6, -1.6, 1.3, 0.9, 0.8, bevel=0.03)
    b.box('Terracotta', 9.3, QUAY_TOP + 1.2, -1.6, 1.4, 0.1, 0.9)
    for k in range(6):
        b.skin('AwnRed' if k % 2 else 'AwnWhite', [[(8.5 + k * 0.28, QUAY_TOP + 2.4, -2.1), (8.78 + k * 0.28, QUAY_TOP + 2.4, -2.1)], [(8.5 + k * 0.28, QUAY_TOP + 2.0, -1.1), (8.78 + k * 0.28, QUAY_TOP + 2.0, -1.1)]],
               axis=lambda kk: (9.3, QUAY_TOP, -1.6), closed=False)
    for dx_ in (-0.6, 0.6):
        b.limb('Iron', (9.3 + dx_, QUAY_TOP, -2.0), (9.3 + dx_, QUAY_TOP + 2.4, -2.0), 0.03, 0.03, 4)
    b.cone('Cone', 9.0, QUAY_TOP + 1.3, -1.6, 0.14, 0.02, 0.35, 8) if 'Cone' in COLOURS else b.ico('Flower', 9.0, QUAY_TOP + 1.45, -1.6, 0.14, 1.0, sub=0)
    b.ico('FlowerYellow', 9.4, QUAY_TOP + 1.45, -1.6, 0.13, 1.0, sub=0)
    b.ico('FlowerPink', 9.8, QUAY_TOP + 1.45, -1.6, 0.13, 1.0, sub=0)
    b.limb('Iron', (7.2, QUAY_TOP, -2.8), (7.2, QUAY_TOP, -2.8), 0.01, 0.01, 3)
    b.spot('Spot_Bridge', 0.0, 5.0, 3.1)


# ---------------------------------------------------------------- the bell tower (collision: 5 x 22 x 5 at x = 20, balcony 3.6 x 2.4)
def bell_tower(b):
    tx = 20.0
    zf = -4.2
    zc = zf - 2.5
    y0 = QUAY_TOP
    b.box('Cream', tx, y0 + 11.0, zc, 5.0, 22.0, 5.0)
    b.box('StoneDark', tx, y0 + 0.8, zf + 0.04, 5.2, 1.6, 0.16, bevel=0.03)
    for y in (4.0, 8.4, 13.0, 17.4):
        b.box('Trim', tx, y0 + y, zf + 0.06, 5.3, 0.22, 0.16)
        b.box('Trim', tx + 2.5, y0 + y, zc, 0.16, 0.22, 5.3)
        b.box('Trim', tx - 2.5, y0 + y, zc, 0.16, 0.22, 5.3)
    for sx in (-1, 1):
        for q in range(int(22 / 1.1)):
            b.box('StoneLight' if q % 2 else 'Trim', tx + sx * 2.45, y0 + 0.5 + q * 1.1, zf + 0.05, 0.3 + 0.08 * (q % 2), 0.5, 0.12)
    for y in (3.0, 6.0, 10.0, 14.6):                                                              # narrow arched windows on every face
        for face in range(3):
            if face == 0:
                b.box('Trim', tx, y0 + y, zf + 0.05, 0.9, 1.7, 0.08)
                b.box('Glass', tx, y0 + y, zf + 0.09, 0.62, 1.4, 0.05)
                b.cone('StoneLight', tx, y0 + y + 0.7, zf + 0.06, 0.45, 0.01, 0.2, 8)
            else:
                xx = tx + (2.55 if face == 1 else -2.55)
                b.box('Trim', xx, y0 + y, zc, 0.08, 1.7, 0.9)
                b.box('Glass', xx + (0.02 if face == 1 else -0.02), y0 + y, zc, 0.05, 1.4, 0.62)
    b.box('StoneLight', tx, y0 + 1.1, zf + 0.08, 1.8, 2.2, 0.14)                                   # the door
    b.box('Door', tx, y0 + 1.0, zf + 0.15, 1.3, 2.0, 0.1)
    for q in range(3):
        b.box('WoodDark', tx - 0.4 + q * 0.4, y0 + 1.0, zf + 0.21, 0.04, 2.0, 0.02)
    # the belfry: four pairs of arched openings with slender columns, a bell inside, louvres, a cornice
    by = y0 + 17.6
    b.box('Stone', tx, by + 0.3, zc, 5.5, 0.6, 5.5)
    b.box('Cream', tx, by + 2.2, zc, 4.6, 3.3, 4.6)
    for face in range(4):
        for q in (-1, 1):
            for (nx, nz, ax, az) in ((0, -1, 1, 0), (0, 1, 1, 0), (-1, 0, 0, 1), (1, 0, 0, 1)):
                pass
    for (nz, nx) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        for q in (-1, 1):
            ox_ = tx + (nx * 2.33) + (q * 0.75 if nz else 0.0)
            oz_ = zc + (nz * 2.33) + (q * 0.75 if nx else 0.0)
            if nz:
                b.box('Glass', ox_, by + 2.0, oz_, 0.9, 2.1, 0.12)
                b.box('StoneLight', ox_, by + 1.0 + 1.35 + 0.1, oz_ + 0.02 * nz, 1.1, 0.18, 0.2)
                b.cone('StoneLight', ox_, by + 3.05, oz_, 0.55, 0.01, 0.34, 10)
                for lv in range(5):
                    b.box('WoodDark', ox_, by + 1.2 + lv * 0.36, oz_ + 0.02 * nz, 0.8, 0.05, 0.08)
            else:
                b.box('Glass', ox_, by + 2.0, oz_, 0.12, 2.1, 0.9)
                b.box('StoneLight', ox_, by + 2.45, oz_, 0.2, 0.18, 1.1)
                b.cone('StoneLight', ox_, by + 3.05, oz_, 0.55, 0.01, 0.34, 10)
                for lv in range(5):
                    b.box('WoodDark', ox_, by + 1.2 + lv * 0.36, oz_, 0.08, 0.05, 0.8)
        b.box('StoneLight', tx + nx * 2.34, by + 2.2, zc + nz * 2.34, 0.26 if nz else 0.14, 3.3, 0.14 if nz else 0.26)
    for sx in (-1, 1):
        for sz in (-1, 1):
            b.box('StoneLight', tx + sx * 2.3, by + 2.2, zc + sz * 2.3, 0.4, 3.3, 0.4, bevel=0.03)
    b.cone('Bell', tx, by + 1.4, zc, 0.45, 0.62, 0.9, 10)
    b.ico('Iron', tx, by + 1.35, zc, 0.12, 1.0, sub=0)
    b.box('WoodDark', tx, by + 2.1, zc, 1.3, 0.14, 0.14)
    b.box('StoneLight', tx, by + 4.0, zc, 5.6, 0.4, 5.6, bevel=0.03)                              # cornice
    b.box('Stone', tx, by + 4.35, zc, 5.8, 0.3, 5.8, bevel=0.03)
    for sx in (-1, 1):
        for sz in (-1, 1):
            b.cone('StoneLight', tx + sx * 2.7, by + 4.5, zc + sz * 2.7, 0.2, 0.1, 0.5, 6)
    # the pointed red roof: four slopes with tile bands, a ball, a cross
    sy = by + 4.5
    pts = [(tx + 3.9 * math.cos(a), sy, zc + 3.9 * math.sin(a)) for a in (math.pi / 4, 3 * math.pi / 4, 5 * math.pi / 4, 7 * math.pi / 4)]
    apex = (tx, sy + 5.0, zc)
    for i in range(4):
        p, q = pts[i], pts[(i + 1) % 4]
        b.skin('Roof', [[p, q], [apex, apex]], axis=lambda k: (tx, sy, zc), closed=False)
        for lv in range(1, 6):
            t = lv / 6
            a_, c_ = tuple(p[j] + (apex[j] - p[j]) * t for j in range(3)), tuple(q[j] + (apex[j] - q[j]) * t for j in range(3))
            b.limb('RoofDark', (a_[0], a_[1] + 0.03, a_[2]), (c_[0], c_[1] + 0.03, c_[2]), 0.03, 0.03, 3)
    b.ico('Gold', tx, sy + 5.1, zc, 0.25, 1.0, sub=1)
    b.limb('Gold', (tx, sy + 5.3, zc), (tx, sy + 6.7, zc), 0.05, 0.04, 4)
    b.box('Gold', tx, sy + 6.2, zc, 0.6, 0.06, 0.06)
    b.box('Gold', tx + 0.35, sy + 5.8, zc, 0.5, 0.2, 0.02)
    balcony(b, tx, 15.0, zf, 1.0, 3.6, 2.4, gap=(-0.3, 0.3), door=False, name='Spot_Tower', seed=11)
    b.box('Door', tx, 16.15, zf + 0.12, 1.1, 2.1, 0.08)                                           # the door onto the belfry balcony
    b.box('StoneLight', tx, 16.2, zf + 0.06, 1.5, 2.4, 0.12)
    b.box('Door', tx, 16.1, zf + 0.12, 1.1, 2.1, 0.08)


# ---------------------------------------------------------------- the cathedral (collision: 14 x 14 x 16 body, drum 9.8, ring 11.8, porch slab 3.4 x 2.6)
def cathedral(b):
    dx, dz = 36.0, -11.2
    zf = -4.2
    y0 = QUAY_TOP
    b.box('Cream', dx, y0 + 7.0, -12.2, 14.0, 14.0, 16.0)
    b.box('StoneDark', dx, y0 + 0.6, zf + 0.04, 14.2, 1.2, 0.16)
    # the pediment on columns with a gable, an arched doorway, rose window, niches with statues, a stepped base
    for cx in (-5.0, -2.5, 0.0, 2.5, 5.0):
        b.cone('StoneLight', dx + cx, y0, zf + 0.5, 0.42, 0.34, 0.5, 8)
        b.cone('Stone', dx + cx, y0 + 0.5, zf + 0.5, 0.34, 0.3, 9.0, 8)
        b.box('StoneLight', dx + cx, y0 + 9.65, zf + 0.5, 0.9, 0.26, 0.9, bevel=0.03)
    b.box('StoneLight', dx, y0 + 10.2, zf + 0.5, 12.6, 0.8, 1.1, bevel=0.03)                     # the entablature
    b.box('Trim', dx, y0 + 10.7, zf + 0.5, 12.9, 0.2, 1.2)
    for sgn in (-1, 1):                                                                           # the gable roof over the portico: two slopes and the triangle
        b.skin('Roof', [[(dx + sgn * 6.7, y0 + 10.7, zf + 1.4), (dx + sgn * 6.7, y0 + 10.7, zf - 2.6)], [(dx, y0 + 13.0, zf + 1.4), (dx, y0 + 13.0, zf - 2.6)]],
               axis=lambda k: (dx, y0 + 6, zf - 0.6), closed=False)
    b.tri('StoneLight', (dx - 6.5, y0 + 10.9, zf + 1.0), (dx + 6.5, y0 + 10.9, zf + 1.0), (dx, y0 + 12.9, zf + 1.0))
    b.limb('StoneDark', (dx, y0 + 11.9, zf + 0.98), (dx, y0 + 11.9, zf + 1.04), 0.6, 0.6, 12)
    b.box('Door', dx, y0 + 2.2, zf + 0.1, 2.4, 4.0, 0.14)
    b.box('StoneLight', dx, y0 + 2.3, zf + 0.05, 3.2, 4.6, 0.12)
    for q in range(5):
        b.box('WoodDark', dx - 0.9 + q * 0.45, y0 + 2.0, zf + 0.2, 0.05, 3.6, 0.03)
    b.limb('StoneLight', (dx, y0 + 6.2, zf + 0.06), (dx, y0 + 6.2, zf + 0.12), 1.5, 1.5, 14)                  # the rose window
    b.limb('Glass', (dx, y0 + 6.2, zf + 0.1), (dx, y0 + 6.2, zf + 0.16), 1.2, 1.2, 14)
    for q in range(6):
        a_ = q * math.pi / 6
        b.limb('StoneLight', (dx + math.cos(a_) * 1.15, y0 + 6.2 + math.sin(a_) * 1.15, zf + 0.19), (dx - math.cos(a_) * 1.15, y0 + 6.2 - math.sin(a_) * 1.15, zf + 0.19), 0.03, 0.03, 4)
    for sx in (-3.9, 3.9):
        b.box('StoneLight', dx + sx, y0 + 4.0, zf + 0.06, 1.3, 2.4, 0.1)                           # niches with statues
        b.box('Glass', dx + sx, y0 + 4.0, zf + 0.1, 1.0, 2.1, 0.05)
        b.cone('Cream', dx + sx, y0 + 3.0, zf + 0.2, 0.2, 0.1, 1.3, 6)
        b.ico('Cream', dx + sx, y0 + 4.5, zf + 0.2, 0.15, 1.0, sub=0)
    # the nave roof: a hipped roof of terracotta tiles up to the drum, side towers corners
    ridge = y0 + 17.0
    for sgn in (-1, 1):
        b.skin('Roof', [[(dx + sgn * 7.4, y0 + 13.9, -4.4), (dx + sgn * 7.4, y0 + 13.9, -20.4)], [(dx + sgn * 4.0, ridge, -6.5), (dx + sgn * 4.0, ridge, -18.3)]],
               axis=lambda k: (dx, y0 + 8, -12.4), closed=False)
    for zz, sgn in ((-4.4, 1), (-20.4, -1)):
        b.skin('Roof', [[(dx - 7.4, y0 + 13.9, zz), (dx + 7.4, y0 + 13.9, zz)], [(dx - 4.0, ridge, -6.5 if sgn > 0 else -18.3), (dx + 4.0, ridge, -6.5 if sgn > 0 else -18.3)]],
               axis=lambda k: (dx, y0 + 8, -12.4), closed=False)
    for q in range(1, 5):
        t = q / 5
        for sgn in (-1, 1):
            b.limb('RoofDark', (dx + sgn * (7.4 - 3.4 * t), y0 + 13.9 + 3.1 * t + 0.03, -4.4 - 2.1 * t), (dx + sgn * (7.4 - 3.4 * t), y0 + 13.9 + 3.1 * t + 0.03, -20.4 + 2.1 * t), 0.035, 0.035, 3)
    b.box('Trim', dx, y0 + 14.0, zf + 0.3, 14.8, 0.3, 0.8)
    # the drum: 14 sides, pilasters, seven arched windows, a cornice; the ring; a green-blue ribbed dome, a lantern, a golden ball
    b.cone('Cream', dx, 14.0, dz, 4.9, 4.9, 8.0, 14)
    for i in range(14):
        a = i / 14 * 2 * math.pi
        b.box('Trim', dx + math.sin(a) * 4.98, 17.7, dz + math.cos(a) * 4.98, 0.4, 7.4, 0.4, rot_y=math.degrees(a))
    for i in range(7):
        a = i / 7 * 2 * math.pi
        sx_, sz_ = math.sin(a) * 4.98, math.cos(a) * 4.98
        b.box('Trim', dx + sx_, 18.5, dz + sz_, 1.1, 2.4, 0.12, rot_y=math.degrees(a))
        b.box('Glass', dx + sx_ * 1.003, 18.5, dz + sz_ * 1.003, 0.8, 2.0, 0.1, rot_y=math.degrees(a))
        b.cone('StoneLight', dx + sx_, 19.6, dz + sz_, 0.45, 0.02, 0.3, 8)
    b.cone('Stone', dx, 21.6, dz, 5.9, 5.9, 0.4, 14)
    b.cone('StoneLight', dx, 21.4, dz, 5.3, 5.6, 0.3, 14)
    rings = []
    nlat = 9
    for k in range(nlat + 1):
        ph = math.pi / 2 * k / nlat
        r = 4.9 * math.cos(ph) + (0.0 if k < nlat else 0.0)
        y = 22.0 + 4.9 * math.sin(ph)
        rings.append([(dx + math.sin(2 * math.pi * i / 14) * max(r, 0.05), y, dz + math.cos(2 * math.pi * i / 14) * max(r, 0.05)) for i in range(14)])
    b.skin('Dome', rings, axis=lambda k: (dx, 22.0, dz), closed=True)
    for i in range(14):
        a = 2 * math.pi * i / 14
        prev = None
        for k in range(nlat + 1):
            ph = math.pi / 2 * k / nlat
            p = (dx + math.sin(a) * 4.97 * math.cos(ph) * 1.0, 22.0 + 4.97 * math.sin(ph), dz + math.cos(a) * 4.97 * math.cos(ph))
            if prev is not None:
                b.limb('DomeRib', prev, p, 0.07, 0.07, 3)
            prev = p
    b.cone('Stone', dx, 26.8, dz, 0.9, 0.7, 0.35, 10)
    b.cone('StoneLight', dx, 27.1, dz, 0.75, 0.7, 2.2, 8)
    for i in range(8):
        a = i * 0.785
        b.box('Glass', dx + math.cos(a) * 0.76, 28.2, dz + math.sin(a) * 0.76, 0.3, 1.1, 0.1, rot_y=-math.degrees(a) + 90)
    b.cone('Stone', dx, 29.3, dz, 0.9, 0.5, 0.3, 8)
    b.ico('Gold', dx, 29.9, dz, 0.6, 1.0, sub=1)
    b.limb('Gold', (dx, 30.4, dz), (dx, 31.6, dz), 0.04, 0.03, 4)
    b.box('Gold', dx, 31.2, dz, 0.5, 0.05, 0.05)
    # the belvedere: a stone podium standing on the portico roof against the drum carries the porch slab (3.4 x 2.6 at 21.9) on corbels
    pz0, pz1 = -4.4, -1.8
    b.box('Stone', dx, 17.25, -4.5, 4.6, 8.7, 4.0)
    b.box('StoneDark', dx, 12.95, -4.5, 4.9, 0.4, 4.3)
    for sx in (-1, 1):
        b.box('StoneLight', dx + sx * 2.15, 17.25, -2.46, 0.3, 8.6, 0.12)
    b.box('StoneLight', dx, 21.3, -2.46, 4.8, 0.3, 0.2)
    b.box('StoneLight', dx, 15.8, -2.46, 4.8, 0.25, 0.2)
    b.box('Glass', dx, 18.6, -2.46, 1.5, 2.8, 0.05)                                           # a blind window on the podium front
    b.box('StoneLight', dx, 18.6, -2.5, 1.9, 3.2, 0.06)
    b.box('StoneLight', dx, 20.2, -2.43, 1.9, 0.2, 0.2)
    b.box('StoneLight', dx, 21.75, (-6.4 + pz1) / 2, 3.4, 0.25, (pz1 + 6.4), bevel=0.03)
    for sx in (-1, 1):
        for q in range(4):
            b.box('StoneDark', dx + sx * 1.35, 21.4 - q * 0.42, -2.15 + 0.05 * q, 0.34, 0.4, 0.7 - 0.12 * q, bevel=0.02)
    for sx in (-1, 1):                                                                           # the balustrade: sides and front on both sides of the opening
        b.box('Iron', dx + sx * 1.6, 22.85, (-5.2 + pz1) / 2, 0.04, 0.04, 3.4)
        b.box('Iron', dx + sx * 1.6, 22.15, (-5.2 + pz1) / 2, 0.04, 0.04, 3.4)
        for q in range(9):
            b.limb('Iron', (dx + sx * 1.6, 21.9, pz1 - 0.1 - q * 0.42), (dx + sx * 1.6, 22.85, pz1 - 0.1 - q * 0.42), 0.02, 0.02, 4)
    for q in range(8):
        px = dx - 1.6 + q * 0.46
        if abs(px - dx) < 0.45:
            continue
        b.limb('Iron', (px, 21.9, pz1 - 0.05), (px, 22.85, pz1 - 0.05), 0.02, 0.02, 4)
    b.box('Iron', dx - 1.15, 22.85, pz1 - 0.05, 1.0, 0.04, 0.04)
    b.box('Iron', dx + 1.15, 22.85, pz1 - 0.05, 1.0, 0.04, 0.04)
    # the door, flat against the front of a little stone dormer that stands on the porch and leans on the dome
    b.box('Stone', dx, 23.0, -5.85, 2.2, 2.2, 1.3)
    b.box('StoneLight', dx, 23.0, -5.18, 1.7, 2.3, 0.1)
    b.box('Door', dx, 22.95, -5.1, 1.2, 2.0, 0.08)
    for q in range(3):
        b.box('WoodDark', dx - 0.4 + q * 0.4, 22.95, -5.05, 0.04, 2.0, 0.02)
    b.box('StoneLight', dx, 24.2, -5.2, 2.0, 0.16, 0.2)
    b.tri('StoneLight', (dx - 1.2, 24.3, -5.15), (dx + 1.2, 24.3, -5.15), (dx, 25.2, -5.15))
    b.skin('Roof', [[(dx - 1.25, 24.1, -5.1), (dx + 1.25, 24.1, -5.1)], [(dx - 1.25, 24.1, -6.6), (dx + 1.25, 24.1, -6.6)]], axis=lambda k: (dx, 20, -6.0), closed=False)
    for q in range(3):
        b.cone('StoneLight', dx - 4.5 + q * 4.5, QUAY_TOP + 15.2, -4.2 + 0.35, 0.3, 0.2, 0.9, 8)
    b.spot('Spot_Dome', dx, 22.0, -1.9)


# ---------------------------------------------------------------- the far bank: the clock tower (5 x 26 x 5 at x = 24) and the balconies
def clock_tower(b):
    cx = 24.0
    zf = 51.0
    zc = zf + 2.5
    y0 = QUAY_TOP
    b.box('Cream', cx, y0 + 13.0, zc, 5.0, 26.0, 5.0)
    b.box('StoneDark', cx, y0 + 0.8, zf - 0.04, 5.2, 1.6, 0.16)
    for y in (3.0, 6.0, 9.0, 12.0, 15.0, 18.0):
        b.box('Trim', cx, y0 + y + 1.2, zf - 0.06, 5.3, 0.2, 0.16)
    for sx in (-1, 1):
        for q in range(int(26 / 1.1)):
            b.box('StoneLight' if q % 2 else 'Trim', cx + sx * 2.45, y0 + 0.5 + q * 1.1, zf - 0.05, 0.3 + 0.08 * (q % 2), 0.5, 0.12)
    for y in (2.2, 5.0, 8.0, 11.0, 14.0):
        b.box('Trim', cx, y0 + y, zf - 0.05, 0.9, 1.7, 0.08)
        b.box('Glass', cx, y0 + y, zf - 0.09, 0.62, 1.4, 0.05)
        b.cone('StoneLight', cx, y0 + y + 0.7, zf - 0.06, 0.45, 0.01, 0.2, 8)
    b.box('StoneLight', cx, y0 + 1.1, zf - 0.08, 1.8, 2.2, 0.14)
    b.box('Door', cx, y0 + 1.0, zf - 0.15, 1.3, 2.0, 0.1)
    # the clock: a round white face in a gilt rim, twelve marks, two hands, a pediment over it
    fy = y0 + 22.0
    b.box('StoneLight', cx, fy, zf - 0.05, 3.4, 3.4, 0.14, bevel=0.05)
    b.limb('Gold', (cx, fy, zf - 0.1), (cx, fy, zf - 0.18), 1.5, 1.5, 20)
    b.limb('ClockFace', (cx, fy, zf - 0.16), (cx, fy, zf - 0.22), 1.4, 1.4, 20)
    for q in range(12):
        a = q * math.pi / 6
        b.box('Iron', cx + math.sin(a) * 1.15, fy + math.cos(a) * 1.15, zf - 0.2, 0.1 if q % 3 else 0.16, 0.18 if q % 3 else 0.28, 0.03, rot_y=0)
    b.box('Iron', cx, fy + 0.45, zf - 0.22, 0.1, 0.9, 0.03)
    b.box('Iron', cx + 0.35, fy, zf - 0.22, 0.7, 0.08, 0.03)
    b.ico('Gold', cx, fy, zf - 0.24, 0.1, 1.0, sub=0)
    b.box('StoneLight', cx, fy + 1.95, zf - 0.1, 3.8, 0.24, 0.3, bevel=0.03)
    # the belfry and the pyramid roof with a gilt ball and a weathervane
    by = y0 + 25.0
    b.box('Stone', cx, by + 0.3, zc, 5.5, 0.6, 5.5)
    for sx in (-1, 1):
        for sz in (-1, 1):
            b.box('StoneLight', cx + sx * 2.2, by + 1.8, zc + sz * 2.2, 0.5, 2.4, 0.5, bevel=0.03)
    b.cone('Bell', cx, by + 1.1, zc, 0.4, 0.55, 0.8, 10)
    b.box('StoneLight', cx, by + 3.2, zc, 5.6, 0.4, 5.6, bevel=0.03)
    sy = by + 3.4
    pts = [(cx + 3.9 * math.cos(a), sy, zc + 3.9 * math.sin(a)) for a in (math.pi / 4, 3 * math.pi / 4, 5 * math.pi / 4, 7 * math.pi / 4)]
    apex = (cx, sy + 5.0, zc)
    for i in range(4):
        p, q = pts[i], pts[(i + 1) % 4]
        b.skin('Roof', [[p, q], [apex, apex]], axis=lambda k: (cx, sy, zc), closed=False)
        for lv in range(1, 6):
            t = lv / 6
            a_, c_ = tuple(p[j] + (apex[j] - p[j]) * t for j in range(3)), tuple(q[j] + (apex[j] - q[j]) * t for j in range(3))
            b.limb('RoofDark', (a_[0], a_[1] + 0.03, a_[2]), (c_[0], c_[1] + 0.03, c_[2]), 0.03, 0.03, 3)
    b.ico('Gold', cx, sy + 5.1, zc, 0.25, 1.0, sub=1)
    b.limb('Iron', (cx, sy + 5.3, zc), (cx, sy + 6.8, zc), 0.04, 0.03, 4)
    b.box('Iron', cx + 0.3, sy + 6.5, zc, 0.8, 0.2, 0.03)
    balcony(b, cx, 18.0, zf, -1.0, 3.6, 2.4, gap=(-0.3, 0.3), door=True, name='Spot_ClockTower', seed=31)


# ---------------------------------------------------------------- the steps down into the canal (collision: 4 steps at x = -34)
def quay_steps(b):
    for i in range(4):
        top = QUAY_TOP - 0.15 - i * 0.3
        zc = -2.65 + i * 0.5 + 0.0
        b.box('StoneLight', -34.0, top - 0.04, zc, 3.0, 0.12, 0.74, bevel=0.03)
        b.box('Stone', -34.0, top - 0.2 - 0.0, zc, 2.9, top + 6.0 - 0.0 if False else 0.3, 0.7)
    b.box('Stone', -34.0, -2.0, -1.5, 3.0, 4.0, 1.0)
    for sx in (-1, 1):                                                                           # a low wall each side and an iron post with a ring
        b.box('Stone', -34.0 + sx * 1.7, QUAY_TOP - 0.1, -2.5, 0.4, 0.5, 1.8, bevel=0.03)
        b.cone('Iron', -34.0 + sx * 1.7, QUAY_TOP + 0.15, -3.7, 0.12, 0.08, 0.8, 6)
    b.spot('Spot_Steps', -34.0, QUAY_TOP, -3.0)


# ---------------------------------------------------------------- life on the quays and in the canal
def tree(b, x, z, s=1.0, seed=0):
    b.limb('WoodDark', (x, QUAY_TOP - 0.1, z), (x, QUAY_TOP + 2.4 * s, z), 0.14 * s, 0.1 * s, 6)
    for k, (dx, dy, dz, r) in enumerate(((0, 3.0, 0, 1.2), (0.8, 2.6, 0.4, 0.9), (-0.8, 2.7, -0.3, 0.9), (0.2, 3.7, 0.2, 0.8))):
        b.ico('Leaf' if k % 2 else 'LeafDark', x + dx * s, QUAY_TOP + dy * s, z + dz * s, r * s, 0.85, sub=1, jit=0.12, seed=seed + k)


def cafe(b, x, z, o, seed):
    """A little café on the far promenade: round tables with parasols and chairs."""
    for k in range(2):
        tx = x + k * 2.2
        b.cone('WoodDark', tx, QUAY_TOP, z, 0.4, 0.35, 0.74, 8)
        b.cone('Wood', tx, QUAY_TOP + 0.72, z, 0.45, 0.45, 0.05, 10)
        b.limb('Iron', (tx, QUAY_TOP, z), (tx, QUAY_TOP + 2.2, z), 0.03, 0.03, 4)
        mat = ('AwnRed', 'AwnGreen')[(seed + k) % 2]
        b.cone(mat, tx, QUAY_TOP + 2.0, z, 1.25, 0.05, 0.35, 8)
        for sg in (-1, 1):
            b.box('Wood', tx + sg * 0.75, QUAY_TOP + 0.45, z, 0.4, 0.05, 0.4)
            b.box('Wood', tx + sg * 0.75, QUAY_TOP + 0.85, z + sg * 0.0, 0.05, 0.7, 0.4)
            for lx, lz in ((-0.15, -0.15), (0.15, 0.15)):
                b.limb('Wood', (tx + sg * 0.75 + lx, QUAY_TOP, z + lz), (tx + sg * 0.75 + lx, QUAY_TOP + 0.45, z + lz), 0.025, 0.025, 4)


def awning(b, x, w, y, zf, o, seed):
    """Striped canvas awning over a ground-floor shop window."""
    n = max(4, int(w / 0.45))
    for q in range(n):
        xa = x - w / 2 + q * w / n
        mat = 'AwnRed' if q % 2 else 'AwnWhite'
        b.skin(mat, [[(xa, y + 0.4, zf + 0.05 * o), (xa + w / n, y + 0.4, zf + 0.05 * o)], [(xa, y - 0.15, zf + 1.1 * o), (xa + w / n, y - 0.15, zf + 1.1 * o)]],
               axis=lambda k: (x, y - 1.0, zf), closed=False)
        b.box(mat, xa + w / (2 * n), y - 0.2, zf + 1.12 * o, w / n, 0.22, 0.03)


def rowboat(b, x, z, yaw, mat='BoatRed', length=3.8, trim='Gold'):
    """A wooden rowing boat: a curved hull through ribs (sheer rising at both ends), a lining, thwarts with cushions, oars in rowlocks, a painted stripe, a lantern."""
    c, s_ = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))

    def P(u, y, v):
        return (x + u * c + v * s_, y, z - u * s_ + v * c)
    half = length / 2
    stations = [-half, -half * 0.86, -half * 0.55, -half * 0.2, half * 0.2, half * 0.55, half * 0.86, half]

    def prof(v):
        t = abs(v) / half
        return 0.5 * (1 - t ** 2.2) ** 0.55 + 0.03, 0.42 + 0.46 * t ** 2
    rings, inner, stripe = [], [], []
    for v in stations:
        w, sheer = prof(v)
        keel = -0.16 + 0.35 * (abs(v) / half) ** 3
        rings.append([P(-w, sheer, v), P(-w * 0.95, 0.12, v), P(-w * 0.55, keel + 0.04, v), P(0, keel, v), P(w * 0.55, keel + 0.04, v), P(w * 0.95, 0.12, v), P(w, sheer, v)])
        wi = w - 0.05
        inner.append([P(-wi, sheer - 0.03, v), P(-wi * 0.9, 0.16, v), P(-wi * 0.5, keel + 0.09, v), P(0, keel + 0.08, v), P(wi * 0.5, keel + 0.09, v), P(wi * 0.9, 0.16, v), P(wi, sheer - 0.03, v)])
        stripe.append([P(-w * 1.01, sheer - 0.04, v), P(-w * 1.01, sheer - 0.14, v)])
    b.skin(mat, rings, axis=lambda k: P(0, 0.4, stations[k]), closed=False)
    b.skin('Wood', inner, axis=lambda k: P(0, 0.4, stations[k]), closed=False, flip=True)
    for side in (0, 1):
        sr = [[P((-1 if side == 0 else 1) * prof(v)[0] * 1.012, prof(v)[1] - 0.03, v), P((-1 if side == 0 else 1) * prof(v)[0] * 1.012, prof(v)[1] - 0.14, v)] for v in stations]
        b.skin(trim, sr, axis=lambda k: P(0, 0.3, stations[k]), closed=False)
    for sgn in (-1, 1):                                                                          # gunwale rails
        for i in range(len(stations) - 1):
            v0, v1 = stations[i], stations[i + 1]
            b.limb('WoodDark', P(sgn * prof(v0)[0], prof(v0)[1] + 0.01, v0), P(sgn * prof(v1)[0], prof(v1)[1] + 0.01, v1), 0.03, 0.03, 4)
    for v, cush in ((-half * 0.45, True), (half * 0.05, False), (half * 0.5, True)):
        w, _ = prof(v)
        px, py, pz = P(0, 0.34, v)
        b.box('Wood', px, py, pz, 2 * w * 0.95, 0.05, 0.3, rot_y=yaw)
        if cush:
            qx, qy, qz = P(0, 0.4, v)
            b.box('AwnRed', qx, qy, qz, 2 * w * 0.7, 0.07, 0.28, rot_y=yaw, bevel=0.02)
    for sgn, (u1, v1) in ((-1, (-0.2, 0.2)), (1, (0.2, -0.1))):                                  # two oars resting in rowlocks
        ox, oy, oz = P(sgn * 0.48, 0.56, half * 0.05)
        ax, ay, az = P(sgn * 1.7, 0.2, half * 0.05 + 0.5)
        bx_, by_, bz_ = P(-sgn * 0.6, 0.9, half * 0.05 - 0.4)
        b.limb('Wood', (bx_, by_, bz_), (ax, ay, az), 0.03, 0.04, 4)
        b.limb('Iron', (ox, oy - 0.1, oz), (ox, oy + 0.08, oz), 0.02, 0.02, 4)
    lx, ly, lz = P(0, 0.9, half - 0.08)
    b.limb('Iron', (lx, ly - 0.35, lz), (lx, ly + 0.15, lz), 0.02, 0.02, 4)
    b.box('Lit', lx, ly + 0.0, lz, 0.14, 0.18, 0.14)
    bx_, by_, bz_ = P(0, 0.62, -half + 0.05)
    rope(b, (bx_, by_, bz_), (bx_ + 0.2 * s_, by_ + 0.05, bz_ + 0.5 * c), 0.05, 'Rope', 0.02, 3)


def stall(b, x, z, seed):
    """A market stall: a table with crates of fruit and flowers under a striped awning on two poles."""
    b.box('Wood', x, QUAY_TOP + 0.5, z, 2.2, 0.08, 1.0, bevel=0.02)
    for sx in (-1, 1):
        b.limb('WoodDark', (x + sx * 1.0, QUAY_TOP, z - 0.4), (x + sx * 1.0, QUAY_TOP + 2.3, z - 0.4), 0.04, 0.04, 5)
        b.limb('WoodDark', (x + sx * 1.0, QUAY_TOP, z + 0.4), (x + sx * 1.0, QUAY_TOP + 1.9, z + 0.4), 0.04, 0.04, 5)
        b.box('WoodDark', x + sx * 1.0, QUAY_TOP + 0.25, z, 0.06, 0.5, 0.8)
    for q in range(8):
        mat = 'AwnRed' if (q + seed) % 2 else 'AwnWhite'
        b.skin(mat, [[(x - 1.2 + q * 0.3, QUAY_TOP + 2.3, z - 0.55), (x - 0.9 + q * 0.3, QUAY_TOP + 2.3, z - 0.55)], [(x - 1.2 + q * 0.3, QUAY_TOP + 1.9, z + 0.75), (x - 0.9 + q * 0.3, QUAY_TOP + 1.9, z + 0.75)]],
               axis=lambda k: (x, QUAY_TOP, z), closed=False)
    for k in range(5):
        px = x - 0.8 + k * 0.4
        b.box('Pallet' if 'Pallet' in COLOURS else 'Wood', px, QUAY_TOP + 0.62, z + 0.05, 0.34, 0.16, 0.6, bevel=0.02)
        for j in range(3):
            b.ico(('Fruit', 'FruitG', 'Flower', 'FlowerYellow')[(k + seed) % 4], px + (j - 1) * 0.08, QUAY_TOP + 0.76, z + 0.05 + (j % 2) * 0.12, 0.09, 1.0, sub=0)
    b.cone('Terracotta', x + 1.5, QUAY_TOP, z + 0.2, 0.25, 0.2, 0.5, 8)
    b.ico('Leaf', x + 1.5, QUAY_TOP + 0.75, z + 0.2, 0.3, 0.9, sub=1, jit=0.15, seed=seed)


def life(b):
    # lamps along both promenades, planters and benches, trees on the quays between the houses and the cathedral
    for x in range(-40, 41, 10):
        if abs(x - 20) < 3 or abs(x - 36) < 8:
            continue
        lamp(b, x + 0.0, -3.55, 1.0)
        if x % 20 == 0:
            planter(b, x + 1.4, QUAY_TOP, -3.6, 1.0, x)
    for x in range(-36, 41, 12):
        if abs(x - 24) < 4:
            continue
        lamp(b, x + 3.0, 50.5, -1.0)
    for x in range(-44, 45, 8):
        if abs(x - 24) < 4 or -2 < x < 2:
            continue
        if x % 16 == 0:
            tree(b, x + 1.0, 49.2, 0.9 + 0.2 * hash01(x), x)
        else:
            planter(b, x + 0.5, QUAY_TOP, 48.7, 1.1, x)
    for q, x in enumerate((-36.0, -26.0, -9.0)):
        cafe(b, x, 49.6, -1.0, q)
    # awnings over some ground-floor windows of the far row, and flower pots on the near row's steps
    for k, (x, w, h, c) in enumerate(FAR):
        if k % 2 == 0 and w > 7:
            awning(b, x - w / 4 - 0.5, 2.6, 3.2, 51.0, -1.0, k)
    for sx_, seed_ in ((-1.8, 1), (17.5, 2), (31.0, 3)):
        stall(b, sx_, 49.6, seed_)
    # benches along the near promenade
    for x in (-37.0, 8.0):
        b.box('Wood', x, QUAY_TOP + 0.45, -3.55, 1.8, 0.06, 0.45, bevel=0.02)
        b.box('Wood', x, QUAY_TOP + 0.85, -3.8, 1.8, 0.4, 0.05, bevel=0.02)
        for dx in (-0.8, 0.8):
            b.box('Iron', x + dx, QUAY_TOP + 0.22, -3.55, 0.06, 0.45, 0.4)
    # boats tied at the quays (the gondolas are the game's own), striped mooring poles near them, a rowing boat at the steps
    rowboat(b, -39.0, 0.9, 92.0, 'BoatRed', 3.8)
    rowboat(b, 30.0, 46.6, 88.0, 'BoatWhite', 3.8, 'AwnGreen')
    rowboat(b, 0.0, -1.55, 90.0, 'BoatRed', 3.6)
    rowboat(b, 3.0, 45.9, 86.0, 'BoatWhite', 3.6, 'AwnRed')
    for px, pz in ((-27.8, 0.9), (-20.4, 0.9), (21.0, 4.8), (27.2, 6.8)):
        b.limb('WoodDark', (px, -2.5, pz), (px, 2.4, pz), 0.14, 0.12, 6)
        for q in range(5):
            b.cone('PoleRed' if q % 2 else 'PoleWhite', px, 0.2 + q * 0.4, pz, 0.145, 0.145, 0.4, 6)
        b.cone('StoneDark', px, 2.35, pz, 0.15, 0.1, 0.12, 6)
    # washing lines across the near alleys at the first floors, laundry in colours
    for (xa, xb, y, z) in ((-30.4, -29.6, 7.0, -4.8),):
        pass
    for k, (x0, x1, y) in enumerate(((-37.0, -30.0, 8.5), (-24.5, -16.0, 9.0), (-9.0, -3.0, 6.5), (2.5, 4.0, 7.0))):
        pass


# ---------------------------------------------------------------- build
def build():
    b = locbuild.Builder()
    for k, v in COLOURS.items():
        b.mat(k, v)
    block_base(b)
    quay(b, False)
    quay(b, True)
    town_rows(b)
    balcony(b, -20.0, 9.0, -4.2, 1.0, 4.0, 2.4, gap=(-0.3, 0.3), door=True, name='Spot_Balcony', seed=3)
    balcony(b, -14.0, 7.0, 51.0, -1.0, 4.0, 2.4, gap=(-0.3, 0.3), door=True, name='Spot_FarBalcony', seed=5)
    footbridge(b)
    bell_tower(b)
    cathedral(b)
    clock_tower(b)
    quay_steps(b)
    life(b)
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
            n.inputs['Base Color'].default_value = (0.05, 0.35, 0.33, 1)
            n.inputs['Alpha'].default_value = 0.78
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
    objs = b.finish('Loc_Town')
    print('LOCATION TRIS', b.tris, 'objects', len(objs))
    b.export(OUT)
    add_preview_water()
    view.shoot('loc_town_overview', (0, 8, 10), 140, (0.2, 0.5, 1.0), lens=45, res=(1200, 800))
    view.shoot('loc_town_near', (-12, 6, -4), 55, (0.3, 0.2, 1.0), lens=50, res=(1100, 700))
    view.shoot('loc_town_bridge', (0, 4, 1), 34, (0.3, 0.3, 1.0), lens=50, res=(1100, 700))
    view.shoot('loc_town_cathedral', (30, 14, -8), 60, (0.0, 0.2, 1.0), lens=50, res=(1100, 700))
    view.shoot('loc_town_far', (6, 6, 52), 60, (0.0, 0.25, -1.0), lens=50, res=(1100, 700))
    print('LOC DONE')


if __name__ == '__main__':
    main()
