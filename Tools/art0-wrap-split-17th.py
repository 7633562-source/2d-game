"""ART-0 (17th pass) step 4: is `bark`'s wrap a RAMP (fixable, as skin/fabric were) or DETAIL?

`Tools/gen-level-textures.py:level_tile` cancels a brightness ramp by adding one constant per row and
one per column, and it fires only when `cut > 1.5` AND `cut > 2x inner`. On today's bytes `bark` is the
last material above the grain-and-capable band (cut X 27.36 / Y 22.71, ratio 1.44x / 1.57x) and it did
NOT fire there, so the tile is unchanged: the card's item-5 eye note ("a 2x2 repeat of skin, fabric,
bark and leaf shows a light cross through the boundary") was never re-read for bark, because skin and
fabric were fixed by the ramp correction and leaf by painting.

Which it is decides the fix, so this probe splits the wrap step into its two parts, per axis:

  ramp  = |mean of the first edge line - mean of the last edge line|  (what level_tile removes)
  detail= the spread of the per-pixel difference once that constant is removed (what only a blend
          or a repaint can touch)

It also reports the share of boundary pixels stepping more than 12 levels - the criterion that called
`skin` a defect (94.6 % on skin, and a fixed skin now measures 5.64 vs its 4.54 grain).
"""
import os
import sys

import numpy as np
from PIL import Image

ART = r"E:/Games/2D Game/My project/Assets/Resources/Art"
MATERIALS = ["skin", "fabric", "denim", "dirt", "grass", "bark", "leaf"]
DEFECT_LEVEL = 12.0


def axis_report(a, axis, name):
    first = a.take(0, axis=axis).astype(np.float64)
    last = a.take(-1, axis=axis).astype(np.float64)
    diff = np.abs(first - last)
    ramp = np.abs(first.mean(axis=0) - last.mean(axis=0))          # per channel
    resid = diff - ramp[None, :]
    inner = np.abs(np.diff(a.astype(np.float64), axis=axis))
    share = float((diff.max(axis=1) > DEFECT_LEVEL).mean()) * 100.0
    return {
        "name": name,
        "cut": float(diff.mean()),
        "ramp": float(ramp.mean()),
        "resid": float(np.abs(resid).mean()),
        "inner": float(inner.mean()),
        "p95": float(np.percentile(diff.max(axis=1), 95)),
        "share12": share,
    }


def main():
    print("%-8s %-3s %8s %8s %8s %8s %8s %8s" %
          ("key", "ax", "cut", "ramp", "resid", "inner", "p95", ">12lv%"))
    for key in MATERIALS:
        a = np.asarray(Image.open(os.path.join(ART, key + ".png")).convert("RGB"))
        for axis, name in ((1, "X"), (0, "Y")):
            r = axis_report(a, axis, name)
            print("%-8s %-3s %8.2f %8.2f %8.2f %8.2f %8.1f %8.1f"
                  % (key, r["name"], r["cut"], r["ramp"], r["resid"], r["inner"], r["p95"], r["share12"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
