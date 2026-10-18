#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f R4 - TWO RENDERS, ONE PICTURE?  Pixel compare of the engine check's own frames.

The check (Assets/Editor/ArtCuffCheck.cs) renders deterministically: one camera, one scene,
the pose written straight onto the transforms - no physics, no wall clock.  So its frames
from two runs are comparable pixel by pixel, and that is the instrument for "the default
picture stays byte-identical": the R3 run's v=0 frames against the R4 run's v=0 frames.

Usage: python Tools/ah5fr4-frame-eq.py <dir-a> <tag-a> <dir-b> <tag-b> <name...>
   or: python Tools/ah5fr4-frame-eq.py baseline r4          (the standard v=0 set)
"""
import os
import sys

import numpy as np
from PIL import Image

RUN = "Trials/Runs/cuff-r4"


def cmp_pair(pa, pb):
    if not (os.path.exists(pa) and os.path.exists(pb)):
        print("%-58s MISSING %s %s" % (os.path.basename(pa), os.path.exists(pa), os.path.exists(pb)))
        return
    a = np.asarray(Image.open(pa).convert("RGBA")).astype(np.int16)
    b = np.asarray(Image.open(pb).convert("RGBA")).astype(np.int16)
    if a.shape != b.shape:
        print("%-58s SHAPE %s vs %s" % (os.path.basename(pa), a.shape, b.shape))
        return
    d = np.abs(a - b).sum(axis=2)
    n = int((d > 0).sum())
    print("%-58s equal %s  differing %d px  max |d| %d" %
          (os.path.basename(pa), "YES" if n == 0 else "no", n, int(d.max())))


def main():
    if len(sys.argv) == 3:
        tags = [("baseline", sys.argv[1]), ("r4", sys.argv[2])]
        names = ["body-v0.00-c0.png", "look-v0.00-c0-knee-Right-deg0.png",
                 "look-v0.00-c0-knee-Left-deg0.png", "look-v0.00-c0-knee-Right-deg66.png",
                 "look-v0.00-c0-elbow-Right-deg-15.png", "look-v0.00-c0-elbow-Left-deg-54.png",
                 "look-v0.00-c0-hip-Right-deg18.png", "look-v0.00-c0-shoulder-Right-deg-14.png"]
        for n in names:
            cmp_pair(os.path.join(RUN, "%s-%s" % (tags[0][0], n)),
                     os.path.join(RUN, "%s-%s" % (tags[1][0], n)))
        return 0
    a_dir, a_tag, b_dir, b_tag = sys.argv[1:5]
    for n in sys.argv[5:]:
        cmp_pair(os.path.join(a_dir, "%s-%s" % (a_tag, n)), os.path.join(b_dir, "%s-%s" % (b_tag, n)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
