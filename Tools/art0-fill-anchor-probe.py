"""ART-0 (18th pass): what makes the 8 m ground_fill tile repeat visible on a wide slab.

The fill is Tiled at its native 8 x 2 m (StaticPlatform: `Tiled` when the slab is wider than one
tile), so a 32 m "Behind" plate shows the SAME 1024 x 256 pixels four times. A perfectly periodic
texture has no seam to measure - the question is not "is the cut clean" (that is `seam_error` and
`level_tile` in Tools/gen-level-textures.py) but "does the eye get a handle on the period".

The handle is a LOCALLY DISTINCTIVE feature: a blob whose colour steps far enough away from its own
surroundings to be recognised, and stays recognisable when it comes round again. This probe measures
exactly that, with no Unity and no trial:

  1. local mean of the luminance over a box of `--radius` px (the blob scale),
  2. resid = pixel - local mean, level 0..255,
  3. connected components of |resid| >= `--cut`, area >= `--area`,
  4. for each component: area, peak |resid|, bbox, centroid x,
  5. salience = peak |resid| / grain, where grain = the 95th percentile of |resid| over the whole tile
     (the tile's own noise floor, the same "vs its own grain" language the wrap work uses).

A feature that is 1.0-1.5x the grain is texture; one that is 3x the grain is an object the eye can
name - and a named object that comes back every 8 m is what a repeat complaint is made of.

Prints a table for every picture given, plus the strip case: the tile repeated `--periods` times at
the shipping scale of the wide plate (32 px/m, i.e. the 1024 px tile drawn 256 px wide).

Usage: python Tools/art0-fill-anchor-probe.py [png ...]      (default: the shipped ground_fill)
"""
import os
import sys

import numpy as np
from PIL import Image

ROOT = r"E:/Games/2D Game/My project"
LEVEL = os.path.join(ROOT, "Assets", "Resources", "Level")


def luminance(a):
    return a[..., 0] * 0.2126 + a[..., 1] * 0.7152 + a[..., 2] * 0.0722


def box_mean(a, radius):
    """Local mean over a (2r+1)^2 box, edges handled by shrinking the window (no wrap)."""
    pad = np.pad(a, radius, mode="edge")
    csum = pad.cumsum(0).cumsum(1)
    csum = np.pad(csum, ((1, 0), (1, 0)))
    h, w = a.shape
    ys, xs = np.arange(h), np.arange(w)
    y0 = ys
    y1 = ys + 2 * radius + 1
    x0 = xs
    x1 = xs + 2 * radius + 1
    total = (csum[np.ix_(y1, x1)] - csum[np.ix_(y0, x1)]
             - csum[np.ix_(y1, x0)] + csum[np.ix_(y0, x0)])
    area = (y1 - y0)[:, None] * (x1 - x0)[None, :]
    return total / area


def components(mask):
    """4-connected components of a boolean mask. Returns a list of (ys, xs) index arrays."""
    h, w = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    out = []
    idx = np.argwhere(mask)
    for sy, sx in idx:
        if seen[sy, sx]:
            continue
        stack = [(sy, sx)]
        seen[sy, sx] = True
        pts = []
        while stack:
            y, x = stack.pop()
            pts.append((y, x))
            for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        arr = np.array(pts)
        out.append((arr[:, 0], arr[:, 1]))
    return out


def probe(path, radius=6, cut=12.0, area=8, top=6, periods=4, ship_ppm=32.0):
    img = Image.open(path).convert("RGBA")
    a = np.asarray(img).astype(np.float64)
    alpha = a[..., 3] / 255.0
    lum = luminance(a[..., :3])
    resid = lum - box_mean(lum, radius)
    grain = float(np.percentile(np.abs(resid), 95))
    mask = np.abs(resid) >= cut
    comps = [c for c in components(mask) if c[0].size >= area]
    feats = []
    for ys, xs in comps:
        r = resid[ys, xs]
        peak = float(np.max(np.abs(r)))
        feats.append({
            "area": int(ys.size),
            "peak": peak,
            "salience": peak / grain if grain > 1e-6 else float("inf"),
            "x": int(round(float(xs.mean()))),
            "y": int(round(float(ys.mean()))),
            "w": int(xs.max() - xs.min() + 1),
            "h": int(ys.max() - ys.min() + 1),
        })
    feats.sort(key=lambda f: f["salience"] * np.log2(1.0 + f["area"]), reverse=True)

    w, h = img.size
    rgb = a[..., :3].reshape(-1, 3).mean(axis=0)
    ppm = w / 8.0                       # 8 m native width (PPU 128)
    print("== %s" % os.path.relpath(path, ROOT).replace("\\", "/"))
    print("   %dx%d  native %.2f x %.2f m  %.0f px/m  grain(p95 |resid|) %.2f  mean rgb %s"
          % (w, h, w / 128.0, h / 128.0, ppm, grain,
             " ".join("%.1f" % v for v in rgb)))
    print("   blobs |resid|>=%.0f area>=%d: %d   strongest %.2fx grain, %d at >=3x grain"
          % (cut, area, len(feats),
             feats[0]["salience"] if feats else 0.0,
             sum(1 for f in feats if f["salience"] >= 3.0)))
    for f in feats[:top]:
        print("   %6.2fx grain  area %5d  peak %5.1f  at x=%4d y=%3d  %dx%d px"
              % (f["salience"], f["area"], f["peak"], f["x"], f["y"], f["w"], f["h"]))

    # The strip case: the tile repeated at the wide plate's shipping scale (32 px/m -> 256 px per
    # 8 m tile). A feature keeps its screen size here, so this is the frame the eye sees.
    small = img.convert("RGB").resize((int(round(8.0 * ship_ppm)), int(round(2.0 * ship_ppm))),
                                      Image.LANCZOS)
    strip = Image.new("RGB", (small.size[0] * periods, small.size[1]))
    for i in range(periods):
        strip.paste(small, (i * small.size[0], 0))
    s = np.asarray(strip).astype(np.float64)
    r2 = luminance(s) - box_mean(luminance(s), 3)
    g2 = float(np.percentile(np.abs(r2), 95))
    print("   strip %d x %.0f m at %.0f px/m: %dx%d, grain %.2f, max |resid| %.1f (%.2fx)"
          % (periods, periods * 8.0, ship_ppm, strip.size[0], strip.size[1], g2,
             float(np.max(np.abs(r2))), float(np.max(np.abs(r2))) / g2 if g2 > 1e-6 else 0.0))
    return feats


def main():
    args = [a for a in sys.argv[1:]]
    paths = args or [os.path.join(LEVEL, "ground_fill.png")]
    for p in paths:
        probe(p)
    return 0


if __name__ == "__main__":
    sys.exit(main())
