"""The robot: its own body on the same bones as the human, built from rounded plates, joints and pistons.
Run: blender --background --python make_robot.py   ->  Assets/_Project/Models/Characters/Robot.fbx
"""
import math
import os
import sys
import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib
from lib import tube, ellipsoid, sweep, box, material, Bone, build_armature, make_object

lib.reset_scene()

material('Skin', '#AEB6C0', 0.35, 0.8)
material('Face', '#C5CCD4', 0.3, 0.8)
material('Outfit', '#5A6572', 0.5, 0.5)
material('Lens', '#66F0FF', 0.1, 0.0, emit='#66F0FF')
material('Accent', '#FF5E5E', 0.4, 0.2)
material('Metal', '#BFC6CE', 0.3, 0.9)

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
    """Every robot part is rigid: it belongs to one bone ('b:Name')."""
    return {kind.split(':')[1]: 1.0}


def bone(name):
    return 'b:' + name


body = []
# Pelvis and waist.
body.append(box((0, -0.035, 0), (0.34, 0.15, 0.22), 0.04, 2, 'Outfit', bone('Hips')))
body.append(box((0, 0.06, 0), (0.30, 0.08, 0.2), 0.02, 2, 'Metal', bone('Spine')))
# Torso: a rounded block, a chest plate with a glowing core and three lights.
body.append(box((0, 0.31, 0), (0.40, 0.5, 0.25), 0.05, 3, 'Skin', bone('Spine')))
body.append(box((0, 0.35, 0.123), (0.27, 0.24, 0.02), 0.012, 2, 'Outfit', bone('Spine')))
body.append(ellipsoid((0, 0.375, 0.135), (0.05, 0.05, 0.016), 'Lens', bone('Spine'), 14, 6))
for i, x in enumerate((-0.07, 0.0, 0.07)):
    body.append(ellipsoid((x, 0.3, 0.134), (0.015, 0.015, 0.008), 'Accent', bone('Spine'), 8, 4))
for y in (0.17, 0.2, 0.23):
    body.append(box((0, y, 0.126), (0.2, 0.01, 0.01), 0.002, 1, 'Metal', bone('Spine')))
body.append(box((0, 0.52, 0), (0.34, 0.06, 0.2), 0.02, 2, 'Metal', bone('Spine')))   # shoulder yoke
# Backpack unit.
body.append(box((0, 0.3, -0.16), (0.26, 0.3, 0.07), 0.03, 2, 'Outfit', bone('Spine')))
for sx in (-1, 1):
    body.append(tube([(0.2, .028, .028), (0.42, .028, .028)], 'y', 8, dome0=1, dome1=1, depth0=0.02, depth1=0.02,
                     mat='Metal', kind=bone('Spine')).moved((sx * 0.07, 0, -0.205)))
# Neck.
body.append(tube([(0.55, .05, .05), (0.66, .045, .045)], 'y', 12, mat='Outfit', kind=bone('Spine')))
body.append(tube([(0.64, .05, .05), (0.69, .05, .05)], 'y', 12, mat='Metal', kind=bone('Head')))
# Head: a rounded box with a dark face plate and ear discs.
body.append(box((0, 0.76, 0.005), (0.27, 0.25, 0.25), 0.06, 3, 'Face', bone('Head')))
body.append(box((0, 0.765, 0.118), (0.2, 0.12, 0.02), 0.02, 2, 'Outfit', bone('Head')))
for sx in (-1, 1):
    body.append(tube([(0.128, .05, .05), (0.15, .05, .05)], 'x', 14, dome1=1, depth1=0.01, mat='Metal',
                     kind=bone('Head')).moved((0, 0.76, 0.0)).scaled((sx, 1, 1)))
body.append(box((0, 0.875, 0.0), (0.12, 0.012, 0.12), 0.004, 1, 'Metal', bone('Head')))

