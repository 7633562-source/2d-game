#!/usr/bin/env python3
"""ART-H4 instrument: WHERE the seam line is (offline joint frames with alpha).

Reads a joint frame composed by Tools/art-h1-pose-frames.py or Tools/arth3-seam.py and, inside
the joint's central disc (opaque px only), reports the strongest vertical luminance steps -
position, distance from the pivot, time the two colours - plus a coarse map of where the steps
are.  The H3 seam-step number says HOW strong the line is; this says WHAT paints it.

Usage: python Tools/arth4-seam-where.py <frame.png> [--disc-mm 44] [--scale 1000] [--top 12]
"""
import argparse
import sys

import numpy as np
from PIL import Image


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("frame")
    ap.add_argument("--scale", type=float, default=1000.0, help="px per metre of the frame")
    ap.add_argument("--disc-mm", type=float, default=44.0, help="the disc radius to read (mm)")
    ap.add_argument("--top", type=int, default=12)
    args = ap.parse_args()

    im = Image.open(args.frame).convert("RGBA")
    a = np.asarray(im).astype(np.float64)
    al = a[:, :, 3]
    lum = 0.2126 * a[:, :, 0] + 0.7152 * a[:, :, 1] + 0.0722 * a[:, :, 2]
    h, w = al.shape
    cy, cx = h // 2, w // 2
    r_px = args.disc_mm / 1000.0 * args.scale
    yy, xx = np.mgrid[0:h, 0:w]
    disc = ((xx + 0.5 - cx) ** 2 + (yy + 0.5 - cy) ** 2) <= r_px * r_px
    ok = disc & (al >= 200)
    pair = ok[:-1, :] & ok[1:, :]
    d = np.abs(lum[1:, :] - lum[:-1, :])
    d[~pair] = 0.0
    flat = np.argsort(d.ravel())[::-1][:args.top]
    print("frame %s  %dx%d  disc %.0f mm (%.0f px)  opaque px in disc=%d"
          % (args.frame, w, h, args.disc_mm, r_px, int(ok.sum())))
    print("top vertical steps (px alpha>=200 both sides), pivot = %d,%d:" % (cx, cy))
    for fl in flat:
        y, x = divmod(int(fl), w)
        if d[y, x] <= 0:
            break
        dist = ((x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2) ** 0.5
        c1 = a[y, x, :3].astype(int)
        c2 = a[y + 1, x, :3].astype(int)
        print("   dL=%6.1f at (%4d,%4d) r=%5.1f mm  above rgb%s  below rgb%s"
              % (d[y, x], x, y, dist / args.scale * 1000, tuple(c1), tuple(c2)))

    # map: char per 4 px of the disc region, strongest step inside each cell
    step = 4
    ramp = " .:-=+*#%@"
    print("step map, %d px/char (' ' none, '@' > 120), cols %d..%d rows %d..%d:"
          % (step, max(0, cx - int(r_px)), min(w, cx + int(r_px)),
             max(0, cy - int(r_px)), min(h, cy + int(r_px))))
    for y in range(max(0, cy - int(r_px)), min(h - step, cy + int(r_px)), step):
        line = []
        for x in range(max(0, cx - int(r_px)), min(w - step, cx + int(r_px)), step):
            v = float(d[y:y + step, x:x + step].max())
            k = int(np.clip(v / 120.0 * (len(ramp) - 1), 0, len(ramp) - 1))
            line.append(ramp[k])
        print("   " + "".join(line))
    return 0


if __name__ == "__main__":
    sys.exit(main())
