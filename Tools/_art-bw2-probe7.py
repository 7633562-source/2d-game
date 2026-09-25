# ART-BW2 probe 7: where are the worst notches? (angles + radii), tree vs candidate.
import sys
sys.path.insert(0, r"E:\Games\2D Game\My project\Tools")
import importlib
sim = importlib.import_module("_art-bw2-sim")
fin = importlib.import_module("_art-bw2-final")

HAPPY = [(0.62, 1.15, 2.10, 0.13, 0.020, 0.10),
         (0.68, 1.45, 2.00, 0.09, -0.015, 0.08),
         (0.58, 1.00, 2.20, 0.16, 0.010, 0.13)]


def show(tag, family, w0p, w0s):
    while len(family) < 3:
        family = list(family) + [family[-1]]
    fin.FAMILY = [tuple(f) for f in family]
    cov, W, H, ox, oy = fin.raster_family(sim.PRI, sim.SEC, w0p, w0s)
    notches, pk = sim.rim_notches(cov, ox, oy)
    notches.sort(reverse=True)
    print("%s  peaks %d  mean %.1f  worst %.1f" % (tag, len(pk),
          sum(n[0] for n in notches) / max(1, len(notches)) * 1000, notches[0][0] * 1000))
    print("   worst: " + "  ".join("%.1fmm@%.0f..%.0f (r %.0f/%.0f)" %
          (n[0] * 1000, n[2], n[3], n[4] * 1000, n[5] * 1000) for n in notches[:5]))


show("tree w58", [(0.65, 1.4, 2.0, 0.0, 0.0, 0.0)], 0.058, 0.040)
show("cand w64 full family", fin.FAMILY, 0.064, 0.046)
show("cand w64 flat-tip family", HAPPY, 0.064, 0.046)
show("cand w64 no family", [(0.62, 1.15, 2.10, 0.0, 0.0, 0.0)], 0.064, 0.046)
