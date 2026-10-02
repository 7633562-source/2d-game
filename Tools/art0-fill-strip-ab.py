"""ART-0 (18th pass): the blind pairwise sheet for the ground_fill repeat.

ground_fill is 8 x 2 m at 1024 x 256 and StaticPlatform draws it `Tiled` whenever a slab is wider
than one tile, so the 32 m "Behind" plate shows the same pixels four times. The question this sheet
asks is the only one an eye can answer: on the wide slab, does one of these two fills read as a
REPEAT more than the other?

One image per case, HEAD bytes on one side and the current bytes on the other, the side random and
written to `map.txt` (opened after the pick). Cases:

  wide_x2    the shipping wide plate: 4 repeats at 32 px/m (8 m tile = 256 px), shown at 2x so the
             reader is not fighting the downscale.
  zoom_x4    the same collision at 128 px/m - two repeats of the tile at 1:1, i.e. what a plateau
             edge or a 16 m slab shows.
  control    HEAD against HEAD (identical bytes): a calibration pair. If the reader picks a side
             here, its verdict on the real pair carries no weight.

Usage: python Tools/art0-fill-strip-ab.py [label]      (label defaults to art-0-fill-18th)
"""
import os
import random
import subprocess
import sys

from PIL import Image

ROOT = r"E:/Games/2D Game/My project"
LEVEL = os.path.join(ROOT, "Assets", "Resources", "Level")
GAP = 8
NEUTRAL = (110, 110, 110)


def head_bytes(key):
    rel = "Assets/Resources/Level/%s.png" % key
    dest = os.path.join(OUT, "_head_%s.png" % key)
    os.makedirs(OUT, exist_ok=True)
    with open(dest, "wb") as fh:
        subprocess.run(["git", "show", "HEAD:%s" % rel], cwd=ROOT, stdout=fh, check=True)
    return dest


def plate(path, meters_wide, ppm, zoom):
    """The slab as the renderer draws it: `meters_wide` of a Tiled 8 x 2 m tile, at `ppm`."""
    tile = Image.open(path).convert("RGB")
    tw = int(round(8.0 * ppm))
    th = int(round(2.0 * ppm))
    small = tile.resize((tw, th), Image.LANCZOS)
    n = int(round(meters_wide / 8.0))
    strip = Image.new("RGB", (tw * n, th))
    for i in range(n):
        strip.paste(small, (i * tw, 0))
    if zoom != 1:
        strip = strip.resize((strip.size[0] * zoom, strip.size[1] * zoom), Image.NEAREST)
    return strip


def pair_image(left, right):
    w = max(left.size[0], right.size[0])
    h = max(left.size[1], right.size[1])
    out = Image.new("RGB", (w * 2 + GAP, h), NEUTRAL)
    out.paste(left, (0, 0))
    out.paste(right, (w + GAP, 0))
    return out


def main():
    global OUT
    label = sys.argv[1] if len(sys.argv) > 1 else "art-0-fill-18th"
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 20260921
    OUT = os.path.join(ROOT, "Trials", "Runs", label)
    os.makedirs(OUT, exist_ok=True)
    old = head_bytes("ground_fill")
    new = os.path.join(LEVEL, "ground_fill.png")
    same = open(old, "rb").read() == open(new, "rb").read()

    rng = random.Random(seed)
    cases = [
        ("wide_x2", 32.0, 32.0, 2, (old, new)),
        ("zoom_x4", 16.0, 128.0, 1, (old, new)),
        ("control", 32.0, 32.0, 2, (old, old)),
    ]
    lines = []
    for name, meters, ppm, zoom, (a, b) in cases:
        sides = [a, b]
        rng.shuffle(sides)
        left, right = sides
        img = pair_image(plate(left, meters, ppm, zoom), plate(right, meters, ppm, zoom))
        path = os.path.join(OUT, "pair_%s.png" % name)
        img.save(path, "PNG", optimize=True)
        side_old = "LEFT" if left == a else "RIGHT"
        lines.append("%-9s %-18s %dx%d  HEAD=%s  NEW=%s" % (
            name, os.path.basename(path), img.size[0], img.size[1], side_old,
            "RIGHT" if side_old == "LEFT" else "LEFT"))
        print("wrote %s %dx%d (mapping withheld)" % (path, img.size[0], img.size[1]))
    with open(os.path.join(OUT, "map.txt"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write("ART-0 18th pass ground_fill pairwise sheet (DO NOT READ BEFORE THE PICK)\n")
        fh.write("HEAD ground_fill == current ground_fill: %s\n" % ("YES (control only)" if same else "NO"))
        for line in lines:
            fh.write(line + "\n")
    print("map: %s/map.txt (do not open before the read)" % OUT)
    return 0


if __name__ == "__main__":
    sys.exit(main())
