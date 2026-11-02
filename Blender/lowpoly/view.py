"""Preview helpers for the live scene: Workbench renders with flat studio light, written to Blender/previews."""
import math
import os
import sys

import bpy
from mathutils import Vector

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
sys.path.insert(0, ROOT)
import lib  # noqa: E402

OUT = os.path.join(ROOT, 'previews')


def setup(res=(900, 900), bg=(0.30, 0.31, 0.33)):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.view_settings.view_transform = 'Standard'
    sc.view_settings.look = 'None'
    world = bpy.data.worlds.get('LPWorld') or bpy.data.worlds.new('LPWorld')
    sc.world = world
    world.use_nodes = True
    bgn = next(n for n in world.node_tree.nodes if n.type == 'BACKGROUND')
    bgn.inputs[0].default_value = (0.20, 0.21, 0.24, 1)
    bgn.inputs[1].default_value = 1.0
    for lname, rot, energy in (('LPSun', (math.radians(48), math.radians(8), math.radians(-40)), 3.2), ('LPFill', (math.radians(70), 0, math.radians(150)), 1.1)):
        lo = bpy.data.objects.get(lname)
        if lo is None:
            ld = bpy.data.lights.get(lname) or bpy.data.lights.new(lname, 'SUN')
            lo = bpy.data.objects.new(lname, ld)
        if lo.name not in sc.objects:
            sc.collection.objects.link(lo)
        lo.data.energy = energy
        lo.rotation_euler = rot
    cam = bpy.data.objects.get('LPCam')
    if cam is None or cam.name not in sc.objects:
        data = bpy.data.cameras.get('LPCam') or bpy.data.cameras.new('LPCam')
        cam = bpy.data.objects.get('LPCam') or bpy.data.objects.new('LPCam', data)
        if cam.name not in sc.objects:
            sc.collection.objects.link(cam)
    sc.camera = cam
    return cam


def shoot(name, target, dist, direction, lens=60, res=(900, 900), ortho=None):
    """direction is in Unity axes (x right, y up, z forward): where the camera stands relative to the target."""
    cam = setup(res)
    cam.data.lens = lens
    cam.data.clip_end = 500
    if ortho:
        cam.data.type = 'ORTHO'
        cam.data.ortho_scale = ortho
    else:
        cam.data.type = 'PERSP'
    tgt = Vector(lib.to_blender(target))
    d = Vector(lib.to_blender(direction)).normalized()
    cam.location = tgt + d * dist
    look = (tgt - cam.location).normalized()
    cam.rotation_euler = look.to_track_quat('-Z', 'Y').to_euler()
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, f'{name}.png')
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def pose(rig, angles):
    """Pose test. angles: [(bone, degrees about the world X axis)], applied parent first, about each bone's own head.
    Facing is -Y, so negative angles on the legs and arms swing them forward and positive ones bend the spine forward."""
    from mathutils import Matrix
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='POSE')
    for pb in rig.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
    bpy.context.view_layer.update()
    for name, deg in angles:
        pb = rig.pose.bones[name]
        head = pb.head.copy()
        rot = Matrix.Rotation(math.radians(deg), 4, 'X')
        pb.matrix = Matrix.Translation(head) @ rot @ Matrix.Translation(-head) @ pb.matrix
        bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode='OBJECT')


def compose(paths, out):
    """Put the renders side by side in one image."""
    import numpy as np
    arrs = []
    for p in paths:
        im = bpy.data.images.load(p)
        w, h = im.size
        arrs.append(np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4))
        bpy.data.images.remove(im)
    h = max(a.shape[0] for a in arrs)
    arrs = [np.pad(a, ((0, h - a.shape[0]), (0, 0), (0, 0))) for a in arrs]
    sheet_arr = np.concatenate(arrs, axis=1)
    im = bpy.data.images.new('compose', sheet_arr.shape[1], sheet_arr.shape[0], alpha=True)
    im.pixels.foreach_set(sheet_arr.ravel())
    im.filepath_raw = out
    im.file_format = 'PNG'
    im.save()
    bpy.data.images.remove(im)
    return out


def sheet(prefix, target=(0, 0, 0), dist=3.6, res=(700, 900)):
    paths = []
    for tag, d in (('front', (0, 0.05, 1)), ('side', (1, 0.05, 0)), ('back', (0, 0.05, -1)), ('three', (0.7, 0.25, 0.8))):
        paths.append(shoot(f'{prefix}_{tag}', target, dist, d, res=res))
    return paths
