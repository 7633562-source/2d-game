# ART-BW2 pricing: the fan plates' FORM. Renders the fan at the take's own scale (635 px/m:
# birdCam 0.85 ortho -> 1080 px / 1.70 m) from the tree's own arrays (16 primaries + 4
# secondaries, B-WING-10 taper 0.65/1.4/2.0) and prices the candidates:
#   CUR   = the tree (hard-edged wedge, straight sides, one silhouette for all plates)
#   SOFT  = the tree's taper + a feathered (anti-aliased) margin
#   FEATH = SOFT + a longer, drawn-out tip whose coverage fades (a soft needle)
#   FAM   = FEATH + a 3-silhouette family by plate index (curved sides, bent axis, varied
#           tip length) - the eye's "no variation in the taper profile" lever
# Rim metric = a port of the bird lane's bw9_probe.notch_report (same raster/peaks/notch math,
# res 0.0005 m, box 0.46 m): it reproduces B-WING-10's 12.1 / 19.4 mm on the tree state, so the
# mm are comparable with that round.
import math
from PIL import Image, ImageDraw, ImageFilter

PX_TAKE = 635.0
W0P = 0.058
W0S = 0.040

PRI = ([0.0060, 0.0114, 0.0167, 0.0231, 0.0296, 0.0345, 0.0387, 0.0444, 0.0511, 0.0567, 0.0607, 0.0652, 0.0713, 0.0773, 0.0826, 0.0880],
       [0.0714, 0.0740, 0.0766, 0.0790, 0.0814, 0.0844, 0.0877, 0.0911, 0.0947, 0.0982, 0.1018, 0.1057, 0.1104, 0.1155, 0.1226, 0.1297],
       [96, 94.1, 92.3, 90, 87.7, 85, 82.2, 79.1, 75.9, 73.2, 71.3, 69.1, 65.8, 62.7, 60.3, 58])
SEC = ([0.008, 0.023, 0.045, 0.062], [0.044, 0.048, 0.052, 0.056], [133, 125, 113, 104])
TAPER = (0.65, 1.4, 2.0)

SKY = (140, 199, 240)
FILL = (0.35, 0.49, 0.59)
ALPHA = 0.42

# the family: (bodyB, bodyP, tipP, bend (axis curve, fraction of L), asym (side curve), tipFade)
FAMILY = [
    (0.62, 1.15, 2.30, +0.055, +0.13, 0.20),
    (0.68, 1.45, 1.85, -0.030, +0.09, 0.14),
    (0.58, 1.00, 2.60, +0.020, +0.16, 0.24),
]


def hw_cur(s, w0, taper=TAPER):
    b, bp, tp = taper
    return (w0 / 2) * (1 - b * s ** bp) * (1 - s ** tp)


def shape_cur(idx, w0, L):
    return (lambda s: hw_cur(s, w0)), (lambda s: hw_cur(s, w0)), (lambda s: 0.0), 0.0


def shape_soft(idx, w0, L):
    return (lambda s: hw_cur(s, w0)), (lambda s: hw_cur(s, w0)), (lambda s: 0.0), 0.0


def shape_feath(idx, w0, L):
    b, bp, tp = 0.62, 1.1, 2.2
    f = lambda s: (w0 / 2) * (1 - b * s ** bp) * (1 - s ** tp)
    return f, f, (lambda s: 0.0), 0.16


def shape_fam(idx, w0, L):
    b, bp, tp, bend, asym, fade = FAMILY[idx % 3]
    half = w0 / 2
    hl = lambda s: half * (1 - b * s ** bp) * (1 - s ** tp) * (1 + asym * math.sin(math.pi * s))
    hr = lambda s: half * (1 - b * s ** bp) * (1 - s ** tp) * (1 - asym * 0.7 * math.sin(math.pi * s))
    off = lambda s: bend * L * math.sin(math.pi * s)
    return hl, hr, off, fade


# --- the rim metric (port of bw9_probe) ------------------------------------------------------
def plates(pri, sec):
    att, ln, ang = pri
    satt, sln, sang = sec
    out = [(att[i], ln[i], ang[i], W0P) for i in range(len(att))]
    out += [(satt[i], sln[i], sang[i], W0S) for i in range(len(satt))]
    return out


def raster(pri, sec, prof, res=0.0005, box=0.46):
    W = H = int(box / res)
    ox, oy = W // 2, H - 40
    cov = {}
    for a, L, t, w0 in plates(pri, sec):
        r = math.radians(t)
        ax, ay = math.sin(r), math.cos(r)
        ux, uy = math.cos(r), -math.sin(r)
        n = 120
        for k in range(n + 1):
            s = k / float(n)
            h = prof(s, w0)
            cxx, cyy = ax * s * L, a + ay * s * L
            steps = max(2, int(2 * h / res) + 1)
            for j in range(steps + 1):
                tt = -h + 2 * h * j / float(steps)
                key = (int(round(ox + (cxx + ux * tt) / res)), int(round(oy - (cyy + uy * tt) / res)))
                cov[key] = cov.get(key, 0) + 1
    return cov, W, H, ox, oy


