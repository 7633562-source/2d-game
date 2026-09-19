"""ART-0 picture probe: diff two capture labels frame by frame and encode the pan.

Usage:  python Tools/art0-frame-diff.py <labelOld> <labelNew>

For every still both labels produced it reports the number of differing pixels,
the bounding box of the differences and the largest channel delta, so a cycle can
say "nothing moved" or "this region moved" instead of "looks the same".
It also prints where each label's pixels come from (build guid from the json) and
whether the pan frame sequence is complete.
"""
import json
import os
import subprocess
import sys

import numpy as np
from PIL import Image

PROJ = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJ, "Trials/Runs")


def load(label, name):
    p = os.path.join(RUNS, "%s_%s.png" % (label, name))
    if not os.path.exists(p):
        return None, p
    return np.asarray(Image.open(p).convert("RGB")).astype(np.int16), p


def diff(label_a, label_b):
    names = sorted({fn.split("_", 1)[1][:-4] for fn in os.listdir(RUNS)
                    if fn.startswith(label_a + "_") and fn.endswith(".png")})
    total = 0
    print("=== stills: %s vs %s ===" % (label_a, label_b))
    for name in names:
        a, pa = load(label_a, name)
        b, pb = load(label_b, name)
        if a is None or b is None:
            print("%-12s (only one label has it: %s / %s)" % (name, os.path.exists(pa), os.path.exists(pb)))
            continue
        if a.shape != b.shape:
            print("%-12s SHAPE %s vs %s" % (name, a.shape, b.shape))
            continue
        d = np.abs(a - b).max(axis=2)
        n = int((d > 0).sum())
        total += n
        if n == 0:
            print("%-12s identical  (%dx%d)" % (name, a.shape[1], a.shape[0]))
            continue
        ys, xs = np.nonzero(d > 0)
        print("%-12s differs px=%-8d (%.4f%%)  box x %d-%d y %d-%d  maxdelta=%d"
              % (name, n, 100.0 * n / d.size, xs.min(), xs.max(), ys.min(), ys.max(), int(d.max())))
    print("total differing pixels over all stills: %d" % total)
    return total


def builds(label):
    p = os.path.join(RUNS, "%s-gallery.json" % label)
    if not os.path.exists(p):
        return None
    j = json.load(open(p, encoding="utf-8"))

    def find(node, key):
        if isinstance(node, dict):
            if key in node:
                return node[key]
            for v in node.values():
                r = find(v, key)
                if r is not None:
                    return r
        elif isinstance(node, list):
            for v in node:
                r = find(v, key)
                if r is not None:
                    return r
        return None

    return {k: find(j, k) for k in ("buildGuid", "realItems", "missingItems", "panFrames")}


def pan(label):
    frames = sorted(fn for fn in os.listdir(RUNS)
                    if fn.startswith("shot_%s_" % label) and fn.endswith(".png"))
    print("=== pan %s: %d frames ===" % (label, len(frames)))
    if not frames:
        return
    # The player writes the frame number with the width the count needs, and it grows
    # within one run: 165 frames are `_00` .. `_99` then `_100`. A fixed %03d therefore
    # finds no input at all above 99 frames (it did on 19.09, ffmpeg exit 2), so the width
    # is read off the files that are actually there.
    width = len(frames[0].rsplit("_", 1)[1][:-4])
    first = int(frames[0].rsplit("_", 1)[1][:-4])
    out = os.path.join(RUNS, "%s.mp4" % label)
    cmd = ["ffmpeg", "-y", "-framerate", "10", "-start_number", str(first),
           "-i", os.path.join(RUNS, "shot_%s_%%0%dd.png" % (label, width)),
           "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "18", out]
    r = subprocess.run(cmd, capture_output=True, text=True)
    print("ffmpeg exit=%d -> %s (%d bytes)" % (r.returncode, out,
                                               os.path.getsize(out) if os.path.exists(out) else -1))
    if r.returncode:
        print(r.stderr[-800:])


if __name__ == "__main__":
    a, b = sys.argv[1], sys.argv[2]
    print("build %s: %s" % (a, builds(a)))
    print("build %s: %s" % (b, builds(b)))
    diff(a, b)
    pan(b)
