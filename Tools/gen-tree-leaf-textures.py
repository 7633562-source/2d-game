#!/usr/bin/env python3
"""ART-0 29th pass: the tree's LEAF CARDS stop being crops of the leaf tile.

The defect this file answers (measured on the shipped frame, not guessed)
-----------------------------------------------------------------------
Three separate vision reads of `Trials/Runs/art0leaf28_tree.png` (the oak at the tree lens,
414.2 px/m) named the same thing, twice in the crown and once on the trunk:

  * "flat rectangular plates inside the crown, hard stepped edges" - the crown's boundary;
  * "identical small green sprites of buds/moss on the trunk" and "flat green chips, no
    volume, they do not read as oak leaves but as a sticker" - the 16-32 px green clusters
    `Tools/art0-trunk-band-probe.py` measures ON the trunk column.

The cause is arithmetic, not taste. `ArtParts.DressAll` dresses every leaf-named part with
the `leaf` MATERIAL, `Tiled` at `ArtParts.LeafTileMeters` = 0.5 m; the canvas is 512 px, so
1024 px/m. The rig's own leaf parts (`Art1Check` INFO `oak-leaf`, this cycle) are:

    <0.02 m  1171     0.02-0.05 m  234     0.05-0.10 m  14     0.10-0.30 m  20     >=0.30 m  2

and the canvas' own leaves are 70-130 px = 0.068-0.127 m long, one per 73 px cell. A part
of 0.04 m - the chips the reads named - therefore shows a 41 px window of a field whose
leaves are 1.7-3x LONGER than the part: `Tools/art0-leaf-window-probe.py` measures every
such window at 87 % ink coverage with a CUT leaf on 100 % of the window borders. A window
like that is a solid green plate with the part's own rectangle as its edge, whatever the
alpha - the material cannot draw a leaf at that size, because its leaves are bigger than
the part.

The answer: a painted leaf CARD canvas, one leaf in the part's own box, transparent
margin, drawn `Sliced` - the mechanism the flower's foliage already uses
(`Art/Flower/leaf.png`, `Tools/gen-flower-leaf-textures.py`, the rig's 16-card collage
stepping aside). A part 0.02-0.12 m gets the card; a part at or above 0.12 m keeps the
material, because at that size a window of the field holds nine or more leaves and reads
as the mass a crown is made of.

Why one square canvas (the aspect numbers, quoted from this cycle's gate)
------------------------------------------------------------------------
`Art1Check` INFO `oak-leaf-aspect`, the oak rig as the gallery builds it:

    <0.02 m     n=1171  min 0.33  p25 0.75  med 1.00  p75 1.00  max 2.59
    0.02-0.05 m n= 234  min 0.56  p25 0.88  med 1.00  p75 1.33  max 2.22
    0.05-0.10 m n=  14  min 0.72  p25 0.72  med 1.41  p75 2.00  max 2.23

The boxes are SQUARE at the median in both bands a card is drawn in, so the canvas is
square and the leaf paints itself inside it with its own margin. The tails distort the
leaf - 2.2x wide on a 2.2 box, 1.6x narrow on a 0.6 box - and that is recorded, not
hidden: at 8-45 px on screen the silhouette, not the leaf's exact proportions, is what
the eye gets, and the flower's family split bought 1.36x for twelve hand-tuned kinds
while these are 248 rig cards whose aspect IS the leaf's own drawing angle.

Run from the project root:
    python Tools/gen-tree-leaf-textures.py            # repaint + verify
    python Tools/gen-tree-leaf-textures.py --sheet    # ... and write the contact sheet

Writes `Assets/Resources/Art/Tree/leaf_card.png` and its `.meta` (the project's own
Single-sprite shape, `isReadable: 1`, `alphaIsTransparency: 1`, `spriteMeshType: 0`
FullRect for the Sliced draw mode, no fallback physics shape, guid reused when the file
exists so a repaint never churns an import identity). Deterministic: every wobble comes
from `random.Random(<fixed seed>)` and no timestamp is written, so a second run
reproduces the same bytes.
"""

