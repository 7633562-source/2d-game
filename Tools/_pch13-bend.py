# -*- coding: utf-8 -*-
"""B-PERCH-13 phase-7 strip (bird lane, 26.09): the LEFT limb alone, five+ moments of
the same crop in the same static camera (the wind-0 twin), so the bend's own pixel
read is not polluted by camera motion (the B-BEND-2 lesson).
The crop x>=927 excludes the plant's rest-pose joint seam (x<=926) and the fork's
inner wedge (x>=985); the red dashed guide = the UNLOADED top edge at x=930, read
from the first panel's own pixels and held across every panel.
Usage: python Tools/_pch13-bend.py <take_label> [tag] [t_before,t_seat,...]
"""
import sys
from PIL import Image, ImageDraw

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
BOX = (927, 592, 972, 700)
Z = 7
TAKE = sys.argv[1] if len(sys.argv) > 1 else "bPCH13b_tree"
TAG = sys.argv[2] if len(sys.argv) > 2 else "bPCH13_bend"


def key_times(take):
    t = {}
    with open(RUNS + "\\%s_trigperch.csv" % take, encoding="utf-8", errors="ignore") as f:
        for ln in f:
            p = ln.rstrip("\n").split(",", 2)
            if len(p) >= 2 and p[0] in ("PADS_DOWN", "GUIDE_ON", "SEAT", "RELEASE"):
                t[p[0]] = float(p[1])
    return t


T = key_times(TAKE)
SEAT = T.get("SEAT", 13.0)
REL = T.get("RELEASE", SEAT + 2.8)
if len(sys.argv) > 3:
    TIMES = [float(x) for x in sys.argv[3].split(",")]
else:
    TIMES = [SEAT - 2.0, SEAT + 0.04, SEAT + 0.6, SEAT + 1.5, SEAT + 2.5, REL + 0.6, REL + 3.0]
RATE = 20


def load(t):
    i = int(round(t * RATE))
    p = RUNS + "\\shot_%s_%d.png" % (TAKE, i)
    if not p or not __import__("os").path.exists(p):
        best = None
        for d in range(1, 6):
            for s in (-d, d):
                q = RUNS + "\\shot_%s_%d.png" % (TAKE, i + s)
                if __import__("os").path.exists(q):
                    best = q
                    break
            if best:
                break
        if best is None:
            raise SystemExit("no shot near t=%.2f" % t)
        p = best
    return Image.open(p).convert("RGB"), i


def is_wood(c):
    r, g, b = c
    return 60 < r < 190 and 30 < g < 120 and b < 90 and r > g > b


def top_edge(img, x):
    px = img.load()
    for y in range(BOX[1] - 30, BOX[3]):
        if is_wood(px[x, y]):
            return y
    return None


def panel(img, label, guide, idx):
    x0, y0, x1, y1 = BOX
    im = img.crop(BOX).resize(((x1 - x0) * Z, (y1 - y0) * Z), Image.NEAREST)
    d = ImageDraw.Draw(im)
    Y = (guide - y0) * Z
    for X in range(8, im.width - 8, 22):
        d.line([(X, Y), (X + 11, Y)], fill=(255, 30, 30), width=2)
    for gy in range(y0 - y0 % 20 + 20, y1, 20):
        Yy = (gy - y0) * Z
        d.line([(0, Yy), (6, Yy)], fill=(255, 255, 255), width=1)
        d.text((8, Yy + 2), str(gy), fill=(255, 0, 255))
    band = 22
    out = Image.new("RGB", (im.width, im.height + band), (12, 12, 12))
    out.paste(im, (0, band))
    ImageDraw.Draw(out).text((5, 5), label, fill=(255, 230, 80))
    return out


imgs = [load(t) for t in TIMES]
first = imgs[0][0]
guide = top_edge(first, 930)
print("take=%s seat=%.2f rel=%.2f guide(unloaded @x930)=%s" % (TAKE, SEAT, REL, guide))
panels = []
for (img, idx), t in zip(imgs, TIMES):
    te = top_edge(img, 930)
    print("f%-5d t=%.2f top@x930=%s d=%s" % (idx, t, te, (te - guide) if te is not None else "n/a"))
    panels.append(panel(img, "f%d t=%.2f" % (idx, t), guide, idx))
gap = 8
out = Image.new("RGB", (len(panels) * panels[0].width + (len(panels) - 1) * gap, panels[0].height), (0, 0, 0))
for k, p in enumerate(panels):
    out.paste(p, (k * (p.width + gap), 0))
path = RUNS + r"\reviews\%s.png" % TAG
out.save(path)
print("saved", path, out.size)
