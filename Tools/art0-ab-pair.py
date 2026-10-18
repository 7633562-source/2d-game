#!/usr/bin/env python3
"""A/B two capture labels of the SAME scene and lens, with the sides withheld.

Usage:
    python Tools/art0-ab-pair.py <labelA> <labelB> <still> [zoom] [x0,y0,x1,y1] [seed ...]

Both labels must be the same camera (the gallery prints `ortho=` per still, the player
the buildGuid). The tool stacks the two panels of the SAME rectangle vertically, shuffles
which label goes on top from a seed, writes the sheet and writes the mapping to a
separate `-map.txt` - open the map AFTER reading the picture.

Default window is the whole frame; pass the box the lens needs, because a fixed box is what
makes the two panels the same rectangle (the same camera is not the same crop).

    python Tools/art0-ab-pair.py gallery10 art0scale21 tree 2 380,0,1920,1050 5 17
"""
import os
import random
import sys

import numpy as np
from PIL import Image

PROJ = "E:/Games/2D Game/My project"
RUNS = os.path.join(PROJ, "Trials", "Runs")


def open_still(label, still):
    path = os.path.join(RUNS, "%s_%s.png" % (label, still))
    if not os.path.exists(path):
        raise SystemExit("no such still: " + path)
    img = Image.open(path).convert("RGB")
    if img.size != (1920, 1080):
        raise SystemExit("%s is %dx%d, expected 1920x1080" % (path, img.size[0], img.size[1]))
    return img


def build_guid(label):
    path = os.path.join(RUNS, "%s-gallery.json" % label)
    if not os.path.exists(path):
        return "?"
    txt = open(path, encoding="utf-8").read()
    i = txt.find('"buildGuid"')
    return txt[i + 13:].split('"')[1] if i >= 0 else "?"


def main():
    labels = [sys.argv[1], sys.argv[2]]
    still = sys.argv[3]
    zoom = int(sys.argv[4]) if len(sys.argv) > 4 else 1
    box = tuple(int(v) for v in sys.argv[5].split(",")) if len(sys.argv) > 5 else (0, 0, 1920, 1080)
    seeds = [int(v) for v in sys.argv[6:]] or [11]

    panels = {}
    for lab in labels:
        img = open_still(lab, still).crop(box)
        if zoom != 1:
            img = img.resize((img.width * zoom, img.height * zoom), Image.NEAREST)
        panels[lab] = img
        print("%-14s %s_%s.png build=%s crop=%s zoom=%dx" % (lab, lab, still, build_guid(lab),
                                                             box, zoom))

    for seed in seeds:
        order = list(labels)
        random.Random(seed).shuffle(order)
        w = max(p.width for p in panels.values())
        h = sum(p.height for p in panels.values()) + 12
        sheet = Image.new("RGB", (w, h), (110, 110, 110))
        y = 0
        for lab in order:
            sheet.paste(panels[lab], (0, y))
            y += panels[lab].height + 12
        name = os.path.join(RUNS, "art-0-ab", "%s_%s_ab_seed%d.png" % (still, "".join(
            l[0] for l in labels), seed))
        os.makedirs(os.path.dirname(name), exist_ok=True)
        sheet.save(name)
        with open(name.replace(".png", "-map.txt"), "w", encoding="utf-8") as fh:
            fh.write("panel 1 (TOP)    = %s\npanel 2 (BOTTOM) = %s\n" % (order[0], order[1]))
            fh.write("\nread the picture first, then this file\n")
        print("wrote %s (%dx%d)" % (name, sheet.width, sheet.height))


if __name__ == "__main__":
    main()
