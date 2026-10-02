"""Cross-check MY placement (cover-bake Frame.place) against the coverage tool's place_scaled.

Expected (Tools/arth5f-measure.py, the shipped canvases):
  thigh rest: drawn top +0.3400, bottom -0.3043 (pivot-local, y-up)
  at v=0.4 (k=0.6) about the pivot: top +0.2900, bottom -0.0966
  shin  rest: drawn top +0.2149, bottom -0.3056
  shin  grown (nk for the knee): top = rest_top + missing(0.0369) -> +0.2518
"""
import importlib.util
import os

import numpy as np

PROJ = r"E:/Games/2D Game/My project"


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(PROJ, "Tools", rel))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


COV = load("cov", "arth5f-coverage.py")
BAKE = load("bake", "art-h5f-cover-bake.py")
F = BAKE.F

ns, joints = F.read_rig()
cans = {k: F.load_canvas(v) for k, v in F.CANVAS.items()}
thigh, shin = cans["thigh"], cans["shin"]


def ext_mine(canvas, anchor, theta, kk, ss, half=0.35, scale=2000.0):
    spec = dict(BAKE.JOINTS["knee"])
    spec["crop"] = (-half, -half, 2 * half, 2 * half)
    fr = BAKE.Frame(spec, {"x": canvas}, ns, scale)
    img = fr.place("x", anchor, theta, kk, ss)
    a = img[..., 3] > 0.05
    rows = np.nonzero(a.any(axis=1))[0]
    return fr.ym(rows.max()), fr.ym(rows.min())     # bottom, top


def ext_cov(canvas, anchor, theta, kk, ss, half=0.35, scale=2000.0):
    im = COV.place_scaled(canvas, anchor, (half * scale, half * scale), theta, scale,
                          (int(2 * half * scale), int(2 * half * scale)), kk, ss)
    a = np.array(im)[:, :, 3] > 12
    rows = np.nonzero(a.any(axis=1))[0]
    ny = a.shape[0]
    y_of = lambda r: (ny / 2.0 - (r + 0.5)) / scale
    return y_of(rows.max()), y_of(rows.min())


for name, c, anchor, kk, ss in (("thigh rest", thigh, (0.0, -0.215), 1.0, 0.0),
                                ("thigh v0.4", thigh, (0.0, -0.215), 0.6, 0.0),
                                ("shin  rest", shin, (0.0, 0.215), 1.0, 0.0),
                                ("shin  grow", shin, (0.0, 0.215), 1.0708, 0.0216)):
    b_m, t_m = ext_mine(c, anchor, 0.0, kk, ss)
    b_c, t_c = ext_cov(c, anchor, 0.0, kk, ss)
    print("%-11s mine bottom %+.4f top %+.4f | cov bottom %+.4f top %+.4f"
          % (name, b_m, t_m, b_c, t_c))
