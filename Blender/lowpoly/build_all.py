"""Rebuild every hero from scratch, render review pictures and (optionally) export the FBX files.

Run live (execute_blender_code: import build_all; build_all.run()) or headless:
    blender -b --python Blender/lowpoly/build_all.py -- [--export]
"""
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.normpath(os.path.join(HERE, '..')))
import lib  # noqa: E402
import finish  # noqa: E402
import heroes  # noqa: E402
import robot  # noqa: E402
import export  # noqa: E402
import view  # noqa: E402

KINDS = ('swimmer', 'kid', 'fat', 'old', 'woman')
SHOW = {'swimmer': ['Trunks', 'Gear_SwimCap', 'Gear_Goggles'], 'kid': ['Trunks', 'Gear_Hair', 'Gear_Shirt', 'Gear_Backpack'],
        'fat': ['Trunks', 'Gear_SwimRing', 'Gear_Sunglasses'], 'old': ['Trunks', 'Gear_Hair', 'Gear_Beard', 'Gear_Shirt'],
        'woman': ['Trunks', 'Gear_Hair', 'Gear_Ponytail', 'Gear_Shirt'], 'robot': ['Gear_Antenna']}
ROW = dict(zip(SHOW, (-3.0, -1.8, -0.6, 0.6, 1.8, 3.0)))


def clean_default_scene():
    for ob in [o for o in bpy.data.objects if o.name in ('Cube', 'Light', 'Camera')]:
        bpy.data.objects.remove(ob, do_unlink=True)


def row(show_all=False):
    """Stand the heroes in a row with their normal kit; everything else hidden."""
    shown = set()
    for kind, names in SHOW.items():
        shown |= {f'LP_{kind}', f'LP_{kind}_eyes', f'LP_{kind}_rig'} | {f'{kind}.{n}' for n in names}
        if kind == 'swimmer':
            shown.add('swimmer.NoGear_Blade')
    mine = tuple(f'LP_{k}' for k in SHOW) + tuple(f'{k}.' for k in SHOW)
    for o in bpy.data.objects:
        if o.type in ('MESH', 'ARMATURE') and o.name.startswith(mine):
            vis = o.name in shown
            o.hide_render = (not vis) or o.name.endswith('_dense')
            o.hide_viewport = not vis
    for kind, x in ROW.items():
        bpy.data.objects[f'LP_{kind}_rig'].location.x = x
    bpy.context.view_layer.update()


def run(do_export=False, pictures=True):
    heroes.materials()
    for kind in KINDS:
        finish.finish(kind)
        heroes.dress(kind)
    robot.build()
    row()
    if pictures:
        tag = 'build'
        view.shoot(f'{tag}_front', (0, -0.05, 0), 9.0, (0, 0.05, 1), lens=60, res=(2000, 560))
        view.shoot(f'{tag}_back', (0, -0.05, 0), 9.0, (0.1, 0.1, -1), lens=60, res=(2000, 560))
        paths = []
        for kind in ('fat', 'old', 'woman', 'kid'):
            top = max(v.co.z for v in bpy.data.objects[f'LP_{kind}'].data.vertices)
            for d in ((0, 0.05, 1), (0.8, 0.1, 0.8)):
                paths.append(view.shoot(f'{tag}_h_{kind}', (-ROW[kind], top - 0.1, 0), 0.75, d, lens=85, res=(380, 380)))
        view.compose(paths, os.path.join(view.OUT, f'{tag}_heads.png'))
        for kind in ('fat', 'swimmer'):
            top = 0.1
            ps = [view.shoot(f'{tag}_t_{kind}{i}', (-ROW[kind], top, 0), 1.7, d, lens=70, res=(400, 400)) for i, d in enumerate([(0, 0.05, 1), (1, 0.05, 0.3), (0.3, 0.05, -1)])]
            view.compose(ps, os.path.join(view.OUT, f'{tag}_trunks_{kind}.png'))
    if do_export:
        res = export.export_all(['swimmer', 'kid', 'fat', 'old', 'woman', 'robot'])
        print('EXPORTED', {k: v[0] // 1024 for k, v in res.items()})


if __name__ == '__main__':
    clean_default_scene()
    run(do_export='--export' in sys.argv)
    print('BUILD DONE')
