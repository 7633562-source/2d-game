"""ART-0 (17th pass) step 5: the 2x2 repeat sheet for the remaining photo tiles, in a RANDOM order.

The card's item 5 was closed for `skin` and `fabric` (ramp levelling) and for `leaf` (painted), and
`bark`, `denim`, `dirt`, `grass` were left with "a cut no worse than their own grain by the numbers".
That was a ratio argument; the only eye evidence on those four (the item-5 note "a 2x2 repeat of skin,
fabric, bark and leaf shows a light cross through the boundary") predates every fix and was never
re-read.

So this writes one image per material: a 2x2 repeat of the shipped 512 at NATIVE size, which is the
scale the game shows a material at (a 512 px tile covers `ArtParts.*TileMeters` = 0.5 m, i.e. 1024
px/m; the oak close-up lens is 414 px/m, so native is the generous case). `skin` (known fixed) and
`leaf` (known painted) ride along as controls.

The order is shuffled and written to `map.txt`, which must NOT be opened before the read. Each panel
is its own file and carries no label inside the picture, so a read cannot leak the mapping.
"""
import os
import random
import sys

from PIL import Image

ART = r"E:/Games/2D Game/My project/Assets/Resources/Art"
OUT = r"E:/Games/2D Game/My project/Trials/Runs/art-0-wrap-17th"
KEYS = ["skin", "fabric", "bark", "denim", "dirt", "grass", "leaf"]
REPEAT = 2


def panel(path, repeat=REPEAT):
    tile = Image.open(path).convert("RGB")
    w, h = tile.size
    out = Image.new("RGB", (w * repeat, h * repeat))
    for i in range(repeat):
        for j in range(repeat):
            out.paste(tile, (i * w, j * h))
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    keys = list(KEYS)
    rng = random.Random(20260919)
    rng.shuffle(keys)
    lines = []
    for i, key in enumerate(keys):
        letter = "ABCDEFG"[i]
        img = panel(os.path.join(ART, key + ".png"))
        name = "panel_%s.png" % letter
        img.save(os.path.join(OUT, name), "PNG", optimize=True)
        lines.append("%s %s %dx%d" % (letter, key, img.size[0], img.size[1]))
        print("wrote %s  %dx%d" % (name, img.size[0], img.size[1]))
    with open(os.path.join(OUT, "map.txt"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write("ART-0 17th pass, 2x2 repeat panels at native 512 (DO NOT READ BEFORE THE PICK)\n")
        for line in lines:
            fh.write(line + "\n")
    print("map written to %s/map.txt (do not open before the read)" % OUT)
    return 0


if __name__ == "__main__":
    sys.exit(main())
