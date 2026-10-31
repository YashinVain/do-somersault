"""Joins a folder of numbered JPEG frames into an H.264 MP4 with Blender's built-in video encoder.
Run: blender --background --factory-startup --python make_video.py -- <frames folder> <output.mp4> [fps]
"""
import os
import sys
import bpy

args = sys.argv[sys.argv.index('--') + 1:]
folder, out = args[0], args[1]
fps = int(args[2]) if len(args) > 2 else 30

frames = sorted(f for f in os.listdir(folder) if f.lower().endswith(('.jpg', '.png')))
if not frames:
    raise SystemExit('no frames in ' + folder)

scene = bpy.context.scene
scene.render.fps = fps
scene.render.resolution_x, scene.render.resolution_y = 1280, 720
scene.render.resolution_percentage = 100
scene.frame_start, scene.frame_end = 1, len(frames)

editor = scene.sequence_editor_create()
strips = editor.strips if hasattr(editor, 'strips') else editor.sequences
strip = strips.new_image(name='film', filepath=os.path.join(folder, frames[0]), channel=1, frame_start=1)
for name in frames[1:]:
    strip.elements.append(name)

scene.render.use_sequencer = True
if hasattr(scene.render.image_settings, 'media_type'):
    scene.render.image_settings.media_type = 'VIDEO'
scene.render.image_settings.file_format = 'FFMPEG'
ff = scene.render.ffmpeg
ff.format = 'MPEG4'
ff.codec = 'H264'
ff.constant_rate_factor = 'HIGH'
ff.ffmpeg_preset = 'GOOD'
ff.audio_codec = 'NONE'
scene.render.filepath = out
os.makedirs(os.path.dirname(out), exist_ok=True)
bpy.ops.render.render(animation=True)
print('VIDEO', out, len(frames), 'frames')
