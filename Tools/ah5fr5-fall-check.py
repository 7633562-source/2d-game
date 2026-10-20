#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f R5 probe - is the v=0.4 take standing at the four lifts?

The R3 report's OPEN 1: the physics half of the walk falls at every v>0 on this line
(v0.4 at 6.295 s). If the r4_on shots are already down at t=15.43+, the R5 A/B cannot be
cut control-vs-v0.4 at those times from those takes and the report has to say so.

Uses the stills tool's own sky mask / horizon / figure_box, so the numbers match the ones
the stills themselves were cut with.

Usage: python Tools/ah5fr5-fall-check.py
"""
import importlib.util
import os

import numpy as np
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(PROJECT, "Trials", "Runs")


def load_mod(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(PROJECT, "Tools", rel))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


ST = load_mod("stills", "arth5f-stills.py")


def main():
    frames = (386, 709, 750, 830)
    for take in ("r4_ctl0", "r4_on", "r4_off"):
        for f in frames:
            p = os.path.join(RUNS, "shot_%s_%d.png" % (take, f))
            if not os.path.exists(p):
                print("  %-8s f%-4d -- no shot" % (take, f))
                continue
            a = np.asarray(Image.open(p).convert("RGB"))
            hy = ST.horizon(a)
            box = ST.figure_box(a, hy)
            if box is None:
                print("  %-8s f%-4d horizon %4d  no figure" % (take, f, hy))
                continue
            x0, y0, x1, y1 = box
            w, h = x1 - x0 + 1, y1 - y0 + 1
            print("  %-8s f%-4d horizon %4d  figure x %4d..%4d y %4d..%4d  %dx%d aspect %.2f"
                  % (take, f, hy, x0, x1, y0, y1, w, h, w / float(h)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
