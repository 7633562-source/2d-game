#!/usr/bin/env python3
"""ART-0 cycle: cut the before/after windows of two capture labels for judging.

Usage:  python art0-stack-window.py <labelOld> <labelNew> <name> <x0,y0,x1,y1> <out.png> [scale]

Both stills are cropped to the same window, laid side by side with a 12 px gutter
and a label bar, so one read compares them. The window is in ORIGINAL pixels of the
1920x1080 still. `scale` magnifies NEAREST (default 1).
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

PROJ = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJ, "Trials", "Runs")


def window(img, box):
    x0, y0, x1, y1 = box
    return img.crop((x0, y0, x1, y1))


def main():
    if len(sys.argv) < 6:
        raise SystemExit(__doc__)
    labels = (sys.argv[1], sys.argv[2])
    name = sys.argv[3]
    box = tuple(int(v) for v in sys.argv[4].split(","))
    out = sys.argv[5]
    scale = int(sys.argv[6]) if len(sys.argv) > 6 else 1

    crops = []
    for label in labels:
        path = os.path.join(RUNS, "%s_%s.png" % (label, name))
        img = Image.open(path).convert("RGB")
        crops.append(window(img, box))

    w, h = crops[0].size
    gutter = 12
    sheet = Image.new("RGB", ((w * 2 + gutter) * scale, h * scale + 26), (18, 18, 20))
    draw = ImageDraw.Draw(sheet)
    draw.text((6, 6), labels[0], fill=(255, 210, 120))
    draw.text(((w + gutter) * scale + 6, 6), labels[1], fill=(140, 210, 255))
    for i, crop in enumerate(crops):
        if scale != 1:
            crop = crop.resize((w * scale, h * scale), Image.NEAREST)
        sheet.paste(crop, ((w + gutter) * i * scale, 26))
    sheet.save(out)
    print("wrote %s  window=%s  scale=%d  (%dx%d per panel)"
          % (out, box, scale, w * scale, h * scale))


if __name__ == "__main__":
    main()
