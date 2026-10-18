#!/usr/bin/env python3
"""ART-0 gap 3b, the last flat element of the plant: the flower's STEM.

Why this file exists
--------------------
The 24th pass painted the lavender spike and the autumn hip. Its own 4x read of the assembled
autumn rose then named what is left, without being asked:

    "the central green stem ... a perfectly straight, single-colour strip with no texture,
     shadow, curve or detail"

That strip is `PlantFlower` line 128:

    HumanSegment stem = AddPart("Stem", new Vector2(stemWidth, stemHeight), stemColor, 0);
    PlantTree.PaintVisualTaper(stem, stemWidth, stemHeight, stemColor, 0.62f, 1);

`PaintVisualTaper` with `bands = 1` calls `PaintVisualConeFill`, which switches the part's own
sprite OFF (`seg.sprite.enabled = false`) and draws SIX flat boxes under `Visual`:

    ConeCore, ConeCoreTip, ConeEdgeL, ConeEdgeLTip, ConeEdgeR, ConeEdgeRTip

one tint each, hard 90-degree corners, and the taper only comes from two rotated wedges. Every
other part of the plant is a painted canvas since the 20th/23rd/24th passes.

The answer is the same one three times over now: ONE canvas on the part's own box, drawn in the
part's own aspect, `Sliced` onto the box the part already has (`painted-parts.mdc`: one bone, one
PNG; the box is the mapping, never `localScale`). The rig's six cone cards step aside. The rig's
WINTER cards (Frost*, WinterBud*) stay: they are painted chips with their own tilt and the canvas
is the cane under them.

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

A key is therefore a READABLE Texture2D under `Assets/Resources/Art/`, the key is a path fragment
(`Flower/stem` -> `Art/Flower/stem.png`), and every canvas here is a LUMINANCE canvas (grey RGB,
the shape in alpha): the part keeps its own kind colour and the renderer MULTIPLIES the canvas, so
one canvas serves summer green, autumn brown and the winter cane.

The two boxes (measured, not guessed)
-------------------------------------
`PlantFlower.ApplyKind` (lines 1326-1437) walks `stemWidth` / `stemHeight` per kind; the season
does NOT touch them (spring scales bloomSize / leafSize / diskSize only, lines 1228-1231), so one
table covers all four seasons. The box the dress reads is not the authored one:
`HumanSegment.CreateBodySprite` (line 273) draws the placeholder at 256 px/m and then boosts the
SHORT side to 64 px, so every stem lands 64 px wide and the authored height moves by up to 8%.
`snapped()` below reproduces that arithmetic (same rounding rule as `Mathf.RoundToInt`), and the
gate's own INFO line prints the box Unity really reports.

Two canvases, not one: the box aspect spans 0.049 (Lavender) to 0.174 (Crocus) - a factor 3.5 -
and one canvas would stretch the corm kinds' stubs by 2.17x. The split is the leaf set's own
pattern (`ArtParts.LeafFamilyWide`): a threshold between the stalks and the two corm kinds
(Crocus 0.09 m, Violet 0.10 m), which are a botanical family of their own.

Run from the project root:   python Tools/gen-flower-stem-textures.py
Writes `Assets/Resources/Art/Flower/stem.png` + `stem_stout.png` and their `.meta`, then prints
the verification table (canvas, box per kind, family, aspect error, on-screen size at the two
lenses, coverage, opaque share, tones, md5) and a contact sheet into `Trials/Runs/art-0-stem/`.

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
OUT_DIR = os.path.join(ROOT, "Trials", "Runs", "art-0-stem")

# stemWidth / stemHeight per kind, transcribed from PlantFlower.ApplyKind (1326-1437).
STEM_SIZE = {
    "Rose":      (0.018, 0.22),
    "Tulip":     (0.016, 0.28),
    "Daisy":     (0.018, 0.20),
    "Poppy":     (0.014, 0.26),
    "Sunflower": (0.028, 0.42),
    "Lily":      (0.018, 0.30),
    "Daffodil":  (0.018, 0.18),
    "Violet":    (0.012, 0.10),
    "Crocus":    (0.014, 0.09),
    "Lavender":  (0.012, 0.24),
    "Iris":      (0.018, 0.27),
    "Bluebell":  (0.013, 0.16),
}

# The family split by BOX ASPECT, `ArtParts.StemFamilyStout` 0.112: the twelve boxes run 0.0492
# (Lavender) .. 0.0980 (Daisy) .. 0.1087 (Daffodil) | 0.1153 (Violet) .. 0.1739 (Crocus), so the
# threshold sits in the only clean gap (a 2.9% margin each side). The two kinds it catches are the
# corm ones - a stub peduncle on a corm, drawn with a wider root collar. Which kinds they are is
# NOT a list here either: family_of() applies the same rule the C# does, so the two cannot drift.
STOUT_ASPECT = 0.112
STOUT_CANVAS = "stem_stout"
STEM_CANVAS = "stem"

PPU_CANVAS = 2064.0

# The two lenses the picture is judged at, copied from the spike set: the gallery's flower still
# (ortho 0,88 -> 1080 px over 1.76 m) and the yard/level camera (414.21 px/m).
GALLERY_PX_M = 1080.0 / 1.76
YARD_PX_M = 414.21

# The luminance window of this family (petal / leaf / spike / hip): the renderer's tint multiplies
# these, so 1.00 is "the kind's own colour" and 0.70 the deepest shade a part carries.
LUM_HI = 1.00
LUM_LO = 0.70

# The rig's own taper (`PaintVisualTaper(stem, w, h, c, 0.62f, 1)`), its root flare, and the
# margin the drawn cane keeps to the left / right canvas border (see paint_stem).
TIP_SCALE = 0.62
MARGIN_PX = 1.6
PPM_PLACEHOLDER = 256.0


def smooth(t):
    t = np.clip(t, 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


# ─── the box, the way HumanSegment.CreateBodySprite builds it ────────────────

def snapped(sw, sh):
    """The box `ArtParts.BoxOf` reports for a stem: the placeholder sprite's own size.

    CreateBodySprite draws at 256 px/m, rounds both sides, then scales the SHORT side up to 64 px
    (and clamps to 8..16384), and sets ppu = w / size.x. Because ppu is derived from the width,
    the width comes back EXACTLY as authored; only the height moves.
    """
    w0 = max(1, int(round(sw * PPM_PLACEHOLDER)))
    h0 = max(1, int(round(sh * PPM_PLACEHOLDER)))
    scale = 1.0
    if w0 > 512 or h0 > 512:
        scale = 512.0 / max(w0, h0)
    min_side = min(w0, h0) * scale
    if min_side < 64.0:
        scale *= 64.0 / max(1.0, min_side)
    w = max(8, min(16384, int(round(w0 * scale))))
    h = max(8, min(16384, int(round(h0 * scale))))
    ppu = w / sw
    return (w / ppu, h / ppu)


def family_of(kind):
    bx, by = snapped(*STEM_SIZE[kind])
    return STOUT_CANVAS if bx / by >= STOUT_ASPECT else STEM_CANVAS


def family_box(canvas):
    """Geometric mean box of the kinds that draw this canvas: the shape the canvas is painted in."""
    keys = [k for k in STEM_SIZE if family_of(k) == canvas]
    bx = math.exp(sum(math.log(snapped(*STEM_SIZE[k])[0]) for k in keys) / len(keys))
    by = math.exp(sum(math.log(snapped(*STEM_SIZE[k])[1]) for k in keys) / len(keys))
    return bx, by


def canvas_size(bx, by):
    return max(24, int(round(bx * PPU_CANVAS))), max(24, int(round(by * PPU_CANVAS)))


# ─── the shared painting machinery (same as the spike / hip set) ─────────────

class Canvas:
    """One RGBA canvas being painted: alpha is the shape, luminance is the form.

    Row 0 of a PIL array is the TOP of the PNG while v is measured from the BOTTOM, so the v axis
    is flipped on the way in - the trap `gen-fire-textures.py`, `gen-flower-leaf-textures.py` and
    `gen-flower-spike-textures.py` all document.
    """

    def __init__(self, w, h, seed):
        self.w, self.h, self.seed = w, h, seed
        self.us = (np.arange(w, dtype=np.float64) + 0.5) / w - 0.5
        self.vs = 1.0 - (np.arange(h, dtype=np.float64) + 0.5) / h
        self.U = self.us[None, :]          # (1, w)  -0.5 .. +0.5 across the box
        self.V = self.vs[:, None]          # (h, 1)   0 .. 1 from the root up
        self.alpha = np.zeros((h, w), dtype=np.float64)
        self.lum = np.ones((h, w), dtype=np.float64)


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


# ─── the stem ────────────────────────────────────────────────────────────────

def paint_stem(w, h, seed, flare):
    """A cane: a round column in the rig's own taper, lit from the upper RIGHT.

    Where the light is: `Human/head` and `Human/torso` rise left-to-right (89->163 and 143->179 on
    the same five bands, measured), which is the `ArtLibrary.DimFar` convention - the LEFT is the
    far side. So the highlight sits just right of centre, the far edge is the darkest line in the
    picture and the near edge keeps a reflected-light rim.

    What makes it a stem and not a post: the cross-section shading (above), a root collar, two
    nodes with a collar lip under each, one leaf scar, a small scale mark near the tip, a slow
    lengthwise sheen, and a tip that closes in a dome instead of being cut by the box.
    """
    rng = random.Random(seed)
    c = Canvas(w, h, seed)
    v = c.V[:, 0]
    u = c.U[0, :]

    # The rig's own taper: full width at the root, TIP_SCALE at the top (PaintVisualConeFill's
    # baseW = width * 1.0 and tipW = width * 0.62), plus the root collar of this family.
    # The widest point stops MARGIN_PX short of the box edge on each side, so the silhouette never
    # touches the left / right canvas border and the check "nothing is cut by the canvas edge"
    # stays a hard gate. The TAPER RATIO is the rig's own 0.62 either way, so the cane is ~12%
    # thinner than the cone fill it replaces and reads as the same plant part.
    collar = 1.0 + flare * np.exp(-(v / 0.07) ** 2)
    profile = (1.0 - (1.0 - TIP_SCALE) * np.clip(v, 0.0, 1.0)) * collar / collar[-1]
    half_max = 0.5 - MARGIN_PX / w
    # The tip closes in a dome inside the box: the dome reaches zero at v = 0.994, before the last
    # canvas row, so nothing is cut by the top edge either.
    dome = np.sqrt(np.clip(1.0 - np.clip((v - 0.952) / 0.042, 0.0, 1.0) ** 2, 0.0, 1.0))
    half = np.maximum(half_max * profile * dome, 1e-6)

    # One canvas pixel is one pixel: the canvas is drawn in its own box' aspect, so a 1.1 px
    # feather is the same length in u and in v.
    feather = 1.1 / w
    alpha = smooth(np.clip((half[:, None] - np.abs(u)[None, :]) / feather, 0.0, 1.0))
    c.alpha = alpha

    # Cross-section of a round column: x = 0 at the far (left) edge, 1 at the near edge.
    x = np.clip((u[None, :] + half[:, None]) / (2.0 * half[:, None]), 0.0, 1.0)
    peak = 0.62
    t = np.clip(1.0 - np.abs(x - peak) / max(peak, 1.0 - peak), 0.0, 1.0) ** 0.75
    tone = LUM_LO + (LUM_HI - LUM_LO) * (0.30 + 0.70 * t)
    # The far edge is the darkest line; the near edge keeps a rim of bounced light.
    tone = tone * np.where(x < peak, np.interp(x, [0.0, peak], [0.90, 1.0]),
                           np.interp(x, [peak, 1.0], [1.0, 0.86]))

    # Down the cane: a slow sheen, a darker root, a darker tip.
    ctrl = np.array([rng.uniform(-0.035, 0.035) for _ in range(12)])
    sheen = 1.0 + np.interp(v, np.linspace(0.0, 1.0, len(ctrl)), ctrl)
    tone = tone * sheen[:, None]
    tone = tone * (1.0 - 0.10 * np.exp(-(v / 0.055) ** 2))[:, None]
    tone = tone * (1.0 - 0.05 * np.exp(-((v - 0.97) / 0.03) ** 2))[:, None]

    # Two nodes, each with the collar lip a stem puts under the bud that left it.
    for vn, depth in ((0.30, 0.075), (0.62, 0.060)):
        tone = tone * (1.0 - depth * np.exp(-((v - vn) / 0.010) ** 2))[:, None]
        tone = tone * (1.0 + 0.040 * np.exp(-((v - (vn - 0.020)) / 0.009) ** 2))[:, None]

    # One leaf scar at the right flank, one bud scale near the tip: the marks that break the ruler.
    scar_v, scar_u = 0.455, 0.30
    tone = tone * (1.0 - 0.085 * np.exp(-(((v[:, None] - scar_v) / 0.012) ** 2
                                        + ((u[None, :] - scar_u * 2.0 * half[:, None] / 0.5 * 0.5)
                                           / (0.16 * np.maximum(half[:, None], 1e-6))) ** 2)))
    tick_v = 0.795
    tone = tone * (1.0 - 0.070 * np.exp(-(((v[:, None] - tick_v) / 0.008) ** 2
                                        + ((u[None, :] + 0.20 * 2.0 * half[:, None] * 0.5)
                                           / (0.12 * np.maximum(half[:, None], 1e-6))) ** 2)))
    # A faint lengthwise fibre, so the cane is not a gradient: two soft lines off the highlight.
    fibre = (np.exp(-((u[None, :] - 0.16 * 2.0 * half[:, None]) / (0.10 * np.maximum(half[:, None], 1e-6))) ** 2)
             + 0.7 * np.exp(-((u[None, :] + 0.46 * 2.0 * half[:, None])
                              / (0.09 * np.maximum(half[:, None], 1e-6))) ** 2))
    tone = tone * (1.0 - 0.045 * fibre)

    c.lum = np.clip(tone, 0.0, 1.0)
    return _finish(c, seed + 1)


# ─── the .meta (same writer as the spike / hip set) ──────────────────────────

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


def edge_counts(img):
    """Opaque pixels on each border. LEFT / RIGHT / TOP must be empty (nothing may be cut);

    the BOTTOM is the root line - the box' own base, where the cane leaves the soil - so the stem
    is allowed to carry it, and the count is printed instead of being a failure.
    """
    a = np.asarray(img)[:, :, 3] > 0
    return {"left": int(a[:, 0].sum()), "right": int(a[:, -1].sum()), "top": int(a[0, :].sum()),
            "bottom": int(a[-1, :].sum())}


def contact_sheet(tiles):
    """The canvas at 8x, then at the gallery lens and the yard lens, magnified back up, tinted the
    way the renderer multiplies it (a grey canvas on a dark sheet reads as a stain; the same
    canvas in stem green on soil reads as a stem).
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
    # NOT the petal / leaf (20260920/1) or spike / hip (20260922) seeds: the same RNG draws the
    # same first guids, the collision `art0-meta-audit.py` caught on the 23rd pass.
    guid_rng = random.Random(20260923)

    jobs = [
        (STEM_CANVAS, family_box(STEM_CANVAS), paint_stem, 951, 0.045),
        (STOUT_CANVAS, family_box(STOUT_CANVAS), paint_stem, 962, 0.150),
    ]

    rows, paintings = [], {}
    for name, box, painter, seed, flare in jobs:
        w, h = canvas_size(box[0], box[1])
        img = painter(w, h, seed, flare)
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
    print("  %-11s %-15s %-18s %-10s %-7s %-9s %-8s %-7s %-8s %s"
          % ("canvas", "key", "box m", "canvas", "ppu", "coverage", "opaque", "tones", "bytes", "md5"))
    for name, key, box, w, h, ppu, size, cov, opaque, tn, md5 in rows:
        print("  %-11s %-15s %-18s %-10s %-7d %-9.1f %-8.1f %-7d %-8d %s"
              % (name, key, "%.4f x %.4f" % box, "%dx%d" % (w, h), ppu, cov, opaque, tn, size, md5))
    print("  resolution: %.0f px/m (canvas), gallery lens %.1f px/m (the flower still's measured"
          % (PPU_CANVAS, GALLERY_PX_M))
    print("              ortho 0,88), yard lens %.1f px/m" % YARD_PX_M)
    print("  .meta: textureType 8 (Sprite), spriteMode 1 (Single), spriteMeshType 0 (FullRect),")
    print("         wrapU/V 1, fallback physics shape 0, alphaIsTransparency 1, isReadable 1")

    cut = {name: edge_counts(img) for name, img in paintings.items()}
    worst_cut = max(max(e["left"], e["right"], e["top"]) for e in cut.values())
    print("  nothing is cut by the canvas edge (LEFT/RIGHT/TOP empty; the BOTTOM is the root line,")
    print("  where the cane leaves the soil - the box' own base):")
    for name in sorted(cut):
        e = cut[name]
        print("    %-11s left=%d right=%d top=%d   bottom=%d (root)" % (name, e["left"], e["right"],
                                                                      e["top"], e["bottom"]))
    print("    -> " + ("OK" if worst_cut == 0 else "FAIL (a form is cut by a side or the top)"))
    if worst_cut != 0:
        return 1

    print()
    print("  per-kind aspect error against the canvas its own box asks for (1.000 = drawn in the box):")
    worst = {}
    for name, key, box, w, h, ppu, size, cov, opaque, tn, md5 in rows:
        drawn = box[0] / box[1]
        print("    %s canvas aspect %.4f (%.4f x %.4f m, %dx%d px)"
              % (key, drawn, box[0], box[1], w, h))
        errs = []
        for kind, (sw, sh) in sorted(STEM_SIZE.items(), key=lambda kv: snapped(*kv[1])[0] / snapped(*kv[1])[1]):
            if family_of(kind) != name:
                continue
            bx, by = snapped(sw, sh)
            a = bx / by
            err = max(a / drawn, drawn / a)
            errs.append((kind, a, err))
            print("      %-10s authored %.3f x %.3f  box %.4f x %.4f m  aspect %.4f  error %.3fx"
                  "   on screen %.0fx%.0f px (gallery) %.0fx%.0f px (yard)"
                  % (kind, sw, sh, bx, by, a, err,
                     bx * GALLERY_PX_M, by * GALLERY_PX_M, bx * YARD_PX_M, by * YARD_PX_M))
        worst[name] = max(e[2] for e in errs)
        print("      worst aspect error in this family: %.3fx (%s)"
              % (worst[name], max(errs, key=lambda e: e[2])[0]))
    one = []
    for kind, (sw, sh) in STEM_SIZE.items():
        bx, by = snapped(sw, sh)
        a = bx / by
        gb = family_box(STEM_CANVAS)
        ga = gb[0] / gb[1]
        gb2 = family_box(STOUT_CANVAS)
        ga2 = gb2[0] / gb2[1]
        one.append((kind, max(a / ga, ga / a), max(a / ga2, ga2 / a)))
    print("  why two canvases: with ONE canvas in the stalk family's box (aspect %.4f) the worst"
          % (family_box(STEM_CANVAS)[0] / family_box(STEM_CANVAS)[1]))
    print("    error would be %.2fx (%s); the split at the corm family gives %.2fx / %.2fx"
          % (max(o[1] for o in one), max(one, key=lambda o: o[1])[0],
             worst[STEM_CANVAS], worst[STOUT_CANVAS]))
    print("  the split rule is ArtParts.StemFamilyStout 0.112 on the BOX aspect, and the only gap"
          " in the set is")
    print("    Daffodil 0.1087 | Violet 0.1153 (a 2.9% margin each side); the two kinds above it"
          " are the corm ones.")

    tiles = [
        ("stalk (Rose)", paintings[STEM_CANVAS], family_box(STEM_CANVAS), (0.22, 0.46, 0.20)),
        ("stalk (Lavender)", paintings[STEM_CANVAS], family_box(STEM_CANVAS), (0.32, 0.40, 0.24)),
        ("stalk autumn", paintings[STEM_CANVAS], family_box(STEM_CANVAS), (0.28, 0.36, 0.17)),
        ("stalk winter", paintings[STEM_CANVAS], family_box(STEM_CANVAS), (0.26, 0.32, 0.20)),
        ("corm (Crocus)", paintings[STOUT_CANVAS], family_box(STOUT_CANVAS), (0.30, 0.44, 0.22)),
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
