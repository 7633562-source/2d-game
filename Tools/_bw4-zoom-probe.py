#!/usr/bin/env python
"""B-WING-4 close: probe the take's shots — size + where the bird is (temp helper).

Usage: python Tools/_bw4-zoom-probe.py <label> <n> [<n> ...]
"""
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")


def main():
    label = sys.argv[1]
    for arg in sys.argv[2:]:
        n = int(arg)
        p = os.path.join(RUNS, "shot_%s_%d.png" % (label, n))
        im = Image.open(p).convert("RGB").resize((480, 270))
        px = im.load()
        dark = []
        for y in range(im.height):
            for x in range(im.width):
                r, g, b = px[x, y]
                if r + g + b < 210:  # dark blob
                    dark.append((x, y))
        if dark:
            xs = [d[0] for d in dark]
            ys = [d[1] for d in dark]
            print("n=%d dark=%d bbox480=(%d,%d)-(%d,%d) center480=(%d,%d)" % (
                n, len(dark), min(xs), min(ys), max(xs), max(ys),
                (min(xs) + max(xs)) // 2, (min(ys) + max(ys)) // 2))
        else:
            print("n=%d dark=0" % n)


if __name__ == "__main__":
    main()
