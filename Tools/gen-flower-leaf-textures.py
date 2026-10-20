#!/usr/bin/env python3
"""ART-0 gap 3b: the flower's FOLIAGE - one painted leaf-pair canvas per leaflet family.

Why this file exists
--------------------
`Assets/PlantFlower.cs` paints a garden leaf as a COLLAGE of flat boxes: `PaintLeafMidrib`
builds Blade / BladeTip / Midrib / MidribTip / Petiole / PetioleTip / Tip / TipTip / TipR /
TipRTip / BladeR / BladeRTip / MidribR / MidribRTip and then two rotated lobes `Leaflet0/1`
(+ `LeafletTip0/1`) on top - 16 cards, one flat tint each, hard 90-degree corners. The bloom
next to it was painted in the 20th pass (`Tools/gen-flower-textures.py`) and now carries a
shading ramp, so the two halves of the same plant no longer look like the same drawing.

The eye's read of the shipping frame (`Trials/Runs/art0period22_flower.png`, 6x crop
`Trials/Runs/art-0-leaflet/before_daisy_6x.png`): the petals have "a soft gradient
shading", the leaves are "a stack of rectangles ... hard 90-degree corners ... flat single
colour". That is the defect this canvas set answers.

The answer: one canvas per leaflet family, drawn in the leaf part's OWN box aspect, so
`Sliced` maps the whole picture onto the box the part already has (`painted-parts.mdc`:
one bone, one PNG; the box is the mapping, never localScale). The rig's own cards step
aside; the aimed `Petiole`/`PetioleTip` cards stay, because their direction is computed at
build time from the stem join and a static canvas cannot aim.

What the loader needs (quoted, not guessed)
-------------------------------------------
`Assets/ArtLibrary.cs`

    Albedo(key):  Texture2D tex = Resources.Load<Texture2D>("Art/" + key);      // line 139
    Tiled(key, metersPerTile):
                  float ppu = tex.width / metersPerTile;                         // line 165
                  Sprite.Create(tex, full rect, pivot .5/.5, ppu, 0, FullRect)

`Assets/ArtParts.cs`

    Dress(sr, key, tileMeters, real, keepColor):
                  Sprite sprite = ArtLibrary.Tiled(key, tile);    // tile = max(box.x, box.y)
                  sr.sprite = sprite; sr.drawMode = Sliced;       // tileMeters == 0
                  sr.size = box;                                  // size == the box it had

So a key is a READABLE Texture2D under `Assets/Resources/Art/`, the key is a path fragment
("Flower/leaf" -> `Art/Flower/leaf.png`), and the canvases are LUMINANCE canvases (grey RGB,
the shape in alpha) because every flower part keeps its own kind tint: the renderer's colour
MULTIPLIES the canvas, so one canvas serves all twelve kinds and all four seasons.

The two families (measured, not guessed)
----------------------------------------
`ApplyKind` gives every kind its own `leafSize` (the leaf part's box). The FOLIAGE rows are
drawn by the same code for all twelve kinds, so the box aspect is the only thing that
differs - and it splits cleanly into two groups with a factor-3 gap between them:

    broad leaflet   1.667 .. 1.944   rose, daisy, poppy, sunflower, violet, lavender
    erect blade     0.333 .. 0.571   tulip, lily, daffodil, crocus, iris, bluebell

`ArtParts.LeafFamilyWide = 0.8` is that split: a leaf part whose box aspect is 0.8 or wider
gets `Flower/leaf`, anything narrower gets `Flower/leaf_erect`. Each canvas is drawn in the
GEOMETRIC MEAN box of its own family, so no kind is more than 1.40x off its canvas' aspect -
printed per kind below.

Run from the project root:   python Tools/gen-flower-leaf-textures.py
Writes `Assets/Resources/Art/Flower/leaf.png` + `leaf_erect.png` and their `.meta`, then
prints the verification table (canvas, family box, per-kind aspect error, the on-screen size
at the two lenses the game uses, coverage, opaque share, tones) and a contact sheet.

Deterministic: every wobble comes from `random.Random(<fixed seed>)`; no timestamp is
written, so a second run reproduces the same bytes and the same guids.
"""

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
OUT_DIR = os.path.join(ROOT, "Trials", "Runs", "art-0-leaflet")

