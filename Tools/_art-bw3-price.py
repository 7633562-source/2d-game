# ART-BW3 "THE ROW'S RHYTHM" pricing (v2). The row = the fan's 16 primary + 4 secondary plates.
# The rhythm = a 3-slot BEAT whose drawn POINT differs (needle / blade / full - the tip's power,
# ink floor and fade sit apart far enough to read at the take's 635 px/m) + a DRIFT along the
# plate's own index (the outer plates' points run finer), assigned by a non-cyclic 4-step pattern
# so no short stamp cycle repeats. No tip insets: probe 14 of ART-BW2 priced them out (worst
# notch 19.9 -> 24.8-26.0 mm).
# Raster conventions mirror _art-bw2-final.py (bend lateral, hlo = trailing side), so the mm stay
# comparable with B-WING-10 / ART-BW2.
import math
import sys

sys.path.insert(0, r"E:\Games\2D Game\My project\Tools")
import importlib

sim = importlib.import_module("_art-bw2-sim")
from PIL import Image, ImageDraw, ImageFilter

PX = 635.0
W2_PRI, W2_SEC = 0.064, 0.046
BW2F = [(0.62, 1.15, 1.95, 0.14, +0.022, 0.060),
        (0.68, 1.45, 2.20, 0.12, -0.018, 0.050),
        (0.58, 1.00, 2.50, 0.17, +0.014, 0.080)]

# beat v1 (mild) - kept for the record
BEAT1 = [
    (0.62, 1.15, 2.75, 0.16, +0.026, 0.080, 0.66, 63.0),
    (0.68, 1.45, 2.20, 0.12, -0.020, 0.050, 0.74, 64.0),
    (0.58, 1.00, 1.70, 0.19, +0.016, 0.035, 0.86, 65.0),
]
# beat v2: the point's own contrast widened (needle holds its width then needles; the full point
# tapers early and stays wide and inky)
BEAT2 = [
    (0.62, 1.15, 3.20, 0.17, +0.030, 0.090, 0.60, 62.0),
    (0.68, 1.45, 2.20, 0.12, -0.020, 0.050, 0.74, 64.0),
    (0.58, 0.95, 1.35, 0.22, +0.018, 0.030, 0.90, 66.0),
]
DRIFT_TP = 0.50
DRIFT_FLOOR = -0.10
DRIFT_W_MM = 1.0


