#!/usr/bin/env python3
"""ART-0 gap 3b, the two last rows: the lavender SPIKE and the autumn HIP.

Why this file exists
--------------------
The 23rd pass painted the flower's foliage (one canvas per leaflet family). The inventory's
gap 3b left two rows behind, and they are the only plant pictures in the game that still
arrive as a pile of flat cards:

  * `PlantFlower.PaintSpikeFlorets` (line 604) builds `Floret0/1/2` + `FloretTip0/1/2` - six
    flat boxes, one tint each, hard 90-degree corners - and switches the `Spike_i` part's own
    sprite OFF (`spike.sprite.enabled = false`, line 611). The autumn twin
    `PaintSpentSpike` (line 677) does the same with `SpentFloret0/1/2` + `SpentFloretTip0/1/2`.
  * `PaintSpentBloom` (line 412) builds `Spent`, `SpentTip`, `SpentR`, `SpentRTip`, `SpentCap`,
    `SpentCapTip`, `SpentCapR`, `SpentCapRTip`, `SpentCapFill`, `SpentCapFillTip`,
    `SpentCapFillR`, `SpentCapFillRTip` - twelve flat boxes for the autumn seed head - and
    switches the `Bloom` part's own sprite OFF (line 420).

So the bloom beside them carries a painted shading ramp while the spike and the hip are still
rectangles. The eye's read of the shipping frame `Trials/Runs/art0leaflet_flower.png`: the
petals and the leaves "curved contours ... a soft gradient" (23rd pass), the spike and the hip
"chopped straight strips".

The answer is the same as the foliage's: ONE canvas on the part's own box, drawn in the part's
own aspect, `Sliced` onto the box the part already has (`painted-parts.mdc`: one bone, one PNG;
the box is the mapping, never `localScale`). The rig's cards step aside.

What the loader needs (quoted, not guessed)
-------------------------------------------
`Assets/ArtLibrary.cs`

    Albedo(key):  Texture2D tex = Resources.Load<Texture2D>("Art/" + key);      // line 139
    Tiled(key, metersPerTile):
                  float ppu = tex.width / metersPerTile;                         // line 165
                  Sprite.Create(tex, full rect, pivot .5/.5, ppu, 0, FullRect)

`Assets/ArtParts.cs`

    Dress(sr, key, tileMeters, real, keepColor, enable):
                  float tile = ... Mathf.Max(box.x, box.y);      // tileMeters == 0
                  Sprite sprite = ArtLibrary.Tiled(key, tile);
                  sr.drawMode = Sliced;                          // mode first (21st pass)
                  sr.sprite = sprite; sr.size = box; sr.enabled = true;

A key is therefore a READABLE Texture2D under `Assets/Resources/Art/`, the key is a path
fragment (`Flower/spike` -> `Art/Flower/spike.png`), and both canvases are LUMINANCE canvases
(grey RGB, the shape in alpha): the flower part keeps its own kind colour and the renderer
MULTIPLIES the canvas, so one canvas serves the summer spike and the autumn one.

The boxes (measured, not guessed)
--------------------------------
`PlantFlower.ApplyKind`: the spike is `FlowerKind.Lavender` only (`spikeCount = 4`), at
`bloomSize` 0.028 x 0.050 m, and every one of the four spikes carries that same box, so the
canvas is drawn in it as the box draws it.

The hip is drawn by the `Bloom` part of the other ELEVEN kinds IN AUTUMN (`spentHead` at
`PlantFlower` line 222), and the box the dress READS is not the authored `bloomSize`:
`HumanSegment.CreateBodySprite` snaps the placeholder to whole pixels at 256 px/m with a 64 px
minimum side and takes ppu from the width, so the drawn box aspect is an integer pixel ratio
(`live_box()`, printed per kind below). A spring or summer rig draws the petal fan instead, so
these eleven boxes are the whole set a hip canvas is ever drawn in. They span 0.6957 (Tulip) ..
0.8205 (Lily) | 1.0000 (Daisy, Sunflower) .. 1.1562 (Iris) | 1.4062 (Poppy) - a factor 2.02.

WHAT THE BOX DOES (26th pass, measured - the 24th/25th passes' "aspect error" reading was not
this). `Dress` fills the part's box with the canvas in `Sliced` mode at `size = box`, so canvas
u lands on box.x and v on box.y and the drawn form is (form x box): the BOX shapes the form and
the canvas' own aspect only sets the sampling grid. Measured, not argued - drawn into the
Poppy's box, the 165 x 172 single canvas and the 227 x 161 wide canvas both give a 48 x 37 px
form (`Tools/art0-hip-lens-ab.py`). So a family canvas that only changes the pixel grid buys
sampling, nothing else: the SHAPE has to be PRE-SQUASHED by the reciprocal of what the family's
box will do to it, and that squash belongs to the painting, not to the raster. `hip_form_scale`
is the number, referenced to the ROUND family's mean box - the shape the accepted rose hip
already shows - and it is aspect(round box) / aspect(this family's box):

    `Flower/hip`       round   Daisy, Sunflower, Violet, Rose, Daffodil, Iris   squash x1.000
    `Flower/hip_tall`  tall    Tulip, Lily, Crocus, Bluebell                    squash x1.457
    `Flower/hip_wide`  wide    Poppy alone (the one flat bloom)                  squash x0.764

THE SQUASH IS FITTED, NOT PAINTED THROUGH (measured, not assumed - the first version of this
pass wrote the squash as a plain widening and its own `border_pixels` gate failed every hip: a
1.457 x widening of the round form's half-width 0.356 is 0.519, i.e. 0.019 past the canvas
edge). So each canvas is painted twice: once as the painter writes it, then again through
`fitted_map`, which keeps the ratio `sx / sv` exactly, scales the PAIR down until a half-extent
reaches `HIP_FIT_U` / `HIP_FIT_V` (0.480, i.e. a 2% margin), and shifts by the least amount that
brings the form back off an edge. The fitted maps are printed by `main` and are, on today's
bytes: hip identity (no squash, no shift - the round canvas ships byte for byte), hip_tall
x1.357 / y0.931 placed at u +0.000, hip_wide x0.764 / y1.000. The fit only ever SHRINKS a form,
so no canvas grows past the picture the round family already had.

The result, per kind, is in the table `main` prints: every kind draws the same hip form up to
its family's own box spread - worst per-kind error against the round hip 1.116x (Lily, tall),
1.077x (round, unchanged), 1.010x (Poppy), instead of the 1.536x / 1.320x the un-squashed
canvases gave. The canvases are correct as drawn in the BOX, not as PNGs standing alone - the
tall one looks pre-stretched on the contact sheet, by exactly the amount its box gives back.

Canvas resolution: `PPU_CANVAS` 2064 px/m, not the petal set's 1032. At 1032 the spike's
0.028 m box is 29 px across and the floret lobe's own edge feather is under one pixel, i.e. the
canvas would alias into exactly the hard corners this pass removes; 2064 gives 58 x 103 px for
the spike and 130 x 176 / 184 x 172 / 227 x 161 px for the three hips, all far below the meta's
512 maxTextureSize.

Run from the project root:   python Tools/gen-flower-spike-textures.py
Writes `Assets/Resources/Art/Flower/spike.png` + `hip.png` + `hip_tall.png` + `hip_wide.png` and
their `.meta`, then prints the verification table (canvas, box, per-kind aspect error, on-screen
size at the two lenses, coverage, opaque share, tones, md5) and a contact sheet into
`Trials/Runs/art-0-spike/`.

Deterministic: every wobble comes from `random.Random(<fixed seed>)`, no timestamp is written,
so a second run reproduces the same bytes and the same guids. The guid pool is checked against
every `.meta` in `Assets/` before a new one is drawn - the 26th pass caught `random.Random(
20260922)` handing out a guid that `hip.png` already carried, which the 23rd pass had met in the
same form from another generator.
"""

