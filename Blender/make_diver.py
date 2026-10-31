"""The human character of the game: one body, rigged to the bones the game drives, plus every prop as a separate part.
Run: blender --background --python make_diver.py
Output: Assets/_Project/Models/Characters/Diver.fbx, Blender/Diver.blend, previews in Blender/previews.
All numbers are Unity metres; the bone positions match the body the game's physics uses (see Docs/Blender.md).
"""
import math
import os
import sys
import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib
from lib import tube, ellipsoid, sweep, box, ss, material, Bone, build_armature, make_object

lib.reset_scene()

S = 14  # sides of the main limbs

# ---------------------------------------------------------------- materials (the names are the roles the game tints)
material('Skin', '#EFBC98', 0.55)
material('Face', '#EFBC98', 0.55)
material('Outfit', '#283B8F', 0.6)
material('Shirt', '#F4F1EA', 0.7)
material('Cap', '#FFC743', 0.35)
material('Strap', '#1E2A4A', 0.45)
material('Lens', '#7FD3F0', 0.15)
material('Hair', '#3A2A20', 0.7)
material('Accent', '#C8553D', 0.5)
material('Metal', '#BFC6CE', 0.3, 0.9)
material('EyeWhite', '#F7F5F0', 0.3)
material('Mouth', '#7A3B35', 0.6)

# ---------------------------------------------------------------- the armature
HEAD_C = (0.0, 0.74, 0.012)
bones = [
    Bone('Hips', (0, 0, 0), (0, 0.1, 0)),
    Bone('Spine', (0, 0.1, 0), (0, 0.73, 0.01), 'Hips'),
    Bone('Head', (0, 0.73, 0.01), (0, 0.89, 0.01), 'Spine'),
]
for side, sx in (('L', -1), ('R', 1)):
    ax, lx = sx * 0.235, sx * 0.1
    bones += [
        Bone(f'Arm_{side}', (ax, 0.5, 0), (ax, 0.23, 0), 'Spine'),
        Bone(f'ForeArm_{side}', (ax, 0.23, 0), (ax, -0.11, 0), f'Arm_{side}'),
        Bone(f'Thigh_{side}', (lx, 0.02, 0), (lx, -0.42, 0), 'Hips'),
        Bone(f'Shin_{side}', (lx, -0.42, 0), (lx, -0.845, 0), f'Thigh_{side}'),
        Bone(f'Ankle_{side}', (lx, -0.845, 0), (lx, -0.845, 0.16), f'Shin_{side}'),
    ]


def weights(kind, p):
    """Which bones move a vertex and how much, from where it sits on the body."""
    x, y, z = p
    side = 'L' if x < 0 else 'R'
    if ':' in kind:
        kind, side = kind.split(':')
    A, F, T, SH, AN = f'Arm_{side}', f'ForeArm_{side}', f'Thigh_{side}', f'Shin_{side}', f'Ankle_{side}'
    if kind in ('torso', 'shirt'):
        hips = 1 - ss(0.02, 0.16, y)
        pull = ss(0.13, 0.2, abs(x)) * ss(0.34, 0.48, y) * 0.5
        A2 = 'Arm_L' if x < 0 else 'Arm_R'
        return {'Hips': hips, 'Spine': (1 - hips) * (1 - pull), A2: (1 - hips) * pull}
    if kind == 'neck':
        h = ss(0.6, 0.72, y)
        return {'Spine': 1 - h, 'Head': h}
    if kind == 'head':
        return {'Head': 1.0}
    if kind == 'spine':
        return {'Spine': 1.0}
    if kind == 'trunks':
        hips = 1 - ss(0.04, 0.12, y)
        return {'Hips': hips, 'Spine': 1 - hips}
    if kind == 'trunksleg':
        return {'Hips': 0.35, T: 0.65}
    if kind == 'arm':          # shoulder to wrist in one piece
        elbow = ss(0.31, 0.17, y)
        spine = 0.4 * ss(0.42, 0.52, y)
        return {A: (1 - spine) * (1 - elbow), F: (1 - spine) * elbow, 'Spine': spine}
    if kind == 'hand':
        return {F: 1.0}
    if kind == 'leg':          # hip to ankle in one piece
        h = 0.5 * ss(-0.08, 0.06, y)
        s = ss(-0.34, -0.50, y)
        a = 0.5 * ss(-0.76, -0.86, y)
        return {'Hips': h, T: (1 - h) * (1 - s), SH: (1 - h) * s * (1 - a), AN: (1 - h) * s * a}
    if kind == 'thigh':        # the right leg is two pieces because of the prosthetic
        h = 0.5 * ss(-0.08, 0.06, y)
        s = 0.5 * ss(-0.34, -0.46, y)
        return {'Hips': h, T: 1 - h - s, SH: s}
    if kind == 'shin':
        t = 0.5 * ss(-0.6, -0.4, y)
        a = 0.5 * ss(-0.76, -0.86, y)
        return {T: t, SH: 1 - t - a, AN: a}
    if kind == 'foot':
        return {AN: 1.0}
    return {'Spine': 1.0}