# leafSize per kind, transcribed from PlantFlower.ApplyKind (lines 1315-1426), summer.
# Spring scales it by 0.90 in both axes (aspect unchanged), autumn only re-tints, winter
# skips the leaves, so this table is the whole aspect range the canvases meet.
LEAF_SIZE = {
    "Rose":      (0.060, 0.035),
    "Tulip":     (0.050, 0.100),
    "Daisy":     (0.060, 0.035),
    "Poppy":     (0.070, 0.040),
    "Sunflower": (0.090, 0.050),
    "Lily":      (0.050, 0.090),
    "Daffodil":  (0.040, 0.090),
    "Violet":    (0.050, 0.030),
    "Crocus":    (0.030, 0.080),
    "Lavender":  (0.035, 0.018),
    "Iris":      (0.040, 0.120),
    "Bluebell":  (0.040, 0.070),
}

# Must match ArtParts.LeafFamilyWide.
WIDE = 0.8

# 1032 px/m: the same canvas resolution convention the petal set uses (a 0.0384 x 0.0615 m
# petal box -> the shipped 40 x 64 canvas), i.e. ~1.6 canvas px per gallery screen px.
PPU_CANVAS = 1032.0

# The two lenses the picture is judged at: the gallery close-up row (RowBounds "flower",
# 2.95 m across 1920 px) and the yard/level camera (ortho ~1.44 at 1080 -> 750 px/m;
# the tree lens the earlier passes quote is 414.21 px/m, so 400 is the conservative one).
GALLERY_PX_M = 1920.0 / 2.95
YARD_PX_M = 414.21

# The luminance window every canvas lives in (same as the petal set): the renderer tint
# multiplies these, so 1.00 is "the kind's own colour" and 0.70 the deepest shade a leaf
# carries. Below ~0.55 every kind would drag toward black.
LUM_HI = 1.00
LUM_LO = 0.70

# The feather, as a fraction of a leaflet's half-width. 0.22 of an 18 px leaflet half-width
# is ~2 canvas px = ~1.2 px at the gallery lens: enough to take the 90-degree corners off
# the silhouette, not enough to turn the leaf into a blur.
RIM = 0.22

# Layout, in fractions of the leaf part's own box (the canvas IS that box).
# The rig keeps the sinus between the two leaflets empty on purpose (`sinus stays empty`
# in PlantFlower), so the canvas does too: two blades flanking a transparent middle.
BASE_F = 0.16          # where a leaflet's stalk leaves the node
TIP_F = 0.88           # where its point ends
BASE_X = 0.06          # base centre, in box widths from the middle
TIP_X = 0.44           # tip centre
HALF_W = 0.15          # half-width of a blade, in box widths

FAMILIES = {
    "wide": dict(
        key="leaf", seed=911,
        # A broad ovate leaflet pair: the 0.30 x 0.69 of-box blade is 0.0176 x 0.0230 m,
        # aspect 0.76, leaning out of the node.
        lobe=0.38,     # width profile: fraction of the half-width at the base
        bow=0.014,     # the blade's own outward bow, in box widths
        tip_pow=1.50,  # how fast the point closes (1.0 = a straight wedge at the tip)
        asym=0.030,    # how much further the right blade leans than the left
        stalk=0.30,    # the stalk fraction of the blade's length, 0..1 from the node
        ribs=2),
    "erect": dict(
        key="leaf_erect", seed=922,
        # An erect strap pair (tulip, iris, crocus...): near-parallel sides, the point in
        # the top fifth, the same footprint in box fractions.
        lobe=0.62,
        bow=0.010,
        tip_pow=4.00,
        asym=0.025,
        stalk=0.20,
        ribs=3),
}


