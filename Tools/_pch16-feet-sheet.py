# -*- coding: utf-8 -*-
"""B-PERCH-16 (bird lane, 26.09): THE FEET'S OWN PIXELS sheet - the seated bird's feet
with the measured lines drawn on top: RED = the wood's first row per column (the top
silhouette the eye sees), GREEN = the claw mask's bottom row per column (the feet's own
pixels: the black claw blob + the tan leg sprite). Panel 1 = the bird's context at 6x,
panel 2 = the feet at 12x with the lines, panel 3 = the same feet with the claw plane's
world value from the record drawn as a blue line (the claws' tips).
Usage: python Tools/_pch16-feet-sheet.py <take> <frame> [tag]
Writes Trials/Runs/reviews/<tag>_feetsheet.png and prints the per-column table.
"""
import sys
from PIL import Image, ImageDraw
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
REV = RUNS + r"\reviews"
TAKE = sys.argv[1]
F = int(sys.argv[2])
TAG = sys.argv[3] if len(sys.argv) > 3 else "bPCH16"


def path(f):
    return RUNS + r"\shot_%s_%s.png" % (TAKE, ("%02d" % f) if f < 100 else str(f))


im = Image.open(path(F)).convert("RGB")
a = np.asarray(im).astype(int)
r, g, b = a[..., 0], a[..., 1], a[..., 2]
mx = np.maximum(np.maximum(r, g), b)
sky = (np.abs(r - 140) < 22) & (np.abs(g - 199) < 24) & (np.abs(b - 240) < 24)
wood = (r > g + 12) & (g >= b) & (r > 60) & ~sky
leg = (np.abs(r - 170) < 25) & (np.abs(g - 155) < 25) & (np.abs(b - 140) < 25)
black = (mx < 45) & ~sky
claw = leg | black

X0, X1, Y0, Y1 = 812, 884, 258, 324          # the feet's own window (bPCH14e/bPCH16b framing)
wt, cb = {}, {}
for x in range(X0, X1):
    for y in range(Y0, Y1 - 2):
        if wood[y, x] and wood[y + 1, x] and wood[y + 2, x]:
            wt[x] = y
            break
    ys = np.where(claw[Y0:Y1, x])[0]
    if len(ys):
        cb[x] = (int(ys.min()) + Y0, int(ys.max()) + Y0)
print("take=%s frame=%d  feet window x%d-%d y%d-%d" % (TAKE, F, X0, X1, Y0, Y1))
print(" x   claw_top claw_bot | wood_top(red) | wood_top - claw_bot")
for x in range(X0, X1, 2):
    c = cb.get(x)
    w = wt.get(x)
    print("%4d %9s %8s | %13s | %s" % (x, c[0] if c else "-", c[1] if c else "-",
                                       w if w is not None else "-",
                                       (w - c[1]) if (w is not None and c) else "-"))

Z = 12
crop = im.crop((X0, Y0, X1, Y1)).resize(((X1 - X0) * Z, (Y1 - Y0) * Z), Image.NEAREST)
d = ImageDraw.Draw(crop)
for x, y in wt.items():
    d.rectangle([(x - X0) * Z, (y - Y0) * Z + Z // 3, (x - X0) * Z + Z - 1, (y - Y0) * Z + Z - 1], fill=(255, 0, 0))
for x, (ct, cbm) in cb.items():
    d.rectangle([(x - X0) * Z, (cbm - Y0) * Z, (x - X0) * Z + Z - 1, (cbm - Y0) * Z + Z // 3], fill=(0, 255, 0))
# the context panel: the whole seated bird at 6x
ctx = im.crop((700, 200, 980, 380)).resize((280 * 5, 180 * 5), Image.NEAREST)
dc = ImageDraw.Draw(ctx)
dc.rectangle([(X0 - 700) * 5, (Y0 - 200) * 5, (X1 - 700) * 5, (Y1 - 200) * 5], outline=(255, 0, 255), width=2)
gap = 14
W = max(crop.size[0], ctx.size[0])
H = crop.size[1] + gap + ctx.size[1]
out = Image.new("RGB", (W, H), (0, 0, 0))
out.paste(crop, (0, 0))
out.paste(ctx, (0, crop.size[1] + gap))
p = REV + r"\%s_feetsheet.png" % TAG
out.save(p)
print("saved", p, out.size)
