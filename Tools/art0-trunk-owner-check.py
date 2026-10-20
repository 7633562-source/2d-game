#!/usr/bin/env python3
"""ART-0 (30th pass) - read the trunk probe's own frames, independently of the C# that made them.

`Assets/Editor/ArtTrunkProbe.cs` renders the "oak close" lens three times:

  `art0trunk30-probe.png`       every renderer of the oak as the dress left it
  `art0trunk30-barkonly.png`    the same frame with every leaf-MATERIAL renderer switched off
  `art0trunk30-owners-off.png`  the same frame with the renderers the probe named as owners off

and writes its measurement lines to `art0trunk30-probe-lines.txt`. This tool answers the
questions from those bytes and those lines, with its own masks (it repeats the probe's
thresholds rather than importing them):

  1. LENS - is the probe's camera the shipped still's camera? Compares `-probe.png` with
     `art0leaf29_tree.png` (`art0-frame-diff.py` cannot: that compares two CAPTURES).
  2. BOLE - where the trunk is, from the bark mask of `barkOnly` (runs dilated by
     DILATE px, so a bough leaving the column does not swallow the background).
  3. GREEN-ON-BOLE - green pixels inside that column, in all three frames. The
     difference between `base` and `barkOnly` is what the leaf MATERIAL paints on the
     bole; what SURVIVES `barkOnly` is painted by something else, and can never be the
     leaf material's work. That number is the honest limit of the material's ownership.
  4. GROUPS - the probe's per-part ownership lines summed by cluster (the ancestor whose
     name carries a part group), because one tuft is ten sub-parts and a per-part count
     is fragmented by construction.

Usage:  python Tools/art0-trunk-owner-check.py [still]     (still defaults to art0leaf29_tree)
"""
import os
import re
import sys
from collections import defaultdict

import numpy as np
from PIL import Image

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(PROJ, "Trials", "Runs")

GREEN_MARGIN = 24
GREEN_FLOOR = 60
BARK_RG, BARK_GB = 12, 8
MIN_CLUSTER = 12
DILATE = 6                 # px: the bole is the bark column, slightly widened
ROW_TOP = 30               # above this is the crown, not the bole
ROW_BOTTOM = 985           # the ground strip starts here: the grass cap is green, and
                           # soil passes the bark test, so rows below it are not the bole


def load(name):
    path = os.path.join(RUNS, name if name.endswith(".png") else name + ".png")
    if not os.path.exists(path):
        raise SystemExit("FAIL no such frame: " + path)
    return np.asarray(Image.open(path).convert("RGB"), dtype=np.int16)


def green(rgb):
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    return (g > r + GREEN_MARGIN) & (g > b + GREEN_MARGIN) & (g > GREEN_FLOOR)


def bark(rgb):
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    return (r > g + BARK_RG) & (g > b + BARK_GB) & (r > 60) & (r < 210)


def column_mask(barkonly):
    """The bole: every bark RUN of every row of the bark-only frame, widened by DILATE."""
    bm = bark(barkonly)
    h, w = bm.shape
    out = np.zeros_like(bm)
    for y in range(ROW_TOP, min(ROW_BOTTOM + 1, h)):
        row = np.flatnonzero(bm[y])
        if row.size == 0:
            continue
        split = np.flatnonzero(np.diff(row) > 1)
        for run in np.split(row, split + 1):
            if run.size < 2:
                continue
            out[y, max(0, run[0] - DILATE):min(w, run[-1] + 1 + DILATE)] = True
    return out


def clusters(mask, min_px=MIN_CLUSTER):
    h, w = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    out = []
    ys, xs = np.nonzero(mask)
    for y0, x0 in zip(ys, xs):
        if seen[y0, x0]:
            continue
        stack = [(y0, x0)]
        seen[y0, x0] = True
        cells = []
        while stack:
            y, x = stack.pop()
            cells.append((y, x))
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ny, nx = y + dy, x + dx
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        if len(cells) >= min_px:
            cy = [c[0] for c in cells]
            cx = [c[1] for c in cells]
            out.append((min(cx), min(cy), max(cx) - min(cx) + 1, max(cy) - min(cy) + 1, len(cells)))
    out.sort(key=lambda c: -c[4])
    return out


CAND = re.compile(r"cand path=(\S+) sprite=(\S+) world=([\d.]+)x([\d.]+)m min=([\d.]+)m "
                  r"order=(-?\d+) ownedChipPixels=(\d+) changedPixels=(\d+)")


def group_of(path):
    """The cluster a part belongs to: the nearest ancestor-or-self whose name is not a
    bare leaf sub-part word. 'Spur0_Tuft2_LobeL1' under 'Spur0_Tuft2' is one cluster."""
    parts = path.split("/")
    for name in reversed(parts):
        if re.match(r"(?i)(spur|tuft|crown|fork|spray|raceme|catkin|leafcluster|bud)", name):
            return name
    return parts[-1] if parts else path


