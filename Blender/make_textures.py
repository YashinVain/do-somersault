"""Seamless textures for every surface of the game: an albedo and a normal map for each material family.
Run: blender --background --factory-startup --python make_textures.py   -> Assets/_Project/Textures/<name>_a.png and <name>_n.png

The albedo maps are close to neutral grey with a hint of temperature: the game multiplies them by the colour of each material,
so one stone texture serves the grey rock, the orange cliff and the green jungle stone alike. Everything is generated with
periodic noise (numpy), so it tiles without a seam.
"""
import os
import sys
import numpy as np
import bpy

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(ROOT, '..', 'Assets', '_Project', 'Textures'))
os.makedirs(OUT, exist_ok=True)


# ------------------------------------------------------------------ periodic noise

def smooth(t):
    return t * t * (3 - 2 * t)


def pnoise(n, fx, fy, seed):
    """Periodic value noise with fx x fy lattice cells across the tile, in [0, 1]."""
    rng = np.random.default_rng(seed)
    lat = rng.random((fy, fx))
    xs = np.arange(n) / n * fx
    ys = np.arange(n) / n * fy
    x0 = np.floor(xs).astype(int)
    y0 = np.floor(ys).astype(int)
    tx = smooth(xs - x0)[None, :]
    ty = smooth(ys - y0)[:, None]
    x0, x1 = x0 % fx, (x0 + 1) % fx
    y0, y1 = y0 % fy, (y0 + 1) % fy
    a = lat[np.ix_(y0, x0)]
    b = lat[np.ix_(y0, x1)]
    c = lat[np.ix_(y1, x0)]
    d = lat[np.ix_(y1, x1)]
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty


def fbm(n, fx, fy, octaves, seed, gain=0.5):
    total = np.zeros((n, n))
    amp, norm = 1.0, 0.0
    for o in range(octaves):
        total += amp * pnoise(n, fx * 2 ** o, fy * 2 ** o, seed + o * 17)
        norm += amp
        amp *= gain
    return total / norm


def voronoi(n, cx, cy, seed, jitter=0.9):
    """Periodic cellular noise: nearest and second nearest distance (in cell units) and a random value per cell."""
    rng = np.random.default_rng(seed)
    jx = rng.random((cy, cx))
    jy = rng.random((cy, cx))
    val = rng.random((cy, cx))
    px = (np.arange(n) + 0.5) / n
    X, Y = np.meshgrid(px, px)
    ci = np.floor(X * cx).astype(int)
    cj = np.floor(Y * cy).astype(int)
    f1 = np.full((n, n), 9.0)
    f2 = np.full((n, n), 9.0)
    cell = np.zeros((n, n))
    for dj in (-1, 0, 1):
        for di in (-1, 0, 1):
            ii, jj = ci + di, cj + dj
            sx = (ii + 0.5 + (jx[jj % cy, ii % cx] - 0.5) * jitter) / cx
            sy = (jj + 0.5 + (jy[jj % cy, ii % cx] - 0.5) * jitter) / cy
            d = np.hypot((X - sx) * cx, (Y - sy) * cy)
            closer = d < f1
            f2 = np.where(closer, f1, np.minimum(f2, d))
            cell = np.where(closer, val[jj % cy, ii % cx], cell)
            f1 = np.where(closer, d, f1)
    return f1, f2, cell


def remap(x, lo, hi):
    return np.clip((x - lo) / (hi - lo), 0.0, 1.0)


def normal_map(h, strength):
    """Tangent-space normal map from a height field (periodic differences, so it tiles)."""
    n = h.shape[0]
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5 * n * strength / 256.0
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5 * n * strength / 256.0
    nz = np.ones_like(h)
    ln = np.sqrt(dx * dx + dy * dy + nz * nz)
    return np.stack([-dx / ln * 0.5 + 0.5, dy / ln * 0.5 + 0.5, nz / ln * 0.5 + 0.5], axis=-1)


def tint(c, rgb):
    return np.stack([c * rgb[0], c * rgb[1], c * rgb[2]], axis=-1)