def fat(kind, p):
    """The 'Fat' shape key: a rounder middle, wider arms and legs (authored as +50 % width)."""
    x, y, z = p
    base = kind.split(':')[0]
    if base in ('torso', 'trunks', 'shirt'):
        bump = math.exp(-((y - 0.1) / 0.24) ** 2)
        belly = 0.05 * bump if z > 0 else 0.0
        return (x * (1 + 0.5 * bump), y, z * (1 + 0.8 * bump) + belly)
    if base == 'trunksleg':
        return (x * 1.45, y, z * 1.25)
    if base in ('arm', 'hand'):
        ax = (-1 if x < 0 else 1) * 0.235
        return (ax * 1.45 + (x - ax) * 1.25, y, z * 1.25)
    if base in ('leg', 'thigh', 'shin', 'foot'):
        ax = (-1 if x < 0 else 1) * 0.1
        return (ax * 1.5 + (x - ax) * 1.3, y, z * 1.2)
    if base == 'ring':
        return (x * 1.5, y, z * 1.38)
    if base == 'head':
        return (x * 1.06, y, z)
    return (x, y, z)


def limb_side(sx):
    return 'L' if sx < 0 else 'R'


# ---------------------------------------------------------------- the body
body = []

# Torso: broad shoulders, a waist, rounded hips.
torso_rows = lib.densify([
    (-0.03, .145, .100), (0.03, .163, .108), (0.12, .138, .092), (0.24, .152, .098), (0.34, .180, .108),
    (0.44, .202, .114), (0.51, .192, .102), (0.56, .135, .082)], 0.06)
body.append(tube(torso_rows, 'y', 16, dome0=2, dome1=2, depth0=0.06, depth1=0.05, mat='Skin', kind='torso'))
# Neck and head.
body.append(tube([(0.52, .060, .062), (0.62, .055, .058), (0.69, .056, .060)], 'y', 12, mat='Face', kind='neck'))
body.append(lib.smooth_head(HEAD_C, 0.128, 0.155, 0.14))
for sx in (-1, 1):
    body.append(ellipsoid((sx * 0.131, 0.742, -0.006), (0.012, 0.034, 0.024), 'Face', 'head', 8, 5, rot=(0, 0, -sx * 8)))
body.append(ellipsoid((0, 0.726, 0.15), (0.019, 0.026, 0.025), 'Face', 'head', 8, 5, rot=(-12, 0, 0)))           # nose
body.append(sweep([(-0.03, 0.678, 0.133), (-0.012, 0.668, 0.141), (0.012, 0.668, 0.141), (0.03, 0.678, 0.133)],
                  0.0035, 0.0045, ref=(0, 0, 1), sides=6, mat='Mouth', kind='head'))                          # a small smile
for sx in (-1, 1):
    body.append(ellipsoid((sx * 0.05, 0.802, 0.13), (0.03, 0.0055, 0.01), 'Hair', 'head', 8, 4, rot=(0, 0, -sx * 9)))  # brows

# Arms (one piece each, so the elbow bends smoothly) and mitten hands.
arm_rows = lib.densify([(0.5, .060, .058), (0.42, .057, .055), (0.33, .050, .049), (0.23, .045, .045), (0.14, .042, .040),
                        (0.04, .036, .034), (-0.01, .033, .030)], 0.05)
for sx in (-1, 1):
    side = limb_side(sx)
    ax = sx * 0.235
    inner = -sx
    body.append(tube(arm_rows, 'y', S, dome0=2, dome1=1, depth0=0.055, depth1=0.02, mat='Skin', kind='arm:' + side).moved((ax, 0, 0)))
    body.append(ellipsoid((ax, -0.062, 0), (0.021, 0.05, 0.03), 'Skin', 'hand:' + side, 10, 6))
    body.append(ellipsoid((ax + inner * 0.015, -0.05, 0.03), (0.01, 0.026, 0.012), 'Skin', 'hand:' + side, 8, 5, rot=(-30, 0, 0)))

