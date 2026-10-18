"""ART-0 (fresh read follow-up): does `ground_fill` carry a horizontal RHYTHM the eye reads as columns?

A read of the current build's LEVEL still (`Trials/Runs/gallery7_level.png`) named "a rhythmic vertical
banding in the soil, the dark hollows and light ledges repeat at an equal step of about 1-1.2 m". The
fill canvas is 1024 x 256 px at PPU 128 = 8 x 2 m, so a 1 m step is 128 px of the canvas - a period
INSIDE one tile, not the tile itself. That would be an asset defect, not the level lane's stretch.

This measures, per column of the canvas:
  bandmean  = the column mean over a soil band
  bandstd   = the column std over the same band
and reports, for each band, the strongest period of the column profile (autocorrelation over lags
8..512 px) and its strength against the profile's own spread. A landscape tile has no preferred
period; a rhythm does.

Usage: python Tools/art0-fill-column-rhythm.py
"""
import os

import numpy as np
from PIL import Image

ROOT = r"E:/Games/2D Game/My project"
FILL = os.path.join(ROOT, "Assets", "Resources", "Level", "ground_fill.png")
FRAME = os.path.join(ROOT, "Trials", "Runs", "gallery7_level.png")


def periods(profile, lo=8, hi=512):
    p = profile - profile.mean()
    n = len(p)
    ac = np.correlate(p, p, mode="full")[n - 1:]
    ac = ac / (ac[0] if ac[0] else 1.0)
    best = []
    for lag in range(lo, min(hi, n - 2)):
        if ac[lag] > ac[lag - 1] and ac[lag] >= ac[lag + 1]:
            best.append((float(ac[lag]), lag))
    best.sort(reverse=True)
    return ac, best[:5]


def report(name, arr, rows, cols):
    band = arr[rows[0]:rows[1], cols[0]:cols[1]]
    if band.ndim == 3:
        band = band.mean(axis=2)
    cm = band.mean(axis=0)
    cs = band.std(axis=0)
    print("--- %s band rows=%s cols=%s  (%d x %d)" % (name, rows, cols, band.shape[1], band.shape[0]))
    for label, prof in (("column mean", cm), ("column std", cs)):
        spread = float(prof.std())
        ac, best = periods(prof)
        got = ", ".join("lag=%d (%.0f px) r=%.3f" % (l, l, r) for r, l in best) or "no local peak"
        print("    %-11s spread=%.2f levels | %s" % (label, spread, got))


def main():
    img = Image.open(FILL).convert("RGB")
    arr = np.asarray(img).astype(np.float64)
    h, w = arr.shape[:2]
    print("ground_fill %dx%d  (= %.1f x %.1f m at PPU 128)" % (w, h, w / 128.0, h / 128.0))
    report("fill", arr, (0, h), (0, w))

    fr = Image.open(FRAME).convert("RGB")
    farr = np.asarray(fr).astype(np.float64)
    fh, fw = farr.shape[:2]
    print("\nframe gallery7_level.png %dx%d" % (fw, fh))
    # the soil band of the level row: find it by the most brown rows
    rows = farr.mean(axis=1)
    brown = (rows[:, 0] > rows[:, 2] + 20) & (rows[:, 0] > 60) & (rows[:, 0] < 200)
    idx = np.where(brown)[0]
    if len(idx) == 0:
        print("    no soil band found")
        return
    print("    soil-ish rows %d..%d" % (idx.min(), idx.max()))
    report("frame soil", farr, (int(idx.min()), int(idx.max()) + 1), (0, fw))


if __name__ == "__main__":
    main()
