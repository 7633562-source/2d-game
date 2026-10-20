# ART-BW2 final candidate v2: the exact shape the art lane's canvas will draw (mirrored from the
# C# constants), priced on the bird lane's rim instrument (port of bw9_probe) and previewed at the
# take's own scale (635 px/m, x4 = the instrument's sheet scale) WITH the feathered margin and the
# faded tip emulated (a per-plate coverage mask: solid body, a ramp of FEATHER metres, the tip's
# last `fade` of the length dropping to TIP_FLOOR).
import math
import sys
sys.path.insert(0, r"E:\Games\2D Game\My project\Tools")
import importlib
sim = importlib.import_module("_art-bw2-sim")
from PIL import Image, ImageDraw, ImageFilter

PX = 635.0
W_PRI = 0.064
W_SEC = 0.046
FEATHER = 0.0032
TIP_FLOOR = 0.72
FAMILY = [(0.62, 1.15, 1.95, 0.14, 0.022, 0.06, 0.000),
          (0.68, 1.45, 2.20, 0.12, -0.018, 0.05, 0.000),
          (0.58, 1.00, 2.50, 0.17, 0.014, 0.08, 0.000)]


def shape(idx, w0, L, n=200):
    b, bp, tp, asym, bend, fade, ins = FAMILY[idx % len(FAMILY)]
    rows = []
    for k in range(n + 1):
        s = k / float(n)
        se = min(1.0, s / (1.0 - ins))
        hw = (w0 / 2) * (1 - b * se ** bp) * (1 - se ** tp)
        sh = math.sin(math.pi * se)
        rows.append((se, hw * (1 - asym * 0.7 * sh), hw * (1 + asym * sh), bend * L * sh, fade))
    return rows


# --- the rim metric with the family (same raster as the pricing tool) -------------------------
def raster_family(pri, sec, w0p, w0s, res=0.0005, box=0.46, n=120):
    W = H = int(box / res)
    ox, oy = W // 2, H - 40
    cov = {}
    att, ln, ang = pri
    satt, sln, sang = sec
    for i in range(len(att)):
        add_plate(cov, att[i], ln[i], ang[i], w0p, i, res, ox, oy, n)
    for i in range(len(satt)):
        add_plate(cov, satt[i], sln[i], sang[i], w0s, i + 1, res, ox, oy, n)
    return cov, W, H, ox, oy


def add_plate(cov, a, L, t, w0, idx, res, ox, oy, n):
    r = math.radians(t)
    ax, ay = math.sin(r), math.cos(r)
    ux, uy = math.cos(r), -math.sin(r)
    for k in range(n + 1):
        s, hlo, hhi, off, fade = shape(idx, w0, L, n)[k]
        cxx, cyy = ax * (s * L) + ux * off, a + ay * (s * L) + uy * off
        steps = max(2, int((hlo + hhi) / res) + 1)
        for j in range(steps + 1):
            tt = j / float(steps)
            x = cxx - ux * hlo + (ux * hhi + ux * hlo) * tt
            y = cyy - uy * hlo + (uy * hhi + uy * hlo) * tt
            key = (int(round(ox + x / res)), int(round(oy - y / res)))
            cov[key] = cov.get(key, 0) + 1


def price(tag, family, w0p=0.058, w0s=0.040):
    global FAMILY
    keep = FAMILY
    FAMILY = family
    cov, W, H, ox, oy = raster_family(sim.PRI, sim.SEC, w0p, w0s)
    notches, pk = sim.rim_notches(cov, ox, oy)
    notches.sort(reverse=True)
    print("%-30s peaks %2d  mean %5.1f  worst %5.1f" %
          (tag, len(pk), sum(n[0] for n in notches) / max(1, len(notches)) * 1000,
           notches[0][0] * 1000))
    FAMILY = keep


