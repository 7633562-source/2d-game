#!/usr/bin/env python3
"""ART-0 22nd pass: what a limb actually draws, before and after the tile-period fix.

The 21st pass left a measured item: with the drawn box finally equal to the part's box,
a limb of 0.10 x 0.30 m takes ONE whole 0.5 m `bark` canvas in Sliced draw mode, i.e. the
tile is STRETCHED onto the limb (`Sliced` maps the whole canvas onto the rect). The grain
then sits at a different scale on a limb (0.5 m of content across 0.10 m = 5.0x) than on
the trunk (Tiled at its own 0.5 m period = 1.0x), so one tree carries two bark scales.

This tool answers, offline and on the shipped bytes:

  1. the bark canvas' own structure scale (px per feature, from the mean |d/dx| autocorrelation);
  2. what Sliced does to a limb (source px per metre, i.e. the compression factor);
  3. what Tiled does to the same limb (a crop at the canvas' natural 1024 px/m);
  4. whether the crop a Tiled limb would show is REPRESENTATIVE of the canvas - the Tiled
     branch always starts at the rect's own corner, so every limb of a family shares one
     corner crop, and a corner that is an outlier (a knot) would darken every limb;
  5. two rendered limb panels (Sliced vs Tiled) at the gallery tree lens, for the eye.

    python Tools/art0-bark-crop-probe.py
"""
import os

import numpy as np
from PIL import Image

PROJ = "E:/Games/2D Game/My project"
ART = os.path.join(PROJ, "Assets", "Resources", "Art")
OUT = os.path.join(PROJ, "Trials", "Runs", "art-0-barkcrop")
CANVAS = 512                     # shipped material tile
PERIOD_M = 0.5                   # ArtParts.BarkTileMeters
LIMB = (0.10, 0.30)              # m, the tree lane's limb box
TREE_ORTHO = 1.303696870803833   # gallery `tree` lens (capture log)
FRAME_H = 1080


def lum(img):
    a = np.asarray(img.convert("RGB"), dtype=np.float64)
    return 0.299 * a[:, :, 0] + 0.587 * a[:, :, 1] + 0.114 * a[:, :, 2]


def structure_px(g):
    """Feature scale in px: the first lag (x and y) where the mean-removed
    autocorrelation falls below 0.5, plus the mean |gradient|."""
    g = g - g.mean()
    out = {}
    for axis, name in ((1, "x"), (0, "y")):
        num = (g * np.roll(g, -1, axis=axis))[:-1 if axis == 1 else None,
                                              :-1 if axis == 1 else None]
        den = (g * g)[:-1 if axis == 1 else None, :-1 if axis == 1 else None]
        r = float(num.mean() / den.mean())
        half = 1
        for lag in range(1, 160):
            s = np.roll(g, -lag, axis=axis)
            if axis == 1:
                r = float(((g[:, :-lag] - g[:, :-lag].mean()) *
                           (s[:, :-lag] - s[:, :-lag].mean())).mean() /
                          (g[:, :-lag].std() * s[:, :-lag].std()))
            else:
                r = float(((g[:-lag, :] - g[:-lag, :].mean()) *
                           (s[:-lag, :] - s[:-lag, :].mean())).mean() /
                          (g[:-lag, :].std() * s[:-lag, :].std()))
            if r < 0.5:
                half = lag
                break
        grad = float(np.abs(np.diff(g, axis=axis)).mean())
        out[name] = (half, grad)
    return out


def windows(g, w, h):
    """Every sliding w x h window mean, so the corner crop can be placed in the
    distribution the whole canvas offers."""
    c = np.cumsum(np.cumsum(g, axis=0), axis=1)
    c = np.pad(c, ((1, 0), (1, 0)))
    rows = g.shape[0] - h + 1
    cols = g.shape[1] - w + 1
    if rows < 1 or cols < 1:
        return None
    s = (c[h:h + rows, w:w + cols] - c[0:rows, w:w + cols]
         - c[h:h + rows, 0:cols] + c[0:rows, 0:cols])
    return s / (w * h)


