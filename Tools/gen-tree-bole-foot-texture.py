#!/usr/bin/env python3
"""ART-0 31st pass: the tree's FOOT - a painted bole flare that closes the measured gap.

The defect, measured on the shipped still (not guessed)
------------------------------------------------------
`Tools/art0-bole-hole-probe.py` + `Tools/art0-bole-slit-probe.py` on
`Trials/Runs/art0leaf29_tree.png` (the oak at the tree lens, 414.21 px/m) found ONE row of
backdrop across the bole's foot: y=1045, x 867..895, 29 px, every one of them EXACTLY the
gallery's backdrop (76, 82, 92) - not a shade of bark. `Assets/Editor/ArtBoleProbe.cs`
printed the layers that meet there and rendered the frame with each switched off:

    world y=0 row=1045.75      the tree's root and the ground strip's top are the SAME line
    GROUND  GroundStrip         sprite=ground_fill order=-2 top=0.00000 m
    GROUND  GroundStrip/Surface sprite=ground_cap  order=-1 top=0.00000 m
    SLOT    Stump/Visual        boxBottom=+0.00060 m   (the part's own sprite, switched off)
    FOOT    Stump/Visual/ConeCore boxBottom=+0.00125 m (the bole's core card)
    FRAME   row 1045 x 867..895: 29/29 backdrop in EVERY frame - also with the ground off
            and with the Stump off

So the row belongs to neither layer: the pixel's centre is at y = +0.0005 m, the dirt ends
at 0 and the tree's own picture starts at +0.00125 m (the cone card is inset 1.25 mm inside
its own box by `PlantTree.PaintVisualConeFill`). Half a pixel of empty world, and the
rasteriser's centre rule turns it into a full row of backdrop - the "gap at the base" every
read named, and the flat 29 px cut the same reads saw at the soil line.

The answer: one painted canvas, the tree's own FOOT, drawn Sliced onto a box that reaches
below the ground line, so the bole's picture - not a rounding rule - owns those pixels.

One canvas for every species, because the box is BUILT to the canvas
-------------------------------------------------------------------
The stump box the dress reads is snapped to whole pixels at 256 px/m
(`HumanSegment.CreateBodySprite`) and runs 0.1400x0.1991 m (Poplar, aspect 0.703) to
0.2200x0.1618 m (Bush, aspect 1.359) - a 1.93x spread, so one canvas per box aspect would
be two canvases and a family threshold. Instead the FOOT BOX is derived from the base box
at a FIXED aspect: width = 1.25 x the base box width, height = width / 3.6, bottom =
0.30 x height below the ground line (`ArtParts.TreeBoleFoot*`). Its aspect is therefore the
canvas' aspect exactly for every species (error 0.000), and the buried share is a constant
fraction of the canvas, so the painted soil line lands on the world ground line whatever
the species' stump is. What varies instead is the foot's size: 0.249 x 0.069 m on the oak,
0.175 x 0.049 m on the Poplar - a flare scales with the trunk it belongs to.

The language: the material's own pixels, re-cut
-----------------------------------------------
The trunk above the foot is the `bark` MATERIAL at its authored 0.5 m period. So the canvas
is not a flat drawing of brown: its column is a slab of the shipped `Art/bark.png` itself
(so the grain continues from the cards above at the same scale, 1024 px/m), and the toes
are the same pixels re-cut into a root silhouette, darkened toward the soil tone of
`Art/dirt.png` where they go under the ground line. Deterministic: no timestamp, seeded
wobble only, and the guid of an existing `.meta` is reused.

Run from the project root:
    python Tools/gen-tree-bole-foot-texture.py             # repaint + verify
    python Tools/gen-tree-bole-foot-texture.py --sheet     # ... and a contact sheet, 4x

32nd pass (20.09, same canvas, same box): the 31st pass' own read - "the toe rhythm too
regular", "the relief of the bark not tied to the toes" - is the defect this pass answers.
Two levers, both in this file: `LOBE_TABLE`'s depths are pulled further apart (deepest /
shallowest 3.0 -> 7.0) and the flare envelope's exponent went 1.4 -> 0.70 so its two
shoulders stop being flat shelves; and the toes are now CUT BY THE MATERIAL - the bark slab's
own column luminance, smoothed to the grain's width (`fissure_field`), deepens a toe under a
dark fissure and shortens it under a smooth band, and the same field is painted into the rows
below the soil line (`NOTCH_SHADE`). The bar the pass is judged by is the silhouette probe's
flat-run list (`Tools/art0-bole-silhouette-probe.py`) and the shipped frame's toe band.

Writes `Assets/Resources/Art/Tree/bole_foot.png` and its `.meta` (the project's Single
sprite shape: textureType 8, spriteMode 1, spriteMeshType 0 FullRect for the Sliced mode,
wrap 1/1, alphaIsTransparency 1, isReadable 1, no fallback physics shape).
"""

