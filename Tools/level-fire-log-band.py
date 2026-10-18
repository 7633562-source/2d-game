"""LEVEL 20.09: is the FIRE row's log band flat or painted?

The gallery's own catalogue line says "logs dressed 0/3 by Campfire.DressLog" on
the freshly built player, while the artist's nineteenth pass added
Art/Campfire/log_a|b|c.png and wired DressLog into both the yard (BuildLogs) and
the gallery (GalleryScene.BuildFireRow). A bookkeeping counter can be wrong; the
frame cannot. This reads the shipped FIRE still offline (PIL, no Unity):

  * the fire's own ink box (pixels that differ from the night background);
  * the LOG band - the lower part of that box, where the three logs are;
  * how many distinct colours that band holds and its per-channel spread - a
    flat tinted box is one colour, painted bark is many;
  * the same numbers for the previous still, so the two builds are compared.

Usage:
    python Tools/level-fire-log-band.py <old.png> <new.png>
"""
import sys

import numpy as np
from PIL import Image

NIGHT = np.array([26, 30, 44], dtype=np.int32)


def band_stats(path, band_fraction=0.32):
    img = Image.open(path).convert("RGB")
    a = np.asarray(img).astype(np.int32)
    diff = np.abs(a - NIGHT).sum(axis=2)
    ink = diff > 30
    ys, xs = np.nonzero(ink)
    if xs.size == 0:
        return None
    x0, x1 = int(xs.min()), int(xs.max())
    y0, y1 = int(ys.min()), int(ys.max())
    height = y1 - y0 + 1
    cut = y1 - max(2, int(round(height * band_fraction)))
    band = a[cut:y1 + 1, x0:x1 + 1].reshape(-1, 3)
    mask = ink[cut:y1 + 1, x0:x1 + 1].reshape(-1)
    lit = band[mask]
    if lit.size == 0:
        return None
    colours = np.unique(lit, axis=0)
    return dict(path=path, image=img.size, ink_box=(x0, y0, x1, y1),
                height=height, band_rows=(cut, y1), band_pixels=int(lit.shape[0]),
                distinct_colours=int(colours.shape[0]),
                std=lit.std(axis=0).round(1).tolist(),
                mean=lit.mean(axis=0).round(1).tolist(),
                top5=[c.tolist() for c in colours[np.argsort(-np.unique(
                    lit, axis=0, return_counts=True)[1])[:5]]])


def main():
    old_path, new_path = sys.argv[1], sys.argv[2]
    old = band_stats(old_path)
    new = band_stats(new_path)
    for tag, s in (("OLD", old), ("NEW", new)):
        if s is None:
            print("%s %s: no ink" % (tag, old_path if tag == "OLD" else new_path))
            continue
        print("%s %s  size=%s ink_box=%s height=%dpx" % (tag, s["path"], s["image"], s["ink_box"], s["height"]))
        print("    LOG band rows %s: %d px, distinct colours %d, std %s, mean %s"
              % (s["band_rows"], s["band_pixels"], s["distinct_colours"], s["std"], s["mean"]))
        print("    most common colours: %s" % s["top5"])

    if old and new:
        a = np.asarray(Image.open(old_path).convert("RGB")).astype(np.int32)
        b = np.asarray(Image.open(new_path).convert("RGB")).astype(np.int32)
        if a.shape == b.shape:
            changed = int((np.abs(a - b).sum(axis=2) > 12).sum())
            print("CHANGED pixels old->new: %d of %d (%.4f%% of frame)"
                  % (changed, a.shape[0] * a.shape[1], 100.0 * changed / (a.shape[0] * a.shape[1])))
        else:
            print("frame sizes differ: %s vs %s" % (a.shape, b.shape))

    if new:
        # A flat tinted box has one colour over its whole area; painted bark does not.
        verdict = "FLAT (undressed box)" if new["distinct_colours"] <= 3 else "TEXTURED (painted)"
        print("FIRE-LOG-BAND verdict on %s: distinct_colours=%d -> %s"
              % (new_path, new["distinct_colours"], verdict))


main()
