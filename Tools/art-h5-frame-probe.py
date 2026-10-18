#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5 R2: pin the Joint frame conventions (up/z0/z1 signs) so HumanSkin.cs can
replicate the sim's formula literally."""
import importlib.util
import os
import sys

import numpy as np

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
spec = importlib.util.spec_from_file_location(
    "art_h5_skin_proto", os.path.join(PROJECT, "Tools", "art-h5-skin-proto.py"))
p = importlib.util.module_from_spec(spec)
sys.modules["art_h5_skin_proto"] = p
spec.loader.exec_module(p)

j = p.Joint(p.frames_tool(), "knee", 65.0, 3000.0, 0.30)
g = j.measure()
print("c=%d scale=%.0f r_eye=%.4f" % (j.c, j.scale, j.r_eye))
print("up=(%.6f, %.6f)  z0=%.3f z1=%.3f px" % (g["up"][0], g["up"][1], g["z0"], g["z1"]))
print("geom keys:", sorted(g.keys()))
du, dv = j.du_dv()
ci = int(j.c)
for name, row in (("above-100px", ci - 100), ("center", ci), ("below+100px", ci + 100),
                  ("below+300px", ci + 300)):
    x = ci
    print("%-12s px(x=%d,y=%d) du=%+8.2f dv=%+8.2f" % (name, x, row, du[row, x], dv[row, x]))
print("du at +250px above: %.2f ; -300px below: %.2f"
      % (du[ci - 250, ci], du[ci + 300, ci]))
P = np.asarray(j.composed(0.0, "parent"))[:, :, 3]
C = np.asarray(j.composed(0.0, "child"))[:, :, 3]
for name, row in (("above-100px", ci - 100), ("center", ci), ("below+100px", ci + 100),
                  ("below+200px", ci + 200), ("above-250px", ci - 250)):
    print("%-12s alpha parent=%3d child=%3d" % (name, P[row, ci], C[row, ci]))
