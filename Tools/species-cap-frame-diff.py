#!/usr/bin/env python3
"""Per-band pixel difference between two captured frames, to isolate WHERE a picture moved.

    python Tools/species-cap-frame-diff.py <oldA.png>:<newB.png> [<oldC.png>:<newD.png> ...]

Why not a single PSNR/percentage over the whole frame: the trees SWAY. Two runs of the SAME
build and the SAME art differ in a tenth of the frame's pixels (the crown's silhouette sits at
a different angle), so a full-frame number cannot tell "the grass cap was repainted" from "the
tree happened to be captured at a different sway angle". The grass is a horizontal band at the
bottom of the frame, so the difference PROFILE by row is the evidence: a cap repaint puts its
pixels in the ground rows (plus the few trunk columns), while sway noise is spread over the
crown. The control pair (same build, same art, two runs) is printed by the same tool for that
reason - it is the noise floor of this measurement.

Frames are decoded to raw RGB24 through ffmpeg and compared row by row in Python, so nothing
here needs an image library.
"""
import hashlib
import os
import subprocess
import sys

FFMPEG = "ffmpeg"
TMP = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Temp", "species-cap-frame-diff")
BANDS = 24


def raw(png):
    os.makedirs(TMP, exist_ok=True)
    # The cache key must be the FULL path: two frames of the same scene share a basename
    # (`treeoak_photo.png` in the snapshot and in Trials/Runs), and keying by basename made
    # the second request reuse the first's raw file - which compares a frame with itself and
    # reports a perfect match. Found the hard way on the first A/B this tool was written for.
    tag = hashlib.md5(os.path.abspath(png).encode("utf-8")).hexdigest()[:10]
    out = os.path.join(TMP, os.path.basename(png).replace(".png", "") + "-" + tag + ".rgb")
    if not os.path.exists(out) or os.path.getmtime(out) < os.path.getmtime(png):
        done = subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-i", png,
                               "-f", "rawvideo", "-pix_fmt", "rgb24", out],
                              capture_output=True, text=True)
        if done.returncode != 0:
            print("FAIL ffmpeg {0}: {1}".format(png, done.stderr.strip()[:200]))
            raise SystemExit(2)
    return open(out, "rb").read()


def compare(a_path, b_path, name):
    a, b = raw(a_path), raw(b_path)
    if len(a) != len(b) or len(a) % 3:
        print("FAIL size mismatch {0} {1}".format(len(a), len(b)))
        return 1
    px = len(a) // 3
    height, width = 1080, px // 1080
    row_bytes = width * 3
    band_rows = [0] * BANDS
    differing = 0
    for row in range(height):
        off = row * row_bytes
        if a[off:off + row_bytes] == b[off:off + row_bytes]:
            continue
        band = min(BANDS - 1, row * BANDS // height)
        for col in range(width):
            p = off + col * 3
            if a[p:p + 3] != b[p:p + 3]:
                differing += 1
                band_rows[band] += 1
    per_band = height // BANDS
    print("== {0}".format(name))
    print("   whole frame: {0} of {1} px differ ({2:.3f} %)".format(
        differing, px, 100.0 * differing / max(px, 1)))
    worst = sorted(range(BANDS), key=lambda k: -band_rows[k])[:4]
    for band in worst:
        y0, y1 = band * per_band, band * (per_band + 1)
        print("   band {0:2d} rows {1:4d}-{2:4d}: {3:8d} px differ ({4:6.2f} % of the band)".format(
            band, y0, y1, band_rows[band], 100.0 * band_rows[band] / max(width * per_band, 1)))
    return 0


def main():
    pairs = [item for item in sys.argv[1:] if ":" in item]
    if not pairs:
        print(__doc__)
        return 2
    for item in pairs:
        a, b = item.split(":", 1)
        compare(a, b, os.path.basename(b))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
