"""ART-0 cycle: is the drawn grass cap oriented as its canvas, or flipped?

The junction probe found the LEVEL still shows, top to bottom: 4 soil-toned rows, 5 green rows,
then the soil slab. A cap canvas that carries blades on top and roots at the bottom can only
produce that if it is drawn flipped, or if the soil slab's top edge is painted above it. This
prints the two canvases' per-row profile next to the frame's, so the sequence can be compared
row by row instead of argued about.

Usage:  python Tools/art0-cap-orient.py <frame.png> [x] [window]
"""
import os
import sys

from PIL import Image

PROJ = r"E:/Games/2D Game/My project"


def profile(img, rows=None, step=1):
    px = img.load()
    w, h = img.size
    out = []
    for y in range(h):
        if rows is not None and y not in rows:
            continue
        r = g = b = a = 0
        n = 0
        for x in range(0, w, step):
            c = px[x, y]
            r += c[0]
            g += c[1]
            b += c[2]
            a += c[3] if len(c) > 3 else 255
            n += 1
        out.append((y, r / n, g / n, b / n, a / n))
    return out


def show(title, prof):
    print(title)
    for y, r, g, b, a in prof:
        print("  row %2d  rgb %3d,%3d,%3d  a %3d" % (y, r, g, b, a))


def main(frame, x=None, window=200):
    cap = Image.open(os.path.join(PROJ, "Assets/Resources/Level/ground_cap.png")).convert("RGBA")
    fill = Image.open(os.path.join(PROJ, "Assets/Resources/Level/ground_fill.png")).convert("RGBA")
    print("ground_cap.png %dx%d   ground_fill.png %dx%d"
          % (cap.width, cap.height, fill.width, fill.height))
    show("GROUND_CAP canvas (row 0 = the TOP of the PNG):", profile(cap))
    show("GROUND_FILL canvas, first 6 rows:", profile(fill, rows=set(range(6))))
    show("GROUND_FILL canvas, last 3 rows:",
         profile(fill, rows=set(range(fill.height - 3, fill.height))))

    img = Image.open(frame).convert("RGB")
    w, h = img.size
    x0 = int(x) if x is not None else w // 2
    x0 = max(0, min(w - window, x0 - window // 2))
    print("frame %s  window x %d..%d" % (os.path.basename(frame), x0, x0 + window))
    px = img.load()
    print("  row   rgb                       green? brown?")
    for y in range(660, 690):
        r = g = b = 0
        n = 0
        for x in range(x0, x0 + window, 2):
            c = px[x, y]
            r += c[0]
            g += c[1]
            b += c[2]
            n += 1
        r, g, b = r / n, g / n, b / n
        kind = "green" if (g > r + 12 and g > b + 12) else ("brown" if (r > g > b and r - b > 30) else "-")
        print("  %4d  %3d,%3d,%3d   %s" % (y, r, g, b, kind))
    return 0


if __name__ == "__main__":
    a = sys.argv[1:]
    sys.exit(main(a[0], a[1] if len(a) > 1 else None, int(a[2]) if len(a) > 2 else 200))
