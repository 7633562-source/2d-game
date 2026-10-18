"""ART-0 28th pass - is the shipped leaf tile the pixels the pass measured?

The generator writes through its own `save()` (which also owns the .meta guid bookkeeping), the
sweep wrote through PIL's default, so the file hashes differ. This compares the PIXELS, so the
numbers quoted for the sweep tile are the numbers of the shipped bytes.

Run: python Tools/_art0-leaf-pixel-equal.py A.png B.png
"""
import sys

import numpy as np
from PIL import Image

a = np.asarray(Image.open(sys.argv[1]).convert("RGB")).astype(int)
b = np.asarray(Image.open(sys.argv[2]).convert("RGB")).astype(int)
print("A %s %s\nB %s %s" % (sys.argv[1], a.shape, sys.argv[2], b.shape))
if a.shape != b.shape:
    raise SystemExit("FAIL different shapes")
d = np.abs(a - b)
print("PIXEL-EQUAL best=%d worst=%d mean=%.4f  -> %s"
      % (d.max(), d.max(), d.mean(), "IDENTICAL" if d.max() == 0 else "DIFFERENT"))
