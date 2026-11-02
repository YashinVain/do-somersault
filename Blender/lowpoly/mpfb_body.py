"""Bodies from MPFB (the MakeHuman add-on), reduced to flat-faced low poly and rigged to the game's 13 bones.

Pipeline for one body:
  1. MPFB makes a human from the macro sliders (gender, age, muscle, weight, height) and its own rig with good skin weights.
  2. The arms are lowered to hang beside the body, and that pose is baked into mesh and skeleton.
  3. MPFB's helper geometry is deleted; the smooth body stays (hidden) as 'LP_<kind>_dense' for cutting clothes.
  4. The ~120 MPFB bones are folded into the game's 13 (Hips, Spine, Head, Arm_L/R, ForeArm_L/R, Thigh_L/R, Shin_L/R, Ankle_L/R) by
     adding their weights, and the new skeleton is placed on the body's real joints.
  5. The body is scaled to the hero's height with the soles at y = -0.875 and the hips at the origin (the game's convention).
  6. A collapse decimate leaves a thousand-odd flat triangles; the head and hands keep more.
Everything below the Unity/Blender axis swap works in Blender space; lib.to_unity gives Unity coordinates.
"""
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector, Quaternion, Matrix

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
sys.path.insert(0, ROOT)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib  # noqa: E402

from bl_ext.blender_org.mpfb.services.humanservice import HumanService  # noqa: E402
from bl_ext.blender_org.mpfb.services.targetservice import TargetService  # noqa: E402

# how each hero's body is shaped: MPFB macro sliders (0..1) and the height the game wants (metres)
BODIES = {
    'swimmer': dict(gender=1.0, age=0.5, muscle=0.62, weight=0.36, proportions=0.55, height=0.55, tall=1.77, target=1900),
    'old': dict(gender=1.0, age=0.88, muscle=0.35, weight=0.6, proportions=0.5, height=0.5, tall=1.72, target=1900),
    'kid': dict(gender=1.0, age=0.19, muscle=0.4, weight=0.4, proportions=0.5, height=0.5, tall=1.45, target=1700),
    'fat': dict(gender=1.0, age=0.5, muscle=0.4, weight=1.0, proportions=0.5, height=0.42, tall=1.68, target=2300, fat_extra=0.075),
    'woman': dict(gender=0.0, age=0.5, muscle=0.55, weight=0.36, proportions=0.55, height=0.55, tall=1.70, target=1900),
}

# MPFB bone -> game bone
FOLD = [
    ('Head', ['head', 'jaw', 'eye', 'oris', 'levator', 'special', 'tongue', 'orbicularis', 'temporalis', 'oculi', 'risorius', 'neck02', 'neck03']),
    ('Spine', ['spine01', 'spine02', 'spine03', 'breast', 'clavicle', 'shoulder01', 'neck01']),
    ('Hips', ['root', 'pelvis', 'spine05', 'spine04']),
    ('Arm', ['upperarm']),
    ('ForeArm', ['lowerarm', 'wrist', 'finger', 'metacarpal']),
    ('Thigh', ['upperleg']),
    ('Shin', ['lowerleg']),
    ('Ankle', ['foot', 'toe']),
]


def game_bone(mpfb_name):
    side = '.L' if mpfb_name.endswith('.L') else '.R' if mpfb_name.endswith('.R') else ''
    for target, prefixes in FOLD:
        for pre in prefixes:
            if mpfb_name.startswith(pre):
                if target in ('Hips', 'Spine', 'Head'):
                    return target
                return f"{target}_{side[1:]}" if side else None
    return None


def wpos(rig, bone, tail=False):
    b = rig.data.bones[bone]
    return rig.matrix_world @ (b.tail_local if tail else b.head_local)


