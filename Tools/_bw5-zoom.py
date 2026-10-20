#!/usr/bin/env python
"""B-WING-5: zoom sheets for the eyes, from the birdCam 0.85 take (temp helper).

The B-WING-4 sheet tool found the bird as "the darkest blob" — it missed the
translucent bird under the 0.85 camera and cropped half of it out. Here the
bird is found as the pixels that differ from their own ROW's median colour
(the sky is a per-row gradient, the ground is uniform), which holds for a
translucent bird over sky and over ground alike.

Usage: python Tools/_bw5-zoom.py <label> <out.png> <f,f,...> ["<title>"]
Cells: a 520x560 crop centred on the bird, upscaled x2, with the rows' own
telemetry burnt in (t, mode, fanO, chord, elbow, height, CHUTE).
"""
import os
import sys

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")
CAP = 25.0
CELL_W, CELL_H = 520, 560
SCALE = 2


def read_rows(label):
    rows, cols = {}, None
    with open(os.path.join(RUNS, label + ".csv"), encoding="utf-8-sig") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            p = line.split(",")
            if cols is None:
                cols = p
                continue
            d = {}
            for c, v in zip(cols, p):
                try:
                    d[c] = float(v)
                except ValueError:
                    d[c] = v
            rows[round(d.get("t", 0.0) * CAP)] = d
    return rows


def bird_center(im):
    w, h = im.size
    sw, sh = 480, 270
    small = im.resize((sw, sh)).convert("RGB")
    px = small.load()
    xs, ys = [], []
    for y in range(sh):
        row = sorted(px[x, y][0] + px[x, y][1] + px[x, y][2] for x in range(sw))
        med = row[sw // 2]
        for x in range(sw):
            r, g, b = px[x, y]
            if abs(r + g + b - med) > 55:
                xs.append(x)
                ys.append(y)
    if not xs:
        return w // 2, h // 2
    # keep the densest band (the bird), not stray specks: median of the hits
    xs.sort()
    ys.sort()
    cx = xs[len(xs) // 2] * w / sw
    cy = ys[len(ys) // 2] * h / sh
    return cx, cy


def cell(label, n, rows):
    im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, n))).convert("RGB")
    W, H = im.size
    cx, cy = bird_center(im)
    x0 = int(round(cx)) - CELL_W // 2
    y0 = int(round(cy)) - CELL_H // 2
    x0 = max(0, min(W - CELL_W, x0))
    y0 = max(0, min(H - CELL_H, y0))
    crop = im.crop((x0, y0, x0 + CELL_W, y0 + CELL_H)).resize(
        (CELL_W * SCALE, CELL_H * SCALE), Image.LANCZOS)
    d = ImageDraw.Draw(crop)
    d.rectangle([0, 0, CELL_W * SCALE, 46], fill=(0, 0, 0))
    t = n / CAP
    r = rows.get(n)
    line1 = "t=%.2fs  f%d" % (t, n)
    if r:
        line1 += "   mode %d" % int(r.get("mode", -1))
    d.text((6, 3), line1, fill=(255, 255, 0))
    line2 = ""
    if r:
        line2 = "fanO %.2f  chord %.4f  el %.0f  h %.2f" % (
            r.get("fanOpen", 0), r.get("fanChord", 0), r.get("elbowDeg", 0),
            r.get("heightAboveSupport", 0))
        if r.get("chuteOn", 0):
            line2 += "  CHUTE"
    d.text((6, 24), line2, fill=(180, 255, 180))
    return crop


def main():
    label, out, frames_s = sys.argv[1], sys.argv[2], sys.argv[3]
    title = sys.argv[4] if len(sys.argv) > 4 else ""
    rows = read_rows(label)
    frames = [int(v) for v in frames_s.split(",")]
    cells = [cell(label, n, rows) for n in frames]
    cols = min(4, len(cells))
    nrows = (len(cells) + cols - 1) // cols
    cw, ch = cells[0].size
    sheet = Image.new("RGB", (cols * cw, nrows * ch + 28), (20, 20, 20))
    d = ImageDraw.Draw(sheet)
    d.text((8, 6), "%s  %s  %s  t=%.2f..%.2f s"
           % (label, title, frames_s, frames[0] / CAP, frames[-1] / CAP), fill=(255, 220, 120))
    for i, c in enumerate(cells):
        sheet.paste(c, ((i % cols) * cw, 28 + (i // cols) * ch))
    sheet.save(out)
    print("zoom: %d frames %s -> %s (%d bytes)" % (len(frames), frames, out, os.path.getsize(out)))


if __name__ == "__main__":
    main()
