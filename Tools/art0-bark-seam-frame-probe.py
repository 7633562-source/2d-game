#!/usr/bin/env python3
"""ART-0 (29th cycle) - is there a seam at the bark tile's own period ON THE TRUNK?

The standing question the 17th pass' frame probe could not answer: `ArtParts.Dress` builds
every wood renderer from `ArtLibrary.Tiled(ArtLibrary.Bark, 0.5f)` - one Sprite whose native
size is exactly 0.5 m - and draws it in Tiled mode, so a flat bark surface MUST repeat every
0.5 m. `Tools/art0-bark-rhythm-probe.py` looked for that repeat in the crown-crossed band of
`art0leaffield_tree.png` and returned "no signature", which it correctly refused to read as a
verdict (the crown's cut planes cross the trunk there).

This tool asks the sharper question - not "does the surface repeat" (it must) but "is there a
row where the picture STEPS": the tile's own wrap seam. It measures, per row of the trunk
band, the mean absolute difference to the next row, over the bark-masked pixels only; a seam
is a row whose step is an outlier against the band's own distribution, and the tile's Y wrap
sits every `0.5 * pxPerMetre` rows inside each part.

  * the row-to-row step profile of the trunk band, its median and p95,
  * the rows above `OUTLIER` x the p95, with their world height and their distance to the
    nearest multiple of the tile period (a seam must land at a period line; the rig's own
    segment boundaries are free to land anywhere),
  * the same profile for the leaf-dressed chips EXCLUDED (the green mask), so a green chip
    edge is never reported as a bark seam.

Usage: python Tools/art0-bark-seam-frame-probe.py [label]
"""
import os
import sys

import numpy as np
from PIL import Image

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(PROJ, "Trials", "Runs")

GROUND_ROW = 1010
GREEN_MARGIN = 24
OUTLIER = 3.0
MIN_ROW_PIXELS = 12


def load(label):
    path = os.path.join(RUNS, label + "_tree.png")
    if not os.path.exists(path):
        raise SystemExit("FAIL no such still: " + path)
    return np.asarray(Image.open(path).convert("RGB"), dtype=np.float64)


def main():
    label = sys.argv[1] if len(sys.argv) > 1 else "art0leaf28"
    px_per_metre = float(sys.argv[2]) if len(sys.argv) > 2 else 414.2135620117188
    rgb = load(label)
    band = rgb[:GROUND_ROW]

    r, g, b = band[:, :, 0], band[:, :, 1], band[:, :, 2]
    bark = (r > g + 12) & (g > b + 8) & (r > 60) & (r < 210)
    green = (g > r + GREEN_MARGIN) & (g > b + GREEN_MARGIN) & (g > 60)
    bark = bark & ~green
    rows = np.nonzero(bark.sum(axis=1) >= MIN_ROW_PIXELS)[0]
    rows = rows[rows + 1 < bark.shape[0]]
    if len(rows) == 0:
        raise SystemExit("FAIL no bark rows in " + label)

    step = np.full(GROUND_ROW, np.nan)
    for y in rows:
        m = bark[y] & bark[y + 1]
        if m.sum() < MIN_ROW_PIXELS:
            continue
        d = np.abs(band[y + 1][m] - band[y][m]).mean(axis=1)
        step[y] = d.mean()

    valid = ~np.isnan(step)
    med = float(np.nanmedian(step))
    p95 = float(np.nanpercentile(step[valid], 95))
    period = 0.5 * px_per_metre
    print("ART-0-BARK-SEAM label=%s rows=%d  median row step=%.2f  p95=%.2f  bar=%.2f (%.1fx p95)"
          % (label, int(valid.sum()), med, p95, OUTLIER * p95, OUTLIER))
    print("ART-0-BARK-SEAM tile period = 0.5 m = %.2f px ; trunk rows %d..%d" % (period, rows.min(), rows.max()))

    order = np.argsort(-np.nan_to_num(step, nan=0.0))
    print("ART-0-BARK-SEAM top rows by step:")
    for y in order[:14]:
        if np.isnan(step[y]):
            continue
        h = (GROUND_ROW - y) / px_per_metre
        to_grid = (y % period)
        to_grid = min(to_grid, period - to_grid) / px_per_metre * 1000.0
        flag = "SEAM?" if step[y] >= OUTLIER * p95 else ""
        print("    row %4d  h=%.3f m  step=%.2f (%.1fx median)  %4.1f mm from the period grid  %s"
              % (y, h, step[y], step[y] / med, to_grid, flag))

    # the trunk's own column limits, so the band is named and not guessed
    cols = np.nonzero(bark.any(axis=0))[0]
    print("ART-0-BARK-SEAM bark columns x %d..%d" % (cols.min(), cols.max()))
    prof = np.nan_to_num(step)
    prof = prof - prof.mean()
    ac = float((prof[:-int(period)] * prof[int(period):]).sum() / max(1e-9, (prof * prof).sum()))
    print("ART-0-BARK-SEAM autocorrelation of the row-step profile at one tile period: %.3f" % ac)
    return 0


if __name__ == "__main__":
    sys.exit(main())
