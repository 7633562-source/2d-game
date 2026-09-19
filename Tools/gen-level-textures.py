#!/usr/bin/env python3
"""ART-0 delivery: the four level textures LevelArt asks for + the material tiles ArtLibrary reads.

Contract (quoted from the tree, no guessing):

  Assets/LevelArt.cs
    LevelArt.PathsFor      -> "Level/ground_fill", "Level/ground_cap", "Level/sky", "Level/sign_finish"
    LevelArt.LoadAt        -> Resources.Load<Sprite>(path), then Resources.LoadAll<Sprite>(path)
  Assets/Resources/Level/README.txt
    "Do not change file names: ground_fill, ground_cap, sky, sign_finish"
  ArtLibrary (pre-stub revision a13565ca, the last one that loaded anything)
    LoadSource             -> Resources.Load<Texture2D>("Art/" + key)
    keys                   -> skin, fabric, denim, dirt, grass, bark, leaf
    SoftMap / TileMap      -> need src.isReadable (GetPixelBilinear), TileSize = 512

So: level textures are Sprite-imported under Resources/Level/, materials are readable textures
under Resources/Art/. The materials are downscaled 1024 -> 512 here, because ArtLibrary.TileMap
downsamples to 512 at runtime anyway and tiled surfaces gain nothing above that at this scale.

Painted here: ground_fill (soil side view), ground_cap (grass strip), sky (colour ramp),
sign_finish (finish pennant). All four are seamless in X where they are tiled.

The seven material tiles are downscaled 1024 -> 512 and then levelled: a legacy surface
whose top edge is brighter than its bottom edge cannot tile, because every repeat steps by
that difference and the eye reads the step as a line at the repeat (ART-0 review, 19.09:
skin stepped 24.7 levels over 94.6 % of its boundary, 5.4x its own grain). Levelling adds
one constant per row and one per column, so the level returns to itself at the wrap while
every interior difference and every detail is left alone.

Run from the project root:   python Tools/gen-level-textures.py
Writes the PNGs plus their .meta import settings, then prints a verification table.
"""

import math
import os
import random
import re
import shutil
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LEGACY = os.path.join(ROOT, "Assets", "LegacyArt")
LEVEL_DIR = os.path.join(ROOT, "Assets", "Resources", "Level")
ART_DIR = os.path.join(ROOT, "Assets", "Resources", "Art")
META_TEMPLATE = os.path.join(LEGACY, "Apple", "fruit.png.meta")

# ─── palette (matches the flat colours the code falls back to) ───
DIRT_BASE = (107, 77, 41)      # LevelArt.FillTint fallback 0.42 0.30 0.16
DIRT_TOP = (94, 68, 36)
DIRT_DEEP = (69, 48, 27)
STRATA_LIGHT = (124, 93, 52)
STRATA_DARK = (85, 62, 32)
PEBBLE_LIGHT = (140, 108, 68)
PEBBLE_DARK = (60, 43, 23)
ROOT_FIBRE = (74, 54, 30)      # roots read warm brown, not near-black: PEBBLE_DARK/DIRT_DEEP mix

GRASS_BODY = (76, 148, 71)     # LevelArt.CapTint fallback 0.30 0.58 0.28
GRASS_ROOT = (53, 107, 49)
GRASS_BLADE = (90, 163, 83)
GRASS_TIP = (116, 192, 103)
GRASS_BLADE_DARK = (61, 124, 57)

SKY_TOP = (94, 155, 220)
SKY_MID = (140, 199, 240)      # DayNightCycle.daySky 0.55 0.78 0.94
SKY_HORIZON = (195, 225, 244)
SKY_WARM = (255, 240, 222)

POLE = (61, 38, 20)            # LevelTest1.FencePostTint 0.24 0.15 0.08
POLE_LIGHT = (78, 51, 25)
POLE_DARK = (42, 26, 13)
# Finish cloth (second sign pass, 19.09). The first pass painted a red/cream chequer; two
# eye checks at gameplay scale called it a roadworks barrier / slalom marker and named the
# classic black/white chequer as the finish read. LevelArt.SignTint keeps its flat red on
# purpose: it is the colour mark for the missing-PNG path, not this picture.
FLAG = (34, 32, 38)            # chequer cell A: graphite black
FLAG_DARK = (19, 18, 23)       # rim + the mast's shadow on the cloth
FLAG_LIGHT = (240, 237, 228)   # chequer cell B: warm white
CAP = (166, 166, 172)          # mast cap: a bare post top reads as a cut stick
CAP_DARK = (104, 104, 112)


