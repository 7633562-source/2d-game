# B-PERCH-12: coarse ASCII map of a shot - where the wood, the bird and the legs are.
# Usage: python Tools/_pch12-map.py <take> <frame> [x0 y0 x1 y1 step]
import sys
from PIL import Image

take = sys.argv[1]
fr = int(sys.argv[2])
x0, y0, x1, y1, st = (800, 240, 1000, 420, 4)
if len(sys.argv) >= 8:
    x0, y0, x1, y1, st = (int(v) for v in sys.argv[3:8])


def cls(c):
    r, g, b = c
    if abs(r - 140) < 22 and abs(g - 199) < 22 and abs(b - 240) < 22:
        return '.'
    mx, mn = max(c), min(c)
    if mx > 170 and mx - mn < 60:
        return 'l'
    if r > 60 and r < 190 and 30 < g < 120 and b < 90 and r > g > b:
        return '#'
    if g > r + 8 and g > b + 20:
        return 'g'
    if b > r + 20 and b > 120:
        return 'B'
    if mx < 90:
        return 'x'
    return '?'


im = Image.open(r"Trials/Runs/shot_%s_%d.png" % (take, fr)).convert("RGB")
px = im.load()
hdr = "     " + "".join(str((x // 100) % 10) if x % 20 == 0 else " " for x in range(x0, x1, st))
print("take=%s frame=%d  x %d..%d y %d..%d step %d" % (take, fr, x0, x1, y0, y1, st))
print(hdr)
for y in range(y0, y1, st):
    print("%4d %s" % (y, "".join(cls(px[x, y]) for x in range(x0, x1, st))))