def lines_report():
    p = os.path.join(RUNS, "art0trunk30-probe-lines.txt")
    if not os.path.exists(p):
        return
    groups = defaultdict(int)
    parts = defaultdict(int)
    total_owner_px = 0
    n = 0
    for line in open(p, encoding="utf-8", errors="replace"):
        m = CAND.search(line)
        if not m:
            continue
        n += 1
        owned = int(m.group(7))
        if owned <= 0:
            continue
        total_owner_px += owned
        groups[group_of(m.group(1))] += owned
        parts[m.group(1)] += owned
    print("  LINES  candidate lines=%d with owned>0 ; attributed=%d px" % (n, total_owner_px))
    for name, px in sorted(groups.items(), key=lambda kv: -kv[1])[:8]:
        print("        group %-28s owned=%d px" % (name, px))
    for name, px in sorted(parts.items(), key=lambda kv: -kv[1])[:5]:
        print("        part  owned=%3d px  %s" % (px, name))


def main():
    still = sys.argv[1] if len(sys.argv) > 1 else "art0leaf29_tree"
    base = load("art0trunk30-probe")
    barkonly = load("art0trunk30-barkonly")
    cardsoff = load("art0trunk30-cardsoff")
    bothoff = load("art0trunk30-bothoff")
    owners_off = load("art0trunk30-owners-off")

    print("ART-0-TRUNK-CHECK frame=%s still=%s" % (base.shape, still))
    ref = load(still)
    if ref.shape == base.shape:
        diff = np.abs(ref.astype(np.int32) - base.astype(np.int32)).sum(axis=2)
        changed = int((diff > 26).sum())
        print("  LENS   probe vs %s: %d px differ (%.3f%%)" % (still, changed, 100.0 * changed / diff.size))
        if changed:
            ys, xs = np.nonzero(diff > 26)
            print("         differing box x %d..%d y %d..%d maxdelta=%d"
                  % (xs.min(), xs.max(), ys.min(), ys.max(), int(diff.max())))
    else:
        print("  LENS   size mismatch %s vs %s" % (base.shape, ref.shape))

    col = column_mask(barkonly)
    print("  BOLE   column pixels=%d (rows %d..%d, dilation %d px)" % (int(col.sum()), ROW_TOP, ROW_BOTTOM, DILATE))

    g_base = green(base) & col
    g_barkonly = green(barkonly) & col
    g_cardsoff = green(cardsoff) & col
    g_off = green(owners_off) & col
    n_base = int(g_base.sum())
    print("  GREEN-ON-BOLE (the column mask, %d px)" % int(col.sum()))
    print("         base                     %5d px in %d clusters" % (n_base, len(clusters(g_base))))
    print("         leaf material OFF        %5d px  -> the material paints %5d px (%.1f %%)"
          % (int(g_barkonly.sum()), n_base - int(g_barkonly.sum()),
             100.0 * (n_base - g_barkonly.sum()) / max(1, n_base)))
    print("         leaf CARDS off           %5d px  -> the cards paint %5d px (%.1f %%)"
          % (int(g_cardsoff.sum()), n_base - int(g_cardsoff.sum()),
             100.0 * (n_base - g_cardsoff.sum()) / max(1, n_base)))
    both = green(barkonly) & col
    print("         the probe's 174 owners off %5d px (a per-part count; a tuft is ten parts,"
          % int(g_off.sum()))
    print("                                     so each pixel stays green under its siblings)")
    neither = green(base) & col & ~green(barkonly) & ~green(cardsoff)
    print("  NEITHER  green on the bole that is NOT the material and NOT a card: %d px"
          % int(neither.sum()))
    both_off = green(bothoff) & col
    print("           with both leaf languages off: %d px of green left on the bole"
          % int(both_off.sum()))
    for name in ("art0trunk30-probe", "art0trunk30-barkonly", "art0trunk30-cardsoff",
                 "art0trunk30-bothoff", "art0trunk30-owners-off"):
        p = os.path.join(RUNS, name + ".png")
        if os.path.exists(p):
            print("  frame %-30s %8d B" % (name + ".png", os.path.getsize(p)))
    print("  chips on the bole, base frame:")
    for c in clusters(g_base)[:10]:
        print("         box %dx%d px at (%d,%d) ink=%d" % (c[2], c[3], c[0], c[1], c[4]))

    # The complaint's own word was "identical ... repeated". Test it at native resolution:
    # two marks are a literal repeat only if the same pixels sit in the same size box.
    cl = clusters(g_base)
    native_exact = 0
    pairs = 0
    by_size = {}
    for box in cl:
        x, y, w, h, _ = box
        by_size.setdefault((w, h), []).append(base[y:y + h, x:x + w].astype(np.int16))
    for size, crops in by_size.items():
        for i in range(len(crops)):
            for j in range(i + 1, len(crops)):
                pairs += 1
                if np.array_equal(crops[i], crops[j]):
                    native_exact += 1
    small = np.asarray(
        [np.asarray(Image.fromarray(base[y:y + h, x:x + w].astype(np.uint8)).resize((32, 32), Image.NEAREST),
                    dtype=np.int16) for x, y, w, h, _ in cl])
    loose = 0
    for i in range(len(small)):
        for j in range(i + 1, len(small)):
            if np.abs(small[i] - small[j]).max() <= 6:
                loose += 1
    print("  REPEATS  clusters=%d, same-size pairs=%d, byte-identical at native size=%d,"
          % (len(cl), pairs, native_exact))
    print("           pairs within 6 levels after a 32x32 nearest resample=%d" % loose)

    lines_report()
    return 0


if __name__ == "__main__":
    sys.exit(main())
