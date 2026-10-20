# ART-BW2 probe 3: 1:1 ASCII of the fan + x12 zoom of the trailing edge, after-state.
import os
from PIL import Image, ImageDraw, ImageFont

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"
OUT = r"E:\Games\2D Game\My project\Trials\Runs\reviews"
BOX = (690, 700, 1090, 1060)
FONT = "C:/Windows/Fonts/arial.ttf"
label, n, tag = "aBW1f2_wander", 95, "after_aBW1f2_f95"

im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, n))).convert("RGB")
crop = im.crop(BOX)
px = crop.load()

# dense slate = the fan body tones; pale = single plate over sky
def cls(p):
    r, g, b = p[:3]
    if r < 136 and g - r >= 22 and b >= g and b - r >= 26:
        return '#' if r < 120 else '+'
    return ' '

X0, X1, Y0, Y1 = 180, 322, 52, 212
for y in range(Y0, Y1, 2):
    line = "".join(cls(px[x, y]) for x in range(X0, X1))
    print("%3d|%s" % (y, line))

# x12 zoom of the trailing-edge window
win = (X0, Y0, X1, Y1)
sub = crop.crop(win)
z = 5
big = sub.resize((sub.size[0] * z, sub.size[1] * z), Image.LANCZOS)
d = ImageDraw.Draw(big)
f = ImageFont.truetype(FONT, 20)
d.rectangle([0, 0, big.size[0], 26], fill=(0, 0, 0))
d.text((6, 3), "%s win=%s x%d" % (tag, str(win), z), font=f, fill=(255, 255, 0))
o = os.path.join(OUT, "aBW2_trail_%s.png" % tag)
big.save(o)
print("->", o, big.size)
