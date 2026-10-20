# -*- coding: utf-8 -*-
"""B-PERCH-16: the bird's OWN palette map (f290-only colors, isolated by an f240 diff):
L=leg sprite (170,155,140)+-22  K=claw/eye black (max<40)  B=body blue-grey (b>r+15)
W=wood brown (r>g+12,g>=b)  .=sky  +=other
Usage: python Tools/_pch16-map2.py <take> <frame> [x0 x1 y0 y1]
"""
import sys
from PIL import Image
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
TAKE = sys.argv[1]
F = int(sys.argv[2])
X0 = int(sys.argv[3]) if len(sys.argv) > 3 else 830
X1 = int(sys.argv[4]) if len(sys.argv) > 4 else 890
Y0 = int(sys.argv[5]) if len(sys.argv) > 5 else 275
Y1 = int(sys.argv[6]) if len(sys.argv) > 6 else 340

a = np.asarray(Image.open(RUNS + r"\shot_%s_%d.png" % (TAKE, F)).convert("RGB")).astype(int)[Y0:Y1, X0:X1]
r, g, b = a[..., 0], a[..., 1], a[..., 2]
mx = np.maximum(np.maximum(r, g), b)
sky = (np.abs(r - 140) < 22) & (np.abs(g - 199) < 24) & (np.abs(b - 240) < 24)
leg = (np.abs(r - 170) < 25) & (np.abs(g - 155) < 25) & (np.abs(b - 140) < 25)
K = (mx < 40) & ~sky
B = (b > r + 15) & ~sky & ~leg
W = (r > g + 12) & (g >= b) & (r > 60) & ~sky
ch = np.where(K, "K", np.where(leg, "L", np.where(B, "B", np.where(W, "W", np.where(sky, ".", "+")))))

print("take=%s f=%d  x %d..%d  y %d..%d   (L=leg, K=black claws, B=body, W=wood)" % (TAKE, F, X0, X1, Y0, Y1))
print("     " + "".join(str(x // 10 % 10) for x in range(X0, X1)))
print("     " + "".join(str(x % 10) for x in range(X0, X1)))
for j in range(Y1 - Y0):
    print("%4d %s" % (Y0 + j, "".join(ch[j])))
# the claws' own columns: K pixels 8-connected to L (leg) pixels
from collections import deque
cand = K | leg
lab = np.zeros(cand.shape, int)
cur = 0
best = (0, 0)
for sy, sx in zip(*np.where(cand)):
    if lab[sy, sx]:
        continue
    cur += 1
    n = nl = 0
    q = deque([(sy, sx)])
    lab[sy, sx] = cur
    while q:
        y, x = q.popleft()
        n += 1
        if leg[y, x]:
            nl += 1
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                ny, nx = y + dy, x + dx
                if 0 <= ny < cand.shape[0] and 0 <= nx < cand.shape[1] and cand[ny, nx] and not lab[ny, nx]:
                    lab[ny, nx] = cur
                    q.append((ny, nx))
    if nl > best[0]:
        best = (nl, cur)
bird = (lab == best[1]) if best[1] else (K | leg)
print("# foot mask (leg+its black) px=%d, cols=%s" % (int(bird.sum()),
      ",".join(str(x + X0) for x in range(bird.shape[1]) if bird[:, x].any())))
wt = {}
for x in range(bird.shape[1]):
    if not bird[:, x].any():
        continue
    for y in range(bird.shape[0] - 2):
        if W[y, x] and W[y + 1, x] and W[y + 2, x]:
            wt[x] = y
            break
print("# per foot column: foot_top foot_bot wood_top(below) delta")
for x in range(bird.shape[1]):
    ys = np.where(bird[:, x])[0]
    if not len(ys):
        continue
    print("  x%-4d foot %d..%d  wood_top_below=%s  wood_top-window_top_scan=%s" % (
        x + X0, ys.min() + Y0, ys.max() + Y0,
        (wt[x] + Y0) if x in wt else "-",
        (next((y + Y0 for y in range(bird.shape[0]) if W[y, x]), None))))
