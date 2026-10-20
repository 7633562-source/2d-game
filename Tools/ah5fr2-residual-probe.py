# Why is the (-45,+10) mm neighbourhood left uncovered? Print the layers there.
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
fr, rgba, shape = BAKE.bake(spec, cans, ns, 3000.0, None, verbose=False)
par = fr.place("thigh", spec["qp"], 0.0, 1.0, 0.0)
ch = fr.place("shin", spec["qc"], 0.0, 1.0, 0.0)
rest = BAKE.over(par, ch)
for (xm, ym) in ((-44.0, 10.0), (-45.0, 8.0), (-42.0, 12.0), (-44.0, 0.0), (-44.0, 20.0)):
    c = int((xm / 1000.0 - fr.x0) * fr.scale - 0.5)
    r = int((fr.y0 + fr.h - ym / 1000.0) * fr.scale - 0.5)
    print("(%+5.1f,%+5.1f) mm: rest_a %.3f thigh_a %.3f shin_a %.3f | shape %s cover_a %.3f"
          % (xm, ym, rest[r, c, 3], par[r, c, 3], ch[r, c, 3], bool(shape[r, c]), rgba[r, c, 3]))
# the shape's boundary along that row
r = int((fr.y0 + fr.h - 0.010) * fr.scale - 0.5)
row = shape[r]
cc = np.nonzero(row)[0]
print("row y=+10mm: shape cols %d..%d of %d ; gaps:" % (cc.min(), cc.max(), fr.nx),
      np.diff(cc)[np.diff(cc) > 1][:6])

# the lab's residual, pixel by pixel
k, missing = BAKE.game_numbers(cans["thigh"], ns["thighSize"].y)
_, mc = BAKE.game_numbers(cans["shin"], ns["shinSize"].y)
co = fr.place("thigh", spec["qp"], 0.0, k, 0.0)
n_top, n_bot = BAKE.COV.ink_extent(cans["shin"])
span = n_top - n_bot
n_k = (span + mc) / span
n_shift = n_bot - n_bot * n_k
cs = fr.place("shin", spec["qc"], 0.0, n_k, n_shift)
game = BAKE.over(BAKE.over(rgba.copy(), co), cs)
X, Y = np.meshgrid(fr.xm(np.arange(fr.nx)), fr.ym(np.arange(fr.ny)))
r_eye = min(BAKE.F.ink_half(cans["thigh"], spec["qp"]), BAKE.F.ink_half(cans["shin"], spec["qc"]))
disc = (X * X + Y * Y) <= r_eye * r_eye
res = disc & (game[..., 3] <= 0.06)
rr, cc2 = np.nonzero(res)
print("residual %d px; samples:" % res.sum())
for i in range(0, min(len(rr), 8)):
    r0, c0 = rr[i], cc2[i]
    print("  (%+6.1f,%+6.1f) mm  game %.3f  cover %.3f  par04 %.3f  shin_grown %.3f  shape %s"
          % (fr.xm(c0) * 1000, fr.ym(r0) * 1000, game[r0, c0, 3], rgba[r0, c0, 3],
             co[r0, c0, 3], cs[r0, c0, 3], bool(shape[r0, c0])))
print("  x mm %+.1f..%+.1f  y mm %+.1f..%+.1f" % (fr.xm(cc2.min()) * 1000, fr.xm(cc2.max()) * 1000,
                                                  fr.ym(rr.max()) * 1000, fr.ym(rr.min()) * 1000))