import math
import os
import random
import re

import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Art", "Tree")
META_TEMPLATE = os.path.join(ROOT, "Assets", "LegacyArt", "Apple", "fruit.png.meta")
SHEET = os.path.join(ROOT, "Trials", "Runs", "art0-leafcard-sheet.png")

# The level's own leaf language (`Tools/gen-level-textures.py`, the painted `leaf`
# material): the crown and the ground must not be two different drawings. Copied, not
# imported - that file is a script and cannot be imported by name.
LEAF_SHADOW = (28, 59, 3)
LEAF_TONES = ((44, 98, 3), (61, 128, 4), (78, 159, 5), (94, 188, 6))
LEAF_EDGE = (35, 76, 2)
LEAF_VEIN = (116, 214, 9)

CANVAS = 128          # px, square: the rig's leaf cards are square at the median aspect
SUPER = 4             # supersample factor; the box downscale is the soft cut face
LEAF_LEN = 0.88       # of the canvas width, tip to base
LEAF_WID = 0.62       # of the canvas height, widest point
PPU = 1024.0          # the material's own scale (512 px per 0.5 m), so nothing is rescaled


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def leaf_halfwidth(t, wid):
    """The level's own ovate profile (`gen-level-textures.py`'s `_leaf_sprite`), so the
    card's leaf and the tile's leaves are the same shape."""
    return wid * 0.5 * (math.sin(math.pi * t) ** 0.62) * (1.0 - 0.18 * t)


def paint_leaf_card(size=CANVAS, seed=20260920):
    """One ovate leaf on transparency, drawn in FLAT tone bands (the painted language:
    the log canvases carry 11 flat tones, the flame's ramp is the only gradient).

    Light comes from the upper left, as everywhere else in this project's flat art:
    the upper third is the lit tone, the lower third the shaded one, the middle the
    body tone. The rim and the veins are drawn at partial alpha on purpose - a
    contrasty outline is what turns a leaf into a vector sticker (the 19.09 read of
    the first painted leaf material), and painted-parts.mdc wants a soft cut face.
    """
    rng = random.Random(seed)
    px = size * SUPER
    img = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    pad = int(px * 0.06)
    length = (px - 2 * pad) * LEAF_LEN
    wid = px * LEAF_WID
    cy = px * 0.5
    x0 = pad + (px - 2 * pad - length) * 0.5

    body = LEAF_TONES[2]
    lit = LEAF_TONES[3]
    dark = LEAF_TONES[1]
    darkest = LEAF_TONES[0]

    top, bot = [], []
    for i in range(int(length) + 1):
        t = i / float(max(1, int(length)))
        half = leaf_halfwidth(t, wid)
        top.append((x0 + i, cy - half))
        bot.append((x0 + i, cy + half))

    # body, then the lit upper band and the shaded lower band, each a flat tone and each
    # clipped by the same profile so the silhouette stays one shape.
    d.polygon(top + bot[::-1], fill=body + (255,))
    for band, tone, side in ((0.34, lit, -1.0), (0.30, dark, 1.0)):
        band_top, band_bot = [], []
        for i in range(int(length) + 1):
            t = i / float(max(1, int(length)))
            half = leaf_halfwidth(t, wid)
            outer = cy + side * half
            inner = cy + side * half * (1.0 - band)
            band_top.append((x0 + i, min(outer, inner)))
            band_bot.append((x0 + i, max(outer, inner)))
        d.polygon(band_top + band_bot[::-1], fill=tone + (255,))

    # the midrib, five side veins, and a soft rim on the cut face
    d.line([(x0, cy), (x0 + length, cy)], fill=LEAF_VEIN + (150,), width=max(1, SUPER - 1))
    for k in range(5):
        t = 0.16 + 0.17 * k
        x = x0 + length * t
        v = leaf_halfwidth(t, wid) * 0.62 * (1.0 - 0.12 * k)
        for sign in (-1.0, 1.0):
            d.line([(x, cy), (x + v * 0.95, cy + sign * v * 0.72)],
                   fill=LEAF_VEIN + (110,), width=1)
    d.line(bot, fill=LEAF_EDGE + (110,), width=max(1, SUPER - 1))
    # a one-step darker shadow under the leaf's own belly: without it a flat leaf reads
    # as a decal of the sky rather than a leaf lying on the branch
    for i in range(int(length) + 1):
        t = i / float(max(1, int(length)))
        half = leaf_halfwidth(t, wid)
        d.line([(x0 + i, cy + half * 0.985), (x0 + i, cy + half)],
               fill=darkest + (255,), width=1)

    small = img.resize((size, size), Image.BOX)
    rng.random()   # keep the seed live: a later edit that draws wobble stays deterministic
    return small


