# -*- coding: utf-8 -*-
"""B-PERCH-22 (bird lane, 27.09): the phase-8 baseline cut - the PECK bout strip + the film.

Scene: the SOLO crow on the dirt (-bird 1 -birdDrive wander -birdNoSlide 1 -camAuto 1), no tree.
The peck bouts are read from the take's *_events.csv (mode 6 windows). This tool cuts
  (a) the standard peck strip: 12 panels across one bout at 20 fps (the stroke included),
      fixed crop (the camAuto camera holds the bird through a bout - the bird stands still
      while pecking, comX constant), 3x nearest;
  (b) the film frames for ffmpeg: t0..t1 at 20 fps, cropped to the bird's zone, 2x.

Usage: python Tools/_pch22-cuts.py <take> [tag] [strip|film|all] [t0 t1]
Writes Trials/Runs/reviews/<tag>_peck.png and Trials/Runs/reviews/<tag>_film/f_%04d.png
"""
import os
import sys
import numpy as np
from PIL import Image, ImageDraw

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
REV = RUNS + r"\reviews"
TAKE = sys.argv[1] if len(sys.argv) > 1 else "bPCH22a_ground"
TAG = sys.argv[2] if len(sys.argv) > 2 else TAKE
WHICH = sys.argv[3] if len(sys.argv) > 3 else "all"
T0 = float(sys.argv[4]) if len(sys.argv) > 4 else 6.80
T1 = float(sys.argv[5]) if len(sys.argv) > 5 else 8.80
RATE = 20

# the bird's zone in the owner camera (the vision-checked box on this take: x 890-1030, y 470-620)
BOX = (870, 440, 1080, 620)
SCALE = 3
FILM_BOX = (850, 420, 1110, 650)
FILM_SCALE = 2
PANELS = [6.90, 7.10, 7.40, 7.70, 7.90, 8.10, 8.20, 8.25, 8.30, 8.40, 8.55, 8.60]
LABELS = {8.20: "t=%.2f STROKE", 8.25: "t=%.2f STROKE", 8.30: "t=%.2f STROKE"}
CAMNOTE = ("camera: -camAuto 1 (the frame pans/zooms with the object); the bird pecks standing still, "
           "so the crop is fixed across the bout - panels left-to-right, top row then bottom")


def load(tt):
    i = int(round(tt * RATE))
    p = os.path.join(RUNS, "shot_%s_%d.png" % (TAKE, i))
    if not os.path.exists(p):
        q = os.path.join(RUNS, "shot_%s_%02d.png" % (TAKE, i))
        if os.path.exists(q):
            return np.asarray(Image.open(q).convert("RGB")).astype(np.uint8)
    if not os.path.exists(p):
        for d in range(1, 6):
            for s in (-d, d):
                q = os.path.join(RUNS, "shot_%s_%d.png" % (TAKE, i + s))
                if os.path.exists(q):
                    return np.asarray(Image.open(q).convert("RGB")).astype(np.uint8)
        raise SystemExit("no shot near t=%.2f" % tt)
    return np.asarray(Image.open(p).convert("RGB")).astype(np.uint8)


def panel_fixed(tt):
    a = load(tt)
    if a is None:
        return None
    x0, y0, x1, y1 = BOX
    crop = Image.fromarray(a[y0:y1, x0:x1])
    crop = crop.resize((crop.size[0] * SCALE, crop.size[1] * SCALE), Image.NEAREST)
    d = ImageDraw.Draw(crop)
    d.rectangle([0, 0, crop.size[0] - 1, crop.size[1] - 1], outline=(255, 255, 0), width=2)
    d.text((6, 4), LABELS.get(tt, "t=%.2f") % tt, fill=(255, 255, 0))
    return crop


def strip():
    panels = [panel_fixed(t) for t in PANELS]
    panels = [p for p in panels if p is not None]
    w = max(p.size[0] for p in panels)
    h = max(p.size[1] for p in panels)
    gap = 6
    cols = 4
    rows = (len(panels) + cols - 1) // cols
    fh = 24 if CAMNOTE else 0
    out = Image.new("RGB", (cols * w + (cols - 1) * gap, rows * h + (rows - 1) * gap + fh), (0, 0, 0))
    for k, p in enumerate(panels):
        out.paste(p, ((k % cols) * (w + gap), (k // cols) * (h + gap)))
    if CAMNOTE:
        ImageDraw.Draw(out).text((6, rows * h + (rows - 1) * gap + 6), CAMNOTE, fill=(180, 220, 255))
    p = os.path.join(REV, "%s_peck.png" % TAG)
    out.save(p)
    print("wrote", p, out.size)


def film():
    d = os.path.join(REV, "%s_film" % TAG)
    os.makedirs(d, exist_ok=True)
    n = 0
    t = T0
    while t <= T1 + 1e-6:
        a = load(t)
        x0, y0, x1, y1 = FILM_BOX
        crop = Image.fromarray(a[y0:y1, x0:x1])
        crop = crop.resize((crop.size[0] * FILM_SCALE, crop.size[1] * FILM_SCALE), Image.NEAREST)
        crop.save(os.path.join(d, "f_%04d.png" % n))
        n += 1
        t += 1.0 / RATE
    print("wrote %d frames to %s" % (n, d))


if WHICH in ("strip", "all"):
    strip()
if WHICH in ("film", "all"):
    film()
