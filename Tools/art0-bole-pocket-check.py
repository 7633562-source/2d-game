"""ART-0 31st pass: are the bole's pockets the BACKDROP, or just grey bark?

The hole probe (`Tools/art0-bole-hole-probe.py`) listed three tall pockets
inside the bole besides the 1 px base slit. Its backdrop test is a tolerance
(|r-g|<14, |g-b|<20, |r-b|<22), which dull grey-brown bark can also pass, so a
pocket is only a hole if its pixels are the backdrop's EXACT colour - the
gallery backdrop is flat (76, 82, 92) everywhere away from the tree.

Usage:  python Tools/art0-bole-pocket-check.py [still.png]
"""
import os
import sys
from collections import Counter

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STILL = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    ROOT, "Trials", "Runs", "art0leaf29_tree.png")

POCKETS = [
    ("base_slit", 867, 1045, 895, 1045),
    ("pocket_a", 901, 831, 903, 885),
    ("pocket_b", 860, 544, 866, 593),
    ("pocket_c", 859, 861, 861, 890),
]


def main():
    im = Image.open(STILL).convert("RGB")
    px = im.load()
    backdrop = px[50, 50]
    print("BACKDROP %s (sampled at 50,50 / 1800,60 / 960,20 / 300,900)"
          % (backdrop,))
    for (name, x0, y0, x1, y1) in POCKETS:
        hist = Counter()
        exact = 0
        total = 0
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                c = px[x, y]
                hist[c] += 1
                total += 1
                if c == backdrop:
                    exact += 1
        print("\n%s x %d..%d y %d..%d : %d px, %d exactly the backdrop (%.1f %%)"
              % (name, x0, x1, y0, y1, total, exact, 100.0 * exact / max(1, total)))
        for c, n in hist.most_common(6):
            print("   %s x%d%s" % (c, n, "   <-- BACKDROP" if c == backdrop else ""))


if __name__ == "__main__":
    main()
