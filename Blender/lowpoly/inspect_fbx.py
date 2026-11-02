"""Print the skeleton of exported character FBX files (bone parents, positions) and which bones each mesh is weighted to.
    blender -b --python Blender/lowpoly/inspect_fbx.py -- Swimmer Robot
"""
import os
import sys

import bpy

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', '_Project', 'Models', 'Characters'))
names = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else ['Swimmer', 'Robot']
for n in names:
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT, f'{n}.fbx'))
    print('=====', n)
    for o in bpy.data.objects:
        if o.type == 'ARMATURE':
            print('ARMATURE', o.name, 'loc', tuple(round(v, 3) for v in o.location), 'scale', tuple(round(v, 3) for v in o.scale), 'rot', tuple(round(v, 3) for v in o.rotation_euler))
            for b in o.data.bones:
                print('  bone', b.name, 'parent', b.parent.name if b.parent else None, 'head', tuple(round(v, 3) for v in b.head_local), 'tail', tuple(round(v, 3) for v in b.tail_local))
        elif o.type == 'MESH':
            groups = sorted({o.vertex_groups[g.group].name for v in o.data.vertices for g in v.groups})
            print('MESH', o.name, 'parent', o.parent.name if o.parent else None, 'verts', len(o.data.vertices), 'groups', groups)
