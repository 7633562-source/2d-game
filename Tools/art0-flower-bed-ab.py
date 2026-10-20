#!/usr/bin/env python3
"""ART-0 gap 3 (20th pass): the blind A/B of the flower bed - the form this lane trusts.

Read-only (PIL + numpy), no Unity. The two stills are the SAME build (`buildGuid
55be038d16df4c08b4b26d140d9de0a8`), the SAME camera and the SAME moment - only the picture
differs:

    Trials/Runs/flower-20th_flower.png      real petal canvases on  (SetRealArt true)
    Trials/Runs/flower-20th_flowerbox.png   the flat collider boxes (SetRealArt false)

An absolute verdict ("how good is the real art") is the form this card has twice found
unstable; a pairwise side-by-side with the sides withheld is what decides. So this tool:

  1. locates the bed the way `Tools/art0-flower-frame-probe.py` does (ink above the ground
     band, plants split on column gaps) and prints the geometry only;
  2. crops the widest window of the bed that both frames share and magnifies it NEAREST
     (6x by default - the bloom is 10-30 px, so 1:1 is not a readable test);
  3. stacks the two panels with the ORDER SHUFFLED from a seed, writes the sheet, and
     writes the mapping to a separate file.

The map is written to `<name>-map.txt` and must be opened AFTER the picture has been read.

    python Tools/art0-flower-bed-ab.py            # seeds 31 and 41, 6x
    python Tools/art0-flower-bed-ab.py 31 6       # one seed, one zoom
"""
import os
import random
import sys

import numpy as np
from PIL import Image

ROOT = "E:/Games/2D Game/My project"
RUNS = os.path.join(ROOT, "Trials", "Runs")
LABEL = "flower-20th"
OUT = os.path.join(RUNS, "art-0-flower-20th")

PANELS = {
    "flower": "real petal canvases (SetRealArt true)",
    "flowerbox": "flat collider boxes (SetRealArt false)",
}


def load(name):
    return Image.open(os.path.join(RUNS, "%s_%s.png" % (LABEL, name))).convert("RGB")


def bg_color(img):
    a = np.asarray(img).astype(np.int32)
    border = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
    return np.median(border, axis=0)


def geometry(name):
    """The bed's ink box and its plants, exactly the probe's rule."""
    img = load(name)
    a = np.asarray(img).astype(np.int32)
    mask = np.abs(a - bg_color(img)).sum(axis=2) > 26
    spans = mask.sum(axis=1)
    wide = np.flatnonzero(spans > 0.9 * mask.shape[1])
    top = int(wide[0]) if wide.size else mask.shape[0]
    plant = mask.copy()
    plant[top:, :] = False

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
            plants.append((x0, x1, int(rows[0]), int(rows[-1]), int(sub.sum())))
    return img, top, plants


def main():
    seeds = [int(sys.argv[1])] if len(sys.argv) > 2 else ([int(sys.argv[1])] if len(sys.argv) > 1
                                                          else [31, 41])
    zoom = int(sys.argv[2]) if len(sys.argv) > 2 else 6
    os.makedirs(OUT, exist_ok=True)

    data = {}
    for name in PANELS:
        img, top, plants = geometry(name)
        data[name] = (img, top, plants)
        print("%-9s ground band row %4d, plants %d, x %d..%d, above-ground ink rows %d..%d"
              % (name, top, len(plants),
                 min(p[0] for p in plants) if plants else -1,
                 max(p[1] for p in plants) if plants else -1,
                 min(p[2] for p in plants) if plants else -1,
                 max(p[3] for p in plants) if plants else -1))

    # The window both frames share: ONE plant's own box (the widest bloom, so the petals are
    # actually resolved), plus a margin. Chosen once, from the same rule for both panels, so
    # the two panels are the same rectangle of the same camera.
    pick = max(data["flower"][2], key=lambda p: (p[1] - p[0]) * (p[3] - p[2]))
    cx0, cx1 = pick[0] - 14, pick[1] + 14
    cy0, cy1 = pick[2] - 14, pick[3] + 14
    box = (max(0, cx0), max(0, cy0), min(1920, cx1), min(1080, cy1))
    print("plant picked from the real frame: x %d-%d y %d-%d -> window %dx%d px at %dx = %dx%d"
          % (pick[0], pick[1], pick[2], pick[3], box[2] - box[0], box[3] - box[1], zoom,
             (box[2] - box[0]) * zoom, (box[3] - box[1]) * zoom))

    for seed in seeds:
        order = list(PANELS)
        random.Random(seed).shuffle(order)
        panels = [data[n][0].crop(box).resize(((box[2] - box[0]) * zoom, (box[3] - box[1]) * zoom),
                                             Image.NEAREST) for n in order]
        w = max(p.size[0] for p in panels)
        h = sum(p.size[1] for p in panels) + 10
        sheet = Image.new("RGB", (w, h), (110, 110, 110))
        y = 0
        for p in panels:
            sheet.paste(p, (0, y))
            y += p.size[1] + 10
        name = os.path.join(OUT, "bed_ab_seed%d_%dx.png" % (seed, zoom))
        sheet.save(name)
        with open(os.path.join(OUT, "bed_ab_seed%d_%dx-map.txt" % (seed, zoom)), "w",
                  encoding="utf-8") as fh:
            fh.write("panel 1 (TOP)    = %s   %s\n" % (order[0], PANELS[order[0]]))
            fh.write("panel 2 (BOTTOM) = %s   %s\n" % (order[1], PANELS[order[1]]))
            fh.write("\nread the picture first, then this file\n")
        print("wrote %s (%dx%d)" % (name, sheet.size[0], sheet.size[1]))


if __name__ == "__main__":
    main()
