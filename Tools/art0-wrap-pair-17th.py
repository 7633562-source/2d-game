"""ART-0 (17th pass): the pairwise read - old bytes against new bytes, side by side, positions random.

The lane's rule is that only the PREFERENCE between two panels is stable (a vision read's absolute
verdicts are not), so this is the decisive form: one image per material, two 2x2 native repeats side
by side separated by a neutral gap, the HEAD bytes on one side and this pass's bytes on the other,
which side is random and written to `map.txt` (opened after the pick).

Usage: python Tools/art0-wrap-pair-17th.py
"""
import os
import random
import subprocess
import sys

from PIL import Image

ROOT = r"E:/Games/2D Game/My project"
ART = os.path.join(ROOT, "Assets", "Resources", "Art")
OUT = os.path.join(ROOT, "Trials", "Runs", "art-0-wrap-17th-pair")
OLD = os.path.join(OUT, "_head")
GAP = 6
NEUTRAL = (128, 128, 128)
REPEAT = 2


def head_bytes(key):
    rel = "Assets/Resources/Art/%s.png" % key
    dest = os.path.join(OLD, key + ".png")
    with open(dest, "wb") as fh:
        subprocess.run(["git", "show", "HEAD:%s" % rel], cwd=ROOT, stdout=fh, check=True)
    return dest


def panel(path, repeat=REPEAT):
    tile = Image.open(path).convert("RGB")
    w, h = tile.size
    out = Image.new("RGB", (w * repeat, h * repeat))
    for i in range(repeat):
        for j in range(repeat):
            out.paste(tile, (i * w, j * h))
    return out


def pair_image(left, right):
    a, b = panel(left), panel(right)
    w, h = a.size
    out = Image.new("RGB", (w * 2 + GAP, h), NEUTRAL)
    out.paste(a, (0, 0))
    out.paste(b, (w + GAP, 0))
    return out


def main():
    os.makedirs(OLD, exist_ok=True)
    rng = random.Random(20260921)
    lines = []
    for key in ("skin", "fabric"):
        old, new = head_bytes(key), os.path.join(ART, key + ".png")
        left, right = (old, new) if rng.random() < 0.5 else (new, old)
        img = pair_image(left, right)
        name = "pair_%s.png" % key
        img.save(os.path.join(OUT, name), "PNG", optimize=True)
        side_old = "LEFT" if left == old else "RIGHT"
        lines.append("%s  %s  %dx%d  HEAD=%s NEW=%s" % (key, name, img.size[0], img.size[1],
                                                        side_old, "RIGHT" if side_old == "LEFT" else "LEFT"))
        print("wrote %s %dx%d (mapping withheld)" % (name, img.size[0], img.size[1]))
    with open(os.path.join(OUT, "map.txt"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write("ART-0 17th pass pairwise sheet (DO NOT READ BEFORE THE PICK)\n")
        for line in lines:
            fh.write(line + "\n")
    print("map: %s/map.txt (do not open before the read)" % OUT)
    return 0


if __name__ == "__main__":
    sys.exit(main())
