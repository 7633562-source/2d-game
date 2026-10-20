"""ART-0 28th pass probe - the `leaf` material: coverage evenness and repeat visibility.

The leaf canvas is dressed 0.5 m per tile on every crown (`ArtParts` -> `ArtLibrary.Tiled`,
LeafTileMeters 0.5), so a 512 px tile is 1024 px/m. Two things make such a tile read as
"wallpaper" instead of foliage, and both are measurable on the shipped bytes with no Unity:

  * LARGE-SCALE UNEVENNESS - clumps and holes at a scale the eye recognises when the tile
    comes round again. Measured as the spread of the low-frequency field (the luminance
    blurred over a 1/8-tile window) as a fraction of its mean, plus the darkest window.
  * A RECOGNISABLE ANCHOR - the tile is too much like its own half-period shift. Measured
    as the toroidal autocorrelation at a 256 px shift in x and in y (1.0 = the tile repeats
    itself exactly at half period). The element scale is the same autocorrelation's
    correlation length: how big a leaf is, in px and in cm.

Run: python Tools/art0-leaf-cover-probe.py [path]
"""
import sys

import numpy as np
from PIL import Image

PATH = "Assets/Resources/Art/leaf.png"
BLUR = 64          # px: 1/8 of the tile = 6.3 cm of crown
PPM = 1024.0       # px per metre at the shipping 0.5 m dress period


def blur(a, k):
    """Toroidal box blur (the tile repeats, so its blur must wrap too)."""
    out = a.astype(np.float64)
    for axis in (0, 1):
        out = np.roll(out, k // 2, axis=axis)
        acc = np.zeros_like(out)
        for i in range(k):
            acc += np.roll(out, -i, axis=axis)
        out = acc / k
    return out


def autocorr(a):
    """Toroidal autocorrelation of a zero-mean field, normalised to 1.0 at zero shift."""
    f = a - a.mean()
    fft = np.fft.fft2(f)
    ac = np.fft.ifft2(fft * np.conj(fft)).real
    ac /= ac[0, 0]
    return ac


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else PATH
    im = Image.open(path).convert("RGB")
    a = np.asarray(im).astype(np.float64)
    lum = 0.2126 * a[:, :, 0] + 0.7152 * a[:, :, 1] + 0.0722 * a[:, :, 2]
    w, h = im.size
    print("%s  %dx%d  mean RGB (%.1f, %.1f, %.1f)  mean lum %.1f"
          % (path, w, h, a[:, :, 0].mean(), a[:, :, 1].mean(), a[:, :, 2].mean(), lum.mean()))

    for k in (64, 128, 170):
        lo = blur(lum, k)
        rel = lo.std() / lo.mean()
        print("  low-frequency (blur %3d px = %4.1f cm): mean %6.1f, std %5.2f = %4.1f%% of the mean,"
              " min %5.1f at %s, max %6.1f, band %5.1f levels"
              % (k, 100.0 * k / PPM, lo.mean(), lo.std(), 100.0 * rel, lo.min(),
                 np.unravel_index(lo.argmin(), lo.shape), lo.max(), lo.max() - lo.min()))

    # leaf coverage: how much of the tile is painted lighter than the shadow field
    thr = np.percentile(lum, 55)
    cover = (lum > thr).mean()
    print("  light-covered fraction (lum > p55): %.3f" % cover)

    ac = autocorr(lum)
    # correlation length: first shift (x) where the autocorrelation falls below 0.5
    row = ac[0, 1:w // 2]
    below = np.nonzero(row < 0.5)[0]
    clen = int(below[0] + 1) if len(below) else w // 2
    ax = ac[0, 256]
    ay = ac[256, 0]
    print("  autocorrelation: length %.0f px = %.1f cm (first shift below 0.5); at the HALF PERIOD "
          "(256 px) x %.3f y %.3f" % (clen, 100.0 * clen / PPM, ax, ay))
    # self-similarity against the full wrap
    print("  autocorrelation at shifts: 64 %.3f  128 %.3f  256 %.3f  384 %.3f (x)"
          % (ac[0, 64], ac[0, 128], ac[0, 256], ac[0, 384]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
