# ART-BW3R burst clip frames: leg-tight crops, BEFORE (aL1pre, the uniform legs) then AFTER
# (aL1s, the far leg's haze with the step switches on), captioned per frame. Assemble:
#   ffmpeg -y -framerate 10 -i burst_%03d.png -c:v libx264 -crf 20 -pix_fmt yuv420p aL1-legs-burst.mp4
import csv
import os

from PIL import Image, ImageDraw, ImageFont

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
OUT = r"F:\hermes-data\profiles\h_2dgame_artist\cache\scratch\burst"
BOX = (880, 870, 1090, 1030)
SCALE = 4.0
CAP = 25.0
FONT = "C:/Windows/Fonts/arial.ttf"
CLIPS = [("aL1pre_wander", 30, 46, "BEFORE aL1pre - both legs one tone"),
         ("aL1s_wander", 30, 46, "AFTER aL1s - far leg haze, step on")]


def rows(label):
    out = {}
    with open(os.path.join(RUNS, label + ".csv"), encoding="utf-8-sig") as f:
        for d in csv.DictReader(f):
            out[round(float(d["t"]) * CAP)] = d
    return out


os.makedirs(OUT, exist_ok=True)
n = 0
for label, f0, f1, title in CLIPS:
    for fr in range(f0, f1):
        im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, fr))).convert("RGB").crop(BOX)
        im = im.resize((int(im.size[0] * SCALE), int(im.size[1] * SCALE)), Image.LANCZOS)
        d = ImageDraw.Draw(im)
        bar = int(14 * SCALE)
        f1f = ImageFont.truetype(FONT, int(5.5 * SCALE))
        d.rectangle([0, 0, im.size[0], bar], fill=(0, 0, 0))
        d.text((int(1.5 * SCALE), int(0.8 * SCALE)), "%s  f%d  t=%.2fs" % (title, fr, fr / CAP),
               font=f1f, fill=(255, 255, 0))
        n += 1
        im.save(os.path.join(OUT, "burst_%03d.png" % n))
print("frames -> %s : %d" % (OUT, n))