def make_human(kind):
    spec = BODIES[kind]
    macro = {"gender": spec['gender'], "age": spec['age'], "muscle": spec['muscle'], "weight": spec['weight'], "proportions": spec['proportions'],
             "height": spec['height'], "cupsize": 0.5, "firmness": 0.5, "race": {"asian": 0.1, "caucasian": 0.8, "african": 0.1}}
    human = HumanService.create_human(mask_helpers=False, detailed_helpers=True, extra_vertex_groups=True, feet_on_ground=True, scale=0.1, macro_detail_dict=macro)
    human.name = f'MPFB_{kind}'
    TargetService.bake_targets(human)
    rig = HumanService.add_builtin_rig(human, 'default_no_toes', import_weights=True)
    return human, rig


CURL = {1: (6, 10, 8), 2: (8, 14, 10), 3: (9, 15, 11), 4: (10, 16, 12), 5: (11, 17, 13)}  # degrees per finger (1 = thumb) and joint


def curl_fingers(rig):
    """A relaxed hand: every finger joint bent toward the palm (the hands hang with the palms against the thighs), the fingers drawn
    together a little. Must run in pose mode, after the arms are lowered."""
    for side in ('L', 'R'):
        sign = 1.0 if side == 'L' else -1.0  # Blender +x is the left hand; inward is toward the body
        # first draw the fingers together: each first joint turns toward the middle finger's direction (the thumb only half way)
        mid = rig.pose.bones.get(f'finger3-1.{side}')
        if mid is not None:
            target = (Vector(mid.tail) - Vector(mid.head)).normalized()
            for n, share in ((2, 0.9), (4, 0.9), (5, 0.9), (1, 0.45)):
                pb = rig.pose.bones.get(f'finger{n}-1.{side}')
                if pb is None:
                    continue
                head = Vector(pb.head)
                v = (Vector(pb.tail) - head).normalized()
                q = Quaternion().slerp(v.rotation_difference(target), share)
                pb.matrix = Matrix.Translation(head) @ q.to_matrix().to_4x4() @ Matrix.Translation(-head) @ pb.matrix
                bpy.context.view_layer.update()
        for n in range(1, 6):
            for j in range(1, 4):
                pb = rig.pose.bones.get(f'finger{n}-{j}.{side}')
                if pb is None:
                    continue
                head = Vector(pb.head)
                rot = Matrix.Rotation(math.radians(CURL[n][j - 1]) * sign, 4, 'Y')
                pb.matrix = Matrix.Translation(head) @ rot @ Matrix.Translation(-head) @ pb.matrix
                bpy.context.view_layer.update()


def lower_arms(human, rig, angle_out=15.0):
    """Swing both arms down beside the body (a few degrees out), bake the pose into mesh and skeleton."""
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='POSE')
    bpy.context.view_layer.update()
    for side in ('L', 'R'):
        ua = rig.pose.bones[f'upperarm01.{side}']
        head = Vector(ua.head)
        wrist = Vector(rig.pose.bones[f'wrist.{side}'].head)
        cur = (wrist - head).normalized()
        out = 1.0 if side == 'L' else -1.0
        target = Vector((out * math.sin(math.radians(angle_out)), cur.y * 0.4, -math.cos(math.radians(angle_out)))).normalized()
        rot = cur.rotation_difference(target).to_matrix().to_4x4()
        about_head = Matrix.Translation(head) @ rot @ Matrix.Translation(-head)
        ua.matrix = about_head @ ua.matrix
        bpy.context.view_layer.update()
    curl_fingers(rig)
    bpy.ops.object.mode_set(mode='OBJECT')
    # bake: the body takes the pose, the rig keeps it as its rest pose
    for m in human.modifiers:
        if m.type == 'ARMATURE':
            with bpy.context.temp_override(object=human, active_object=human, selected_objects=[human], selected_editable_objects=[human]):
                bpy.ops.object.modifier_apply(modifier=m.name)
            break
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.select_all(action='SELECT')
    bpy.ops.pose.armature_apply()
    bpy.ops.object.mode_set(mode='OBJECT')


def delete_helpers(human):
    gi = {g.name: g.index for g in human.vertex_groups}
    kill = []
    for v in human.data.vertices:
        for g in v.groups:
            if g.group in (gi.get('HelperGeometry', -1), gi.get('JointCubes', -1)) and g.weight > 0.01:
                kill.append(v.index)
                break
    bm = bmesh.new()
    bm.from_mesh(human.data)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in kill], context='VERTS')
    bm.to_mesh(human.data)
    bm.free()


