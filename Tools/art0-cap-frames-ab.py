"""ART-0 cycle: is the whole frame difference of the two builds the grass cap and nothing else?

One PNG changed (`Resources/Level/ground_cap.png`), and the cap is drawn on EVERY ground piece in
the gallery, so every still legitimately shows a difference band - but only where the ground
surface is. This probe proves that shape of difference instead of asserting it:

* for each still it prints every run of differing rows and compares the run's height with the cap's
  height at that still's own ortho (`0.14 m * 1080 / (2 * ortho)`, all from the two capture logs),
* it writes blind A/B panels (crop windows identical in both builds, old and new in a shuffled
  order, mapping in plain text) so the change can be read rather than counted.

Usage:  python Tools/art0-cap-frames-ab.py <labelOld> <labelNew>
"""
import json
import os
import random
import re
import sys

import numpy as np
from PIL import Image, ImageDraw

PROJ = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJ, "Trials", "Runs")
TMP = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Temp", "grok-burn")
STILLS = ["photo", "human", "tree", "apple", "materials", "level", "fire", "placeholder"]
CAP_M = 0.14


def ortho_of(label, name):
    path = os.path.join(TMP, label + "-capture.log")
    if not os.path.exists(path):
        return None
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        for line in fh:
            if "still %s_%s.png" % (label, name) in line:
                m = re.search(r"ortho=([0-9]+,[0-9]+)", line)
                if m:
                    return float(m.group(1).replace(",", "."))
    return None


def runs(rows):
    out, start = [], None
    for y in range(len(rows) + 1):
        on = y < len(rows) and rows[y]
        if on and start is None:
            start = y
        elif not on and start is not None:
            out.append((start, y - 1))
            start = None
    return out


def main(old, new):
    out_dir = os.path.join(RUNS, "art-0-cap-frames")
    os.makedirs(out_dir, exist_ok=True)
    print("%-12s %-8s %-8s %s" % ("still", "cap px", "diff px", "differing row runs (start-end height)"))
    crops = []
    for name in STILLS:
        pa = os.path.join(RUNS, "%s_%s.png" % (old, name))
        pb = os.path.join(RUNS, "%s_%s.png" % (new, name))
        if not (os.path.exists(pa) and os.path.exists(pb)):
            continue
        a = np.asarray(Image.open(pa).convert("RGB")).astype(np.int16)
        b = np.asarray(Image.open(pb).convert("RGB")).astype(np.int16)
        d = np.abs(a - b).max(axis=2)
        rows = (d > 8).any(axis=1)
        rs = runs(rows)
        ortho = ortho_of(new, name)
        cap_px = None if ortho is None else CAP_M * 1080.0 / (2.0 * ortho)
        print("%-12s %-8s %-8d %s" % (
            name,
            "-" if cap_px is None else "%.0f" % cap_px,
            int((d > 8).sum()),
            " ".join("y%d-%d h%d%s" % (s, e, e - s + 1,
                                       "" if cap_px is None else
                                       ("=cap" if abs((e - s + 1) - cap_px) <= max(3, 0.35 * cap_px)
                                        else "!=cap"))
                     for s, e in rs)))
        if name in ("level", "human", "materials"):
            for s, e in rs:
                pad = 14
                y0, y1 = max(0, s - pad), min(a.shape[0], e + 1 + pad)
                crops.append((name, s, e, pa, pb, y0, y1))

    order = list(range(len(crops)))
    random.Random(1919).shuffle(order)
    lines = []
    for i, idx in enumerate(order):
        name, s, e, pa, pb, y0, y1 = crops[idx]
        a = Image.open(pa).convert("RGB").crop((0, y0, 1920, y1))
        b = Image.open(pb).convert("RGB").crop((0, y0, 1920, y1))
        sheet = Image.new("RGB", (a.width, a.height * 2 + 4), (255, 0, 255))
        sheet.paste(a, (0, 0))
        sheet.paste(b, (0, a.height + 4))
        path = os.path.join(out_dir, "ab_%02d.png" % (i + 1))
        sheet.save(path)
        lines.append("panel %02d = %s band y%d-%d, window y%d-%d, TOP=%s BOTTOM=%s"
                     % (i + 1, name, s, e, y0, y1, old, new))
    with open(os.path.join(out_dir, "map.txt"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")
    print("panels: %s" % out_dir)
    for line in lines:
        print("  " + line)
    return 0


if __name__ == "__main__":
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)
    sys.exit(main(sys.argv[1], sys.argv[2]))
