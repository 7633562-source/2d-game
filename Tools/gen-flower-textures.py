#!/usr/bin/env python3
"""ART-0 gap 3: the flower's petal set - one canvas per flower part role.

Why this file exists
--------------------
`Assets/PlantFlower.cs` builds every petal as a collider-sized RECTANGLE:
`HumanSegment.ApplyPicture(sr, petal, HumanVisualShape.Box, null, petalPaint)` - one flat
colour, hard corners (the "transparent rectangle" of Yury's 18.09 complaint, still true for
the last object in the game that has no painted art). The review answer to ART-0 gap item 3
("check whether the strawberry/apple leaf set can cover it") is NO for the bloom: a petal
is not a leaf, and the 512 leaf tile sliced onto a 0.03 m petal box is a 6 % fragment of a
0.5 m tile - mush. So the bloom gets its own canvas set.

What the loader needs (quoted, not guessed)
-------------------------------------------
`Assets/ArtLibrary.cs`

    Albedo(key):  Texture2D tex = Resources.Load<Texture2D>("Art/" + key);     // line 139
    Tiled(key, metersPerTile):
                  if (... metersPerTile <= 0.01f) return null;
                  float ppu = tex.width / metersPerTile;                        // line 165
                  Sprite.Create(tex, full rect, pivot .5/.5, ppu, 0, FullRect)

`Assets/ArtParts.cs`

    Dress(sr, key, tileMeters, real):
                  float tile = local > 0.01f ? local : Mathf.Max(box.x, box.y); // one repeat
                  Sprite sprite = ArtLibrary.Tiled(key, tile);
                  sr.sprite = sprite; sr.color = <white today>; sr.size = box;   // Sliced

So a key is a READABLE Texture2D under `Assets/Resources/Art/`, the key is a path fragment
("Flower/petal" -> `Art/Flower/petal.png`), and `Dress` maps the WHOLE canvas onto the box
the part already has (Sliced, size = sprite.size, never localScale). One canvas per part role
is therefore right: the canvas is drawn in that role's box aspect.

The tint is kept
----------------
Every flower part is tinted per kind (`petalPaint`, `sepalColor`, `diskColor`, the leaflet
tint), so these canvases are LUMINANCE canvases: a grey form 0.70-1.00 with the silhouette in
the alpha channel. `ArtParts.Dress(..., keepColor: true)` leaves `sr.color` alone and the
renderer's own kind colour MULTIPLIES the canvas - twelve kinds, one canvas each.

Boxes (read out of `PlantFlower.PaintBloomPetals` / `PaintCalyx` / `ApplyKind`)
------------------------------------------------------------------------------
    petal      bloomSize * (0.36, 0.52)        rose 0.029x0.036  sunflower 0.058x0.083
    petal tip  petal    * (0.48, 0.26)         the distal lip, a WIDE box
    cup        bloomSize                       the vessel of tulip / lily / crocus / daffodil
    sepal      bloomSize * (0.22, 0.32)        calyx blade
    disk       diskSize                        the head plate (daisy, sunflower, poppy...)

Run from the project root:   python Tools/gen-flower-textures.py
Writes `Assets/Resources/Art/Flower/*.png` plus their `.meta` import settings, then prints
the verification table (canvas, its role's box, aspect error, coverage, opaque share, tones).

Deterministic: every wobble comes from `random.Random(<fixed seed>)`; no timestamps are
written, so a second run reproduces the same bytes and the same guids.
"""

import math
import os
import random
import re
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FLOWER_DIR = os.path.join(ROOT, "Assets", "Resources", "Art", "Flower")
META_TEMPLATE = os.path.join(ROOT, "Assets", "LegacyArt", "Apple", "fruit.png.meta")

# role -> (canvas w, h, the box the canvas is drawn in, in metres)
# The box column is the median of the shipping boxes for that role across the 12 kinds
# (`PlantFlower.ApplyKind`), so the aspect error printed below is the honest one.
ROLES = {
    "petal":     dict(w=40, h=64, box=(0.0384, 0.0615), seeds=(41,)),
    "petal_tip": dict(w=44, h=34, box=(0.0184, 0.0142), seeds=(52,)),
    "cup":       dict(w=40, h=52, box=(0.0725, 0.0850), seeds=(63,)),
    "calyx":     dict(w=26, h=40, box=(0.0253, 0.0373), seeds=(74,)),
    "disk":      dict(w=56, h=56, box=(0.0345, 0.0363), seeds=(85,)),
}

KEYS = ("petal", "petal_tip", "cup", "calyx", "disk")