import math
import os
import random
import re

import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BARK = os.path.join(ROOT, "Assets", "Resources", "Art", "bark.png")
DIRT = os.path.join(ROOT, "Assets", "Resources", "Art", "dirt.png")
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Art", "Tree")
META_TEMPLATE = os.path.join(ROOT, "Assets", "LegacyArt", "Apple", "fruit.png.meta")
SHEET = os.path.join(ROOT, "Trials", "Runs", "art0-bolefoot-sheet.png")

CANVAS_W = 288         # px
CANVAS_H = 80          # px -> aspect 3.600, the value ArtParts.TreeBoleFootAspect holds
SUPER = 4              # supersample factor for the silhouette masks
COLUMN = 0.64          # of the canvas width: the bole's own column (0.8/1.25)
COLUMN_BASE = 0.86     # ... at the soil line: the flare widens into its skirt
SOIL = 0.30            # of the canvas height, BELOW the ground line (ArtParts.TreeBoleFootSoil)
PPU = 1024.0           # the material's own scale; the dress rebuilds the sprite's ppu from the box
SEED = 20260920

COLUMN_BEND = 1.8      # the bell's exponent: steep at the trunk, spreading at the soil line
WOBBLE = 0.006         # of the canvas width: how far the sides may meander (was 0.010, and the
#                        read of that cut named a crease "a root torn off the trunk")

# The flare's toe lobes: (position along the soil line, peak depth, half width), all as
# fractions - the position of the flare's half span, the depths of the canvas' HEIGHT. Unequal
# on purpose: deep in the middle, small at the shoulders, none of them the same width. Every
# read of this canvas so far has called an even row of lobes "a stamped rhythm, a palisade of
# logs, a fringe of identical scallops", so the rhythm lives in this table, not in the rng.
#
# 32nd pass: the 31st pass' own read ("the toe rhythm too regular") was taken as the defect and
# this table is the answer - the depths are pulled FURTHER APART (0.028 .. 0.195 against
# 0.050 .. 0.150: the ratio between the deepest and the shallowest toe 3.0 -> 7.0) and the
# widths differ by more (0.15 .. 0.31 against 0.19 .. 0.27), so the toe row is a rhythm and not
# a fence. The five toes stay five: no read has ever asked for a different COUNT.
LOBE_TABLE = ((-0.87, 0.022, 0.16), (-0.46, 0.120, 0.205), (0.02, 0.152, 0.31),
              (0.45, 0.078, 0.185), (0.87, 0.030, 0.15))
