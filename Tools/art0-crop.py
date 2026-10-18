"""ART-0 crop / contact sheet: pull a region out of a real 1920x1080 capture frame
and upscale it with NEAREST (a smooth filter would hide the pixel pattern the art is
judged by). Several crops can be stacked into one sheet, because one image is easier
to read than four files.

Usage:
  python Tools/art0-crop.py <label> <still> <x0> <y0> <x1> <y1> <zoom> <out>
  python Tools/art0-crop.py <label> <still> <x0,y0,x1,y1> <x0,y0,x1,y1> ... --zoom N --out name

Sheet crops are stacked vertically with a magenta separator.
"""
import os
import sys

from PIL import Image

PROJ = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJ, "Trials/Runs")


def crop(img, box, zoom):
    c = img.crop(box)
    return c.resize((c.width * zoom, c.height * zoom), Image.NEAREST)


def main(argv):
    label, still = argv[0], argv[1]
    zoom, out = 3, "%s_%s_sheet.png" % (label, still)
    boxes = []
    rest = argv[2:]
    i = 0
    while i < len(rest):
        a = rest[i]
        if a == "--zoom":
            zoom = int(rest[i + 1])
            i += 2
            continue
        if a == "--out":
            out = rest[i + 1]
            i += 2
            continue
        boxes.append(tuple(int(v) for v in a.split(",")))
        i += 1
    if not boxes:
        print("no crop boxes given")
        return 1
    img = Image.open(os.path.join(RUNS, "%s_%s.png" % (label, still))).convert("RGB")
    parts = [crop(img, b, zoom) for b in boxes]
    w = max(p.width for p in parts)
    h = sum(p.height for p in parts) + 4 * (len(parts) - 1)
    sheet = Image.new("RGB", (w, h), (255, 0, 255))
    y = 0
    for p in parts:
        sheet.paste(p, (0, y))
        y += p.height + 4
    path = os.path.join(RUNS, out)
    sheet.save(path)
    print("SHEET %s  %dx%d  crops=%d zoom=%dx frame=%s_%s" % (path, sheet.width, sheet.height, len(parts), zoom, label, still))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
