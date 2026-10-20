# ART-BW3R tone probe v2: the dark-core ruler over the ART's own mask (the bird+fan slate),
# so the grass/foliage cannot read as a core. Mask = the standing row probe's own rule.
# usage: python _art-bw3r-tone.py <label> <frame> <x0> <y0> <x1> <y1> [<label> <frame> ...]
import sys

from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"

for i in range(1, len(sys.argv), 6):
    label, frame = sys.argv[i], int(sys.argv[i + 1])
    box = tuple(int(v) for v in sys.argv[i + 2:i + 6])
    im = Image.open(RUNS + "\\shot_%s_%d.png" % (label, frame)).convert("RGB").crop(box)
    px = im.load()
    w, h = im.size
    n = dark = 0
    acc = [0, 0, 0]
    worst = (999, None)
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            if r < 135 and g < 192 and b < 232 and b > 90:
                n += 1
                s = r + g + b
                acc[0] += r
                acc[1] += g
                acc[2] += b
                if s < 250:
                    dark += 1
                if s < worst[0]:
                    worst = (s, (r, g, b))
    print("%s f%d box %s: mask %d px | sum<250 %d | darkest-in-mask %s sum %d | mean %s" % (
        label, frame, box, n, dark, worst[1], worst[0],
        tuple(round(v / n) for v in acc) if n else None))