# Arms: shoulder ball, upper arm, elbow ball, forearm and a blocky hand.
for sx in (-1, 1):
    side = 'L' if sx < 0 else 'R'
    ax = sx * 0.235
    body.append(ellipsoid((ax, 0.5, 0), (0.075, 0.075, 0.075), 'Outfit', bone('Spine'), 14, 8))
    body.append(tube([(0.5, .052, .052), (0.4, .05, .05), (0.25, .048, .048)], 'y', 14, mat='Skin', kind=bone('Arm_' + side)).moved((ax, 0, 0)))
    body.append(tube([(0.42, .056, .056), (0.34, .056, .056)], 'y', 14, mat='Metal', kind=bone('Arm_' + side)).moved((ax, 0, 0)))
    body.append(ellipsoid((ax, 0.23, 0), (0.056, 0.056, 0.056), 'Outfit', bone('Arm_' + side), 12, 7))
    body.append(tube([(0.23, .046, .046), (0.13, .044, .044), (0.0, .04, .04)], 'y', 14, dome1=1, depth1=0.02, mat='Skin',
                     kind=bone('ForeArm_' + side)).moved((ax, 0, 0)))
    body.append(box((ax, -0.065, 0), (0.07, 0.11, 0.085), 0.022, 2, 'Metal', bone('ForeArm_' + side)))
    body.append(box((ax - sx * 0.0, -0.09, 0.052), (0.05, 0.06, 0.025), 0.01, 1, 'Metal', bone('ForeArm_' + side)))

# Legs: thigh, knee ball, shin with a piston, ankle ball and a plated foot.
for sx in (-1, 1):
    side = 'L' if sx < 0 else 'R'
    lx = sx * 0.1
    body.append(tube([(0.02, .08, .08), (-0.15, .075, .075), (-0.36, .066, .066)], 'y', 14, dome0=2, depth0=0.05, mat='Skin',
                     kind=bone('Thigh_' + side)).moved((lx, 0, 0)))
    body.append(ellipsoid((lx, -0.42, 0), (0.07, 0.07, 0.07), 'Outfit', bone('Thigh_' + side), 12, 7))
    body.append(tube([(-0.42, .058, .058), (-0.55, .056, .056), (-0.78, .046, .046)], 'y', 14, mat='Skin',
                     kind=bone('Shin_' + side)).moved((lx, 0, 0)))
    body.append(tube([(-0.5, .012, .012), (-0.76, .012, .012)], 'y', 8, mat='Metal', kind=bone('Shin_' + side)).moved((lx, 0, 0.07)))
    body.append(ellipsoid((lx, -0.845, 0), (0.05, 0.05, 0.05), 'Outfit', bone('Ankle_' + side), 12, 7))
    body.append(box((lx, -0.855, 0.04), (0.1, 0.05, 0.24), 0.02, 2, 'Metal', bone('Ankle_' + side)))
    body.append(box((lx, -0.835, 0.095), (0.07, 0.02, 0.1), 0.008, 1, 'Outfit', bone('Ankle_' + side)))

gear = {}
# Eyes (shown when the character has Eyes): two glowing lenses on the face plate.
gear['Gear_Eyes'] = [ellipsoid((sx * 0.052, 0.77, 0.132), (0.028, 0.028, 0.012), 'Lens', bone('Head'), 12, 6) for sx in (-1, 1)]
# Antenna.
gear['Gear_Antenna'] = [tube([(0.875, .008, .008), (0.96, .008, .008)], 'y', 8, mat='Metal', kind=bone('Head')),
                        ellipsoid((0, 0.98, 0), (0.026, 0.026, 0.026), 'Accent', bone('Head'), 10, 6)]

armature = build_armature('Armature', bones)
objects = [armature, make_object('Body', body, armature, weights)]
for name, shapes in gear.items():
    objects.append(make_object(name, shapes, armature, weights))

out_dir = os.path.join(lib.PROJECT, 'Assets', '_Project', 'Models', 'Characters')
if '--no-export' not in sys.argv:
    lib.export_fbx(os.path.join(out_dir, 'Robot.fbx'), objects)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(lib.ROOT, 'Robot.blend'))

if '--no-preview' not in sys.argv:
    cam = lib.setup_render(640)
    lib.render_views(cam, 'robot', (0, 0.0, 0), 3.4, [('front', (0.35, 0.1, 1)), ('side', (1, 0.1, 0)), ('back', (-0.4, 0.2, -1))])
print('DONE', len(objects))
