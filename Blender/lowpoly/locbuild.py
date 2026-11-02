"""Helpers for building a whole location as one Blender scene (one mesh object per material, flat shading, Spot_* empties).

Everything is placed in the game's axes (Unity: x right, y up, z forward, metres, water at y = 0); the helpers convert to Blender's.
    b = Builder()
    b.box('Deck', cx, cy, cz, sx, sy, sz)       # centre and size, axis-aligned
    b.cone('Foliage', x, y0, z, r_bottom, r_top, height)
    b.ico('Leaves', x, y, z, r, squash=1.0)
    b.spot('Spot_Block', x, y, z)
    b.finish(); b.export(path)
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')))
import lib  # noqa: E402


def B(x, y, z):
    return Vector(lib.to_blender((x, y, z)))


def hexrgb(h):
    h = h.lstrip('#')
    return [(int(h[i:i + 2], 16) / 255) ** 2.2 for i in (0, 2, 4)]


class Builder:
    def __init__(self):
        self.bms = {}
        self.colours = {}
        self.spots = {}
        self.tris = 0

    def mat(self, name, hexcol):
        self.colours[name] = hexcol

    def _bm(self, name):
        if name not in self.bms:
            self.bms[name] = bmesh.new()
        return self.bms[name]

    def box(self, mat, cx, cy, cz, sx, sy, sz, bevel=0.0, rot_y=0.0):
        """A box by centre and size (Unity axes). rot_y turns it about the vertical axis (degrees)."""
        bm = self._bm(mat)
        res = bmesh.ops.create_cube(bm, size=1.0)
        for v in res['verts']:
            v.co = Vector((v.co.x * sx, v.co.y * sz, v.co.z * sy))  # Unity z is Blender -y, Unity y is Blender z
        if rot_y:
            bmesh.ops.rotate(bm, verts=res['verts'], cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(-rot_y), 3, 'Z'))
        bmesh.ops.translate(bm, verts=res['verts'], vec=B(cx, cy, cz))
        if bevel > 0:
            edges = list({e for v in res['verts'] for e in v.link_edges})
            bmesh.ops.bevel(bm, geom=edges, offset=bevel, segments=1, profile=0.5, affect='EDGES')

    def cone(self, mat, x, y0, z, r0, r1, h, sides=8):
        """A vertical frustum standing on y0 (r0 at the bottom, r1 at the top; r1 = 0 is a point)."""
        bm = self._bm(mat)
        kw = dict(cap_ends=True, cap_tris=False, segments=sides, matrix=Matrix.Translation(B(x, y0 + h / 2, z)))
        try:
            bmesh.ops.create_cone(bm, radius1=r0, radius2=max(r1, 1e-4), depth=h, **kw)
        except TypeError:
            bmesh.ops.create_cone(bm, diameter1=2 * r0, diameter2=2 * max(r1, 1e-4), depth=h, **kw)

    def ico(self, mat, x, y, z, r, squash=1.0, sub=1):
        bm = self._bm(mat)
        try:
            res = bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r)
        except TypeError:
            res = bmesh.ops.create_icosphere(bm, subdivisions=sub, diameter=2 * r)
        for v in res['verts']:
            v.co.z *= squash
        bmesh.ops.translate(bm, verts=res['verts'], vec=B(x, y, z))

    def tri(self, mat, p0, p1, p2):
        """A flag-like triangle, visible from both sides (Unity coordinates)."""
        bm = self._bm(mat)
        for pts in ((p0, p1, p2), (p2, p1, p0)):  # two faces on their own points (a face cannot be doubled on shared ones)
            bm.faces.new([bm.verts.new(B(*p)) for p in pts])

    def spot(self, name, x, y, z):
        self.spots[name] = (x, y, z)

    def finish(self, collection_name='Location'):
        coll = bpy.data.collections.new(collection_name)
        bpy.context.scene.collection.children.link(coll)
        self.objects = []
        for name, bm in self.bms.items():
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
            me = bpy.data.meshes.new(name)
            bm.to_mesh(me)
            self.tris += sum(max(1, len(p.vertices) - 2) for p in me.polygons)
            bm.free()
            for p in me.polygons:
                p.use_smooth = False
            m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
            m.use_nodes = True
            for n in m.node_tree.nodes:
                if n.type == 'BSDF_PRINCIPLED':
                    n.inputs['Base Color'].default_value = (*hexrgb(self.colours.get(name, '#999999')), 1)
                    n.inputs['Roughness'].default_value = 0.15 if name in ('Glass',) else 0.85
            m.diffuse_color = (*hexrgb(self.colours.get(name, '#999999')), 1)
            me.materials.append(m)
            ob = bpy.data.objects.new(name, me)
            coll.objects.link(ob)
            self.objects.append(ob)
        for name, p in self.spots.items():
            e = bpy.data.objects.new(name, None)
            e.empty_display_type = 'ARROWS'
            e.empty_display_size = 0.5
            e.location = B(*p)
            coll.objects.link(e)
            self.objects.append(e)
        bpy.context.view_layer.update()
        return self.objects

    def export(self, path):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        bpy.ops.object.select_all(action='DESELECT')
        for o in self.objects:
            o.select_set(True)
        bpy.context.view_layer.objects.active = self.objects[0]
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'}, axis_forward='-Z', axis_up='Y',
                                 apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', add_leaf_bones=False, bake_anim=False,
                                 mesh_smooth_type='FACE', path_mode='COPY', embed_textures=False)
