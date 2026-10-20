#!/usr/bin/env python3
"""Render ONE canvas into a part's box at a lens, the way the dresser draws it: the A/B a
stretched canvas can be judged on when no frame carries that kind.

    python Tools/art0-hip-lens-ab.py <canvasA.png> <canvasB.png> box=W,H px_m=613.6
        tint=r,g,b bg=r,g,b pad=30 still=poppylens

`ArtParts.Dress` puts a canvas in `Sliced` mode with `size = the part's box`, so the canvas is
STRETCHED to that box: what the eye gets is `box` at the lens, showing the canvas with the box's
aspect. A canvas whose aspect does not match the box is therefore the visible defect, and this
tool renders exactly that - each canvas into the SAME box, same lens, same tint, same background,
so the only variable is the canvas. It writes two PNGs in the naming `art0-ab-tight.py` reads
(`hipab_a_<still>.png`, `hipab_b_<still>.png`), which is what makes the blind pairwise sheet and
its map file possible.

This is a RENDER, not a frame: it proves what the canvas does in the box, it does not prove the
plant. Report it as such.
"""
import os
import sys

import numpy as np
from PIL import Image

RUNS = os.path.join("Trials", "Runs")


def render(canvas_path, box, px_m, tint, bg, pad):
    img = Image.open(canvas_path).convert("RGBA")
    bw = max(1, int(round(box[0] * px_m)))
    bh = max(1, int(round(box[1] * px_m)))
    small = img.resize((bw, bh), Image.LANCZOS)
    arr = np.asarray(small).astype(np.float64)
    arr[:, :, 0] *= tint[0]
    arr[:, :, 1] *= tint[1]
    arr[:, :, 2] *= tint[2]
    small = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")
    # The form the eye gets is the canvas' alpha STRETCHED to the box: measure it, do not assume
    # it. `Dress` sets `size = box` in Sliced mode, so canvas pixel (u,v) lands on box point
    # (u * box.x, v * box.y) - the drawn shape is `box`-shaped whatever the canvas' aspect is.
    alpha = np.asarray(small)[:, :, 3] > 8
    if alpha.any():
        ys, xs = np.nonzero(alpha)
        w = xs.max() - xs.min() + 1
        h = ys.max() - ys.min() + 1
        shape = "%dx%d px, drawn form aspect %.3f" % (w, h, w / float(h))
    else:
        shape = "nothing opaque"
    out = Image.new("RGBA", (bw + 2 * pad, bh + 2 * pad), (bg[0], bg[1], bg[2], 255))
    out.alpha_composite(small, (pad, pad))
    return out.convert("RGB"), shape


def main():
    args = dict(a.split("=", 1) for a in sys.argv[3:] if "=" in a)
    if len(sys.argv) < 4 or "box" not in args:
        print(__doc__)
        return 2
    box = tuple(float(v) for v in args["box"].split(","))
    px_m = float(args.get("px_m", 613.6))
    tint = tuple(float(v) for v in args.get("tint", "1,1,1").split(","))
    bg = tuple(int(v) for v in args.get("bg", "86,82,76").split(","))
    pad = int(args.get("pad", 30))
    still = args.get("still", "lens")
    os.makedirs(RUNS, exist_ok=True)
    for name, path in (("a", sys.argv[1]), ("b", sys.argv[2])):
        out = os.path.join(RUNS, "hipab_%s_%s.png" % (name, still))
        img, shape = render(path, box, px_m, tint, bg, pad)
        img.save(out)
        print("%-6s %-52s canvas %-9s -> %dx%d px (box %.4f x %.4f m at %.1f px/m)"
              % (name, os.path.basename(path), Image.open(path).size, img.width, img.height,
                 box[0], box[1], px_m))
        print("       drawn form: %s" % shape)
    print("wrote Trials/Runs/hipab_a_%s.png and hipab_b_%s.png" % (still, still))
    print("blind sheet: python Tools/art0-ab-tight.py hipab_a hipab_b %s 6 0,0,%d,%d <seed> %s"
          % (still, 2 * pad + int(round(box[0] * px_m)), 2 * pad + int(round(box[1] * px_m)), still))
    return 0


if __name__ == "__main__":
    sys.exit(main())
