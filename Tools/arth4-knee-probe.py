#!/usr/bin/env python3
"""ART-H4 probe (art lane, task "knee arc"): find the joint markers in a trial still and
measure the CIRCULAR OVERLAP SEAM - a ring of tone steps at a fixed radius around a joint.

Calibration: every hinge carries a dark circle marker of jointMarkerDiameter = 0.03 m
(Assets/HumanSegment.cs), so the measured marker diameter in px gives px/m - no camera
constant has to be guessed.

Usage:
  python Tools/arth4-knee-probe.py <still.png> [--seed x,y] [--blobs] [--crop x0,y0,x1,y1]
Output: marker list + px/m, an ASCII luminance map around the chosen joint, and the ring
profile (mean |tone step| per radius + how many of 72 angles carry a step > 40).
"""
import argparse
import math
import sys

import numpy as np
from PIL import Image

MARKER_M = 0.03


def label(dark_pts):
    """4-connected component labelling over the set of dark pixels only."""
    pts = set(dark_pts)
    comps = []
    while pts:
        seed = pts.pop()
        stack = [seed]
        comp = [seed]
        while stack:
            cy, cx = stack.pop()
            for nb in ((cy - 1, cx), (cy + 1, cx), (cy, cx - 1), (cy, cx + 1)):
                if nb in pts:
                    pts.discard(nb)
                    stack.append(nb)
                    comp.append(nb)
        comps.append(comp)
    return comps


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("still")
    ap.add_argument("--seed", default=None, help="x,y - pick the marker nearest this point")
    ap.add_argument("--max-r", type=int, default=34)
    ap.add_argument("--map", type=int, default=0, help="px per char: coarse map of the whole still")
    ap.add_argument("--local-step", type=int, default=1, help="px per char in the joint's ASCII map")
    ap.add_argument("--ppm", type=float, default=0.0, help="px per metre override (known-scale frames)")
    ap.add_argument("--lum-thresh", type=float, default=120.0, help="dark threshold for blobs")
    ap.add_argument("--no-ring", action="store_true")
    args = ap.parse_args()

    im = Image.open(args.still).convert("RGB")
    a = np.asarray(im).astype(np.float32)
    lum = 0.2126 * a[:, :, 0] + 0.7152 * a[:, :, 1] + 0.0722 * a[:, :, 2]
    h, w = lum.shape
    ys, xs = np.where(lum < args.lum_thresh)
    dark_pts = list(zip(ys.tolist(), xs.tolist()))
    print("dark px=%d (lum < %.0f)" % (len(dark_pts), args.lum_thresh))
    comps = label(dark_pts)
    blobs = []
    for comp in comps:
        ay = np.array([p[0] for p in comp]); ax = np.array([p[1] for p in comp])
        area = ax.size
        if area < 12 or area > 900:
            continue
        bw = ax.max() - ax.min() + 1
        bh = ay.max() - ay.min() + 1
        if max(bw, bh) > 2.2 * min(bw, bh):
            continue
        blobs.append((ax.mean(), ay.mean(), bw, bh, area))
    blobs.sort(key=lambda b: -b[4])
    dia = float(np.median([(b[2] + b[3]) / 2.0 for b in blobs])) if blobs else 0.0
    ppm = args.ppm if args.ppm > 0 else (dia / MARKER_M if dia > 0 else 0.0)
    print("blobs=%d  median dia=%.2f px -> %.1f px/m (marker %.3f m)  img=%dx%d"
          % (len(blobs), dia, ppm, MARKER_M, w, h))
    for b in blobs[:16]:
        print("  blob x=%.1f y=%.1f  w=%d h=%d  area=%d  (%.4f m dia)"
              % (b[0], b[1], b[2], b[3], b[4], ((b[2] + b[3]) / 2.0) / ppm if ppm else 0))

    if not blobs:
        return 2

    if args.map:
        step = args.map
        ramp = "@%#*+=-:."
        marks = [(int(b[1]) // step, int(b[0]) // step) for b in blobs
                 if abs(b[2] - dia) <= 2 and abs(b[3] - dia) <= 2]
        print("coarse map, %d px/char (M = a %.1f-px marker blob):" % (step, dia))
        for y in range(0, h - step + 1, step):
            line = []
            for x in range(0, w - step + 1, step):
                v = float(lum[y:y + step, x:x + step].mean())
                k = int(np.clip(v / 255.0 * (len(ramp) - 1), 0, len(ramp) - 1))
                line.append(ramp[k])
            print("   " + "".join(line))
        for my, mx in marks:
            print("   marker blob near char col %d row %d -> px (%d, %d)"
                  % (mx, my, mx * step, my * step))
        if args.seed is None and args.no_ring:
            return 0

    if args.seed:
        sx, sy = [float(v) for v in args.seed.split(",")]
        if blobs:
            best = min(blobs, key=lambda b: (b[0] - sx) ** 2 + (b[1] - sy) ** 2)
            cx, cy = best[0], best[1]
        else:
            print("no marker blobs - using the seed straight")
            cx, cy = sx, sy
    elif blobs:
        cx, cy = blobs[0][0], blobs[0][1]
    else:
        print("no marker blobs and no seed - nothing to measure")
        return 2
    print("joint at (%.1f, %.1f) px" % (cx, cy))
    if args.no_ring:
        return 0

    r = args.max_r
    x0, x1 = max(0, int(cx) - r), min(w - 1, int(cx) + r)
    y0, y1 = max(0, int(cy) - r), min(h - 1, int(cy) + r)
    patch = lum[y0:y1 + 1, x0:x1 + 1]

    ramp = "@%#*+=-:."
    st = max(1, args.local_step)
    print("ASCII map cols %d..%d, rows %d..%d, %d px/char ('@' dark, '.' bright):"
          % (x0, x1, y0, y1, st))
    for row in range(0, patch.shape[0] - st + 1, st):
        line = []
        for col in range(0, patch.shape[1] - st + 1, st):
            v = float(patch[row:row + st, col:col + st].mean())
            k = int(np.clip(v / 255.0 * (len(ramp) - 1), 0, len(ramp) - 1))
            line.append(ramp[k])
        print("   " + "".join(line))

    print("ring profile: radius px | mean tone-step | angles>40 | mm radius")
    for rr in range(3, r + 1):
        steps = []
        ang_hi = 0
        for k in range(72):
            th = 2.0 * math.pi * k / 72.0
            xi = int(round(cx + rr * math.cos(th)))
            yi = int(round(cy + rr * math.sin(th)))
            xin = int(round(cx + (rr - 1.4) * math.cos(th)))
            yin = int(round(cy + (rr - 1.4) * math.sin(th)))
            xo = int(round(cx + (rr + 1.4) * math.cos(th)))
            yo = int(round(cy + (rr + 1.4) * math.sin(th)))
            if not (0 <= xin < w and 0 <= xo < w and 0 <= xi < w):
                continue
            if not (0 <= yin < h and 0 <= yo < h and 0 <= yi < h):
                continue
            s = abs(lum[yo, xo] - lum[yin, xin])
            steps.append(s)
            if s > 40:
                ang_hi += 1
        if not steps:
            continue
        m = float(np.mean(steps))
        print("   %2d px | %6.1f | %2d/72 | %5.1f mm" % (rr, m, ang_hi, rr / ppm * 1000 if ppm else 0))
    return 0


if __name__ == "__main__":
    sys.exit(main())