def save(name, rgb, suffix):
    n = rgb.shape[0]
    img = bpy.data.images.new(f'{name}_{suffix}', n, n, alpha=False)
    img.colorspace_settings.name = 'Non-Color'          # write the numbers as they are; Unity decides what they mean
    rgba = np.ones((n, n, 4), dtype=np.float32)
    rgba[..., :3] = np.clip(rgb, 0, 1)
    img.pixels.foreach_set(rgba[::-1].ravel())           # Blender's rows start at the bottom
    img.filepath_raw = os.path.join(OUT, f'{name}_{suffix}.png')
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


def make(name, n, albedo, height, strength):
    """albedo: (n, n, 3) in 0..1; height: (n, n)."""
    save(name, albedo, 'a')
    save(name, normal_map(height, strength), 'n')
    print('texture', name, n)


# ------------------------------------------------------------------ the families

def rock(n=1024):
    """Layered rock: irregular strata, rows of blocks split by vertical joints, weathering and pits."""
    yy = np.arange(n)[:, None] / n
    xx = np.arange(n)[None, :] / n
    warp = fbm(n, 3, 5, 4, 11)
    rows = 9
    yw = yy + 0.16 * (warp - 0.5)
    row = np.floor(yw * rows)
    fy = yw * rows - row
    rng = np.random.default_rng(12)
    # every row has its own vertical joints and its own tone
    joints = np.sort(rng.random((rows + 1, 3)), axis=1)
    jx = xx + 0.12 * (fbm(n, 3, 5, 3, 14) - 0.5)
    block = np.zeros((n, n))
    nearest_joint = np.full((n, n), 1.0)
    for r in range(rows):
        mask = (row == r)
        for j in joints[r]:
            d = np.minimum(np.abs(jx - j), np.minimum(np.abs(jx - j - 1), np.abs(jx - j + 1)))
            nearest_joint = np.where(mask, np.minimum(nearest_joint, d), nearest_joint)
        block = np.where(mask, rng.random(), block)
    seam_h = 0.35 + 0.65 * remap(np.minimum(fy, 1 - fy), 0.0, 0.1 + 0.1 * fbm(n, 36, 36, 4, 16))            # the gap between strata
    seam_v = 0.4 + 0.6 * remap(nearest_joint, 0.0, 0.02)                       # the gap between blocks
    gap = np.minimum(seam_h, seam_v)
    weather = fbm(n, 6, 6, 5, 15)
    grain = fbm(n, 36, 36, 4, 16)
    pits = remap(pnoise(n, 90, 90, 17), 0.8, 1.0)
    face = 0.55 * grain + 0.45 * weather
    lip = np.exp(-((fy - 0.1) / 0.07) ** 2) - 0.8 * np.exp(-((fy - 0.93) / 0.07) ** 2)   # painted light on top of each course, shade under it
    h = 0.5 * face + 0.5 * gap + 0.12 * block - 0.15 * pits
    c = 0.42 + 0.22 * face + 0.1 * block - 0.05 * pits + 0.16 * lip
    c *= 0.66 + 0.34 * gap
    return c, h, 2.6

def bark(n=512):
    """Deep vertical furrows that wander, broken by short cross cracks."""
    yy = np.arange(n)[:, None] / n
    xx = np.arange(n)[None, :] / n
    u = xx * 15 + 0.8 * fbm(n, 2, 6, 4, 21)
    furrow = np.abs(np.sin(u * np.pi))                  # 0 at the bottom of a furrow
    breaks = remap(pnoise(n, 14, 5, 22), 0.55, 0.85)
    ridge = furrow * (1 - 0.55 * breaks)
    fine = fbm(n, 40, 8, 3, 23)
    h = 0.75 * ridge + 0.2 * fine
    c = 0.22 + 0.44 * ridge + 0.14 * fine
    return c, h, 3.0

