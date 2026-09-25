# ART-BW2 probe 4: the notch metric vs the plate PICTURE width (the fan's plates' own ink).
# Uses the pricing sim's own metric (port of bw9_probe). Read-only, no build.
import sys
sys.path.insert(0, r"E:\Games\2D Game\My project\Tools")
import importlib
sim = importlib.import_module("_art-bw2-sim")

PRI, SEC = sim.PRI, sim.SEC
for tag, w0p, taper in (("cur  w58 0.65/1.4/2.0", 0.058, None),
                        ("wide w62 0.65/1.4/2.0", 0.062, None),
                        ("wide w66 0.65/1.4/2.0", 0.066, None),
                        ("wide w70 0.65/1.4/2.0", 0.070, None),
                        ("fam  w58 0.62/1.15/2.3", 0.058, "fam"),
                        ("fam  w62 0.62/1.15/2.3", 0.062, "fam"),
                        ("fam  w66 0.62/1.15/2.3", 0.066, "fam")):
    sim.W0P = w0p
    if taper is None:
        prof = lambda s, w: sim.hw_cur(s, w, sim.TAPER)
    else:
        prof = lambda s, w: sim.hw_cur(s, w, (0.62, 1.15, 2.30))
    sim.notch_report(tag, PRI, SEC, prof)
sim.W0P = 0.058
