# -*- coding: utf-8 -*-
"""B-PERCH-23 (bird lane, 27.09): the peck stroke's own meter (v2).

Reads the stroke frames of a ground take and prints, per frame:
  * PARTS: the components above the grass (n > 300 px). 2 parts = the head's plate is SEPARATE
    from the body (the eyes' "the head detaches"); 1 part = the head is merged with the body.
  * GAP: the smallest horizontal run of pure background between the body's blob and the other
    big blob (only when the parts are separate).
  * TIP: the lowest bird pixel in the head's own column band (the rightmost 45 px of the bird) —
    the beak's tip, and how far it sits above the grass line (px).
Also saves a zoom sheet of the stroke frames.

Usage: python Tools/_pch23-stroke.py <take> [tag] [t...]
"""
import os
import sys
import numpy as np
from PIL import Image, ImageDraw

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
OUT = r"F:\hermes-data\scratch\pch23"
TAKE = sys.argv[1] if len(sys.argv) > 1 else "bPCH23a_ground"
TAG = sys.argv[2] if len(sys.argv) > 2 else TAKE
TIMES = [float(x) for x in sys.argv[3:]] or [8.10, 8.20, 8.25, 8.30, 8.40]
RATE = 20
BG = np.array([140, 199, 240])
BOX = (850, 420, 1110, 650)
S = 4
PART_PX = 300          # a bird part (the head's plate is ~590 px, the legs ~180-260, a speck < 60)
HEAD_BAND = 45         # the head's own column band at the bird's right end


def load(tt):
    i = int(round(tt * RATE))
    for d in (0, -1, 1, -2, 2):
        p = os.path.join(RUNS, "shot_%s_%d.png" % (TAKE, i + d))
        if os.path.exists(p):
            return np.asarray(Image.open(p).convert("RGB")).astype(int)
    raise SystemExit("no shot near %.2f" % tt)


def grass_top():
    a = load(TIMES[0])
    for y in range(540, 700):
        row = a[y, 0:1920]
        if (np.abs(row - BG).sum(axis=1) > 60).mean() > 0.5:
            return y
    return -1


def labels(mask):
    h, w = mask.shape
    lab = np.zeros((h, w), np.int32)
    cur = 0
    for y0 in range(h):
        for x0 in range(w):
            if mask[y0, x0] and lab[y0, x0] == 0:
                cur += 1
                stack = [(y0, x0)]
                lab[y0, x0] = cur
                while stack:
                    y, x = stack.pop()
                    for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                        if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and lab[ny, nx] == 0:
                            lab[ny, nx] = cur
                            stack.append((ny, nx))
    return lab, cur


def measure(tt, gtop, sheet, head_band=HEAD_BAND):
    a = load(tt)
    x0, y0, x1, y1 = BOX
    sub = a[y0:y1, x0:x1]
    mask = (np.abs(sub - BG).sum(axis=2) > 60)
    gy = gtop - 4 - y0
    if 0 < gy < mask.shape[0]:
        mask[gy:, :] = False
    lab, n = labels(mask)
    if n < 1:
        print("t=%.2f: no bird pixels" % tt)
        return None
    parts = []
    for k in range(1, n + 1):
        m = lab == k
        c = int(m.sum())
        if c > PART_PX:
            yy, xx = np.where(m)
            parts.append((c, m, (int(xx.min()) + x0, int(xx.max()) + x0, int(yy.min()) + y0, int(yy.max()) + y0)))
    if not parts:
        print("t=%.2f: nothing big above the grass" % tt)
        return None
    parts.sort(key=lambda p: -p[0])
    body = parts[0]
    xmax = body[2][1]
    for c, m, bb in parts[1:]:
        xmax = max(xmax, bb[1])
    rec = dict(t=tt, parts=len(parts), bbox=body[2])
    if len(parts) > 1:
        head = max(parts[1:], key=lambda p: p[2][1])   # the rightmost other big part = the head's plate
        best = None
        bys, bxs = np.where(body[1])
        hys, hxs = np.where(head[1])
        for yy in range(hys.min(), hys.max() + 1):
            bxx = np.where(body[1][yy])[0]
            hxx = np.where(head[1][yy])[0]
            if len(bxx) and len(hxx):
                g = int(hxx.min()) - int(bxx.max()) - 1
                if g >= 0 and (best is None or g < best[0]):
                    best = (g, yy)
        rec["gap"] = best[0] if best else 0
        rec["head_bbox"] = head[2]
    else:
        rec["gap"] = 0
        rec["head_bbox"] = body[2]
    # the beak's tip: the lowest bird pixel in the head's own column band (the bird's right end)
    band = mask[:, max(0, (xmax - head_band) - x0):(xmax - x0) + 1]
    yy, xx = np.where(band)
    tipy = int(yy.max()) + y0
    rec["tip_above"] = gtop - tipy
    print("t=%.2f: PARTS %d (%s)%s | bird x..x %d..%d | head band tip y=%d (%d px above grass %d) | body bbox x %d..%d y %d..%d" % (
        tt, len(parts), "the head's plate is SEPARATE" if len(parts) > 1 else "the head is MERGED with the body",
        (" | GAP %d px (row %d)" % (rec["gap"], best[1] + y0)) if len(parts) > 1 and best else "",
        int(np.where(mask)[1].min()) + x0, xmax, tipy, rec["tip_above"], gtop,
        body[2][0], body[2][1], body[2][2], body[2][3]))
    if sheet is not None:
        crop = Image.fromarray(sub.astype(np.uint8)).resize((sub.shape[1] * S, sub.shape[0] * S), Image.NEAREST)
        d = ImageDraw.Draw(crop)
        d.text((8, 6), "t=%.2f parts=%d gap=%s tip=%d" % (tt, len(parts), rec["gap"], rec["tip_above"]),
               fill=(255, 255, 0))
        sheet.append(crop)
    return rec


def main():
    os.makedirs(OUT, exist_ok=True)
    gtop = grass_top()
    print("take %s (tag %s): the grass line y=%d" % (TAKE, TAG, gtop))
    sheet, res = [], []
    for t in TIMES:
        r = measure(t, gtop, sheet)
        if r:
            res.append(r)
    if sheet:
        w, h = sheet[0].size
        rows = (len(sheet) + 1) // 2
        out = Image.new("RGB", (2 * w + 8, rows * h + (rows - 1) * 8), (0, 0, 0))
        for k, p in enumerate(sheet):
            out.paste(p, ((k % 2) * (w + 8), (k // 2) * (h + 8)))
        path = os.path.join(OUT, "%s_stroke.png" % TAG)
        out.save(path)
        print("wrote", path, out.size)
    stroke = [r for r in res if r["t"] >= 8.15]
    if stroke:
        sep = [r for r in stroke if r["parts"] > 1]
        print("summary %s: stroke frames %d | head SEPARATE in %d | gap %s px | tip above grass %d..%d px" % (
            TAG, len(stroke), len(sep),
            ("%d..%d" % (min(r["gap"] for r in sep), max(r["gap"] for r in sep))) if sep else "n/a",
            min(r["tip_above"] for r in stroke), max(r["tip_above"] for r in stroke)))


main()