import hashlib
import math
import os
import random
import re
import sys

import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FLOWER_DIR = os.path.join(ROOT, "Assets", "Resources", "Art", "Flower")
META_TEMPLATE = os.path.join(ROOT, "Assets", "LegacyArt", "Apple", "fruit.png.meta")
OUT_DIR = os.path.join(ROOT, "Trials", "Runs", "art-0-spike")

# bloomSize per kind, transcribed from PlantFlower.ApplyKind (lines 1329-1425). Lavender is
# the SPIKE (spikeCount 4); the other eleven kinds draw the Bloom part, which is the autumn
# hip. Spring scales the box by 0.88 in both axes -> the aspects below are the whole range.
BLOOM_SIZE = {
    "Rose":      (0.080, 0.070),
    "Tulip":     (0.070, 0.100),
    "Daisy":     (0.100, 0.100),
    "Poppy":     (0.110, 0.080),
    "Sunflower": (0.160, 0.160),
    "Lily":      (0.090, 0.110),
    "Daffodil":  (0.080, 0.070),
    "Violet":    (0.055, 0.050),
    "Crocus":    (0.050, 0.070),
    "Iris":      (0.090, 0.080),
    "Bluebell":  (0.050, 0.070),
}
SPIKE_KIND = "Lavender"
SPIKE_BOX = (0.028, 0.050)

PPU_CANVAS = 2064.0

# The two lenses the picture is judged at: the gallery's flower still (the capture log prints
# ortho=0,88 for the `flower` bound, i.e. 1.76 m of world over 1080 px) and the yard/level
# camera the earlier passes quote (414.21 px/m). The 23rd pass quoted 1920/2.95 = 650.8 px/m
# from the bound's own width; the measured lens is what the eye actually gets, so it is the
# one printed here.
GALLERY_PX_M = 1080.0 / 1.76
BOUND_PX_M = 1920.0 / 2.95
YARD_PX_M = 414.21

# The luminance window every canvas in this family lives in (the petal and leaf sets): the
# renderer tint multiplies these, so 1.00 is "the kind's own colour" and 0.70 the deepest
# shade a part carries. Below ~0.55 every kind would drag toward black.
LUM_HI = 1.00
LUM_LO = 0.70

# The edge feather as a fraction of a form's own half-size: 0.16 of a 21 px floret half-width
# is ~3.4 canvas px = ~1.1 px at the gallery lens - enough to take the cut corners off the
# silhouette, not enough to blur it into a stain. (The leaf set used 0.22; the eye read the
# first spike/hip sheet as "airbrushed", so these two run a tighter rim.)
RIM = 0.16

# Summer / autumn tint of the lavender spike (bloomColor, and the spent lerp the rig applies to
# the cards), used ONLY to draw the contact sheet the way the renderer draws it.
TINT_LAVENDER = (0.52, 0.36, 0.72)
TINT_SPENT = (0.42, 0.32, 0.16)
TINT_HIP = (0.42, 0.28, 0.12)


