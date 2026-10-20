"""ART-0 (17th pass): the paired blind sheet - old bytes vs new bytes, one shuffle, map read last.

Six panels, each a 2 x 2 native repeat: `skin` and `fabric` at HEAD (before this pass) and at the
working tree (after the wrap band), plus `bark` and `grass` as controls that must read the same as
before. The order is shuffled into `map.txt`, which is opened only after the pick. A reader that can
find a joint at all should name the two HEAD panels and leave the two new ones alone; if it names a
new panel instead, the band did not work or made things worse.

Usage: python Tools/art0-wrap-ab-17th.py
"""
import os
import random
import subprocess
import sys

from PIL import Image

ROOT = r"E:/Games/2D Game/My project"
ART = os.path.join(ROOT, "Assets", "Resources", "Art")
OUT = os.path.join(ROOT, "Trials", "Runs", "art-0-wrap-17th-ab")
OLD = os.path.join(OUT, "_head")
REPEAT = 2


def head_bytes(key, dest_dir):
    """The committed bytes of a shipped picture, straight out of git."""
    rel = "Assets/Resources/Art/%s.png" % key
    dest = os.path.join(dest_dir, key + ".png")
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


def main():
    os.makedirs(OLD, exist_ok=True)
    items = []
    for key in ("skin", "fabric"):
        items.append(("head " + key, head_bytes(key, OLD)))
        items.append(("new " + key, os.path.join(ART, key + ".png")))
    for key in ("bark", "grass"):
        items.append(("control " + key, os.path.join(ART, key + ".png")))

    rng = random.Random(20260920)
    rng.shuffle(items)
    lines = []
    for i, (name, path) in enumerate(items):
        letter = "ABCDEF"[i]
        img = panel(path)
        img.save(os.path.join(OUT, "panel_%s.png" % letter), "PNG", optimize=True)
        lines.append("%s %s %dx%d" % (letter, name, img.size[0], img.size[1]))
        print("wrote panel_%s.png %dx%d" % (letter, img.size[0], img.size[1]))
    with open(os.path.join(OUT, "map.txt"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write("ART-0 17th pass paired sheet (DO NOT READ BEFORE THE PICK)\n")
        for line in lines:
            fh.write(line + "\n")
    print("map: %s/map.txt (do not open before the read)" % OUT)
    return 0


if __name__ == "__main__":
    sys.exit(main())
