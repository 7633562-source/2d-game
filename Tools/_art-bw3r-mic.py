# ART-BW3R mic: the standing instrument (bw11_mic.py) with the crop box, the shot rate and the
# zoom exposed - the bird lane's take (bVW1fv_wander, shotRate 20, camAuto) delivers the art at
# ~0.48x the art takes' linear scale, so a re-anchored box + a matching zoom puts the plates back
# at the standing sheets' delivered size. Same caption, same LANCZOS, same convention.
# usage: _art-bw3r-mic.py single <label> <frame> <rate> <x0> <y0> <x1> <y1> <zoom> <out> [A|B]
import csv
import os
import sys

from PIL import Image, ImageDraw, ImageFont

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
FONT = "C:/Windows/Fonts/arial.ttf"


def rows(label, rate):
    out = {}
    with open(os.path.join(RUNS, label + ".csv"), encoding="utf-8-sig") as f:
        for d in csv.DictReader(f):
            out[round(float(d["t"]) * rate)] = d
    return out


def num(r, k):
    try:
        return float(r[k])
    except (KeyError, ValueError, TypeError):
        return None


def cell(label, n, rate, box, zoom, letter=None):
    im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, n))).convert("RGB")
    crop = im.crop(box)
    W, H = crop.size
    c = crop.resize((int(W * zoom), int(H * zoom)), Image.LANCZOS)
    d = ImageDraw.Draw(c)
    bar = int(14 * zoom)
    f1 = ImageFont.truetype(FONT, int(5.5 * zoom))
    f2 = ImageFont.truetype(FONT, int(5.0 * zoom))
    d.rectangle([0, 0, c.size[0], bar], fill=(0, 0, 0))
    r = rows(label, rate).get(n) or {}
    if letter:
        f3 = ImageFont.truetype(FONT, int(16 * zoom))
        d.text((int(2 * zoom), int(1.2 * zoom)), letter, font=f3, fill=(255, 255, 0))
        tx = int(10 * zoom)
    else:
        tx = int(1.5 * zoom)
    line1 = "%s f%d  t=%.2fs  mode %s" % (label if not letter else "", n, n / float(rate), r.get("mode", "?"))
    d.text((tx, int(0.8 * zoom)), line1.strip(), font=f1, fill=(255, 255, 0))
    line2 = "fanO %.2f  chord %.4f  el %.0f  h %.2f" % (
        num(r, "fanOpen") or 0, num(r, "fanChord") or 0, num(r, "elbowDeg") or 0,
        num(r, "heightAboveSupport") or 0)
    d.text((tx, int(6.5 * zoom)), line2, font=f2, fill=(180, 255, 180))
    return c


mode = sys.argv[1]
if mode == "single":
    a = sys.argv[2:]
    label, n, rate = a[0], int(a[1]), int(a[2])
    box = tuple(int(v) for v in a[3:7])
    zoom, out = float(a[7]), a[8]
    letter = a[9] if len(a) > 9 else None
    c = cell(label, n, rate, box, zoom, letter)
    c.save(out)
    print("single %s f%d rate %d box %s x%g -> %s %s" % (label, n, rate, box, zoom, out, c.size))
elif mode == "pair":
    # pair <out> <cellA.png> <cellB.png>
    out, pa, pb = sys.argv[2], sys.argv[3], sys.argv[4]
    a = Image.open(pa).convert("RGB")
    b = Image.open(pb).convert("RGB")
    h = max(a.size[1], b.size[1])
    w = a.size[0] + b.size[0] + 8
    sheet = Image.new("RGB", (w, h), (20, 20, 20))
    sheet.paste(a, (0, 0))
    sheet.paste(b, (a.size[0] + 8, 0))
    sheet.save(out)
    print("pair %s + %s -> %s %s" % (pa, pb, out, sheet.size))
