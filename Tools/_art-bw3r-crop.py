# ART-BW3R probe5: leg-tight crops for the direct look (the leg-identity claim).
#   <label> <frame> <x0> <y0> <x1> <y1> <scale> <out.png>
import os
import sys

from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"

lab, fr = sys.argv[1], int(sys.argv[2])
box = tuple(int(v) for v in sys.argv[3:7])
scale = float(sys.argv[7])
out = sys.argv[8]

im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (lab, fr))).convert("RGB").crop(box)
c = im.resize((int(im.size[0] * scale), int(im.size[1] * scale)), Image.NEAREST)
c.save(out)
print("%s f%d box %s x%g -> %s %s" % (lab, fr, box, scale, out, c.size))
