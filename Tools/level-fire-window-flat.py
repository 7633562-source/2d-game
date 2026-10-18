"""LEVEL 20.09: the log pile window, old build vs new - flat or textured?

Companion of Tools/level-fire-band-map.py: that one located the difference
(x 839..1077, y 731..842 - the fire's log pile, 0.70 x 0.33 m at the row's lens),
this one says WHAT it is. A flat collider-sized box has one modal colour over
most of its area; painted bark does not. Both stills, same window, offline.

Usage:
    python Tools/level-fire-window-flat.py <old.png> <new.png> <x0> <y0> <x1> <y1>
"""
import sys

import numpy as np
from PIL import Image


def stats(img, win):
    a = np.asarray(img.convert("RGB")).astype(np.int32)
    cut = a[win[1]:win[3], win[0]:win[2]].reshape(-1, 3)
    colours, counts = np.unique(cut, axis=0, return_counts=True)
    order = np.argsort(-counts)
    modal = counts[order[0]] / float(cut.shape[0])
    return dict(pixels=int(cut.shape[0]),
                distinct=int(colours.shape[0]),
                modal_fraction=modal,
                modal_colour=colours[order[0]].tolist(),
                std=cut.std(axis=0).round(1).tolist(),
                top=[(colours[i].tolist(), int(counts[i])) for i in order[:4]])


def main():
    old_img = Image.open(sys.argv[1])
    new_img = Image.open(sys.argv[2])
    win = tuple(int(v) for v in sys.argv[3:7])
    print("window x %d..%d  y %d..%d  (%d x %d px)" % (win[0], win[2], win[1], win[3],
                                                       win[2] - win[0], win[3] - win[1]))
    for tag, img in (("OLD", old_img), ("NEW", new_img)):
        s = stats(img, win)
        print("%s  pixels %d  distinct colours %d  modal colour fraction %.4f  modal %s  std %s"
              % (tag, s["pixels"], s["distinct"], s["modal_fraction"], s["modal_colour"], s["std"]))
        print("     top colours: %s" % s["top"])
    o, n = stats(old_img, win), stats(new_img, win)
    print("FLATNESS  old %.4f -> new %.4f  (a flat box dominates one colour; painted art spreads)"
          % (o["modal_fraction"], n["modal_fraction"]))
    print("COLOURS   old %d -> new %d" % (o["distinct"], n["distinct"]))

    # The flat box is the point: its modal colours must be GONE, not merely rare.
    old_colours = [c for c, _ in o["top"]]
    new_arr = np.asarray(new_img.convert("RGB")).astype(np.int32)
    winpix = new_arr[win[1]:win[3], win[0]:win[2]].reshape(-1, 3)
    old_arr = np.asarray(old_img.convert("RGB")).astype(np.int32)
    oldpix = old_arr[win[1]:win[3], win[0]:win[2]].reshape(-1, 3)
    for colour in old_colours:
        c = np.array(colour, dtype=np.int32)
        print("colour %-16s old %6d px -> new %6d px"
              % (str(colour), int((oldpix == c).all(axis=1).sum()),
                 int((winpix == c).all(axis=1).sum())))


main()
