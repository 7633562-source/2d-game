# ART-BW2 probe 1: locate the fan and zoom its trailing edge at x8, both states.
# Reads only existing takes on disk (no re-run).
import os
from PIL import Image, ImageDraw, ImageFont

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
OUT = r"E:\Games\2D Game\My project\Trials\Runs\reviews"
BOX = (690, 700, 1090, 1060)   # the bw11 mic crop
FONT = "C:/Windows/Fonts/arial.ttf"

CASES = [("bW10f2", 93, "before_bW10f2_f93"), ("aBW1f2_wander", 95, "after_aBW1f2_f95")]


def slate_mask(im):
    w, h = im.size
    px = im.load()
    x0, y0, x1, y1 = w, h, -1, -1
    n = 0
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y][:3]
            # slate tones: blue-dominant, red well below blue; sky is (140,199,240)
            if b - r >= 22 and 70 <= r <= 175 and g > r and b > g:
                n += 1
                if x < x0: x0 = x
                if x > x1: x1 = x
                if y < y0: y0 = y
                if y > y1: y1 = y
    return (x0, y0, x1, y1, n)


for label, n, tag in CASES:
    p = os.path.join(RUNS, "shot_%s_%d.png" % (label, n))
    im = Image.open(p).convert("RGB")
    print(tag, "shot", im.size, os.path.getsize(p))
    crop = im.crop(BOX)
    x0, y0, x1, y1, cnt = slate_mask(crop)
    print("  slate bbox in crop:", (x0, y0, x1, y1), "px", cnt)
    # pad and zoom x8
    pad = 6
    bx = (max(0, x0 - pad), max(0, y0 - pad), min(crop.size[0], x1 + pad), min(crop.size[1], y1 + pad))
    sub = crop.crop(bx)
    z = 8
    big = sub.resize((sub.size[0] * z, sub.size[1] * z), Image.NEAREST)
    d = ImageDraw.Draw(big)
    f = ImageFont.truetype(FONT, 22)
    d.rectangle([0, 0, big.size[0], 30], fill=(0, 0, 0))
    d.text((6, 4), "%s  crop=%s x8 NEAREST" % (tag, str(bx)), font=f, fill=(255, 255, 0))
    o = os.path.join(OUT, "aBW2_zoom_%s.png" % tag)
    big.save(o)
    print("  ->", o, big.size)