# Legs: the left one in one piece; the right one in two, so a blade can take the lower half.
leg_rows = [(0.02, .096, .098), (-0.08, .093, .095), (-0.2, .080, .082), (-0.34, .065, .068), (-0.42, .059, .061),
            (-0.5, .055, .061, 0, -0.005), (-0.6, .051, .058, 0, -0.011), (-0.72, .041, .044, 0, -0.006), (-0.82, .037, .039),
            (-0.845, .036, .037)]


def foot_shapes(sx, kind):
    lx = sx * 0.1
    foot = tube([(-0.075, .038, .031), (-0.03, .042, .034), (0.05, .046, .028), (0.12, .049, .023), (0.15, .042, .018)],
                'z', 12, dome0=2, dome1=2, depth0=0.03, depth1=0.022, mat='Skin', kind=kind)
    foot.moved((lx, -0.852, 0))
    shapes = [foot]
    toe_x = [lx + sx * 0.028, lx + sx * 0.014, lx, lx - sx * 0.014, lx - sx * 0.029]
    for k, tx in enumerate(toe_x):
        shapes.append(ellipsoid((tx, -0.862, 0.152 + (0.004 if k in (0, 4) else 0)), (0.0085 if k else 0.0105, 0.009, 0.014),
                                'Skin', kind, 6, 4))
    return shapes


body.append(tube(lib.densify(leg_rows, 0.05), 'y', S, dome0=2, dome1=1, depth0=0.06, depth1=0.02, mat='Skin',
                 kind='leg:L').moved((-0.1, 0, 0)))
body += foot_shapes(-1, 'foot:L')
body.append(tube(lib.densify(leg_rows[:5] + [(-0.45, .058, .06)], 0.05), 'y', S, dome0=2, dome1=2, depth0=0.06, depth1=0.045,
                 mat='Skin', kind='thigh:R').moved((0.1, 0, 0)))
right_lower = [tube(lib.densify(leg_rows[4:], 0.05), 'y', S, dome0=2, dome1=1, depth0=0.05, depth1=0.02, mat='Skin',
                    kind='shin:R').moved((0.1, 0, 0))] + foot_shapes(1, 'foot:R')

# Trunks.
trunks = [tube(lib.densify([(0.075, .167, .119), (0.04, .172, .122), (0.0, .170, .120), (-0.035, .140, .100)], 0.04), 'y', 16,
               dome1=2, depth1=0.03, mat='Outfit', kind='trunks')]
for sx in (-1, 1):
    # each leg of the trunks starts inside the pelvis with a rounded top, so the two legs flow out of one waist
    trunks.append(tube([(-0.02, .104, .105), (-0.1, .103, .104), (-0.165, .100, .101)], 'y', 12, dome0=2, depth0=0.07, dome1=1,
                       depth1=0.012, mat='Outfit', kind='trunksleg:' + limb_side(sx)).moved((sx * 0.1, 0, 0)))
trunks.append(ellipsoid((0, -0.05, 0), (0.135, 0.085, 0.105), 'Outfit', 'trunks', 14, 6))     # fills the crotch between the legs
trunks.append(tube([(0.072, .173, .124), (0.092, .173, .124)], 'y', 16, mat='Outfit', kind='trunks', closed=False))

# ---------------------------------------------------------------- props
gear = {}   # object name -> list of shapes

# Swim cap: a dome that covers the top of the head.
cap = ellipsoid(HEAD_C, (0.137, 0.163, 0.149), 'Cap', 'head', 16, 8, cut=(-8, 90), rot=(-14, 0, 0))
cap.moved((0, 0.004, -0.004))
gear['Gear_SwimCap'] = [cap]

# Goggles.
gear['Gear_Goggles_Mask'] = [tube([(0.757, .134, .147, 0, 0.012), (0.775, .134, .147, 0, 0.012)], 'y', 20,
                                   mat='Strap', kind='head', closed=False)]
