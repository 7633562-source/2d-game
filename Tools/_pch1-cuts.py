# -*- coding: utf-8 -*-
"""B-PERCH-1 cuts (25.09): the seat A/B and the landing strip from the -0.12 take.
Usage: python Tools/_pch1-cuts.py <seatFrame> <f1,f2,...>
  seatFrame = the seated frame index in bPCH1m2v_tree (t = idx/20)
  f1,f2,... = landing-strip frame indices (t = idx/20)
Writes reviews/bPCH1_seat_AB.png and reviews/bPCH1_land_strip.png
"""
import sys
from PIL import Image, ImageDraw

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
PRE = "bBEND1v_tree"
POST = "bPCH1m2v_tree"


def crop_panel(path, box, label, zoom, band=22):
    a = Image.open(path).convert("RGB")
    im = a.crop((box[0], box[2], box[1], box[3])).resize(
        ((box[1] - box[0]) * zoom, (box[3] - box[2]) * zoom), Image.NEAREST)
    d = ImageDraw.Draw(im)
    for gx in range(box[0] - box[0] % 10 + 10, box[1], 10):
        X = (gx - box[0]) * zoom
        d.line([(X, 0), (X, 6)], fill=(255, 255, 255), width=1)
        if gx % 20 == 0:
            d.text((X + 2, 7), str(gx), fill=(255, 0, 255))
    for gy in range(box[2] - box[2] % 20 + 20, box[3], 20):
        Y = (gy - box[2]) * zoom
        d.line([(0, Y), (6, Y)], fill=(255, 255, 255), width=1)
        d.text((8, Y + 2), str(gy), fill=(0, 255, 255))
    out = Image.new("RGB", (im.width, im.height + band), (12, 12, 12))
    out.paste(im, (0, band))
    ImageDraw.Draw(out).text((5, 5), label, fill=(255, 230, 80))
    return out


def seat_ab(seat_frame):
    box = (884, 968, 556, 700)
    A = crop_panel(f"{RUNS}\\shot_{PRE}_120.png", box, "BEFORE (PRE): feet float on the leaf pads", 7)
    B = crop_panel(f"{RUNS}\\shot_{POST}_{seat_frame}.png", box,
                   "AFTER (seatDy): feet on the wood (t=%.1fs)" % (seat_frame / 20.0), 7)
    gap = 12
    out = Image.new("RGB", (A.width + gap + B.width, A.height), (0, 0, 0))
    out.paste(A, (0, 0))
    out.paste(B, (A.width + gap, 0))
    p = RUNS + r"\reviews\bPCH1_seat_AB.png"
    out.save(p)
    print("saved", p, out.size)


def land_strip(frames):
    box = (872, 992, 512, 712)
    panels = []
    for f in frames:
        panels.append(crop_panel(f"{RUNS}\\shot_{POST}_{f}.png", box, "t=%.1f s" % (f / 20.0), 4))
    cols = 4
    rows = (len(panels) + cols - 1) // cols
    gap = 8
    w = max(p.width for p in panels)
    h = max(p.height for p in panels)
    out = Image.new("RGB", (cols * w + (cols - 1) * gap, rows * h + (rows - 1) * gap), (0, 0, 0))
    for k, p in enumerate(panels):
        out.paste(p, ((k % cols) * (w + gap), (k // cols) * (h + gap)))
    p = RUNS + r"\reviews\bPCH1_land_strip.png"
    out.save(p)
    print("saved", p, out.size)


if __name__ == "__main__":
    seat_frame = int(sys.argv[1])
    frames = [int(x) for x in sys.argv[2].split(",")]
    seat_ab(seat_frame)
    land_strip(frames)