# The luminance window every canvas lives in. The renderer tint multiplies these values,
# so 1.00 means "the kind's own colour" and 0.70 is the deepest shade a part carries;
# going below ~0.55 would drag every kind toward black and kill the twelve colours.
LUM_HI = 1.00
LUM_LO = 0.70

# The silhouette's feathered rim, as a fraction of the part's half-width. 0.14 is about
# 2 px on the widest row of the 40 px petal canvas: solid form, soft cut face (the
# painted-parts rule), and the box's hard corners are gone.
EDGE = 0.14


def smooth(t):
    t = np.clip(t, 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def axes(w, h):
    """us in -1..1 across the canvas, vs in 0..1 from the BOTTOM (a part's own height).

    Row 0 of a PIL array is the TOP of the PNG, so vs is flipped on the way in - the same
    trap `gen-fire-textures.flame_mask` documents.
    """
    ys = (np.arange(h, dtype=np.float64) + 0.5) / h
    us = (np.arange(w, dtype=np.float64) + 0.5) / w * 2.0 - 1.0
    return us, 1.0 - ys


def jaw(rng, n, amp):
    """A small deterministic wobble of n cells, so an outline is not analytic-clean."""
    phase = rng.random() * 2.0 * math.pi
    return lambda t: amp * np.sin(2.0 * math.pi * n * t + phase)


def _finish(a, lum, box_aspect, dither=0.014, seed=7):
    """Alpha + luminance -> RGBA. RGB is grey: the renderer's tint carries the colour."""
    rng = random.Random(seed)
    sound = np.array([[rng.uniform(-dither, dither) for _ in range(a.shape[1])]
                      for _ in range(a.shape[0])])
    lum = np.clip(lum + sound, 0.0, 1.0)
    h, w = a.shape
    rgb = np.round(lum[:, :, None] * 255.0).astype(np.uint8)
    rgba = np.concatenate([np.repeat(rgb, 3, axis=2),
                           np.round(np.clip(a, 0.0, 1.0) * 255.0).astype(np.uint8)[:, :, None]],
                          axis=2)
    return Image.fromarray(rgba, "RGBA")


def paint_petal(w=40, h=64, seed=41):
    """One petal face: narrow at the base, widest above the middle, rounded tip.

    The canvas IS the petal's box, so the shape has to live in the alpha channel: the box's
    hard corners are exactly what the flat rectangle showed, and they are what goes away.
    """
    rng = random.Random(seed)
    us, vs = axes(w, h)
    v = vs
    edge = jaw(rng, 3.0, 0.035)
    prof = 0.97 * (0.42 + 0.58 * np.clip(v / 0.30, 0.0, 1.0)) * np.clip(1.0 - v ** 2.40, 0.0, 1.0) ** 0.50
    prof = np.clip(prof * (1.0 + edge(v)), 0.0, 1.0)
    cx = 0.055 * v ** 1.5 + rng.uniform(-0.04, 0.04) * np.sin(2.0 * math.pi * 1.3 * v)

    t = np.abs(us[None, :] - cx[:, None]) / np.maximum(prof, 1e-6)[:, None]
    # SOLID inside the silhouette, feathered over the last 14 % of the half-width:
    # a soft-everywhere field reads as a fuzzy blob at 10-30 px, not as a petal.
    a = smooth(np.clip((1.0 - t) / EDGE, 0.0, 1.0))
    a *= np.clip(v / 0.05, 0.0, 1.0)[:, None]        # no hard cut where it meets the centre

    # Luminance: a shaded base, a bright belly, a rim that catches the light on the upper
    # half, a soft midrib, two side veins. This is what makes one petal read as a form
    # instead of a card - at 10-30 px on screen the ramp is what the eye gets.
    lum = (LUM_LO + (LUM_HI - LUM_LO) * smooth((v - 0.06) / 0.78))[:, None]
    rib = np.exp(-((us[None, :] - cx[:, None]) / 0.075) ** 2)
    lum = lum * (1.0 - 0.075 * rib * smooth(v / 0.15)[:, None] * smooth((1.0 - v) / 0.10)[:, None])
    vein = np.exp(-((np.abs(us[None, :] - cx[:, None]) - 0.46 * prof[:, None]) / 0.10) ** 2)
    lum = lum * (1.0 - 0.035 * vein * smooth(v / 0.30)[:, None] * smooth((0.92 - v) / 0.20)[:, None])
    rim = np.clip((t - 0.74) / 0.26, 0.0, 1.0) * np.clip((v - 0.28) / 0.30, 0.0, 1.0)[:, None]
    lum = lum * (1.0 + 0.055 * rim)
    return _finish(a, lum, 0.0384 / 0.0615, seed=seed + 1)


def paint_petal_tip(w=44, h=34, seed=52):
    """The distal lip: the outer end of a petal, a WIDE box, brighter on top."""
    rng = random.Random(seed)
    us, vs = axes(w, h)
    v = vs
    edge = jaw(rng, 2.0, 0.030)
    prof = (0.76 + 0.24 * np.clip(v / 0.20, 0.0, 1.0)) * np.clip(1.0 - v ** 3.0, 0.0, 1.0) ** 0.50
    prof = np.clip(prof * (1.0 + edge(v)), 0.0, 1.0)
    cx = rng.uniform(-0.05, 0.05) * np.sin(2.0 * math.pi * v)
    t = np.abs(us[None, :] - cx[:, None]) / np.maximum(prof, 1e-6)[:, None]
    a = smooth(np.clip((1.0 - t) / EDGE, 0.0, 1.0))
    a *= np.clip(v / 0.06, 0.0, 1.0)[:, None]
    # The base is the petal's attachment edge (hidden under the flower's centre) and the
    # top is the lit lip. Lateral shading, not a vertical gradient: without it the eye
    # reads a flat card with a blown-out top third.
    side = np.clip(np.abs(us[None, :] - cx[:, None]) / np.maximum(prof, 1e-6)[:, None], 0.0, 1.0)

    lum = (LUM_LO + (LUM_HI - LUM_LO) * smooth((v - 0.10) / 0.55))[:, None]
    lum = lum * (1.0 - 0.055 * side ** 2)                        # the flanks fall away
    lum = lum * (1.0 - 0.05 * np.exp(-(v / 0.09) ** 2)[:, None])  # the underside is in shadow
    lum = lum * (1.0 + 0.05 * np.clip((t - 0.70) / 0.30, 0.0, 1.0) * np.clip((v - 0.45) / 0.30, 0.0, 1.0)[:, None])
    return _finish(a, lum, 0.0184 / 0.0142, seed=seed + 1)


def paint_cup(w=40, h=52, seed=63):
    """The vessel of the cup kinds (tulip, lily, crocus, daffodil).

    The bloom box stays VISIBLE for those kinds (`PaintBloomPetals`: only open kinds hide
    it), so it is the cup the eye sees the petals rising out of: a closed bottom, a belly,
    three petal seams and a mouth that fades out instead of ending on a straight cut.
    """
    rng = random.Random(seed)
    us, vs = axes(w, h)
    v = vs
    edge = jaw(rng, 2.5, 0.028)
    prof = 0.62 + 0.38 * np.clip(v / 0.88, 0.0, 1.0) ** 1.10
    prof = np.clip(prof * (1.0 + edge(v)), 0.0, 1.0)
    cx = rng.uniform(-0.04, 0.04) * np.sin(2.0 * math.pi * 0.9 * v)
    t = np.abs(us[None, :] - cx[:, None]) / np.maximum(prof, 1e-6)[:, None]
    a = smooth(np.clip((1.0 - t) / EDGE, 0.0, 1.0))
    # rounded bottom, and a mouth with a real edge: a U-shaped cut (the middle of the
    # opening dips) instead of a fade, which at 10-20 px read as a rendering bug - a
    # hard bottom under a soapy top.
    a *= np.clip(v / 0.06, 0.0, 1.0)[:, None]
    top = 0.93 + 0.06 * (1.0 - np.clip((us / 0.70) ** 2, 0.0, 1.0))
    a = a * smooth(np.clip((top[None, :] - v[:, None]) / 0.09, 0.0, 1.0))

    lum = (LUM_LO + (LUM_HI - LUM_LO) * (0.45 + 0.55 * smooth((v - 0.08) / 0.60)))[:, None]
    seams = 0.0
    for off in (0.0, 0.48, -0.48):
        seams += np.exp(-((np.abs(us[None, :] - cx[:, None]) - abs(off) * prof[:, None]) / 0.09) ** 2)
    lum = lum * (1.0 - 0.055 * np.clip(seams, 0.0, 1.0) * smooth(v / 0.25)[:, None])
    lum = lum * (1.0 - 0.12 * np.clip((v - 0.84) / 0.16, 0.0, 1.0)[:, None])   # the mouth is darker
    return _finish(a, lum, 0.0725 / 0.0850, seed=seed + 1)


def paint_calyx(w=26, h=40, seed=74):
    """A sepal: a narrow green blade under the bloom, pointed, with a midrib."""
    rng = random.Random(seed)
    us, vs = axes(w, h)
    v = vs
    edge = jaw(rng, 3.5, 0.040)
    prof = (0.34 + 0.66 * np.clip(v / 0.28, 0.0, 1.0)) * np.clip(1.0 - v ** 1.85, 0.0, 1.0) ** 0.50
    prof = np.clip(prof * (1.0 + edge(v)), 0.0, 1.0)
    cx = 0.08 * v ** 1.4
    t = np.abs(us[None, :] - cx[:, None]) / np.maximum(prof, 1e-6)[:, None]
    a = smooth(np.clip((1.0 - t) / EDGE, 0.0, 1.0))
    a *= np.clip(v / 0.05, 0.0, 1.0)[:, None]
    lum = (LUM_LO + (LUM_HI - LUM_LO) * smooth((v - 0.05) / 0.70))[:, None]
    rib = np.exp(-((us[None, :] - cx[:, None]) / 0.10) ** 2)
    lum = lum * (1.0 - 0.07 * rib * smooth(v / 0.20)[:, None])
    lum = lum * (1.0 + 0.04 * np.clip((t - 0.72) / 0.28, 0.0, 1.0) * np.clip((v - 0.35) / 0.30, 0.0, 1.0)[:, None])
    return _finish(a, lum, 0.0253 / 0.0373, seed=seed + 1)


def paint_disk(w=56, h=56, seed=85):
    """The head plate: a ring of florets around a seeded centre (daisy, sunflower, poppy...).

    The kinds' disk colours run from pale yellow to dark brown, so this canvas is a
    LATTICE, not a colour: an outer ring of florets, a granulated middle, a darker centre.
    At 0.018-0.07 m on screen it is a 6-25 px dot - the ring and the centre is what reads.
    """
    rng = random.Random(seed)
    us, vs = axes(w, h)
    v = vs * 2.0 - 1.0
    r = np.sqrt(us[None, :] ** 2 + v[:, None] ** 2)
    a = smooth(np.clip((0.97 - r) / (EDGE * 0.6), 0.0, 1.0))

    ang = np.arctan2(v[:, None], us[None, :])
    lum = 0.78 + 0.16 * smooth((0.95 - r) / 0.30)
    # A plate has a top and a bottom: a directional term keeps it from reading as a coin.
    lum = lum * (1.0 + 0.065 * np.clip(0.75 * v[:, None] - 0.45 * us[None, :], -1.0, 1.0))
    # the seed head: a granulated centre that darkens toward the middle
    grain = 0.5 + 0.5 * np.sin(6.0 * us[None, :] * 3.1) * np.sin(6.0 * v[:, None] * 3.3)
    lum = lum * (1.0 - 0.16 * np.clip(1.0 - r / 0.62, 0.0, 1.0) ** 1.2 * (0.65 + 0.35 * grain))
    # the floret ring: one bright bead per 12 degrees, sitting at r 0.66..0.86
    ring = np.exp(-((r - 0.76) / 0.085) ** 2) * (0.5 + 0.5 * np.cos(12.0 * ang))
    lum = lum * (1.0 + 0.14 * np.clip(ring, 0.0, 1.0))
    lum = lum * (1.0 - 0.20 * np.clip((r - 0.90) / 0.10, 0.0, 1.0))     # a rim, so the plate has an edge
    return _finish(a, lum, 0.0345 / 0.0363, seed=seed + 1)


PAINTERS = {
    "petal": paint_petal,
    "petal_tip": paint_petal_tip,
    "cup": paint_cup,
    "calyx": paint_calyx,
    "disk": paint_disk,
}


# ─── import settings (the same shape as Tools/gen-level-textures.py / gen-fire-textures.py) ──

def new_guid(rng):
    return "".join(rng.choice("0123456789abcdef") for _ in range(32))


def existing_guid(png_path):
    meta = png_path + ".meta"
    if not os.path.exists(meta):
        return None
    with open(meta, "r", encoding="utf-8") as fh:
        m = re.search(r"^guid: ([0-9a-f]{32})$", fh.read(), re.MULTILINE)
    return m.group(1) if m else None


def write_meta(png_path, pixels_per_unit, readable, alpha, guid):
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
    set_line("isReadable", 1 if readable else 0)
    set_line("wrapU", 1, indent=4)
    set_line("wrapV", 1, indent=4)
    set_line("alphaIsTransparency", 1 if alpha else 0)
    set_line("spriteGenerateFallbackPhysicsShape", 0)     # art never carries a collider
    set_line("textureType", 8)                            # Sprite
    set_line("spriteMode", 1)                             # Single
    set_line("spriteID", guid[:16] + "0800000000000000", indent=4)
    # Parity with every shipped part meta (Apple/Strawberry use 512) and a smaller build:
    # the biggest canvas here is 64 px, so a 512 cap never touches the pixels. The template
    # carries the field once per platform override, so all of them move together.
    text = re.sub(r"^(\s*)maxTextureSize: .*$", r"\g<1>maxTextureSize: 512", text,
                  flags=re.MULTILINE)
    text = re.sub(r"^\s*nameFileIdTable: .*$", "    nameFileIdTable: {}", text, count=1,
                  flags=re.MULTILINE)
    with open(png_path + ".meta", "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


# ─── verification ────────────────────────────────────────────────────────────

def tones(img):
    """Distinct luminance values of the pixels that carry paint (alpha >= 128)."""
    a = np.asarray(img)
    solid = a[:, :, 3] >= 128
    if not solid.any():
        return 0
    return int(len(np.unique(a[:, :, 0][solid])))


def contact_sheet(paintings, scale=6):
    """Every canvas magnified with NEAREST, side by side - the eye's first pass."""
    tiles = []
    for key in KEYS:
        img = paintings[key]
        tiles.append((key, img.resize((img.width * scale, img.height * scale), Image.NEAREST)))
    width = sum(t.width + 12 for _, t in tiles) + 12
    height = max(t.height for _, t in tiles) + 44
    sheet = Image.new("RGBA", (width, height), (26, 30, 44, 255))
    x = 12
    for _, t in tiles:
        sheet.alpha_composite(t, (x, 12))
        x += t.width + 12
    return sheet


def main():
    only = sys.argv[1:] if len(sys.argv) > 1 else None
    os.makedirs(FLOWER_DIR, exist_ok=True)
    guid_rng = random.Random(20260920)

    paintings = {key: PAINTERS[key]() for key in KEYS}
    rows = []
    for key in KEYS:
        img = paintings[key]
        path = os.path.join(FLOWER_DIR, key + ".png")
        if only and key not in only:
            continue
        # The .meta ppu is cosmetic: `ArtParts.Dress` asks `ArtLibrary.Tiled` for the box's
        # own period, which recomputes ppu = tex.width / metres. Carried anyway, at the
        # canvas' own resolution over its role's box, so the imported sprite is that box.
        box = ROLES[key]["box"]
        ppu = int(round(img.width / box[0]))
        img.save(path, "PNG", optimize=True)
        write_meta(path, ppu, True, True, existing_guid(path) or new_guid(guid_rng))
        a = np.asarray(img)[:, :, 3].astype(np.float64) / 255.0
        rows.append((key, img.width, img.height, box, ppu, os.path.getsize(path),
                     img.width / float(img.height) / (box[0] / box[1]),
                     100.0 * a.mean(), 100.0 * (a >= 0.999).mean(), tones(img)))

    print("canvas set written to Assets/Resources/Art/Flower/")
    print("  %-10s %-9s %-15s %-9s %-8s %-9s %-9s %s"
          % ("key", "canvas", "role box m", "aspect", "coverage", "opaque", "tones", "bytes"))
    for key, w, h, box, ppu, size, aspect, cov, opaque, tn in rows:
        print("  %-10s %-9s %-15s %-9.4f %-8.1f %-9.1f %-9d %d"
              % (key, "%dx%d" % (w, h), "%.4f x %.4f" % box, aspect, cov, opaque, tn, size))
    print("  ppu written: " + ", ".join("%s=%d" % (k, v) for k, _, _, _, v, _, _, _, _, _ in rows))
    print("  .meta: textureType 8 (Sprite), spriteMode 1 (Single), spriteMeshType 0 (FullRect),")
    print("         wrapU/V 1, fallback physics shape 0, isReadable 1 (Albedo/Sample path)")

    sheet = contact_sheet(paintings)
    out = os.path.join(ROOT, "Trials", "Runs", "art-0-flower-20th")
    os.makedirs(out, exist_ok=True)
    sheet.save(os.path.join(out, "canvases_6x.png"))
    print("  contact sheet: Trials/Runs/art-0-flower-20th/canvases_6x.png (%dx%d, 6x NEAREST)"
          % (sheet.width, sheet.height))
    return 0


if __name__ == "__main__":
    sys.exit(main())