gl = []
for sx in (-1, 1):
    gl.append(ellipsoid((sx * 0.05, 0.762, 0.147), (0.045, 0.039, 0.02), 'Strap', 'head', 12, 6, rot=(-4, 0, 0)))
    gl.append(ellipsoid((sx * 0.05, 0.762, 0.153), (0.036, 0.03, 0.016), 'Lens', 'head', 12, 6, rot=(-4, 0, 0)))
gl.append(box((0, 0.766, 0.158), (0.03, 0.014, 0.012), 0.004, 1, 'Strap', 'head'))
gear['Gear_Goggles'] = gl

# Eyes: white, with an iris in the role colour.
eyes = []
for sx in (-1, 1):
    eyes.append(ellipsoid((sx * 0.047, 0.757, 0.133), (0.0215, 0.024, 0.016), 'EyeWhite', 'head', 12, 6))
    eyes.append(ellipsoid((sx * 0.047, 0.757, 0.146), (0.012, 0.0135, 0.007), 'Lens', 'head', 10, 5))
gear['Gear_Eyes'] = eyes

# Hair: a close shell, a swept fringe, sideburns and the nape.
hair = [ellipsoid(HEAD_C, (0.14, 0.166, 0.153), 'Hair', 'head', 16, 8, cut=(10, 90), rot=(-33, 0, 0)).moved((0, 0.003, -0.01)),
        ellipsoid((0, 0.725, -0.085), (0.128, 0.105, 0.075), 'Hair', 'head', 14, 7, rot=(8, 0, 0))]
hair.append(ellipsoid((0.025, 0.872, 0.1), (0.078, 0.024, 0.052), 'Hair', 'head', 12, 5, rot=(-38, 0, -14)))
hair.append(ellipsoid((-0.065, 0.862, 0.092), (0.05, 0.022, 0.042), 'Hair', 'head', 10, 5, rot=(-32, 0, 16)))
for sx in (-1, 1):
    hair.append(ellipsoid((sx * 0.126, 0.752, 0.015), (0.014, 0.052, 0.04), 'Hair', 'head', 8, 5))
gear['Gear_Hair'] = hair

# Beard and moustache.
gear['Gear_Beard'] = [ellipsoid((0, 0.652, 0.082), (0.074, 0.055, 0.062), 'Hair', 'head', 12, 6, rot=(-14, 0, 0)),
                      ellipsoid((0, 0.688, 0.145), (0.042, 0.012, 0.018), 'Hair', 'head', 10, 5),
                      ellipsoid((-0.1, 0.69, 0.03), (0.022, 0.06, 0.05), 'Hair', 'head', 8, 5),
                      ellipsoid((0.1, 0.69, 0.03), (0.022, 0.06, 0.05), 'Hair', 'head', 8, 5)]

# Ponytail.
gear['Gear_Ponytail'] = [
    sweep([(0, 0.8, -0.14), (0, 0.78, -0.2), (0, 0.7, -0.245), (0, 0.6, -0.25), (0, 0.5, -0.225)],
          [0.05, 0.055, 0.052, 0.045, 0.014], [0.05, 0.055, 0.052, 0.045, 0.014], ref=(1, 0, 0), sides=10,
          mat='Hair', kind='head'),
    ellipsoid((0, 0.8, -0.15), (0.05, 0.035, 0.035), 'Accent', 'head', 8, 5)]

# Pirate hat: a bicorne with a turned-up brim and a skull badge.
hat = [ellipsoid((0, 0.87, -0.005), (0.16, 0.085, 0.145), 'Accent', 'head', 16, 7, rot=(-6, 0, 0)),
       ellipsoid((0, 0.822, 0.0), (0.245, 0.014, 0.185), 'Accent', 'head', 20, 5)]
for sx in (-1, 1):
    hat.append(ellipsoid((sx * 0.215, 0.865, 0.0), (0.075, 0.013, 0.17), 'Accent', 'head', 14, 5, rot=(0, 0, sx * 38)))
hat.append(ellipsoid((0, 0.885, 0.135), (0.032, 0.03, 0.01), 'Shirt', 'head', 8, 4, rot=(-12, 0, 0)))
gear['Gear_PirateHat'] = hat

# Eyepatch and its band.
gear['Gear_Eyepatch'] = [ellipsoid((0.047, 0.757, 0.15), (0.038, 0.034, 0.013), 'Strap', 'head', 12, 6, rot=(-4, 0, 0)),
                         tube([(0.77, .134, .147, 0, 0.012), (0.784, .134, .147, 0, 0.012)], 'y', 20, mat='Strap', kind='head',
                              closed=False).rotated((-18, 0, 22), (0, 0.77, 0.012))]