def fold_weights(human, rig):
    """Add the weights of every MPFB bone into the game bone that replaces it."""
    old = {g.index: g.name for g in human.vertex_groups}
    me = human.data
    folded = {}
    for v in me.vertices:
        acc = {}
        for g in v.groups:
            nm = game_bone(old.get(g.group, ''))
            if nm:
                acc[nm] = acc.get(nm, 0.0) + g.weight
        folded[v.index] = acc
    for g in list(human.vertex_groups):
        human.vertex_groups.remove(g)
    groups = {}
    for idx, acc in folded.items():
        tot = sum(acc.values()) or 1.0
        for nm, w in acc.items():
            if w / tot < 0.01:
                continue
            if nm not in groups:
                groups[nm] = human.vertex_groups.new(name=nm)
            groups[nm].add([idx], w / tot, 'REPLACE')


def landmarks(rig):
    """Joint positions of the MPFB skeleton in Blender world space."""
    lm = {}
    for side in ('L', 'R'):
        lm[f'arm_{side}'] = wpos(rig, f'upperarm01.{side}')
        lm[f'fore_{side}'] = wpos(rig, f'lowerarm01.{side}')
        lm[f'wrist_{side}'] = wpos(rig, f'wrist.{side}')
        lm[f'thigh_{side}'] = wpos(rig, f'upperleg01.{side}')
        lm[f'shin_{side}'] = wpos(rig, f'lowerleg01.{side}')
        lm[f'ankle_{side}'] = wpos(rig, f'foot.{side}')
        lm[f'toe_{side}'] = wpos(rig, f'foot.{side}', tail=True)
        lm[f'eye_{side}'] = wpos(rig, f'eye.{side}')
    lm['head'] = wpos(rig, 'head')
    lm['head_tail'] = wpos(rig, 'head', tail=True)
    lm['pelvis'] = (wpos(rig, 'pelvis.L') + wpos(rig, 'pelvis.R')) / 2
    lm['spine'] = wpos(rig, 'spine04')
    lm['jaw'] = wpos(rig, 'jaw')
    return lm


HAND_SCALE = 1.05  # MPFB hands read small at game distance


def fatten(human, lm, amount):
    """MPFB's heaviest weight slider is not heavy enough for the fat man: inflate torso, hips and thighs along their normals (most at the
    belly, more at the front than the back) and move the arms outward so the hands clear the belly. The joints follow."""
    names = {g.index: g.name for g in human.vertex_groups}
    belly = lm['pelvis'].z + 0.16
    g = lambda z: math.exp(-((z - belly) / 0.30) ** 2)
    me = human.data
    me.update()
    for v in me.vertices:
        w = {names[x.group]: x.weight for x in v.groups}
        torso = w.get('Spine', 0) + w.get('Hips', 0) + 0.7 * (w.get('Thigh_L', 0) + w.get('Thigh_R', 0))
        arm = sum(w.get(n, 0) for n in ('Arm_L', 'Arm_R', 'ForeArm_L', 'ForeArm_R'))
        if torso > 0.5 and w.get('Head', 0) < 0.5:
            v.co += v.normal * amount * g(v.co.z) * (1.25 if v.normal.y < 0 else 0.8)
        elif arm > 0.5:
            v.co.x += math.copysign(amount * 0.9 * g(v.co.z), v.co.x)
    bm = bmesh.new()
    bm.from_mesh(me)
    dl = bm.verts.layers.deform.active
    belly_verts = [bv for bv in bm.verts if g(bv.co.z) > 0.05 and sum(wt for gi, wt in bv[dl].items() if names.get(gi) in ('Spine', 'Hips')) > 0.6]
    for _ in range(8):
        bmesh.ops.smooth_vert(bm, verts=belly_verts, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=False)
    bm.to_mesh(me)
    bm.free()
    for key, p in lm.items():
        if key.startswith(('arm_', 'fore_', 'wrist_')):
            p.x += math.copysign(amount * 0.9 * g(p.z), p.x)
    me.update()