def smooth(t):
    t = np.clip(t, 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def family_box(names):
    """Geometric mean box of a family: the shape every canvas is drawn in."""
    bx = math.exp(sum(math.log(LEAF_SIZE[n][0]) for n in names) / len(names))
    by = math.exp(sum(math.log(LEAF_SIZE[n][1]) for n in names) / len(names))
    return bx, by


def family_names():
    wide = [n for n, s in LEAF_SIZE.items() if s[0] / s[1] >= WIDE]
    erect = [n for n, s in LEAF_SIZE.items() if s[0] / s[1] < WIDE]
    return wide, erect


def canvas_size(bx, by):
    """Canvas pixels: the family's mean box at the petal set's resolution, both axes, so a
    canvas pixel is square in world metres inside its family."""
    w = int(round(bx * PPU_CANVAS))
    h = int(round(by * PPU_CANVAS))
    return max(24, w), max(24, h)


def _finish(a, lum, seed):
    """Alpha + luminance -> RGBA. RGB is grey: the renderer's tint carries the colour."""
    rng = random.Random(seed)
    sound = np.array([[rng.uniform(-0.014, 0.014) for _ in range(a.shape[1])]
                      for _ in range(a.shape[0])])
    lum = np.clip(lum + sound, 0.0, 1.0)
    h, w = a.shape
    rgb = np.round(lum[:, :, None] * 255.0).astype(np.uint8)
    rgba = np.concatenate([np.repeat(rgb, 3, axis=2),
                           np.round(np.clip(a, 0.0, 1.0) * 255.0).astype(np.uint8)[:, :, None]],
                          axis=2)
    return Image.fromarray(rgba, "RGBA")


def paint_leaf_pair(w, h, style, seed):
    """One pair of leaflets on a common node, drawn in the box fractions the rig uses.

    us is -0.5..0.5 across the box, vs is 0..1 from the BOTTOM. Row 0 of a PIL array is the
    TOP of the PNG, so vs is flipped on the way in (the same trap gen-fire-textures and
    gen-flower-textures document).
    """
    rng = random.Random(seed)
    us = (np.arange(w, dtype=np.float64) + 0.5) / w - 0.5
    vs = 1.0 - (np.arange(h, dtype=np.float64) + 0.5) / h

    alpha = np.zeros((h, w), dtype=np.float64)
    lum = np.ones((h, w), dtype=np.float64)

    def lay(a, shade):
        """Composite one grey form: alpha is kept (the shape), luminance is written where
        the form is - a later form covers an earlier one the way the cards stack."""
        nonlocal alpha, lum
        alpha = np.maximum(alpha, a)
        lum = np.where(a > 0.001, shade, lum)

    for sign in (-1.0, 1.0):
        # The blade's own axis: the base leaves the node at BASE_X, the point lands near
        # TIP_X, bowed outward by a small wobble so the pair is a plant and not a symbol.
        # The two blades differ a little on purpose (length, lean, belly): a pair drawn
        # strictly parallel reads as a tuning fork at the yard lens.
        tipf = TIP_F + sign * 0.015
        tipx = TIP_X + sign * style["asym"]
        lobe = style["lobe"] + sign * 0.05
        bow = sign * style["bow"] + rng.uniform(-0.006, 0.006)
        wob = rng.uniform(0.0, 2.0 * math.pi)

        t = np.clip((vs - BASE_F) / (tipf - BASE_F), 0.0, 1.0)      # 0 base, 1 point
        cx = sign * (BASE_X + (tipx - BASE_X) * t) + bow * np.sin(math.pi * t) \
            + 0.004 * np.sin(2.0 * math.pi * 1.5 * t + wob)
        # Width profile: a point at the node (the stalk), then a belly, then a WEDGE to the
        # point ((1 - t^p) with p ~ 1.5 closes gradually; the first version's (1 - t^2.3)^0.5
        # kept 46 % of the width at 90 % of the height and the tip read as a blunt cut).
        # The base closes to a POINT too: a constant-width base ended in a flat horizontal
        # bar, which every read of the sheet named as the defect.
        prof = np.clip(t / style["stalk"], 0.0, 1.0) ** 0.85 \
            * np.clip(1.0 - t ** style["tip_pow"], 0.0, 1.0)
        prof = np.clip(prof * HALF_W, 1e-6, None)

        # A leaflet exists only inside its own v span; the base and the top both close in a
        # point, so no card corner is left anywhere on the silhouette.
        span = smooth(np.clip((vs - BASE_F) / 0.03, 0.0, 1.0)) \
            * smooth(np.clip((tipf - vs) / 0.05, 0.0, 1.0))
        d = np.abs(us[None, :] - cx[:, None]) / prof[:, None]
        lay(smooth(np.clip((1.0 - d) / RIM, 0.0, 1.0)) * span[:, None],
            _leaf_shade(us, cx, prof, d, t, sign, style))

    # No node lump: the two blades' stalks already converge on the box' own centre-bottom,
    # which is where the rig's aimed `Petiole` card arrives - the junction is drawn by the
    # rig, and a painted pad there read as a floaty green spot in three separate reads.
    _ = (h, w)

    return _finish(alpha, lum, seed + 1)


def _leaf_shade(us, cx, prof, d, t, sign, style):
    """The blade's tone: a shaded base, a bright belly, a midrib groove with a lit ridge
    beside it, a few lateral veins and a rim that catches the light on the outer flank.

    Grey only - the renderer's kind tint multiplies all of it, so this is the FORM: a leaf
    with no groove at all reads as a flat plate, which is what the cards were.
    """
    h, w = len(t), len(us)
    shade = (LUM_LO + (LUM_HI - LUM_LO) * smooth((t - 0.05) / 0.50))[:, None]
    shade = shade * (1.0 - 0.055 * smooth((0.28 - t) / 0.28))[:, None]        # base in shade
    rib = np.exp(-((us[None, :] - cx[:, None]) / (prof * 0.17)[:, None]) ** 2)
    keep = smooth(t / 0.12)[:, None] * smooth((1.0 - t) / 0.06)[:, None]
    shade = shade * (1.0 - 0.110 * rib * keep)                               # the groove
    ridge = np.exp(-((np.abs(us[None, :] - cx[:, None]) - 0.30 * prof[:, None])
                     / (prof * 0.20)[:, None]) ** 2)
    shade = shade * (1.0 + 0.050 * ridge * keep)                             # the keel
    for k in range(style["ribs"]):
        at = 0.32 + 0.20 * k
        lat = np.exp(-((np.abs(us[None, :] - cx[:, None]) - 0.55 * prof[:, None])
                       / (prof * 0.15)[:, None]) ** 2)
        shade = shade * (1.0 - 0.035 * lat
                         * smooth((t - at + 0.16) / 0.18)[:, None]
                         * smooth((at + 0.16 - t) / 0.18)[:, None])
    rim = np.clip((d - 0.66) / 0.34, 0.0, 1.0)
    outer = (sign * (us[None, :] - cx[:, None]) > 0.0).astype(np.float64)
    shade = shade * (1.0 + 0.060 * rim * outer * smooth((t - 0.20) / 0.35)[:, None])
    _ = (h, w)
    return shade



def tones(img):
    a = np.asarray(img)
    solid = a[:, :, 3] >= 128
    if not solid.any():
        return 0
    return int(len(np.unique(a[:, :, 0][solid])))


def contact_sheet(paintings, greens):
    """For each family, one row of what the eye gets: the canvas magnified 8x, then the same
    canvas at the gallery lens and at the yard lens, each magnified back up - TINTED with a
    real kind's leaf colour over a mid-grey ground, because that is how it is drawn (the
    renderer's colour multiplies the grey canvas). A grey canvas on a dark sheet reads as
    fangs; the same canvas in leaf green on soil does not.
    """
    tiles = []
    for key, img in paintings.items():
        bx, by = img.info["box"]
        tint = greens[key]
        for label, px_m in (("canvas 8x", None), ("gallery lens", GALLERY_PX_M),
                            ("yard lens", YARD_PX_M)):
            if px_m is None:
                small = img
                zoom = 8
            else:
                small = img.resize((max(1, int(round(bx * px_m))),
                                    max(1, int(round(by * px_m)))), Image.LANCZOS)
                zoom = max(6, min(14, int(round(140.0 / max(1, small.height)))))
            big = small.resize((small.width * zoom, small.height * zoom), Image.NEAREST)
            tiles.append((key + " " + label, big, tint))
    pad = 12
    label_h = 16
    width = sum(t[1].width + pad for t in tiles) + pad
    big_h = max(t[1].height for t in tiles) + 2 * pad
    sheet = Image.new("RGBA", (width, 2 * big_h), (86, 82, 76, 255))
    draw = ImageDraw.Draw(sheet)
    x = pad
    for label, big, tint in tiles:
        lit = Image.new("RGBA", big.size, (int(tint[0] * 255), int(tint[1] * 255),
                                           int(tint[2] * 255), 255))
        # The canvas multiplies the tint, so the tile is the canvas' grey * the tint.
        arr = np.asarray(big).astype(np.float64)
        lit_arr = np.asarray(lit).astype(np.float64)
        out = arr.copy()
        out[:, :, 0] = arr[:, :, 0] / 255.0 * lit_arr[:, :, 0]
        out[:, :, 1] = arr[:, :, 1] / 255.0 * lit_arr[:, :, 1]
        out[:, :, 2] = arr[:, :, 2] / 255.0 * lit_arr[:, :, 2]
        out[:, :, 3] = arr[:, :, 3]
        sheet.alpha_composite(Image.fromarray(out.astype(np.uint8), "RGBA"), (x, pad))
        draw.text((x, big_h - label_h), label, fill=(230, 230, 230, 255))
        x += big.width + pad
    return sheet




# ─── import settings (the same shape as Tools/gen-flower-textures.py) ────────

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
    text = re.sub(r"^(\s*)maxTextureSize: .*$", r"\g<1>maxTextureSize: 512", text,
                  flags=re.MULTILINE)
    text = re.sub(r"^\s*nameFileIdTable: .*$", "    nameFileIdTable: {}", text, count=1,
                  flags=re.MULTILINE)
    with open(png_path + ".meta", "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def main():
    only = sys.argv[1:] if len(sys.argv) > 1 else None
    os.makedirs(FLOWER_DIR, exist_ok=True)
    os.makedirs(OUT_DIR, exist_ok=True)
    # NOT 20260920: that is the petal set's seed, and the same RNG draws the same first two
    # guids, which collided with petal.png.meta / petal_tip.png.meta (art0-meta-audit).
    guid_rng = random.Random(20260921)

    wide, erect = family_names()
    if not wide or not erect:
        print("FAMILY SPLIT FAILED: wide=%d erect=%d" % (len(wide), len(erect)))
        return 1

    paintings, meta_rows, worst = {}, [], 0.0
    for family, names in (("wide", wide), ("erect", erect)):
        style = FAMILIES[family]
        bx, by = family_box(names)
        w, h = canvas_size(bx, by)
        img = paint_leaf_pair(w, h, style, style["seed"])
        img.info["box"] = (bx, by)
        paintings[style["key"]] = img
        meta_rows.append((family, style["key"], names, bx, by, w, h))
        worst = max(worst, max(max(a / (bx / by), (bx / by) / a)
                               for a in (LEAF_SIZE[n][0] / LEAF_SIZE[n][1] for n in names)))

    rows = []
    for family, key, names, bx, by, w, h in meta_rows:
        img = paintings[key]
        path = os.path.join(FLOWER_DIR, key.split("/")[-1] + ".png")
        if only and key not in only:
            continue
        ppu = int(round(w / bx))
        img.save(path, "PNG", optimize=True)
        write_meta(path, ppu, existing_guid(path) or new_guid(guid_rng))
        a = np.asarray(img)[:, :, 3].astype(np.float64) / 255.0
        rows.append((family, key, len(names), bx, by, w, h, ppu, os.path.getsize(path),
                     100.0 * a.mean(), 100.0 * (a >= 0.999).mean(), tones(img)))

    print("canvas set written to Assets/Resources/Art/Flower/")
    print("  %-6s %-16s %-6s %-15s %-10s %-8s %-9s %-9s %s"
          % ("family", "key", "kinds", "family box m", "canvas", "coverage", "opaque", "tones", "bytes"))
    for family, key, kinds, bx, by, w, h, ppu, size, cov, opaque, tn in rows:
        print("  %-6s %-16s %-6d %-15s %-10s %-8.1f %-9.1f %-9d %d"
              % (family, key, kinds, "%.4f x %.4f" % (bx, by), "%dx%d" % (w, h),
                 cov, opaque, tn, size))
    print("  ppu written: " + ", ".join("%s=%d" % (r[1], r[7]) for r in rows))
    print("  resolution: %.0f px/m (canvas), gallery lens %.1f px/m, yard lens %.1f px/m"
          % (PPU_CANVAS, GALLERY_PX_M, YARD_PX_M))
    print("  .meta: textureType 8 (Sprite), spriteMode 1 (Single), spriteMeshType 0 (FullRect),")
    print("         wrapU/V 1, fallback physics shape 0, alphaIsTransparency 1, isReadable 1")

    print()
    print("  per-kind aspect error against its own family canvas (1.000 = drawn in the box):")
    for _, key, _, bx, by, _, _ in meta_rows:
        drawn = bx / by
        print("    %s canvas aspect %.3f" % (key, drawn))
        for n in sorted(LEAF_SIZE, key=lambda n: LEAF_SIZE[n][0] / LEAF_SIZE[n][1],
                        reverse=True):
            a = LEAF_SIZE[n][0] / LEAF_SIZE[n][1]
            fam = "wide" if a >= WIDE else "erect"
            if FAMILIES[fam]["key"] != key:
                continue
            px_w = LEAF_SIZE[n][0] * GALLERY_PX_M
            px_h = LEAF_SIZE[n][1] * GALLERY_PX_M
            print("      %-10s box %.3f x %.3f m  aspect %.3f  error %.3fx  on screen %.0fx%.0f px"
                  % (n, LEAF_SIZE[n][0], LEAF_SIZE[n][1], a, max(a / drawn, drawn / a),
                     px_w, px_h))
    print("  worst aspect error in the set: %.3fx" % worst)

    greens = {k: (0.24, 0.50, 0.22) for k in paintings}     # PlantFlower default leafColor
    sheet = contact_sheet(paintings, greens)
    out = os.path.join(OUT_DIR, "canvases.png")
    sheet.save(out)
    print("  contact sheet: %s (%dx%d: the canvas at 8x, then at the gallery lens and at the"
          % (os.path.relpath(out, ROOT).replace("\\", "/"), sheet.width, sheet.height))
    print("                 yard lens, magnified back up, tinted leaf-green - what the eye gets)")
    for family, key, _, bx, by, w, h in meta_rows:
        print("  rim: %s %.1f canvas px = %.1f px at the gallery lens = %.1f px at the yard lens"
              % (key, RIM * HALF_W * w, RIM * HALF_W * bx * GALLERY_PX_M,
                 RIM * HALF_W * bx * YARD_PX_M))
    return 0


if __name__ == "__main__":
    sys.exit(main())
