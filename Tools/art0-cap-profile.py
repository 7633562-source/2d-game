"""ART-0 cap silhouette probe: measure the grass strip as the FRAME shows it.

The canvas metrics (Tools/gen-level-textures.py prints its own) judge the 128x14 PNG.
What the player draws is that strip at ~64.7 px/m in the level still, so this probe
reads the silhouette out of the real 1920x1080 frame instead: for every column it
finds the topmost grass-coloured pixel inside a search band, then reports the
amplitude of the resulting profile.

  std          - how much the top moves at all across the strip
  distinct     - how many different top rows exist
  tuftCols     - columns whose top is within 1 px of a neighbour (tufts, not a fence)
  spikeCols    - columns whose top is more than 2 px above BOTH neighbours (fence teeth)
  period       - best period of the profile (autocorrelation peak over the strip),
                 as a fraction of the search width; the drawn cap repeats every 1.28 m

Usage:  python Tools/art0-cap-profile.py <label> [x0] [x1] [yTop] [yBottom] [subdir]
"""
import os
import sys

import numpy as np
from PIL import Image

PROJ = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJ, "Trials/Runs")


def profile(img, x0, x1, y0, y1, still):
    a = np.asarray(img.convert("RGB")).astype(np.int16)
    band = a[y0:y1, x0:x1]
    g = band[:, :, 1]
    r = band[:, :, 0]
    b = band[:, :, 2]
    grass = (g > r + 6) & (g > b + 6) & (g > 55)
    tops = np.full(band.shape[1], -1)
    for x in range(band.shape[1]):
        col = np.nonzero(grass[:, x])[0]
        if col.size:
            tops[x] = col[0]
    valid = tops >= 0
    if not valid.any():
        print("%-16s no grass found in the band" % still)
        return
    t = tops[valid].astype(float)
    d = np.abs(np.diff(t))
    neigh_ok = 0
    for i in range(1, len(t) - 1):
        if abs(t[i] - t[i - 1]) <= 1 or abs(t[i] - t[i + 1]) <= 1:
            neigh_ok += 1
    spikes = sum(1 for i in range(1, len(t) - 1)
                 if t[i] < t[i - 1] - 2 and t[i] < t[i + 1] - 2)
    # best period via autocorrelation of the centred profile
    c = t - t.mean()
    ac = np.correlate(c, c, "full")[len(c) - 1:]
    ac = ac / (ac[0] if ac[0] else 1)
    best, bestval = 0, -2
    for lag in range(8, len(ac)):
        if ac[lag] > bestval:
            best, bestval = lag, ac[lag]
    print("%-16s cols=%-4d top min/max/mean=%.0f/%.0f/%.1f std=%.2f distinct=%d "
          "tuftCols=%.0f%% spikeCols=%d bestLag=%d (r=%.2f)"
          % (still, int(valid.sum()), t.min(), t.max(), t.mean(), t.std(),
             len(set(t.astype(int))), 100.0 * neigh_ok / max(1, len(t) - 2),
             spikes, best, bestval))
    print("                 profile first 128 cols: %s" % "".join(str(int(v) % 10) for v in t[:128]))


def main(label, x0, x1, y0, y1, sub=""):
    base = os.path.join(RUNS, sub) if sub else RUNS
    print("=== cap silhouette from the frame: %s (band x %d-%d y %d-%d) ===" % (label, x0, x1, y0, y1))
    for fn in sorted(f for f in os.listdir(base) if f.startswith(label + "_") and f.endswith(".png")):
        still = fn[len(label) + 1:-4]
        if still.startswith("shot_"):
            continue
        profile(Image.open(os.path.join(base, fn)), x0, x1, y0, y1, still)


if __name__ == "__main__":
    a = sys.argv[1:]
    main(a[0], int(a[1]) if len(a) > 1 else 300, int(a[2]) if len(a) > 2 else 1600,
         int(a[3]) if len(a) > 3 else 655, int(a[4]) if len(a) > 4 else 690,
         a[5] if len(a) > 5 else "")
