#!/usr/bin/env python3
"""ART-0 leaf-field probe: does the leaf material read as foliage or as one macro leaf?

Two things are measured, both from the shipped bytes only (no Unity, no trial):

  wrap      - the wrap seam's step against the tile's own grain, and where that step
              RANKS among every step a 3 x 3 repeat of the tile shows. Rank 100 % means
              "the worst step in the whole sheet is at the seam" (a line the eye finds);
              a rank inside the interior distribution means the seam is one more
              neighbour step. This is the metric the ART-0 card's rolled-crop trap
              needed, and it is kept unchanged from Tools/art0-leafwrap-probe.py.

  feature   - the scale of the repeating DETAIL, as the autocorrelation half-width of
              the luminance field: the distance at which two pixels of the tile stop
              being correlated (r < 0.5). A macro photograph of a single leaf has one
              feature much larger than the tile, so the half-width runs into the tile
              size; a foliage field of many small leaves decorrelates within one leaf.
              Also prints the count of dark "leaf gap" blobs per tile, which is what the
              eye counts when it decides whether a crown repeats.

Usage:  python Tools/art0-leaf-field-probe.py OLD.png NEW.png [name]
"""

import sys

import numpy as np
from PIL import Image


def cut_inner(a, axis):
    cut = np.abs(a.take(0, axis=axis).astype(np.int16) - a.take(-1, axis=axis).astype(np.int16))
    inner = np.abs(np.diff(a.astype(np.int16), axis=axis))
    return float(cut.mean()), float(inner.mean())


def seam_rank(a, axis, repeat=3):
    """Rank (0-100 %) of the wrap step among every step of a `repeat` x `repeat` tiling."""
    sheet = np.concatenate([np.concatenate([a] * repeat, axis=1)] * repeat, axis=0)
    steps = np.abs(np.diff(sheet.astype(np.int16), axis=axis)).mean(axis=2)
    n = a.shape[axis]
    seam_idx = [(k + 1) * n - 1 for k in range(repeat - 1)]
    seam = np.array([steps.take(i, axis=axis).mean() for i in seam_idx])
    keep = np.setdiff1d(np.arange(steps.shape[axis]), np.array(seam_idx))
    flat = np.sort(steps.take(keep, axis=axis).ravel())
    pct = [float((flat < s).mean() * 100.0) for s in seam]
    return float(np.mean(seam)), float(np.max(pct)), float(np.median(pct))


def seam_vs_pairs(a, axis):
    """Where the wrap step sits among the tile's OWN neighbour steps, as a percentile.

    This is the fair form of the rank: the 3 x 3 rank below is a percentile over every
    single pixel step, and a painted tile's interior is mostly flat, so a sub-level wrap
    step lands high in a distribution of near-zero steps. Here the wrap is compared with
    the 511 column (or row) pair differences of the tile itself - each of them a mean over
    the whole edge. A wrap at the median of those is, by construction, not special: one
    edge pair in 512 of a textured picture is as busy as the next.
    """
    d = np.abs(np.diff(a.astype(np.int16), axis=axis)).mean(axis=2)
    pairs = d.mean(axis=0) if axis == 1 else d.mean(axis=1)
    seam = float(np.abs(a.take(0, axis=axis).astype(np.int16)
                        - a.take(-1, axis=axis).astype(np.int16)).mean())
    return seam, float((pairs < seam).mean() * 100.0), float(pairs.max()), float(pairs.mean())


def half_width(lum, axis):
    """Autocorrelation half-width in px along one axis: where r first drops below 0.5."""
    v = lum.mean(axis=1 - axis)
    v = v - v.mean()
    if not np.any(v):
        return 0.0
    n = v.shape[0]
    ac = np.correlate(v, v, mode="full")[n - 1:]
    ac = ac / ac[0]
    below = np.nonzero(ac < 0.5)[0]
    return float(below[0]) if below.size else float(n)


def gap_blobs(lum):
    """Dark blobs per tile: the shadows between leaves, 4-connected, >= 12 px."""
    dark = lum < (lum.mean() - 0.6 * lum.std())
    h, w = dark.shape
    seen = np.zeros_like(dark)
    count = 0
    for y0 in range(h):
        for x0 in range(w):
            if not dark[y0, x0] or seen[y0, x0]:
                continue
            stack = [(y0, x0)]
            seen[y0, x0] = True
            size = 0
            while stack:
                y, x = stack.pop()
                size += 1
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    ny, nx = (y + dy) % h, (x + dx) % w
                    if dark[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
            if size >= 12:
                count += 1
    return count


def report(tag, path):
    img = Image.open(path).convert("RGB")
    a = np.asarray(img).astype(np.float64)
    rgb = a.astype(np.int16)
    lum = a.mean(axis=2)
    print("%s  %s  %dx%d  mean rgb %s  std %.1f" % (
        tag, path, img.size[0], img.size[1],
        np.round(a.reshape(-1, 3).mean(axis=0), 1).tolist(), float(lum.std())))
    for axis, ax in ((1, "X"), (0, "Y")):
        cut, inner = cut_inner(a, axis)
        seam, pct_max, pct_med = seam_rank(a, axis)
        sp, sp_pct, sp_max, sp_mean = seam_vs_pairs(a, axis)
        print("  %s: cut %.2f inner %.2f ratio %.2f | seam step %.2f rank max %.1f%% "
              "median %.1f%% | half-width %.0f px"
              % (ax, cut, inner, cut / max(inner, 1e-9), seam, pct_max, pct_med,
                 half_width(lum, axis)))
        print("     vs own edge pairs: seam %.2f at percentile %.1f%% of %d pairs "
              "(busiest %.2f, mean %.2f), %.2f %% of the 0-255 range"
              % (sp, sp_pct, a.shape[axis] - 1, sp_max, sp_mean, 100.0 * sp / 255.0))
    print("  dark blobs per tile: %d | distinct colours: %d"
          % (gap_blobs(lum), len(set(map(tuple, rgb.reshape(-1, 3).tolist())))))
    return a


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    old = report("OLD", sys.argv[1])
    new = report("NEW", sys.argv[2])
    if old.shape == new.shape:
        d = np.abs(old - new)
        print("old vs new: mean |d| %.2f  max %.0f  px changed %d of %d (%.1f %%)"
              % (d.mean(), d.max(), int((d.max(axis=2) > 0).sum()), d.shape[0] * d.shape[1],
                 100.0 * (d.max(axis=2) > 0).mean()))
        print("colour drift: old mean %s -> new mean %s" % (
            np.round(old.reshape(-1, 3).mean(axis=0), 1).tolist(),
            np.round(new.reshape(-1, 3).mean(axis=0), 1).tolist()))
    return 0


if __name__ == "__main__":
    sys.exit(main())
