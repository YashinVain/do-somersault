"""Location 4, the night harbour, as one scene (Docs/ModelBrief.md section 3.4). Output: Models/Locations/Loc_Port.fbx + previews.
    blender -b --python Blender/lowpoly/loc_port.py
Site space like the game's: the block is 90 x 90 m, x -45..45, z -30..60, water at y = 0 (not modelled), sea floor at -6. The quay, the container
stacks, the tug, the barge, the crane and the harbour light stand exactly where the game's solid boxes are; this only dresses them.
Materials called Lit and RedLight are made to glow by the game (UseLocationModel); the rest is plain colour, night is the game's lighting.
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

OUT = os.path.normpath(os.path.join(HERE, '..', '..', 'Assets', '_Project', 'Models', 'Locations', 'Loc_Port.fbx'))

CONT = {'Blue': '#2F5B94', 'Orange': '#C2602B', 'Green': '#3C7A52', 'Red': '#9B3030', 'Grey': '#8A9097', 'Yellow': '#C9A22E'}
CONT_ORDER = ['Blue', 'Orange', 'Green', 'Red', 'Grey', 'Yellow']


def shade(hexcol, k):
    h = hexcol.lstrip('#')
    return '#' + ''.join('%02X' % max(0, min(255, int(int(h[i:i + 2], 16) * k))) for i in (0, 2, 4))


COLOURS = {
    'Soil': '#4A3A2E', 'Earth': '#5E4A38', 'BlockRock': '#4A4C50', 'Seabed': '#3E5560', 'SeabedDark': '#33474F',
    'Concrete': '#6E7378', 'ConcreteDark': '#4C5157', 'ConcreteLight': '#838990', 'Asphalt': '#3B4046', 'Joint': '#464B50', 'Weed': '#2F5B45', 'Rust': '#8A4B2A',
    'CraneYellow': '#D9A62E', 'CraneShade': '#B58318', 'Hazard': '#1E2126', 'Grey': '#59616B', 'GreyLight': '#8A929A', 'Black': '#1E2126', 'Tire': '#16181C',
    'HullRed': '#8E2B2B', 'HullRedDark': '#6B1F1F', 'White': '#E8E6DE', 'WhiteShade': '#C9C7BF', 'Orange': '#E07A2F',
    'WallA': '#8A8F96', 'WallB': '#767B83', 'Roof': '#3D4249', 'RoofLight': '#565C64', 'Door': '#4E555E',
    'Lit': '#FFD98A', 'RedLight': '#FF5A4A', 'GreenLight': '#5AE08A', 'Glass': '#1F2E40',
    'Wood': '#7A5A3E', 'WoodDark': '#5A4330', 'Rope': '#C9B88A', 'Pallet': '#A88652', 'Rock': '#5B6066', 'RockDark': '#464B50', 'Sign': '#E8E6DE',
    'Barrel': '#3C6E8E', 'BarrelRed': '#A8402E', 'Cone': '#E8661F', 'Gull': '#F4F4F0', 'Steel': '#9CA5AE',
}
for _n, _c in CONT.items():
    COLOURS['Cont' + _n] = _c
    COLOURS['Cont' + _n + 'Dark'] = shade(_c, 0.78)
COLOURS['ContSteel'] = '#59616B'


def hash01(i):
    return (math.sin(i * 12.9898) * 43758.5453) % 1.0


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


QUAY_TOP, QUAY_EDGE, QUAY_BACK = 1.2, -3.0, -30.0
HALF = 45.0


# ---------------------------------------------------------------- small things
def rope(b, p0, p1, sag=0.4, mat='Rope', r=0.035, n=6):
    pts = []
    for i in range(n + 1):
        t = i / n
        pts.append((p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t - sag * 4 * t * (1 - t), p0[2] + (p1[2] - p0[2]) * t))
    for a, c in zip(pts, pts[1:]):
        b.limb(mat, a, c, r, r, 4)


def tyre(b, x, y, z, r=0.4, h=0.3):
    b.cone('Tire', x, y, z, r, r, h, 8)


def barrel(b, x, y, z, mat='Barrel'):
    b.cone(mat, x, y, z, 0.3, 0.3, 0.85, 8)
    b.cone('Steel', x, y + 0.25, z, 0.31, 0.31, 0.04, 8)
    b.cone('Steel', x, y + 0.58, z, 0.31, 0.31, 0.04, 8)


def crate(b, x, y, z, s=0.9, yaw=0.0, mat='Pallet'):
    b.box(mat, x, y + s / 2, z, s, s, s, bevel=0.03, rot_y=yaw)
    b.box('WoodDark', x, y + s / 2, z, s + 0.02, 0.06, s + 0.02, rot_y=yaw)
    b.box('WoodDark', x, y + s * 0.15, z, s + 0.02, 0.05, s + 0.02, rot_y=yaw)


def pallet(b, x, y, z, yaw=0.0):
    for dz in (-0.45, 0, 0.45):
        b.box('Pallet', x, y + 0.07, z + dz, 1.1, 0.1, 0.16, rot_y=yaw)
    b.box('Pallet', x, y + 0.15, z, 1.1, 0.04, 1.1, rot_y=yaw)


def cone_marker(b, x, z, y=QUAY_TOP):
    b.cone('Cone', x, y, z, 0.22, 0.04, 0.6, 6)
    b.box('Black', x, y + 0.02, z, 0.5, 0.04, 0.5)


def lifebuoy_post(b, x, z):
    b.limb('Grey', (x, QUAY_TOP, z), (x, QUAY_TOP + 1.6, z), 0.05, 0.05, 5)
    b.box('Grey', x + 0.2, QUAY_TOP + 1.5, z, 0.45, 0.04, 0.04)
    b.cone('Orange', x + 0.45, QUAY_TOP + 1.1, z - 0.03, 0.38, 0.38, 0.1, 12)
    b.cone('White', x + 0.46, QUAY_TOP + 1.11, z - 0.03, 0.4, 0.4, 0.05, 12)
    b.cone('Orange', x + 0.45, QUAY_TOP + 1.115, z - 0.03, 0.2, 0.2, 0.06, 12) if False else None


# ---------------------------------------------------------------- the block
def block_base(b):
    top = -6.1
    b.box('Soil', 0, top - 0.7, 15, 90, 1.4, 90)
    b.box('Earth', 0, (top - 1.4 - 9.0) / 2, 15, 90, (top - 1.4) - (-9.0), 90)
    b.box('BlockRock', 0, (-9.0 - 16.0) / 2, 15, 90, 7.0, 90)
    b.box('Seabed', 0, -6.05, 15, 90, 0.1, 90)
    for k in range(30):          # silt ripples, and what the harbour has dropped: tyres, barrels, a crate, a chain
        x, z = -42 + 84 * hash01(k * 3.7 + 1), -1 + 58 * hash01(k * 6.1 + 2)
        b.ico('SeabedDark', x, -6.0, z, 1.0, 1.0, sub=1, sc=(2.0 + 3 * hash01(k + 9), 0.07, 0.7 + 0.5 * hash01(k + 4)), jit=0.1, seed=k)
    for k in range(18):
        x, z = -40 + 80 * hash01(k * 2.1 + 5), 2 + 20 * hash01(k * 3.3 + 6)
        r = hash01(k + 12)
        if r < 0.4:
            tyre(b, x, -6.0, z, 0.45, 0.32)
        elif r < 0.7:
            barrel(b, x, -6.0, z, 'Rust' if k % 2 else 'Barrel')
        else:
            crate(b, x, -6.0, z, 0.8, 360 * hash01(k), 'Pallet')
    for k in range(10):
        x, z = -38 + 76 * hash01(k * 5.1 + 20), 3 + 14 * hash01(k * 4.3 + 21)
        b.ico('Rock', x, -6.0, z, 0.8, 0.6, sub=1, jit=0.15, seed=k + 40)


# ---------------------------------------------------------------- the quay
def quay(b):
    b.box('Concrete', 0, (QUAY_TOP - 6.0) / 2, (QUAY_EDGE + QUAY_BACK) / 2, 90, QUAY_TOP + 6.0, QUAY_EDGE - QUAY_BACK)
    # expansion joints, patched asphalt, worn paint
    for x in range(-42, 45, 6):
        b.box('Joint', x, QUAY_TOP + 0.004, (QUAY_EDGE + QUAY_BACK) / 2 - 1.0, 0.06, 0.01, 25.0)
    for z in (-8.0, -14.0, -20.0):
        b.box('Joint', 0, QUAY_TOP + 0.004, z, 90, 0.01, 0.06)
    for k in range(9):
        b.box('Asphalt', -40 + 10 * k + 3 * hash01(k), QUAY_TOP + 0.006, -9 - 12 * hash01(k * 3 + 1), 2.5 + 3 * hash01(k + 5), 0.01, 1.8 + 2 * hash01(k + 8), rot_y=15 * hash01(k))
    b.box('ConcreteLight', 0, QUAY_TOP - 0.05, QUAY_EDGE + 0.1, 90, 0.1, 0.5)                    # the kerb along the edge
    b.box('CraneYellow', 0, QUAY_TOP + 0.007, QUAY_EDGE - 0.45, 90, 0.014, 0.28)                    # the yellow edge line
    for x in range(-42, 43, 12):
        b.box('Sign', x, QUAY_TOP + 0.008, -6.2, 3.0, 0.014, 0.12)                                # bay lines for the trucks
    # the front of the quay: block joints, weed at the waterline, rubber fenders on chains, ladders, bollards
    for x in range(-45, 46, 3):
        b.box('Joint', x, (QUAY_TOP - 6.0) / 2, QUAY_EDGE + 0.02, 0.05, QUAY_TOP + 6.0, 0.05)
    for y in (0.6, -1.4, -3.4):
        b.box('Joint', 0, y, QUAY_EDGE + 0.02, 90, 0.05, 0.05)
    for k in range(60):
        x = -44 + 88 * hash01(k * 2.3 + 3)
        b.ico('Weed', x, -0.15 - 0.4 * hash01(k), QUAY_EDGE + 0.05, 0.4, 0.4, sub=0, sc=(1.8, 1.0, 0.6))
    for x in range(-42, 43, 6):
        z = -4.1
        b.cone('Black', x, QUAY_TOP, z, 0.32, 0.26, 0.55, 8)
        b.cone('Black', x, QUAY_TOP + 0.5, z, 0.4, 0.4, 0.16, 8)
        b.box('Tire', x + 3.0, -0.6, QUAY_EDGE + 0.2, 0.5, 1.5, 0.3, bevel=0.1)
        b.limb('Black', (x + 3.0, QUAY_TOP - 0.1, QUAY_EDGE + 0.1), (x + 3.0, QUAY_TOP - 0.1, QUAY_EDGE + 0.1), 0.01, 0.01, 3) if False else None
        b.box('Steel', x + 3.0, QUAY_TOP - 0.12, QUAY_EDGE + 0.28, 0.12, 0.2, 0.04)
    for x in (-34.0, -8.0, 16.0, 38.0):
        for dx in (-0.25, 0.25):
            b.limb('Rust', (x + dx, QUAY_TOP, QUAY_EDGE + 0.12), (x + dx, -2.2, QUAY_EDGE + 0.12), 0.03, 0.03, 4)
        y = QUAY_TOP - 0.3
        while y > -2.2:
            b.box('Rust', x, y, QUAY_EDGE + 0.12, 0.5, 0.04, 0.04)
            y -= 0.35
    # crane rails with sleepers
    for rz in (-8.0, -14.0):
        b.box('Grey', 24.0, QUAY_TOP + 0.08, rz, 36.0, 0.16, 0.2)
        b.box('GreyLight', 24.0, QUAY_TOP + 0.17, rz, 36.0, 0.03, 0.08)
        for xs in [6.2 + 0.9 * q for q in range(40)]:
            b.box('WoodDark', xs, QUAY_TOP + 0.03, rz, 0.28, 0.06, 0.7)
    lifebuoy_post(b, -27.0, -4.6)
    spot_x = -30.0
    b.spot('Spot_Quay', spot_x, QUAY_TOP, QUAY_EDGE)


def lamps(b):
    for x in range(-36, 37, 12):
        px, pz = x, -5.2
        b.cone('Grey', px, QUAY_TOP, pz, 0.28, 0.2, 0.5, 8)
        b.cone('Grey', px, QUAY_TOP + 0.4, pz, 0.12, 0.08, 6.9, 8)
        b.limb('Grey', (px, QUAY_TOP + 6.6, pz), (px, QUAY_TOP + 7.1, pz + 0.5), 0.07, 0.06, 5)
        b.limb('Grey', (px, QUAY_TOP + 7.1, pz + 0.5), (px, QUAY_TOP + 7.1, pz + 1.0), 0.06, 0.06, 5)
        b.box('Grey', x, QUAY_TOP + 7.15, -4.2, 0.78, 0.12, 0.42, bevel=0.03)
        b.box('Lit', x, QUAY_TOP + 7.07, -4.2, 0.66, 0.05, 0.32)
        b.box('Steel', x, QUAY_TOP + 1.0, pz + 0.12, 0.25, 0.4, 0.08)


# ---------------------------------------------------------------- warehouses
def warehouse(b, cx, w, seed):
    z0, z1, depth = -29.0, -19.0, 10.0
    zc = -24.0
    wall_h = 10.0
    y0 = QUAY_TOP
    b.box('ConcreteDark', cx, y0 + 0.45, zc, w + 0.3, 0.9, depth + 0.3)
    b.box('WallA', cx, y0 + wall_h / 2 + 0.45, zc, w, wall_h - 0.9, depth)
    # corrugation: vertical ribs on the front and the sides
    xs = cx - w / 2 + 0.5
    while xs < cx + w / 2 - 0.4:
        b.box('WallB', xs, y0 + wall_h / 2 + 0.45, z1 + 0.03, 0.12, wall_h - 1.0, 0.06)
        xs += 1.0
    for sx in (-1, 1):
        zs = z0 + 0.5
        while zs < z1 - 0.3:
            b.box('WallB', cx + sx * (w / 2 + 0.03), y0 + wall_h / 2 + 0.45, zs, 0.06, wall_h - 1.0, 0.12)
            zs += 1.0
    b.box('WallB', cx, y0 + wall_h - 0.1, z1 + 0.05, w + 0.1, 0.5, 0.12)                       # eaves band
    # gable roof: two sheets on a ridge, gables closed
    ridge = y0 + wall_h + 1.9
    eave = y0 + wall_h
    oz = 0.5
    for sgn, mat in ((-1, 'Roof'), (1, 'Roof')):
        ze = zc + sgn * (depth / 2 + oz)
        b.skin(mat, [[(cx - w / 2 - 0.3, eave - 0.1 * 0, ze), (cx + w / 2 + 0.3, eave, ze)], [(cx - w / 2 - 0.3, ridge, zc), (cx + w / 2 + 0.3, ridge, zc)]],
               axis=lambda k: (cx, y0 + 3.0, zc), closed=False)
        b.skin('RoofLight', [[(cx - w / 2 - 0.3, eave - 0.12, ze), (cx + w / 2 + 0.3, eave - 0.12, ze)], [(cx - w / 2 - 0.3, ridge - 0.12, zc), (cx + w / 2 + 0.3, ridge - 0.12, zc)]],
               axis=lambda k: (cx, y0 + 30.0, zc), closed=False)
    for sx in (-1, 1):
        xg = cx + sx * (w / 2 + 0.02)
        b.skin('WallA', [[(xg, eave, z0), (xg, eave, z1)], [(xg, ridge, zc), (xg, ridge, zc)]], axis=lambda k: (cx, eave - 3, zc), closed=False)
    b.box('RoofLight', cx, ridge + 0.05, zc, w + 0.6, 0.12, 0.3)                                  # the ridge cap
    for k in range(int(w // 6)):                                                                  # roof vents
        b.cone('Grey', cx - w / 2 + 3 + k * 6, ridge - 0.9, zc - 2.2, 0.35, 0.3, 0.6, 8)
    # doors: rolling shutters with slats, frames, dock lamps above them
    nd = 2 if w > 17 else 1
    for q in range(nd):
        dx = cx - (w / 4 if nd == 2 else 0) + (q * w / 2 if nd == 2 else 0)
        b.box('Grey', dx, y0 + 2.1, z1 + 0.06, 3.7, 4.0, 0.12, bevel=0.04)
        b.box('Door', dx, y0 + 2.0, z1 + 0.14, 3.3, 3.7, 0.08)
        for q2 in range(9):
            b.box('Grey', dx, y0 + 0.4 + q2 * 0.4, z1 + 0.19, 3.3, 0.03, 0.03)
        b.box('Grey', dx, y0 + 4.3, z1 + 0.24, 1.0, 0.18, 0.3, bevel=0.03)
        b.box('Lit', dx, y0 + 4.18, z1 + 0.34, 0.8, 0.04, 0.2)
        b.box('Sign', dx + 1.55, y0 + 3.4, z1 + 0.2, 0.3, 0.3, 0.02)
    # lit windows in a row high up, a few left dark, with frames
    wx = cx - w / 2 + 2.0
    q = 0
    while wx < cx + w / 2 - 1.0:
        lit = hash01(seed * 13.1 + q * 3.7) > 0.25
        b.box('Grey', wx, y0 + 6.0, z1 + 0.05, 1.6, 1.2, 0.08)
        b.box('Lit' if lit else 'Glass', wx, y0 + 6.0, z1 + 0.1, 1.4, 1.0, 0.06)
        b.box('Grey', wx, y0 + 6.0, z1 + 0.14, 0.05, 1.0, 0.04)
        b.box('Grey', wx, y0 + 6.0, z1 + 0.14, 1.4, 0.05, 0.04)
        wx += 3.2
        q += 1
    # drainpipes at the corners, a wall lamp, a small man door and a loading step
    for sx in (-1, 1):
        b.limb('Grey', (cx + sx * (w / 2 - 0.25), y0 + wall_h, z1 + 0.18), (cx + sx * (w / 2 - 0.25), y0 + 0.3, z1 + 0.18), 0.07, 0.07, 6)
    b.box('Grey', cx + w / 2 - 1.2, y0 + 1.1, z1 + 0.06, 1.0, 2.1, 0.1, bevel=0.03)
    b.box('Door', cx + w / 2 - 1.2, y0 + 1.05, z1 + 0.12, 0.85, 1.95, 0.06)
    b.box('Lit', cx + w / 2 - 1.2, y0 + 2.45, z1 + 0.28, 0.3, 0.14, 0.1)
    b.box('ConcreteLight', cx + w / 2 - 1.2, y0 + 0.07, z1 + 0.5, 1.4, 0.14, 0.7)
    # at the doors: pallets, barrels, crates and cones
    for q in range(3):
        pallet(b, cx - w / 2 + 3.0 + 2.4 * q, y0, z1 + 1.4, 8 * q)
        if q % 2 == 0:
            crate(b, cx - w / 2 + 3.0 + 2.4 * q, y0 + 0.15, z1 + 1.4, 0.9, 8 * q)
    for q in range(3):
        barrel(b, cx + w / 2 - 3.5 + 0.7 * q, y0, z1 + 1.3 + 0.2 * (q % 2), 'Barrel' if q % 2 else 'BarrelRed')


# ---------------------------------------------------------------- containers
def container(b, cx, cy, cz, name):
    """One 2.44 x 2.6 x 6.1 container (door end towards +z): painted body, corrugated sides, steel corner posts, locking bars on the door."""
    col, dark, steel = 'Cont' + name, 'Cont' + name + 'Dark', 'ContSteel'
    b.box(col, cx, cy, cz, 2.36, 2.46, 6.02, bevel=0.03)
    for sx in (-1, 1):
        zz = cz - 2.7
        while zz < cz + 2.75:
            b.box(dark, cx + sx * 1.2, cy, zz, 0.07, 2.28, 0.26)
            zz += 0.5
    zz = cz - 2.7
    for q in range(5):                                       # roof corrugation, a few ribs
        b.box(dark, cx, cy + 1.25, cz - 2.4 + q * 1.2, 2.2, 0.04, 0.3)
    for sx in (-1, 1):
        for sz in (-1, 1):
            b.box(steel, cx + sx * 1.17, cy, cz + sz * 2.95, 0.14, 2.6, 0.14)              # corner posts
            b.box(steel, cx + sx * 1.17, cy + 1.3, cz + sz * 2.95, 0.18, 0.1, 0.18)        # corner casting
            b.box(steel, cx + sx * 1.17, cy - 1.3, cz + sz * 2.95, 0.18, 0.1, 0.18)
    b.box(steel, cx, cy - 1.28, cz, 2.44, 0.1, 6.1)
    b.box(steel, cx, cy + 1.28, cz, 2.44, 0.06, 6.1)
    # door end (+z): two leaves, four locking bars, handles
    b.box(dark, cx, cy, cz + 3.0, 2.2, 2.3, 0.05)
    for dx in (-0.85, -0.3, 0.3, 0.85):
        b.box(steel, cx + dx, cy, cz + 3.05, 0.04, 2.2, 0.04)
        b.box(steel, cx + dx, cy - 0.5, cz + 3.08, 0.12, 0.05, 0.05)
    b.box(steel, cx, cy, cz + 3.04, 0.03, 2.3, 0.03)
    b.box(dark, cx, cy, cz - 3.0, 2.2, 2.3, 0.04)


def containers(b):
    for col in range(3):
        high = 3 if col == 1 else 2
        for row in range(high):
            name = CONT_ORDER[(col * 2 + row * 3 + 1) % 6]
            container(b, (col - 1) * 2.6, QUAY_TOP + 1.3 + row * 2.6, -6.65, name)
    for k in range(4):
        sx = -12.0 - k * 2.7 if k < 2 else 11.0 + (k - 2) * 2.7
        for row in range(2 + k % 2):
            container(b, sx, QUAY_TOP + 1.3 + row * 2.6, -6.65, CONT_ORDER[(k + row * 2) % 6])
    top = QUAY_TOP + 2.6 * 3
    b.box('Sign', 0, top + 0.012, -3.7, 2.2, 0.02, 0.14)                                     # a white line on the diving edge
    b.spot('Spot_Containers', 0.0, top, -3.6)


# ---------------------------------------------------------------- hulls
def hull(b, pt, stations, bands, deck_mat, deck_y, keel_fn):
    """A hull through cross-sections. pt(s, lat, y) -> Unity point; stations = [(s, half_width)]; bands = [(material, y_top, y_bottom or None)]."""
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


# ---------------------------------------------------------------- the tug (collision: hull 4.2 x 2.6 x 10.4, wheelhouse, funnel, bow)
def tug(b):
    tx, tz = -14.0, 9.0

    def pt(s, lat, y):
        return (tx + lat, y, tz + s)
    stations = [(-5.2, 1.7), (-4.9, 1.95), (-3.8, 2.1), (-1.0, 2.1), (2.0, 2.1), (4.2, 2.0), (5.4, 1.5), (6.3, 0.9), (6.9, 0.45), (7.1, 0.12)]
    hull(b, pt, stations, (('HullRed', 2.6, 0.45), ('Black', 0.45, -0.55), ('HullRedDark', -0.55, None)), 'Grey', 2.6, lambda s: -1.4 if s < 3 else -1.4 + (s - 3) / 4.1 * 1.4)
    # bulwarks and a fender rail along the deck edge, bow bollards
    for side in (-1, 1):
        for i in range(len(stations) - 1):
            s0, w0 = stations[i]
            s1, w1 = stations[i + 1]
            b.limb('Grey', pt(s0, side * (w0 - 0.05), 3.15), pt(s1, side * (w1 - 0.05), 3.15), 0.04, 0.04, 4)
            b.limb('Grey', pt(s0, side * (w0 - 0.05), 2.85), pt(s1, side * (w1 - 0.05), 2.85), 0.03, 0.03, 4)
        for s in (-4.5, -2.4, -0.3, 1.8, 3.9):
            w = 2.1 if -3.8 < s < 4.2 else 1.8
            b.limb('Grey', pt(s, side * (w - 0.05), 2.6), pt(s, side * (w - 0.05), 3.18), 0.035, 0.035, 4)
        for s in (-4.0, -2.0, 0.0, 2.0, 4.0):
            tyre(b, tx + side * 2.15, 0.2, tz + s, 0.42, 0.34)
    for ds in (-1.0, 1.0):
        b.cone('Black', tx + ds * 0.9, 2.6, tz + 5.4, 0.2, 0.15, 0.4, 8)
    for ds in (-1.0, 1.0):
        b.cone('Black', tx + ds * 1.6, 2.6, tz - 4.6, 0.18, 0.14, 0.35, 8)
    # a big tyre fender on the bow
    for q in range(5):
        a = (q - 2) * 0.35
        tyre(b, tx + math.sin(a) * 0.5, 1.0, tz + 7.15 - abs(a) * 0.4, 0.45, 0.34)
    # wheelhouse (2.2 high from the deck), roof with a plain front edge, glass all round
    wx, wz = tx, tz + 1.8
    b.box('White', wx, 3.7, wz, 3.0, 2.2, 3.2, bevel=0.04)
    b.box('Lit', wx, 3.95, wz + 1.62, 2.6, 0.8, 0.05)                                       # the front glazing: the lit bridge
    for q in range(5):
        b.box('Grey', wx - 1.04 + q * 0.52, 3.95, wz + 1.66, 0.05, 0.84, 0.05)
    for sx in (-1, 1):
        b.box('Glass', wx + sx * 1.52, 3.95, wz + 0.3, 0.05, 0.7, 1.7)
        b.box('Lit', wx + sx * 1.53, 3.95, wz - 0.9, 0.04, 0.5, 0.5)
    b.box('Black', wx, 4.86, wz, 3.34, 0.14, 3.5, bevel=0.03)
    b.box('WhiteShade', wx, 2.7, wz - 1.45, 3.0, 0.2, 0.3)
    b.box('Door', wx - 1.0, 3.4, wz - 1.62, 0.7, 1.6, 0.05)
    # the mast with its lamps and radar, the funnel with a red band
    b.limb('Grey', (wx, 4.9, wz - 0.9), (wx, 7.4, wz - 0.9), 0.06, 0.04, 5)
    b.box('Grey', wx, 7.0, wz - 0.9, 1.4, 0.05, 0.05)
    b.box('Grey', wx, 5.5, wz - 0.9, 0.9, 0.2, 0.7, bevel=0.03)
    b.ico('GreenLight', wx - 0.7, 6.95, wz - 0.9, 0.1, 1.0, sub=0)
    b.ico('RedLight', wx + 0.7, 6.95, wz - 0.9, 0.1, 1.0, sub=0)
    b.ico('Lit', wx, 7.45, wz - 0.9, 0.1, 1.0, sub=0)
    b.cone('Black', tx, 2.6, tz - 2.2, 0.62, 0.5, 3.4, 10)
    b.cone('HullRed', tx, 5.0, tz - 2.2, 0.56, 0.53, 0.6, 10)
    b.cone('Black', tx, 6.0, tz - 2.2, 0.5, 0.5, 0.1, 10)
    # deck gear: a towing winch and a bit of rope, a boat hook, a life raft, bollards for the lines to the quay
    b.box('Grey', tx, 2.9, tz + 4.2, 1.2, 0.5, 0.9, bevel=0.04)
    b.cone('Black', tx, 3.15, tz + 4.2, 0.35, 0.35, 0.3, 8)
    b.cone('Orange', tx + 1.1, 2.7, tz - 3.4, 0.4, 0.4, 0.8, 10)
    b.cone('Grey', tx + 1.1, 3.45, tz - 3.4, 0.42, 0.42, 0.06, 10)
    rope(b, (tx - 0.9, 2.7, tz + 5.4), (-12.5, 1.5, QUAY_EDGE - 1.0 - 3.1 + 3.1 + 0.0), 0.8, 'Rope', 0.04, 8)
    rope(b, (tx + 0.9, 2.7, tz + 5.4), (-12.0, 1.5, QUAY_EDGE - 1.0), 0.9, 'Rope', 0.04, 8)
    b.spot('Spot_Tug', tx, 4.9, tz + 3.4)


# ---------------------------------------------------------------- the barge (collision: hull 9 x 2.2 x 4.4, three crates)
def barge(b):
    bx, bz = -4.0, 28.0

    def pt(s, lat, y):
        return (bx + s, y, bz + lat)
    stations = [(-4.5, 1.55), (-4.2, 1.9), (-3.6, 2.2), (0.0, 2.2), (3.6, 2.2), (4.2, 1.9), (4.5, 1.55)]
    hull(b, pt, stations, (('HullRed', 2.1, 1.7), ('Black', 1.7, None)), 'Black', 2.0, lambda s: -0.6)
    b.box('HullRed', bx, 2.05, bz, 9.3, 0.2, 4.7, bevel=0.05)                                   # the red rim
    b.box('Black', bx, 2.12, bz, 8.9, 0.04, 4.3)
    b.box('CraneYellow', bx, 1.1, bz + 2.22, 9.0, 0.25, 0.05)
    b.box('CraneYellow', bx, 1.1, bz - 2.22, 9.0, 0.25, 0.05)
    for s in (-3.4, -1.1, 1.1, 3.4):                                                             # deck stiffeners and bollards
        b.box('Grey', bx + s, 2.16, bz, 0.12, 0.05, 4.2)
    for sx in (-1, 1):
        b.cone('Black', bx + sx * 4.0, 2.1, bz + 1.8, 0.2, 0.15, 0.4, 8)
        b.cone('Black', bx + sx * 4.0, 2.1, bz - 1.8, 0.2, 0.15, 0.4, 8)
    for k in range(6):
        tyre(b, bx - 3.6 + 1.45 * k, 0.9, bz + 2.4, 0.38, 0.3)
    for k in range(3):
        crate(b, bx - 2.6 + 2.6 * k, 2.25, bz + 0.9, 2.2, 0, 'Cont' + CONT_ORDER[(k * 2 + 1) % 6])
        b.box('ContSteel', bx - 2.6 + 2.6 * k, 4.46, bz + 0.9, 2.26, 0.05, 2.26)
    b.limb('Grey', (bx + 3.8, 2.1, bz - 1.9), (bx + 3.8, 3.0, bz - 1.9), 0.05, 0.05, 5)
    b.ico('RedLight', bx + 3.8, 3.0, bz - 1.9, 0.16, 1.0, sub=0)
    b.limb('Rope', (bx - 4.0, 2.15, bz + 1.8), (bx - 4.0, 2.15, bz + 1.8), 0.02, 0.02, 3)
    b.spot('Spot_Barge', bx, 2.15, bz - 1.8)


# ---------------------------------------------------------------- the gantry crane (collision: 4 legs, beam, boom, cabin, catwalk)
def crane(b):
    cx = 24.0
    ytop = 22.0
    for lx in (cx - 3.0, cx + 3.0):
        for lz in (-8.0, -14.0):
            b.box('CraneYellow', lx, (QUAY_TOP + ytop) / 2, lz, 0.9, ytop - QUAY_TOP, 0.9, bevel=0.04)
            for q in range(6):                                                                   # hazard stripes on the foot
                b.box('Hazard' if q % 2 else 'CraneYellow', lx, QUAY_TOP + 0.3 + q * 0.5, lz, 0.94, 0.25, 0.94)
            b.box('Grey', lx, QUAY_TOP + 0.3, lz, 1.5, 0.5, 1.5, bevel=0.05)                    # the bogie over the rail
            b.cone('Black', lx - 0.5, QUAY_TOP + 0.1, lz, 0.2, 0.2, 0.2, 8)
    # bracing: diagonals on the two sides and across the front and back, cross beams at 8 and 15 m
    for lz0, lz1 in ((-8.0, -14.0),):
        for sx in (cx - 3.0, cx + 3.0):
            b.limb('CraneShade', (sx, 3.0, lz0), (sx, 14.5, lz1), 0.14, 0.14, 4)
            b.limb('CraneShade', (sx, 3.0, lz1), (sx, 14.5, lz0), 0.14, 0.14, 4)
            b.limb('CraneShade', (sx, 15.5, lz0), (sx, 21.5, lz1), 0.12, 0.12, 4)
            b.limb('CraneShade', (sx, 15.5, lz1), (sx, 21.5, lz0), 0.12, 0.12, 4)
    for lz in (-8.0, -14.0):
        b.limb('CraneShade', (cx - 3.0, 3.0, lz), (cx + 3.0, 14.5, lz), 0.12, 0.12, 4)
        b.limb('CraneShade', (cx + 3.0, 3.0, lz), (cx - 3.0, 14.5, lz), 0.12, 0.12, 4)
    for y in (8.0, 14.4):
        for lz in (-8.0, -14.0):
            b.box('CraneShade', cx, y, lz, 6.0, 0.35, 0.4)
        for sx in (cx - 3.0, cx + 3.0):
            b.box('CraneShade', sx, y, -11.0, 0.4, 0.35, 6.0)
    # the top girder, boom and cabin
    b.box('CraneYellow', cx, 22.6, -11.0, 8.4, 1.6, 7.4, bevel=0.05)
    for q in range(8):
        b.box('CraneShade', cx - 3.7 + q * 1.05, 22.6, -7.28, 0.1, 1.5, 0.06)
    b.box('CraneYellow', cx, 22.6, 1.0, 1.6, 1.2, 22.0, bevel=0.04)
    for z in [-8.0 + 2.4 * q for q in range(8)]:
        b.limb('CraneShade', (cx - 0.7, 23.2, z), (cx - 0.7, 24.5, z + 1.2), 0.07, 0.07, 4)
        b.limb('CraneShade', (cx + 0.7, 23.2, z), (cx + 0.7, 24.5, z + 1.2), 0.07, 0.07, 4)
        b.limb('CraneShade', (cx - 0.7, 24.5, z + 1.2), (cx - 0.7, 23.2, z + 2.4), 0.07, 0.07, 4)
        b.limb('CraneShade', (cx + 0.7, 24.5, z + 1.2), (cx + 0.7, 23.2, z + 2.4), 0.07, 0.07, 4)
    for sx in (-0.7, 0.7):
        b.limb('CraneYellow', (cx + sx, 24.5, -8.0), (cx + sx, 24.5, 9.6), 0.08, 0.08, 4)
    b.box('CraneYellow', cx, 22.0, 6.0, 1.2, 0.5, 1.2)                                          # the trolley and a hook on cables
    for sx in (-0.4, 0.4):
        b.limb('Grey', (cx + sx, 21.8, 6.0), (cx + sx * 0.4, 19.0, 6.0), 0.025, 0.025, 3)
    b.box('Grey', cx, 18.8, 6.0, 0.5, 0.4, 0.3, bevel=0.05)
    b.limb('Black', (cx, 18.6, 6.0), (cx, 18.0, 6.0), 0.07, 0.05, 5)
    b.box('White', cx, 21.2, -4.5, 2.0, 1.8, 2.4, bevel=0.05)
    b.box('Lit', cx, 21.35, -3.27, 1.6, 0.8, 0.05)
    for q in range(4):
        b.box('Grey', cx - 0.6 + q * 0.4, 21.35, -3.25, 0.04, 0.8, 0.04)
    b.box('Grey', cx, 20.2, -4.5, 2.2, 0.2, 2.6)
    b.box('CraneYellow', cx, 22.6, 12.0 - 0.02, 1.6, 1.2, 0.04)
    b.box('Lit', cx, 23.205, 11.55, 1.6, 0.01, 0.14)                                            # the lit line at the boom tip
    b.limb('Grey', (cx, 23.2, 11.2), (cx, 24.4, 11.2), 0.04, 0.04, 4)
    b.ico('RedLight', cx, 24.6, 11.2, 0.2, 1.0, sub=0)
    b.limb('Grey', (cx + 0.7, 24.5, 9.6), (cx + 0.7, 24.5, 9.6), 0.02, 0.02, 3)
    # the catwalk at 15 m: slab with a grating, a yellow edge, rails on the sides and the back, brackets
    b.box('Grey', cx, 14.85, -5.0, 6.6, 0.3, 6.4, bevel=0.03)
    for q in range(16):
        b.box('GreyLight', cx - 3.0 + q * 0.4, 15.005, -5.0, 0.04, 0.012, 6.2)
    b.box('CraneYellow', cx, 15.008, -1.9, 6.6, 0.012, 0.18)
    for rx in (cx - 3.2, cx + 3.2):
        for rz in (-8.0, -6.8, -5.6, -4.4, -3.2):
            b.limb('CraneYellow', (rx, 15.0, rz), (rx, 16.1, rz), 0.04, 0.04, 4)
        for hy in (15.6, 15.95):
            b.limb('CraneYellow', (rx, hy, -8.0), (rx, hy, -3.2), 0.03, 0.03, 4)
    for hy in (15.6, 15.95):
        b.limb('CraneYellow', (cx - 3.2, hy, -8.0), (cx + 3.2, hy, -8.0), 0.03, 0.03, 4)
    for bx_ in (cx - 2.5, cx, cx + 2.5):
        b.limb('CraneShade', (bx_, 14.7, -2.0), (bx_, 12.6, -8.0), 0.07, 0.07, 4)
    # the ladder up the back legs with a safety cage
    for lx in (cx - 0.35, cx + 0.35):
        b.box('Grey', lx, (2.4 + 22.0) / 2, -14.7, 0.06, 22.0 - 2.4, 0.06)
    y = 2.4
    while y < 22.0:
        b.box('Grey', cx, y, -14.7, 0.7, 0.04, 0.05)
        y += 0.4
    y = 6.0
    while y < 21.6:
        for a in range(8):
            a0, a1 = a * 0.7854 + 0.0, (a + 1) * 0.7854
            if 0.5 < a0 < 2.6 + 3.2:
                b.limb('Grey', (cx + math.cos(a0) * 0.55, y, -14.7 - math.sin(a0) * 0.55), (cx + math.cos(a1) * 0.55, y, -14.7 - math.sin(a1) * 0.55), 0.02, 0.02, 3)
        y += 1.4
    b.spot('Spot_Catwalk', cx, 15.0, -1.8)
    b.spot('Spot_Boom', cx, 23.2, 11.6)


# ---------------------------------------------------------------- the breakwater and the harbour light (collision: rock 9 x 6 x 9, tower 2.8, gallery 5.6)
def harbour_light(b):
    lx, lz = 34.0, 32.0
    # the mole: a concrete road on rubble running east off the block, the light on its end
    b.box('ConcreteDark', 40.6, 1.0, lz, 11.0, 3.4, 5.4, bevel=0.08)
    b.box('Concrete', 40.6, 2.74, lz, 11.0, 0.12, 5.4, bevel=0.03)
    b.box('ConcreteLight', 40.6, 2.9, lz - 2.55, 11.0, 0.5, 0.3, bevel=0.04)       # a parapet on the harbour side... the open sea has the other
    for k in range(40):
        side = -1 if k % 2 else 1
        x = 36.5 + 8.5 * hash01(k * 2.3 + 3)
        z = lz + side * (3.1 + 1.0 * hash01(k * 3.1 + 4))
        b.ico('Rock' if k % 3 else 'RockDark', x, 0.0 + 0.8 * hash01(k), z, 1.0 + 0.7 * hash01(k + 6), 1.0, sub=1, sc=(1.1, 0.85, 1.0), jit=0.2, seed=k + 3)
    for x in (37.0, 40.0, 43.0):
        b.cone('Black', x, 2.8, lz + 2.2, 0.2, 0.15, 0.45, 8)
        b.cone('Black', x, 2.8, lz - 2.2, 0.2, 0.15, 0.45, 8)
    # the foot of the light: a heap of rubble up to the plinth
    for k in range(11):
        a = k * 0.571
        b.ico('Rock' if k % 2 else 'RockDark', lx + math.cos(a) * 3.1, 0.0 + 0.4 * hash01(k), lz + math.sin(a) * 3.1, 1.5 + 0.6 * hash01(k + 2), 1.0, sub=1, sc=(1.1, 0.9, 1.0),
              jit=0.2, seed=k + 33)
    b.cone('Concrete', lx, -1.0, lz, 4.2, 3.4, 5.0, 12)
    b.cone('ConcreteLight', lx, 4.0, lz, 2.4, 2.1, 0.3, 12)
    # the tower: white 8-sided shaft with a red band
    def tr(y):
        return 1.7 - (y + 6.0) / 20.0 * 0.45
    b.cone('White', lx, 4.3, lz, tr(4.3), tr(6.0), 1.7, 8)
    b.cone('HullRed', lx, 5.1, lz, tr(5.1) * 1.03, tr(6.9) * 1.03, 1.8, 8)
    b.cone('White', lx, 6.9, lz, tr(6.9), tr(11.85), 4.95, 8)
    b.box('Door', lx, 5.2, lz - 1.63, 0.8, 1.5, 0.12)
    for wy in (8.0, 10.0):
        b.box('Glass', lx, wy, lz - tr(wy) + 0.05, 0.4, 0.8, 0.12)
    # the gallery: grey floor, brackets, a rail round three quarters, open to the west where one jumps
    b.cone('Grey', lx, 11.55, lz, 2.9, 2.9, 0.3, 12)
    for k in range(12):
        a = k * 0.5236
        b.limb('Grey', (lx + math.cos(a) * 1.4, 11.2, lz + math.sin(a) * 1.4), (lx + math.cos(a) * 2.7, 11.6, lz + math.sin(a) * 2.7), 0.1, 0.07, 4)
    for k in range(17):
        a = math.radians(-100 + k * 13.0)            # a gap towards -x (180 degrees): angles around 180 are left out
        if abs(((math.degrees(a) + 180) % 360) - 180) > 60 or True:
            pass
    for k in range(20):
        a = math.radians(-120 + k * 12.5)
        px, pz = lx + math.cos(a) * 2.8, lz + math.sin(a) * 2.8
        if px < lx - 1.2:
            continue
        b.limb('Grey', (px, 11.85, pz), (px, 12.95, pz), 0.04, 0.04, 4)
    for hy in (12.45, 12.95):
        for k in range(19):
            a0, a1 = math.radians(-120 + k * 12.5), math.radians(-120 + (k + 1) * 12.5)
            if lx + math.cos(a0) * 2.8 < lx - 1.2 or lx + math.cos(a1) * 2.8 < lx - 1.2:
                continue
            b.limb('Grey', (lx + math.cos(a0) * 2.8, hy, lz + math.sin(a0) * 2.8), (lx + math.cos(a1) * 2.8, hy, lz + math.sin(a1) * 2.8), 0.03, 0.03, 4)
    b.cone('Lit', lx, 12.0, lz, 0.9, 0.9, 1.4, 8)
    for k in range(8):
        a = k * 0.7854
        b.limb('Grey', (lx + math.cos(a) * 0.95, 12.0, lz + math.sin(a) * 0.95), (lx + math.cos(a) * 0.95, 13.4, lz + math.sin(a) * 0.95), 0.045, 0.045, 4)
    b.cone('HullRed', lx, 13.4, lz, 1.9, 0.12, 1.1, 8)
    b.ico('RedLight', lx, 14.55, lz, 0.14, 1.0, sub=0)
    b.spot('Spot_Harbour', 31.7, 12.0, 32.0)


# ---------------------------------------------------------------- quay clutter: a site office, a forklift, stacked pallets, nets, drums, bins
def yard(b):
    # a site office: a cabin on blocks with steps and lit windows, west end
    ox, oz = -39.0, -9.0
    b.box('WallA', ox, QUAY_TOP + 1.6, oz, 6.0, 2.4, 2.6, bevel=0.04)
    b.box('White', ox, QUAY_TOP + 2.88, oz, 6.2, 0.2, 2.8, bevel=0.03)
    for q in range(3):
        b.box('Lit', ox - 1.7 + q * 1.7, QUAY_TOP + 1.8, oz + 1.32, 1.2, 0.8, 0.05)
    b.box('Door', ox + 2.3, QUAY_TOP + 1.2, oz + 1.31, 0.8, 1.9, 0.06)
    for q in range(3):
        b.box('Concrete', ox + 2.3, QUAY_TOP + 0.12 + q * 0.2, oz + 1.5 + 0.28 * (2 - q) * 0.0 + 0.3 * (2 - q) * 0.0, 1.0, 0.2, 0.35 + 0.35 * (2 - q))
    for dx in (-2.7, 2.7):
        b.box('ConcreteDark', ox + dx, QUAY_TOP + 0.2, oz, 0.5, 0.4, 2.4)
    b.limb('Grey', (ox - 2.6, QUAY_TOP + 3.0, oz), (ox - 2.6, QUAY_TOP + 4.6, oz), 0.04, 0.03, 4)
    # a forklift
    fx, fz = -20.0, -6.8
    b.box('CraneYellow', fx, QUAY_TOP + 0.7, fz, 1.2, 0.8, 1.9, bevel=0.06)
    b.box('Black', fx, QUAY_TOP + 1.6, fz - 0.2, 1.1, 0.06, 1.1)
    for dx in (-0.55, 0.55):
        b.limb('Black', (fx + dx, QUAY_TOP + 0.9, fz - 0.7), (fx + dx, QUAY_TOP + 1.6, fz - 0.7), 0.03, 0.03, 4)
        b.limb('Black', (fx + dx, QUAY_TOP + 0.9, fz + 0.3), (fx + dx, QUAY_TOP + 1.6, fz + 0.3), 0.03, 0.03, 4)
    b.box('Grey', fx, QUAY_TOP + 1.0, fz + 1.1, 0.8, 1.8, 0.1)
    for dx in (-0.3, 0.3):
        b.box('Grey', fx + dx, QUAY_TOP + 0.3, fz + 1.7, 0.12, 0.08, 1.0)
    for dx in (-0.65, 0.65):
        for dz in (-0.6, 0.7):
            b.cone('Tire', fx + dx, QUAY_TOP + 0.05, fz + dz, 0.33, 0.33, 0.28, 8)
    b.ico('Orange', fx, QUAY_TOP + 1.8, fz - 0.2, 0.12, 1.0, sub=0)
    # pallets, drums, nets, bins along the back of the quay between the warehouses
    for q in range(4):
        pallet(b, -15.0 + 1.3 * (q % 2), QUAY_TOP + 0.0 + (0.15 + 0.5) * (q // 2), -12.0, 5 * q)
        crate(b, -15.0 + 1.3 * (q % 2), QUAY_TOP + 0.15 + (0.65) * (q // 2), -12.0, 0.9, 5 * q)
    for q in range(8):
        barrel(b, 16.0 + 0.7 * (q % 4), QUAY_TOP, -11.0 - 0.7 * (q // 4), 'BarrelRed' if q % 3 == 0 else 'Barrel')
    for q in range(5):
        b.cone('Net', 31.0, QUAY_TOP, -9.0) if False else None
        b.ico('Weed', 33.0 + 0.9 * q, QUAY_TOP + 0.3, -7.0 + 0.4 * math.sin(q), 0.55, 0.5, sub=1, sc=(1.2, 0.7, 1.0), jit=0.2, seed=q)
    for q in range(3):
        b.cone('Grey', 38.0 + q * 0.9, QUAY_TOP, -6.0, 0.35, 0.3, 0.9, 8)
        b.cone('Black', 38.0 + q * 0.9, QUAY_TOP + 0.9, -6.0, 0.37, 0.37, 0.08, 8)
    for cxx, czz in ((-33.0, -6.5), (-26.0, -9.0), (4.5, -5.0), (30.0, -4.8), (41.0, -10.0)):
        cone_marker(b, cxx, czz)
    # a stack of tyres and coils of rope at the quay edge between the bollards, away from the diving place
    for q in range(5):
        tyre(b, -8.0, QUAY_TOP + q * 0.32, -4.6, 0.5, 0.3)
    b.cone('Rope', -10.0, QUAY_TOP, -4.6, 0.55, 0.45, 0.3, 10)
    b.cone('Rope', -10.0, QUAY_TOP + 0.3, -4.6, 0.4, 0.3, 0.2, 10)
    # an anchor displayed on a plinth near the east end
    ax, az = 41.0, -5.0
    b.box('ConcreteDark', ax, QUAY_TOP + 0.3, az, 1.2, 0.6, 1.2)
    b.limb('Rust', (ax, QUAY_TOP + 0.6, az), (ax, QUAY_TOP + 2.3, az), 0.07, 0.06, 5)
    b.box('Rust', ax, QUAY_TOP + 2.0, az, 0.8, 0.06, 0.06)
    b.limb('Rust', (ax - 0.5, QUAY_TOP + 0.9, az), (ax, QUAY_TOP + 0.6, az), 0.05, 0.07, 5)
    b.limb('Rust', (ax + 0.5, QUAY_TOP + 0.9, az), (ax, QUAY_TOP + 0.6, az), 0.05, 0.07, 5)


# ---------------------------------------------------------------- build
def build():
    b = locbuild.Builder()
    for k, v in COLOURS.items():
        b.mat(k, v)
    block_base(b)
    quay(b)
    lamps(b)
    warehouse(b, -30.0, 22.0, 1)
    warehouse(b, 6.0, 16.0, 2)
    warehouse(b, 36.0, 18.0, 3)
    containers(b)
    tug(b)
    barge(b)
    crane(b)
    harbour_light(b)
    yard(b)
    return b


def add_preview_water():
    import bmesh
    me = bpy.data.meshes.new('PreviewWater')
    ob = bpy.data.objects.new('PreviewWater', me)
    bpy.context.scene.collection.objects.link(ob)
    bm = bmesh.new()
    v = [bm.verts.new((x, y, 0.0)) for x, y in ((-HALF, 30.0), (HALF, 30.0), (HALF, -60.0), (-HALF, -60.0))]   # Blender y = -Unity z: z -30..60
    bm.faces.new(v)
    bm.to_mesh(me)
    bm.free()
    m = bpy.data.materials.new('PreviewWater')
    m.use_nodes = True
    for n in m.node_tree.nodes:
        if n.type == 'BSDF_PRINCIPLED':
            n.inputs['Base Color'].default_value = (0.01, 0.12, 0.25, 1)
            n.inputs['Alpha'].default_value = 0.7
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
    objs = b.finish('Loc_Port')
    print('LOCATION TRIS', b.tris, 'objects', len(objs))
    b.export(OUT)
    add_preview_water()
    view.shoot('loc_port_overview', (0, 5, 8), 150, (0.35, 0.55, 1.0), lens=50, res=(1200, 800))
    view.shoot('loc_port_crane', (24, 12, -6), 50, (0.6, 0.25, 1.0), lens=50, res=(1100, 700))
    view.shoot('loc_port_tug', (-14, 3, 9), 26, (0.8, 0.45, 0.8), lens=50, res=(1100, 700))
    view.shoot('loc_port_yard', (-6, 5, -12), 50, (0.2, 0.3, 1.0), lens=50, res=(1100, 700))
    view.shoot('loc_port_light', (34, 8, 32), 34, (-0.9, 0.3, 0.5), lens=50, res=(1100, 700))
    view.shoot('loc_port_barge', (-4, 2, 28), 22, (0.5, 0.4, 1.0), lens=50, res=(1100, 700))
    print('LOC DONE')


if __name__ == '__main__':
    main()
