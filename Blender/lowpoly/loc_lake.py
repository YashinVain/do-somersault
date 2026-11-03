"""Location 2, the forest lake, as one scene (Docs/ModelBrief.md section 3.2). Output: Models/Locations/Loc_Lake.fbx + previews.
    blender -b --python Blender/lowpoly/loc_lake.py
Site space like the game's: the block is 100 x 100 m with its centre at z = 6, water at y = 0 (not modelled). The ground follows the same
height function as the game's physics (lake_height below mirrors PrototypeSceneBuilder.LakeHeight).
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

OUT = os.path.normpath(os.path.join(HERE, '..', '..', 'Assets', '_Project', 'Models', 'Locations', 'Loc_Lake.fbx'))

COLOURS = {
    'Soil': '#5A4330', 'LakeMud': '#4F5A45', 'WetSand': '#8F8767', 'LakeSand': '#D8C38D', 'SandDry': '#E4D3A2', 'GrassDry': '#A3B45F', 'Dirt': '#B79F6F', 'Grass': '#5E9A4E', 'GrassLight': '#76B05A', 'GrassDark': '#4A8443', 'Moss': '#6E8F5A',
    'Wood': '#9C6B43', 'WoodLight': '#AF7E50', 'WoodDark': '#6E4A2F', 'Rope': '#3A3329', 'Paint_White': '#F2EFE6', 'Stone': '#A69A88',
    'StoneDark': '#857A6B', 'Rock': '#8C8F8A', 'RockDark': '#767A76', 'Bark': '#7E5238', 'BarkLight': '#946447', 'Willow': '#6E9E52', 'WillowDark': '#4F8444',
    'PineA': '#2F6B45', 'PineB': '#3C7D52', 'PineTrunk': '#8A5B3D', 'Reed': '#8A9A4F', 'LilyPad': '#4E8F45', 'Lily': '#F2B8C6', 'LifeRing': '#E04A3A',
    'Tent': '#E8B84A', 'TentDark': '#C98F2E', 'Canvas': '#EFE6D2', 'Ember': '#F28C28', 'Tuft': '#5BAF4A', 'FlowerWhite': '#FFFFFF', 'FlowerYellow': '#FFD23F',
    'Metal': '#BFC6CE', 'Bucket': '#4C6A7A',
}

X0, X1, Z0, Z1 = -50, 50, -44, 56


def hash01(i):
    return (math.sin(i * 12.9898) * 43758.5453) % 1.0


def lake_noise(x, z):   # the same in C#: PrototypeSceneBuilder.LakeNoise
    return 0.5 * (math.sin(x * 0.13 + 1.7) * math.cos(z * 0.11 + 0.4) * 0.6 + math.sin(x * 0.29 + z * 0.21 + 2.3) * 0.4)


def lake_height(x, z):
    dx, dz = (x - 6.0) / 34.0, (z - 4.0) / 24.0
    ang = math.atan2(dz, dx)
    wobble = 1.0 + 0.08 * math.sin(ang * 3 + 0.7) + 0.05 * math.sin(ang * 5 + 2.1)
    d = math.hypot(dx, dz) / wobble
    t = max(0.0, min(1.0, (d - 0.74) / (1.22 - 0.74)))
    # a steep drop-off under the water, a shallow shelf at the waterline, then a long soft beach up to the grass
    if t < 0.5:
        u = t / 0.5
        h = -4.2 + (-0.15 + 4.2) * (u * u * (3 - 2 * u))
    else:
        u = (t - 0.5) / 0.5
        h = -0.15 + (1.3 + 0.15) * (u * u * (3 - 2 * u))
    shore = t * t * (3 - 2 * t)
    h += shore * lake_noise(x, z) * 2.4
    if (abs(x) < 3 or abs(x + 12) < 2.5) and z < -12:
        h = min(h, 0.95 if abs(x) < 3 else 0.75)    # the earth lane runs level with the end of the pier / the jetty
    return h


def near(x, z, cx, cz, r):
    return math.hypot(x - cx, z - cz) < r


def dry_and_free(x, z, margin=0.0):
    """Land (not water) and clear of the jetty, the pier, the aqueduct, the islet, the boulder and the camp."""
    if lake_height(x, z) < 0.9:
        return False
    if z < -9 and (abs(x) < 4.5 + margin or abs(x + 12) < 4.0 + margin):
        return False
    if abs(x - 26) < 5.5 + margin and z < 4:
        return False
    if near(x, z, -17, -9, 7 + margin) or near(x, z, 8, 25, 6 + margin) or near(x, z, CAMP[0], CAMP[1], 6 + margin) or near(x, z, 30, 8, 5 + margin):
        return False
    return True


CAMP = (-20.0, 34.0)


def pine(b, x, z, s=1.0, dark=False):
    y0 = lake_height(x, z) - 0.25
    m = 'PineB' if dark else 'PineA'
    b.cone('PineTrunk', x, y0, z, 0.25 * s, 0.17 * s, 1.9 * s, 6)
    for i, (r, h) in enumerate(((2.1, 2.4), (1.7, 2.2), (1.25, 2.0), (0.8, 1.8))):
        b.cone(m, x, y0 + (1.6 + i * 1.3) * s, z, r * s, 0.05, h * s, 8)


def plank_run(b, x, z0, z1, top, width, thick=0.06):
    z = z0
    i = 0
    while z < z1 - 0.1:
        b.box('WoodLight' if i % 3 == 0 else 'Wood', x, top - thick / 2, z + 0.14, width, thick, 0.28)
        z += 0.32
        i += 1


def rock_pillar(b, name_seed, x, z, y0, y1, r0, r1, cap=True):
    """A rough column of rock: two offset frusta, a darker base, and a grassy cap."""
    h = y1 - y0
    b.cone('Rock', x, y0, z, r0, r1, h, 9)
    b.cone('RockDark', x + 0.5, y0, z - 0.3, r0 * 0.8, r1 * 0.8, h * 0.9, 7)
    b.cone('Rock', x - 0.4, y0 + h * 0.4, z + 0.4, r0 * 0.55, r1 * 0.9, h * 0.65, 6)
    if cap:
        b.ico('Grass', x, y1, z, r1 * 1.05, 0.28, sub=2)
        b.ico('GrassLight', x + 0.4, y1 + 0.1, z - 0.3, r1 * 0.6, 0.3, sub=1)


def rowboat(b, x, y, z, yaw=0.0, length=4.2):
    """A wooden rowing boat: pointed ends, a hull with a lip, a bench and an oar."""
    c, s = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))

    def P(dx, dz):
        return x + dx * c + dz * s, z - dx * s + dz * c
    for k in (-1, 1):
        px, pz = P(0, k * length / 2)
        b.box('WoodDark', px, y + 0.15, pz, 1.05, 0.5, 1.05, rot_y=45 + yaw, bevel=0.03)
    px, pz = P(0, 0)
    b.box('WoodDark', px, y + 0.15, pz, 1.5, 0.5, length, bevel=0.04, rot_y=yaw)
    b.box('WoodLight', px, y + 0.3, pz, 1.2, 0.08, length - 0.6, rot_y=yaw)
    bx, bz = P(0, 0.3)
    b.box('Wood', bx, y + 0.42, bz, 1.3, 0.07, 0.35, rot_y=yaw)
    ox, oz = P(0.3, 1.1)
    b.box('Wood', ox, y + 0.55, oz, 0.06, 0.06, 2.4, rot_y=yaw)


def aqueduct(b):
    aq_top, ax, a_end, a_start, half = 12.0, 26.0, -1.0, -44.0, 1.7
    zc = (a_start + a_end) / 2
    b.box('Stone', ax, aq_top - 0.6, zc, half * 2, 1.2, a_end - a_start, bevel=0.04)
    for i in range(0, int(a_end - a_start - 5), 3):                       # the low wall on top, in blocks
        z = a_start + 1.5 + i
        b.box('StoneDark', ax + half - 0.2, aq_top + 0.4, z, 0.4, 0.8, 2.85, bevel=0.03)
    for k in range(5):                                                      # piers at the joints between the bays
        zb = -3.0 - 9.0 * k
        if k == 4:
            b.box('StoneDark', ax, 2.4, -41.5, half * 2, 16.8, 5.0, bevel=0.04)
        else:
            b.box('StoneDark', ax, 2.4, zb, half * 2, 16.8, 3.0, bevel=0.04)
    for k in range(4):                                                      # the arches: a block over each 6 m opening with a round hole
        zcen = -7.5 - 9.0 * k
        spring, rad = 5.4, 3.0
        n = 14
        w = 6.0 / n
        for i in range(n):
            zz = zcen - 3.0 + (i + 0.5) * w
            hh = spring + math.sqrt(max(0.0, rad * rad - (zz - zcen) ** 2))
            top = aq_top - 1.2
            b.box('Stone' if (i + k) % 2 else 'StoneDark', ax, (hh + top) / 2, zz, half * 2, top - hh, w + 0.01)
        b.box('StoneDark', ax, 5.2, zcen, half * 2 + 0.3, 0.3, 6.2)    # the keystone ring's band at the springing line
    for fx, fy, fz, fr, fm in ((24.5, -0.8, 1.5, 1.4, 'StoneDark'), (28.5, -1.0, 3.0, 1.2, 'Stone'), (26.5, -0.3, 0.2, 0.9, 'StoneDark')):
        b.ico(fm, fx, fy, fz, fr, 0.7, sub=1, sc=(1.0, 1.0, 1.3))
    b.spot('Spot_Aqueduct', ax, aq_top, a_end)


def willow_islet(b):
    islet_x, islet_z = -17.0, -9.0
    rock_pillar(b, 41, islet_x, islet_z, -4.0, 0.7, 3.8, 2.9)
    t = [(-17, 0.5, -9.4), (-17.25, 2.8, -9.1), (-17, 5.0, -8.3), (-17, 6.5, -7.1)]
    for (p0, p1, r0, r1, m) in ((t[0], t[1], 0.75, 0.62, 'Bark'), (t[1], t[2], 0.62, 0.5, 'BarkLight'), (t[2], t[3], 0.5, 0.42, 'Bark')):
        b.limb(m, p0, p1, r0, r1, 8)
    b.limb('Bark', (-17, 6.1, -7.2), (-17, 6.62, -1.9), 0.36, 0.26, 7)         # the long branch
    b.limb('BarkLight', (-17, 4.6, -8.2), (-17, 6.2, -4.5), 0.17, 0.13, 6)       # a brace under it
    b.limb('Bark', (-17, 6.4, -4.6), (-16.1, 7.6, -4.2), 0.1, 0.05, 5)
    for k in range(4):
        a = k * 1.6 + 0.4
        b.limb('Bark', (t[0][0], t[0][1] + 0.2, t[0][2]), (t[0][0] + math.cos(a) * 1.5, t[0][1] - 0.5, t[0][2] + math.sin(a) * 1.5), 0.26, 0.1, 6)
    b.limb('Bark', t[3], (-17, 7.9, -7.2), 0.4, 0.25, 6)
    b.box('WoodLight', -17, 6.95, -2.2, 1.2, 0.1, 1.4, bevel=0.02)                 # the plank at the end of the branch
    b.box('WoodDark', -17, 6.85, -2.2, 0.08, 0.1, 1.4)
    crowns = [((-17, 7.7, -7.3), 4.2), ((-18.7, 7.5, -7.6), 2.9), ((-15.4, 7.5, -7.1), 2.9), ((-17, 9.1, -7.4), 3.1), ((-17, 7.3, -9.2), 2.8),
              ((-17, 7.1, -5.5), 2.1), ((-18.2, 8.7, -8.4), 2.4), ((-15.8, 8.6, -8.2), 2.4)]
    for i, (p, size) in enumerate(crowns):
        b.ico('WillowDark' if i == 0 or i % 2 else 'Willow', p[0], p[1], p[2], size / 2, 0.85, sub=2)
    # hanging willow strands on the long branch
    for i in range(7):
        zz = -7.0 + i * 0.7
        b.limb('Willow', (-17 + 0.3 * math.sin(i), 6.2, zz), (-17 + 0.3 * math.sin(i), 4.6 - 0.1 * i, zz + 0.1), 0.1, 0.02, 4)
    b.spot('Spot_Branch', -17, 7, -1.6)


def landfall(x, top, z_from):
    """Where a deck at height `top` comes ashore along x: walking inland (towards -z) from z_from, the first z where the ground is within
    0.12 of the deck; the deck is run half a metre further in so its end lies on the ground and never hangs over the water."""
    z = z_from
    while z > -45 and lake_height(x, z) < top - 0.12:
        z -= 0.1
    return z - 0.5


def jetty_and_tower(b):
    # the jetty: boards, piles, a life ring, a moored boat and a few fishing things
    jx, jtop, jend = -12.0, 0.8, -7.0
    js = landfall(jx, jtop, jend)
    plank_run(b, jx, js, jend, jtop, 1.8)
    for z in range(int(js) + 1, -7, 3):
        for dx in (-0.8, 0.8):
            b.limb('WoodDark', (jx + dx, min(-4.0, lake_height(jx + dx, z) - 0.2), z), (jx + dx, jtop - 0.1, z), 0.11, 0.1, 6)
    for dx in (-0.9, 0.9):
        b.box('WoodDark', jx + dx, jtop - 0.12, (js + jend) / 2, 0.12, 0.14, jend - js)   # the stringers
    for z in (-17.5, -13.0, -8.0):
        b.limb('WoodDark', (jx - 0.9, jtop, z), (jx - 0.9, jtop + 0.6, z), 0.07, 0.06, 5)
    b.cone('LifeRing', jx - 0.95, 0.35, -10.0, 0.26, 0.26, 0.14, 12)
    b.cone('Paint_White', jx - 0.95, 0.355, -10.0, 0.265, 0.265, 0.03, 12)
    b.box('Bucket', jx + 0.55, jtop + 0.15, -9.0, 0.3, 0.3, 0.3)                                # a bucket and a fishing rod
    b.limb('WoodLight', (jx + 0.5, jtop + 0.1, -9.6), (jx + 0.2, jtop + 1.3, -7.6), 0.03, 0.015, 4)
    rowboat(b, jx + 1.9, 0.05, -12.5, yaw=6, length=2.8)
    b.spot('Spot_Jetty', jx, jtop, jend)

    # the long pier and the tower
    pier_top, back, front, top = 1.0, -4.2, -1.0, 9.6
    ps = landfall(0.0, pier_top, back)
    plank_run(b, 0.0, ps, back, pier_top, 2.4)
    for z in range(int(ps) + 1, int(back), 3):
        for dx in (-1.1, 1.1):
            b.limb('WoodDark', (dx, min(-4.0, lake_height(dx, z) - 0.2), z), (dx, pier_top - 0.1, z), 0.12, 0.11, 6)
    for dx in (-1.15, 1.15):
        b.box('WoodDark', dx, pier_top - 0.12, (ps + back) / 2, 0.14, 0.16, back - ps)
    for x in (-1.5, 1.5):
        for z in (back, front):
            b.box('WoodDark', x, (-4.0 + top) / 2, z, 0.24, top + 4.0, 0.24, bevel=0.015)
    span = front - back
    y = pier_top
    while y < top - 2.6:
        for x in (-1.53, 1.53):
            b.limb('Wood', (x, y, back), (x, y + 2.7, front), 0.06, 0.06, 4)
            b.limb('Wood', (x, y, front), (x, y + 2.7, back), 0.06, 0.06, 4)
        for x in (-1.5, 1.5):
            b.box('WoodDark', x, y + 2.7, (back + front) / 2, 0.16, 0.14, span)
        y += 2.7
    for z in (back, front):                                                  # the 5 m arm on slanted struts
        b.limb('WoodDark', (1.5, 1.5, z), (3.7, 4.85, z), 0.1, 0.09, 4)
    for dx, h, edge_z, ropes in ((3.0, 5.0, 0.2, (4.45,)), (0.0, 9.0, 0.5, (-1.45, 1.45))):
        b.box('Wood', dx, h - 0.11, (edge_z + back) / 2, 3.0, 0.22, edge_z - back, bevel=0.02)
        b.box('Paint_White', dx, h + 0.005, edge_z - 0.08, 3.0, 0.012, 0.16)
        rail_end = edge_z - 1.5
        for x in ropes:
            b.box('Rope', x, h + 0.95, (rail_end + back) / 2, 0.03, 0.03, rail_end - back)
            b.box('Rope', x, h + 0.5, (rail_end + back) / 2, 0.025, 0.025, rail_end - back)
            for pz in (back + 0.1, (back + rail_end) / 2, rail_end):
                b.box('WoodDark', x, h + 0.5, pz, 0.08, 1.0, 0.08)
        b.spot('Spot_TowerLower' if h < 6 else 'Spot_TowerTop', dx, h, edge_z)
    for x in (-0.3, 0.3):
        b.box('WoodDark', x, (pier_top + top) / 2, back - 0.14, 0.06, top - pier_top, 0.06)
    y = pier_top + 0.35
    while y < top:
        b.box('Wood', 0, y, back - 0.14, 0.6, 0.04, 0.05)
        y += 0.36


def shore_life(b):
    # reeds and lily pads
    clumps = 0
    for i in range(400):
        x, z = X0 + 4 + (X1 - X0 - 8) * hash01(i * 3.7 + 1), Z0 + 4 + (Z1 - Z0 - 8) * hash01(i * 5.1 + 2)
        h = lake_height(x, z)
        if h < -0.5 or h > 0.1 or near(x, z, -17, -9, 6):
            continue
        if clumps >= 14:
            break
        clumps += 1
        for k in range(9):
            px, pz = x + (hash01(i * 7 + k) - 0.5) * 2.6, z + (hash01(i * 11 + k) - 0.5) * 2.6
            gy = min(0.0, lake_height(px, pz))
            rh = 0.9 + 0.8 * hash01(i * 13 + k)
            b.cone('Reed', px, gy, pz, 0.05, 0.015, rh, 4)
        for k in range(5):
            px, pz = x + (hash01(i * 17 + k) - 0.5) * 6, z + (hash01(i * 19 + k) - 0.5) * 6
            if lake_height(px, pz) > -0.3:
                continue
            b.cone('LilyPad', px, 0.0, pz, 0.38, 0.38, 0.03, 7)
            if k % 2 == 0:
                b.ico('Lily', px, 0.1, pz, 0.12, 0.8, sub=0)


def camp(b):
    cx, cz = CAMP
    gy = lake_height(cx, cz)
    b.limb('Canvas', (cx - 1.6, gy - 0.05, cz), (cx, gy + 1.9, cz), 0.05, 0.05, 4)          # a tent: two canvas slopes on a ridge
    for sgn, m in ((-1, 'Tent'), (1, 'TentDark')):
        for i in range(6):
            t = i / 5
            b.box(m, cx + sgn * (1.5 * (1 - t) + 0.05), gy + 0.1 + 1.85 * t, cz, 1.5 * (1 - t) + 0.2, 0.16, 2.4)
    b.box('Canvas', cx, gy + 0.9, cz - 1.25, 0.2, 1.7, 0.05)
    fx, fz = cx + 4.0, cz - 1.5                                                             # a fire ring with logs around it
    for i in range(8):
        a = i * 0.785
        b.ico('Rock', fx + math.cos(a) * 0.7, lake_height(fx, fz) + 0.1, fz + math.sin(a) * 0.7, 0.22, 0.8, sub=0)
    b.cone('Ember', fx, lake_height(fx, fz), fz, 0.35, 0.05, 0.5, 5)
    for a in (0.3, 2.4, 4.4):
        b.box('Wood', fx + math.cos(a) * 1.9, lake_height(fx, fz) + 0.22, fz + math.sin(a) * 1.9, 1.7, 0.4, 0.4, rot_y=math.degrees(-a) + 90, bevel=0.03)


def build():
    b = locbuild.Builder()
    for k, v in COLOURS.items():
        b.mat(k, v)

    # --- the ground: soil block with a rolling, jittered, triangulated surface
    b.box('Soil', 0, -5.15, 6, 100, 1.7, 100)
    for i in range(0, 100, 2):                                  # soil walls on the four sides up to the surface
        for (x, z, sx, sz) in ((X0 + 0.5, Z0 + i + 1, 1.0, 2.0), (X1 - 0.5, Z0 + i + 1, 1.0, 2.0), (X0 + i + 1, Z0 + 0.5, 2.0, 1.0), (X0 + i + 1, Z1 - 0.5, 2.0, 1.0)):
            h = lake_height(x, z)
            b.box('Soil', x, (-4.3 + h) / 2 - 0.05, z, sx, h + 4.3 + 0.1, sz)

    def level(x, y, z):
        return y + 0.14 * noise.noise(Vector((x * 0.33, z * 0.33, 9.1)))

    def mat(cx, cz, yy):
        # the beach in soft steps (mud, wet sand, sand, dry sand, dry grass, grass); yy is the height with a little noise, and the triangles are
        # cut along these very contours (see splits below), so the borders are smooth wandering lines
        if cz < -12 and yy > 0.45:   # the approach to the jetty and the pier: a narrow earth path with grassy verges, not a sand road
            for lane in (0.0, -12.0):
                if abs(cx - lane) < 1.5:
                    return 'Dirt'
            return 'GrassDry' if yy < 0.8 else 'Grass'
        if yy < -0.6:
            return 'LakeMud'
        if yy < -0.1:
            return 'WetSand'
        if yy < 0.5:
            return 'LakeSand'
        if yy < 0.9:
            return 'SandDry'
        if yy < 1.3:
            return 'GrassDry'
        slope = abs(lake_height(cx + 1, cz) - lake_height(cx - 1, cz)) + abs(lake_height(cx, cz + 1) - lake_height(cx, cz - 1))
        n = noise.noise(Vector((cx * 0.1, cz * 0.1, 3.3)))
        if slope > 2.4:
            return 'Moss'
        return 'GrassLight' if n > 0.2 else 'GrassDark' if n < -0.25 else 'Grass'

    def jitter(x, z):
        edge = min(abs(x - X0), abs(x - X1), abs(z - Z0), abs(z - Z1)) < 1e-3
        h = lake_height(x, z)
        if edge or h < 0.6 or (z < -10 and (abs(x) < 6 or abs(x + 12) < 5)):
            return 0.0, 0.0
        if h < 1.8:
            return 0.0, 0.0     # a regular grid next to the water, so the beach line stays smooth
        amp = 0.5
        return (hash01(x * 31.7 + z * 7.3) - 0.5) * amp, (hash01(x * 12.9 + z * 53.1) - 0.5) * amp
    splits = [(level, [-0.6, -0.1, 0.45, 0.5, 0.8, 0.9, 1.3]),
              (lambda x, y, z: x, [-13.5, -10.5, -1.5, 1.5]),
              (lambda x, y, z: z, [-12.0])]
    b.terrain(X0, X1, Z0, Z1, 100.0 / 140.0, lake_height, mat, jitter=jitter, splits=splits)
    jetty_and_tower(b)
    willow_islet(b)
    aqueduct(b)
    rowboat(b, 30, 0.05, 8, yaw=90)
    b.spot('Spot_Rowboat', 28.1, 0.35, 8)
    rock_pillar(b, 77, 8, 25, -4.0, 6.0, 3.8, 2.5)
    b.spot('Spot_Boulder', 8, 6, 22.7)
    shore_life(b)
    camp(b)

    # --- shore decor: pines in groves, boulders, tufts, flowers, logs
    n = 0
    GROVES = [(-34, 36, 11, 16), (-8, 46, 9, 12), (20, 44, 9, 13), (40, 30, 9, 11), (44, 4, 8, 8), (40, -20, 8, 8), (-42, 14, 8, 8), (-42, -22, 8, 8),
              (-30, -34, 8, 7), (12, -36, 8, 7), (44, -38, 7, 5), (-6, 36, 4, 3)]
    for cx, cz, rad, cnt in GROVES:
        for i in range(cnt):
            a, r = hash01(n * 1.7 + 3) * 6.283, rad * math.sqrt(hash01(n * 2.9 + 4))
            x, z = cx + math.cos(a) * r, cz + math.sin(a) * r
            n += 1
            if abs(x) > 47 or z < -41 or z > 53 or not dry_and_free(x, z, 0.5):
                continue
            pine(b, x, z, 0.8 + 0.65 * hash01(n * 3.1 + 7), dark=hash01(n * 5.3) > 0.5)
    for i in range(60):   # lone small pines and young trees between the groves
        x, z = X0 + 3 + 94 * hash01(i * 4.3 + 40), Z0 + 3 + 98 * hash01(i * 6.1 + 41)
        if dry_and_free(x, z, 1.0):
            pine(b, x, z, 0.55 + 0.4 * hash01(i + 90), dark=hash01(i + 5) > 0.5)
    for i in range(24):
        x, z = X0 + 3 + 94 * hash01(i * 8.7 + 50), Z0 + 3 + 98 * hash01(i * 3.9 + 51)
        h = lake_height(x, z)
        if h < 0.2 or h > 1.4 or not dry_and_free(x, z, 0.0) and h > 0.9:
            continue
        r = 0.6 + 1.3 * hash01(i + 60)
        m = 'Rock' if hash01(i + 61) > 0.4 else 'RockDark'
        b.ico(m, x, h + r * 0.2, z, r, 0.6, sub=1, sc=(1 + 0.4 * hash01(i + 62), 1.0, 1 + 0.4 * hash01(i + 63)))
    for i in range(320):
        x, z = X0 + 2 + 96 * hash01(i * 2.3 + 70), Z0 + 2 + 100 * hash01(i * 4.1 + 71)
        h = lake_height(x, z)
        if h < 0.9 or not dry_and_free(x, z, 0.0):
            continue
        for j in range(3):
            a = j * 2.1 + hash01(i) * 3
            b.cone('Tuft', x + math.cos(a) * 0.12, h - 0.02, z + math.sin(a) * 0.12, 0.11, 0.015, 0.4 + 0.3 * hash01(i + j), 4)
    for i in range(14):
        fx, fz = X0 + 4 + 92 * hash01(i * 5.9 + 80), Z0 + 4 + 96 * hash01(i * 7.7 + 81)
        if not dry_and_free(fx, fz, 1.0):
            continue
        for j in range(10):
            a, rr = j * 2.4, 0.4 + 1.8 * hash01(i * 10 + j)
            px, pz = fx + math.cos(a) * rr, fz + math.sin(a) * rr
            b.ico(('FlowerWhite', 'FlowerYellow', 'Lily')[j % 3], px, lake_height(px, pz) + 0.14, pz, 0.12, 1.0, sub=0)
    for i in range(7):
        x, z = X0 + 4 + 92 * hash01(i * 3.3 + 95), Z0 + 4 + 96 * hash01(i * 8.1 + 96)
        if dry_and_free(x, z, 2.0):
            b.box('Bark', x, lake_height(x, z) + 0.2, z, 2.6 + hash01(i), 0.45, 0.45, bevel=0.04, rot_y=360 * hash01(i + 5))
    return b


def main():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    b = build()
    objs = b.finish('Loc_Lake')
    print('LOCATION TRIS', b.tris, 'objects', len(objs))
    b.export(OUT)
    view.shoot('loc_lake_overview', (4, 0, 6), 135, (-0.5, 0.62, -1), lens=50, res=(1200, 800))
    view.shoot('loc_lake_tower', (0, 3, -4), 36, (0.9, 0.35, 0.5), lens=50, res=(1100, 700))
    view.shoot('loc_lake_aqueduct', (26, 6, -22), 55, (-0.9, 0.25, 0.4), lens=50, res=(1100, 700))
    view.shoot('loc_lake_islet', (-17, 4, -6), 30, (0.7, 0.35, 0.8), lens=50, res=(1100, 700))
    view.shoot('loc_lake_pier', (0, 0.8, -21), 16, (-0.6, 0.5, -1), lens=50, res=(1100, 700))
    view.shoot('loc_lake_pier_side', (-6, 0.8, -22), 22, (1, 0.25, -0.2), lens=50, res=(1100, 700))
    print('LOC DONE')


if __name__ == '__main__':
    main()
