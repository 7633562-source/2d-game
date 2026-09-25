# ART-BW2 probe 2: find the fan by its slate tone, ASCII map + x6 zoom of the fan bbox.
import os
from PIL import Image, ImageDraw, ImageFont

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
OUT = r"E:\Games\2D Game\My project\Trials\Runs\reviews"
BOX = (690, 700, 1090, 1060)
FONT = "C:/Windows/Fonts/arial.ttf"
CASES = [("bW10f2", 93, "before_bW10f2_f93"), ("aBW1f2_wander", 95, "after_aBW1f2_f95")]


def slate(px):
    r, g, b = px[:3]
    return r < 136 and g - r >= 22 and b >= g and b - r >= 26


for label, n, tag in CASES:
    im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, n))).convert("RGB")
    crop = im.crop(BOX)
    W, H = crop.size
    px = crop.load()
    x0, y0, x1, y1, cnt = W, H, -1, -1, 0
    for y in range(H):
        for x in range(W):
            if slate(px[x, y]):
                cnt += 1
                if x < x0: x0 = x
                if x > x1: x1 = x
                if y < y0: y0 = y
                if y > y1: y1 = y
    print(tag, "slate bbox", (x0, y0, x1, y1), "n", cnt)
    # ascii map 40 cols
    cols, rows = 40, 18
    for ry in range(rows):
        line = ""
        for rx in range(cols):
            sx0, sx1 = W * rx // cols, W * (rx + 1) // cols
            sy0, sy1 = H * ry // rows, H * (ry + 1) // rows
            c = 0
            for yy in range(sy0, sy1, 2):
                for xx in range(sx0, sx1, 2):
                    if slate(px[xx, yy]):
                        c += 1
            frac = c / max(1, ((sy1 - sy0) // 2 + 1) * ((sx1 - sx0) // 2 + 1))
            line += " .:-=+*#%@"[min(9, int(frac * 10))] if frac > 0.02 else " "
        print("%3d|%s" % (H * ry // rows, line))
    pad = 10
    bx = (max(0, x0 - pad), max(0, y0 - pad), min(W, x1 + pad), min(H, y1 + pad))
    sub = crop.crop(bx)
    z = 6
    big = sub.resize((sub.size[0] * z, sub.size[1] * z), Image.LANCZOS)
    d = ImageDraw.Draw(big)
    f = ImageFont.truetype(FONT, 20)
    d.rectangle([0, 0, big.size[0], 26], fill=(0, 0, 0))
    d.text((6, 3), "%s box=%s x6" % (tag, str(bx)), font=f, fill=(255, 255, 0))
    o = os.path.join(OUT, "aBW2_fan_%s.png" % tag)
    big.save(o)
    print("  ->", o, big.size)
