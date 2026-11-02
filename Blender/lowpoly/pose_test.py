"""Bend the robot and the swimmer the same way and render both from the side: does the robot stay in one piece?
    blender -b --python Blender/lowpoly/pose_test.py
"""
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.normpath(os.path.join(HERE, '..')))
import finish  # noqa: E402
import heroes  # noqa: E402
import robot  # noqa: E402
import view  # noqa: E402

for ob in [o for o in bpy.data.objects if o.name in ('Cube', 'Light', 'Camera')]:
    bpy.data.objects.remove(ob, do_unlink=True)
heroes.materials()
finish.finish('swimmer')
robot.build()
for o in bpy.data.objects:
    if o.type == 'MESH':
        o.hide_render = o.name.endswith('_dense') or o.name.startswith('LP_swimmer') and False
bpy.data.objects['LP_robot_rig'].location.x = 1.0
bpy.data.objects['LP_swimmer_rig'].location.x = -1.0
POSES = {'tuck': [('Spine', 40), ('Thigh_L', -100), ('Thigh_R', -100), ('Shin_L', 120), ('Shin_R', 120), ('Arm_L', -60), ('Arm_R', -60)],
         'open': [('Spine', -9), ('Thigh_L', -22), ('Thigh_R', -9), ('Shin_L', 44), ('Shin_R', 26), ('Arm_L', -40), ('Arm_R', -40), ('Head', -14)]}
paths = []
for name, ang in POSES.items():
    for k in ('LP_swimmer_rig', 'LP_robot_rig'):
        view.pose(bpy.data.objects[k], ang)
    paths.append(view.shoot(f'pt_{name}', (0, -0.05, 0), 5.0, (0, 0.05, 1), lens=60, res=(900, 640)))
view.compose(paths, os.path.join(view.OUT, 'pose_test.png'))
print('POSE TEST DONE')
