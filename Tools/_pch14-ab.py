# -*- coding: utf-8 -*-
"""B-PERCH-14 (bird lane, 26.09): the seat A/B/C strip at the exposed fork.
One take per row (labelled with the seat value), the same crop box, the seat
moment and the settled hold.
Usage: python Tools/_pch14-ab.py
Writes Trials/Runs/reviews/bPCH14_ab_seat.png (+ _one_<label>.png per take at 8x)
"""
from PIL import Image, ImageDraw

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
REV = RUNS + r"\reviews"
TAKES = [("bPCH14a_tree", 0.335), ("bPCH14b_tree", 0.365), ("bPCH14c_tree", 0.393)]
BOX = (816, 256, 888, 328)      # x0,y0,x1,y1 - the fork + the feet
S = 7


def seat_time(take):
    with open(RUNS + "\\%s_trigperch.csv" % take, encoding="utf-8", errors="ignore") as f:
        for ln in f:
            p = ln.split(",", 2)
            if len(p) >= 2 and p[0] == "SEAT":
                return float(p[1])
    return 13.0


def load(take, t):
    best = None
    fr = int(round(t * 20))
    for d in range(0, 7):
        for f in (fr - d, fr + d):
            try:
                return Image.open(RUNS + "\\shot_%s_%d.png" % (take, f)).convert("RGB")
            except Exception:
                pass
    raise SystemExit("no shot near %.2f in %s" % (t, take))


def panel(img, label):
    x0, y0, x1, y1 = BOX
    c = img.crop(BOX)
    c = c.resize((c.size[0] * S, c.size[1] * S), Image.NEAREST)
    d = ImageDraw.Draw(c)
    d.rectangle([0, 0, c.size[0] - 1, c.size[1] - 1], outline=(255, 255, 0), width=2)
    d.text((6, 4), label, fill=(255, 255, 0))
    return c


rows = []
for take, seat in TAKES:
    t = seat_time(take)
    one = panel(load(take, t), "%s seatDy=%.3f  t=%.2f SEAT" % (take, seat, t))
    one.save(REV + r"\_pch14_one_%s.png" % take)
    settled = panel(load(take, t + 1.5), "%s seatDy=%.3f  t=%.2f settled" % (take, seat, t + 1.5))
    rows.append([one, settled])

w = rows[0][0].size[0]
h = rows[0][0].size[1]
gap = 8
cols = 2
out = Image.new("RGB", (cols * w + (cols - 1) * gap, len(rows) * h + (len(rows) - 1) * gap), (0, 0, 0))
for r, row in enumerate(rows):
    for c, p in enumerate(row):
        out.paste(p, (c * (w + gap), r * (h + gap)))
p = REV + r"\bPCH14_ab_seat.png"
out.save(p)
print("saved", p, out.size)