def wood(n=512):
    planks = 4
    yy = np.arange(n)[:, None] / n
    plank_id = np.floor(yy * planks)
    local = yy * planks - plank_id
    seam = remap(np.minimum(local, 1 - local), 0.0, 0.045)
    grain = fbm(n, 5, 80, 3, 31)                          # long streaks along x
    rings = 0.5 + 0.5 * np.sin((fbm(n, 3, 20, 3, 32) * 14 + yy * 9) * np.pi)
    shade = np.take(np.random.default_rng(33).random(planks), plank_id.astype(int)[:, 0])[:, None]
    f1, _, _ = voronoi(n, 3, 4, 34)
    knots = remap(0.3 - f1, 0.0, 0.3) * (np.take(np.random.default_rng(35).random(planks), plank_id.astype(int)[:, 0])[:, None] > 0.6)
    h = 0.4 * grain + 0.25 * rings + 0.5 * seam - 0.25 * knots
    c = 0.30 + 0.28 * grain + 0.16 * rings + 0.2 * shade
    c *= 0.4 + 0.6 * seam
    c *= 1 - 0.3 * knots
    return c, h, 1.6


def plaster(n=512):
    """A hand-floated wall: soft trowel waves, fine sand and a few hairline cracks."""
    wave = fbm(n, 5, 5, 4, 41)
    fine = fbm(n, 70, 70, 3, 43)
    speck = remap(pnoise(n, 160, 160, 42), 0.9, 1.0)
    f1, f2, _ = voronoi(n, 4, 4, 44)
    crack = remap(f2 - f1, 0.0, 0.035) * 0.2 + 0.8
    crack = np.where(pnoise(n, 5, 5, 45) > 0.75, crack, 1.0)
    h = 0.35 * wave + 0.45 * fine + 0.2 * speck
    c = 0.66 + 0.07 * wave + 0.07 * fine - 0.12 * speck
    c *= crack
    return c, h, 1.3

def roof(n=512):
    rows, cols = 8, 6
    yy = np.arange(n)[:, None] / n * rows
    xx = np.arange(n)[None, :] / n * cols
    row = np.floor(yy)
    xo = xx + 0.5 * (row % 2)                              # every second row is shifted half a tile
    fx = xo - np.floor(xo) - 0.5
    fy = yy - row
    curve = np.sqrt(np.clip(1 - (2 * fx) ** 2, 0, 1))      # a rounded tile edge
    lip = remap(fy, 0.0, 0.5) * (0.5 + 0.5 * curve)
    shade = np.random.default_rng(51).random((rows + 1, cols * 2))
    tile = shade[(row.astype(int) % (rows)), (np.floor(xo).astype(int) % (cols * 2))]
    tile = np.broadcast_to(tile, (n, n))
    grime = fbm(n, 6, 6, 4, 52)
    h = 0.7 * lip + 0.1 * fbm(n, 30, 30, 3, 53)
    c = 0.36 + 0.12 * tile + 0.14 * lip + 0.06 * grime + 0.14 * np.exp(-((fy - 0.5) / 0.1) ** 2) * curve
    c *= 0.62 + 0.38 * remap(fy, 0.0, 0.35)                 # the shadow under the tile above
    return c, h, 3.0


def foliage(n=512):
    f1, f2, cell = voronoi(n, 12, 12, 61, 1.0)
    f1b, _, cellb = voronoi(n, 28, 28, 62, 1.0)
    clump = remap(0.75 - f1, 0.0, 0.75)
    leaf = remap(0.6 - f1b, 0.0, 0.6)
    h = 0.5 * clump + 0.5 * leaf
    c = 0.30 + 0.30 * clump + 0.22 * leaf + 0.12 * cellb + 0.08 * cell
    c *= 0.62 + 0.38 * remap(f2 - f1, 0.0, 0.2)
    return c, h, 2.4


def grass(n=512):
    blades = fbm(n, 90, 40, 3, 71)
    patch = fbm(n, 5, 5, 4, 72)
    tufts = remap(voronoi(n, 20, 20, 73)[0], 0.0, 0.7)
    h = 0.5 * blades + 0.3 * patch + 0.3 * (1 - tufts)
    c = 0.32 + 0.3 * patch + 0.22 * blades + 0.1 * (1 - tufts)
    return c, h, 1.4


