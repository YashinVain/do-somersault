"""Helpers for static props (trees, rocks, houses): lumpy faceted blobs, palm leaves, and export without a skeleton."""
import math
import os
import bpy
import bmesh
from mathutils import Vector

import lib
from lib import Shape, to_blender, to_unity, make_object


def _hash(ix, iy, iz, seed):
    h = (ix * 73856093) ^ (iy * 19349663) ^ (iz * 83492791) ^ (seed * 2654435761)
    h &= 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0


def vnoise(x, y, z, seed=1):
    """Smooth value noise in [-1, 1]."""
    ix, iy, iz = math.floor(x), math.floor(y), math.floor(z)
    fx, fy, fz = x - ix, y - iy, z - iz
    fx, fy, fz = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy), fz * fz * (3 - 2 * fz)

    def lerp(a, b, t):
        return a + (b - a) * t
    c = [[[_hash(ix + i, iy + j, iz + k, seed) for k in (0, 1)] for j in (0, 1)] for i in (0, 1)]
    x00, x10 = lerp(c[0][0][0], c[1][0][0], fx), lerp(c[0][1][0], c[1][1][0], fx)
    x01, x11 = lerp(c[0][0][1], c[1][0][1], fx), lerp(c[0][1][1], c[1][1][1], fx)
    return lerp(lerp(x00, x10, fy), lerp(x01, x11, fy), fz) * 2 - 1


def orient_closed(shape):
    """Make every face of a closed, roughly convex shape point away from its centre."""
    pts = [to_blender(v) for v in shape.verts]
    cen = Vector((0, 0, 0))
    for p in pts:
        cen += p
    cen /= max(1, len(pts))
    out = []
    for f in shape.faces:
        a, b, c = pts[f[0]], pts[f[1]], pts[f[2]]
        n = (b - a).cross(c - a)
        mid = (a + b + c) / 3
        out.append(tuple(reversed(f)) if n.dot(mid - cen) < 0 else tuple(f))
    shape.faces = out
    return shape


def blob(center, radii, mat='Rock0', subdiv=1, noise=0.14, seed=1, freq=1.6, flat_bottom=None, rot=None, kind='static'):
    """A lumpy faceted ball: an icosphere stretched to the radii and pushed in and out by noise.
    flat_bottom=y cuts everything below that height (as a fraction of the radius) so it sits on the ground."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    verts = []
    for v in bm.verts:
        d = Vector(v.co).normalized()
        n = vnoise(d.x * freq + seed * 3.1, d.y * freq + seed * 1.7, d.z * freq - seed * 2.3, seed) * noise
        n += vnoise(d.x * freq * 2.3, d.y * freq * 2.3, d.z * freq * 2.3, seed + 7) * noise * 0.4
        p = d * (1.0 + n)
        y = p.y
        if flat_bottom is not None and y < flat_bottom:
            y = flat_bottom
        verts.append((p.x * radii[0], y * radii[1], p.z * radii[2]))
    faces = [tuple(v.index for v in f.verts) for f in bm.faces]
    bm.free()
    s = orient_closed(Shape(verts, faces, mat, kind, flat=True))
    if rot:
        s.rotated(rot)
    return s.moved(center)


def blade(origin, yaw_deg, length, width, droop=0.5, rise=0.35, segs=7, mat='PalmLeaf', teeth=0.35, kind='static'):
    """A palm leaf: an arched, folded strip with a toothed edge, visible from both sides. yaw turns it about the vertical."""
    verts, faces = [], []
    yaw = math.radians(yaw_deg)
    dx, dz = math.sin(yaw), math.cos(yaw)
    px, pz = dz, -dx
    for i in range(segs + 1):
        t = i / segs
        dist = length * t
        h = length * (rise * math.sin(t * math.pi * 0.85) - droop * t * t)
        inner = 0 < i < segs
        w = width * math.sin(math.pi * min(1.0, t * 0.92 + 0.08)) * (1 - teeth * (i % 2)) if inner else 0.0
        fold = width * 0.18 if inner else 0.0
        ctr = (origin[0] + dx * dist, origin[1] + h, origin[2] + dz * dist)
        verts.append((ctr[0] - px * w, ctr[1] - fold * 0.3, ctr[2] - pz * w))
        verts.append((ctr[0], ctr[1] + fold, ctr[2]))
        verts.append((ctr[0] + px * w, ctr[1] - fold * 0.3, ctr[2] + pz * w))
    for i in range(segs):
        a = i * 3
        for j in range(2):
            faces.append((a + j, a + j + 1, a + 3 + j + 1, a + 3 + j))
    n = len(faces)
    faces += [tuple(reversed(f)) for f in faces[:n]]   # the back side
    return Shape(verts, faces, mat, kind, flat=True)


def make_static(name, shapes, origin=(0, 0, 0), parent=None):
    """A mesh object without a skeleton; the shapes are in Unity space and the object sits at `origin`."""
    moved = [Shape([(v[0] - origin[0], v[1] - origin[1], v[2] - origin[2]) for v in s.verts], s.faces, s.mat, s.kind, s.flat)
             for s in shapes]
    obj = make_object(name, moved, None, None)
    obj.location = to_blender(origin)
    if parent is not None:
        obj.parent = parent
    return obj


def empty(name):
    e = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(e)
    return e


def export_static(path, objects):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        add_leaf_bones=False, bake_anim=False, mesh_smooth_type='FACE', path_mode='COPY', embed_textures=False,
        bake_space_transform=True)