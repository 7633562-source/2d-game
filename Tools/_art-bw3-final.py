# ART-BW3 final mirror: the exact numbers now in Assets/Art/Bird/Rect.cs (RowBeat, RowSlot,
# the drift), priced on the bird lane's notch instrument (port in _art-bw2-sim) and previewed at
# the take's own scale (635 px/m, x4) with the feathered margin and the faded point emulated.
# The mirror must stay 1:1 with the C# - if the art changes, this file changes with it.
import math
import sys

sys.path.insert(0, r"E:\Games\2D Game\My project\Tools")
import importlib

sim = importlib.import_module("_art-bw2-sim")
from PIL import Image, ImageDraw, ImageFilter

PX = 635.0
FEATHER = 0.0032
W_PRI, W_SEC = 0.064, 0.046

# RowBeat: (bodyB, bodyP, tipP, curve, bend, fade, floor, width mm)
BEAT = [
    (0.62, 1.15, 3.10, 0.17, +0.028, 0.085, 0.64, 58.0),
    (0.68, 1.45, 2.20, 0.12, -0.020, 0.050, 0.76, 64.0),
    (0.58, 0.95, 1.40, 0.22, +0.018, 0.030, 0.90, 70.0),
]
TIP_POWER_DRIFT = 0.50
INK_FLOOR_DRIFT = 0.10
WIDTH_DRIFT_MM = 1.0


def row_slot(k):
    return (k + k // 4) % len(BEAT)


def plate_params(k, primary):
    n = 16 if primary else 4
    row_d = (k / (n - 1.0)) if n > 1 else 0.0
    b, bp, tp, asym, bend, fade, floor, wmm = BEAT[row_slot(k)]
    width_mm = wmm + WIDTH_DRIFT_MM * (row_d - 0.5)
    if not primary:
        width_mm *= W_SEC / W_PRI
    tp += TIP_POWER_DRIFT * (row_d - 0.5)
    floor = max(0.35, min(1.0, floor + 0.05 - INK_FLOOR_DRIFT * row_d))
    return b, bp, tp, asym, bend, fade, floor, width_mm, row_d


def shape(idx, w0, L):
    primary = idx < 16
    k = idx if primary else idx - 16
    b, bp, tp, asym, bend, fade, floor, wmm, row_d = plate_params(k, primary)
    half = (wmm / 1000.0) * 0.5

    def hl(s):
        sh = math.sin(math.pi * s)
        return half * (1 - b * s ** bp) * (1 - s ** tp) * (1 - asym * 0.7 * sh)

    def hr(s):
        sh = math.sin(math.pi * s)
        return half * (1 - b * s ** bp) * (1 - s ** tp) * (1 + asym * sh)

    def off(s):
        return bend * L * math.sin(math.pi * s)

    return hl, hr, off, fade, floor


def shape_bw2(idx, w0, L):
    fam = [(0.62, 1.15, 1.95, 0.14, +0.022, 0.060),
           (0.68, 1.45, 2.20, 0.12, -0.018, 0.050),
           (0.58, 1.00, 2.50, 0.17, +0.014, 0.080)]
    b, bp, tp, asym, bend, fade = fam[idx % 3]
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


def raster(fn, w0p=W_PRI, w0s=W_SEC, res=0.0005, box=0.46, n=120):
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


def price(tag, fn, w0p=W_PRI, w0s=W_SEC):
    cov, W, H, ox, oy = raster(fn, w0p, w0s)
    notches, pk = sim.rim_notches(cov, ox, oy)
    notches.sort(reverse=True)
    mean = sum(n[0] for n in notches) / max(1, len(notches)) * 1000
    print("%-36s peaks %2d  mean %5.1f  worst %5.1f" %
          (tag, len(pk), mean, notches[0][0] * 1000 if notches else 0))


def render(name, fn, sigma=0.65, z=4):
    CW, CH = int(0.62 * PX), int(0.55 * PX)
    im = Image.new("RGB", (CW, CH), sim.SKY)
    ox, oy = int(CW * 0.30), CH - 4
    att, ln, ang = sim.PRI
    satt, sln, sang = sim.SEC
    order = [(att[i], ln[i], ang[i], i) for i in range(len(att))]
    order += [(satt[i], sln[i], sang[i], i + 16) for i in range(len(satt))]
    for a, L, t, idx in order:
        hl, hr, off, fade, floor = fn(idx, W_PRI if idx < 16 else W_SEC, L)
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
    out = r"E:\Games\2D Game\My project\Trials\Runs\reviews\aBW3_%s.png" % name
    big.save(out)
    print("preview ->", out, big.size)
    return out


if __name__ == "__main__":

    def tree_shape(i, w, L):
        f, fr, off, _ = sim.shape_cur(i, w, L)
        return f, fr, off, 0.0, 1.0

    print("== the rim metric (bird lane's notch math; mm) ==")
    price("tree w58 single", tree_shape, 0.058, 0.040)
    price("BW2 final w64 family", shape_bw2)
    price("BW3 final: beat + pattern + drift", shape)

    render("cand_final", shape)

    print("== the beat (row: bodyB bodyP tipP curve bend fade floor width-mm) ==")
    for i, row in enumerate(BEAT):
        print("  %s %s" % ("ABC"[i], " ".join("%.3f" % v for v in row)))
    print("== per plate across the sixteen primaries ==")
    for k in range(16):
        b, bp, tp, asym, bend, fade, floor, wmm, d = plate_params(k, True)
        print("  k=%-2d slot=%s d=%.2f tipP=%.2f fade=%.3f floor=%.2f w=%.1fmm" %
              (k, "ABC"[row_slot(k)], d, tp, fade, floor, wmm))
    print("== the secondaries (k 0..3) ==")
    for k in range(4):
        b, bp, tp, asym, bend, fade, floor, wmm, d = plate_params(k, False)
        print("  k=%-2d slot=%s d=%.2f fade=%.3f floor=%.2f w=%.1fmm" %
              (k, "ABC"[row_slot(k)], d, fade, floor, wmm))

    tile = Image.new("RGB", (3 * 160 + 20, 340), (30, 30, 30))
    d0 = ImageDraw.Draw(tile)
    for v in range(3):
        cw, ch = 128, 300
        cv = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        b, bp, tp, asym, bend, fade, floor, wmm, d = plate_params(v, True)
        half = cw * 0.5
        ramp = max(1.0, FEATHER * cw / (wmm / 1000.0))
        for y in range(ch):
            s = 1 - y / float(ch - 1)
            sh = math.sin(math.pi * s)
            hw = half * (1 - b * s ** bp) * (1 - s ** tp)
            offx = bend * ch * sh
            hi = hw * (1 + asym * sh)
            lo = hw * (1 - asym * 0.7 * sh)
            end = 1.0 if not (fade > 0 and s > 1 - fade) else floor + (1 - floor) * (1 - s) / fade
            for x in range(cw):
                dd = x + 0.5 - half - offx
                side = hi if dd >= 0 else lo
                ad = abs(dd)
                a = 1.0 if ad <= side - ramp else (0.0 if ad >= side else (side - ad) / ramp)
                cv.putpixel((x, y), (255, 255, 255, int(255 * a * end)))
        tile.paste(cv, (10 + v * 160, 24), cv)
        d0.text((10 + v * 160, 5), "slot %s (root top, tip bottom)" % "ABC"[v], fill=(255, 255, 0))
    out = r"E:\Games\2D Game\My project\Trials\Runs\reviews\aBW3_beat_canvases.png"
    tile.save(out)
    print("canvases ->", out)