def enlarge_hands(human, lm, k):
    """Scale each hand about its wrist, fading out over the lower forearm so there is no step. Hand = the ForeArm group below the wrist."""
    names = {g.index: g.name for g in human.vertex_groups}
    for v in human.data.vertices:
        for g in v.groups:
            for s in ('L', 'R'):
                if names.get(g.group) != f'ForeArm_{s}' or g.weight < 0.5:
                    continue
                w = lm[f'wrist_{s}']
                t = min(1.0, max(0.0, (w.z + 0.03 - v.co.z) / 0.06))
                t = t * t * (3 - 2 * t)
                if t > 0:
                    v.co = w + (v.co - w) * (1 + (k - 1) * t)
    human.data.update()


def build(kind):
    spec = BODIES[kind]
    for n in list(bpy.data.objects.keys()):
        if n.startswith(f'MPFB_{kind}') or n.startswith(f'LP_{kind}'):
            bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
    human, rig = make_human(kind)
    lower_arms(human, rig)
    delete_helpers(human)
    fold_weights(human, rig)
    lm = landmarks(rig)

    # free the body from MPFB's rig and move it to the game's frame: scaled to the hero's height, soles at y = -0.875, hips at the origin
    mw = human.matrix_world.copy()
    for m in [m for m in human.modifiers if m.type == 'ARMATURE']:
        human.modifiers.remove(m)
    human.parent = None
    human.data.transform(mw)
    human.matrix_world = Matrix.Identity(4)
    zs = [v.co.z for v in human.data.vertices]
    zmin, zmax = min(zs), max(zs)
    s = spec['tall'] / (zmax - zmin)
    cx, cy = lm['pelvis'].x, lm['pelvis'].y

    def fit(p):
        return Vector(((p.x - cx) * s, (p.y - cy) * s, (p.z - zmin) * s - 0.875))

    for v in human.data.vertices:
        v.co = fit(v.co)
    human.data.update()
    lm = {k: fit(v) for k, v in lm.items()}
    if spec.get('fat_extra'):
        fatten(human, lm, spec['fat_extra'])
    enlarge_hands(human, lm, spec.get('hands', HAND_SCALE))
    bpy.data.objects.remove(rig, do_unlink=True)
    human['lm'] = json.dumps({k: [round(c, 5) for c in lib.to_unity(v)] for k, v in lm.items()})
    human['kind'] = kind
    return human, lm


def game_bones(lm):
    """The game's 13 bones on this body's real joints (positions in Unity coordinates)."""
    U = lambda v: tuple(lib.to_unity(v))
    B = lib.Bone
    head = U(lm['head'])
    tail = U(lm['head_tail'])
    bones = [B('Hips', (0, 0, 0), (0, 0.1, 0)), B('Spine', (0, 0.1, 0), (0, head[1], head[2]), 'Hips'),
             B('Head', (0, head[1], head[2]), (0, tail[1], tail[2]), 'Spine')]
    for side in ('L', 'R'):
        bones += [B(f'Arm_{side}', U(lm[f'arm_{side}']), U(lm[f'fore_{side}']), 'Spine'),
                  B(f'ForeArm_{side}', U(lm[f'fore_{side}']), U(lm[f'wrist_{side}']), f'Arm_{side}'),
                  B(f'Thigh_{side}', U(lm[f'thigh_{side}']), U(lm[f'shin_{side}']), 'Hips'),
                  B(f'Shin_{side}', U(lm[f'shin_{side}']), U(lm[f'ankle_{side}']), f'Thigh_{side}'),
                  B(f'Ankle_{side}', U(lm[f'ankle_{side}']), U(lm[f'toe_{side}']), f'Shin_{side}')]
    return bones


def make_rig(kind, lm):
    arm = lib.build_armature(f'LP_{kind}_rig', game_bones(lm))
    arm.name = f'LP_{kind}_rig'
    return arm
