"""ART-0 gap 3 (20th pass): what the flower bed's frame carries, before and after the petals.

Read-only, offline (PIL). The acceptance for this lane is a PICTURE - the game's own renderer,
the same camera - so this measures the two stills of the SAME build and SAME lens:

    Trials/Runs/flower-20th_flower.png     real petal canvases on
    Trials/Runs/flower-20th_flowerbox.png  the flat collider boxes back (SetRealArt(false))

The still is mostly the ground band (a full-width strip), so the analysis works ABOVE it:
plant rows only, labels excluded (they sit on the soil).

Two things the eye was asked about, in numbers:

  1. silhouette: the mean horizontal run of ink inside one plant divided by that plant's own
     width. Every row of a rectangular card is one full-width run (ratio ~1.0, and the outline
     is 90-degree corners); a tapered petal's rows are narrow at the base and the tip, so the
     ratio drops and the outline is a curve.
  2. tone: how many distinct brightness levels sit inside one plant. A flat box carries one
     colour (the tint) plus its alpha edge; a canvas carries a shading ramp.

It also writes the pair stacked at 2x with the panels in a SHUFFLED order and the mapping in
bed_pair_2x-map.txt (a named panel is not evidence: read the picture, then the map).
"""
import os
import random

import numpy as np
from PIL import Image

ROOT = "E:/Games/2D Game/My project"
RUNS = os.path.join(ROOT, "Trials", "Runs")
LABEL = "flower-20th"
OUT = os.path.join(RUNS, "art-0-flower-20th")


def load(name):
    path = os.path.join(RUNS, "%s_%s.png" % (LABEL, name))
    return Image.open(path).convert("RGB"), path


def bg_color(img):
    """The scene's own backdrop: the median of the frame's border pixels."""
    a = np.asarray(img).astype(np.int32)
    border = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
    return np.median(border, axis=0)


def ground_top(mask):
    """The top row of the ground band: the first row whose ink spans nearly the whole frame."""
    spans = mask.sum(axis=1)
    wide = np.flatnonzero(spans > 0.9 * mask.shape[1])
    return int(wide[0]) if wide.size else mask.shape[0]


def analyze(name):
    img, path = load(name)
    a = np.asarray(img).astype(np.int32)
    mask = np.abs(a - bg_color(img)).sum(axis=2) > 26
    top = ground_top(mask)
    plant = mask.copy()
    plant[top:, :] = False          # the ground band and the labels on it are not the plants

    cols = plant.sum(axis=0)
    up = np.flatnonzero(cols > 0)
    plants = []
    if up.size:
        gaps = np.flatnonzero(np.diff(up) > 3)
        edges = np.concatenate([[up[0]], up[gaps + 1], [up[-1] + 1]])
        for i in range(len(edges) - 1):
            x0, x1 = int(edges[i]), int(edges[i + 1])
            sub = plant[:, x0:x1]
            rows = np.flatnonzero(sub.any(axis=1))
            if sub.sum() < 60 or rows.size == 0:
                continue
            y0, y1 = int(rows[0]), int(rows[-1])
            width = x1 - x0
            runs = []
            for row in sub:
                idx = np.flatnonzero(row)
                if idx.size == 0:
                    continue
                split = np.flatnonzero(np.diff(idx) > 1)
                for piece in np.split(idx, split + 1):
                    if piece.size:
                        runs.append(piece.size)
            runs = np.array(runs, dtype=np.float64)
            pix = a[:, x0:x1][sub]
            tones = len(set(map(tuple, (pix // 24).tolist())))
            plants.append(dict(x0=x0, x1=x1, y0=y0, y1=y1, width=width, height=y1 - y0 + 1,
                               area=int(sub.sum()), runs=runs.size,
                               mean_run=float(runs.mean()), max_run=int(runs.max()),
                               ratio=float(runs.mean()) / max(1, width), tones=tones))

    print("%-10s %s" % (name, os.path.basename(path)))
    print("  ground band starts at row %d; plant pixels=%d in %d plants"
          % (top, int(plant.sum()), len(plants)))
    for p in plants:
        print("  plant x %4d-%4d y %4d-%4d  %3dx%3d px  area=%6d  meanRun=%6.1f"
              " (%.2f of width)  longestRun=%4d  runs=%5d  tones=%3d"
              % (p["x0"], p["x1"], p["y0"], p["y1"], p["width"], p["height"], p["area"],
                 p["mean_run"], p["ratio"], p["max_run"], p["runs"], p["tones"]))
    if plants:
        print("  -> mean of (meanRun/width) = %.3f   mean tones/plant = %.1f   max tones = %d"
              % (np.mean([p["ratio"] for p in plants]),
                 np.mean([p["tones"] for p in plants]),
                 max(p["tones"] for p in plants)))
    return plants


def main():
    os.makedirs(OUT, exist_ok=True)
    a = analyze("flower")
    b = analyze("flowerbox")

    order = ["flower", "flowerbox"]
    random.Random(20).shuffle(order)
    panels = []
    for name in order:
        img, _ = load(name)
        am = np.asarray(img).astype(np.int32)
        m = np.abs(am - bg_color(img)).sum(axis=2) > 26
        top = ground_top(m)
        m[top:, :] = False
        rows = np.flatnonzero(m.any(axis=1))
        cols = np.flatnonzero(m.any(axis=0))
        box = (max(0, int(cols[0]) - 10), max(0, int(rows[0]) - 10),
               int(cols[-1]) + 10, min(img.size[1], int(rows[-1]) + 10))
        panels.append(img.crop(box).resize(((box[2] - box[0]) * 2, (box[3] - box[1]) * 2),
                                           Image.NEAREST))
    w = max(p.size[0] for p in panels)
    h = sum(p.size[1] for p in panels) + 8
    sheet = Image.new("RGB", (w, h), (110, 110, 110))
    y = 0
    for p in panels:
        sheet.paste(p, (0, y))
        y += p.size[1] + 8
    out_path = os.path.join(OUT, "bed_pair_2x.png")
    sheet.save(out_path)
    with open(os.path.join(OUT, "bed_pair_2x-map.txt"), "w", encoding="utf-8") as fh:
        fh.write("panel 1 (top)    = %s\npanel 2 (bottom) = %s\n" % (order[0], order[1]))
        fh.write("\nread the picture first, then this file\n")
    print("\nwrote %s (%dx%d), map in bed_pair_2x-map.txt"
          % (out_path, sheet.size[0], sheet.size[1]))
    print("plant counts: real=%d box=%d" % (len(a), len(b)))


if __name__ == "__main__":
    main()