def write_meta(png_path, pixels_per_unit, readable, alpha, guid):
    """Copy the project's own Single-sprite .meta shape and set the fields the loader needs."""
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
    set_line("spriteGenerateFallbackPhysicsShape", 0)
    set_line("textureType", 8)                           # Sprite
    set_line("spriteMode", 1)                            # Single
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


def report(img, cards):
    """The verification table: what the canvas is, and what it draws as on the parts the
    gate says carry it (the bands are quoted from `Art1Check` INFO `oak-leaf`)."""
    a = np.asarray(img.convert("RGBA")).astype(np.float64)
    ink = a[:, :, 3] > 8
    cover = ink.mean()
    tones = len({tuple(int(v) for v in row) for row in a[:, :, :3][ink]})
    print("ART-0-LEAF-CARD canvas=%dx%d px ppu=%g (%.3f m natural) bytes=%d"
          % (img.width, img.height, PPU, img.width / PPU, os.path.getsize(cards)))
    print("ART-0-LEAF-CARD ink coverage=%.3f of the box, opaque share=%.3f, distinct tones=%d"
          % (cover, (a[:, :, 3] > 250).mean(), tones))
    print("ART-0-LEAF-CARD each card's drawn leaf, at the two lenses the game shows:")
    for box, lens, ppm in ((0.02, "tree close", 414.21), (0.05, "tree close", 414.21),
                           (0.10, "tree close", 414.21), (0.05, "yard", 117.4)):
        side = box * ppm
        print("    card %.2f m -> %.1f px square (%s lens), leaf %.1f x %.1f px"
              % (box, side, lens, side * LEAF_LEN, side * LEAF_WID))


def sheet(img):
    """The card at the three sizes the oak's own leaves take, on the yard's soil and on the
    game's background grey, so the eye can judge the silhouette - not the 512 px canvas."""
    tiles = []
    for box, ppm in ((0.02, 414.21), (0.04, 414.21), (0.08, 414.21), (0.12, 414.21),
                     (0.04, 117.4), (0.08, 117.4)):
        side = max(4, int(round(box * ppm)))
        tiles.append(img.resize((side, side), Image.BOX))
    w = sum(t.width for t in tiles) + 8 * (len(tiles) + 1)
    h = max(t.height for t in tiles) + 16
    out = Image.new("RGB", (w, h), (60, 60, 66))
    x = 8
    for t in tiles:
        out.paste(t, (x, (h - t.height) // 2), t)
        x += t.width + 8
    os.makedirs(os.path.dirname(SHEET), exist_ok=True)
    out.resize((out.width * 3, out.height * 3), Image.NEAREST).save(SHEET)
    print("ART-0-LEAF-CARD sheet -> " + SHEET)


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    img = paint_leaf_card()
    path = os.path.join(OUT_DIR, "leaf_card.png")
    img.save(path, "PNG", optimize=True)
    guid = existing_guid(path) or new_guid(random.Random(11))
    write_meta(path, int(PPU), True, True, guid)
    report(img, path)
    if "--sheet" in os.sys.argv:
        sheet(img)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