def main():
    os.makedirs(OUT, exist_ok=True)
    key = "bark"
    img = Image.open(os.path.join(ART, key + ".png")).convert("RGB")
    if img.size != (CANVAS, CANVAS):
        raise SystemExit("%s is %dx%d, expected %dx%d" % (key, img.size[0], img.size[1],
                                                          CANVAS, CANVAS))
    g = lum(img)
    px_per_m = CANVAS / PERIOD_M
    print("=== %s canvas %dx%d, period %.2f m -> natural %.0f px/m" %
          (key, CANVAS, CANVAS, PERIOD_M, px_per_m))
    print("mean lum %.1f  std %.1f  min %.0f  max %.0f" %
          (g.mean(), g.std(), g.min(), g.max()))
    for name, (half, grad) in structure_px(g).items():
        print("structure axis %s: autocorr<0.5 at %d px = %.3f m  mean|d| %.2f level/px" %
              (name, half, half / px_per_m, grad))

    # the pixels a limb draws today vs at natural scale
    bw = int(round(LIMB[0] * px_per_m))
    bh = int(round(LIMB[1] * px_per_m))
    print("\n=== limb box %.2f x %.2f m = %d x %d source px at natural scale" %
          (LIMB[0], LIMB[1], bw, bh))
    print("Sliced (today): the whole %d px canvas across %.2f m -> %.0f source px/m "
          "= %.2fx the natural %.0f px/m" %
          (CANVAS, LIMB[0], CANVAS / LIMB[0], (CANVAS / LIMB[0]) / px_per_m, px_per_m))
    print("                across %.2f m -> %.0f source px/m = %.2fx" %
          (LIMB[1], CANVAS / LIMB[1], (CANVAS / LIMB[1]) / px_per_m))

    # is the corner crop the Tiled branch would show representative?
    corner = g[CANVAS - bh:, 0:bw]          # Unity's rect corner = image bottom-left
    w = windows(g, bw, bh)
    ref = w.mean() if w is not None else float("nan")
    print("\n=== the corner crop a Tiled limb shows: %d..%d rows, 0..%d cols" %
          (CANVAS - bh, CANVAS, bw))
    print("crop mean %.1f std %.1f   canvas mean %.1f std %.1f   canvas-wide window mean %.1f" %
          (corner.mean(), corner.std(), g.mean(), g.std(), ref))
    if w is not None:
        pct = float((w < corner.mean()).mean() * 100)
        print("that crop's mean sits at percentile %.1f of all %d sliding %dx%d windows "
              "(%.1f..%.1f)" %
              (pct, w.size, bw, bh, w.min(), w.max()))
        print("window-mean spread %.1f level (1 sigma) vs the canvas' own %.1f" %
              (w.std(), g.std()))

    # the eye: both renderings of the same limb at the gallery tree lens
    m_per_px = 2.0 * TREE_ORTHO / FRAME_H
    print("\n=== gallery `tree` lens ortho %.8f -> %.2f px/m on screen; the limb is %.1f x %.1f px" %
          (TREE_ORTHO, 1.0 / m_per_px, LIMB[0] / m_per_px, LIMB[1] / m_per_px))
    onw = max(2, int(round(LIMB[0] / m_per_px)))
    onh = max(2, int(round(LIMB[1] / m_per_px)))
    zoom = 10
    for key in ("bark", "leaf"):
        canvas = Image.open(os.path.join(ART, key + ".png")).convert("RGB")
        sliced = canvas.resize((onw, onh), Image.LANCZOS)
        tiled = canvas.crop((0, CANVAS - bh, bw, CANVAS)).resize((onw, onh), Image.LANCZOS)
        sheet = Image.new("RGB", (onw * zoom * 2 + 30, onh * zoom + 40), (110, 110, 110))
        for i, p in enumerate((sliced, tiled)):
            sheet.paste(p.resize((onw * zoom, onh * zoom), Image.NEAREST),
                        (i * (onw * zoom + 30), 30))
        path = os.path.join(OUT, "limb_%s_sliced_vs_tiled_10x.png" % key)
        sheet.save(path)
        print("wrote %s (LEFT panel = Sliced, RIGHT panel = Tiled; %d x %d px each at %dx)" %
              (path, onw, onh, zoom))

    # What the change is worth ON SCREEN, per part size: both renderings of the same part
    # as the gallery lens would show them. If the mean |difference| is a couple of levels
    # the frames cannot carry the change and the report has to say so.
    print("\n=== the pair as the gallery lens shows it (both panels the same part, same box)")
    print("%-6s %-14s %8s %8s %9s %9s %9s" %
          ("key", "box m", "screen", "|A-B|", "grainA", "grainB", "bands"))
    boxes = [(0.10, 0.30), (0.12, 0.08), (0.20, 0.15), (0.30, 0.60), (0.05, 0.05)]
    for key in ("bark", "leaf"):
        canvas = Image.open(os.path.join(ART, key + ".png")).convert("RGB")
        cg = lum(canvas)
        for bw_m, bh_m in boxes:
            bw_px = int(round(bw_m * px_per_m))
            bh_px = int(round(bh_m * px_per_m))
            ow = max(2, int(round(bw_m / m_per_px)))
            oh = max(2, int(round(bh_m / m_per_px)))
            a = lum(canvas.resize((ow, oh), Image.LANCZOS))
            b = lum(canvas.crop((0, CANVAS - bh_px, bw_px, CANVAS)).resize((ow, oh), Image.LANCZOS))
            diff = np.abs(a - b).mean()
            grain_a = np.abs(np.diff(a, axis=1)).mean()
            grain_b = np.abs(np.diff(b, axis=1)).mean()
            print("%-6s %-14s %8s %8.2f %9.2f %9.2f %9s" %
                  (key, "%.2fx%.2f" % (bw_m, bh_m), "%dx%d" % (ow, oh), diff, grain_a, grain_b,
                   "yes" if diff >= 3.0 else "sub-visual"))


if __name__ == "__main__":
    main()