def rim_notches(cov, ox, oy, res_deg=0.25, lo=26.0, hi=140.0, res=0.0005):
    prof = []
    th = lo
    while th <= hi:
        r = math.radians(th)
        ux, uy = math.sin(r), math.cos(r)
        rr, last = 0.0, 0.0
        while rr < 0.34:
            key = (int(round(ox + ux * rr / res)), int(round(oy - uy * rr / res)))
            if key in cov:
                last = rr
            rr += res
        prof.append((th, last))
        th += res_deg
    vals = [v for _, v in prof]
    pk = []
    for i in range(6, len(vals) - 6):
        if vals[i] == max(vals[i - 6:i + 7]) and vals[i] > 0.02:
            if not pk or prof[i][0] - pk[-1][0] > 1.5:
                pk.append(prof[i])
    out = []
    for k in range(len(pk) - 1):
        (t0, r0), (t1, r1) = pk[k], pk[k + 1]
        seg = [(t, v) for t, v in prof if t0 < t < t1]
        if not seg:
            continue
        best = None
        for t, v in seg:
            chord = r0 + (r1 - r0) * (t - t0) / (t1 - t0)
            d = chord - v
            if best is None or d > best[0]:
                best = (d, t)
        out.append((best[0], best[1], t0, t1, r0, r1))
    return out, pk


def notch_report(tag, pri, sec, prof, fade=0.0):
    cov, W, H, ox, oy = raster(pri, sec, prof)
    notches, pk = rim_notches(cov, ox, oy)
    notches.sort(reverse=True)
    mean = sum(n[0] for n in notches) / max(1, len(notches)) * 1000
    print("%-28s peaks %2d  mean notch %5.1f  worst %5.1f" %
          (tag, len(pk), mean, notches[0][0] * 1000 if notches else 0))
    return mean, (notches[0][0] * 1000 if notches else 0)


# --- render at the take scale ------------------------------------------------------------------
def render(name, shape_fn, soft_px, z=4, box=(0.62, 0.55)):
    CW, CH = int(box[0] * PX_TAKE), int(box[1] * PX_TAKE)
    im = Image.new("RGB", (CW, CH), SKY)
    ox, oy = int(CW * 0.30), CH - 4
    for idx, (a, L, t, w0) in enumerate(plates(PRI, SEC)):
        hl, hr, off, fade = shape_fn(idx, w0, L)
        r = math.radians(t)
        ax, ay = math.sin(r), math.cos(r)
        ux, uy = math.cos(r), -math.sin(r)
        n = 200
        left, right = [], []
        for k in range(n + 1):
            s = k / float(n)
            o = off(s)
            cxx, cyy = ax * (s * L + o), a + ay * (s * L + o)
            left.append((ox + (cxx + ux * -hl(s)) * PX_TAKE, oy - (cyy + uy * -hl(s)) * PX_TAKE))
            right.append((ox + (cxx + ux * hr(s)) * PX_TAKE, oy - (cyy + uy * hr(s)) * PX_TAKE))
        pad = 10
        bx0 = int(min(p[0] for p in left + right)) - pad
        by0 = int(min(p[1] for p in left + right)) - pad
        bx1 = int(max(p[0] for p in left + right)) + pad
        by1 = int(max(p[1] for p in left + right)) + pad
        bw, bh = bx1 - bx0, by1 - by0
        if bw <= 2 or bh <= 2:
            continue
        m = Image.new("L", (bw, bh), 0)
        dm = ImageDraw.Draw(m)
        poly = [(p[0] - bx0, p[1] - by0) for p in left] + [(p[0] - bx0, p[1] - by0) for p in right[::-1]]
        dm.polygon(poly, fill=255)
        if fade > 0.0:
            px = m.load()
            for yy in range(bh):
                for xx in range(bw):
                    if px[xx, yy] == 0:
                        continue
                    wx = (xx + bx0 - ox) / PX_TAKE
                    wy = (oy - (yy + by0)) / PX_TAKE
                    s = (wx * ax + (wy - a) * ay) / L
                    if s > 1 - fade:
                        px[xx, yy] = int(px[xx, yy] * max(0.0, (1 - s) / fade))
        if soft_px > 0:
            m = m.filter(ImageFilter.GaussianBlur(soft_px))
        cov = m.point(lambda v: int(v * ALPHA))
        layer = Image.new("RGB", (bw, bh),
                          (int(FILL[0] * 255), int(FILL[1] * 255), int(FILL[2] * 255)))
        im.paste(layer, (bx0, by0), cov)
    big = im.resize((CW * z, CH * z), Image.LANCZOS)
    out = r"E:\Games\2D Game\My project\Trials\Runs\reviews\aBW2_sim_%s.png" % name
    big.save(out)
    return out, big


if __name__ == "__main__":
    print("== the rim metric (port of bw9_probe; mm) ==")
    notch_report("CUR 0.65/1.4/2.0", PRI, SEC, lambda s, w0: hw_cur(s, w0, TAPER))
    notch_report("FEATH 0.62/1.1/2.2", PRI, SEC, lambda s, w0: hw_cur(s, w0, (0.62, 1.1, 2.2)))

    for tag, fn, soft in (("cur", shape_cur, 0.0),
                          ("soft", shape_soft, 1.1),
                          ("feath", shape_feath, 1.1),
                          ("fam", shape_fam, 1.1)):
        p, im = render(tag, fn, soft)
        print(tag, "->", p, im.size)
