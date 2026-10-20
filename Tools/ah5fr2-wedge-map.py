# ASCII map: the wedge (hole at v=0.4) against the cover's shape (the rest limb) - knee.
import importlib.util
import os
import sys

import numpy as np

PROJ = r"E:/Games/2D Game/My project"


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(PROJ, "Tools", rel))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


BAKE = load("bake", "art-h5f-cover-bake.py")
F = BAKE.F

joint = sys.argv[1] if len(sys.argv) > 1 else "knee"
thetas = [float(x) for x in (sys.argv[2].split(",") if len(sys.argv) > 2 else ["0", "65"])]
ns, joints = F.read_rig()
cans = {k: F.load_canvas(v) for k, v in F.CANVAS.items()}
spec = BAKE.resolve_spec(joint, "near")
fr = BAKE.Frame(spec, cans, ns, 500.0)      # 500 px/m -> 2 mm cells

v = 0.4
par = BAKE.pose(fr, spec, 0.0)["par_now"]
rest = BAKE.over(fr.place(spec["parent"], spec["qp"], 0.0, 1.0, 0.0),
                 fr.place(spec["child"], spec["qc"], 0.0, 1.0, 0.0))
shape = rest[..., 3] > 0.05

for theta in thetas:
    ch = BAKE.pose(fr, spec, theta)["ch_now"]
    lab = BAKE.over(par, ch)
    paint = lab[..., 3] > 0.05
    print("=== %s v=%.1f theta=%.1f:  # both sprite paint | o HOLE (background) inside "
          "the rest limb | - cover-only (outside the rest limb) ===" % (joint, v, theta))
    step = 4   # 4 px = 8 mm at 500 px/m for print; sample every 4th px
    for r in range(0, fr.ny, step):
        line = ""
        for c in range(0, fr.nx, step):
            p = paint[r:r + step, c:c + step].mean() > 0.5
            s = shape[r:r + step, c:c + step].mean() > 0.5
            line += "#" if p else ("o" if s else "-")
        print("%+7.1f %s" % (fr.ym(r) * 1000, line))
    xs = "".join("%d" % (abs(fr.xm(c)) // 10 % 10) for c in range(0, fr.nx, step))
    print("        " + xs + "  (x: tens of mm left/right of the hinge)")
