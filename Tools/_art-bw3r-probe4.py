# ART-BW3R probe4: the dense mask run (columns with >=3 mask px) = the bird+fan's real extent,
# immune to sparse outliers. Also the leg band's own colours: the most common colours in the
# region just below the body (the legs), for the leg-identity claim.
import os

from PIL import Image

RUNS = r"E:\Games\2D Game\My project\Trials\Runs"


def mask(px, w, h):
    m = []
    for y in range(h):
        row = []
        for x in range(w):
            r, g, b = px[x, y]
            row.append(1 if (r < 135 and g < 192 and b < 232 and b > 90) else 0)
        m.append(row)
    return m


def dense(label, frame):
    im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, frame))).convert("RGB")
    w, h = im.size
    m = mask(im.load(), w, h)
    cols = [sum(m[y][x] for y in range(h)) for x in range(w)]
    best = run = 0
    x0 = bx0 = bx1 = 0
    for x in range(w):
        if cols[x] >= 3:
            if run == 0:
                x0 = x
            run += 1
            if run > best:
                best, bx0, bx1 = run, x0, x - 1
        else:
            run = 0
    rows = [sum(m[y][x] for x in range(bx0, bx1 + 1)) for y in range(h)] if best else []
    ys = [y for y in range(h) if rows and rows[y] >= 3]
    print("%-16s f%-4d dense cols %4d..%4d w %3d | rows %4d..%4d h %3d" % (
        label, frame, bx0, bx1, best, min(ys), max(ys), max(ys) - min(ys)) if ys else
        "%s f%d: none" % (label, frame))
    return bx0, bx1, ys


def tops(label, frame, box, k=12):
    im = Image.open(os.path.join(RUNS, "shot_%s_%d.png" % (label, frame))).convert("RGB").crop(box)
    c = {}
    for p in im.getdata():
        c[p] = c.get(p, 0) + 1
    top = sorted(c.items(), key=lambda kv: -kv[1])[:k]
    print("   %s f%d box %s: %s" % (label, frame, box, " ".join("%s x%d" % (t[0], t[1]) for t in top)))


for lab, fr in [("bVW1fv_wander", 92), ("bVW1fv_wander", 105), ("bVW1fv_wander", 40),
                ("aBW3b", 93), ("aBW1f2_wander", 95)]:
    dense(lab, fr)

# the legs in the new take's walk (f40, mask 902..1014 x 478..592): the band below the body
tops("bVW1fv_wander", 40, (930, 560, 1000, 610))
# the legs in the art take (aBW3b f93, mask 722..1004 x 764..1058): band below the body
tops("aBW3b", 93, (780, 950, 1000, 1060))
