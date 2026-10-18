# ART-BW2 probe 6: which element of the final candidate moves the rim's worst notch?
import sys
sys.path.insert(0, r"E:\Games\2D Game\My project\Tools")
import importlib
sim = importlib.import_module("_art-bw2-sim")
fin = importlib.import_module("_art-bw2-final")

FULL = [list(f) for f in fin.FAMILY]


def price(tag, family, w0p=0.064, w0s=0.046):
    fin.FAMILY = [tuple(f) for f in family]
    cov, W, H, ox, oy = fin.raster_family(sim.PRI, sim.SEC, w0p, w0s)
    notches, pk = sim.rim_notches(cov, ox, oy)
    notches.sort(reverse=True)
    print("%-34s peaks %2d  mean %5.1f  worst %5.1f" %
          (tag, len(pk), sum(n[0] for n in notches) / max(1, len(notches)) * 1000,
           notches[0][0] * 1000))
    worst = notches[0]
    return worst


def variant(bend=None, asym=None, tap=None):
    out = []
    for f in FULL:
        g = list(f)
        if bend is not None:
            g[4] = bend
        if asym is not None:
            g[3] = asym
        if tap is not None:
            g[0], g[1], g[2] = tap
        out.append(g)
    return out


price("w64 family FULL", FULL)
price("w64 family no bend", variant(bend=0.0))
price("w64 family no asym", variant(asym=0.0))
price("w64 family no bend+asym", variant(bend=0.0, asym=0.0))
price("w64 one taper (family=same)", variant(tap=(0.65, 1.4, 2.0), bend=0.0, asym=0.0))
price("w58 family FULL", FULL, 0.058, 0.040)
price("w64 tree taper", variant(tap=(0.65, 1.4, 2.0), bend=0.0, asym=0.0), 0.064, 0.046)
w = price("w62 family FULL", FULL, 0.062, 0.044)
