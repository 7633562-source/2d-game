"""ART-0 31st pass: is the oak's bole CUT by a horizontal transparent band?

The two zooms of the shipped still (a vision read is a hypothesis, not evidence)
claim "a horizontal seam with 1-2 px of the backdrop showing through" and
"rectangular cut-outs" on the lower trunk. The first probe of this pass could
not test that: it spanned 120 px, so the backdrop it counted was the fork's own
opening between the two limbs.

This probe tests the claim where it can only be true - inside the bole's own
columns:

  * INK = every pixel that is not the gallery's neutral backdrop;
  * for each column x of the bole band it prints the column's VERTICAL runs of
    ink, so a band of backdrop across the trunk shows up as a column with two
    runs where its neighbours have one;
  * enclosed backdrop regions (flood fill from the border) are listed too: a
    real hole in a silhouette is one of those, a cut across a limb is not;
  * the widest row band of backdrop that touches ink on BOTH sides in the same
    row inside one limb is printed as the seam candidate.

Usage:  python Tools/art0-bole-hole-probe.py [still.png]
"""
import os
import sys
from collections import deque

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STILL = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    ROOT, "Trials", "Runs", "art0leaf29_tree.png")
OUT = os.path.join(ROOT, "Trials", "Runs", "art0bole31")
PX_PER_M = 414.21


def is_backdrop(r, g, b):
    return abs(r - g) < 14 and abs(g - b) < 20 and abs(r - b) < 22


def main():
    im = Image.open(STILL).convert("RGB")
    w, h = im.size
    px = im.load()
    os.makedirs(OUT, exist_ok=True)
    print("STILL %s %dx%d  %.2f px/m" % (os.path.basename(STILL), w, h, PX_PER_M))

    ink = bytearray(w * h)
    for y in range(h):
        row = y * w
        for x in range(w):
            r, g, b = px[x, y]
            ink[row + x] = 0 if is_backdrop(r, g, b) else 1

    # Enclosed backdrop: flood fill the backdrop from the border.
    seen = bytearray(w * h)
    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if not ink[y * w + x] and not seen[y * w + x]:
                seen[y * w + x] = 1
                q.append((x, y))
    for y in range(h):
        for x in (0, w - 1):
            if not ink[y * w + x] and not seen[y * w + x]:
                seen[y * w + x] = 1
                q.append((x, y))
    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and not ink[ny * w + nx] and not seen[ny * w + nx]:
                seen[ny * w + nx] = 1
                q.append((nx, ny))

    regions = {}
    for y in range(h):
        for x in range(w):
            if ink[y * w + x] or seen[y * w + x] or (x, y) in regions:
                continue
            comp = []
            q2 = deque([(x, y)])
            seen[y * w + x] = 1
            while q2:
                cx, cy = q2.popleft()
                comp.append((cx, cy))
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = cx + dx, cy + dy
                    if 0 <= nx < w and 0 <= ny < h and not ink[ny * w + nx] and not seen[ny * w + nx]:
                        seen[ny * w + nx] = 1
                        q2.append((nx, ny))
            if len(comp) >= 2:
                xs = [c[0] for c in comp]
                ys = [c[1] for c in comp]
                regions[(x, y)] = (len(comp), min(xs), max(xs), min(ys), max(ys))
    big = [v for v in regions.values() if v[0] >= 4]
    print("ENCLOSED backdrop regions (>=4 px): %d" % len(big))
    for v in sorted(big, key=lambda t: -t[0])[:15]:
        print("  area %5d px  x %4d..%4d  y %4d..%4d  (%.3f x %.3f m)"
              % (v[0], v[1], v[2], v[3], v[4],
                 (v[2] - v[1] + 1) / PX_PER_M, (v[4] - v[3] + 1) / PX_PER_M))

    # The bole band, found as the widest ink column cluster below the crown.
    cols = []
    for x in range(w):
        n = sum(ink[y * w + x] for y in range(300, h))
        cols.append(n)
    peak = max(range(w), key=lambda x: cols[x])
    x0 = x1 = peak
    while x0 > 0 and cols[x0 - 1] > 20:
        x0 -= 1
    while x1 < w - 1 and cols[x1 + 1] > 20:
        x1 += 1
    print("BAND  x %d..%d (%d px = %.3f m) under the crown" % (x0, x1, x1 - x0 + 1,
                                                               (x1 - x0 + 1) / PX_PER_M))

    multi = 0
    for x in range(x0, x1 + 1):
        runs = []
        y = 300
        while y < h:
            if ink[y * w + x]:
                y0 = y
                while y < h and ink[y * w + x]:
                    y += 1
                if y - y0 >= 2:
                    runs.append((y0, y - 1))
            y += 1
        if len(runs) > 1:
            multi += 1
            if multi <= 25:
                print("  x=%4d %d runs: %s" % (x, len(runs), runs[:6]))
    print("CUTS  %d of %d bole columns carry more than one ink run"
          % (multi, x1 - x0 + 1))
    print("OUT   %s" % OUT)


if __name__ == "__main__":
    main()
