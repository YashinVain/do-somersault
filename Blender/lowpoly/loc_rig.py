"""Location 8, the offshore platform at sunset, as one scene (Docs/ModelBrief.md section 3.8). Output: Models/Locations/Loc_Rig.fbx + previews.
    blender -b --python Blender/lowpoly/loc_rig.py
Site space like the game's: the block is 90 x 90 m, x -45..45, z -30..60, water at y = 0 (not modelled), sea floor at -6. Legs, main deck, quarters, derrick,
boat landing, walkway, helideck, crane, supply vessel and navigation tower stand exactly where the game's solid boxes are; the flame on the flare stack is the
game's (animated), everything else is here. Materials Lit / RedLight glow in the game.
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

OUT = os.path.normpath(os.path.join(HERE, '..', '..', 'Assets', '_Project', 'Models', 'Locations', 'Loc_Rig.fbx'))

COLOURS = {
    'Soil': '#3E3A40', 'Earth': '#4E4549', 'BlockRock': '#403C42', 'Seabed': '#5C6A6E', 'SeabedDark': '#4C585C', 'Pipeline': '#2E3236',
    'Steel': '#B4BAC2', 'SteelDark': '#7B838C', 'SteelLight': '#D6DADF', 'Yellow': '#E0A92B', 'YellowDark': '#B98A1E', 'White': '#EEEBE3', 'WhiteShade': '#CFCBC1',
    'Red': '#C2412F', 'RedDark': '#8E2F23', 'Orange': '#E8742B', 'Lit': '#FFC46A', 'RedLight': '#FF5A4A', 'GreenLight': '#5AE08A', 'Glass': '#2A3C4E',
    'Deck': '#868E97', 'Grating': '#9AA1A9', 'Black': '#1A1D21', 'Rubber': '#222428', 'Concrete': '#B4B6B7', 'ConcreteDark': '#8E9091', 'Rope': '#C9B88A',
    'Rust': '#8A4B2A', 'Growth': '#3F5A3A', 'Barnacle': '#B8B5A5', 'Green': '#3E7A4A', 'Blue': '#2F5B94', 'Sign': '#F2F0EA', 'Cable': '#2B2E33',
}

HALF = 45.0


def hash01(i):
    return (math.sin(i * 12.9898) * 43758.5453) % 1.0


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


LEG_X = (-14.0, 14.0)
LEG_Z = (-9.0, -27.0)
DECK_Y = 13.0                 # top of the main deck
DECK_X0, DECK_X1, DECK_Z0, DECK_Z1 = -19.0, 19.0, -32.0, -4.0


# ---------------------------------------------------------------- small things
def pipe(b, p0, p1, r=0.15, mat='Orange', sides=8):
    b.limb(mat, p0, p1, r, r, sides)


def flange(b, p, axis, r=0.2, mat='SteelDark'):
    x, y, z = p
    if axis == 'x':
        b.limb(mat, (x - 0.05, y, z), (x + 0.05, y, z), r * 1.5, r * 1.5, 8)
    elif axis == 'y':
        b.limb(mat, (x, y - 0.05, z), (x, y + 0.05, z), r * 1.5, r * 1.5, 8)
    else:
        b.limb(mat, (x, y, z - 0.05), (x, y, z + 0.05), r * 1.5, r * 1.5, 8)


def rail(b, pts, y, h=1.1, mat='Yellow', skip=None, post_gap=1.8):
    """A handrail with posts and a mid rail along a polyline of (x, z) points at height y. skip(x, z) -> True leaves a gap."""
    for (x0, z0), (x1, z1) in zip(pts, pts[1:]):
        ln = math.hypot(x1 - x0, z1 - z0)
        n = max(1, int(ln / post_gap))
        for i in range(n + 1):
            t = i / n
            x, z = x0 + (x1 - x0) * t, z0 + (z1 - z0) * t
            if skip and skip(x, z):
                continue
            b.limb(mat, (x, y, z), (x, y + h, z), 0.04, 0.04, 4)
        for hy in (h, h * 0.5):
            for i in range(n):
                ta, tb = i / n, (i + 1) / n
                xa, za = x0 + (x1 - x0) * ta, z0 + (z1 - z0) * ta
                xb, zb = x0 + (x1 - x0) * tb, z0 + (z1 - z0) * tb
                if skip and (skip(xa, za) or skip(xb, zb)):
                    continue
                b.limb(mat, (xa, y + hy, za), (xb, y + hy, zb), 0.025, 0.025, 4)


def ladder(b, x, y0, y1, z, face='z', cage=True):
    """A steel ladder with an optional safety cage (a ladder going up along a wall that faces +z or -z)."""
    s = 1.0 if face == 'z' else -1.0
    for dx in (-0.25, 0.25):
        b.limb('Yellow', (x + dx, y0, z), (x + dx, y1, z), 0.03, 0.03, 4)
    y = y0 + 0.25
    while y < y1:
        b.limb('SteelLight', (x - 0.25, y, z), (x + 0.25, y, z), 0.02, 0.02, 3)
        y += 0.3
    if cage:
        y = y0 + 2.2
        while y < y1:
            for a in range(7):
                a0, a1 = a * 0.45 - 1.35, (a + 1) * 0.45 - 1.35
                b.limb('Yellow', (x + math.sin(a0) * 0.5, y, z + s * (0.15 + math.cos(a0) * 0.5)), (x + math.sin(a1) * 0.5, y, z + s * (0.15 + math.cos(a1) * 0.5)), 0.02, 0.02, 3)
            y += 0.9
        for a in (-0.9, 0.0, 0.9):
            b.limb('Yellow', (x + math.sin(a) * 0.5, y0 + 2.2, z + s * (0.15 + math.cos(a) * 0.5)), (x + math.sin(a) * 0.5, y1, z + s * (0.15 + math.cos(a) * 0.5)), 0.02, 0.02, 3)


def hazard(b, x, y, z, w, d, along='x'):
    """A hazard-striped edge strip (black and yellow) with a white line behind it: the place to jump from, easy to see at sunset."""
    n = max(2, int(w / 0.3) if along == 'x' else int(d / 0.3))
    for i in range(n):
        if along == 'x':
            b.box('Yellow' if i % 2 else 'Black', x - w / 2 + (i + 0.5) * w / n, y + 0.012, z, w / n, 0.024, d)
        else:
            b.box('Yellow' if i % 2 else 'Black', x, y + 0.012, z - d / 2 + (i + 0.5) * d / n, w, 0.024, d / n)


def light_post(b, x, y, z, h=2.2):
    b.limb('SteelDark', (x, y, z), (x, y + h, z), 0.05, 0.04, 5)
    b.box('Steel', x, y + h + 0.05, z, 0.34, 0.1, 0.22)
    b.box('Lit', x, y + h - 0.02, z, 0.28, 0.08, 0.18)


def drum_stack(b, x, y, z, n=3, mat='Blue'):
    for k in range(n):
        b.cone(mat if k % 2 else 'Red', x + (k % 2) * 0.62, y, z + (k // 2) * 0.62, 0.28, 0.28, 0.85, 8)
        b.cone('Steel', x + (k % 2) * 0.62, y + 0.25, z + (k // 2) * 0.62, 0.285, 0.285, 0.04, 8)


def container(b, x, y, z, col, sx=2.44, sz=6.1):
    b.box(col, x, y + 1.3, z, sx, 2.6, sz, bevel=0.03)
    for q in range(int(sz / 0.5)):
        b.box('SteelDark', x - sx / 2 - 0.02, y + 1.3, z - sz / 2 + 0.3 + q * 0.5, 0.06, 2.3, 0.2)


# ---------------------------------------------------------------- hulls (copied from the port: ribs, sheer, bands)
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


# ---------------------------------------------------------------- the block and the sea floor
def block_base(b):
    top = -6.1
    b.box('Soil', 0, top - 0.7, 15, 90, 1.4, 90)
    b.box('Earth', 0, (top - 1.4 - 9.0) / 2, 15, 90, (top - 1.4) - (-9.0), 90)
    b.box('BlockRock', 0, (-9.0 - 16.0) / 2, 15, 90, 7.0, 90)
    b.box('Seabed', 0, -6.05, 15, 90, 0.1, 90)
    for k in range(30):
        x, z = -42 + 84 * hash01(k * 3.7 + 1), -2 + 58 * hash01(k * 6.1 + 2)
        b.ico('SeabedDark', x, -6.0, z, 1.0, 1.0, sub=1, sc=(2.0 + 3 * hash01(k + 9), 0.07, 0.7 + 0.5 * hash01(k + 4)), jit=0.1, seed=k)
    # subsea pipelines and cables running out from the legs across the sea floor, and the anchor blocks of the legs
    for lx in LEG_X:
        b.limb('Pipeline', (lx, -5.6, LEG_Z[0] + 1.2), (lx + (12 if lx > 0 else -12), -5.8, 25.0), 0.35, 0.35, 8)
        b.limb('Cable', (lx + 1.0, -5.9, LEG_Z[0] + 1.0), (lx + (3 if lx > 0 else -3) + 8, -5.95, 40.0), 0.09, 0.09, 5)
        for k in range(6):
            t = k / 5
            b.box('Concrete', lx + (12 if lx > 0 else -12) * t, -5.9, LEG_Z[0] + 1.2 + (25.0 - LEG_Z[0] - 1.2) * t, 0.8, 0.3, 1.2)
    for lx in LEG_X:
        for lz in LEG_Z:
            b.box('Concrete', lx, -5.5, lz, 5.4, 1.0, 5.4, bevel=0.1)
            for k in range(4):
                a = k * 1.5708 + 0.78
                b.ico('ConcreteDark', lx + math.cos(a) * 3.0, -5.7, lz + math.sin(a) * 3.0, 0.7, 0.6, sub=1, jit=0.15, seed=k + int(lx))
    for k in range(18):                                          # junk and rocks on the sea floor
        x, z = -40 + 80 * hash01(k * 2.9 + 11), -4 + 60 * hash01(k * 5.3 + 12)
        if k % 3 == 0:
            b.cone('Rubber', x, -6.0, z, 0.45, 0.45, 0.3, 8)
        else:
            b.ico('SeabedDark', x, -6.0, z, 0.5 + 0.5 * hash01(k + 3), 0.6, sub=1, jit=0.16, seed=k + 30)


# ---------------------------------------------------------------- the four legs of the jacket and its bracing
def legs(b):
    for lx in LEG_X:
        for lz in LEG_Z:
            b.cone('Steel', lx, -6.0, lz, 1.62, 1.3, 19.0, 12)                      # the leg itself from the sea floor to under the deck
            b.cone('Growth', lx, -2.5, lz, 1.64, 1.58, 3.4, 12)                       # marine growth around the waterline
            for k in range(14):
                a = k * 0.45
                b.ico('Barnacle', lx + math.cos(a) * 1.55, -0.3 + 0.8 * hash01(k), lz + math.sin(a) * 1.55, 0.18, 0.5, sub=0)
            for y, r in ((0.3, 1.8), (8.0, 1.7), (12.0, 1.62)):                         # yellow bands and welded collars
                b.cone('Yellow', lx, y - 0.25, lz, r, r, 0.5, 12)
            for y in (4.0, 6.5, 10.0):
                b.cone('SteelDark', lx, y, lz, 1.4, 1.4, 0.16, 12)
            b.cone('SteelDark', lx, 12.4, lz, 1.9, 1.7, 0.6, 12)                       # the leg's cap under the deck
            for a in (0.7, 2.4, 3.9, 5.4):                                              # anodes and a riser pipe at the sides
                b.box('Rust', lx + math.cos(a) * 1.5, 1.0 + 3 * hash01(a), lz + math.sin(a) * 1.5, 0.18, 0.5, 0.18)
            b.limb('SteelDark', (lx + (1.75 if lx > 0 else -1.75), -5.0, lz), (lx + (1.75 if lx > 0 else -1.75), 12.4, lz), 0.12, 0.12, 6)
            for y in (1.0, 5.0, 9.0):
                b.box('SteelDark', lx + (1.62 if lx > 0 else -1.62), y, lz, 0.25, 0.14, 0.4)
            # rubber boat fenders on the splash zone (vertical, on the sea-facing sides)
    for lx in LEG_X:                                                                      # X bracing in the side planes (seen from the sea), strong pipes
        for (z0, z1) in ((LEG_Z[0], LEG_Z[1]),):
            pipe(b, (lx, -3.0, z0), (lx, 11.5, z1), 0.42, 'SteelDark', 10)
            pipe(b, (lx, -3.0, z1), (lx, 11.5, z0), 0.42, 'SteelDark', 10)
            pipe(b, (lx, 5.0, z0), (lx, 5.0, z1), 0.42, 'SteelDark', 10)
            pipe(b, (lx, -2.5, z0), (lx, -2.5, z1), 0.34, 'SteelDark', 10)
            for k in range(4):
                flange(b, (lx, -2.0 + k * 3.8, z0 + (z1 - z0) * (-2.0 + k * 3.8 + 3.0) / 14.5), 'x', 0.3)
    for lz in LEG_Z:
        pipe(b, (LEG_X[0], 5.0, lz), (LEG_X[1], 5.0, lz), 0.42, 'SteelDark', 10)
        pipe(b, (LEG_X[0], -3.0, lz), (LEG_X[1], 11.5, lz), 0.3, 'SteelDark', 8)
        pipe(b, (LEG_X[1], -3.0, lz), (LEG_X[0], 11.5, lz), 0.3, 'SteelDark', 8)
        pipe(b, (LEG_X[0], -2.5, lz), (LEG_X[1], -2.5, lz), 0.34, 'SteelDark', 10)
    # a leg ladder with a cage from the landing to the walkway, then to the main deck, on the near leg
    ladder(b, -9.6, 2.6, 8.0, -8.2, 'z')
    ladder(b, -9.6, 8.0, DECK_Y - 0.1, -8.2, 'z')


# ---------------------------------------------------------------- the main deck (collision: 38 x 0.6 x 28 at y 12.7, top 13.0) with its underside
def main_deck(b):
    b.box('Deck', 0, 12.7, -18.0, 38.0, 0.6, 28.0)
    # steel plate in panels with welds, grating strips and painted lanes
    for x in range(-18, 19, 3):
        b.box('SteelDark', x, DECK_Y + 0.004, -18.0, 0.04, 0.01, 27.6)
    for z in range(-31, -3, 3):
        b.box('SteelDark', 0, DECK_Y + 0.004, z, 37.6, 0.01, 0.04)
    for k in range(6):
        b.box('Grating', -15.0 + 6 * k, DECK_Y + 0.012, -8.0, 2.4, 0.02, 3.0)
        for q in range(6):
            b.box('SteelLight', -15.0 + 6 * k - 1.0 + q * 0.4, DECK_Y + 0.024, -8.0, 0.03, 0.01, 3.0)
    b.box('Yellow', 0, 12.25, -4.05, 38.0, 0.6, 0.14)                                       # the yellow skirt on the sea edge
    b.box('White', 4.0, DECK_Y + 0.006, -4.12, 6.0, 0.012, 0.2)                              # the paint where the dive starts
    hazard(b, 4.0, DECK_Y, -4.45, 6.0, 0.4)
    b.box('Yellow', 0, DECK_Y + 0.008, -4.9, 37.0, 0.014, 0.1)
    for x in (-8.0, 12.0):
        b.box('Yellow', x, DECK_Y + 0.008, -16.0, 0.14, 0.014, 22.0)
    # handrails: along the sea edge with an opening at the diving place, round the sides and the back
    rail(b, [(DECK_X0 + 0.2, -4.2), (0.5, -4.2)], DECK_Y)
    rail(b, [(7.5, -4.2), (DECK_X1 - 0.2, -4.2)], DECK_Y)
    rail(b, [(DECK_X0 + 0.2, -4.2), (DECK_X0 + 0.2, DECK_Z0 + 0.2), (DECK_X1 - 0.2, DECK_Z0 + 0.2), (DECK_X1 - 0.2, -4.2)], DECK_Y)
    for x in (0.5, 7.5):                                                                      # chains across the gap ends and end posts
        b.limb('Yellow', (x, DECK_Y, -4.2), (x, DECK_Y + 1.2, -4.2), 0.06, 0.06, 5)
        b.ico('Red', x, DECK_Y + 1.25, -4.2, 0.08, 1.0, sub=0)
    # underside: transverse beams, longitudinal girders, a cable tray, pipe runs
    for x in range(-18, 19, 3):
        b.box('SteelDark', x, 11.85, -18.0, 0.34, 0.9, 27.6)
        b.box('SteelDark', x, 11.35, -18.0, 0.5, 0.1, 27.6)
    for z in (-8.0, -14.0, -20.0, -26.0):
        b.box('Steel', 0, 11.7, z, 37.0, 0.7, 0.3)
    b.box('Cable', 6.0, 11.0, -6.0, 18.0, 0.1, 0.6)
    pipe(b, (-16.0, 10.8, -12.0), (16.0, 10.8, -12.0), 0.2, 'Orange', 8)
    pipe(b, (-16.0, 10.8, -13.0), (16.0, 10.8, -13.0), 0.14, 'Red', 8)
    for x in range(-15, 16, 5):
        b.box('SteelDark', x, 11.0, -12.5, 0.1, 0.4, 1.6)
    b.cone('Orange', -16.5, 8.6, -12.0, 0.45, 0.45, 2.2, 8)                                    # a vertical pipe riser and a valve
    flange(b, (-16.5, 10.2, -12.0), 'y', 0.45)
    # life rafts in orange capsules, a fire-fighting monitor, lamp posts round the deck, an eye wash station
    for sx in (-1, 1):
        b.box('Orange', sx * 17.8, DECK_Y + 0.5, -6.5, 1.0, 0.9, 1.6, bevel=0.2)
        b.box('SteelDark', sx * 17.8, DECK_Y + 0.05, -6.5, 1.1, 0.1, 1.8)
    for x, z in ((-17.0, -30.5), (17.0, -30.5), (-17.0, -5.5), (17.0, -5.5), (0.0, -30.5)):
        light_post(b, x, DECK_Y, z, 3.0)
    b.limb('Red', (-6.0, DECK_Y, -5.5), (-6.0, DECK_Y + 1.3, -5.5), 0.12, 0.1, 6)
    b.limb('Red', (-6.0, DECK_Y + 1.3, -5.5), (-5.5, DECK_Y + 1.5, -5.0), 0.07, 0.06, 5)
    # "RIG 7" painted in big blocks on the yellow skirt (only boxes), flags on a mast at the corner
    for q, (x, w) in enumerate(((-13.0, 0.6), (-12.0, 0.6), (-11.0, 0.9), (-9.5, 0.6), (-8.5, 0.6))):
        b.box('Sign', x, 12.25, -4.14, w, 0.4, 0.03)
    b.limb('SteelDark', (-18.0, DECK_Y, -30.0), (-18.0, DECK_Y + 6.0, -30.0), 0.07, 0.05, 5)
    b.tri('Red', (-18.0, DECK_Y + 6.0, -30.0), (-18.0, DECK_Y + 5.4, -30.0 + 1.0), (-18.0, DECK_Y + 4.8, -30.0))
    b.tri('White', (-18.0, DECK_Y + 5.4, -30.0), (-18.0, DECK_Y + 4.8, -30.0 + 1.0), (-18.0, DECK_Y + 4.2, -30.0))
    # drums, pipes on racks, containers, a forklift, a lay-down area
    drum_stack(b, 15.0, DECK_Y, -9.0, 3)
    drum_stack(b, 16.0, DECK_Y, -7.0, 3, 'Orange')
    container(b, -16.0, DECK_Y, -22.0, 'Blue', 2.44, 6.1)
    container(b, -16.0, DECK_Y + 2.6, -22.0, 'Red', 2.44, 6.1)
    container(b, -16.0, DECK_Y, -28.0, 'Green' if 'Green' in COLOURS else 'Blue', 2.44, 6.1)
    for r in range(3):
        for q in range(4):
            pipe(b, (-14.0, DECK_Y + 0.5 + q * 0.28, -11.0 - r * 0.0 + q * 0.0), (-9.0, DECK_Y + 0.5 + q * 0.28, -11.0), 0.13, 'SteelLight' if q % 2 else 'Steel', 8)
        break
    for dx in (-14.0, -9.0):
        b.box('SteelDark', dx, DECK_Y + 0.25, -11.0, 0.2, 0.5, 0.9)
    b.box('Yellow', -3.0, DECK_Y + 0.6, -9.0, 1.2, 1.0, 2.0, bevel=0.1)                     # a forklift
    b.box('Black', -3.0, DECK_Y + 1.55, -9.0, 1.0, 0.06, 1.0)
    for dx in (-0.45, 0.45):
        b.limb('Black', (-3.0 + dx, DECK_Y + 1.0, -9.5), (-3.0 + dx, DECK_Y + 1.55, -9.5), 0.03, 0.03, 4)
        b.box('Steel', -3.0 + dx * 0.8, DECK_Y + 0.4, -7.8, 0.12, 0.08, 1.2)
    for dx in (-0.55, 0.55):
        for dz in (-9.5, -8.4):
            b.cone('Rubber', -3.0 + dx, DECK_Y, dz, 0.3, 0.3, 0.26, 8)
    b.box('SteelDark', -3.0, DECK_Y + 1.0, -7.2, 0.8, 1.4, 0.1)


# ---------------------------------------------------------------- the quarters block (collision: 12 x 9 x 8 at (9, 17.6, -26)) and the tanks
def quarters(b):
    qx, qz = 9.0, -26.0
    b.box('White', qx, 17.6, qz, 12.0, 9.0, 8.0, bevel=0.05)
    b.box('WhiteShade', qx, 13.3, qz, 12.2, 0.5, 8.2)
    for y in (15.7, 18.1, 20.5):
        b.box('WhiteShade', qx, y + 1.15, -21.96, 12.1, 0.14, 0.1)
    for q in range(13):                                                                        # wall panels
        b.box('WhiteShade', qx - 6.0 + q * 1.0, 17.6, -21.97, 0.04, 8.8, 0.06)
    for fy in (15.2, 17.6, 20.0):
        for wx in range(4, 15, 2):
            b.box('Steel', wx, fy + 0.5, -21.97, 1.3, 1.45, 0.06)
            b.box('Lit', wx, fy + 0.5, -21.94, 1.05, 1.2, 0.05)
            b.box('Steel', wx, fy + 0.5, -21.9, 0.05, 1.2, 0.04)
            b.box('Steel', wx, fy + 0.7, -21.9, 1.05, 0.05, 0.04)
    b.box('SteelDark', qx, 22.3, qz, 12.6, 0.4, 8.6)                                           # roof, with its equipment
    for x, z in ((6.0, -27.0), (12.0, -24.0)):
        b.cone('WhiteShade', x, 22.5, z, 0.7, 0.7, 0.6, 8)
        b.cone('SteelDark', x, 23.1, z, 0.75, 0.1, 0.3, 8)
    b.limb('SteelDark', (3.0, 22.5, -29.0), (3.0, 30.0, -29.0), 0.1, 0.07, 6)                  # a comms mast with dishes and a red light
    for k in range(4):
        b.box('Steel', 3.0, 24.5 + k * 1.6, -29.0, 1.6 - k * 0.3, 0.06, 0.06)
    b.ico('Steel', 3.0, 26.2, -28.4, 0.5, 0.35, sub=1, sc=(1.0, 1.0, 0.5))
    b.ico('RedLight', 3.0, 30.1, -29.0, 0.14, 1.0, sub=0)
    b.box('Steel', 13.0, 23.6, -23.0, 0.7, 0.7, 0.7)                                          # radar
    b.limb('SteelDark', (13.0, 22.5, -23.0), (13.0, 23.2, -23.0), 0.08, 0.08, 5)
    b.box('SteelLight', 13.0, 24.1, -23.0, 2.2, 0.1, 0.25)
    rail(b, [(qx - 6.1, -21.9), (qx + 6.1, -21.9), (qx + 6.1, -30.1), (qx - 6.1, -30.1), (qx - 6.1, -21.9)], 22.5, 1.0)
    # an outside stair zigzagging up the front, a door and a lit canopy at the foot
    for k in range(18):
        b.box('Grating', 16.0, DECK_Y + 0.25 + k * 0.5, -22.4 - (k % 6) * 0.0, 1.0 if k % 6 else 1.2, 0.08, 0.5)
    b.box('White', 3.5, DECK_Y + 1.1, -21.97, 1.0, 2.2, 0.08)
    b.box('Lit', 3.5, DECK_Y + 2.45, -21.8, 0.3, 0.14, 0.12)
    b.box('Yellow', 3.5, DECK_Y + 2.3, -21.75, 1.6, 0.1, 0.6)
    # tanks: two white cylinders with domed tops, ladders and pipes between them
    for tx in (-2.0, 1.5):
        b.cone('White', tx, DECK_Y, -24.0, 1.6, 1.6, 5.2, 12)
        b.ico('White', tx, DECK_Y + 5.2, -24.0, 1.6, 0.4, sub=2)
        b.cone('WhiteShade', tx, DECK_Y + 1.5, -24.0, 1.64, 1.64, 0.14, 12)
        b.cone('WhiteShade', tx, DECK_Y + 3.6, -24.0, 1.64, 1.64, 0.14, 12)
        b.cone('Yellow', tx, DECK_Y, -24.0, 1.7, 1.7, 0.4, 12)
        ladder(b, tx, DECK_Y + 0.5, DECK_Y + 5.2, -22.35, 'z', False)
        b.box('Red', tx, DECK_Y + 5.9, -24.0, 0.1, 0.5, 0.1)
    pipe(b, (-0.25, DECK_Y + 4.0, -24.0), (-0.25, DECK_Y + 4.0, -24.0), 0.01, 'Steel', 3)
    pipe(b, (-1.0, DECK_Y + 0.8, -22.4), (0.5, DECK_Y + 0.8, -22.4), 0.14, 'Orange', 8)
    # orange pipe runs along the deck from the tanks to the derrick, on supports, with valves and elbows
    for pz in (-10.0, -12.0, -14.0):
        pipe(b, (-12.0, DECK_Y + 0.6, pz), (16.0, DECK_Y + 0.6, pz), 0.25, 'Orange', 10)
        for x in range(-11, 16, 4):
            b.box('SteelDark', x, DECK_Y + 0.3, pz, 0.2, 0.6, 0.6)
        for x in (-6.0, 4.0, 10.0):
            flange(b, (x, DECK_Y + 0.6, pz), 'x', 0.25)
    for x in (2.0, 11.0):                                                                      # valve wheels on the pipes
        b.limb('Red', (x, DECK_Y + 0.85, -12.0), (x, DECK_Y + 1.4, -12.0), 0.03, 0.03, 4)
        b.limb('Red', (x - 0.25, DECK_Y + 1.4, -12.0), (x + 0.25, DECK_Y + 1.4, -12.0), 0.04, 0.04, 6)


# ---------------------------------------------------------------- the derrick (collision 7 x 32 x 7 at (-4, 13, -17)) and the flare stack
def derrick(b):
    dx, dz, y0 = -4.0, -17.0, DECK_Y
    top = 45.0
    rows = [y0 + (top - y0) * i / 8 for i in range(9)]

    def half(y):
        return 3.2 - 2.4 * (y - y0) / (top - y0)
    for sx in (-1, 1):
        for sz in (-1, 1):
            b.limb('Red', (dx + sx * 3.2, y0, dz + sz * 3.2), (dx + sx * 0.8, top, dz + sz * 0.8), 0.3, 0.22, 6)
    for i in range(1, 8):
        y = rows[i]
        k = half(y)
        for s in (-1, 1):
            b.limb('RedDark', (dx - k, y, dz + s * k), (dx + k, y, dz + s * k), 0.11, 0.11, 4)
            b.limb('RedDark', (dx + s * k, y, dz - k), (dx + s * k, y, dz + k), 0.11, 0.11, 4)
    for i in range(8):                                                                         # X bracing on all four sides
        ya, yb = rows[i], rows[i + 1]
        ka, kb = half(ya), half(yb)
        for s in (-1, 1):
            b.limb('RedDark', (dx - ka, ya, dz + s * ka), (dx + kb, yb, dz + s * kb), 0.06, 0.06, 4)
            b.limb('RedDark', (dx + ka, ya, dz + s * ka), (dx - kb, yb, dz + s * kb), 0.06, 0.06, 4)
            b.limb('RedDark', (dx + s * ka, ya, dz - ka), (dx + s * kb, yb, dz + kb), 0.06, 0.06, 4)
            b.limb('RedDark', (dx + s * ka, ya, dz + ka), (dx + s * kb, yb, dz - kb), 0.06, 0.06, 4)
    # the drill floor: a platform at 6 m up with rails, a doghouse, the travelling block, the crown block and the top light
    for k, (y, w) in enumerate(((y0 + 5.0, 3.0), (y0 + 15.0, 2.6), (y0 + 26.0, 1.9))):
        b.box('Grating', dx, y, dz, w * 2 + 0.6, 0.12, w * 2 + 0.6)
        rail(b, [(dx - w - 0.3, dz - w - 0.3), (dx + w + 0.3, dz - w - 0.3), (dx + w + 0.3, dz + w + 0.3), (dx - w - 0.3, dz + w + 0.3), (dx - w - 0.3, dz - w - 0.3)], y, 1.0, 'Yellow')
    b.box('White', dx - 5.2, y0 + 1.6, dz + 0.5, 3.0, 3.2, 3.0, bevel=0.05)                  # the doghouse beside the foot
    b.box('Lit', dx - 3.65, y0 + 1.9, dz + 0.5, 0.05, 0.9, 1.6)
    b.box('SteelDark', dx - 5.2, y0 + 3.3, dz + 0.5, 3.3, 0.2, 3.3)
    b.box('SteelDark', dx, top - 0.5, dz, 2.0, 0.5, 2.0)                                       # the crown block
    for q in range(4):
        b.cone('Steel', dx + (q % 2 - 0.5) * 0.9, top - 0.3, dz + (q // 2 - 0.5) * 0.9, 0.3, 0.3, 0.4, 8)
    b.limb('Steel', (dx, top - 0.5, dz), (dx, y0 + 9.0, dz), 0.06, 0.06, 4)
    b.box('Yellow', dx, y0 + 8.4, dz, 1.0, 1.2, 1.0, bevel=0.1)                               # travelling block and hook
    b.limb('SteelDark', (dx, y0 + 7.8, dz), (dx, y0 + 6.0, dz), 0.1, 0.07, 6)
    b.limb('SteelDark', (dx, top + 0.3, dz), (dx, top + 1.8, dz), 0.05, 0.04, 4)
    b.ico('RedLight', dx, top + 1.9, dz, 0.28, 1.0, sub=1)
    b.cone('Steel', dx, top - 0.1, dz, 0.9, 0.9, 0.3, 8)
    for k in range(6):                                                                         # stands of drill pipe racked in the derrick
        b.limb('SteelLight', (dx + 1.2 + k * 0.28, y0 + 5.2, dz + 1.6), (dx + 1.1 + k * 0.28, y0 + 30.0, dz + 1.5), 0.07, 0.07, 6)
    b.limb('Orange', (dx, y0, dz), (dx, y0 + 5.0, dz), 0.4, 0.4, 8)                            # the riser and the drill string
    b.cone('SteelDark', dx, y0 - 0.05, dz, 2.2, 2.0, 0.5, 10)
    ladder(b, dx + 3.1, y0, y0 + 26.0, dz + 3.3, 'z')


def flare(b):
    fx, fz = 17.0, -29.0
    h = 30.0
    for sx in (-1, 1):
        for sz in (-1, 1):
            b.limb('SteelDark', (fx + sx * 0.9, DECK_Y, fz + sz * 0.9), (fx + sx * 0.4, DECK_Y + h, fz + sz * 0.4), 0.09, 0.07, 5)
    for k in range(10):
        y = DECK_Y + 1.5 + k * 3.0
        t = (y - DECK_Y) / h
        w = 0.9 - 0.5 * t
        for s in (-1, 1):
            b.limb('SteelDark', (fx - w, y, fz + s * w), (fx + w, y, fz + s * w), 0.04, 0.04, 3)
            b.limb('SteelDark', (fx + s * w, y, fz - w), (fx + s * w, y, fz + w), 0.04, 0.04, 3)
        b.limb('SteelDark', (fx - w, y, fz - w), (fx + w * 0.6, y + 3.0, fz + w * 0.6), 0.035, 0.035, 3)
        b.limb('SteelDark', (fx + w, y, fz - w), (fx - w * 0.6, y + 3.0, fz + w * 0.6), 0.035, 0.035, 3)
    b.cone('Steel', fx, DECK_Y + h - 0.2, fz, 0.5, 0.7, 1.2, 10)                                # the flare tip, where the game puts its flame
    b.cone('SteelDark', fx, DECK_Y + h + 0.9, fz, 0.7, 0.5, 0.4, 10)
    for y in (DECK_Y + 10.0, DECK_Y + 20.0):
        b.cone('Grating', fx, y, fz, 1.7, 1.7, 0.1, 10)
    ladder(b, fx, DECK_Y, DECK_Y + 28.0, fz - 1.0, 'z')
    b.cone('Concrete', fx, DECK_Y, fz, 1.5, 1.4, 0.5, 8)                                      # base
    for q in range(3):                                                                         # warning stripes on the base of the stack
        b.cone('Yellow' if q % 2 == 0 else 'Black', fx, DECK_Y + 0.5 + q * 0.5, fz, 0.95, 0.9, 0.5, 8)


# ---------------------------------------------------------------- 0: the boat landing, 2.5 m (collision 5 x 0.25 x 4.2 at (-14, 2.35, -5.9))
def landing(b):
    lx, lz = -14.0, -5.9
    b.box('Grating', lx, 2.35, lz, 5.0, 0.25, 4.2)
    for q in range(12):
        b.box('SteelLight', lx - 2.2 + q * 0.4, 2.48, lz, 0.04, 0.02, 4.1)
    b.box('SteelDark', lx, 2.15, lz, 5.1, 0.18, 4.3)
    for dz in (-1.8, 0.0, 1.8):
        b.box('SteelDark', lx, 1.95, lz + dz, 4.9, 0.3, 0.2)
    b.box('Yellow', lx, 2.4, lz + 2.12, 5.0, 0.3, 0.06)
    hazard(b, lx, 2.475, -4.1, 3.0, 0.45)
    b.box('White', lx, 2.482, -3.88, 3.0, 0.02, 0.1)
    for rx in (-16.5, -11.5):                                                                 # diagonal braces to the leg, as in the game
        pipe(b, (rx, 2.2, -3.9), (rx, -0.8, -7.6), 0.14, 'SteelDark', 8)
        pipe(b, (rx, 2.2, -7.9), (rx, -0.8, -7.6), 0.1, 'SteelDark', 8)
    rail(b, [(-16.45, -3.9), (-16.45, -7.9), (-11.55, -7.9)], 2.475, 1.0)
    rail(b, [(-11.55, -3.9), (-11.55, -6.4)], 2.475, 1.0)
    for lxx in (-16.2, -11.8):                                                                # rubber fenders along the edge and mooring bollards
        b.cone('Rubber', lxx, 0.7, -3.75, 0.18, 0.18, 1.8, 6)
    for bx in (-15.6, -12.4):
        b.cone('SteelDark', bx, 2.475, -7.4, 0.2, 0.14, 0.4, 8)
        b.cone('SteelDark', bx, 2.85, -7.4, 0.26, 0.26, 0.08, 8)
    # a stair/ladder down to the water, a life ring on a post and a lamp
    for y in range(0, 5):
        b.box('Grating', -12.5, 0.4 + y * 0.4, -7.9 + 0.2 * (4 - y) * 0.0, 0.7, 0.04, 0.5)
    for dx in (-0.4, 0.4):
        b.limb('Yellow', (-12.5 + dx, 0.2, -8.0), (-12.5 + dx, 2.4, -8.0), 0.03, 0.03, 4)
    b.limb('SteelDark', (-11.9, 2.475, -4.4), (-11.9, 3.8, -4.4), 0.04, 0.04, 5)
    b.cone('Orange', -11.9, 3.2, -4.55, 0.35, 0.35, 0.1, 12)
    light_post(b, -15.9, 2.475, -4.4, 2.0)
    # the lifeboat on davits above (collision-free in the game, a decorative orange capsule)
    boat_x, boat_y, boat_z = -17.5, 3.2, -9.0
    b.ico('Orange', boat_x, boat_y + 0.5, boat_z, 0.9, 1.0, sub=2, sc=(1.0, 0.75, 1.9))
    b.box('SteelDark', boat_x, boat_y + 0.0, boat_z, 0.7, 0.15, 3.2, bevel=0.05)
    b.box('Glass', boat_x, boat_y + 0.8, boat_z - 1.1, 0.8, 0.3, 0.05)
    for dz in (-1.2, 1.2):
        b.limb('Yellow', (boat_x - 1.0, 12.5, boat_z + dz), (boat_x - 0.9, boat_y + 1.2, boat_z + dz), 0.07, 0.06, 5)
        b.limb('Rope', (boat_x, boat_y + 1.4, boat_z + dz), (boat_x - 0.5, 12.0, boat_z + dz), 0.02, 0.02, 3)
    b.spot('Spot_Landing', lx, 2.475, -3.8)


# ---------------------------------------------------------------- 1: the lower walkway, 8 m (collision 14 x 0.25 x 5.4 at (-2, 7.875, -5.9))
def walkway(b):
    wx, wz = -2.0, -5.9
    b.box('Grating', wx, 7.875, wz, 14.0, 0.25, 5.4)
    for q in range(34):
        b.box('SteelLight', wx - 6.6 + q * 0.4, 8.0, wz, 0.04, 0.02, 5.3)
    b.box('SteelDark', wx, 7.7, wz, 14.1, 0.2, 5.5)
    for x in (-7.0, -2.0, 3.0):
        b.box('SteelDark', x, 7.4, wz, 0.2, 0.5, 5.4)
    for rx in (-8.8, 4.8):
        rail(b, [(rx, -3.3), (rx, -8.5)], 8.0, 1.0)
    rail(b, [(-8.8, -8.5), (4.8, -8.5)], 8.0, 1.0)
    for bx in (-8.0, 4.0):                                                                     # braces up to the deck, as in the game
        pipe(b, (bx, 7.7, -3.5), (bx, 5.0, -8.5), 0.16, 'SteelDark', 8)
        pipe(b, (bx, 11.4, -3.5), (bx, 7.7, -3.5), 0.16, 'SteelDark', 8)
    hazard(b, wx, 8.0, -3.45, 4.0, 0.4)
    b.box('White', wx, 8.008, -3.22, 4.0, 0.012, 0.1)
    for x in (-6.0, 2.0):
        light_post(b, x, 8.0, -8.3, 2.0)
    b.box('Yellow', -7.5, 8.45, -7.9, 1.0, 0.9, 0.5, bevel=0.05)                                # a locker
    b.box('SteelDark', -7.5, 8.45, -7.65, 0.9, 0.8, 0.04)
    for k in range(3):                                                                         # a hose reel and a coil of rope
        b.cone('Red', 1.0 + k * 0.1, 8.0, -8.1, 0.3, 0.3, 0.2, 10)
    b.cone('Rope', 3.4, 8.0, -7.9, 0.3, 0.26, 0.2, 8)
    b.spot('Spot_Walkway', wx, 8.0, -3.2)


# ---------------------------------------------------------------- 3: the helideck, 19 m (collision: disc 9.8 x 0.4 at (12, 18.8, -3.5))
def helideck(b):
    hx, hy, hz = 12.0, 18.8, -3.5
    # a round deck on three struts and a frame; the perimeter net, the lights, the markings
    for q in range(20):
        a = q * math.pi * 2 / 20
        b.box('SteelDark', hx + math.cos(a) * 4.0, hy - 0.5, hz + math.sin(a) * 4.0, 0.2, 0.8, 0.2, rot_y=0)
    b.cone('SteelDark', hx, hy - 0.4, hz, 4.95, 4.95, 0.8, 20)                                  # the frame under the deck
    b.cone('Deck', hx, hy - 0.2, hz, 4.9, 4.9, 0.4, 20)
    b.cone('Yellow', hx, hy + 0.2, hz, 4.25, 4.25, 0.02, 20)
    b.cone('Deck', hx, hy + 0.21, hz, 3.95, 3.95, 0.03, 20)
    for hx_ in (-0.9, 0.9):
        b.box('White', hx + hx_, hy + 0.24, hz, 0.4, 0.02, 2.6)
    b.box('White', hx, hy + 0.24, hz, 1.9, 0.02, 0.4)
    for q in range(12):                                                                         # twelve perimeter lights, glowing
        a = q * math.pi * 2 / 12
        b.ico('Lit', hx + math.cos(a) * 4.75, hy + 0.3, hz + math.sin(a) * 4.75, 0.14, 1.0, sub=0)
    for q in range(24):                                                                         # the safety net posts and a red line round the edge
        a = q * math.pi * 2 / 24
        b.limb('Yellow', (hx + math.cos(a) * 5.0, hy - 0.2, hz + math.sin(a) * 5.0), (hx + math.cos(a) * 5.4, hy + 0.2, hz + math.sin(a) * 5.4), 0.04, 0.04, 4)
    for q in range(24):
        a, a2 = q * math.pi * 2 / 24, (q + 1) * math.pi * 2 / 24
        b.limb('Yellow', (hx + math.cos(a) * 5.4, hy + 0.2, hz + math.sin(a) * 5.4), (hx + math.cos(a2) * 5.4, hy + 0.2, hz + math.sin(a2) * 5.4), 0.03, 0.03, 3)
        b.limb('SteelDark', (hx + math.cos(a) * 5.2, hy - 0.1, hz + math.sin(a) * 5.2), (hx + math.cos(a2) * 5.2, hy - 0.1, hz + math.sin(a2) * 5.2), 0.02, 0.02, 3)
    for bx in (8.5, 15.5):
        pipe(b, (bx, 13.2, -6.8), (bx, 18.4, -1.2), 0.2, 'Steel', 8)
    pipe(b, (12.0, 13.2, -5.0), (12.0, 18.4, -0.2), 0.2, 'Steel', 8)
    pipe(b, (9.0, 13.2, -2.0), (10.5, 18.4, -2.8), 0.14, 'SteelDark', 6)
    pipe(b, (15.0, 13.2, -2.0), (13.5, 18.4, -2.8), 0.14, 'SteelDark', 6)
    # a windsock, a fire monitor and a little red hangar light
    b.limb('SteelDark', (hx - 5.0, hy, hz - 4.0), (hx - 5.0, hy + 4.0, hz - 4.0), 0.06, 0.05, 5)
    b.skin('Orange', [[(hx - 5.0, hy + 4.0, hz - 4.0)] * 6, [(hx - 5.0 + 1.6, hy + 3.9 + math.cos(a) * 0.3, hz - 4.0 + math.sin(a) * 0.3) for a in range(6)]], axis=lambda k: (hx - 5.0, hy + 4.0, hz - 4.0), closed=True)
    b.cone('Red', hx - 3.8, hy, hz - 4.6, 0.25, 0.2, 0.5, 8)
    # the stairs from the main deck to the helideck, a ladder at the back
    for k in range(16):
        b.box('Grating', 6.8 + k * 0.1, DECK_Y + 0.3 + k * 0.36, -7.3 + k * 0.0, 0.9, 0.06, 0.4 + 0.0)
    ladder(b, hx + 3.5, DECK_Y, hy - 0.3, hz - 4.2, 'z')
    # the mark at the dive: a red/white chequered edge and a yellow arrow
    b.box('Red', hx, hy + 0.225, hz + 4.55, 2.6, 0.02, 0.3)
    b.box('White', hx, hy + 0.23, hz + 4.55, 1.3, 0.02, 0.3)
    b.tri('Yellow', (hx - 0.5, hy + 0.235, hz + 3.6), (hx + 0.5, hy + 0.235, hz + 3.6), (hx, hy + 0.235, hz + 4.3))
    b.spot('Spot_Helideck', hx, 19.0, 1.0)


# ---------------------------------------------------------------- 4: the crane (pedestal 2.6 x 12, cab 2.6 x 1.8 x 2.4, boom 1.4 x 0.25 x 13.4 at y 26.4)
def crane(b):
    cx, cz = -12.0, -8.0
    b.cone('Yellow', cx, 13.0, cz, 1.35, 1.1, 12.0, 8)
    for y in (14.5, 17.0, 20.0, 23.0):
        b.cone('YellowDark', cx, y, cz, 1.3, 1.3, 0.2, 8)
    b.cone('SteelDark', cx, 25.0, cz, 1.7, 1.7, 0.5, 12)                                       # the slew ring on top of the pedestal
    ladder(b, cx - 1.6, DECK_Y, 25.0, cz + 0.0, 'z')
    b.box('White', cx, 25.8, -8.4, 2.6, 1.8, 2.4, bevel=0.05)                                  # the cab, glass all round at the front
    b.box('Glass', cx, 26.0, -7.17, 2.1, 0.9, 0.05)
    for q in range(3):
        b.box('Steel', cx - 0.7 + q * 0.7, 26.0, -7.15, 0.04, 0.9, 0.04)
    b.box('Lit', cx, 25.6, -7.16, 0.4, 0.2, 0.02)
    b.box('SteelDark', cx, 26.8, -8.4, 2.8, 0.15, 2.6)
    b.box('Yellow', cx, 25.8, -10.4, 2.2, 1.4, 1.2, bevel=0.1)                                 # the counterweight
    b.box('SteelDark', cx, 25.8, -11.05, 2.2, 1.4, 0.1)
    # the boom: a lattice (two chords with webs), a top chord and a stay, a hook block on cables; the tip is clear for the diver
    boom_y, z0, z1 = 26.4, -8.6, 4.8
    for bx in (-12.65, -11.35):
        pipe(b, (bx, boom_y - 0.1, z0), (bx, boom_y - 0.9, z1), 0.07, 'Yellow', 6)
        for z in [-7.0 + 2.2 * q for q in range(6)]:
            pipe(b, (bx, boom_y - 0.1 - (z + 8.0) * 0.06, z), (bx, boom_y + 0.55, z + 1.1), 0.05, 'Yellow', 4)
            pipe(b, (bx, boom_y + 0.55, z + 1.1), (bx, boom_y - 0.1 - (z + 11.0) * 0.06, z + 2.2), 0.05, 'Yellow', 4)
    b.box('Yellow', cx, boom_y, -1.9, 1.4, 0.25, 13.4)
    for z in range(-8, 5, 2):
        b.box('YellowDark', cx, boom_y - 0.05, z, 1.4, 0.3, 0.1)
    for bx in (-12.7, -11.3):
        b.limb('Yellow', (bx, 27.0, 4.4), (bx, 27.0, -8.0), 0.03, 0.03, 4)
        for z in (-7.0, -3.0, 1.0, 4.4):
            b.limb('Yellow', (bx, 26.5, z), (bx, 27.0, z), 0.03, 0.03, 4)
    b.limb('SteelDark', (cx, 28.5, -8.4), (cx, 31.5, -8.4), 0.12, 0.1, 6)                       # the A-frame and the pendants to the boom tip
    for bx in (-12.4, -11.6):
        b.limb('SteelDark', (bx, 31.4, -8.4), (bx, 31.4, -8.4), 0.01, 0.01, 3)
        b.limb('SteelDark', (bx * 1.0, 31.5, -8.4), (bx, 27.0, 4.2), 0.025, 0.025, 3)
    b.box('Steel', cx, 31.6, -8.4, 1.0, 0.2, 0.4)
    b.ico('RedLight', cx, 27.5, 4.6, 0.17, 1.0, sub=0)
    b.limb('SteelDark', (cx, 27.4, 4.6), (cx, 27.2, 4.6), 0.03, 0.03, 4)
    b.limb('Cable', (cx, 26.2, 1.5), (cx, 20.5, 1.5), 0.03, 0.03, 4)                           # the hook block hanging near the middle of the boom
    b.box('Yellow', cx, 20.0, 1.5, 0.6, 0.9, 0.6, bevel=0.1)
    b.limb('SteelDark', (cx, 19.5, 1.5), (cx, 18.7, 1.5), 0.08, 0.05, 6)
    b.box('White', cx, 26.535, 4.7, 1.4, 0.012, 0.14)                                           # the edge at the tip
    hazard(b, cx, 26.525, 4.45, 1.4, 0.35)
    b.spot('Spot_Crane', cx, 26.525, 4.7)


# ---------------------------------------------------------------- 5: the supply vessel (collision: hull 4.4 x 3.2 x 14 at (24, 1.6, 30), bow, house, deck)
def vessel(b):
    vx, vz = 24.0, 30.0

    def pt(s, lat, y):
        return (vx + lat, y, vz + s)
    stations = [(-8.9, 0.12), (-8.5, 0.6), (-7.6, 1.3), (-6.0, 1.95), (-4.0, 2.2), (0.0, 2.2), (5.0, 2.2), (6.8, 2.0), (7.0, 1.8)]
    hull(b, pt, stations, (('Red', 3.2, 0.9), ('White', 0.9, 0.55), ('Red', 0.55, None)), 'Deck', 3.2, lambda s: -1.6 if abs(s) < 5.5 else -1.6 + (abs(s) - 5.5) / 3.4 * 1.2)
    for side in (-1, 1):                                                                          # bulwark plates, a rubbing strake, rail posts
        for i in range(len(stations) - 1):
            s0, w0 = stations[i]
            s1, w1 = stations[i + 1]
            b.limb('Rubber', pt(s0, side * (w0 + 0.03), 2.0), pt(s1, side * (w1 + 0.03), 2.0), 0.07, 0.07, 4)
            if s0 > -6.5:
                b.limb('White', pt(s0, side * (w0 - 0.04), 4.2), pt(s1, side * (w1 - 0.04), 4.2), 0.03, 0.03, 4)
                b.limb('White', pt(s0, side * (w0 - 0.04), 3.7), pt(s1, side * (w1 - 0.04), 3.7), 0.025, 0.025, 4)
        for s in (-5.5, -3.5, -1.5, 0.5, 2.5, 4.5, 6.2):
            w = 2.15
            b.limb('White', pt(s, side * (w - 0.04), 3.2), pt(s, side * (w - 0.04), 4.25), 0.03, 0.03, 4)
    # the house aft (3.8 x 3.6 x 4.2 above the deck at z 32.1..36.3), the bridge windows, the funnel, a mast with radar
    b.box('White', vx, 5.2, vz + 4.2, 3.8, 3.6, 4.2, bevel=0.05)
    b.box('White', vx, 7.35, vz + 3.9, 4.2, 0.9, 3.4, bevel=0.05)
    for q in range(5):
        b.box('Lit', vx - 1.5 + q * 0.75, 7.4, vz + 2.17, 0.62, 0.55, 0.05)
    for sx in (-1, 1):
        b.box('Glass', vx + sx * 1.92, 5.7, vz + 3.6, 0.05, 0.7, 0.8)
        b.box('Lit', vx + sx * 1.93, 4.5, vz + 5.0, 0.04, 0.5, 0.5)
    b.box('Lit', vx, 5.9, vz + 2.08, 3.0, 0.8, 0.05)
    b.box('Black', vx, 7.9, vz + 3.9, 4.3, 0.12, 3.5)
    b.cone('Yellow', vx, 7.0, vz + 5.2, 0.55, 0.45, 2.2, 8)
    b.cone('Black', vx, 9.0, vz + 5.2, 0.5, 0.5, 0.15, 8)
    b.limb('SteelDark', (vx - 0.8, 7.9, vz + 3.0), (vx - 0.8, 10.4, vz + 3.0), 0.05, 0.04, 5)
    b.box('Steel', vx - 0.8, 10.1, vz + 3.0, 1.0, 0.06, 0.06)
    b.box('SteelLight', vx - 0.8, 10.5, vz + 3.0, 1.2, 0.08, 0.2)
    b.ico('GreenLight', vx - 1.9, 7.2, vz + 2.3, 0.1, 1.0, sub=0)
    b.ico('RedLight', vx + 1.9, 7.2, vz + 2.3, 0.1, 1.0, sub=0)
    # a deck crane near the house, pipes and cargo tubes lashed on the open deck, a winch forward, nothing at the bow tip
    b.cone('Yellow', vx + 1.3, 3.2, vz - 0.4, 0.2, 0.2, 1.4, 8)
    b.limb('Yellow', (vx + 1.3, 4.5, vz - 0.4), (vx + 1.5, 6.6, vz - 2.8), 0.12, 0.09, 6)
    b.limb('Cable', (vx + 1.5, 6.6, vz - 2.8), (vx + 1.5, 4.2, vz - 2.8), 0.02, 0.02, 3)
    b.box('Orange', vx + 1.5, 4.1, vz - 2.8, 0.3, 0.3, 0.3)
    for q in range(5):
        pipe(b, (vx - 0.9, 3.45 + (q % 2) * 0.28, vz + 0.6 - q * 0.05), (vx - 0.9, 3.45 + (q % 2) * 0.28, vz + 3.1), 0.14, 'SteelLight' if q % 2 else 'Steel', 8)
    b.box('Blue', vx - 0.8, 4.0, vz - 3.2, 1.4, 1.6, 2.4, bevel=0.05)
    b.box('SteelDark', vx - 0.8, 3.25, vz - 3.2, 1.5, 0.1, 2.5)
    b.cone('SteelDark', vx, 3.2, vz - 5.4, 0.45, 0.4, 0.5, 8)
    b.cone('SteelDark', vx, 3.7, vz - 5.4, 0.55, 0.55, 0.08, 8)
    for dx in (-1.4, 1.4):
        b.cone('Black', vx + dx, 3.2, vz + 1.0, 0.18, 0.15, 0.35, 8)
    for k in range(6):                                                                         # tyres hung along the sides as fenders
        b.cone('Rubber', vx - 2.28, 1.3, vz - 4.0 + k * 1.7, 0.4, 0.4, 0.3, 8)
        b.cone('Rubber', vx + 2.28 - 0.3, 1.3, vz - 4.0 + k * 1.7, 0.4, 0.4, 0.3, 8)
    b.box('White', vx, 3.222, vz - 8.0, 1.0, 0.012, 0.16)                                       # the line at the bow edge
    hazard(b, vx, 3.2, vz - 8.2, 0.9, 0.35)
    b.spot('Spot_Vessel', vx, 3.2, vz - 8.3)


# ---------------------------------------------------------------- 6: the navigation tower on a rock mound (collision: rock, shaft 3 x 16, gallery 6 x 0.3 at 13.85)
def nav_tower(b):
    nx, nz = -30.0, 30.0
    # a mound of riprap with a concrete collar round the base; the tower shaft in yellow and white bands, a door and windows, the gallery, a lantern room, a red cap
    for k in range(16):
        a = k * 0.4
        r = 2.0 + 2.2 * hash01(k + 4)
        b.ico('ConcreteDark' if k % 2 else 'Steel', nx + math.cos(a) * r * 1.2, -0.6 + 0.9 * hash01(k), nz + math.sin(a) * r, 1.2 + 0.7 * hash01(k + 2), 0.7, sub=1, sc=(1.1, 0.8, 1.0), jit=0.2, seed=k + 10)
    b.cone('ConcreteDark', nx, -6.0, nz, 4.8, 3.6, 6.9, 12)
    b.cone('Concrete', nx, 0.6, nz, 3.4, 2.9, 0.9, 12)
    for k in range(8):
        a = k * 0.785
        b.ico('Growth', nx + math.cos(a) * 3.5, -0.2, nz + math.sin(a) * 3.5, 0.5, 0.4, sub=0)
    for q in range(8):
        y = 1.4 + q * 1.4
        t = (y + 6.0) / 20.0
        r0, r1 = 2.2 - 0.7 * ((y + 6.0) / 20.0), 2.2 - 0.7 * ((y + 7.4) / 20.0)
        b.cone('Yellow' if q % 2 == 0 else 'White', nx, y, nz, r0, r1, 1.4, 8)
    b.cone('Yellow', nx, 1.4 - 1.5, nz, 2.0, 1.95, 1.5, 8)
    b.box('Black', nx - 1.9, 2.4, nz, 0.12, 1.8, 1.0)                                          # a door and little windows
    b.box('Steel', nx - 1.93, 2.4, nz, 0.06, 1.8, 1.1)
    for y in (5.0, 8.0, 11.0):
        b.box('Glass', nx - 1.7 + 0.0, y, nz - 0.3, 0.1, 0.8, 0.45)
        b.box('Glass', nx + 0.3, y, nz - 1.7, 0.45, 0.8, 0.1)
    b.cone('SteelDark', nx, 13.7, nz, 3.0, 3.0, 0.3, 12)
    b.cone('Steel', nx, 13.3, nz, 2.0, 2.2, 0.5, 12)
    for q in range(12):
        a = q * 0.5236
        b.limb('SteelDark', (nx + math.cos(a) * 1.5, 13.4, nz + math.sin(a) * 1.5), (nx + math.cos(a) * 2.7, 13.8, nz + math.sin(a) * 2.7), 0.08, 0.06, 4)
    for q in range(20):
        a = q * 0.314
        if math.cos(a) > 0.6:
            continue
        b.limb('Yellow', (nx + math.cos(a) * 2.9, 14.0, nz + math.sin(a) * 2.9), (nx + math.cos(a) * 2.9, 15.1, nz + math.sin(a) * 2.9), 0.04, 0.04, 4)
    for q in range(20):
        a, a2 = q * 0.314, (q + 1) * 0.314
        if math.cos(a) > 0.6 or math.cos(a2) > 0.6:
            continue
        b.limb('Yellow', (nx + math.cos(a) * 2.9, 15.1, nz + math.sin(a) * 2.9), (nx + math.cos(a2) * 2.9, 15.1, nz + math.sin(a2) * 2.9), 0.03, 0.03, 3)
        b.limb('Yellow', (nx + math.cos(a) * 2.9, 14.55, nz + math.sin(a) * 2.9), (nx + math.cos(a2) * 2.9, 14.55, nz + math.sin(a2) * 2.9), 0.025, 0.025, 3)
    b.cone('Lit', nx, 14.0, nz, 0.95, 0.95, 1.5, 8)
    for q in range(8):
        a = q * 0.785
        b.limb('Red', (nx + math.cos(a) * 1.0, 14.0, nz + math.sin(a) * 1.0), (nx + math.cos(a) * 1.0, 15.5, nz + math.sin(a) * 1.0), 0.05, 0.05, 4)
    b.cone('Red', nx, 15.5, nz, 1.5, 0.12, 1.2, 8)
    b.ico('Steel', nx, 16.8, nz, 0.15, 1.0, sub=0)
    b.limb('SteelDark', (nx, 16.8, nz), (nx, 18.0, nz), 0.03, 0.02, 4)
    for k in range(3):                                                                         # a ladder up the shaft, a solar panel and a mooring ring at the foot
        pass
    ladder(b, nx + 0.0, 1.5, 13.3, nz + 2.0, 'z', True)
    b.box('Blue', nx + 1.6, 12.0, nz - 1.0, 0.1, 1.0, 1.6, rot_y=0)
    b.box('White', nx + 2.2, 13.9 + 0.012, nz, 0.5, 0.012, 0.14)
    hazard(b, nx + 2.55, 14.0, nz, 0.35, 1.2, along='z')
    b.box('White', nx + 2.9, 14.012, nz, 0.1, 0.012, 1.2)
    b.spot('Spot_NavTower', nx + 2.6, 14.0, nz)


# ---------------------------------------------------------------- build
def build():
    b = locbuild.Builder()
    for k, v in COLOURS.items():
        b.mat(k, v)
    block_base(b)
    legs(b)
    main_deck(b)
    quarters(b)
    derrick(b)
    flare(b)
    landing(b)
    walkway(b)
    helideck(b)
    crane(b)
    vessel(b)
    nav_tower(b)
    b.spot('Spot_Deck', 4.0, 13.0, -4.0)
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
            n.inputs['Base Color'].default_value = (0.01, 0.12, 0.17, 1)
            n.inputs['Alpha'].default_value = 0.75
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
    objs = b.finish('Loc_Rig')
    print('LOCATION TRIS', b.tris, 'objects', len(objs))
    b.export(OUT)
    add_preview_water()
    view.shoot('loc_rig_overview', (0, 12, 4), 150, (0.35, 0.58, 1.0), lens=50, res=(1200, 800))
    view.shoot('loc_rig_deck', (0, 18, -16), 70, (0.1, 0.35, 1.0), lens=50, res=(1100, 700))
    view.shoot('loc_rig_helideck', (12, 18, -3), 30, (0.4, 0.3, 1.0), lens=50, res=(1100, 700))
    view.shoot('loc_rig_low', (-14, 6, -6), 36, (0.5, 0.2, 1.0), lens=50, res=(1100, 700))
    view.shoot('loc_rig_vessel', (24, 4, 30), 26, (-0.4, 0.4, -1.0), lens=50, res=(1100, 700))
    view.shoot('loc_rig_nav', (-30, 8, 30), 36, (0.5, 0.2, 1.0), lens=50, res=(1100, 700))
    print('LOC DONE')


if __name__ == '__main__':
    main()
