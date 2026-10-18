# -*- coding: utf-8 -*-
"""B-PERCH-2 zoom sheet: the perch + feet region of the static twin, one camera, one crop.

Usage: python Tools/_pch2-zoom.py <out.png> <zoom> <x0,y0,x1,y1> <take:frame:label> ...
Grid: white ticks every 10 px, labels: x every 20 (magenta), y every 20 (cyan).
"""
import sys
from PIL import Image, ImageDraw

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"


def panel(take, frame, label, box, z):
    x0, y0, x1, y1 = box
    a = Image.open(f"{RUNS}\\shot_{take}_{frame}.png").convert("RGB")
    im = a.crop((x0, y0, x1, y1)).resize(((x1 - x0) * z, (y1 - y0) * z), Image.NEAREST)
    d = ImageDraw.Draw(im)
    for gx in range(x0 - x0 % 10 + 10, x1, 10):
        X = (gx - x0) * z
        d.line([(X, 0), (X, 6)], fill=(255, 255, 255), width=1)
        if gx % 20 == 0:
            d.text((X + 2, 7), str(gx), fill=(255, 0, 255))
    for gy in range(y0 - y0 % 20 + 20, y1, 20):
        Y = (gy - y0) * z
        d.line([(0, Y), (6, Y)], fill=(255, 255, 255), width=1)
        d.text((8, Y + 2), str(gy), fill=(0, 255, 255))
    band = 22
    out = Image.new("RGB", (im.width, im.height + band), (12, 12, 12))
    out.paste(im, (0, band))
    ImageDraw.Draw(out).text((5, 5), label, fill=(255, 230, 80))
    return out


def main():
    out_path, z = sys.argv[1], int(sys.argv[2])
    box = tuple(int(v) for v in sys.argv[3].split(","))
    panels = []
    for spec in sys.argv[4:]:
        take, fr, label = spec.split(":", 2)
        panels.append(panel(take, int(fr), label, box, z))
    gap = 8
    W = sum(p.width for p in panels) + gap * (len(panels) - 1)
    H = max(p.height for p in panels)
    out = Image.new("RGB", (W, H), (0, 0, 0))
    x = 0
    for p in panels:
        out.paste(p, (x, 0))
        x += p.width + gap
    out.save(out_path)
    print("saved", out_path, out.size)


if __name__ == "__main__":
    main()
