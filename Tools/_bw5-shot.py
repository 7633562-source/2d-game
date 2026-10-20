"""B-WING-5: downscale one take shot for a quick look (temp helper).

Usage: python Tools/_bw5-shot.py <label> <n> <out.png> [x0 y0 x1 y1]
"""
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")


def main():
    label, n, out = sys.argv[1], int(sys.argv[2]), sys.argv[3]
    im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, n))).convert("RGB")
    if len(sys.argv) > 7:
        x0, y0, x1, y1 = (int(v) for v in sys.argv[4:8])
        im = im.crop((x0, y0, x1, y1))
    else:
        im = im.resize((im.width // 2, im.height // 2), Image.LANCZOS)
    im.save(out)
    print("wrote %s %s" % (out, im.size))


if __name__ == "__main__":
    main()
