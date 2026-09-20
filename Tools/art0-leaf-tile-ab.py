"""ART-0 28th pass - a blind pairwise panel of two leaf tiles, each drawn 2 x 2.

The gallery's material row shows a 1 x 1 m swatch of a texture dressed at a 0.5 m period,
i.e. exactly a 2 x 2 repeat - the hardest case for a tiling material and the one the reads
judged. This builds that swatch for two candidate tiles, side by side, with the sides chosen
by a seed, and writes a map file next to the panel. The map is opened AFTER the read.

Run: python Tools/art0-leaf-tile-ab.py LEFT.png RIGHT.png OUT.png SEED [tag]
"""
import pathlib
import random
import sys

from PIL import Image, ImageDraw

ROOT = pathlib.Path(__file__).resolve().parent.parent
SWATCH = 512        # one tile on screen: a 1 x 1 m swatch of a 0.5 m material is 2 x 2 tiles
GAP = 24
BAR = 16


def swatch(path):
    tile = Image.open(path).convert("RGB").resize((SWATCH, SWATCH), Image.LANCZOS)
    out = Image.new("RGB", (SWATCH * 2, SWATCH * 2))
    for ty in range(2):
        for tx in range(2):
            out.paste(tile, (tx * SWATCH, ty * SWATCH))
    return out


def main():
    a, b, out_path, seed = sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4])
    tag = sys.argv[5] if len(sys.argv) > 5 else ""
    rng = random.Random(seed)
    left_is_a = rng.random() < 0.5
    left, right = (a, b) if left_is_a else (b, a)
    la, ra = swatch(left), swatch(right)
    h = max(la.height, ra.height)
    panel = Image.new("RGB", (la.width + GAP + ra.width, h), (24, 24, 24))
    panel.paste(la, (0, 0))
    panel.paste(ra, (la.width + GAP, 0))
    d = ImageDraw.Draw(panel)
    d.rectangle([0, 0, 8, BAR], fill=(255, 255, 255))
    d.rectangle([la.width + GAP, 0, la.width + GAP + 8, BAR], fill=(255, 255, 255))
    d.rectangle([la.width, 0, la.width + GAP - 1, h], fill=(255, 255, 255))
    panel.save(out_path)
    print("PANEL %s  %dx%d  seed %d" % (out_path, panel.width, panel.height, seed))
    print("  left  = %s" % pathlib.Path(left).name)
    print("  right = %s" % pathlib.Path(right).name)
    if tag:
        mapfile = pathlib.Path(out_path).with_suffix("").as_posix() + "-map.txt"
        with open(mapfile, "w", encoding="utf-8") as fh:
            fh.write("left  = %s\nright = %s\nseed = %d\ntag = %s\n"
                     % (left, right, seed, tag))
        print("  map written after the read: %s" % mapfile)
    return 0


if __name__ == "__main__":
    sys.exit(main())
