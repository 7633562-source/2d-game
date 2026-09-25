# ART-BW2 probe 5: the pricing PAIR - A = the tree state (hard wedge, w58), B = the candidate
# (the 3-silhouette family, soft edges, drawn-out tip, picture width w64/w46), same frame, x4
# of the take scale. Same look as the instrument's pair sheet (A|B, letters).
import sys
sys.path.insert(0, r"E:\Games\2D Game\My project\Tools")
import importlib
sim = importlib.import_module("_art-bw2-sim")
from PIL import Image, ImageDraw, ImageFont

FONT = "C:/Windows/Fonts/arial.ttf"

sim.W0P, sim.W0S = 0.058, 0.040
pA, imA = sim.render("pairA_cur", sim.shape_cur, 0.0)
sim.W0P, sim.W0S = 0.064, 0.046
pB, imB = sim.render("pairB_cand", sim.shape_fam, 1.1)
sim.W0P, sim.W0S = 0.058, 0.040

# trim both cells to the same window around the fan, then compose with a caption bar
def trim(im, margin=14):
    a = im.load()
    W, H = im.size
    sky = a[4, 4]
    xs, ys = [], []
    for y in range(0, H, 2):
        for x in range(0, W, 2):
            px = a[x, y]
            if sum(abs(px[i] - sky[i]) for i in range(3)) > 30:
                xs.append(x)
                ys.append(y)
    return im.crop((max(0, min(xs) - margin), max(0, min(ys) - margin),
                    min(W, max(xs) + margin), min(H, max(ys) + margin)))

A, B = trim(imA), trim(imB)
W = max(A.width, B.width)
H = max(A.height, B.height)
bar = 40
sheet = Image.new("RGB", (2 * W + 12, H + bar), (20, 20, 20))
for i, (cell, letter, tag) in enumerate(((A, "A", "tree: hard wedge, one silhouette"),
                                         (B, "B", "candidate: family, soft edge"))):
    if cell.width < W:
        cell = cell.crop((0, 0, W, cell.height))
    sheet.paste(cell, (i * (W + 12), bar))
d = ImageDraw.Draw(sheet)
f1 = ImageFont.truetype(FONT, 26)
d.text((8, 8), "A = tree (w58, hard)", font=f1, fill=(255, 255, 0))
d.text((W + 20, 8), "B = candidate (w64, family, soft)", font=f1, fill=(255, 255, 0))
out = r"E:\Games\2D Game\My project\Trials\Runs\reviews\aBW2_sim_pair.png"
sheet.save(out)
print("pair ->", out, sheet.size)
print("A", A.size, "B", B.size)
