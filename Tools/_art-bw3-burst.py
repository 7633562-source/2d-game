# ART-BW3 burst clip frames: the same-pose burst window (before aBW1f2 f87-117, after aBW3
# f86-116) cropped to the instrument's own box at 2x, each frame captioned like the mic cells.
# Frames land in the scratch dir; assemble with:
#   ffmpeg -y -framerate 25/3 -i burst_%03d.png -c:v libx264 -crf 20 -pix_fmt yuv420p aBW3-ab-burst.mp4
import csv
import os

from PIL import Image, ImageDraw, ImageFont

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
OUT = r"F:\hermes-data\profiles\h_2dgame_artist\cache\scratch\burst"
BOX = (690, 700, 1090, 1060)
CAP = 25.0
FONT = "C:/Windows/Fonts/arial.ttf"
CLIPS = [("aBW1f2_wander", 87, 118, "BEFORE aBW1f2"), ("aBW3b", 86, 117, "AFTER aBW3b")]


def rows(label):
    out = {}
    with open(os.path.join(RUNS, label + ".csv"), encoding="utf-8-sig") as f:
        for d in csv.DictReader(f):
            out[round(float(d["t"]) * CAP)] = d
    return out


os.makedirs(OUT, exist_ok=True)
idx = 0
for label, n0, n1, tag in CLIPS:
    rs = rows(label)
    for n in range(n0, n1):
        im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, n))).convert("RGB")
        crop = im.crop(BOX)
        crop = crop.resize((crop.size[0] * 2, crop.size[1] * 2), Image.LANCZOS)
        d = ImageDraw.Draw(crop)
        bar = 30
        d.rectangle([0, 0, crop.size[0], bar], fill=(0, 0, 0))
        f = ImageFont.truetype(FONT, 16)
        r = rs.get(n) or {}
        d.text((4, 2), "%s  f%d" % (tag, n), font=f, fill=(255, 255, 0))
        f2 = ImageFont.truetype(FONT, 14)
        d.text((4, 17), "fanO %s  chord %s  el %s  h %s" % (
            r.get("fanOpen", "?"), (r.get("fanChord", "?") or "?")[:7],
            (r.get("elbowDeg", "?") or "?")[:5], (r.get("heightAboveSupport", "?") or "?")[:5]),
            font=f2, fill=(180, 255, 180))
        idx += 1
        out = os.path.join(OUT, "burst_%03d.png" % idx)
        crop.save(out)
print("burst frames:", idx, "->", OUT)
