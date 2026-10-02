#!/usr/bin/env python
"""B-WING-4 close: the take's zoom sheets for the eyes (temp helper).

Usage: python Tools/_bw4-zoom.py <label> <out.png> <t0:t1> ["<title>"]
Six frames evenly across the window; each cell = a 480x510 crop centred on the
bird, upscaled x2, with the rows' own telemetry burnt in.

The bird is found as the darkest blob above the ground line (the take's sky is
light; the ground reads as a near-solid dark band).
"""
import os
import sys

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")
CAP = 25.0
CELL_W, CELL_H = 480, 510
SCALE = 2


def read_rows(label):
    rows, cols = [], None
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
            rows.append(d)
    return rows


def bird_center(im):
    w, h = im.size
    sw, sh = 192, 108
    small = im.resize((sw, sh)).convert("L")
    px = small.load()
    # ground line = the topmost row that is mostly dark
    ground = sh
    for y in range(sh):
        dark = sum(1 for x in range(sw) if px[x, y] < 130)
        if dark > sw * 0.9:
            ground = y
            break
    best = None
    for y in range(max(0, ground - 60)):
        for x in range(sw):
            if px[x, y] < 150:
                if best is None:
                    best = [x, y, x, y]
                else:
                    best = [min(best[0], x), min(best[1], y),
                            max(best[2], x), max(best[3], y)]
    if best is None:
        return w // 2, h // 2
    cx = (best[0] + best[2]) / 2.0 * w / sw
    cy = (best[1] + best[3]) / 2.0 * h / sh
    return cx, cy


def cell(label, n, rows_by_t):
    im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, n))).convert("RGB")
    W, H = im.size
    cx, cy = bird_center(im)
    x0 = int(round(cx)) - CELL_W // 2
    y0 = int(round(cy)) - int(CELL_H * 0.42)
    x0 = max(0, min(W - CELL_W, x0))
    y0 = max(0, min(H - CELL_H, y0))
    crop = im.crop((x0, y0, x0 + CELL_W, y0 + CELL_H)).resize(
        (CELL_W * SCALE, CELL_H * SCALE), Image.LANCZOS)
    d = ImageDraw.Draw(crop)
    d.rectangle([0, 0, CELL_W * SCALE, 46], fill=(0, 0, 0))
    t = n / CAP
    r = rows_by_t.get(n)
    line1 = "t=%.2fs  f%d" % (t, n)
    if r:
        line1 += "   mode %d" % int(r.get("mode", -1))
    d.text((6, 3), line1, fill=(255, 255, 0))
    line2 = ""
    if r:
        line2 = "fanO %.2f  chord %.4f  wr %.0f  el %.0f  reach %.3f" % (
            r.get("fanOpen", 0), r.get("fanChord", 0), r.get("wristDeg", 0),
            r.get("elbowDeg", 0), r.get("wingReach", 0))
        if r.get("chuteOn", 0):
            line2 += "  CHUTE"
    d.text((6, 24), line2, fill=(180, 255, 180))
    return crop


def main():
    label, out, win = sys.argv[1], sys.argv[2], sys.argv[3]
    title = sys.argv[4] if len(sys.argv) > 4 else ""
    rows = read_rows(label)
    by_n = {}
    for r in rows:
        by_n[int(round(r.get("t", 0) * CAP))] = r
    if "," in win:
        frames = [int(v) for v in win.split(",")]
    else:
        t0, t1 = [float(v) for v in win.split(":")]
        f0, f1 = int(round(t0 * CAP)), int(round(t1 * CAP))
        frames = [f0 + int(round(i * (f1 - f0) / 5.0)) for i in range(6)]
    cells = [cell(label, n, by_n) for n in frames]
    cols = min(4, len(cells))
    rows_n = (len(cells) + cols - 1) // cols
    cw, ch = cells[0].size
    sheet = Image.new("RGB", (cols * cw, rows_n * ch + 28), (20, 20, 20))
    d = ImageDraw.Draw(sheet)
    d.text((8, 6), "%s  %s  %s  t=%.2f..%.2f s" % (label, title, win, frames[0] / CAP, frames[-1] / CAP),
           fill=(255, 220, 120))
    for i, c in enumerate(cells):
        sheet.paste(c, ((i % cols) * cw, 28 + (i // cols) * ch))
    sheet.save(out)
    print("zoom: %d frames %s -> %s (%d bytes)" % (len(frames), frames, out, os.path.getsize(out)))


if __name__ == "__main__":
    main()
