# ART-BW3R tops: the most common colours in a box (counts), for the dress's own tones.
# usage: _art-bw3r-tops.py <label> <frame> <x0> <y0> <x1> <y1> [k]
import sys

from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
label, frame = sys.argv[1], int(sys.argv[2])
box = tuple(int(v) for v in sys.argv[3:7])
k = int(sys.argv[7]) if len(sys.argv) > 7 else 16

im = Image.open(RUNS + "\\shot_%s_%d.png" % (label, frame)).convert("RGB").crop(box)
c = {}
for p in im.get_flattened_data() if hasattr(im, "get_flattened_data") else im.getdata():
    c[p] = c.get(p, 0) + 1
for t, n in sorted(c.items(), key=lambda kv: -kv[1])[:k]:
    print("%s x%d  C=(%.3f,%.3f,%.3f)" % (t, n, t[0] / 255.0, t[1] / 255.0, t[2] / 255.0))
