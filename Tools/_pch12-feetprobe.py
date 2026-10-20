# B-PERCH-12 probe: where do the seated bird's legs sit against the wood's top edge?
# Reads one shot of take bPCH11a_tree (the seat frame), classifies pixels and prints
# the per-column wood top edge under the bird + the leg pixels' own span.
import sys
from PIL import Image

TAKE = sys.argv[1] if len(sys.argv) > 1 else "bPCH11a_tree"
FRAME = int(sys.argv[2]) if len(sys.argv) > 2 else 259
X0, X1 = 780, 980
Y0, Y1 = 240, 420

SKY = (140, 199, 240)


def is_sky(c):
    return abs(c[0] - 140) < 22 and abs(c[1] - 199) < 22 and abs(c[2] - 240) < 22


def is_wood(c):
    r, g, b = c
    return r > 60 and r < 190 and g > 30 and g < 120 and b < 90 and r > g > b


def is_leaf(c):
    r, g, b = c
    return g > r + 8 and g > b + 20


def is_body(c):
    r, g, b = c
    return b > r + 20 and b > 120 and r > 60 and not is_sky(c)


def is_leg(c):
    # the picture's leg/foot tones: cream / very light grey, low saturation, bright
    r, g, b = c
    mx, mn = max(c), min(c)
    return mx > 170 and (mx - mn) < 60 and not is_sky(c)


im = Image.open(r"Trials/Runs/shot_%s_%d.png" % (TAKE, FRAME)).convert("RGB")
px = im.load()

print("wood top edge per column (x: first wood y scanning down):")
tops = {}
for x in range(X0, X1, 2):
    for y in range(Y0, Y1):
        if is_wood(px[x, y]):
            tops[x] = y
            break
legs = []
for y in range(Y0, Y1):
    for x in range(X0, X1):
        if is_leg(px[x, y]):
            legs.append((x, y))
if legs:
    xs = [p[0] for p in legs]
    ys = [p[1] for p in legs]
    print("leg-ish pixels: n=%d x %d..%d  y %d..%d" % (len(legs), min(xs), max(xs), min(ys), max(ys)))
    byx = {}
    for x, y in legs:
        byx.setdefault(x // 4 * 4, []).append(y)
    for x in sorted(byx):
        ys2 = byx[x]
        print("  x~%d: n=%2d y %d..%d" % (x, len(ys2), min(ys2), max(ys2)))
else:
    print("no leg pixels found")
row = []
for x in range(X0, X1, 4):
    row.append("%d:%s" % (x, tops.get(x, "-")))
print("tops:", " ".join(row))
