#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""B-BEND-2 (bird lane, 25.09): the phase-7 bend sheet and the phase-6 landing strip,
built from the STATIC-lens take bBEND1v_tree (one camera, one crop; no camAuto).

Why: the 18:47 round's bend sheets were built from the camAuto scene take, so the
"unloaded" frame came from a different camera pose and the eyes correctly saw the
WHOLE silhouette shift (a rigid transfer) instead of a local elastic sag. The static
twin carries both states in the same lens.

Prints the identity checks (A vs B seated, C vs D settled, A vs C the load) and the
twig top-edge profile per panel.
"""
from PIL import Image, ImageDraw
import numpy as np

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
REV = RUNS + r"\reviews"
TAKE = "shot_bBEND1v_tree_"
SKY = np.array([140, 199, 240])


def load(i):
    return np.asarray(Image.open(f"{RUNS}\\{TAKE}{i}.png").convert("RGB")).astype(int)


def wood(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    return (r > g + 15) & (g >= b) & (r > 50)


def crop(a, box):
    x0, y0, x1, y1 = box
    return a[y0:y1, x0:x1]


def panel(a, box, label, zoom, caption=None):
    c = crop(a, box).astype("uint8")
    im = Image.fromarray(c).resize(
        ((box[2] - box[0]) * zoom, (box[3] - box[1]) * zoom), Image.NEAREST)
    band = 30
    out = Image.new("RGB", (im.width, im.height + band), (12, 12, 12))
    out.paste(im, (0, band))
    d = ImageDraw.Draw(out)
    d.text((6, 8), label, fill=(255, 232, 90))
    if caption:
        d.text((6, band + im.height - 22), caption, fill=(255, 120, 120))
    return out


def grid(panels, cols=2, gap=8):
    w = max(p.width for p in panels)
    rows = (len(panels) + cols - 1) // cols
    h = max(p.height for p in panels)
    out = Image.new("RGB", (cols * w + (cols - 1) * gap, rows * h + (rows - 1) * gap), (0, 0, 0))
    for k, p in enumerate(panels):
        out.paste(p, ((k % cols) * (w + gap), (k // cols) * (h + gap)))
    return out


def top_profile(a, box, xs):
    m = wood(crop(a, box))
    prof = {}
    for x in xs:
        col = np.nonzero(m[:, x - box[0]])[0]
        prof[x] = int(col.min() + box[1]) if len(col) else -1
    return prof


def main():
    A = load(120)   # t=6.0  seated #1
    B = load(180)   # t=9.0  seated #2
    C = load(600)   # t=30.0 unloaded (bird gone, settled: pixel-identical to every later frame)
    D = load(800)   # t=40.0 unloaded
    box = (880, 540, 1060, 720)          # 180x180, the load twig + the fork/trunk knots

    def diff(p, q, name):
        r = crop(p, box) - crop(q, box)
        n = int((np.abs(r).sum(2) > 12).sum())
        print(f"{name}: differing px {n} / {box[2]-box[0]}x{box[3]-box[1]}")
    diff(A, B, "A vs B (seated vs seated)")
    diff(C, D, "C vs D (unloaded vs unloaded)")
    diff(A, C, "A vs C (seated vs unloaded)")
    xs = [900, 920, 940, 960, 980, 1000]
    pA, pC = top_profile(A, box, xs), top_profile(C, box, xs)
    print("twig top edge (y, px):  x   seated  unloaded  drop")
    for x in xs:
        print(f"                        {x}   {pA[x]}    {pC[x]}     {pA[x]-pC[x]}")

    z = 6
    sheet = grid([
        panel(A, box, "A: bird SEATED #1 (t=6.0 s)", z),
        panel(B, box, "B: bird SEATED #2 (t=9.0 s)", z),
        panel(C, box, "C: bird GONE (t=16.0 s)", z),
        panel(D, box, "D: bird GONE, settled (t=30.0 s)", z),
    ], cols=2)
    sheet.save(REV + r"\bBEND2_bend_abcd.png")

    # phase 6: the landing window, same lens, wider crop (the approach is in frame)
    lbox = (880, 500, 1080, 740)
    fr = [84, 89, 94, 99, 102, 105, 111, 120]      # t = 4.20 .. 6.00
    caps = ["t=4.20", "t=4.45", "t=4.70", "t=4.95", "t=5.10", "t=5.25", "t=5.55", "t=6.00"]
    ps = [panel(load(i), lbox, cap, 4, None) for i, cap in zip(fr, caps)]
    strip = grid(ps, cols=4)
    strip.save(REV + r"\bBEND2_landing_strip.png")
    print("wrote", REV + r"\bBEND2_bend_abcd.png", sheet.size)
    print("wrote", REV + r"\bBEND2_landing_strip.png", strip.size)


main()
