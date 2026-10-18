# -*- coding: utf-8 -*-
"""B-PERCH-17 (bird lane, 26.09): the cut of the open-mark take (slot 11).
The camAuto framing moves with the objects, so a fixed box loses the bird on the
approach panels (the bPCH17a_v2 sheet's top row carried no bird). This cut TRACKS
the bird's own pixels per panel for the seq/leave strips (a fixed crop around the
bird's box), and keeps the FIXED seat box for the hold strip (the camera is static
in the hold, so the seat zoom stays comparable panel to panel).

Usage: python Tools/_pch17-cuts.py <take> [tag] [seq|seat|leave|all]
Writes Trials/Runs/reviews/<tag>_seq.png / _seat.png / _leave.png
Env: PCH17_SEATBOX "x0,y0,x1,y1" (default = the slot-11 feet box), PCH17_MARGIN px.
"""
import os
import sys
import numpy as np
from PIL import Image, ImageDraw

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
REV = RUNS + r"\reviews"
TAKE = sys.argv[1] if len(sys.argv) > 1 else "bPCH17a_tree"
TAG = sys.argv[2] if len(sys.argv) > 2 else TAKE
WHICH = sys.argv[3] if len(sys.argv) > 3 else "all"
RATE = 20
MG = int(os.environ.get("PCH17_MARGIN", "70"))
SEATBOX = tuple(int(v) for v in os.environ.get("PCH17_SEATBOX", "930,380,1010,462").split(","))
SEQ_SIZE = 300
CAMNOTE = ("camera: -camAuto 1 (owner flags) - the frame pans/zooms with the objects; "
           "the panels are re-framed on the bird's own pixels, so the world point shifts")

t = {}
with open(RUNS + r"\%s_trigperch.csv" % TAKE, encoding="utf-8", errors="ignore") as f:
    for ln in f:
        p = ln.rstrip("\n").split(",", 2)
        if len(p) >= 2 and p[0] in ("PADS_DOWN", "GUIDE_ON", "SEAT", "RELEASE"):
            t[p[0]] = float(p[1])
SEAT = t.get("SEAT", 13.0)
REL = t.get("RELEASE", SEAT + 2.77)


def load(tt):
    i = int(round(tt * RATE))
    p = RUNS + r"\shot_%s_%d.png" % (TAKE, i)
    if not os.path.exists(p):
        for d in range(1, 6):
            for s in (-d, d):
                q = RUNS + r"\shot_%s_%d.png" % (TAKE, i + s)
                if os.path.exists(q):
                    p = q
                    break
            else:
                continue
            break
    return np.asarray(Image.open(p).convert("RGB")).astype(int)