def mix(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def smooth(t):
    return t * t * (3.0 - 2.0 * t)


def wrap_noise(width, cells, seed, octaves=1, gain=0.5, growth=2.0):
    """Value noise in x with an integer number of cells over `width`.

    The cell grid wraps by construction, so x=0 and x=width give the same value:
    the painted band is seamless when tiled horizontally. `growth` sets the octave ratio;
    a non-integer ratio keeps the octaves incommensurate, so the sum has no short visible
    repeat inside the texture (powers of two make the coarse octave repeat every 1-2 m).
    """
    rng = random.Random(seed)
    layers = []
    n = cells
    for o in range(octaves):
        layers.append((n, [rng.random() for _ in range(n)], gain ** o))
        n = min(width, int(round(n * growth)))
    total = sum(w for _, _, w in layers)

    def at(x):
        acc = 0.0
        for n, vals, w in layers:
            f = x / float(width) * n
            i = int(math.floor(f))
            t = f - i
            acc += w * (vals[i % n] + (vals[(i + 1) % n] - vals[i % n]) * smooth(t))
        return acc / total

    return at


def new_image(w, h, rgba=True):
    return Image.new("RGBA" if rgba else "RGB", (w, h), (0, 0, 0, 0) if rgba else (0, 0, 0))


# ─── ground_fill: side view of soil, 1024x256 @ 128 PPU = 8 x 2 m ───
# 8 x 2 m is the shape of the common slabs (LevelTest1 Spawn / FinishPlaza / UpperCourt), so those
# draw at 1:1. Sliced stretches the whole canvas onto the piece, therefore the texel density is
# texture_width / piece_width: 128 px/m on an 8 m slab, 32 px/m on the 32 m "Behind" slab.
# Painting stays horizontal-dominant so a wide slab degrades without turning shapes into smears.
def paint_ground_fill(w=1024, h=256):
    img = new_image(w, h)
    px = img.load()
    strata = wrap_noise(w, 5, 101, octaves=3, gain=0.55, growth=2.3)
    coarse = wrap_noise(w, 3, 105, octaves=2, gain=0.5, growth=2.7)
    wobble = wrap_noise(w, 4, 102, octaves=2, gain=0.5, growth=2.5)
    clods = wrap_noise(w, 24, 106, octaves=2, gain=0.5, growth=2.3)    # ~33 cm lumps
    clods2 = wrap_noise(w, 13, 108, octaves=2, gain=0.5, growth=2.3)   # incommensurate: no 33 cm repeat
    crumb = wrap_noise(w, 128, 107, octaves=2, gain=0.5, growth=2.3)   # ~6 cm crumbs
    # Precompute the x-only fields once: the per-pixel loop must stay cheap.
    f_strata = [strata(x) for x in range(w)]
    f_coarse = [coarse(x) for x in range(w)]
    f_wobble = [wobble(x) for x in range(w)]
    f_clods = [(clods(x) + clods2(x)) * 0.5 for x in range(w)]
    f_crumb = [crumb(x) for x in range(w)]
    grain = random.Random(103)

    for y in range(h):
        v = y / float(h - 1)
        if v < 0.09:                      # the strip the grass cap covers
            base = mix(DIRT_TOP, DIRT_BASE, v / 0.09)
        else:
            base = mix(DIRT_BASE, DIRT_DEEP, (v - 0.09) / 0.91)
        for x in range(w):
            # Quantised strata: flat layers with hard edges read crisp. A smooth gradient would read
            # as a blurred smear as soon as the slab stretches the fill horizontally.
            t = y / 9.0 + f_wobble[x] * 3.5 + f_strata[x] * 5.0
            li = int(math.floor(t))
            frac = t - li
            tone = ((li * 2654435761) % 7) / 7.0
            c = mix(base, STRATA_LIGHT if tone > 0.5 else STRATA_DARK, abs(tone - 0.5) * 0.55)
            if frac < 0.15 or frac > 0.90:
                c = mix(c, STRATA_DARK, 0.50)                 # the line between two layers
            # Coarse band, also quantised: keeps structure when a wide slab stretches the fill.
            wide = math.sin((y * 0.044) + f_coarse[x] * 1.5 + 0.8)
            c = mix(c, STRATA_DARK if wide > 0 else STRATA_LIGHT, abs(wide) * 0.14)
            clod = f_clods[x]
            c = mix(c, STRATA_LIGHT if clod > 0.5 else STRATA_DARK, abs(clod - 0.5) * 0.70)
            cr = f_crumb[x]
            c = mix(c, STRATA_LIGHT if cr > 0.5 else STRATA_DARK, abs(cr - 0.5) * 0.50)
            j = grain.randint(-12, 12)                         # loose grain, seamless in statistics
            px[x, y] = (max(0, min(255, c[0] + j)), max(0, min(255, c[1] + j)),
                        max(0, min(255, c[2] + j)), 255)

    rng = random.Random(104)
    # Roots: TAPER and MEANDER. A constant-thickness straight run with blunt ends reads as a
    # rectangular smudge in the soil, not as a root - measured 19.09: 6 such bars sat in rows
    # 21-30 / 37-49 as 20-84 px dark runs of constant thickness.
    for _ in range(7):
        flen = int(rng.uniform(70.0, 170.0))
        fx = rng.uniform(0.0, w)
        fy = rng.uniform(h * 0.08, h * 0.22)
        px_x, px_y = fx, fy
        thick = 2 if rng.random() < 0.5 else 1
        for i in range(flen):
            t = i / float(flen)
            k = 0.30 * (1.0 - t) ** 1.4 + 0.04          # fades into the soil at the tip
            if rng.random() < 0.22:                     # drifts, never runs straight
                px_y += rng.choice((-1.0, 0.0, 1.0))
            if i > int(flen * 0.45):
                thick = 1                               # thins out with distance
            for dy in range(thick):
                yy = int(px_y) + dy
                if 0 <= yy < h:
                    xx = int(px_x) % w
                    c = mix(px[xx, yy][:3], ROOT_FIBRE, k)
                    px[xx, yy] = (c[0], c[1], c[2], 255)
            px_x += 1.0
    for _ in range(8):                                  # cracks: 1 px, fading ends, forked
        cx, cy = rng.uniform(0.0, w), rng.uniform(h * 0.15, h * 0.95)
        length = int(rng.uniform(60, 200))
        for i in range(length):
            if rng.random() < 0.25:
                cy += rng.choice((-1.0, 1.0))
            y = int(cy)
            if 0 <= y < h:
                t = i / float(length)
                # A constant 0.70 mix reads as a pen stroke: the line has to fade in and out.
                k = 0.26 + 0.34 * math.sin(math.pi * t)
                x = int((cx + i) % w)
                c = mix(px[x, y][:3], PEBBLE_DARK, k)
                px[x, y] = (c[0], c[1], c[2], 255)
                if rng.random() < 0.05:                 # a crack forks
                    by, blen, bdir = y, int(rng.uniform(8, 24)), rng.choice((-1, 1))
                    for j in range(blen):
                        if rng.random() < 0.3:
                            by += bdir
                        if 0 <= by < h:
                            bx = int(cx + i + j) % w
                            cb = mix(px[bx, by][:3], PEBBLE_DARK, 0.26 * (1.0 - j / float(blen)))
                            px[bx, by] = (cb[0], cb[1], cb[2], 255)
    for _ in range(48):                                 # stones, drawn in their own box
        cx, cy = rng.uniform(0.0, w), rng.uniform(h * 0.10, h * 0.97)
        ry = rng.uniform(1.5, 4.5)
        rx = ry * rng.uniform(1.2, 2.2)                 # wider than tall, never a circle
        light = rng.random() < 0.4
        subtle = rng.random() < 0.22                    # a few stones barely leave the soil tone
        if not subtle:
            # Contact shadow first, offset one pixel down-right and slightly larger than the
            # stone: what survives is a 1-2 px crescent, i.e. the stone sits IN the soil.
            for dy in range(-int(ry) - 1, int(ry) + 2):
                y = int(cy) + dy + 1
                if y < 0 or y >= h:
                    continue
                for dx in range(-int(rx) - 1, int(rx) + 2):
                    nx, ny = dx / (rx * 1.06), (dy - 0.6) / (ry * 1.06)
                    if nx * nx + ny * ny >= 1.0:
                        continue
                    x = int(cx + dx + 1) % w
                    px[x, y] = mix(px[x, y][:3], PEBBLE_DARK, 0.34) + (255,)
        for dy in range(-int(ry) - 1, int(ry) + 2):
            y = int(cy) + dy
            if y < 0 or y >= h:
                continue
            for dx in range(-int(rx) - 1, int(rx) + 2):
                nx, ny = dx / rx, dy / ry
                d = nx * nx + ny * ny
                if d >= 1.0:
                    continue
                x = int(cx + dx) % w
                k = min(0.85, (1.0 - d) * 1.45)
                k *= 0.90 + rng.random() * 0.18         # jitters the rim: no analytic ellipse edge
                if subtle:
                    k *= 0.45
                if light and dy < -ry * 0.35:
                    k = min(0.95, k + 0.25)             # lit top edge: reads as a stone
                c = mix(px[x, y][:3], PEBBLE_LIGHT if light else PEBBLE_DARK, k)
                px[x, y] = (c[0], c[1], c[2], 255)
    return img


# ─── ground_cap: grass strip, 128x14 @ 100 PPU = 1.28 x 0.14 m (cap height) ───
# 0.14 m is fixed by StaticPlatform.SpawnSurfaceCap (capHeight = 0.14f), so the strip has to read
# as grass inside 14 px: uneven blade silhouette, light blade tips, a dark root line at the bottom.
#
# Repaint 19.09 after the eye check on the tiled preview (Trials/Runs/ART-0-frames/cap-on-ground.png,
# 8 m slab at game scale). Two defects were visible in the picture and measured in the pixels:
#   1. the silhouette was flat. The old painter kept the top at 1..5 px of the 14, so the strip read
#      as a green ribbon / rubber edge, not grass. Blades are now 3..11 px, grouped in clumps.
#   2. one blade was painted full-height in GRASS_BLADE_DARK and stood 12.7 levels below both of its
#      neighbours (mean neighbour step 0.45, so 28x the grain). Tiled every 1.28 m along every slab
#      that single column read as a regular tick - a ruler mark - which is what the preview showed.
#      Dark tufts are now drawn as runs (2..5 px wide) and a guard pass removes any column that is
#      left standing >= 3 levels below BOTH of its neighbours.
# The grain itself must stay: blade tips have to differ from their neighbours by more than the dark
# tufts do, otherwise the strip flattens into a band again.
# Fourth repaint 19.09 (ART-0 verify 19.09h). The engine gate was green and the canvas metrics were
# green, but the strip had never been measured in a FRAME: the 19.09f cap only ever reached the gates
# as a Tiled pixel test, no player build showed it. First real frames (Trials/Runs/art0verify1909h_*,
# 1920x1080, 64.7 px/m -> the 14 px canvas is 9 px on screen, 1 canvas px = 0.65 screen px) put the
# strip's top edge at rows 17..19 with std 0.34 px, 3 distinct rows over 20 m, tuft columns 100 % and
# 0 height spikes: every tuft top sat within 1 px of its neighbour, so the silhouette averaged out to
# a straight line and two independent eye reads named the result a flat ribbon, not grass. The teeth
# the eye did see sat on the SOIL line (the ragged root edge), which is the wrong end of the strip.
# A 1 px step is 0.65 screen px: below the sampling of the frame. The tips therefore have to span
# more rows, and neighbouring tufts have to differ by more than one - that is what MIN/MAX buy, and
# it is the only thing this repaint changes: the tuft construction, the brightness fields, the
# de-tick guard and the wrap-exact clump ring are untouched.
GRASS_TUFT_MIN = 3          # px: smallest blade tip row (a tip at row 3 leaves 11 rows of grass)
GRASS_TUFT_MAX = 9          # px: largest blade tip row, i.e. the shortest blade
# Third repaint 19.09f. The "gap in the cover" branch below used to SUBTRACT 3 from the tip row, and
# a smaller row is a TALLER blade: the two tufts that hit `blade(mid) < 0.16` were painted as the
# strip's tallest blades (tip row 2..4 against a 6..7 line, 5 columns wide at x=112) instead of as
# the gap the comment promised. At the tile's 1.28 m repeat that mound is the only landmark on the
# strip, and a blind eye read of the 8x preview named it a ruler mark. A gap is now painted where
# the comment always said it was: at the root line.
GRASS_TOP_GAP = 9           # px: tip row of a gap tuft, one row above the 4 root rows
GRASS_ROOT_ROWS = 4         # px: unbroken dark line where the grass meets the soil

# Second repaint 19.09, same canvas, after an eye check at game scale
# (Trials/Runs/ART-0-frames/cap-on-ground.png, 8 repeats at 2x) and a per-column measurement:
#   3. the silhouette was a 1 px sawtooth - 9 columns at the top row, 10 at the next, 56 at the
#      third - which at 0.14 m reads as bristles / comb teeth, not as grass. Blades are now drawn
#      as TUFTS: a run of 2..5 columns that shares one top edge and one light face.
#   4. two columns measured 5.1 and ~3.0 levels brighter than BOTH of their neighbours (mean
#      neighbour step 0.67, so 7.7x the tile's own grain). The strip repeats every 1.28 m, so the
#      brightest blade came back as a regular picket: the eye named the exact spot on the tiled
#      preview. The de-tick pass now guards bright columns as well as dark ones.
#   5. rows 10-12 of 14 were a flat slab (std 1.4 levels across 128 px): the strip read as two
#      pasted bands under the blades. The root rows carry grain now (std target >= 2).
GRASS_CLUMP_MIN = 2         # px: narrowest tuft (blades sharing a top)
GRASS_CLUMP_MAX = 5         # px: widest tuft
GRASS_TICK_LEVELS = 3.0     # levels: a column this far from BOTH neighbours is a ruler mark


def _cap_clumps(w, seed, lo=GRASS_CLUMP_MIN, hi=GRASS_CLUMP_MAX):
    """Cut the 128 px strip into tufts of 2..5 px that cover it exactly.

    Grass grows in tufts, and the tile repeats every 1.28 m: a blade exactly one column wide is
    both a bristle to the eye and the same bristle at every repeat. The widths are drawn from a
    seeded sequence and the ring is closed on the last clump, so the tuft pattern across the wrap
    seam is the same as inside the tile. Returns [(start_x, width), ...].
    """
    rng = random.Random(seed)
    out, x = [], 0
    while x < w:
        left = w - x
        if left <= hi:
            width = left                          # close the ring exactly
        else:
            width = rng.randint(lo, hi)
            if left - width < lo:                 # never leave a stub thinner than a tuft
                width = left - lo
        out.append((x, width))
        x += width
    return out


def _cap_column_colours(h, w, px):
    """Per-column brightness over the opaque pixels: the tick detector's input."""
    lum, cnt = [0.0] * w, [0] * w
    for x in range(w):
        s = 0.0
        for y in range(h):
            c = px[x, y]
            if c[3] > 0:
                s += (c[0] + c[1] + c[2]) / 3.0
                cnt[x] += 1
        lum[x] = s / cnt[x] if cnt[x] else 0.0
    return lum, cnt


def _cap_de_tick(img, rounds=6):
    """Remove single columns that stand out from both neighbours, dark OR bright.

    A tiled strip repeats whatever a single column does every 1.28 m, so one blade painted darker
    (or lighter) than its neighbours turns into a regular ruler mark. The pass moves only the
    colour of such a column toward the mean of its two neighbours (never its silhouette, never a
    column inside a wider run), so the strip keeps its tufts and loses the tick. Returns
    (image, columns touched, before-levels per column).
    """
    w, h = img.size
    start = _cap_column_colours(h, w, img.load())[0]   # per-column luminance before the pass
    fixed = []
    px = img.load()
    for _ in range(rounds):
        lum, cnt = _cap_column_colours(h, w, px)
        worst = None
        for x in range(w):
            if cnt[x] == 0:
                continue
            a, b = lum[(x - 1) % w], lum[(x + 1) % w]
            drop = min(a, b) - lum[x]                  # dark stand-out
            rise = lum[x] - max(a, b)                  # bright stand-out
            dev = max(drop, rise)
            if dev > GRASS_TICK_LEVELS and (worst is None or dev > worst[1]):
                worst = (x, dev)
        if worst is None:
            break
        x, boost = worst[0], worst[1]
        target = (lum[(x - 1) % w] + lum[(x + 1) % w]) * 0.5
        fixed.append(x)
        for y in range(h):
            c = px[x, y]
            if c[3] == 0:
                continue
            l = (c[0] + c[1] + c[2]) / 3.0
            if l <= 0.1:
                continue
            k = 1.0 + (target - l) / l          # one multiplicative step per column
            px[x, y] = (min(255, int(round(c[0] * k))), min(255, int(round(c[1] * k))),
                        min(255, int(round(c[2] * k))), c[3])
    return img, fixed, start


def _cap_taper(tops, h=14, rise=2, dip=3, rounds=4):
    """Turn a needle into a clump: no blade may stand `rise` px above both of its neighbours.

    The tile repeats every 1.28 m, and a single column jutting out of its neighbours is read as the
    same stalk at every repeat - the ruler mark the eye check caught (ART-0 frames, 19.09). The
    constraint is applied to the blade heights, not to the tops: a column that is 4 px taller than
    both of its neighbours is one pixel wide at its tip, i.e. a needle, however smooth the tops
    look. `dip` bounds how far a notch may cut down into the clump, so gaps stay notches and not
    slots. Grass tapers; a needle does not.
    """
    w = len(tops)
    height = [h - t for t in tops]
    for _ in range(rounds):
        changed = False
        for x in range(w):
            a, b = height[(x - 1) % w], height[(x + 1) % w]
            hi = min(a, b) + rise
            if height[x] > hi:
                height[x] = hi
                changed = True
            elif height[x] < max(1, max(a, b) - dip):
                height[x] = max(1, max(a, b) - dip)
                changed = True
        if not changed:
            break
    return [h - v for v in height]


def paint_ground_cap(w=128, h=14):
    img = new_image(w, h)
    px = img.load()
    clump = wrap_noise(w, 7, 211, octaves=3, gain=0.62, growth=2.7)    # clumps of one height
    blade = wrap_noise(w, 41, 212, octaves=2, gain=0.45, growth=2.6)   # blade-to-blade variation
    shade = wrap_noise(w, 16, 202, octaves=3, gain=0.50)               # light falling across the strip
    tip = wrap_noise(w, 23, 213, octaves=3, gain=0.50)                 # which tips catch the light
    dark = wrap_noise(w, 11, 215, octaves=1, gain=0.5)                 # dark tufts, ~0.31 m wide runs
    grain = wrap_noise(w, 54, 216, octaves=2, gain=0.5, growth=2.3)    # 2 px cells: root-row grain
    span = float(GRASS_TUFT_MAX - GRASS_TUFT_MIN)

    # One top and one light face per TUFTS, not per column: the silhouette is a run of blades that
    # share an edge. A 1 px column would come back at every repeat as the same picket.
    tops = [0] * w
    lit_by_x = [1.0] * w
    dark_by_x = [False] * w
    for start, width in _cap_clumps(w, 217):
        mid = (start + width // 2) % w
        # The clump field carries the low frequencies and the blade field the high ones. The tile
        # is only 1.28 m of ground, so a dominant low-frequency hump would come back at every
        # repeat as a wave (eye check, 19.09): the weight sits on the blade field instead.
        f = clump(mid) * 0.45 + blade(mid) * 0.55
        base = int(round(GRASS_TUFT_MIN + f * span))
        if blade(mid) < 0.16:
            base = GRASS_TOP_GAP                  # a gap in the cover: the tuft sits at the root line
        base = max(1, min(GRASS_TUFT_MAX, base))
        for i in range(width):
            x = start + i
            step = 0
            if width >= 4 and i == width - 1 and blade(x) > 0.58:
                step = -1                       # the tuft's last blade is a step short
            elif width >= 3 and i - width // 2 == 1 and blade(x) < 0.34:
                step = 1                        # one blade leans out INSIDE its own tuft
            tops[x] = max(1, min(GRASS_TOP_GAP, base - step))
        lit = 0.55 + 0.45 * tip(mid)            # a tuft catches the light as a group
        darkx = dark(mid) > 0.86 and base < GRASS_TUFT_MAX - 1
        for i in range(width):
            x = start + i
            lit_by_x[x] = lit
            dark_by_x[x] = darkx
    tops = _cap_taper(tops)
    for x in range(w):
        top = tops[x]
        darkx = dark_by_x[x]
        lit = lit_by_x[x]
        for y in range(top, h):
            if y >= h - GRASS_ROOT_ROWS:
                c = mix(GRASS_ROOT, GRASS_BODY, 0.24 + shade(x) * 0.30)
            elif y <= top + 1:
                c = mix(GRASS_TIP, GRASS_BLADE, 0.20 + (1.0 - lit) * 0.35)
            elif darkx:
                c = mix(GRASS_BODY, GRASS_BLADE_DARK, 0.62 + shade(x) * 0.16)
            else:
                t = (y - top) / float(max(1, h - GRASS_ROOT_ROWS - top))
                c = mix(GRASS_BLADE, GRASS_BODY, min(1.0, t) * (0.35 + shade(x) * 0.30))
                if y <= top + 3:
                    c = mix(c, GRASS_TIP, (0.30 + 0.25 * lit) * (1.0 - (y - top) / 3.0))
                    c = mix(c, GRASS_BLADE_DARK, 0.10)     # a dark edge under each lit tip
            px[x, y] = (c[0], c[1], c[2], 255)

    # Root line: every column is opaque at the bottom, so tiled grass never breaks the soil line.
    # The boundary is ragged on purpose (eye check, 19.09: a clean straight line between green and
    # brown reads as two pasted strips). The last row carries soil-toned pixels where the grass
    # does not cover - short runs, bounded by the same de-rhythm rule as the blades - and one row
    # up a few roots dip into it. Nothing here changes the silhouette: the box stays 0.14 m.
    # Second eye check 19.09: rows 10-12 came out as one flat slab (std 1.4 levels over 128 px), so
    # the root rows now mix in the grain field and a per-pixel dither: a band under the blades has
    # to look like depth of cover, not like a painted stripe.
    edge = wrap_noise(w, 10, 218, octaves=1, gain=0.5)
    edge2 = wrap_noise(w, 17, 219, octaves=1, gain=0.5)
    dither = random.Random(220)
    for y in range(h - GRASS_ROOT_ROWS, h):
        deep = (h - 1 - y) / float(GRASS_ROOT_ROWS - 1)   # 0 at the soil line, 1 under the blades
        for x in range(w):
            c = mix(GRASS_ROOT, GRASS_BODY,
                    0.24 + shade(x) * 0.30 + (grain(x) - 0.5) * 0.46)
            soil = mix(DIRT_TOP, DIRT_BASE, 0.45)
            if y == h - 1:
                # grass fingers over soil: ~6 px runs of each, so the contact line is broken
                c = mix(c, soil, 0.05 if edge((x + 37) % w) > 0.52 else 0.85)
            elif y == h - 2:
                c = mix(c, soil, 0.45 if edge2((x + 11) % w) > 0.80 else 0.06)
            else:
                c = mix(c, DIRT_TOP, 0.08)
            j = dither.choice((-2, -1, 0, 0, 1, 2))       # kills the flat slab look
            j = j if deep > 0.5 else j // 2
            px[x, y] = (max(0, min(255, c[0] + j)), max(0, min(255, c[1] + j)),
                        max(0, min(255, c[2] + j)), 255)

    img, fixed, before = _cap_de_tick(img)
    if fixed:
        px = img.load()
        after, _ = _cap_column_colours(h, w, px)
        print("ground_cap de-tick: %d column(s) %s (%.1f -> %.1f levels, neighbours %.1f / %.1f)"
              % (len(fixed), fixed, before[fixed[0]], after[fixed[0]],
                 after[fixed[0] - 1], after[(fixed[0] + 1) % w]))
    return img


# ─── sky: colour ramp, 512x128 @ 32 PPU (drawn Sliced over 120x24 m) ───
# Stops sit where the camera can actually see them: the sky object is 24 m tall centred at y = 6
# (LevelBackdrop.BuildSky) and the camera shows about y in [-6, 4.2], i.e. texture v 0.58 .. 0.99.
# So the deep zenith colour is off-screen and the visible band runs daySky -> horizon haze.
SKY_STOPS = ((0.00, SKY_TOP), (0.30, SKY_TOP), (0.62, SKY_MID), (0.90, SKY_HORIZON), (1.00, SKY_HORIZON))
SKY_WARM_FROM = 0.88


def ramp(stops, v):
    for i in range(len(stops) - 1):
        v0, c0 = stops[i]
        v1, c1 = stops[i + 1]
        if v <= v1 or i == len(stops) - 2:
            if v1 <= v0:
                return c1
            return mix(c0, c1, smooth((v - v0) / (v1 - v0)))
    return stops[-1][1]


def paint_sky(w=512, h=128):
    img = new_image(w, h)
    px = img.load()
    dither = random.Random(301)
    for y in range(h):
        v = y / float(h - 1)
        c = ramp(SKY_STOPS, v)
        c = mix(c, SKY_WARM, max(0.0, (v - SKY_WARM_FROM) / (1.0 - SKY_WARM_FROM)) * 0.14)
        for x in range(w):
            d = dither.choice((-1, 0, 0, 0, 1))     # kills 8-bit banding in a smooth ramp
            px[x, y] = (max(0, min(255, c[0] + d)), max(0, min(255, c[1] + d)),
                        max(0, min(255, c[2] + d)), 255)
    return img


# ─── sign_finish: finish banner on a post, 64x320 @ 100 PPU ───
# The piece box is 0.18 x 0.90 m (LevelTest1 "FinishSign"), aspect 1:5, and StaticPlatform draws it
# Sliced, so the canvas is painted 1:5 and comes out undistorted.
#
# Redesign 19.09 after the eye check. The old canvas had a 16 px mast (nearly half the 0.18 m box)
# and a 28 px pennant that tapered to a 1 px point: at gameplay scale it read as a rigid slalom
# pole, not as a finish. Now the mast is 11 px, the banner 46 px (0.13 m) of a bold chequer - the
# one finish motif that survives being 3 x 7 screen pixels - with a squared tag end.
# The chequer is clipped by the banner silhouette, and every diagonal is an integer step, so the
# edge cannot wobble.
#
# Second pass 19.09 (same canvas, same PPU, same .meta): the chequer's dark cell is graphite black
# instead of coral red. Judged at 16x81 / 8x40 / 5x25 px the red/cream cloth read as a roadworks
# barrier; the black/white chequer is the classic finish and carries the higher contrast the small
# sizes need against a light horizon. Geometry, silhouette, rings and mast byte-for-byte unchanged.
#
# Third pass 19.09: a fresh eye check at 3x still named the defect - "a coarse 2x3 grid in a racing
# context marks an obstacle or an apex cone, and a brown post reads as a wooden stake". Both are
# addressable without moving a single opaque pixel:
#   * `cell_w` / `cell_h` / `rows` make the chequer grid a parameter, so 4 columns x 6 rows
#     (11 x 31 px, exact integer steps, no partial cell) is a finish chequer, not a hazard block;
#   * `post` takes the mast palette, so the wood palette swaps for the neutral metal of the cap that
#     already sits on top of the same mast.
# Folds in the cloth are deliberately NOT added: the flat-shading rule bans gradients inside a
# silhouette, and at ~11 x 47 screen px the banner is too small to carry one.
#
# The grid was chosen by a blind four-way read of the same sheet at native / 16x81 / 8x40 px
# (`Trials/Runs/art-0-sign-2026-09-19/`, panels shuffled, mapping in `map.txt`): the shipped brown
# 2x3 came LAST ("a wooden post breaks the finish association, and 2x3 giant cells read as a
# roadworks barrier"), 4 columns of 11 px came THIRD ("the fine grid dissolves into grey mush at
# 8x40"), 2x3 on metal came SECOND, and the winner is 3 x 5 cells of 15 x 38 px on the metal mast -
# dense enough to be a chequer, coarse enough to survive the 11 x 47 px the banner really occupies.
SIGN_POST_WOOD = (POLE_DARK, POLE_LIGHT, POLE)                      # (dark, light, mid)
SIGN_POST_METAL = ((84, 88, 96), (172, 176, 182), (126, 130, 138))


def paint_sign_finish(w=64, h=320, cell_w=15, cell_h=38, rows=5, post=SIGN_POST_METAL):
    """Finish banner on a post. Defaults = the shipped look (third pass).

    cell_w=23, cell_h=62, rows=3, post=SIGN_POST_WOOD reproduces the second pass byte for byte.
    """
    post_dark, post_light, post_mid = post
    img = new_image(w, h)
    px = img.load()
    post_l, post_r = 7, 17
    ban_left = post_r + 1
    ban_top, ban_bottom = 10, 196
    ban_full_r, ban_fly_r = 46, 61
    half_full = (ban_bottom - ban_top) // 2          # 93 px: the banner is 0.52 m of the 0.90 m post
    half_fly = 40                                    # squared tag end, not a needle tip
    cap_bottom = 2
    foot_top = h - 14
    mid_y = (ban_top + ban_bottom) // 2

    def half_at(x):
        if x <= ban_full_r:
            return half_full
        t = (x - ban_full_r) / float(ban_fly_r - ban_full_r)
        return half_full - int(round(t * (half_full - half_fly)))

    for y in range(h):
        for x in range(w):
            c = None
            # mast: lit left edge, dark right edge
            if post_l <= x <= post_r:
                if x == post_l or x >= post_r:
                    c = post_dark
                elif x <= post_l + 2:
                    c = post_light
                else:
                    c = post_mid
            # foot: wider than the mast and centred on it (the old foot sat to the right of the mast,
            # which is what made the base read as a pasted-on block)
            if y >= foot_top and post_l - 3 <= x <= post_r + 6:
                c = post_dark if (x <= post_l - 2 or x >= post_r + 5 or y >= h - 2) else post_mid
            # cap: a bare post top reads as a cut stick
            if y <= cap_bottom and post_l - 1 <= x <= post_r + 1:
                c = CAP if y < cap_bottom else CAP_DARK
            # banner: chequer clipped by the tapering silhouette
            if x >= ban_left and ban_top <= y <= ban_bottom:
                d = abs(y - mid_y)
                half = half_at(x)
                if d <= half:
                    row = min(rows - 1, (y - ban_top) // cell_h)
                    col = (x - ban_left) // cell_w
                    c = FLAG if (row + col) % 2 == 0 else FLAG_LIGHT
                    if d >= half - 1 or x >= ban_fly_r:
                        c = FLAG_DARK                     # rim: the silhouette holds at small scale
                    if x == ban_left:
                        c = mix(FLAG, post_dark, 0.55)    # cloth presses against the mast
                    elif x == ban_left + 1:
                        c = FLAG_DARK                     # the mast casts onto its own cloth
                    # two metal rings at the hoist: without an attachment cue the cloth reads as a
                    # slab glued to the post (eye check 19.09)
                    if x <= ban_left + 4 and (y <= ban_top + 2 or y >= ban_bottom - 2):
                        c = CAP_DARK
            if c is not None:
                px[x, y] = (c[0], c[1], c[2], 255)
    return img


# ─── materials: LegacyArt 1024 -> Resources/Art 512 ───
MATERIALS = ["skin", "fabric", "denim", "dirt", "grass", "bark", "leaf"]

# Wrap blend bands, in px of the 512 tile (key -> band). A material dressed Tiled shows the
# wrap every `LeafTileMeters` = 0.5 m, so a step at the edge is a line on a 0.5 m grid.
# `level_tile` removes a brightness ramp (skin, fabric); it cannot touch a DETAIL mismatch,
# which is what `leaf` has: its veins are cut by the tile boundary, and the 19.09 review read
# the swatch as "coarse 2x2 seam grid, the pattern is destroyed". 32 px of 512 = 6.25 % per
# axis, and at LeafTileMeters the band is 2 screen px at the level lens.
WRAP_BLEND = {"leaf": 32}


def write_meta(png_path, pixels_per_unit, readable, alpha, guid):
    """Copy the project's own Single-sprite .meta shape and set the fields the loaders need."""
    with open(META_TEMPLATE, "r", encoding="utf-8") as fh:
        text = fh.read()

    def set_line(key, value, indent=2):
        nonlocal text
        pattern = re.compile(r"^%s%s:.*$" % (" " * indent, re.escape(key)), re.MULTILINE)
        assert pattern.search(text), "template has no %s" % key
        text = pattern.sub("%s%s: %s" % (" " * indent, key, value), text, count=1)

    def set_text(key, value, indent=2):
        nonlocal text
        pattern = re.compile(r"^%s%s:.*$" % (" " * indent, re.escape(key)), re.MULTILINE)
        text = pattern.sub("%s%s: %s" % (" " * indent, key, value), text, count=1)

    text = re.sub(r"^guid: .*$", "guid: " + guid, text, count=1, flags=re.MULTILINE)
    set_line("spriteMeshType", 0)            # FullRect: Sliced / Tiled draw modes need it
    set_line("spritePixelsToUnits", pixels_per_unit)
    set_line("isReadable", 1 if readable else 0)
    set_line("wrapU", 1, indent=4)           # Repeat, for the tiled surfaces
    set_line("wrapV", 1, indent=4)
    set_line("alphaIsTransparency", 1 if alpha else 0)
    set_line("spriteGenerateFallbackPhysicsShape", 0)   # art never carries a collider
    set_line("textureType", 8)               # Sprite
    set_line("spriteMode", 1)                # Single
    set_text("spriteID", guid[:16] + "0800000000000000", indent=4)
    text = re.sub(r"^\s*nameFileIdTable: .*$", "    nameFileIdTable: {}", text, count=1, flags=re.MULTILINE)

    with open(png_path + ".meta", "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def new_guid(rng):
    return "".join(rng.choice("0123456789abcdef") for _ in range(32))


def existing_guid(path):
    """The guid already in <path>.meta, if any.

    A repaint must not churn the import identity: a new guid makes Unity re-import the asset as a
    different one, which breaks every reference to it (and shows up as a needless diff in a cycle
    that is supposed to change pixels only).
    """
    meta = path + ".meta"
    if not os.path.exists(meta):
        return None
    with open(meta, "r", encoding="utf-8") as fh:
        m = re.search(r"^guid: ([0-9a-f]+)$", fh.read(), re.MULTILINE)
    return m.group(1) if m else None


def save(img, path, pixels_per_unit, readable, alpha, guid_rng):
    img.save(path, "PNG", optimize=True)
    write_meta(path, pixels_per_unit, readable, alpha, existing_guid(path) or new_guid(guid_rng))
    return os.path.getsize(path)


def cap_tick_report(img):
    """The tiled-strip defect test: does one column stand out from both of its neighbours?

    The cap repeats every 1.28 m, so a single column darker (or brighter) than its neighbours is
    a ruler mark, not a blade. Only the opaque pixels count: a blade is short where the sky shows
    through, and a mean that includes those transparent rows would report height, not colour.
    Also reports the tuft fraction: the share of columns that share their top with a neighbour
    within 1 px. A strip built from 1 px needles scores near 0 - that is the comb-teeth look.
    Returns (worst dark stand-out, column, mean neighbour step, worst height spike,
             worst bright stand-out, column, tuft fraction).
    """
    a = np.asarray(img.convert("RGBA")).astype(np.float64)
    rgb, alpha = a[:, :, :3].mean(axis=2), (a[:, :, 3] > 0)
    col = (rgb * alpha).sum(axis=0) / np.maximum(alpha.sum(axis=0), 1)
    height = alpha.sum(axis=0).astype(np.float64)
    w = col.shape[0]
    worse = np.minimum(col[(np.arange(w) - 1) % w], col[(np.arange(w) + 1) % w]) - col
    rise = col - np.maximum(col[(np.arange(w) - 1) % w], col[(np.arange(w) + 1) % w])
    spike = height - np.maximum(height[(np.arange(w) - 1) % w], height[(np.arange(w) + 1) % w])
    step = np.abs(np.diff(col))
    tops = np.array([int(np.argmax(alpha[:, x])) if alpha[:, x].any() else -1 for x in range(w)])
    tuft = float(np.mean([min(abs(tops[x] - tops[(x - 1) % w]), abs(tops[x] - tops[(x + 1) % w])) <= 1
                          for x in range(w)]))
    i = int(worse.argmax())
    j = int(rise.argmax())
    return (float(worse.max()), i, float(step.mean()), float(spike.max()),
            float(rise.max()), j, tuft)



def seam_error(img):
    """Horizontal tiling check: the jump across the wrap seam vs the jump between neighbours.

    A white-noise grain makes column 0 differ from the last column by the same amount two
    neighbouring columns differ by, so the seam is invisible; the structured strata/blades wrap
    exactly by construction. Reported as (seam jump, typical inner jump), mean over all rows.
    Transparent rows are skipped: their RGB is (0,0,0) and would count as a black step.
    """
    a = np.asarray(img.convert("RGBA")).astype(np.float64)
    rgb, alpha = a[:, :, :3], (a[:, :, 3] > 0)
    w = rgb.shape[1]
    hit = alpha[:, 0] & alpha[:, w - 1]                 # rows where both edges are painted
    seam = float(np.abs(rgb[hit, 0] - rgb[hit, w - 1]).mean()) if hit.any() else 0.0
    inner = float(np.abs(np.diff(rgb, axis=1)).mean())
    return seam, inner


def wrap_stats(a, axis):
    """(cut, inner) in 0-255 levels, averaged over the RGB channels.

    cut   = |first edge pixel - last edge pixel|: the step a repeat shows at the wrap.
    inner = |neighbour pixel|: the grain the eye already accepts inside the tile.
    A tile whose cut is no worse than its own grain cannot be seen to repeat.
    """
    cut = np.abs(a.take(0, axis=axis).astype(np.int16) - a.take(-1, axis=axis).astype(np.int16))
    inner = np.abs(np.diff(a.astype(np.int16), axis=axis))
    return float(cut.mean()), float(inner.mean())


def level_tile(img, ratio=2.0, floor=1.5, cap=64.0):
    """Cancel a brightness ramp across a tile with one constant per row and one per column.

    A legacy surface whose top edge is brighter than its bottom edge cannot tile: every
    repeat steps by that difference and the eye reads the step as a line on the grid. The
    correction c(y) = d * y / (h - 1), d being the row means' end-to-end difference, makes
    the row means meet at the wrap, and the same in x. A constant per row cannot change an
    interior difference, so no detail is blurred and no pixel is resampled or relocated -
    unlike a roll, which only moves the step inside the tile (the 2x2 A/B showed that).

    Left alone when the cut is no worse than the tile's own grain, or when the step is so
    large that the file is not a surface. Returns (image, note).
    """
    a = np.asarray(img.convert("RGB")).astype(np.float64)
    h, w = a.shape[:2]
    correction = np.zeros_like(a)
    parts = []
    for axis, n, name in ((0, h, "Y"), (1, w, "X")):
        cut, inner = wrap_stats(a, axis)
        step = a.mean(axis=1 - axis)[0] - a.mean(axis=1 - axis)[-1]
        if cut <= floor or cut <= ratio * inner:
            parts.append("%s ok cut %.2f vs inner %.2f" % (name, cut, inner))
            continue
        if float(np.abs(step).max()) > cap:
            parts.append("%s step %.1f over cap %.0f, left alone" % (
                name, float(np.abs(step).max()), cap))
            continue
        ramp = np.linspace(0.0, 1.0, n)[:, None] * step[None, :]
        correction += ramp.reshape((n, 1, 3) if axis == 0 else (1, n, 3))
        parts.append("%s ramped %.1f..%.1f" % (name, float(step.min()), float(step.max())))
    if correction.any():
        out = np.clip(a + correction, 0.0, 255.0).astype(np.uint8)
        for axis, name in ((0, "Y"), (1, "X")):
            cut, inner = wrap_stats(out, axis)
            parts.append("%s now cut %.2f vs inner %.2f" % (name, cut, inner))
    else:
        out = a.astype(np.uint8)
    return Image.fromarray(out, "RGB"), "; ".join(parts)


def _band_max_step(a, axis, lo, hi):
    """Worst single-pixel step inside [lo, hi) along one axis, in 0-255 levels."""
    d = np.abs(np.diff(a.astype(np.int16), axis=axis))
    sl = [slice(None)] * 3
    sl[axis] = slice(lo, hi)
    return float(d[tuple(sl)].max())


def wrap_blend(img, band):
    """Fade a tile's tail edge into the head it will sit next to, so the repeat has no line.

    `level_tile` cancels a RAMP and cannot help a DETAIL mismatch: leaf's veins are sliced by
    the boundary, and the leftover difference between the last and the first column is 1.3x
    the tile's own grain - a systematic line at the same place in every repeat.

    Two attempts, both measured (the number after each is the seam step `cut`, 0-255):

    1. Tail faded into the head at the same offset: out[W-B+i] = mix(T[i], H[i], t). The
       junction step becomes |H[B-1] - H[0]| - the head's own difference over B px. X 16.83 ->
       16.18, Y 17.07 -> 17.26: a change of nothing. `cut` is a mean over rows and cannot see
       why, but the reason is simple: distance does not matter to the eye, magnitude does. A
       B-px gap is still a 1-px jump of the same size, and it is still in the same place in
       every repeat, so the eye still draws the grid.
    2. What is kept: the head band is MIRRORED, out[W-B+i] = mix(T[i], H[B-1-i], t), so the
       band ends exactly ON H[0] - the pixel that follows the wrap. The junction step goes to
       (1 - B/(B+1)) x |T[B-1] - H[0]|, i.e. ~3 % of the original, and the band approaches it
       linearly from the tail, so nothing is stamped and no line is moved inside: `_band_max_step`
       prints the worst step inside the band against the worst one outside it.

    A crop cannot do this at all: rolling to the least discontinuous cut only moves the step
    inside the tile, where the eye still finds it (the 2x2 A/B in the ART-0 card rated the
    rolled sheet as seamless by the metric and worse by eye).

    Returns (image, note).
    """
    a = np.asarray(img.convert("RGB")).astype(np.float64)
    h, w = a.shape[:2]
    t = (np.arange(band, dtype=np.float64) + 1.0) / (band + 1.0)
    parts = []
    for axis, n, name in ((1, w, "X"), (0, h, "Y")):
        cut_before, inner = wrap_stats(a, axis)
        tail = a.take(np.arange(n - band, n), axis=axis).copy()
        # Mirrored head: index band-1 .. 0 along THAT axis, so the band ends exactly on H[0].
        head = a.take(np.arange(band)[::-1], axis=axis).copy()
        shape = [1, 1, 1]
        shape[axis] = band
        f = t.reshape(shape)
        blended = (1.0 - f) * tail + f * head
        if axis == 1:
            a[:, n - band:, :] = blended
        else:
            a[n - band:, :, :] = blended
        cut_after, _ = wrap_stats(a, axis)
        outside = max(_band_max_step(a, axis, 0, band + 1), _band_max_step(a, axis, n - band - 1, n - 1))
        inside = _band_max_step(a, axis, n - band, n)
        parts.append("%s cut %.2f -> %.2f (grain %.2f), step in band %.0f vs outside %.0f"
                     % (name, cut_before, cut_after, inner, inside, outside))
    out = Image.fromarray(np.clip(np.round(a), 0, 255).astype(np.uint8), "RGB")
    return out, "wrap blend band %d (mirror): %s" % (band, "; ".join(parts))


def main():
    os.makedirs(LEVEL_DIR, exist_ok=True)
    os.makedirs(ART_DIR, exist_ok=True)
    guid_rng = random.Random(20260918)
    rows = []

    # `python Tools/gen-level-textures.py ground_cap` repaints one file and leaves the other
    # assets byte-identical (their guids are reused, so Unity sees no change at all).
    want = set(sys.argv[1:])
    if want:
        unknown = want - set(["ground_fill", "ground_cap", "sky", "sign_finish"]) - set(MATERIALS)
        assert not unknown, "unknown key(s): %s" % sorted(unknown)

    level = [
        ("ground_fill", paint_ground_fill, 128, False, False),
        ("ground_cap", paint_ground_cap, 100, False, True),
        ("sky", paint_sky, 32, False, False),
        ("sign_finish", paint_sign_finish, 100, False, True),
    ]
    for name, painter, ppu, readable, alpha in level:
        if want and name not in want:
            continue
        img = painter()
        path = os.path.join(LEVEL_DIR, name + ".png")
        size = save(img, path, ppu, readable, alpha, guid_rng)
        seam, inner = seam_error(img)
        note = "tile seam x: %.2f vs inner %.2f" % (seam, inner)
        if name == "ground_cap":
            worst, wx, step, spike, bright, bx, tuft = cap_tick_report(img)
            tops = np.array([int(np.argmax(np.asarray(img.convert("RGBA"))[:, :, 3][:, x] > 0))
                             if np.asarray(img.convert("RGBA"))[:, :, 3][:, x].any() else -1
                             for x in range(img.size[0])])
            note += ("; tip rows %d..%d std %.2f, %d distinct," %
                     (tops.min(), tops.max(), float(tops.std()), len(set(tops.tolist()))))
            note += ("; worst dark column %.1f at x=%d, worst BRIGHT %.1f at x=%d, worst height spike %.0f px,"
                     " mean step %.2f, tuft columns %.0f%%"
                     % (worst, wx, bright, bx, spike, step, tuft * 100))
        rows.append(("Resources/Level/" + name + ".png", "%dx%d" % img.size, ppu,
                     "%d KB" % (size // 1024) if size >= 1024 else "%d B" % size, note))

    for key in MATERIALS:
        if want and key not in want:
            continue
        src = os.path.join(LEGACY, key + ".png")
        before = os.path.getsize(src)
        img = Image.open(src).convert("RGB")
        src_size = img.size
        down = img.resize((512, 512), Image.LANCZOS)
        out, note = level_tile(down)
        band = WRAP_BLEND.get(key, 0)
        if band:
            out, blend_note = wrap_blend(out, band)
            note += "; " + blend_note
        path = os.path.join(ART_DIR, key + ".png")
        size = save(out, path, 512, True, False, guid_rng)
        rows.append(("Resources/Art/" + key + ".png",
                     "%dx%d -> %dx%d" % (src_size[0], src_size[1], 512, 512), 512,
                     "%d KB -> %d KB" % (before // 1024, size // 1024), "readable; " + note))

    meta_dir = os.path.join(ROOT, "Assets", "Resources", "Art.meta")
    if not os.path.exists(meta_dir):
        folder_meta = os.path.join(ROOT, "Assets", "Resources", "Level.meta")
        with open(folder_meta, "r", encoding="utf-8") as fh:
            text = fh.read()
        text = re.sub(r"^guid: .*$", "guid: " + new_guid(guid_rng), text, count=1, flags=re.MULTILINE)
        with open(meta_dir, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)

    print("%-38s %-22s %5s %18s %s" % ("file", "pixels", "PPU", "bytes", "note"))
    for r in rows:
        print("%-38s %-22s %5s %18s %s" % r)
    print("\nwrote %d PNGs + 1 folder meta" % len(rows))


if __name__ == "__main__":
    main()