def sand(n=512):
    ripple = 0.5 + 0.5 * np.sin((np.arange(n)[None, :] / n * 9 + fbm(n, 3, 3, 3, 81) * 2.2) * 2 * np.pi)
    grain = pnoise(n, 150, 150, 82)
    patch = fbm(n, 4, 4, 4, 83)
    h = 0.35 * ripple + 0.35 * grain + 0.2 * patch
    c = 0.58 + 0.12 * patch + 0.1 * grain + 0.06 * ripple
    return c, h, 0.9


def snow(n=512):
    drift = fbm(n, 4, 4, 4, 91)
    fine = fbm(n, 60, 60, 3, 92)
    sparkle = remap(pnoise(n, 200, 200, 93), 0.93, 1.0)
    h = 0.6 * drift + 0.2 * fine
    c = 0.74 + 0.12 * drift + 0.05 * fine + 0.2 * sparkle
    return c, h, 0.8


def ice(n=512):
    f1, f2, cell = voronoi(n, 5, 5, 101)
    veins = 1 - remap(f2 - f1, 0.0, 0.07)
    cloud = fbm(n, 4, 4, 4, 102)
    bubbles = remap(voronoi(n, 40, 40, 103)[0], 0.0, 0.25)
    h = 0.4 * cloud + 0.3 * veins - 0.15 * (1 - bubbles)
    c = 0.70 + 0.14 * cloud - 0.16 * veins + 0.06 * bubbles
    return c, h, 1.2


def metal(n=512):
    brushed = fbm(n, 4, 150, 3, 111)
    scratch = remap(pnoise(n, 3, 220, 112), 0.86, 1.0) + remap(pnoise(n, 140, 5, 113), 0.9, 1.0)
    panel = 0.5 + 0.5 * np.sin(np.arange(n)[None, :] / n * 2 * np.pi * 4)
    seam = remap(np.abs(np.sin(np.arange(n)[None, :] / n * np.pi * 4)), 0.0, 0.03) * np.ones((n, 1))
    h = 0.4 * brushed + 0.2 * scratch + 0.5 * seam
    c = 0.45 + 0.22 * brushed + 0.15 * scratch
    c *= 0.6 + 0.4 * seam
    return c, h, 1.0


def concrete(n=512):
    pores = remap(voronoi(n, 60, 60, 121)[0], 0.0, 0.3)
    stain = fbm(n, 4, 6, 4, 122)
    grain = fbm(n, 50, 50, 3, 123)
    f1, f2, _ = voronoi(n, 2, 2, 124)
    joint = remap(f2 - f1, 0.0, 0.04)
    h = 0.5 * grain + 0.3 * pores + 0.2 * stain
    c = 0.52 + 0.14 * stain + 0.1 * grain - 0.2 * (1 - pores)
    c *= 0.7 + 0.3 * joint
    return c, h, 1.2


def fabric(n=512):
    weave = 0.5 + 0.25 * np.sin(np.arange(n)[None, :] / n * 2 * np.pi * 48) + 0.25 * np.sin(np.arange(n)[:, None] / n * 2 * np.pi * 48)
    lumps = fbm(n, 30, 30, 3, 131)
    h = 0.6 * weave + 0.2 * lumps
    c = 0.55 + 0.18 * weave + 0.08 * lumps
    return c, h, 1.8


def skin(n=512):
    mottle = fbm(n, 5, 5, 4, 141)
    pores = remap(voronoi(n, 70, 70, 142)[0], 0.0, 0.35)
    fine = fbm(n, 90, 90, 2, 143)
    h = 0.5 * fine + 0.3 * (1 - pores) + 0.2 * mottle
    c = 0.72 + 0.1 * mottle - 0.05 * (1 - pores) + 0.03 * fine
    return c, h, 0.6


def hair(n=512):
    strands = fbm(n, 2, 160, 3, 151)
    clumps = fbm(n, 12, 3, 3, 152)
    h = 0.6 * strands + 0.3 * clumps
    c = 0.42 + 0.3 * strands + 0.2 * clumps
    return c, h, 1.6