def smooth(t):
    t = np.clip(t, 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def canvas_size(bx, by):
    w = int(round(bx * PPU_CANVAS))
    h = int(round(by * PPU_CANVAS))
    return max(24, w), max(24, h)


def round_to_int(v):
    """`Mathf.RoundToInt`: .NET's `Math.Round(v, MidpointRounding.ToEven)` - half to EVEN.
    Python's `round()` on a float is the same rule, so the two agree on every value below."""
    return int(round(v))


def live_box(size):
    """The box the dress actually READS, i.e. what `HumanSegment.CreateBodySprite` hands it.

    That function snaps the placeholder to whole pixels at 256 px/m with a 64 px minimum side,
    scales both axes by one uniform factor, and takes ppu from the WIDTH - so the drawn box is
    `(size.x, size.x * h / w)` and the aspect the dress sees is the integer PIXEL ratio, not the
    authored one. A 0.110 x 0.080 Poppy bloom is authored 1.375 but drawn 90 x 64 px = 1.406.
    """
    w0 = max(1, round_to_int(size[0] * 256.0))
    h0 = max(1, round_to_int(size[1] * 256.0))
    scale = 1.0
    if w0 > 512 or h0 > 512:
        scale = 512.0 / max(w0, h0)
    min_side = min(w0, h0) * scale
    if min_side < 64.0:
        scale *= 64.0 / max(1.0, min_side)
    w = round_to_int(w0 * scale)
    h = round_to_int(h0 * scale)
    ppu = w / size[0]
    return (w / ppu, h / ppu)


# The hip exists in AUTUMN only: `PlantFlower` line 222 sets `spentHead = ResolvedSeason ==
# Autumn`, and a spring rig draws the petal fan instead (the gate measured it: `Crocus Spring
# hips=0`). Autumn does not rescale `bloomSize`, so the autumn box IS the summer one and the
# members below are the whole set of boxes a hip canvas is ever drawn in.
HIP_SEASON = ("Autumn", 1.0)


def hip_members():
    """Every (kind, season) box the rig can draw a spent bloom in, as the dress reads it."""
    out = []
    season, scale = HIP_SEASON
    for kind in sorted(BLOOM_SIZE):
        if kind == SPIKE_KIND:
            continue
        bx, by = BLOOM_SIZE[kind]
        out.append((kind, season, live_box((bx * scale, by * scale))))
    return out


# The family split, at the two gaps in the LIVE autumn set (the exact numbers are in the table
# `main` prints; kinds listed by their live box aspect):
#   0.6957 Tulip | 0.7191 Crocus | 0.7191 Bluebell | 0.8205 Lily      <- the TALL family
#   ---- gap x1.219 ----
#   1.0000 Daisy | 1.0000 Sunflower | 1.0781 Violet | 1.1094 Rose/Daffodil | 1.1562 Iris
#                                                          <- the ROUND family
#   ---- gap x1.203 ----
#   1.4062 Poppy                                           <- the WIDE family (Poppy alone)
# The thresholds sit at the geometric middle of each gap, so a box that drifts across one only
# swaps one hip canvas for another.
HIP_TALL_MAX = 0.906      # sqrt(0.8205 * 1.0) = 0.9058
HIP_WIDE_MIN = 1.268      # sqrt(1.1563 * 1.3906) = 1.2680
HIP_CANVASES = ("hip", "hip_tall", "hip_wide")


def hip_family(aspect):
    if aspect < HIP_TALL_MAX:
        return "hip_tall"
    if aspect >= HIP_WIDE_MIN:
        return "hip_wide"
    return "hip"


def family_box(name):
    """Geometric mean of the LIVE boxes of the kinds this canvas is drawn for - the shape it is
    painted in, and the family's own cost (worst member error) is printed by `main`."""
    members = [b for _k, _s, b in hip_members() if hip_family(b[0] / b[1]) == name]
    bx = math.exp(sum(math.log(b[0]) for b in members) / len(members))
    by = math.exp(sum(math.log(b[1]) for b in members) / len(members))
    return bx, by


def family_members(name):
    """(kind, season, box) of the canvas' family, widest first."""
    return sorted([m for m in hip_members() if hip_family(m[2][0] / m[2][1]) == name],
                  key=lambda m: m[2][0] / m[2][1], reverse=True)


def form_bbox(img):
    """The opaque form's size as box fractions (du, dv) - what shape the canvas actually carries."""
    a = np.asarray(img)[:, :, 3]
    ys, xs = np.nonzero(a > 4)
    if len(xs) == 0:
        return 0.0, 0.0
    return ((xs.max() - xs.min() + 1) / float(img.width),
            (ys.max() - ys.min() + 1) / float(img.height))


def form_extents(img, thresh=4):
    """The opaque form's EXTENTS in centred box fractions: (u0, u1, v0, v1).

    `form_bbox` gives the span, which is all a canvas that is drawn as painted needs. The
    pre-squash needs the POSITION as well: it scales the form about its own centre and then has
    to prove the result still fits inside the canvas, and a form that sits off-centre (this
    painter's does - the neck reaches left) can be pushed over an edge by the scale alone.
    u is centred (-0.5 is the left canvas edge, +0.5 the right); v runs bottom to top.
    """
    a = np.asarray(img)[:, :, 3]
    ys, xs = np.nonzero(a > thresh)
    if len(xs) == 0:
        return 0.0, 0.0, 0.0, 0.0
    w, h = float(img.width), float(img.height)
    return ((xs.min() + 0.5) / w - 0.5, (xs.max() + 0.5) / w - 0.5,
            1.0 - (ys.max() + 0.5) / h, 1.0 - (ys.min() + 0.5) / h)


# The margin a form's edge keeps from the canvas border: 0.02 of the box on every side, i.e.
# ~2.6 px on the widest hip canvas and ~2 px at the gallery lens - enough to keep the alpha
# feather off the edge. The budget is stated per axis because the canvas' own coordinates are
# asymmetric: u is centred (-0.5 is the left edge, +0.5 the right) and v runs 0 (bottom) to 1
# (top). 0.480 is deliberately WIDER than the accepted round hip's own half-extents (0.356 u,
# 0.375 v measured on the painting), so the round family is never touched by the fit: its canvas
# is the accepted picture and must not move a pixel.
HIP_FIT_U = 0.480
HIP_FIT_V = 0.480


def hip_form_scale(name):
    """How much WIDER this family's canvas has to paint its form (in box fractions).

    `ArtParts.Dress` maps the canvas onto the part's own box, so the box - not the canvas -
    shapes the drawn form: a form painted at the same fractions of every canvas reads as a
    narrow hip on a Tulip (box aspect 0.696) and a flat pancake on a Poppy (1.406), a factor
    2.02 apart. The fix is to pre-squash the form by the reciprocal of what the family's box
    will do to it, referenced to the round family - the shape the accepted rose hip shows:

        s = aspect(round family's mean box) / aspect(this family's mean box)
    """
    ref = family_box("hip")
    bx, by = family_box(name)
    return (ref[0] / ref[1]) / (bx / by)


def fitted_map(name, extents):
    """The `form_map` for one family canvas, from the RAW painting's extents.

    The pre-squash ratio `sx / sv = hip_form_scale(name)` is kept exactly; the pair is then
    scaled down together (that is what `sv` gives) until the form fits inside the margin, and
    shifted by the least amount that brings it back off an edge. Both are needed because the
    pre-squash is a WIDENING for the tall family: 1.457 x the round form's half-width 0.356 is
    0.519, i.e. the neck would run 0.019 past the canvas edge and the painter's own
    `border_pixels` gate would (correctly) fail the pass. Returns None when the fitted map is
    the identity, so a family that needs nothing is painted byte for byte as before.

    The budget is per axis (`HIP_FIT_U` about u = 0, `HIP_FIT_V` about v = 0.5): a raw painting
    that is off-centre on one axis has that axis' budget only, and the shift is what buys the
    rest.
    """
    u0, u1, v0, v1 = extents
    hu, hv = (u1 - u0) / 2.0, (v1 - v0) / 2.0
    cu, cv = (u0 + u1) / 2.0, (v0 + v1) / 2.0
    s = hip_form_scale(name)
    sv = min(1.0, HIP_FIT_V / hv, HIP_FIT_U / (hu * s))
    sx = s * sv
    ou, ov = cu, cv
    if ou - hu * sx < -HIP_FIT_U:
        ou = -HIP_FIT_U + hu * sx
    if ou + hu * sx > HIP_FIT_U:
        ou = HIP_FIT_U - hu * sx
    if ov - hv * sv < 0.5 - HIP_FIT_V:
        ov = 0.5 - HIP_FIT_V + hv * sv
    if ov + hv * sv > 0.5 + HIP_FIT_V:
        ov = 0.5 + HIP_FIT_V - hv * sv
    if sx == 1.0 and sv == 1.0 and (ou, ov) == (cu, cv):
        return None
    return sx, sv, cu, cv, ou, ov


# ─── the shared painting machinery ───────────────────────────────────────────

class Canvas:
    """One RGBA canvas being painted: alpha is the shape, luminance is the form.

    Row 0 of a PIL array is the TOP of the PNG while v is measured from the BOTTOM, so the v
    axis is flipped on the way in - the same trap `gen-fire-textures.py` and
    `gen-flower-leaf-textures.py` document.
    """

    def __init__(self, w, h, seed):
        self.w, self.h, self.seed = w, h, seed
        self.us = (np.arange(w, dtype=np.float64) + 0.5) / w - 0.5
        self.vs = 1.0 - (np.arange(h, dtype=np.float64) + 0.5) / h
        self.U = self.us[None, :]
        self.V = self.vs[:, None]
        self.alpha = np.zeros((h, w), dtype=np.float64)
        self.lum = np.ones((h, w), dtype=np.float64)
        # The form map: the identity until `form_map` is called, so the spike and the round hip
        # are painted through it unchanged, byte for byte.
        self.fsx, self.fsv = 1.0, 1.0
        self.fcx, self.fcy = 0.0, 0.0
        self.fou, self.fov = 0.0, 0.0

    def form_map(self, sx, sv, cx, cy, ou, ov):
        """Place the painter's own coordinates through an anisotropic map.

        A family whose box will stretch the form in the world needs the form painted
        PRE-SQUASHED by the reciprocal of that stretch (`hip_form_scale`), and the squash is
        applied to the FORM's coordinates rather than to the finished raster: a sample at canvas
        (u, v) is evaluated at the form point

            (u - ou) / sx + cx          (and the same for v)

        so every ellipse, feather and gradient keeps the painter's own normalised geometry (the
        shade functions are written in `ru/hw`, `rv/hh`, which are unchanged) and only the
        placement and the two axes move. A raster resample would shear and blur; this is exact,
        which is why the tall hip and the round one are the same painting.
        """
        self.fsx, self.fsv = float(sx), float(sv)
        self.fcx, self.fcy = float(cx), float(cy)
        self.fou, self.fov = float(ou), float(ov)

    def dist(self, cx, cy, hw, hh, ang_deg):
        """Normalised distance from a form's centre: 0 at the centre, 1 on its edge.

        The form is an ellipse rotated by ang_deg about (cx, cy); hw/hh are in box fractions
        (the box is 1.0 wide and 1.0 tall), i.e. every painter is written in the BOX' own
        fractions - `ArtParts.Dress` fills the box with the canvas, so the drawn form is
        (fraction x box) and the canvas' own aspect only sets the sampling grid. WHICH fraction
        the form occupies is therefore the painter's whole lever, and `form_map` is how a family
        asks for it pre-squashed (the table in `main` prints the result per kind).
        """
        a = math.radians(ang_deg)
        du = (self.U - self.fou) / self.fsx + self.fcx - cx
        dv = (self.V - self.fov) / self.fsv + self.fcy - cy
        ru = du * math.cos(a) + dv * math.sin(a)
        rv = -du * math.sin(a) + dv * math.cos(a)
        return np.sqrt((np.abs(ru) / hw) ** 2 + (np.abs(rv) / hh) ** 2), ru, rv

    def lay(self, mask, shade):
        self.alpha = np.maximum(self.alpha, mask)
        self.lum = np.where(mask > 0.001, shade, self.lum)

    def oval(self, cx, cy, hw, hh, ang_deg, shade_fn, feather=RIM):
        d, ru, rv = self.dist(cx, cy, hw, hh, ang_deg)
        mask = smooth(np.clip((1.0 - d) / feather, 0.0, 1.0))
        self.lay(mask, shade_fn(d, ru, rv, hw, hh))

    def shade_band(self, cy, hw, hh, depth, ang_deg=0.0):
        """A soft shadow the form above throws on the one below: luminance only, no alpha."""
        v = (self.V - self.fov) / self.fsv + self.fcy
        d, _, _ = self.dist(0.0, cy, hw, hh, ang_deg)
        band = np.exp(-((v - cy) / (hh * 0.55)) ** 2) * smooth(np.clip((1.0 - d) / 0.35, 0.0, 1.0))
        self.lum = self.lum * (1.0 - depth * band)


def _finish(canvas, seed):
    """Alpha + luminance -> RGBA. RGB is grey: the renderer's tint carries the colour."""
    rng = random.Random(seed)
    sound = np.array([[rng.uniform(-0.014, 0.014) for _ in range(canvas.w)]
                      for _ in range(canvas.h)])
    lum = np.clip(canvas.lum + sound, 0.0, 1.0)
    rgb = np.round(lum[:, :, None] * 255.0).astype(np.uint8)
    rgba = np.concatenate([np.repeat(rgb, 3, axis=2),
                           np.round(np.clip(canvas.alpha, 0.0, 1.0) * 255.0).astype(np.uint8)[:, :, None]],
                          axis=2)
    return Image.fromarray(rgba, "RGBA")


# ─── the lavender spike ──────────────────────────────────────────────────────

def paint_spike(w, h, seed, form=None):
    """The lavender raceme: a rachis showing in the gaps, four whorls of overlapping florets
    closing to a bud.

    `form` is unused: the spike has ONE box (0.028 x 0.050 m, aspect 0.560) for all four whorls,
    so its canvas is drawn as painted. It is accepted so every painter in `jobs` has one
    signature.

    The cards this replaces were three packets at -0.32 / -0.18 / +0.20 of the box width, all
    the same tone: the first sheet the eye read came back as "inflated blobs in two parallel
    columns, a mechanical grid, no cane". So the rachis is drawn FIRST (the whorls cover it
    except in the gaps between them, which is what makes the florets hang off something), the
    whorls overlap vertically, each whorl's size and lean wobble on its own, and every lobe
    carries the floret's own opened lip at its outer end.
    """
    rng = random.Random(seed)
    c = Canvas(w, h, seed)

    def floret(hue_shift):
        def shade(d, ru, rv, hw, hh):
            # The form: brightest on the upper-outer flank, a groove where the floret opens,
            # a darker base where it meets the rachis.
            up = smooth(rv / hh * 0.5 + 0.5)
            tone = LUM_LO + (LUM_HI - LUM_LO) * (0.26 + 0.74 * up)
            groove = np.exp(-(ru / (hw * 0.30)) ** 2)
            tone = tone * (1.0 - 0.090 * groove)
            rim = smooth(np.clip((d - 0.62) / 0.38, 0.0, 1.0))
            tone = tone * (1.0 + 0.055 * rim)
            return np.clip(tone + hue_shift, 0.0, 1.0)
        return shade

    def rachis_shade(d, ru, rv, hw, hh):
        _ = (d, ru, hh)
        # In shade, and darker where it is furthest from the light: the cane reads as the
        # thing the florets hang on, not as a second stem.
        return np.clip(0.775 + 0.055 * smooth(rv / max(1e-6, 0.40) + 0.5), 0.0, 1.0)

    # The rachis first: a slim tapered run through the box (the rig's own cane passes at
    # -0.09 .. +0.33 of the box width, so this sits inside its footprint).
    c.oval(0.075, 0.500, 0.028, 0.455, 2.0, rachis_shade)

    # level, near lobe (x, v), near half-size, far lobe, far half-size, near tilt, far tilt.
    whorls = [
        (0.20, (-0.185, 0.205), (0.170, 0.104), (0.145, 0.195), (0.132, 0.084), 12.0, -10.0),
        (0.43, (0.155, 0.435), (0.152, 0.096), (-0.205, 0.425), (0.138, 0.081), -11.0, 9.0),
        (0.65, (-0.165, 0.650), (0.134, 0.087), (0.140, 0.640), (0.118, 0.073), 10.0, -8.0),
        (0.82, (-0.105, 0.820), (0.099, 0.064), (0.100, 0.810), (0.088, 0.057), 9.0, -7.0),
    ]
    for i, (level, near, nsz, far, fsz, nt, ft) in enumerate(whorls):
        # A shadow the whorl above throws on this one: the cards had none, which is part of
        # why the stack read as one flat bar.
        if i > 0:
            c.shade_band(level - 0.070, 0.40, 0.105, 0.095)
        wob = rng.uniform(-5.0, 5.0)
        c.oval(far[0], far[1], fsz[0], fsz[1], ft + wob, floret(-0.040))
        c.oval(near[0], near[1], nsz[0], nsz[1], nt + wob, floret(0.0))
        # The opened lip at the outer end: the rig's own `FloretTip` packet, drawn as the
        # floret's two-lobed mouth instead of one more rectangle.
        out = -1.0 if near[0] < 0.0 else 1.0
        c.oval(near[0] + out * nsz[0] * 0.62, near[1] + nsz[1] * 0.46, nsz[0] * 0.40, nsz[1] * 0.30,
               out * 24.0 + wob, floret(0.030))
        c.oval(near[0] + out * nsz[0] * 0.78, near[1] + nsz[1] * 0.10, nsz[0] * 0.26, nsz[1] * 0.26,
               out * 12.0 + wob, floret(0.010))

    # The tip: a closed bud narrowing to a point inside the box, so the top is not a cut.
    c.shade_band(0.760, 0.26, 0.075, 0.085)
    c.oval(0.010, 0.878, 0.082, 0.058, 4.0, floret(0.020))
    c.oval(0.010, 0.940, 0.034, 0.038, 2.0, floret(0.040))
    # The base: the lowest whorl's stalk end, closing in a point above the box bottom.
    c.oval(-0.030, 0.100, 0.086, 0.048, 8.0, floret(-0.055))
    return _finish(c, seed + 1)


# ─── the autumn hip ──────────────────────────────────────────────────────────

def paint_hip(w, h, seed, form=None):
    """The autumn seed head: the hip body with its base point, the shed cap above it, the dry
    crown at the top and the dimples of the seeds.

    Everything is in the BOX' fractions, so the drawn form is (fraction x box): the box the
    canvas is drawn into is what shapes it, and the canvas' own aspect only sets the sampling
    grid (measured - `Tools/art0-hip-lens-ab.py`, and the table `main` prints). `form` is the
    fitted pre-squash `form_map` for the family this canvas serves (None = the form as painted,
    which is what the round family ships and what the raw measuring pass uses).

    The rig's twelve cards describe exactly this: two overlapping masses (`Spent` near,
    `SpentR` far) with their tips at the bottom (`SpentTip`, `SpentRTip`) and a cap row above
    (`SpentCap*`, `SpentCapFill*`). The first sheet the eye read came back as "an amorphous
    lump, no botanical feature that says seed head" - so this version carries the three things
    a spent hip has and the cards did not: a NECK at the top left where it hangs off the stem,
    a CROWN of dried sepal points on the cap, and a shaded seam between the cap and the body.
    The `Sepal` (calyx) cards and the spent `Disk` stay: the canvas replaces the flat boxes only.

    Every form is placed so that nothing reaches the canvas border - the first version's body
    ran past v = 0 and the canvas cut it with a straight horizontal edge (a hard line at the
    bottom of the sheet). `border_pixels()` in `main` asserts that against the pixels.
    """
    rng = random.Random(seed)
    c = Canvas(w, h, seed)
    if form is not None:
        c.form_map(*form)

    def lit(tint, groove=None):
        """The hip's own light: the whole mass is lit from the upper left, the underside in
        shade, a rim along the silhouette and, where asked, a darker seam."""
        def shade(d, ru, rv, hw, hh):
            up = smooth(rv / hh * 0.5 + 0.5)
            left = smooth(-ru / hw * 0.5 + 0.5)
            tone = LUM_LO + (LUM_HI - LUM_LO) * (0.18 + 0.58 * up + 0.24 * left)
            rim = smooth(np.clip((d - 0.66) / 0.34, 0.0, 1.0))
            tone = tone * (1.0 + 0.050 * rim)
            if groove == "cap":
                tone = tone * (1.0 - 0.075 * np.exp(-((rv + hh * 0.20) / (hh * 0.42)) ** 2))
            if groove == "stem":
                tone = tone * (1.0 - 0.060 * np.exp(-((ru + hw * 0.30) / (hw * 0.35)) ** 2))
            return np.clip(tone + tint, 0.0, 1.0)
        return shade

    # The hip body: a rounded mass, slightly tilted, closing in a point at its base. The lowest
    # form's edge sits at v 0.075, i.e. inside the box: a hip drawn at the part's own box loses
    # the two `SpentTip` cards that hung below it, and that is the box's mapping, not a crop.
    c.oval(-0.050, 0.135, 0.115, 0.055, 10.0, lit(-0.045))       # the base point
    c.oval(0.160, 0.335, 0.170, 0.155, -6.0, lit(-0.020))        # far half
    c.oval(-0.020, 0.375, 0.305, 0.195, 5.0, lit(0.0))           # body
    c.oval(-0.100, 0.535, 0.205, 0.090, -6.0, lit(0.010))        # shoulder
    # The shed cap: a plate above the body with a shaded seam between the two, its far half
    # smaller and darker the way `SpentCapR` was.
    c.shade_band(0.560, 0.30, 0.080, 0.070)
    c.oval(0.070, 0.625, 0.100, 0.065, -5.0, lit(-0.050))
    c.oval(-0.120, 0.675, 0.190, 0.100, -8.0, lit(0.0, "cap"))
    # The crown: the dry sepal points a spent hip carries at its blossom end. The rig's own
    # `Sepal` cards hang at the BOTTOM of the bloom, so this doubles nothing.
    for k, (cx, tilt) in enumerate(((-0.245, -24.0), (-0.105, -7.0), (0.030, 11.0))):
        c.oval(cx + rng.uniform(-0.010, 0.010), 0.785 - 0.012 * abs(k - 1),
               0.024, 0.050 + 0.008 * abs(k - 1), tilt + rng.uniform(-4.0, 4.0),
               lit(0.030 if k == 1 else 0.010))
    # The neck: the stalk scar where the hip hangs off the stem head, top left, with a groove
    # that reads as the stalk's own shadow.
    c.oval(-0.330, 0.700, 0.062, 0.048, -36.0, lit(-0.060, "stem"))
    # Seed dimples along the cap: small darker patches - three is what reads at 50 px.
    for k in range(3):
        cx = -0.215 + 0.165 * k + rng.uniform(-0.012, 0.012)
        cy = 0.665 - 0.030 * k + rng.uniform(-0.010, 0.010)
        c.oval(cx, cy, 0.042, 0.026, rng.uniform(-18.0, 18.0), lit(-0.055))
    # A last dimple on the body's lit side, so the mass is not one smooth balloon.
    c.oval(-0.150, 0.360, 0.052, 0.038, -14.0, lit(-0.035))
    return _finish(c, seed + 1)


def border_pixels(img):
    """Opaque pixels touching the canvas edge: a form the canvas CUT, i.e. the hard straight
    line the eye found at the bottom of the first hip sheet. Must be 0 for both canvases."""
    a = np.asarray(img)[:, :, 3]
    return int((a[0] > 8).sum() + (a[-1] > 8).sum() + (a[:, 0] > 8).sum() + (a[:, -1] > 8).sum())


# ─── import settings (the same shape as Tools/gen-flower-leaf-textures.py) ────

def new_guid(rng):
    return "".join(rng.choice("0123456789abcdef") for _ in range(32))


def existing_guid(png_path):
    meta = png_path + ".meta"
    if not os.path.exists(meta):
        return None
    with open(meta, "r", encoding="utf-8") as fh:
        m = re.search(r"^guid: ([0-9a-f]{32})$", fh.read(), re.MULTILINE)
    return m.group(1) if m else None


def project_guids():
    """Every guid in the project. A fresh draw must avoid all of them: the 24th pass hit the
    collision class once (`art0-meta-audit.py` caught two files sharing a guid) and this pass hit
    it again - `random.Random(20260922)`'s FIRST draw IS the guid `hip.png` already carries, so a
    new canvas next to it came out with the same guid. The audit is the detector; this is the
    guard, so the generator cannot write a duplicate at all."""
    guids = set()
    for root, _dirs, files in os.walk(os.path.join(ROOT, "Assets")):
        for fn in files:
            if not fn.endswith(".meta"):
                continue
            with open(os.path.join(root, fn), encoding="utf-8", errors="replace") as fh:
                m = re.search(r"^guid: ([0-9a-f]{32})$", fh.read(), re.MULTILINE)
            if m:
                guids.add(m.group(1))
    return guids


def unique_guid(rng, taken):
    for _ in range(4096):
        g = new_guid(rng)
        if g not in taken:
            taken.add(g)
            return g
    raise RuntimeError("no unused guid left in the pool")


def write_meta(png_path, pixels_per_unit, guid):
    with open(META_TEMPLATE, "r", encoding="utf-8") as fh:
        text = fh.read()

    def set_line(key, value, indent=2):
        nonlocal text
        pattern = re.compile(r"^%s%s:.*$" % (" " * indent, re.escape(key)), re.MULTILINE)
        assert pattern.search(text), "template has no %s" % key
        text = pattern.sub("%s%s: %s" % (" " * indent, key, value), text, count=1)

    text = re.sub(r"^guid: .*$", "guid: " + guid, text, count=1, flags=re.MULTILINE)
    set_line("spriteMeshType", 0)                        # FullRect: Sliced needs it
    set_line("spritePixelsToUnits", pixels_per_unit)
    set_line("isReadable", 1)                            # Albedo -> Tiled -> Sprite.Create
    set_line("wrapU", 1, indent=4)
    set_line("wrapV", 1, indent=4)
    set_line("alphaIsTransparency", 1)
    set_line("spriteGenerateFallbackPhysicsShape", 0)     # art never carries a collider
    set_line("textureType", 8)                            # Sprite
    set_line("spriteMode", 1)                             # Single
    set_line("spriteID", guid[:16] + "0800000000000000", indent=4)
    text = re.sub(r"^(\s*)maxTextureSize: .*$", r"\g<1>maxTextureSize: 512", text, flags=re.MULTILINE)
    text = re.sub(r"^\s*nameFileIdTable: .*$", "    nameFileIdTable: {}", text, count=1, flags=re.MULTILINE)
    with open(png_path + ".meta", "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def tones(img):
    a = np.asarray(img)
    solid = a[:, :, 3] >= 128
    if not solid.any():
        return 0
    return int(len(np.unique(a[:, :, 0][solid])))


def contact_sheet(tiles):
    """What the eye gets, per canvas: the canvas at 8x, then the same canvas at the gallery
    lens and at the yard lens, magnified back up - TINTED with the colour the renderer would
    multiply it by, because that is how it is drawn (a grey canvas on a dark sheet reads as a
    stain; the same canvas in lavender on soil reads as a spike).
    """
    pad, label_h = 12, 16
    drawn = []
    for label, img, box, tint in tiles:
        for lens, px_m in (("canvas 8x", None), ("gallery lens", GALLERY_PX_M), ("yard lens", YARD_PX_M)):
            if px_m is None:
                small, zoom = img, 8
            else:
                small = img.resize((max(1, int(round(box[0] * px_m))), max(1, int(round(box[1] * px_m)))),
                                   Image.LANCZOS)
                zoom = max(6, min(14, int(round(140.0 / max(1, small.height)))))
            big = small.resize((small.width * zoom, small.height * zoom), Image.NEAREST)
            drawn.append((label + " " + lens, big, tint))
    width = sum(t[1].width + pad for t in drawn) + pad
    big_h = max(t[1].height for t in drawn) + 2 * pad
    sheet = Image.new("RGBA", (width, 2 * big_h), (86, 82, 76, 255))
    draw = ImageDraw.Draw(sheet)
    x = pad
    for label, big, tint in drawn:
        lit = Image.new("RGBA", big.size, (int(tint[0] * 255), int(tint[1] * 255), int(tint[2] * 255), 255))
        arr = np.asarray(big).astype(np.float64)
        lit_arr = np.asarray(lit).astype(np.float64)
        out = arr.copy()
        for ch in range(3):
            out[:, :, ch] = arr[:, :, ch] / 255.0 * lit_arr[:, :, ch]
        sheet.alpha_composite(Image.fromarray(out.astype(np.uint8), "RGBA"), (x, pad))
        draw.text((x, big_h - label_h), label, fill=(230, 230, 230, 255))
        x += big.width + pad
    return sheet


def main():
    os.makedirs(FLOWER_DIR, exist_ok=True)
    os.makedirs(OUT_DIR, exist_ok=True)
    # NOT 20260920 / 20260921: those are the petal and leaf sets' seeds, and the same RNG draws
    # the same first guids (the collision `art0-meta-audit.py` caught on the 23rd pass).
    guid_rng = random.Random(20260922)
    taken_guids = project_guids()

    jobs = [("spike", SPIKE_BOX, paint_spike, 931, None)]
    # Two passes per hip canvas: paint the form as the painter writes it, measure what it
    # actually covers, then paint it again through the fitted pre-squash. The measurement has to
    # come from the pixels - the painter's neck reaches left of the mass, so its extents are not
    # the sum of its own literals, and a guess here is a form cut by the canvas edge.
    raw_extents = {}
    for i, name in enumerate(HIP_CANVASES):
        box = family_box(name)
        seed = 942 + i
        w, h = canvas_size(box[0], box[1])
        raw_extents[name] = form_extents(paint_hip(w, h, seed, None))
        form = fitted_map(name, raw_extents[name])
        jobs.append((name, box, paint_hip, seed, form))

    rows, paintings = [], {}
    maps = {}
    for name, box, painter, seed, form in jobs:
        w, h = canvas_size(box[0], box[1])
        img = painter(w, h, seed, form)
        maps[name] = form
        img.info["box"] = box
        paintings[name] = img
        path = os.path.join(FLOWER_DIR, name + ".png")
        img.save(path, "PNG", optimize=True)
        ppu = int(round(w / box[0]))
        write_meta(path, ppu, existing_guid(path) or unique_guid(guid_rng, taken_guids))
        a = np.asarray(img)[:, :, 3].astype(np.float64) / 255.0
        with open(path, "rb") as fh:
            md5 = hashlib.md5(fh.read()).hexdigest()
        rows.append((name, "Flower/" + name, box, w, h, ppu, os.path.getsize(path),
                     100.0 * a.mean(), 100.0 * (a >= 0.999).mean(), tones(img), md5))

    print("canvas set written to Assets/Resources/Art/Flower/")
    print("  %-6s %-14s %-16s %-10s %-8s %-8s %-9s %-9s %-8s %s"
          % ("canvas", "key", "box m", "canvas", "ppu", "coverage", "opaque", "tones", "bytes", "md5"))
    for name, key, box, w, h, ppu, size, cov, opaque, tn, md5 in rows:
        print("  %-6s %-14s %-16s %-10s %-8d %-8.1f %-9.1f %-9d %-8d %s"
              % (name, key, "%.4f x %.4f" % box, "%dx%d" % (w, h), ppu, cov, opaque, tn, size, md5))
    print("  resolution: %.0f px/m (canvas), gallery lens %.1f px/m (the flower still's measured"
          % (PPU_CANVAS, GALLERY_PX_M))
    print("              ortho 0,88), bound width %.1f px/m, yard lens %.1f px/m"
          % (BOUND_PX_M, YARD_PX_M))
    print("  .meta: textureType 8 (Sprite), spriteMode 1 (Single), spriteMeshType 0 (FullRect),")
    print("         wrapU/V 1, fallback physics shape 0, alphaIsTransparency 1, isReadable 1")

    print("  the pre-squash each canvas was painted through (what the family's box gives back):")
    for name in HIP_CANVASES:
        u0, u1, v0, v1 = raw_extents[name]
        form = maps[name]
        print("    Flower/%-9s raw form u %+.3f..%+.3f  v %.3f..%.3f of the box"
              % (name, u0, u1, v0, v1))
        if form is None:
            print("    %-16s identity - no squash and no shift: the accepted round canvas"
                  % "")
        else:
            sx, sv, cu, cv, ou, ov = form
            print("    %-16s pre-squash x%.3f  painted x%.3f / y%.3f  placed at u %+.3f"
                  " v %+.3f (form centre was u %+.3f v %+.3f)"
                  % ("", hip_form_scale(name), sx, sv, ou, ov, cu, cv))
    print("    the pre-squash is aspect(round family box) / aspect(this family box); the fit")
    print("    shrinks both axes together (sv) until a half-extent reaches HIP_FIT_U %.3f /"
          % HIP_FIT_U)
    print("    HIP_FIT_V %.3f and then shifts the least amount that puts the form back off the"
          % HIP_FIT_V)
    print("    canvas edge. A family that needs neither ships its canvas byte for byte.")

    cut = {name: border_pixels(img) for name, img in paintings.items()}
    print("  no form is cut by the canvas edge: "
          + ", ".join("%s border=%d px" % (k, v) for k, v in sorted(cut.items()))
          + " -> " + ("OK" if max(cut.values()) == 0 else "FAIL (a straight cut edge)"))
    if max(cut.values()) != 0:
        return 1

    print()
    print("  the FORM the eye gets, per kind. `Dress` fills the part's box with the canvas in")
    print("  `Sliced` mode at `size = box`, so canvas u lands on box.x and v on box.y: the drawn")
    print("  form is (canvas form x box) and the box is what shapes it. The canvas' own aspect is")
    print("  NOT a shape lever - it only sets the sampling grid (measured: drawn into the Poppy's")
    print("  box, the 165x172 single canvas and the 227x161 wide one both give a 48x37 px form,")
    print("  `Tools/art0-hip-lens-ab.py`). What IS a shape lever is the fraction of the box the")
    print("  form is painted at, which is what the fitted pre-squash above moves - so the numbers")
    print("  below are the SHAPE the eye gets, and `worst` is each family's own box spread.")
    ref_du, ref_dv = form_bbox(paintings["hip"])
    ref_box = family_box("hip")
    ref_world = (ref_du * ref_box[0]) / (ref_dv * ref_box[1])
    for name, key, box, w, h, ppu, size, cov, opaque, tn, md5 in rows:
        du, dv = form_bbox(paintings[name])
        kinds = ([(SPIKE_KIND, SPIKE_BOX)] if name == "spike"
                 else [(kind + " " + season, kb) for kind, season, kb in family_members(name)])
        worst = 0.0
        print("    %s  canvas aspect %.3f  drawn form %.3f x %.3f of the box"
              % (key, box[0] / box[1], du, dv))
        for kind, kb in sorted(kinds, key=lambda kv: kv[1][0] / kv[1][1], reverse=True):
            world = (du * kb[0]) / (dv * kb[1])
            worst = max(worst, max(world / ref_world, ref_world / world))
            print("      %-16s box %.4f x %.4f m  form aspect %.3f  vs the round hip %.3fx"
                  "  on screen %.0fx%.0f px (gallery) %.0fx%.0f px (yard)"
                  % (kind, kb[0], kb[1], world, max(world / ref_world, ref_world / world),
                     kb[0] * GALLERY_PX_M, kb[1] * GALLERY_PX_M, kb[0] * YARD_PX_M, kb[1] * YARD_PX_M))
        print("      worst form aspect against the round hip: %.3fx" % worst)

    tiles = [
        ("spike summer", paintings["spike"], SPIKE_BOX, TINT_LAVENDER),
        ("spike autumn", paintings["spike"], SPIKE_BOX, TINT_SPENT),
    ]
    for name in HIP_CANVASES:
        box = next(b for n, k, b, w, h, p, s, c, o, t, m in rows if n == name)
        suffix = name[len("hip"):].strip("_")
        tiles.append(("hip autumn " + (suffix if suffix else "round")
                      + "  form %.2f" % hip_form_scale(name),
                      paintings[name], box, TINT_HIP))
    sheet = contact_sheet(tiles)
    out = os.path.join(OUT_DIR, "canvases.png")
    sheet.save(out)
    print()
    print("  contact sheet: %s (%dx%d: each canvas at 8x, then at the gallery lens and at the"
          % (os.path.relpath(out, ROOT).replace("\\", "/"), sheet.width, sheet.height))
    print("                 yard lens, magnified back up, tinted the way the renderer draws it)")
    print("  full-resolution single-canvas sheets:")
    for name, img in paintings.items():
        single = os.path.join(OUT_DIR, name + ".png")
        img.resize((img.width * 5, img.height * 5), Image.NEAREST).save(single)
        print("    %s (%dx%d)" % (os.path.relpath(single, ROOT).replace("\\", "/"),
                                  img.width, img.height))
    return 0


if __name__ == "__main__":
    sys.exit(main())
