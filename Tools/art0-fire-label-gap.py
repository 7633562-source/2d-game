#!/usr/bin/env python3
"""ART-0 cycle: measure the gap between the FIRE row's label and the drawn fire.

A catalogue label must sit clear of the object it names and inside the frame. Both
are measurable on the captured still, so this measures them instead of asking a
reader: the fire is the warm ink (red over blue), the label is near-white low-chroma
ink, and the still's own json gives the lens scale (orthoSize -> px per metre).

Usage:  python Tools/art0-fire-label-gap.py <label>
"""
import json
import os
import sys

import numpy as np
from PIL import Image

PROJ = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJ, "Trials", "Runs")


def main(label):
    still = os.path.join(RUNS, "%s_fire.png" % label)
    meta = os.path.join(RUNS, "%s-gallery.json" % label)
    img = np.asarray(Image.open(still).convert("RGB")).astype(np.int16)
    h, w, _ = img.shape

    ortho = None
    if os.path.exists(meta):
        data = json.load(open(meta, encoding="utf-8"))

        def walk(node):
            if isinstance(node, dict):
                if node.get("target") == "campfire flame core":
                    return node
                for v in node.values():
                    r = walk(v)
                    if r:
                        return r
            elif isinstance(node, list):
                for v in node:
                    r = walk(v)
                    if r:
                        return r
            return None

        fit = walk(data)
        if fit:
            ortho = fit.get("orthoSize")
    px_per_m = (h / (2.0 * ortho)) if ortho else None

    r, g, b = img[:, :, 0], img[:, :, 1], img[:, :, 2]
    # The flame is the bright warm ink; the soil is warm too but dark, so the
    # threshold has to carry a brightness floor as well as a red-over-blue one.
    warm = ((r - b) > 60) & (r > 150)
    white = (img.min(axis=2) > 190) & ((img.max(axis=2) - img.min(axis=2)) < 32)

    ys, xs = np.nonzero(warm)
    fire = (int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())) if xs.size else None
    print("still %s  %dx%d  ortho=%s  px/m=%s"
          % (os.path.basename(still), w, h, ortho, ("%.2f" % px_per_m) if px_per_m else "n/a"))
    print("  fire (bright warm ink) box x %d-%d y %d-%d  px=%d"
          % (fire[0], fire[2], fire[1], fire[3], int(warm.sum())))

    above = white.copy()
    above[fire[1]:, :] = False
    ay, ax = np.nonzero(above)
    if ax.size == 0:
        print("  label: no near-white ink above the fire")
    else:
        print("  label above the fire: box x %d-%d y %d-%d  px=%d"
              % (int(ax.min()), int(ax.max()), int(ay.min()), int(ay.max()), int(ax.size)))
        gap = fire[1] - int(ay.max())
        print("  vertical gap label-bottom -> fire-top: %d px%s"
              % (gap, ("  = %.3f m" % (gap / px_per_m)) if px_per_m else ""))

    print("  label ink touching a frame edge: left %d right %d top %d bottom %d"
          % (int(white[:, 0].sum()), int(white[:, w - 1].sum()),
             int(white[0, :].sum()), int(white[h - 1, :].sum())))

    lw, rw = np.nonzero(white)
    if lw.size:
        print("  all near-white ink: x %d-%d y %d-%d (frame is 0..%d / 0..%d)"
              % (int(rw.min()), int(rw.max()), int(lw.min()), int(lw.max()), w - 1, h - 1))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else "art0firestack2"))
