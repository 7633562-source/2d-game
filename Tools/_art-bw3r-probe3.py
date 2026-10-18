# ART-BW3R probe3: the NEW take's delivered scale. Mask bbox on the fan-spread candidate frames,
# the bird's own body extents, and the image size - vs the standing art take aBW3b f93.
import os

from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"


def bbox(label, frame, box=None):
    im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, frame))).convert("RGB")
    size = im.size
    if box:
        im = im.crop(box)
    px = im.load()
    w, h = im.size
    xs, ys = [], []
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            if r < 135 and g < 192 and b < 232 and b > 90:
                xs.append(x)
                ys.append(y)
    if not xs:
        print("%-16s f%-4d img %s  no mask in %s" % (label, frame, size, box))
        return
    x0, y0, x1, y1 = min(xs), min(ys), max(xs), max(ys)
    ox = box[0] if box else 0
    oy = box[1] if box else 0
    print("%-16s f%-4d img %s  bbox %4d,%4d..%4d,%4d w %3d h %3d centre %4d,%4d  n=%d" % (
        label, frame, size, x0 + ox, y0 + oy, x1 + ox, y1 + oy, x1 - x0, y1 - y0,
        (x0 + x1) // 2 + ox, (y0 + y1) // 2 + oy, len(xs)))


for fr in [92, 105, 20, 40]:
    bbox("bVW1fv_wander", fr)
bbox("aBW3b", 93)
bbox("aBW1f2_wander", 95)
