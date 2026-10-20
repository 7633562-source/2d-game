"""ART-B1 frame reader: the birds as translucent rectangles (23.09).

Two looks, one binary: the runs differ only by `-birdRect 0|1`, so the pixels that
change between a pair of frames ARE the bird's picture. This tool turns that into
numbers and into one tight sheet the eye can judge.

    python Tools/artb1-frames.py diff <labelA> <labelB> [first] [last]
    python Tools/artb1-frames.py pair <labelA> <labelB> <frame> <out.png> [zoom]

`diff` prints per frame: changed px, share of the frame, the change bbox, max channel
delta, and the colour histogram of the changed pixels in B (the rectangle fill read as
ink-over-backdrop). `pair` cuts the same crop from both frames, upscales it nearest and
stacks A | B with a magenta bar - no header, no caption, under ~1000 px per side.
"""

import sys
import os

import numpy as np
from PIL import Image, ImageDraw

RUNS = "E:/Games/2D Game/My project/Trials/Runs"


def frame_path(label, n):
    # The recorder pads to the width of the run: shot_x_00.png ... shot_x_99.png,
    # then shot_x_100.png. Take the name that is on disk.
    for pattern in ("shot_%s_%02d.png", "shot_%s_%03d.png", "shot_%s_%d.png"):
        path = os.path.join(RUNS, pattern % (label, n))
        if os.path.exists(path):
            return path
    return os.path.join(RUNS, "shot_%s_%03d.png" % (label, n))


def frames_of(label):
    out = []
    for name in os.listdir(RUNS):
        if name.startswith("shot_%s_" % label) and name.endswith(".png"):
            out.append(int(name[len(label) + 6:-4]))
    return sorted(out)


def load(label, n):
    return np.asarray(Image.open(frame_path(label, n)).convert("RGB")).astype(np.int16)


def diff_rows(label_a, label_b, first=None, last=None):
    common = sorted(set(frames_of(label_a)) & set(frames_of(label_b)))
    if first is not None:
        common = [n for n in common if n >= first]
    if last is not None:
        common = [n for n in common if n <= last]
    rows = []
    for n in common:
        a = load(label_a, n)
        b = load(label_b, n)
        d = np.abs(a - b).sum(axis=2)
        mask = d > 24
        px = int(mask.sum())
        if px == 0:
            rows.append((n, 0, 0.0, None, 0))
            continue
        ys, xs = np.nonzero(mask)
        box = (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)
        rows.append((n, px, px / mask.size, box, int(d.max())))
    return rows


def print_diff(label_a, label_b, first=None, last=None):
    rows = diff_rows(label_a, label_b, first, last)
    print("frames compared: %d  (%s vs %s)" % (len(rows), label_a, label_b))
    for n, px, share, box, mx in rows:
        print("  f%03d changed=%6d share=%.4f box=%s maxdelta=%d"
              % (n, px, share, box, mx))
    live = [r for r in rows if r[1] > 0]
    if not live:
        print("no frame differs: the two labels draw the same picture")
        return
    boxes = np.array([r[3] for r in live], dtype=float)
    union = (int(boxes[:, 0].min()), int(boxes[:, 1].min()),
             int(boxes[:, 2].max()), int(boxes[:, 3].max()))
    print("union box over %d live frames: %s (w=%d h=%d)"
          % (len(live), union, union[2] - union[0], union[3] - union[1]))
    widest = max(live, key=lambda r: r[1])
    print("widest change: frame %d px=%d" % (widest[0], widest[1]))

    n = widest[0]
    a = load(label_a, n)
    b = load(label_b, n)
    d = np.abs(a - b).sum(axis=2)
    mask = d > 24
    ink = b[mask]
    print("changed pixels in %s: %d" % (label_b, len(ink)))
    if len(ink):
        q = np.quantile(ink, [0.1, 0.5, 0.9], axis=0).astype(int)
        mean = ink.mean(axis=0).astype(int)
        print("  colour mean=%s  p10=%s  p50=%s  p90=%s"
              % (tuple(mean), tuple(q[0]), tuple(q[1]), tuple(q[2])))


def pair(label_a, label_b, n, out_path, zoom=6, box=None):
    a = load(label_a, n)
    b = load(label_b, n)
    d = np.abs(a - b).sum(axis=2)
    mask = d > 24
    if box is None:
        ys, xs = np.nonzero(mask)
        if len(xs) == 0:
            print("frame %d differs nowhere" % n)
            return
        pad = 24
        box = (max(0, int(xs.min()) - pad), max(0, int(ys.min()) - pad),
               min(a.shape[1], int(xs.max()) + pad), min(a.shape[0], int(ys.max()) + pad))
    print("crop box %s zoom %d" % (box, zoom))

    panels = []
    for img in (a, b):
        crop = Image.fromarray(img.astype(np.uint8)).crop(box)
        panels.append(crop.resize((crop.width * zoom, crop.height * zoom), Image.NEAREST))
    bar = 8
    w = panels[0].width
    h = panels[0].height
    sheet = Image.new("RGB", (w * 2 + bar, h), (255, 0, 255))
    sheet.paste(panels[0], (0, 0))
    sheet.paste(panels[1], (w + bar, 0))
    sheet.save(out_path)
    print("wrote %s (%dx%d)" % (out_path, sheet.width, sheet.height))


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return
    mode = sys.argv[1]
    if mode == "diff":
        a, b = sys.argv[2], sys.argv[3]
        first = int(sys.argv[4]) if len(sys.argv) > 4 else None
        last = int(sys.argv[5]) if len(sys.argv) > 5 else None
        print_diff(a, b, first, last)
    elif mode == "pair":
        a, b, n, out = sys.argv[2], sys.argv[3], int(sys.argv[4]), sys.argv[5]
        zoom = int(sys.argv[6]) if len(sys.argv) > 6 else 6
        pair(a, b, n, out, zoom)
    else:
        print(__doc__)


if __name__ == "__main__":
    main()
