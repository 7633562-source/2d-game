"""ART-0 cycle: locate the grass-cap / soil junction in a real LEVEL still and magnify it.

Finding the junction by eye in a 1920x1080 sheet is guessing; this finds it by colour. A row is
GREEN when g > r + 12 and g > b + 12, BROWN when r > g > b and r - b > 30. The probe prints the
per-row mean colour of a 200 px vertical window so the transition zone is read as numbers first,
then writes an 8x NEAREST crop of the junction (green band + the soil under it) for the eye.

Usage:  python Tools/art0-cap-junction.py <frame.png> <out.png> [x] [window]
"""
import os
import sys

from PIL import Image

RUNS = r"E:/Games/2D Game/My project/Trials/Runs"


def classify(px):
    r, g, b = px[0], px[1], px[2]
    if g > r + 12 and g > b + 12:
        return "G"
    if r > g > b and r - b > 30:
        return "B"
    return "."


def main(frame, out, x=None, window=200):
    img = Image.open(frame).convert("RGB")
    w, h = img.size
    x0 = int(x) if x is not None else w // 2
    x0 = max(0, min(w - window, x0 - window // 2))
    px = img.load()

    rows = []
    for y in range(h):
        gr = br = 0
        sr = sg = sb = 0
        for x in range(x0, x0 + window, 4):
            c = px[x, y]
            sr += c[0]
            sg += c[1]
            sb += c[2]
            k = classify(c)
            if k == "G":
                gr += 1
            elif k == "B":
                br += 1
        n = float(len(range(x0, x0 + window, 4)))
        rows.append((y, gr / n, br / n, sr / n, sg / n, sb / n))

    # The junction is the lowest row that is mostly green followed by rows that are mostly brown.
    green_rows = [r for r in rows if r[1] > 0.5]
    brown_rows = [r for r in rows if r[2] > 0.5]
    print("%s  %dx%d  x window %d..%d" % (os.path.basename(frame), w, h, x0, x0 + window))
    print("green rows: %d  first=%s last=%s" % (
        len(green_rows), green_rows[0][0] if green_rows else "-",
        green_rows[-1][0] if green_rows else "-"))
    print("brown rows: %d  first=%s last=%s" % (
        len(brown_rows), brown_rows[0][0] if brown_rows else "-",
        brown_rows[-1][0] if brown_rows else "-"))

    if green_rows:
        yj = green_rows[-1][0]
    elif brown_rows:
        yj = brown_rows[0][0]
    else:
        print("no green and no brown: nothing to measure")
        return 1

    lo = max(0, yj - 12)
    hi = min(h, yj + 12)
    print("rows around the junction (mean RGB over the window, share of green / brown px):")
    for y, gf, bf, r, g, b in rows[lo:hi]:
        mark = " <== junction" if y == yj else ""
        print("  y=%4d  rgb %3d,%3d,%3d  green %.2f brown %.2f%s" % (y, r, g, b, gf, bf, mark))

    y0 = max(0, yj - 22)
    y1 = min(h, yj + 22)
    crop = img.crop((x0, y0, x0 + window, y1))
    zoom = crop.resize((crop.width * 4, crop.height * 4), Image.NEAREST)
    path = os.path.join(RUNS, out)
    zoom.save(path)
    print("JUNCTION %s  crop x %d..%d y %d..%d  4x -> %dx%d"
          % (path, x0, x0 + window, y0, y1, zoom.width, zoom.height))
    return 0


if __name__ == "__main__":
    a = sys.argv[1:]
    sys.exit(main(a[0], a[1], a[2] if len(a) > 2 else None,
                  int(a[3]) if len(a) > 3 else 200))