def slot_of(k):
    return (k + k // 4) % 3


def n_of(group):
    return 16 if group == 0 else 4


def shape_rhythm(idx, w0, L, beat=BEAT2, drift=True, wbeat=True, ins=None, wmm3=None):
    group = 0 if idx < 16 else 1
    k = idx if group == 0 else idx - 16
    n = n_of(group)
    d = (k / (n - 1.0)) if n > 1 else 0.0
    b, bp, tp, asym, bend, fade, floor, wmm = beat[slot_of(k)]
    ins_v = ins[slot_of(k)] if ins else 0.0
    if drift:
        tp += DRIFT_TP * (d - 0.5)
        floor = max(0.35, min(1.0, floor + 0.05 + DRIFT_FLOOR * d))
        wmm += DRIFT_W_MM * (d - 0.5)
        if ins:
            ins_v += 0.010 * (d - 0.5)
    if not wbeat:
        wmm = 64.0
    if wmm3:
        wmm = wmm3[slot_of(k)] + (DRIFT_W_MM * (d - 0.5) if drift else 0.0)
    if group == 1:
        wmm = 46.0 + (wmm - 64.0) * 0.72
    half = (wmm / 1000.0) * 0.5

    def se_of(s):
        return min(1.0, s / (1.0 - ins_v)) if ins_v > 0 else s

    def hl(s):
        se = se_of(s)
        sh = math.sin(math.pi * se)
        return half * (1 - b * se ** bp) * (1 - se ** tp) * (1 - asym * 0.7 * sh)

    def hr(s):
        se = se_of(s)
        sh = math.sin(math.pi * se)
        return half * (1 - b * se ** bp) * (1 - se ** tp) * (1 + asym * sh)

    def off(s):
        se = se_of(s)
        return bend * L * math.sin(math.pi * se)

    return hl, hr, off, fade, floor


def shape_bw2(idx, w0, L):
    b, bp, tp, asym, bend, fade = BW2F[idx % 3]
    half = w0 * 0.5

    def hl(s):
        sh = math.sin(math.pi * s)
        return half * (1 - b * s ** bp) * (1 - s ** tp) * (1 - asym * 0.7 * sh)

    def hr(s):
        sh = math.sin(math.pi * s)
        return half * (1 - b * s ** bp) * (1 - s ** tp) * (1 + asym * sh)

    def off(s):
        return bend * L * math.sin(math.pi * s)

    return hl, hr, off, fade, 0.72


def add_plate(cov, a, L, t, idx, fn, w0p, w0s, res=0.0005, ox=0, oy=0, n=120):
    r = math.radians(t)
    ax, ay = math.sin(r), math.cos(r)
    ux, uy = math.cos(r), -math.sin(r)
    hl, hr, off, fade, floor = fn(idx, w0p if idx < 16 else w0s, L)
    for k in range(n + 1):
        s = k / float(n)
        hlo, hhi = hl(s), hr(s)
        o = off(s)
        cxx, cyy = ax * (s * L) + ux * o, a + ay * (s * L) + uy * o
        steps = max(2, int((hlo + hhi) / res) + 1)
        for j in range(steps + 1):
            tt = j / float(steps)
            x = cxx - ux * hlo + (ux * hhi + ux * hlo) * tt
            y = cyy - uy * hlo + (uy * hhi + uy * hlo) * tt
            key = (int(round(ox + x / res)), int(round(oy - y / res)))
            cov[key] = cov.get(key, 0) + 1


def raster(fn, w0p=0.064, w0s=0.046, res=0.0005, box=0.46, n=120):
    W = H = int(box / res)
    ox, oy = W // 2, H - 40
    cov = {}
    att, ln, ang = sim.PRI
    satt, sln, sang = sim.SEC
    for i in range(len(att)):
        add_plate(cov, att[i], ln[i], ang[i], i, fn, w0p, w0s, res, ox, oy, n)
    for i in range(len(satt)):
        add_plate(cov, satt[i], sln[i], sang[i], i + 16, fn, w0p, w0s, res, ox, oy, n)
    return cov, W, H, ox, oy


def price(tag, fn, w0p=0.064, w0s=0.046):
    cov, W, H, ox, oy = raster(fn, w0p, w0s)
    notches, pk = sim.rim_notches(cov, ox, oy)
    notches.sort(reverse=True)
    mean = sum(n[0] for n in notches) / max(1, len(notches)) * 1000
    print("%-42s peaks %2d  mean %5.1f  worst %5.1f" %
          (tag, len(pk), mean, notches[0][0] * 1000 if notches else 0))
    return mean, (notches[0][0] * 1000 if notches else 0)


def render(name, fn, sigma=0.65, z=4):
    CW, CH = int(0.62 * PX), int(0.55 * PX)
    im = Image.new("RGB", (CW, CH), sim.SKY)
    ox, oy = int(CW * 0.30), CH - 4
    att, ln, ang = sim.PRI
    satt, sln, sang = sim.SEC
    order = [(att[i], ln[i], ang[i], i) for i in range(len(att))]
    order += [(satt[i], sln[i], sang[i], i + 16) for i in range(len(satt))]
    for a, L, t, idx in order:
        hl, hr, off, fade, floor = fn(idx, 0.064 if idx < 16 else 0.046, L)
        r = math.radians(t)
        ax, ay = math.sin(r), math.cos(r)
        ux, uy = math.cos(r), -math.sin(r)
        left, right = [], []
        for k in range(201):
            s = k / 200.0
            o = off(s)
            cxx, cyy = ax * (s * L) + ux * o, a + ay * (s * L) + uy * o
            left.append((ox + (cxx - ux * hl(s)) * PX, oy - (cyy - uy * hl(s)) * PX))
            right.append((ox + (cxx + ux * hr(s)) * PX, oy - (cyy + uy * hr(s)) * PX))
        pad = 12
        bx0 = int(min(p[0] for p in left + right)) - pad
        by0 = int(min(p[1] for p in left + right)) - pad
        bx1 = int(max(p[0] for p in left + right)) + pad
        by1 = int(max(p[1] for p in left + right)) + pad
        if bx1 <= bx0 or by1 <= by0:
            continue
        m = Image.new("L", (bx1 - bx0, by1 - by0), 0)
        dm = ImageDraw.Draw(m)
        dm.polygon([(p[0] - bx0, p[1] - by0) for p in left] +
                   [(p[0] - bx0, p[1] - by0) for p in right[::-1]], fill=255)
        px = m.load()
        for yy in range(m.size[1]):
            for xx in range(m.size[0]):
                if px[xx, yy] == 0:
                    continue
                wx = (xx + bx0 - ox) / PX
                wy = (oy - (yy + by0)) / PX
                s = (wx * ax + (wy - a) * ay) / L
                if s > 1 - fade:
                    px[xx, yy] = int(px[xx, yy] * (floor + (1 - floor) * (1 - s) / fade))
        m = m.filter(ImageFilter.GaussianBlur(sigma))
        cov = m.point(lambda v: int(v * sim.ALPHA))
        layer = Image.new("RGB", m.size, (int(sim.FILL[0] * 255), int(sim.FILL[1] * 255),
                                          int(sim.FILL[2] * 255)))
        im.paste(layer, (bx0, by0), cov)
    big = im.resize((CW * z, CH * z), Image.LANCZOS)
    out = r"E:\Games\2D Game\My project\Trials\Runs\reviews\aBW3_sim_%s.png" % name
    big.save(out)
    print("preview", name, "->", out, big.size)
    return out


if __name__ == "__main__":

    def tree_shape(i, w, L):
        f, fr, off, _ = sim.shape_cur(i, w, L)
        return f, fr, off, 0.0, 1.0

    print("== rim metric (one ruler; mm) ==")
    price("tree w58 single", tree_shape, 0.058, 0.040)
    price("BW2 final w64 family", shape_bw2)
    price("R beat1 only (no drift)", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT1, drift=False, wbeat=False))
    price("R beat2 only (no drift)", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=False, wbeat=False))
    price("R beat2 + drift", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=True, wbeat=False))
    price("R beat2 + drift + width beat", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=True, wbeat=True))
    price("R + insets 0/4.5/8.5", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=True, wbeat=True, ins=(0.0, 0.045, 0.085)))
    price("R + insets 0/6/12", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=True, wbeat=True, ins=(0.0, 0.06, 0.12)))
    price("R + insets 0/3/6", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=True, wbeat=True, ins=(0.0, 0.03, 0.06)))
    price("R + insets 0/2/4", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=True, wbeat=True, ins=(0.0, 0.02, 0.04)))
    price("R beat2 + ins 0/2/4 nodrift", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=False, wbeat=False, ins=(0.0, 0.02, 0.04)))
    price("R beat2 + ins 0/0/12 nodrift", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=False, wbeat=False, ins=(0.0, 0.0, 0.12)))
    price("R beat2 + ins 0/0/2 nodrift", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=False, wbeat=False, ins=(0.0, 0.0, 0.02)))
    price("R + widths 60/64/68", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=True, wbeat=True, wmm3=(60.0, 64.0, 68.0)))
    price("R + widths 58/64/70", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=True, wbeat=True, wmm3=(58.0, 64.0, 70.0)))
    price("R + widths 56/64/72", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=True, wbeat=True, wmm3=(56.0, 64.0, 72.0)))
    price("R + widths 58/64/70 nodrift", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=False, wbeat=True, wmm3=(58.0, 64.0, 70.0)))
    price("R + insets 0/6 no drift", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=False, wbeat=True, ins=(0.0, 0.06, 0.06)))
    price("R + insets 0/5/10", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=True, wbeat=True, ins=(0.0, 0.05, 0.10)))

    print("== previews at take scale ==")
    render("bw2final", shape_bw2)
    render("r_beat2", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=False, wbeat=False))
    render("r_beat2d", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=True, wbeat=False))
    render("r_beat2dw", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=True, wbeat=True))
    render("r_inset36", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=True, wbeat=True, ins=(0.0, 0.03, 0.06)))
    render("r_inset510", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=True, wbeat=True, ins=(0.0, 0.05, 0.10)))
    render("r_w5870", lambda i, w, L: shape_rhythm(i, w, L, beat=BEAT2, drift=True, wbeat=True, wmm3=(58.0, 64.0, 70.0)))

    print("== beat2 (tp, fade, floor, wmm) ==")
    for i, r in enumerate(BEAT2):
        print("  %s tp=%.2f fade=%.3f floor=%.2f w=%.1f" % ("ABC"[i], r[2], r[5], r[6], r[7]))
    print("== the pattern across 16 primaries ==")
    print("  slots:", "".join("ABC"[slot_of(k)] for k in range(16)))
    print("  fades:", " ".join("%.3f" % shape_rhythm(k, w, sim.PRI[1][k])[3] for k, w in ((k, 0.064) for k in range(16))))
    print("  floors:", " ".join("%.2f" % shape_rhythm(k, w, sim.PRI[1][k])[4] for k, w in ((k, 0.064) for k in range(16))))