def painted(n=512):
    strokes = fbm(n, 6, 2, 4, 161)
    grain = fbm(n, 60, 60, 3, 162)
    h = 0.3 * strokes + 0.3 * grain
    c = 0.55 + 0.18 * strokes + 0.08 * grain
    return c, h, 0.7


FAMILIES = [rock, bark, wood, plaster, roof, foliage, grass, sand, snow, ice, metal, concrete, fabric, skin, hair, painted]

CONTRAST = {'rock': 0.85, 'bark': 0.7, 'wood': 0.75, 'plaster': 0.6, 'roof': 0.85, 'foliage': 0.75, 'grass': 0.6, 'sand': 0.6,
            'snow': 0.5, 'ice': 0.65, 'metal': 0.6, 'concrete': 0.65, 'fabric': 0.5, 'skin': 0.5, 'hair': 0.6, 'painted': 0.5}


def blur(a, passes):
    for _ in range(passes):
        a = (a + np.roll(a, 1, 0) + np.roll(a, -1, 0) + np.roll(a, 1, 1) + np.roll(a, -1, 1)) / 5.0
    return a


def paint(c, h, name):
    # a brush leaves no pixel noise: soften, match the mean to mid grey and tame the contrast
    c = blur(c, 1)
    h = blur(h, 1)
    c = 0.5 + (c - c.mean()) * CONTRAST[name]
    return np.clip(c, 0.05, 0.95), h


for fn in FAMILIES:
    n = 1024 if fn is rock else 512
    c, h, k = fn(n)
    c, h = paint(c, h, fn.__name__)
    k *= 0.7
    tints = {'rock': (1.06, 1.0, 0.94), 'bark': (1.12, 0.96, 0.8), 'wood': (1.12, 0.97, 0.8), 'plaster': (1.04, 1.0, 0.94),
             'roof': (1.1, 0.96, 0.88), 'foliage': (0.96, 1.08, 0.9), 'grass': (0.96, 1.08, 0.88), 'sand': (1.08, 1.0, 0.86),
             'snow': (0.97, 1.0, 1.06), 'ice': (0.94, 1.0, 1.1), 'metal': (0.98, 1.0, 1.04), 'concrete': (1.0, 1.0, 0.98),
             'fabric': (1.0, 1.0, 1.0), 'skin': (1.06, 0.98, 0.94), 'hair': (1.04, 1.0, 0.96), 'painted': (1.0, 1.0, 1.0)}[fn.__name__]
    make(fn.__name__, n, tint(np.clip(c, 0, 1), tints), h, k)


# ------------------------------------------------------------------ water: two ripple normal maps, a foam pattern and caustics

def water():
    n = 512
    for idx, (seed, fx, fy) in enumerate([(201, 6, 5), (211, 9, 11)]):
        h = 0.55 * fbm(n, fx, fy, 5, seed, 0.55) + 0.45 * np.abs(2 * fbm(n, fx * 2, fy * 2, 3, seed + 50) - 1)
        save('water', normal_map(h, 3.2 if idx == 0 else 2.2), 'n' + str(idx + 1))
    f1, f2, _ = voronoi(n, 14, 14, 221, 1.0)
    bubbles = remap(0.5 - f1, 0.0, 0.5) * (0.6 + 0.4 * fbm(n, 12, 12, 3, 222))
    foam = np.clip(bubbles * 1.3 + remap(fbm(n, 8, 8, 4, 223), 0.55, 1.0) * 0.6, 0, 1)
    save('foam', np.stack([foam, foam, foam], axis=-1), 'a')
    caustic = remap(f2 - f1, 0.0, 0.5)
    caustic = (1 - caustic) ** 3
    caustic = np.clip(caustic * 1.6 * (0.7 + 0.3 * fbm(n, 5, 5, 3, 231)), 0, 1)
    save('caustics', np.stack([caustic, caustic, caustic], axis=-1), 'a')
    print('texture water, foam, caustics')


water()
print('DONE')
