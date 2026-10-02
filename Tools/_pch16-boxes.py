# -*- coding: utf-8 -*-
"""B-PERCH-16 (bird lane, 26.09): derive the cut boxes from the bird's OWN pixels in a
take's hold window (the camAuto framing moves between takes, and the bPCH12 cut-box
defect cost the phase a round). Prints the bird's body box, the feet box and the two
cut boxes (the sequence strip: the body + the branch under it; the seat zoom: the feet
+- a margin), in the "x0,y0,x1,y1" form the cut scripts take.
Usage: python Tools/_pch16-boxes.py <take> [margin]
"""
import sys
from PIL import Image
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = sys.argv[1]
MG = int(sys.argv[2]) if len(sys.argv) > 2 else 60
RATE = 20

t = {}
with open(RUNS + r"\%s_trigperch.csv" % TAKE, encoding="utf-8", errors="ignore") as f:
    for ln in f:
        p = ln.rstrip("\n").split(",", 2)
        if len(p) >= 2 and p[0] in ("SEAT", "RELEASE", "ARM"):
            t[p[0]] = float(p[1])
print("key times:", t)
seat = t.get("SEAT", 13.0)
rel = t.get("RELEASE", seat + 2.8)
mid = seat + 0.5 * (rel - seat)
i = int(round(mid * RATE))


def load(f):
    p = RUNS + r"\shot_%s_%s.png" % (TAKE, ("%02d" % f) if f < 100 else str(f))
    return np.asarray(Image.open(p).convert("RGB")).astype(int)


a = load(i)
r, g, b = a[..., 0], a[..., 1], a[..., 2]
mx = np.maximum(np.maximum(r, g), b)
sky = (np.abs(r - 140) < 22) & (np.abs(g - 199) < 24) & (np.abs(b - 240) < 24)
leg = (np.abs(r - 170) < 28) & (np.abs(g - 155) < 28) & (np.abs(b - 140) < 28) & ~sky
body = (b > r + 15) & (mx > 60) & (mx < 235) & ~sky
black = (mx < 45) & ~sky
print("frame %d (t=%.2f, mid-hold)" % (i, mid))


def bbox(m, name):
    ys, xs = np.where(m)
    if not len(ys):
        print(name, "none")
        return None
    bx = (int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max()))
    print("%-6s box=(%d,%d,%d,%d) n=%d" % (name, bx[0], bx[1], bx[2], bx[3], len(xs)))
    return bx


bb = bbox(body, "body")
lb = bbox(leg, "leg")
kb = bbox(black, "black")
# union of body + leg + the black pixels near the feet
m = body | leg
if kb:
    near = np.zeros_like(m)
    y0 = max(0, kb[1] - 12)
    y1 = kb[3] + 12
    near[y0:y1, :] = True
    m = m | (black & near)
u = bbox(m, "bird")
if u:
    x0, y0, x1, y1 = u
    print("SEQBOX  = %d,%d,%d,%d" % (max(0, x0 - MG), max(0, y0 - MG), x1 + MG, y1 + MG))
    if lb:
        fx0, fy0, fx1, fy1 = min(lb[0], kb[0]) if kb else lb[0], lb[1], max(lb[2], kb[2]) if kb else lb[2], max(lb[3], kb[3]) if kb else lb[3]
        print("SEATBOX = %d,%d,%d,%d" % (max(0, fx0 - 20), max(0, fy0 - 14), fx1 + 20, fy1 + 14))
