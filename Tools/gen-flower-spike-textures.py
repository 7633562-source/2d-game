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

The two boxes (measured, not guessed)
-------------------------------------
`PlantFlower.ApplyKind`: the spike is `FlowerKind.Lavender` only (`spikeCount = 4`), at
`bloomSize` 0.028 x 0.050 m -> aspect 0.560, and every one of the four spikes carries that
same box, so the canvas is drawn in it EXACTLY (aspect error 1.000x, unlike the leaf set's
1.364x compromise). The hip is drawn by the `Bloom` part of the other ELEVEN kinds, whose
`bloomSize` ranges from Crocus/Bluebell 0.050 x 0.070 (aspect 0.714) to Poppy 0.110 x 0.080
(1.375), so the canvas is drawn in the geometric-mean box of that family and each kind's own
error is printed below (spring scales bloomSize by 0.88 in both axes, which leaves every
aspect unchanged).

Canvas resolution: `PPU_CANVAS` 2064 px/m, not the petal set's 1032. At 1032 the spike's
0.028 m box is 29 px across and the floret lobe's own edge feather is under one pixel, i.e. the
canvas would alias into exactly the hard corners this pass removes; 2064 gives 58 x 103 px for
the spike and 165 x 172 px for the hip, both far below the meta's 512 maxTextureSize.

Run from the project root:   python Tools/gen-flower-spike-textures.py
Writes `Assets/Resources/Art/Flower/spike.png` + `hip.png` and their `.meta`, then prints the
verification table (canvas, box, per-kind aspect error, on-screen size at the two lenses,
coverage, opaque share, tones, md5) and a contact sheet into `Trials/Runs/art-0-spike/`.

Deterministic: every wobble comes from `random.Random(<fixed seed>)`, no timestamp is written,
so a second run reproduces the same bytes and the same guids.
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


def family_box():
    """Geometric mean bloomSize of the eleven hip kinds: the shape the hip canvas is drawn in."""
    keys = [k for k in BLOOM_SIZE if k != SPIKE_KIND]
    bx = math.exp(sum(math.log(BLOOM_SIZE[k][0]) for k in keys) / len(keys))
    by = math.exp(sum(math.log(BLOOM_SIZE[k][1]) for k in keys) / len(keys))
    return bx, by


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

    def dist(self, cx, cy, hw, hh, ang_deg):
        """Normalised distance from a form's centre: 0 at the centre, 1 on its edge.

        The form is an ellipse rotated by ang_deg about (cx, cy); hw/hh are in box fractions
        (the box is 1.0 wide and 1.0 tall), which is what makes a canvas pixel square in world
        metres inside its own box.
        """
        a = math.radians(ang_deg)
        du = self.U - cx
        dv = self.V - cy
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
        d, _, _ = self.dist(0.0, cy, hw, hh, ang_deg)
        band = np.exp(-((self.V - cy) / (hh * 0.55)) ** 2) * smooth(np.clip((1.0 - d) / 0.35, 0.0, 1.0))
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

def paint_spike(w, h, seed):
    """The lavender raceme: a rachis showing in the gaps, four whorls of overlapping florets
    closing to a bud.

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

def paint_hip(w, h, seed):
    """The autumn seed head: the hip body with its base point, the shed cap above it, the dry
    crown at the top and the dimples of the seeds.

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

    hx, hy = family_box()
    jobs = [
        ("spike", SPIKE_BOX, paint_spike, 931),
        ("hip", (hx, hy), paint_hip, 942),
    ]

    rows, paintings = [], {}
    for name, box, painter, seed in jobs:
        w, h = canvas_size(box[0], box[1])
        img = painter(w, h, seed)
        img.info["box"] = box
        paintings[name] = img
        path = os.path.join(FLOWER_DIR, name + ".png")
        img.save(path, "PNG", optimize=True)
        ppu = int(round(w / box[0]))
        write_meta(path, ppu, existing_guid(path) or new_guid(guid_rng))
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

    cut = {name: border_pixels(img) for name, img in paintings.items()}
    print("  no form is cut by the canvas edge: "
          + ", ".join("%s border=%d px" % (k, v) for k, v in sorted(cut.items()))
          + " -> " + ("OK" if max(cut.values()) == 0 else "FAIL (a straight cut edge)"))
    if max(cut.values()) != 0:
        return 1

    print()
    print("  per-kind aspect error against the canvas its own box asks for "
          "(1.000 = drawn in the box):")
    for name, key, box, w, h, ppu, size, cov, opaque, tn, md5 in rows:
        drawn = box[0] / box[1]
        print("    %s canvas aspect %.3f (%dx%d px)" % (key, drawn, w, h))
        if name == "spike":
            kinds = [(SPIKE_KIND, SPIKE_BOX)]
        else:
            kinds = [(k, BLOOM_SIZE[k]) for k in BLOOM_SIZE if k != SPIKE_KIND]
        worst = 0.0
        for kind, kb in sorted(kinds, key=lambda kv: kv[1][0] / kv[1][1], reverse=True):
            a = kb[0] / kb[1]
            worst = max(worst, max(a / drawn, drawn / a))
            print("      %-10s box %.3f x %.3f m  aspect %.3f  error %.3fx  on screen %.0fx%.0f px"
                  " (gallery) %.0fx%.0f px (yard)"
                  % (kind, kb[0], kb[1], a, max(a / drawn, drawn / a),
                     kb[0] * GALLERY_PX_M, kb[1] * GALLERY_PX_M, kb[0] * YARD_PX_M, kb[1] * YARD_PX_M))
        print("      worst aspect error: %.3fx" % worst)

    tiles = [
        ("spike summer", paintings["spike"], SPIKE_BOX, TINT_LAVENDER),
        ("spike autumn", paintings["spike"], SPIKE_BOX, TINT_SPENT),
        ("hip autumn", paintings["hip"], (hx, hy), TINT_HIP),
    ]
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