def bird_box(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mx = np.maximum(np.maximum(r, g), b)
    sky = (np.abs(r - 140) < 22) & (np.abs(g - 199) < 24) & (np.abs(b - 240) < 24)
    wood = (r - b > 40) & (r > 60)
    leaf = (g > r + 8) & (g > b + 8) & ~wood
    bird = (~sky) & (~wood) & (~leaf) & (((mx < 72) & (b >= r - 6)) | ((b > r + 12) & (mx < 215)))
    neigh = np.zeros(bird.shape, dtype=int)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if dx == 0 and dy == 0:
                continue
            neigh += np.roll(np.roll(bird, dy, axis=0), dx, axis=1).astype(int)
    core = bird & (neigh >= 2)
    if core.sum() >= 50:
        bird = core
    ys, xs = np.where(bird)
    if len(ys) < 30:
        return None
    # sparse strays (the trunk's dark patches) must not inflate the box: keep only the
    # rows/columns that carry at least a token share of the mask
    thr = max(4, len(ys) // 200)
    hy = np.bincount(ys)
    hx = np.bincount(xs)
    ky = np.where(hy >= thr)[0]
    kx = np.where(hx >= thr)[0]
    return int(kx.min()), int(ky.min()), int(kx.max()), int(ky.max())


def panel_tracked(tt, label, scale=3):
    a = load(tt)
    bb = bird_box(a)
    if bb is None:
        bb = (760, 380, 1000, 470)
    x0, y0, x1, y1 = bb
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    s = SEQ_SIZE // 2
    bx = (max(0, cx - s), max(0, cy - s), min(a.shape[1], cx + s), min(a.shape[0], cy + s))
    crop = Image.fromarray(np.uint8(a[bx[1]:bx[3], bx[0]:bx[2]]))
    w, h = crop.size
    crop = crop.resize((w * scale, h * scale), Image.NEAREST)
    d = ImageDraw.Draw(crop)
    d.rectangle([0, 0, w * scale - 1, h * scale - 1], outline=(255, 255, 0), width=2)
    d.text((6, 4), label, fill=(255, 255, 0))
    return crop


def panel_fixed(tt, box, label, scale, grid_step=10):
    a = load(tt)
    x0, y0, x1, y1 = box
    crop = Image.fromarray(np.uint8(a[y0:y1, x0:x1]))
    w, h = crop.size
    crop = crop.resize((w * scale, h * scale), Image.NEAREST)
    d = ImageDraw.Draw(crop)
    if grid_step:
        for x in range(0, w * scale, grid_step * scale):
            d.line([(x, 0), (x, h * scale)], fill=(40, 40, 40), width=1)
        for y in range(0, h * scale, grid_step * scale):
            d.line([(0, y), (w * scale, y)], fill=(40, 40, 40), width=1)
    d.rectangle([0, 0, w * scale - 1, h * scale - 1], outline=(255, 255, 0), width=2)
    d.text((6, 4), label, fill=(255, 255, 0))
    return crop


def grid(panels, cols, foot=""):
    w = max(p.size[0] for p in panels)
    h = max(p.size[1] for p in panels)
    gap = 6
    fh = 22 if foot else 0
    rows = (len(panels) + cols - 1) // cols
    out = Image.new("RGB", (cols * w + (cols - 1) * gap, rows * h + (rows - 1) * gap + fh), (0, 0, 0))
    for k, p in enumerate(panels):
        out.paste(p, ((k % cols) * (w + gap), (k // cols) * (h + gap)))
    if foot:
        ImageDraw.Draw(out).text((6, rows * h + (rows - 1) * gap + 5), foot, fill=(180, 220, 255))
    return out


SEQ = [(SEAT - 0.55, "t=%.2f" % (SEAT - 0.55)), (SEAT - 0.35, "t=%.2f" % (SEAT - 0.35)),
       (SEAT - 0.20, "t=%.2f" % (SEAT - 0.20)), (SEAT - 0.10, "t=%.2f" % (SEAT - 0.10)),
       (SEAT, "t=%.2f SEAT" % SEAT), (SEAT + 0.10, "t=%.2f" % (SEAT + 0.10)),
       (SEAT + 1.30, "t=%.2f settled" % (SEAT + 1.30)), (SEAT + 2.20, "t=%.2f settled" % (SEAT + 2.20))]
SEATW = [(SEAT, "t=%.2f SEAT" % SEAT), (SEAT + 0.06, "t=%.2f" % (SEAT + 0.06)),
         (SEAT + 0.30, "t=%.2f" % (SEAT + 0.30)), (SEAT + 0.90, "t=%.2f" % (SEAT + 0.90)),
         (SEAT + 1.50, "t=%.2f" % (SEAT + 1.50)), (SEAT + 2.10, "t=%.2f" % (SEAT + 2.10)),
         (SEAT + 2.20, "t=%.2f" % (SEAT + 2.20)), (SEAT + 2.40, "t=%.2f" % (SEAT + 2.40))]
LEAVE = [(REL - 0.10, "t=%.2f" % (REL - 0.10)), (REL, "t=%.2f RELEASE" % REL),
         (REL + 0.10, "t=%.2f" % (REL + 0.10)), (REL + 0.25, "t=%.2f" % (REL + 0.25)),
         (REL + 0.45, "t=%.2f" % (REL + 0.45)), (REL + 0.75, "t=%.2f" % (REL + 0.75))]

print("take=%s seat=%.2f rel=%.2f times=%s" % (TAKE, SEAT, REL, t))
if WHICH in ("seq", "all"):
    a = REV + r"\%s_seq.png" % TAG
    grid([panel_tracked(tt, lb) for tt, lb in SEQ], cols=4, foot=CAMNOTE + " | panels re-framed on the bird").save(a)
    print("saved", a)
if WHICH in ("seat", "all"):
    b = REV + r"\%s_seat.png" % TAG
    grid([panel_fixed(tt, SEATBOX, lb, 6) for tt, lb in SEATW], cols=4, foot=CAMNOTE + " | fixed seat box").save(b)
    print("saved", b)
if WHICH in ("leave", "all"):
    c = REV + r"\%s_leave.png" % TAG
    grid([panel_tracked(tt, lb) for tt, lb in LEAVE], cols=3, foot=CAMNOTE + " | panels re-framed on the bird").save(c)
    print("saved", c)