# Sunglasses.
sun = [box((sx * 0.054, 0.764, 0.154), (0.076, 0.048, 0.016), 0.014, 2, 'Strap', 'head') for sx in (-1, 1)]
sun.append(box((0, 0.772, 0.157), (0.034, 0.008, 0.01), 0.003, 1, 'Strap', 'head'))
for sx in (-1, 1):
    sun.append(sweep([(sx * 0.094, 0.768, 0.15), (sx * 0.128, 0.768, 0.1), (sx * 0.136, 0.762, 0.02)], 0.005, 0.005, sides=6,
                     mat='Strap', kind='head'))
gear['Gear_Sunglasses'] = sun

# Mask for diving.
gear['Gear_Mask'] = [box((0, 0.762, 0.144), (0.2, 0.1, 0.05), 0.026, 2, 'Strap', 'head'),
                     box((0, 0.762, 0.166), (0.158, 0.068, 0.02), 0.014, 2, 'Lens', 'head'),
                     ellipsoid((0, 0.718, 0.162), (0.022, 0.02, 0.018), 'Strap', 'head', 8, 4)]

# Shirt: body with short sleeves and a collar.
shirt = [tube(lib.densify([(-0.07, .170, .122), (0.0, .176, .126), (0.12, .150, .104), (0.24, .164, .110), (0.34, .192, .120),
                           (0.44, .214, .126), (0.51, .204, .113), (0.55, .15, .092)], 0.06), 'y', 16, dome1=1, depth1=0.03,
              mat='Shirt', kind='shirt'),
         tube([(0.52, .08, .08), (0.56, .074, .074)], 'y', 14, mat='Shirt', kind='shirt', closed=False)]
for sx in (-1, 1):
    shirt.append(tube([(0.53, .072, .07), (0.46, .07, .068), (0.4, .066, .064), (0.345, .064, .062)], 'y', 12,
                      dome0=1, depth0=0.03, mat='Shirt', kind='arm:' + limb_side(sx)).moved((sx * 0.235, 0, 0)))
gear['Gear_Shirt'] = shirt

# Backpack.
bag = [box((0, 0.24, -0.185), (0.3, 0.36, 0.13), 0.04, 2, 'Accent', 'spine'),
       box((0, 0.13, -0.26), (0.22, 0.14, 0.05), 0.02, 2, 'Accent', 'spine'),
       box((0, 0.4, -0.2), (0.28, 0.06, 0.1), 0.025, 2, 'Strap', 'spine')]
for sx in (-1, 1):
    bag.append(sweep([(sx * 0.1, 0.4, -0.12), (sx * 0.105, 0.52, -0.08), (sx * 0.11, 0.575, 0.0), (sx * 0.115, 0.53, 0.085),
                      (sx * 0.12, 0.38, 0.122), (sx * 0.12, 0.2, 0.125)], 0.02, 0.008, ref=(1, 0, 0), sides=8,
                     mat='Strap', kind='spine'))
gear['Gear_Backpack'] = bag

# Scuba tanks with a harness.
tanks = []
for sx in (-1, 1):
    tanks.append(tube([(0.0, .068, .068), (0.12, .07, .07), (0.3, .07, .07), (0.36, .068, .068)], 'y', 14,
                      dome0=3, dome1=3, depth0=0.07, depth1=0.07, mat='Accent', kind='spine').moved((sx * 0.085, 0.02, -0.19)))
    tanks.append(sweep([(sx * 0.11, 0.3, -0.115), (sx * 0.12, 0.52, -0.09), (sx * 0.115, 0.575, 0.0), (sx * 0.12, 0.53, 0.09),
                        (sx * 0.125, 0.3, 0.127), (sx * 0.12, 0.1, 0.12)], 0.022, 0.008, ref=(1, 0, 0), sides=8,
                       mat='Strap', kind='spine'))
tanks.append(box((0, 0.4, -0.19), (0.07, 0.05, 0.05), 0.012, 1, 'Metal', 'spine'))
tanks.append(tube([(0.06, .185, .13), (0.085, .185, .13)], 'y', 16, mat='Strap', kind='torso', closed=False))
gear['Gear_Tank'] = tanks

