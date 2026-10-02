#!/usr/bin/env python3
"""Row profile of one dog part PNG: silhouette width per row + colour, so a repaint
starts from the delivered shape's numbers, not from a look.

  python Tools/art1-dog-part-profile.py neck
  python Tools/art1-dog-part-profile.py shin --every 16

Prints, per sampled row (top to bottom): y, the alpha run [x0..x1], the width in px and
in metres at the part's box scale, and the row's mean RGB of the solid pixels. The last
block prints the widest/narrowest row and the axis of the body.
"""
from __future__ import annotations

import pathlib
import sys

import numpy as np
from PIL import Image

PROJECT = pathlib.Path(__file__).resolve().parent.parent
ART = PROJECT / "Assets" / "Resources" / "Art" / "Dog"

BOXES = {
    "chest": (0.36, 0.14), "pelvis": (0.22, 0.13), "neck": (0.10, 0.08),
    "head": (0.14, 0.10), "tail": (0.20, 0.05), "front_upper": (0.07, 0.17),
    "front_lower": (0.06, 0.17), "thigh": (0.08, 0.17), "shin": (0.06, 0.17),
    "paw": (0.10, 0.05),
}


def main(argv: list[str]) -> int:
    if not argv:
        print(__doc__)
        return 2
    part = argv[0]
    every = 16
    if "--every" in argv:
        every = int(argv[argv.index("--every") + 1])
    img = Image.open(ART / f"{part}.png").convert("RGBA")
    arr = np.asarray(img).astype(np.int16)
    h, w = arr.shape[:2]
    alpha = arr[:, :, 3]
    solid = alpha >= 220
    ys, xs = np.nonzero(solid)
    if len(xs) == 0:
        print("FAIL no solid pixels")
        return 1
    bbox = (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)
    box = BOXES.get(part)
    print(f"part {part} canvas {w}x{h} solid bbox {bbox} "
          f"({bbox[2] - bbox[0]}x{bbox[3] - bbox[1]} px)")
    if box:
        print(f"box {box[0]}x{box[1]} m -> body px/m x={bbox[2] / box[0]:.0f} y={bbox[3] / box[1]:.0f}")
    widths = []
    for y in range(bbox[1], bbox[3], every):
        row = solid[y]
        idx = np.nonzero(row)[0]
        if len(idx) == 0:
            print(f"  y {y:4d}  -")
            continue
        x0, x1 = int(idx.min()), int(idx.max()) + 1
        rgb = arr[y, x0:x1][solid[y, x0:x1]][:, :3]
        mean = rgb.mean(axis=0) if len(rgb) else (0, 0, 0)
        widths.append((x1 - x0, y, x0, x1))
        print(f"  y {y:4d}  x {x0:4d}..{x1:4d}  width {x1 - x0:4d} px  "
              f"rgb {mean[0]:5.0f},{mean[1]:5.0f},{mean[2]:5.0f}")
    if widths:
        widest = max(widths)
        narrowest = min(widths)
        print(f"widest row y={widest[1]} width={widest[0]} px; "
              f"narrowest y={narrowest[1]} width={narrowest[0]} px "
              f"(widest/narrowest {widest[0] / max(1, narrowest[0]):.2f})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
