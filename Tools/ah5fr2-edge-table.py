# Per-column edge table for the knee: thigh bottom, shin top, rest-limb bottom, at v=0.4.
import importlib.util
import os

import numpy as np

PROJ = r"E:/Games/2D Game/My project"


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(PROJ, "Tools", rel))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


BAKE = load("bake", "art-h5f-cover-bake.py")
F = BAKE.F

ns, joints = F.read_rig()
cans = {k: F.load_canvas(v) for k, v in F.CANVAS.items()}
spec = dict(BAKE.JOINTS["knee"]); spec["name"] = "knee"
fr = BAKE.Frame(spec, cans, ns, 2000.0)

k, missing = BAKE.game_numbers(cans["thigh"], ns["thighSize"].y)
_, mc = BAKE.game_numbers(cans["shin"], ns["shinSize"].y)
par = fr.place("thigh", spec["qp"], 0.0, k, 0.0)
rest_thigh = fr.place("thigh", spec["qp"], 0.0, 1.0, 0.0)
rest_shin = fr.place("shin", spec["qc"], 0.0, 1.0, 0.0)
c_top, c_bot = BAKE.COV.ink_extent(cans["shin"])
p_top, p_bot = BAKE.COV.ink_extent(cans["thigh"])
span = c_top - c_bot
n_k = (span + missing) / span
n_shift = c_bot - c_bot * n_k
print("knee k=%.3f missing=%.5f n_k=%.4f n_shift=%.4f" % (k, missing, n_k, n_shift))

for theta in (0.0, 65.0):
    ch = fr.place("shin", spec["qc"], theta, n_k, n_shift)
    lab = BAKE.over(par, ch)
    rest = BAKE.over(rest_thigh, rest_shin)
    print("--- theta %.0f: x | thigh_bot shin_top | rest_limb_bot | cover_bot (shape) ---" % theta)
    for xm in np.arange(-0.085, 0.0851, 0.005):
        c = int((xm - fr.x0) * fr.scale - 0.5)
        if c < 0 or c >= fr.nx:
            continue
        col_a = lab[:, c, 3] > 0.05
        col_r = rest[:, c, 3] > 0.05
        col_p = par[:, c, 3] > 0.05
        col_c = ch[:, c, 3] > 0.05
        def bots(m, top_to_bot=True):
            r = np.nonzero(m)[0]
            if not len(r):
                return None
            return fr.ym(r.max())
        def tops(m):
            r = np.nonzero(m)[0]
            if not len(r):
                return None
            return fr.ym(r.min())
        tb = bots(col_p)          # thigh's lowest painted y
        ct = tops(col_c)          # shin's highest painted y
        rb = bots(col_r)          # the rest limb's lowest painted y
        # the gap between the thigh and the shin (if the thigh above and the shin below)
        gap = None
        if tb is not None and ct is not None and tb > ct:
            gap = (ct, tb)
        print("%+6.1f | %s %s | %s | %s"
              % (xm * 1000,
                 ("%+7.4f" % tb) if tb is not None else "    -  ",
                 ("%+7.4f" % ct) if ct is not None else "    -  ",
                 ("%+7.4f" % rb) if rb is not None else "    -  ",
                 ("GAP %.1f..%.1f mm" % (gap[0] * 1000, gap[1] * 1000)) if gap else ""))
