"""Export every hero body (with its kit of clothes and gear) to Assets/_Project/Models/Characters/<Name>.fbx.

Objects are renamed to the names the game looks for while they are exported, then given their working names back:
  LP_<kind> -> Body, LP_<kind>_eyes -> Gear_Eyes, <kind>.<Name> -> <Name>, the rig -> Armature.
On the swimmer's body the trunks are NoGear_Tank_Mask (the diver's wetsuit replaces them).
"""
import os
import sys

import bpy

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
sys.path.insert(0, ROOT)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib  # noqa: E402

OUT = os.path.normpath(os.path.join(ROOT, '..', 'Assets', '_Project', 'Models', 'Characters'))
FILES = {'swimmer': 'Swimmer', 'kid': 'Kid', 'fat': 'Fat', 'old': 'Old', 'woman': 'Woman', 'robot': 'Robot'}
RENAME = {('swimmer', 'Trunks'): 'NoGear_Tank_Mask'}


def limit_weights(ob, limit=4):
    ob.data.update()
    bpy.context.view_layer.objects.active = ob
    with bpy.context.temp_override(object=ob, active_object=ob, selected_objects=[ob], selected_editable_objects=[ob]):
        bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=limit)
        bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)


def export(kind):
    name = FILES[kind]
    rig = bpy.data.objects[f'LP_{kind}_rig']
    pairs = [(bpy.data.objects[f'LP_{kind}'], 'Body'), (bpy.data.objects[f'LP_{kind}_eyes'], 'Gear_Eyes')]
    for o in bpy.data.objects:
        if o.name.startswith(f'{kind}.'):
            base = o['base'] if 'base' in o else o.name.split('.', 1)[1]
            pairs.append((o, RENAME.get((kind, base), base)))
    meshes = [o for o, _ in pairs]
    for o in meshes + [rig]:
        o.hide_viewport = False
        o.hide_render = False
        o.hide_set(False)
    for o in meshes:
        limit_weights(o)
    home = rig.location.copy()
    rig.location = (0, 0, 0)  # the heroes may be standing in a row for review; the FBX must have the body at the origin
    bpy.context.view_layer.update()
    saved = [(o, o.name) for o in meshes + [rig]]
    for o, _ in saved:
        o.name = o.name + '__tmp'  # free the final names first, they may belong to another body's objects
    for o, final in pairs:
        o.name = final
    rig.name = 'Armature'
    rig.data.name = 'Armature'
    try:
        path = os.path.join(OUT, f'{name}.fbx')
        lib.export_fbx(path, meshes + [rig])
    finally:
        for o, old in saved:
            o.name = old
        rig.data.name = f'LP_{kind}_rig'
        rig.location = home
    return path, sorted(final for _, final in pairs)


def export_all(kinds=None):
    out = {}
    for kind in kinds or FILES:
        path, names = export(kind)
        out[kind] = (os.path.getsize(path), names)
    return out