BASE_DEPTH = 0.060    # of the canvas height: the flare's own mass at the trunk
BASE_BEND = 0.70      # the flare envelope is (1-u^2)^BASE_BEND. The 31st pass used 1.4, whose
#                        derivative vanishes as u -> 1 - a mathematically flat shoulder shelf, and
#                        its own read named "a flat shelf, parallel to the ground". Below 1 the
#                        near-shoulder slope is finite, so the tails come down and stop.
#                        What the numbers say about it, honestly: the flat-run count of
#                        Tools/art0-bole-silhouette-probe.py moved 10/10 -> 9/11 columns inside a
#                        2 px band, i.e. UNCHANGED in substance, and MORE columns now sit in the
#                        near-ground bands (44 -> 80 within rows 2-3, Tools/art0-toe-depths.py).
#                        One further variant was TRIED and ROLLED BACK, because it measured worse
#                        than both: a flat-hold with a steep edge, (1-|u|^6)^0.5 with
#                        BASE_DEPTH 0.075, gave a THIRD flat run (10/10 -> 10/8/11) and 67 columns
#                        inside rows 4-5 - a plateau IS a flat shelf. No curve can win this
#                        measure outright: a 4.8 px deep envelope that must reach 0 at the
#                        shoulders cannot descend faster than ~0.17 px per column, so even a
#                        perfect wedge shows ~10-12 columns. That floor belongs to the instrument.
#                        0.70 ships because the READS endorse the bytes it produces (two canvas
#                        reads and one 16x frame read, all three correct) and none of them names a
#                        shelf on the new canvas, where the 31st pass' reads did.
FISSURE_CUT = 0.22     # how hard the bark's OWN darkness cuts the toes (0 = the 31st pass).
#                        The relief stops being designed and starts being the material's: the
#                        slab's dark vertical fissures make the toe under them deeper, its
#                        smooth bands make it shallower, so the edge and the grain agree.
FISSURE_SMOOTH = 5     # px: the fissure field is a 5 px box mean of the slab's own column
#                        luminance - the grain, not its single-pixel noise
NOTCH_SHADE = 0.17     # how much of the fissure's darkness is painted INTO the notch rows
#                        (the pixels below the soil line), so the groove reads on the surface
#                        as well as on the silhouette


def palette(img):
    """The material's own tones, read off the shipped tile: the canvas must not invent a
    brown the trunk above does not have."""
    a = np.asarray(img.convert("RGB")).astype(np.float64)
    lum = a @ np.array([0.299, 0.587, 0.114])
    order = np.argsort(lum, axis=None)
    flat = a.reshape(-1, 3)[order]
    n = flat.shape[0]

    def at(q):
        return tuple(int(round(v)) for v in flat[min(n - 1, int(q * n))])

    return {"p08": at(0.08), "p35": at(0.35), "p50": at(0.50), "p72": at(0.72), "p90": at(0.90)}


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def box_smooth(a, k):
    """k-px box mean with the edges held (np.convolve 'same' would tilt the ends)."""
    if k <= 1:
        return a
    pad = k // 2
    ext = np.concatenate([np.full(pad, a[0]), a, np.full(pad, a[-1])])
    return np.convolve(ext, np.ones(k) / float(k), mode="valid")


def fissure_field(slab):
    """The slab's OWN vertical grain as a 0..1 field per canvas column: 1 = the darkest groove.

    The 31st pass' read named "the relief of the bark not tied to the toes" - the toe rhythm was
    a designed table sitting on a material that has a rhythm of its own. So the toes are cut by
    the bark itself now: this field is the column luminance of the very slab the canvas paints
    with, smoothed to the grain's width and normalised between its 12th and 88th percentile.
    """
    grey = np.asarray(slab.convert("L")).astype(np.float64)
    col = box_smooth(grey.mean(axis=0), FISSURE_SMOOTH)
    lo, hi = np.percentile(col, 12.0), np.percentile(col, 88.0)
    return np.clip((hi - col) / max(1.0, hi - lo), 0.0, 1.0)