# Fins for the diver.
fins = []
for sx in (-1, 1):
    fins.append(tube([(-0.09, .042, .024), (0.0, .05, .018), (0.16, .06, .012), (0.3, .085, .008), (0.43, .108, .005)], 'z', 12,
                     dome0=1, dome1=1, depth0=0.02, depth1=0.01, mat='Accent', kind='foot:' + limb_side(sx))
                .moved((sx * 0.1, -0.876, 0)))
gear['Gear_Tank_Mask_Fins'] = fins

# Swim ring.
R, r = 0.31, 0.075
ring_shapes = [sweep([(R * math.cos(2 * math.pi * i / 28), 0.03, R * 0.86 * math.sin(2 * math.pi * i / 28)) for i in range(28)],
                     r, r, ref=(0, 1, 0), sides=10, mat='Accent', kind='ring', loop=True)]
for (bx, bz, w, d) in [(0, -R * 0.86, 0.07, 0.1), (0, R * 0.86, 0.07, 0.1), (R, 0, 0.1, 0.07), (-R, 0, 0.1, 0.07)]:
    ring_shapes.append(box((bx, 0.03, bz), (w, 0.17, d), 0.02, 1, 'Shirt', 'ring'))
gear['Gear_SwimRing'] = ring_shapes

# Prosthetic blade for the right leg: a socket below the knee and a carbon spring.
gear['Gear_Blade'] = [
    tube([(-0.42, .064, .066), (-0.5, .058, .06), (-0.56, .05, .05)], 'y', 14, dome0=2, dome1=1, depth0=0.05, depth1=0.02,
         mat='Metal', kind='shin:R').moved((0.1, 0, 0)),
    sweep([(0.1, -0.54, 0.0), (0.1, -0.66, -0.03), (0.1, -0.78, -0.035), (0.1, -0.85, -0.005), (0.1, -0.872, 0.07),
           (0.1, -0.874, 0.15)], 0.018, 0.0055, ref=(1, 0, 0), sides=8, mat='Metal', kind='shin:R'),
    box((0.1, -0.874, 0.11), (0.05, 0.012, 0.1), 0.004, 1, 'Metal', 'foot:R')]

# ---------------------------------------------------------------- build the scene
armature = build_armature('Armature', bones)
objects = [armature]
objects.append(make_object('Body', body, armature, weights, fat))
objects.append(make_object('Trunks', trunks, armature, weights, fat))
objects.append(make_object('NoGear_Blade', right_lower, armature, weights, fat))
for name, shapes in gear.items():
    objects.append(make_object(name, shapes, armature, weights, fat))

out_dir = os.path.join(lib.PROJECT, 'Assets', '_Project', 'Models', 'Characters')
if '--no-export' not in sys.argv:
    lib.export_fbx(os.path.join(out_dir, 'Diver.fbx'), objects)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(lib.ROOT, 'Diver.blend'))

# ---------------------------------------------------------------- previews
if '--no-preview' not in sys.argv:
    cam = lib.setup_render(640)
    for o in objects:
        if o.name.startswith('Gear_'):
            o.hide_render = o.name not in ('Gear_SwimCap', 'Gear_Goggles', 'Gear_Goggles_Mask')
    lib.render_views(cam, 'diver_front', (0, 0.0, 0), 3.4, [('front', (0, 0.1, 1)), ('side', (1, 0.1, 0)), ('back', (0, 0.1, -1))])
    for o in objects:
        if o.name.startswith('Gear_'):
            o.hide_render = o.name not in ('Gear_Hair', 'Gear_Eyes', 'Gear_Shirt', 'Gear_Beard')
    lib.render_views(cam, 'diver_head', (0, 0.74, 0.0), 1.2, [('face', (0.3, 0.1, 1)), ('face2', (-0.5, 0.2, 1))])
    for o in objects:
        if o.name.startswith('Gear_'):
            o.hide_render = o.name in ('Gear_SwimCap', 'Gear_Goggles', 'Gear_Goggles_Mask', 'Gear_Eyes', 'Gear_Mask', 'Gear_Sunglasses',
                                       'Gear_Eyepatch', 'Gear_Hair', 'Gear_Beard', 'Gear_Ponytail', 'Gear_Blade', 'Gear_Shirt',
                                       'Gear_Backpack', 'Gear_Tank_Mask_Fins', 'Gear_PirateHat')
    lib.render_views(cam, 'diver_ring', (0, 0.0, 0), 3.4, [('front', (0.5, 0.2, 1))])
print('DONE', len(objects), 'objects')
