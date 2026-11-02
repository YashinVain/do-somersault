"""The robot: its own body (not a human one), flat-shaded plates and ball joints, on the same 13 bones as everybody.

build() makes LP_robot (+ LP_robot_eyes, LP_robot_rig, a hidden LP_robot_dense for measuring) and robot.Gear_Antenna. The joints are taken
from the swimmer's skeleton, so every animation fits it. Needs LP_swimmer to exist (its landmarks).
"""
import json
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib  # noqa: E402
import body as old  # noqa: E402
import mpfb_body  # noqa: E402
import heroes  # noqa: E402
import finish  # noqa: E402
from lib import box, ellipsoid, sweep, tube  # noqa: E402


def build():
    finish.ensure_materials()
    heroes.materials()
    for n in list(bpy.data.objects.keys()):
        if n.startswith('LP_robot') or n.startswith('robot.'):
            bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
    lu = json.loads(bpy.data.objects['LP_swimmer']['lm'])
    lm = {k: Vector(lib.to_blender(v)) for k, v in lu.items()}
    P = lambda k: tuple(lu[k])
    rig = mpfb_body.make_rig('robot', lm)

    S = []
    # trunk: pelvis, a ringed waist, chest with a lit panel, neck
    S.append(box((0, 0.045, -0.005), (0.33, 0.17, 0.22), 0.025, 1, 'Skin', 'x'))
    S.append(tube([(0.11, 0.115, 0.08), (0.20, 0.115, 0.08)], 'y', 8, mat='Metal', kind='x'))
    S.append(box((0, 0.38, -0.01), (0.41, 0.30, 0.25), 0.03, 1, 'Skin', 'x'))
    S.append(box((0, 0.42, 0.12), (0.16, 0.12, 0.016), 0.0, 1, 'Accent', 'x'))
    S.append(box((0, 0.34, 0.12), (0.22, 0.03, 0.016), 0.0, 1, 'Metal', 'x'))
    S.append(tube([(0.56, 0.05, 0.05), (0.69, 0.05, 0.05)], 'y', 8, mat='Metal', kind='x'))
    # head: a box with a visor and a grille, bolts for ears
    S.append(box((0, 0.80, 0.01), (0.22, 0.23, 0.205), 0.035, 1, 'Skin', 'x'))
    S.append(box((0, 0.825, 0.118), (0.17, 0.075, 0.03), 0.01, 1, 'Lens', 'x'))
    S.append(box((0, 0.735, 0.11), (0.10, 0.028, 0.02), 0.0, 1, 'Metal', 'x'))
    for sx in (-1, 1):
        S.append(box((sx * 0.125, 0.80, 0.01), (0.045, 0.10, 0.10), 0.012, 1, 'Metal', 'x'))
    # arms and legs: ball joints, plated segments
    for s in ('L', 'R'):
        sh, el, wr = Vector(P(f'arm_{s}')), Vector(P(f'fore_{s}')), Vector(P(f'wrist_{s}'))
        th, kn, an = Vector(P(f'thigh_{s}')), Vector(P(f'shin_{s}')), Vector(P(f'ankle_{s}'))
        S.append(ellipsoid(tuple(sh), (0.07, 0.07, 0.07), 'Metal', 'x', 8, 4))
        S.append(sweep([sh, el], 0.047, 0.047, ref=(0, 0, 1), sides=8, mat='Skin', kind='x'))
        S.append(ellipsoid(tuple(el), (0.055, 0.055, 0.055), 'Metal', 'x', 8, 4))
        S.append(sweep([el, wr], 0.043, 0.043, ref=(0, 0, 1), sides=8, mat='Skin', kind='x'))
        d = (wr - el).normalized()
        S.append(box(tuple(wr + d * 0.075), (0.085, 0.13, 0.055), 0.012, 1, 'Metal', 'x'))
        S.append(ellipsoid(tuple(th), (0.085, 0.085, 0.085), 'Metal', 'x', 8, 4))
        S.append(sweep([th, kn], 0.078, 0.078, ref=(0, 0, 1), sides=8, mat='Skin', kind='x'))
        S.append(ellipsoid(tuple(kn), (0.065, 0.065, 0.065), 'Metal', 'x', 8, 4))
        S.append(sweep([kn, an], 0.06, 0.06, ref=(0, 0, 1), sides=8, mat='Skin', kind='x'))
        S.append(box((an.x, -0.875 + 0.045, an.z + 0.075), (0.12, 0.09, 0.28), 0.015, 1, 'Metal', 'x'))

    segs = [(b.name, Vector(b.head), Vector(b.tail)) for b in mpfb_body.game_bones({k: Vector(v) for k, v in lm.items()})]

    def nearest(p):
        p = Vector(p)
        if p.y > 0.68:
            return 'Head'
        best, bd = 'Hips', 1e9
        for name, a, b in segs:
            ab = b - a
            t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
            d = (p - (a + ab * t)).length
            if d < bd:
                best, bd = name, d
        return best

    ob = lib.make_object('LP_robot', S, rig, lambda kind, v: {nearest(v): 1.0})
    me = ob.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(me)
    bm.free()
    old.flat_shade(ob)
    ob['lm'] = json.dumps(lu)
    ob['kind'] = 'robot'

    dense = ob.copy()
    dense.data = ob.data.copy()
    dense.name = 'LP_robot_dense'
    bpy.context.scene.collection.objects.link(dense)
    dense.hide_set(True)
    dense.hide_render = True

    eyes = lib.make_object('LP_robot_eyes', [box((sx * 0.04, 0.825, 0.136), (0.045, 0.034, 0.012), 0.0, 1, 'EyeWhite', 'x') for sx in (-1, 1)], rig, lambda k, v: {'Head': 1.0})
    old.flat_shade(eyes)

    ant = heroes.from_shapes('Gear_Antenna', [tube([(0.91, .008, .008), (1.0, .006, .006)], 'y', 6, mat='Metal', kind='x'),
                                              ellipsoid((0, 1.02, 0.01), (0.025, 0.025, 0.025), 'Accent', 'x', 8, 4)])
    heroes.weight_to(ant, 'Head')
    heroes.skin_to(ant, rig)
    ant.name = 'robot.Gear_Antenna'
    ant['base'] = 'Gear_Antenna'
    return ob, rig, eyes, ant