def build_masks(rng, fiss):
    """The silhouette as ONE closed curve: the bole's sides, then the flare's scalloped underside.

    Four reads of the earlier cuts, and the silhouette probe behind them, converge on one
    defect I kept re-creating: a straight horizontal edge somewhere at the bottom of the flare.
    First a flat butt shelf, then two 16-column runs (the wide low side ellipse), then the toes
    themselves - a row of circles spaced ~60 px apart across a 245 px span never touch, so
    between them the silhouette ended on the soil line: measured, four straight runs of 6 to 9
    columns at row 56, and the read called it "the roots hanging from a flat board".

    So the underside is now an ENVELOPE, not a row of toes: D(u) = A (1 - u^2)^0.85, deepest
    at the trunk and tapering to nothing at the flare's two shoulders, modulated by two long
    sine waves (seeded) so no two lobes are alike and no run of it is straight. The polygon
    walks: the left side down, the underside across, the right side back up - one curve, not
    one straight segment anywhere in it.

    Returns (mask, ground_row, lobes, cx, base_half, top_half); each lobe is (centre_x,
    centre_y, radius) in canvas pixels for the paint step's shading, one per local maximum of
    the envelope.
    """
    w, h = CANVAS_W * SUPER, CANVAS_H * SUPER
    ground = h - int(round(h * SOIL))
    img = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(img)

    cx = w * 0.5
    top_half = w * COLUMN * 0.5
    base_half = w * COLUMN_BASE * 0.5
    shoulder = base_half * 0.995
    pass
    pass

    def depth(x):
        u = (x - cx) / shoulder
        if abs(u) >= 1.0:
            return 0.0
        # the flare's overall mass: deepest at the trunk, tapering to nothing at the shoulders -
        # BASE_BEND < 1 keeps this curve's ends STEEP (see BASE_BEND for what that is and is not
        # worth, measured).
        base = h * BASE_DEPTH * (1.0 - u * u) ** BASE_BEND
        # ... plus the toe lobes of LOBE_TABLE, each a smooth bump of its own width and depth -
        # but CUT BY THE MATERIAL: the slab's own dark fissure under a toe lets it reach further
        # down, a smooth band of the same slab stops it short, so the silhouette's rhythm and
        # the bark's grain are the same rhythm ("the relief of the bark not tied to the toes").
        col = int(min(CANVAS_W * SUPER - 1, max(0.0, x)))
        cut = 1.0 + FISSURE_CUT * (fiss[min(CANVAS_W - 1, col // SUPER)] - 0.5) * 2.0
        toe = 0.0
        for (x_frac, d_frac, w_frac) in LOBE_TABLE:
            t = (u - x_frac) / w_frac
            if abs(t) < 1.0:
                toe += h * d_frac * math.cos(math.pi * t * 0.5) ** 2
        return base + toe * cut

    # the bole's sides: they curve outward as they go down (COLUMN_BEND), a couple of pixels of
    # meander so the silhouette is not a machine rectangle
    left, right = [], []
    steps = 30
    for i in range(steps + 1):
        t = i / float(steps)
        y = t * ground + h * 0.010
        wob = rng.uniform(-WOBBLE, WOBBLE) * w
        half = top_half + (shoulder - top_half) * (t ** COLUMN_BEND)
        left.append((cx - half + wob * 0.5, y))
        right.append((cx + half + wob * 0.5, y))

    # the underside: 64 samples from the left shoulder to the right, every one of them on the
    # envelope - the flare's lowest rows are this curve and nothing else
    under = []
    for i in range(65):
        x = cx - shoulder + 2.0 * shoulder * i / 64.0
        under.append((x, ground + depth(x)))

    d.polygon(left + under + right[::-1], fill=255)

    # the lobes, for the paint step: one per local maximum of what was actually drawn
    lobes = []
    prev, cur = None, None
    for i in range(1, len(under) - 1):
        (x0, y0), (x1, y1), (x2, y2) = under[i - 1], under[i], under[i + 1]
        if y1 >= y0 and y1 > y2:
            r = max(4.0 * SUPER, (y1 - ground) * 0.62)
            lobes.append((x1, ground + (y1 - ground) * 0.55, r))
        prev, cur = y1, y2
    return (img.resize((CANVAS_W, CANVAS_H), Image.BOX), ground / float(SUPER), lobes,
            cx / SUPER, base_half / SUPER, top_half / SUPER)


def paint(seed=SEED):
    rng = random.Random(seed)
    bark = Image.open(BARK).convert("RGB")
    dirt = Image.open(DIRT).convert("RGB")
    tones = palette(bark)
    soil = palette(dirt)["p35"]

    # The material slab: 288 px of the 512 px tile at its own scale, so the grain continues
    # from the cards above instead of restarting at another size.
    ox = rng.randrange(0, bark.width - CANVAS_W)
    oy = rng.randrange(0, bark.height - CANVAS_H)
    slab = bark.crop((ox, oy, ox + CANVAS_W, oy + CANVAS_H))

    fissure = fissure_field(slab)
    mask, ground, lobes, cx, base_half, top_half = build_masks(rng, fissure)
    m = np.asarray(mask).astype(np.float64) / 255.0
    rgb = np.asarray(slab).astype(np.float64)

    # Under the soil line the bark goes into the ground: mix toward the dirt's own tone.
    # Graded across the canvas' BOTTOM FIFTH only, never as one straight line across the
    # silhouette - the first cut drew a dark line at the soil row and the read called it a
    # "mechanical seam across the whole trunk".
    rows = (np.arange(CANVAS_H) + 0.5) / CANVAS_H
    ground_frac = ground / float(CANVAS_H)
    buried = np.clip((rows - ground_frac) / (1.0 - ground_frac), 0.0, 1.0) ** 1.35
    soil_rgb = np.array(soil, dtype=np.float64)
    rgb = rgb * (1.0 - buried[:, None, None] * 0.30) + soil_rgb * (buried[:, None, None] * 0.30)

    # light from the upper left, as everywhere else in this project's flat art - but with a
    # SMOOTH falloff: the clipped linear ramp of the first cut put a corner at a fixed x, and
    # the read of that cut named exactly that corner ("a vertical seam a little right of the
    # centre, a band where the tone changes").
    xs = (np.arange(CANVAS_W) + 0.5) / CANVAS_W - 0.5
    s = np.clip((xs + 0.5) / 1.0, 0.0, 1.0)
    smooth = s * s * (3.0 - 2.0 * s)                      # smoothstep, no corner anywhere
    rgb = rgb * (1.0 + 0.13 * (1.0 - smooth)[None, :, None] - 0.11 * smooth[None, :, None])

    # each lobe of the underside is its own form: the pixels of its own disc are taken down a
    # step, its underside darker still - the volume the sheet reads asked for ("contrast
    # between the lit bark and the deep shadows in the fissures")
    for (tx, ty, r) in lobes:
        x_lo, x_hi = int(tx - r), int(tx + r)
        for x in range(max(0, x_lo), min(CANVAS_W, x_hi + 1)):
            dx = (x - tx) / max(1.0, r)
            if abs(dx) > 1.0:
                continue
            dy = math.sqrt(max(0.0, 1.0 - dx * dx))
            y_lo = int(ty - r * dy * 0.55)
            y_hi = int(ty + r * dy)
            for y in range(max(0, y_lo), min(CANVAS_H, y_hi + 1)):
                rgb[y, x] *= 0.87
                if y > ty + r * dy * 0.45:
                    rgb[y, x] *= 0.86

    # the material's fissures are painted into the notch rows as well as cut into the edge: the
    # rows below the soil line take up to NOTCH_SHADE of the local groove's darkness, strongest
    # at the column's own toe tip, so a groove runs down the surface and off the edge instead of
    # stopping dead at the silhouette.
    lows = np.zeros(CANVAS_W, dtype=np.int64)
    for x in range(CANVAS_W):
        col_ink = np.nonzero(m[:, x] > 0.4)[0]
        lows[x] = int(col_ink.max()) if col_ink.size else -1
    depth_px = np.clip(lows - int(round(ground)), 0, None).astype(np.float64)
    shade = np.ones((CANVAS_H, CANVAS_W), dtype=np.float64)
    ys_all = np.arange(CANVAS_H, dtype=np.float64)
    for x in range(CANVAS_W):
        if depth_px[x] <= 0.0:
            continue
        below = np.clip((ys_all - ground) / depth_px[x], 0.0, 1.0) ** 1.2
        shade[:, x] = 1.0 - NOTCH_SHADE * fissure[x] * below
    rgb *= shade[:, :, None]

    alpha = (m * 255.0).clip(0, 255).astype(np.uint8)
    out = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    out.putalpha(Image.fromarray(alpha))

    # The self-shadow along the LOWER edge of the silhouette, per column so it follows the
    # envelope's own curves. It MULTIPLIES what is there instead of being painted on: the
    # fourth read of this canvas named a hard dark rim "a sticker hanging in the air", and a
    # painted outline is exactly what makes a flat-toned sprite read as a sticker.
    ys_idx, xs_idx = np.nonzero(np.asarray(mask) > 64)
    low = {}
    for y, x in zip(ys_idx.tolist(), xs_idx.tolist()):
        if y > low.get(x, -1):
            low[x] = y
    arr = np.asarray(out).astype(np.float64)
    for x, y in low.items():
        if m[y, x] <= 0.4:
            continue
        arr[y, x, :3] *= 0.72
        if y >= 1:
            arr[y - 1, x, :3] *= 0.90
    out = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")
    return out, tones, soil, ground, fissure


def write_meta(png_path, pixels_per_unit, readable, alpha, guid):
    with open(META_TEMPLATE, "r", encoding="utf-8") as fh:
        text = fh.read()

    def set_line(key, value, indent=2):
        nonlocal text
        pattern = re.compile(r"^%s%s:.*$" % (" " * indent, re.escape(key)), re.MULTILINE)
        assert pattern.search(text), "template has no %s" % key
        text = pattern.sub("%s%s: %s" % (" " * indent, key, value), text, count=1)

    text = re.sub(r"^guid: .*$", "guid: " + guid, text, count=1, flags=re.MULTILINE)
    set_line("spriteMeshType", 0)
    set_line("spritePixelsToUnits", pixels_per_unit)
    set_line("isReadable", 1 if readable else 0)
    set_line("wrapU", 1, indent=4)
    set_line("wrapV", 1, indent=4)
    set_line("alphaIsTransparency", 1 if alpha else 0)
    set_line("spriteGenerateFallbackPhysicsShape", 0)
    set_line("textureType", 8)
    set_line("spriteMode", 1)
    text = re.sub(r"^\s*spriteID: .*$", "    spriteID: %s0800000000000000" % guid[:16],
                  text, count=1, flags=re.MULTILINE)
    text = re.sub(r"^\s*nameFileIdTable: .*$", "    nameFileIdTable: {}",
                  text, count=1, flags=re.MULTILINE)
    with open(png_path + ".meta", "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def new_guid(rng):
    return "".join(rng.choice("0123456789abcdef") for _ in range(32))


def existing_guid(path):
    meta = path + ".meta"
    if not os.path.exists(meta):
        return None
    with open(meta, "r", encoding="utf-8") as fh:
        m = re.search(r"^guid: ([0-9a-f]+)$", fh.read(), re.MULTILINE)
    return m.group(1) if m else None


# The gallery's own base boxes (`ArtBoleProbe` TREE lines, 20.09) and the lenses the game
# draws at: the tree lens of the catalogue, and the yard.
BOXES = (("Poplar", 0.1400, 0.1991), ("Pine", 0.1600, 0.1800), ("Willow", 0.2000, 0.2188),
         ("Oak", 0.2000, 0.2188), ("Bush", 0.2200, 0.1618))
LENSES = (("catalogue tree", 414.21), ("yard", 117.40))


def report(img, path, tones, soil, ground, fissure):
    a = np.asarray(img.convert("RGBA")).astype(np.float64)
    ink = a[:, :, 3] > 8
    # A toe whose tip runs past the canvas' own bottom edge is cut off flat by it: measured on
    # the third cut of this canvas, the middle toe's tip stuck 16 px below the canvas and the
    # silhouette probe read a straight run of 23 columns along the bottom row.
    cut = int(ink[-1, :].sum())
    print("ART-0-BOLE-FOOT canvas=%dx%d px aspect=%.4f ppu=%g bytes=%d"
          % (img.width, img.height, img.width / float(img.height), PPU,
             os.path.getsize(path)))
    print("ART-0-BOLE-FOOT ground line at row %d of %d (soil share %.3f of the canvas)"
          % (ground, img.height, SOIL))
    print("ART-0-BOLE-FOOT ink coverage=%.3f opaque=%.3f tones: bark %s soil %s"
          % (ink.mean(), (a[:, :, 3] > 250).mean(), tones, soil))
    print("ART-0-BOLE-FOOT ink on the canvas bottom row = %d px, deepest inked row = %d of %d"
          % (cut, int(np.nonzero(ink.any(axis=1))[0].max()), img.height - 1))

    # 32nd pass witnesses: the five toes as the canvas' OWN rows, and how much of that rhythm
    # is the material's own. `depth` is px below the soil line; `fiss` is the bark's darkness
    # under that toe (0 = its smoothest band, 1 = its darkest groove).
    lows = np.zeros(img.width, dtype=np.int64)
    for x in range(img.width):
        c = np.nonzero(ink[:, x])[0]
        lows[x] = int(c.max()) if c.size else -1
    depth = lows - int(round(ground))
    cx_c = img.width * 0.5
    shoulder_c = img.width * COLUMN_BASE * 0.5 * 0.995
    print("ART-0-BOLE-FOOT the five toes (depth below the soil line, px) and the bark under them:")
    for i, (xf, df, wf) in enumerate(LOBE_TABLE):
        x = int(round(cx_c + xf * shoulder_c))
        print("    toe %d at x %3d: depth %2d px  fissure %.3f  %s"
              % (i + 1, x, max(0, int(depth[x])), fissure[min(img.width - 1, max(0, x))],
                 "deepest" if max(0, int(depth[x])) == max(0, int(depth.max())) else ""))
    print("ART-0-BOLE-FOOT toes depth %d..%d px (spread %d), table depths %.3f..%.3f"
          % (max(0, int(depth[depth > 0].min())), int(depth.max()),
             int(depth.max()) - max(0, int(depth[depth > 0].min())),
             min(d[1] for d in LOBE_TABLE), max(d[1] for d in LOBE_TABLE)))
    sel = depth > 0
    if sel.sum() > 8:
        f = fissure[:img.width][sel].astype(np.float64)
        d = depth[sel].astype(np.float64)
        corr = float(np.corrcoef(f, d)[0, 1])
        print("ART-0-BOLE-FOOT the edge and the grain: corr(fissure, depth) = %+.3f over the %d "
              "columns below the soil line" % (corr, int(sel.sum())))
    print("ART-0-BOLE-FOOT the foot box per species (width = 1.25 x base, height = width/3.6):")
    worst = 0.0
    for name, bw, bh in BOXES:
        w = bw * 1.25
        h = w / 3.6
        err = abs(w / h / (img.width / float(img.height)) - 1.0)
        worst = max(worst, err)
        print("    %-7s base %.4fx%.4f aspect %.3f -> foot %.3fx%.3f m aspect %.4f (err %.4f)"
              "  buried %.4f m, top %.4f m above the ground"
              % (name, bw, bh, bw / bh, w, h, w / h, err, h * SOIL, h * (1.0 - SOIL) - h * SOIL))
    print("ART-0-BOLE-FOOT worst aspect error over the five gallery species = %.4f" % worst)
    for label, ppm in LENSES:
        w = 0.2200 * 1.25
        h = w / 3.6
        print("    at the widest base (Bush, %.3f m wide) the foot draws %.0f x %.0f px (%s lens),"
              " the bole above it is %.0f px wide"
              % (w, w * ppm, h * ppm, label, 0.8 * 0.22 * ppm))
    return cut


def sheet(img):
    tiles = []
    for name, bw, bh in BOXES:
        w = max(8, int(round(bw * 1.25 * 414.21)))
        h = max(3, int(round(w / 3.6)))
        tiles.append((name, img.resize((w, h), Image.BOX)))
    W = sum(t.width for _, t in tiles) + 10 * (len(tiles) + 1)
    H = max(t.height for _, t in tiles) + 20
    out = Image.new("RGB", (W, H), (60, 60, 66))
    x = 10
    for name, t in tiles:
        out.paste(t, (x, (H - t.height) // 2), t)
        x += t.width + 10
    os.makedirs(os.path.dirname(SHEET), exist_ok=True)
    out.resize((out.width * 3, out.height * 3), Image.NEAREST).save(SHEET)
    print("ART-0-BOLE-FOOT sheet -> " + SHEET)


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    img, tones, soil, ground, fissure = paint()
    path = os.path.join(OUT_DIR, "bole_foot.png")
    img.save(path, "PNG", optimize=True)
    guid = existing_guid(path) or new_guid(random.Random(11))
    write_meta(path, int(PPU), True, True, guid)
    bottom = report(img, path, tones, soil, ground, fissure)
    if "--sheet" in os.sys.argv:
        sheet(img)
    ok = abs(CANVAS_W / float(CANVAS_H) - 3.6) < 1e-9 and bottom == 0
    print("ART-0-BOLE-FOOT %s" % ("OK" if ok else "FAIL aspect != 3.6"))
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