def render(name, family, w0p, w0s, sigma=0.65, z=4):
    global FAMILY
    keep = FAMILY
    FAMILY = family
    CW, CH = int(0.62 * PX), int(0.55 * PX)
    im = Image.new("RGB", (CW, CH), sim.SKY)
    ox, oy = int(CW * 0.30), CH - 4
    att, ln, ang = sim.PRI
    satt, sln, sang = sim.SEC
    order = [(att[i], ln[i], ang[i], w0p, i) for i in range(len(att))]
    order += [(satt[i], sln[i], sang[i], w0s, i + 1) for i in range(len(satt))]
    for a, L, t, w0, idx in order:
        rows = shape(idx, w0, L, n=160)
        r = math.radians(t)
        ax, ay = math.sin(r), math.cos(r)
        ux, uy = math.cos(r), -math.sin(r)
        left, right = [], []
        for s, hlo, hhi, off, fade in rows:
            cxx, cyy = ax * (s * L) + ux * off, a + ay * (s * L) + uy * off
            left.append((ox + (cxx - ux * hlo) * PX, oy - (cyy - uy * hlo) * PX))
            right.append((ox + (cxx + ux * hhi) * PX, oy - (cyy + uy * hhi) * PX))
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
        b, bp, tp, asym, bend, fade, ins = FAMILY[idx % len(FAMILY)]
        px = m.load()
        for yy in range(m.size[1]):
            for xx in range(m.size[0]):
                if px[xx, yy] == 0:
                    continue
                wx = (xx + bx0 - ox) / PX
                wy = (oy - (yy + by0)) / PX
                s = (wx * ax + (wy - a) * ay) / L
                if s > 1 - fade:
                    px[xx, yy] = int(px[xx, yy] * (TIP_FLOOR + (1 - TIP_FLOOR) * (1 - s) / fade))
        m = m.filter(ImageFilter.GaussianBlur(sigma))
        cov = m.point(lambda v: int(v * sim.ALPHA))
        layer = Image.new("RGB", m.size, (int(sim.FILL[0] * 255), int(sim.FILL[1] * 255),
                                          int(sim.FILL[2] * 255)))
        im.paste(layer, (bx0, by0), cov)
    big = im.resize((CW * z, CH * z), Image.LANCZOS)
    out = r"E:\Games\2D Game\My project\Trials\Runs\reviews\aBW2_%s.png" % name
    big.save(out)
    FAMILY = keep
    return out


if __name__ == "__main__":
    print("== the rim metric (bird lane's notch math; mm) ==")
    price("tree w58 single", [(0.65, 1.4, 2.0, 0.0, 0.0, 0.0, 0.0)])
    price("FINAL w64 family", FAMILY, W_PRI, W_SEC)
    price("FINAL w58 family", FAMILY)
    print("preview ->", render("cand_final", FAMILY, W_PRI, W_SEC))

    tile = Image.new("RGB", (3 * 150 + 20, 300), (30, 30, 30))
    d = ImageDraw.Draw(tile)
    for v in range(3):
        cw, ch = 128, 256
        cv = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        b, bp, tp, asym, bend, fade, ins = FAMILY[v]
        half = cw * 0.5
        ramp = max(1.0, FEATHER * cw / W_PRI)
        for y in range(ch):
            s = 1 - y / float(ch - 1)
            se = min(1.0, s / (1.0 - ins))
            hw = half * (1 - b * se ** bp) * (1 - se ** tp)
            sh = math.sin(math.pi * se)
            hi = hw * (1 + asym * sh)
            lo = hw * (1 - asym * 0.7 * sh)
            off = bend * ch * sh
            end = 1.0 if not (fade > 0 and se > 1 - fade) else TIP_FLOOR + (1 - TIP_FLOOR) * (1 - se) / fade
            for x in range(cw):
                dd = x + 0.5 - half - off
                halfNow = hi if dd >= 0 else lo
                ad = abs(dd)
                a = 1.0 if ad <= halfNow - ramp else (0.0 if ad >= halfNow else (halfNow - ad) / ramp)
                cv.putpixel((x, y), (255, 255, 255, int(255 * a * end)))
        tile.paste(cv, (10 + v * 150, 20), cv)
        d.text((10 + v * 150, 4), "variant %d (tip up = -Y)" % v, fill=(255, 255, 0))
    out = r"E:\Games\2D Game\My project\Trials\Runs\reviews\aBW2_canvas_family.png"
    tile.save(out)
    print("canvases ->", out)
