"""ART-0 28th pass - sweep the leaf placement rule against the tile's own metrics.

Loads Tools/gen-level-textures.py as a module (no file writes) and paints the leaf tile for
a set of cell counts and in-cell jitters, then reports, for each: the tile's mean colour
(the palette anchor the crown is tuned to), the large-scale structure at two windows (a void
shows up as a low minimum and a wide band), the autocorrelation at the CELL period (a rigid
grid shows up as a high value there) and at the half period (a recognisable anchor at the
repeat), and the coverage.

Run: python Tools/art0-leaf-placement-sweep.py
"""
import importlib.util
import math
import pathlib
import sys

import numpy as np

ROOT = pathlib.Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location("genlevel", ROOT / "Tools" / "gen-level-textures.py")
gl = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gl)

BLUR = 128
PPM = 1024.0
BASE_MEAN = (66.8, 135.8, 4.5)      # the shipping tile's own mean, the crown's colour anchor


def blur(a, k):
    out = a.astype(np.float64)
    for axis in (0, 1):
        out = np.roll(out, k // 2, axis=axis)
        acc = np.zeros_like(out)
        for i in range(k):
            acc += np.roll(out, -i, axis=axis)
        out = acc / k
    return out


def autocorr(a):
    f = a - a.mean()
    fft = np.fft.fft2(f)
    ac = np.fft.ifft2(fft * np.conj(fft)).real
    return ac / ac[0, 0]


def metrics(img, cells):
    a = np.asarray(img.convert("RGB")).astype(np.float64)
    lum = 0.2126 * a[:, :, 0] + 0.7152 * a[:, :, 1] + 0.0722 * a[:, :, 2]
    lo = blur(lum, BLUR)
    ac = autocorr(lum)
    cell_px = int(round(512.0 / cells))
    return dict(mean=(a[:, :, 0].mean(), a[:, :, 1].mean(), a[:, :, 2].mean()),
                lf_rel=100.0 * lo.std() / lo.mean(), lf_band=lo.max() - lo.min(), lf_min=lo.min(),
                ac_cell=ac[0, cell_px], ac_half=ac[0, 256], ac64=ac[0, 64],
                cover=float((lum > np.percentile(lum, 55)).mean()))


def main():
    out_dir = ROOT / "Trials" / "Runs" / "leaf-sweep"
    out_dir.mkdir(parents=True, exist_ok=True)
    print("%-26s %-22s %7s %7s %7s %7s %7s %7s" % ("leaves/cells/jitter", "mean RGB", "lf%", "band",
                                                    "lfmin", "ac@cell", "ac64", "ac@half"))
    cases = []
    for count, fill, jit in ((64, 20, 0.0), (64, 20, 0.15), (49, 16, 0.0), (49, 16, 0.15),
                             (44, 14, 0.10), (36, 12, 0.10)):
        img = gl.paint_leaf(count=count, fill=fill, jitter=jit)
        cells_n = int(math.ceil(math.sqrt(count)))
        m = metrics(img, cells_n)
        cases.append((cells_n, count, fill, jit, m))
        tag = "n%d-c%d-j%02d" % (count, cells_n, int(round(jit * 100)))
        img.convert("RGB").save(out_dir / (tag + ".png"))
        print("%-26s %-22s %7.1f %7.1f %7.1f %7.3f %7.3f %7.3f" % (
            "%d leaves/%dx%d/j%.2f" % (count, cells_n, cells_n, jit),
            "(%.1f,%.1f,%.1f)" % m["mean"], m["lf_rel"], m["lf_band"], m["lf_min"],
            m["ac_cell"], m["ac64"], m["ac_half"]))
    print("\nbaseline (shipped before this pass, Poisson, 44 leaves): lf%% 19.2, band 99.5, "
          "lfmin 50.5, ac64 0.325, ac@half 0.079")
    print("target: lfmin up (no void), band and lf%% down, ac64 near the baseline, mean near %s"
          % (BASE_MEAN,))
    return 0


if __name__ == "__main__":
    sys.exit(main())
