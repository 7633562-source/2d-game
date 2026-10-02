# -*- coding: utf-8 -*-
"""B-PERCH-16 (bird lane, 26.09): THE FEET'S OWN PIXELS - the bird's own silhouette
(body + the dark parts 4-connected to it) vs the WOOD'S TOP SILHOUETTE per column, on a
seated frame; prints the per-column offset and draws an overlay (red = wood top
silhouette, green = the bird's own bottom row) for the eyes.
Usage: python Tools/_pch16-feetedge.py <take> <frame> [x0 x1 y0 y1] [z]
"""
import sys
from collections import deque
from PIL import Image, ImageDraw
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
REV = RUNS + r"\reviews"
TAKE = sys.argv[1]
F = int(sys.argv[2])
X0 = int(sys.argv[3]) if len(sys.argv) > 3 else 800
X1 = int(sys.argv[4]) if len(sys.argv) > 4 else 900
Y0 = int(sys.argv[5]) if len(sys.argv) > 5 else 240
Y1 = int(sys.argv[6]) if len(sys.argv) > 6 else 340
Z = int(sys.argv[7]) if len(sys.argv) > 7 else 8

im = Image.open(RUNS + r"\shot_%s_%d.png" % (TAKE, F)).convert("RGB")
w = np.asarray(im.crop((X0, Y0, X1, Y1))).astype(int)
r, g, b = w[..., 0], w[..., 1], w[..., 2]
mx = np.maximum(np.maximum(r, g), b)
sky = (np.abs(r - 140) < 20) & (np.abs(g - 199) < 22) & (np.abs(b - 240) < 22)
dark = (mx < 95) & ~sky
body = (b > r + 8) & (b > g + 4) & (mx >= 60) & (mx < 235) & ~sky
wood = (r > g + 12) & (g >= b) & (r > 60) & (r < 215) & (mx >= 95)

# the bird = the largest component of (body | dark) that contains body pixels
cand = body | dark
lab = np.zeros(cand.shape, int)
cur = 0
best = (0, 0)
for sy, sx in zip(*np.where(cand)):
    if lab[sy, sx]:
        continue
    cur += 1
    n = nb = 0
    q = deque([(sy, sx)])
    lab[sy, sx] = cur
    while q:
        y, x = q.popleft()
        n += 1
        if body[y, x]:
            nb += 1
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < cand.shape[0] and 0 <= nx < cand.shape[1] and cand[ny, nx] and not lab[ny, nx]:
                lab[ny, nx] = cur
                q.append((ny, nx))
    if nb > best[0]:
        best = (nb, cur)
bird = lab == best[1] if best[1] else body

wt = {}
for x in range(w.shape[1]):
    for y in range(w.shape[0] - 2):
        if wood[y, x] and wood[y + 1, x] and wood[y + 2, x]:
            wt[x] = y
            break
bb = {}
for x in range(w.shape[1]):
    ys = np.where(bird[:, x])[0]
    if len(ys):
        bb[x] = (int(ys.min()), int(ys.max()))

print("take=%s f=%d  x %d-%d y %d-%d  bird_px=%d (body %d)" % (TAKE, F, X0, X1, Y0, Y1, int(bird.sum()), int(body.sum())))
print("  x  bird_top bird_bot  wood_top  wood_top-bird_bot")
for x in range(0, w.shape[1], 2):
    if x in bb:
        bt, bz = bb[x]
        bt += Y0
        bz += Y0
    else:
        bt = bz = None
    wy = wt.get(x)
    wy = wy + Y0 if wy is not None else None
    print("%4d %8s %8s %9s %10s" % (x + X0, bt, bz, wy if wy is not None else "-",
                                    (wy - bz) if (wy is not None and bz is not None) else "-"))

crop = im.crop((X0, Y0, X1, Y1)).resize(((X1 - X0) * Z, (Y1 - Y0) * Z), Image.NEAREST)
d = ImageDraw.Draw(crop)
for x, y in wt.items():
    d.rectangle([x * Z, y * Z, x * Z + Z - 1, y * Z + Z - 1], fill=(255, 0, 0))
for x, (bt, bz) in bb.items():
    d.rectangle([x * Z, bz * Z, x * Z + Z - 1, bz * Z + Z - 1], fill=(0, 255, 0))
out = REV + r"\_pch16_edge_%s_f%d.png" % (TAKE, F)
crop.save(out)
print("overlay:", out)
