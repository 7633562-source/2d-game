#!/usr/bin/env python3
"""ART-0 leaf pass: build the blind A/B sheets for two capture labels, plus the map.

Usage:  python Tools/art0-leaf-frames-ab.py OLD_LABEL NEW_LABEL OUTDIR [seed]

For each lens it writes one sheet with the two builds side by side: the top row is a 1:1
zoom centred on the median of the pixels that MOVED between the two builds (so the reader
looks exactly where the change is, and not at a region the artist chose), the bottom row is
the whole frame. Which build is left and which is right is decided by the seed and written
to map.txt - the file names carry no verdict, so the read has to come before the map.

Nothing here decides anything: the sheet is read by eye, the map is read after the pick.
"""

import os
import random
import sys

import numpy as np
from PIL import Image

PROJ = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJ, "Trials", "Runs")
LENSES = ("tree", "materials", "photo", "level")


def load(label, lens):
    p = os.path.join(RUNS, "%s_%s.png" % (label, lens))
    if not os.path.exists(p):
        return None, p
    return Image.open(p).convert("RGB"), p


def centre_of_change(a, b):
    d = np.abs(np.asarray(a).astype(np.int16) - np.asarray(b).astype(np.int16)).max(axis=2)
    ys, xs = np.nonzero(d > 0)
    if ys.size == 0:
        h, w = d.shape
        return w // 2, h // 2, 0
    return int(np.median(xs)), int(np.median(ys)), int((d > 0).sum())


def crop(img, cx, cy, cw, ch):
    w, h = img.size
    x0 = max(0, min(w - cw, cx - cw // 2))
    y0 = max(0, min(h - ch, cy - ch // 2))
    return img.crop((x0, y0, x0 + cw, y0 + ch))


def sheet(old, new, lens, outdir, seed):
    a, pa = load(old, lens)
    b, pb = load(new, lens)
    if a is None or b is None:
        print("%-10s skipped (missing: %s / %s)" % (lens, os.path.exists(pa), os.path.exists(pb)))
        return None

    cx, cy, moved = centre_of_change(a, b)
    zoom_w, zoom_h, zoom = 340, 220, 2
    a_zoom = crop(a, cx, cy, zoom_w, zoom_h).resize((zoom_w * zoom, zoom_h * zoom), Image.LANCZOS)
    b_zoom = crop(b, cx, cy, zoom_w, zoom_h).resize((zoom_w * zoom, zoom_h * zoom), Image.LANCZOS)

    full_w = 900
    full_h = int(full_w * a.size[1] / a.size[0])
    a_full = a.resize((full_w, full_h), Image.LANCZOS)
    b_full = b.resize((full_w, full_h), Image.LANCZOS)

    # Deterministic, and not printed: left/right is the reader's question, not the tool's.
    rng = random.Random(seed)
    swap = rng.random() < 0.5
    left_zoom, right_zoom = (b_zoom, a_zoom) if swap else (a_zoom, b_zoom)
    left_full, right_full = (b_full, a_full) if swap else (a_full, b_full)
    labels = (new, old) if swap else (old, new)

    pad = 14
    width = 2 * zoom_w * zoom + pad * 3
    height = zoom_h * zoom + full_h + pad * 3
    canvas = Image.new("RGB", (width, height), (22, 22, 26))
    canvas.paste(left_zoom, (pad, pad))
    canvas.paste(right_zoom, (pad * 2 + zoom_w * zoom, pad))
    canvas.paste(left_full, (pad, pad * 2 + zoom_h * zoom))
    canvas.paste(right_full, (pad * 2 + zoom_w * zoom, pad * 2 + zoom_h * zoom))
    path = os.path.join(outdir, "leaf_%s_panel.png" % lens)
    canvas.save(path)

    with open(os.path.join(outdir, "map.txt"), "a", encoding="utf-8") as fh:
        fh.write("%s: LEFT=%s RIGHT=%s  zoom centred on (%d,%d), %d px moved\n"
                 % (lens, labels[0], labels[1], cx, cy, moved))
    print("%-10s wrote %s (zoom centre %d,%d, %d px moved)" % (lens, path, cx, cy, moved))
    return path


def main():
    old, new, outdir = sys.argv[1], sys.argv[2], sys.argv[3]
    seed = int(sys.argv[4]) if len(sys.argv) > 4 else 7
    os.makedirs(outdir, exist_ok=True)
    map_path = os.path.join(outdir, "map.txt")
    if os.path.exists(map_path):
        os.remove(map_path)
    for lens in LENSES:
        sheet(old, new, lens, outdir, seed)
    print("sheets done; the mapping is in %s (read it AFTER picking)" % map_path)


if __name__ == "__main__":
    main()
