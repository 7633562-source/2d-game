"""ART-0 cycle: prove the kept "old" cap canvas is the shipped old bytes, not a lookalike.

The candidate sheets keep the previous tip band as `cap_S_3_9_1.png`. Before that canvas is used as
the "before" of a pixel comparison, it has to be shown that the painter still produces it - the same
band and `min_top` the shipped file had - bit for bit. This repaints it through the generator's own
`save()` (which is what wrote the shipped file, `optimize=True` and the same meta writer) into a
scratch path and prints the md5 next to the shipped md5 recorded before the repaint.

Usage:  python Tools/art0-cap-oldcheck.py <shippedMd5Before>
"""
import hashlib
import importlib.util
import os
import sys

PROJ = r"E:/Games/2D Game/My project"
GEN = os.path.join(PROJ, "Tools", "gen-level-textures.py")
SCRATCH = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Temp", "art0-cap-oldcheck")


def main(expected):
    os.makedirs(SCRATCH, exist_ok=True)
    spec = importlib.util.spec_from_file_location("genlevel", GEN)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    img = mod.paint_ground_cap(tip_lo=3, tip_hi=9, min_top=1)
    path = os.path.join(SCRATCH, "ground_cap_old.png")
    size = mod.save(img, path, 100, False, True, __import__("random").Random(1))
    md5 = hashlib.md5(open(path, "rb").read()).hexdigest()
    print("repainted band 3..9 min_top 1 -> %s  %d bytes  md5 %s" % (path, size, md5))
    print("shipped before the repaint     -> md5 %s" % expected)
    print("MATCH" if md5 == expected else "DIFFERENT - the kept canvas is not the shipped old bytes")
    return 0 if md5 == expected else 1


if __name__ == "__main__":
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    sys.exit(main(sys.argv[1]))
