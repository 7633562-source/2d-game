#!/usr/bin/env python3
"""ART-1 dog row: reshape the two delivered part paintings that do not read as their
part at the size this catalogue shows them, without repainting them.

The 21.09 read of the delivered PNGs (`Trials/Runs/art1dog13-report.md`, raw sheets):

  neck  - "a horizontal fur roll/pillow": the delivered body is a 245 x 147 px bar of
          CONSTANT width (measured: `Tools/art1-dog-part-profile.py neck`), no taper and
          no crest/throat line, so nothing says "neck" rather than "roll";
  shin  - "a furry bone": joints bulging (95/109 px at the top, 61 px at the hock) over a
          45 px middle, i.e. the middle is 10 % of the part's own height.

Both are fixed by a profile warp that keeps every painted pixel's brushwork and moves the
silhouette only:

  * neck  - a per-COLUMN vertical remap: the crest keeps its level (with a 10 % arch over
            the length), the underside rises toward the head end, so the body tapers
            1.00 -> 0.58 of its height and gains a throat line;
  * shin  - a per-ROW horizontal remap toward a target width curve (cap, muscle, straight
            cannon for the middle, hock, taper to the cut above the paw), smoothed so no
            row jumps.

The source is the PRISTINE byte copy `Trials/Runs/<label>-before/<part>.png` (written by
`Tools/art1-dog-part-reframe.py`), so the run is reproducible and re-runnable: a part whose
current PNG already differs from its pristine copy is left alone.

  python Tools/art1-dog-part-reshape.py [--label art1dog13] [--dry-run] [--parts neck,shin]

Verdict: DOG-RESHAPE OK|FAIL reshaped=N skipped=N
"""
from __future__ import annotations

import argparse
import hashlib
import pathlib
import shutil
import sys

import numpy as np
from PIL import Image

PROJECT = pathlib.Path(__file__).resolve().parent.parent
RUNS = PROJECT / "Trials" / "Runs"
ART = PROJECT / "Assets" / "Resources" / "Art" / "Dog"

ALPHA_ANY = 8

# neck: fraction of the body height kept at the head end, the crest arch, the underside
# rise; measured against the delivered constant-width bar (see the docstring).
NECK_KEEP = 0.58
NECK_ARCH = 0.10

# shin: target width as a fraction of the part's own body height, by position along it
# (0 = stifle end, 1 = cut above the paw). Blended with the delivered curve near both
# ends so the painted cap and cut keep their own shape; the delivered curve has a
# 45 px middle on a 454 px body (10 %) and knobs of 109 px (24 %).
SHIN_TARGET = [
    (0.06, 0.235), (0.14, 0.220), (0.26, 0.200), (0.40, 0.185), (0.55, 0.175),
    (0.68, 0.178), (0.78, 0.180), (0.88, 0.155), (0.94, 0.140),
]
SHIN_BLEND = 0.06      # no reshaping at all within this fraction of either end
SHIN_GAIN_LIMITS = (0.60, 1.80)


def sha(path: pathlib.Path) -> str:
    return hashlib.md5(path.read_bytes()).hexdigest()


def premul(arr: np.ndarray) -> np.ndarray:
    out = arr.astype(np.float64)
    a = out[:, :, 3:4] / 255.0
    out[:, :, :3] *= a
    return out


def unpremul(arr: np.ndarray) -> np.ndarray:
    out = np.clip(arr, 0.0, 255.0)
    a = out[:, :, 3:4] / 255.0
    rgb = np.where(a > 1e-4, out[:, :, :3] / np.maximum(a, 1e-4), out[:, :, :3])
    out = np.concatenate([np.clip(rgb, 0, 255), out[:, :, 3:4]], axis=2)
    return out.round().astype(np.uint8)


def reshape_neck(img: Image.Image) -> Image.Image:
    arr = np.asarray(img).astype(np.float64)
    h, w = arr.shape[:2]
    mask = arr[:, :, 3] >= ALPHA_ANY
    cols = np.nonzero(mask.any(axis=0))[0]
    if len(cols) == 0:
        return img
    x0, x1 = int(cols.min()), int(cols.max()) + 1
    top = np.array([np.nonzero(mask[:, x])[0].min() for x in range(x0, x1)], dtype=np.float64)
    bot = np.array([np.nonzero(mask[:, x])[0].max() for x in range(x0, x1)], dtype=np.float64)
    height = float(np.median(bot - top + 1))
    t = np.linspace(0.0, 1.0, x1 - x0)
    arch = -NECK_ARCH * height * np.sin(np.pi * t)
    rise = (1.0 - NECK_KEEP) * height * t
    ytop = top + arch
    ybot = bot - rise

    src = premul(arr)
    out = np.zeros_like(src)
    for i, x in enumerate(range(x0, x1)):
        ys = np.arange(np.floor(ytop[i]), np.ceil(ybot[i]) + 1)
        if len(ys) < 2:
            continue
        u = (ys - ytop[i]) / max(1e-6, (ybot[i] - ytop[i]))
        sy = top[i] + u * max(1e-6, (bot[i] - top[i]))
        for c in range(4):
            out[ys.astype(int), x, c] = np.interp(sy, np.arange(h), src[:, x, c], left=0.0, right=0.0)
    return Image.fromarray(unpremul(out), "RGBA")


def reshape_shin(img: Image.Image) -> Image.Image:
    arr = np.asarray(img).astype(np.float64)
    h, w = arr.shape[:2]
    mask = arr[:, :, 3] >= ALPHA_ANY
    rows = np.nonzero(mask.any(axis=1))[0]
    if len(rows) == 0:
        return img
    y0, y1 = int(rows.min()), int(rows.max()) + 1
    first = np.array([np.nonzero(mask[y])[0].min() for y in range(y0, y1)], dtype=np.float64)
    last = np.array([np.nonzero(mask[y])[0].max() for y in range(y0, y1)], dtype=np.float64)
    width = last - first + 1
    height = float(y1 - y0)
    s = np.linspace(0.0, 1.0, y1 - y0)

    def smooth(values: np.ndarray, k: int) -> np.ndarray:
        kernel = np.ones(k) / k
        pad = np.r_[np.full(k, values[0]), values, np.full(k, values[-1])]
        return np.convolve(pad, kernel, mode="same")[k:-k]

    delivered = smooth(width, 9)
    target_s = np.array([p[0] for p in SHIN_TARGET])
    target_w = np.array([p[1] for p in SHIN_TARGET]) * height
    target = np.interp(s, target_s, target_w)
    blend = np.clip(np.minimum(s, 1.0 - s) / SHIN_BLEND, 0.0, 1.0)
    want = delivered * (1.0 - blend) + target * blend
    gain = want / np.maximum(delivered, 1.0)
    gain = smooth(gain, 15)
    gain = np.clip(gain, SHIN_GAIN_LIMITS[0], SHIN_GAIN_LIMITS[1])

    src = premul(arr)
    out = np.zeros_like(src)
    for j, y in enumerate(range(y0, y1)):
        c = (first[j] + last[j]) * 0.5
        new_w = max(2.0, width[j] * gain[j])
        xs = np.arange(int(round(c - new_w * 0.5)), int(round(c + new_w * 0.5)))
        if len(xs) < 2:
            continue
        u = (xs - xs[0]) / max(1e-6, xs[-1] - xs[0])
        sx = first[j] + u * (width[j] - 1)
        for ch in range(4):
            out[y, xs, ch] = np.interp(sx, np.arange(w), src[y, :, ch], left=0.0, right=0.0)
    return Image.fromarray(unpremul(out), "RGBA")


RESHAPERS = {"neck": reshape_neck, "shin": reshape_shin}


def end_ratio(img: Image.Image, threshold: int = 128, window: float = 0.15) -> float | None:
    """Height of the body's head end over its chest end, columns at the very edges
    excluded from the windows by using a solid threshold (the painted fringe is soft)."""
    arr = np.asarray(img)[:, :, 3]
    mask = arr >= threshold
    cols = np.nonzero(mask.any(axis=0))[0]
    if len(cols) < 20:
        return None
    x0, x1 = int(cols.min()), int(cols.max()) + 1
    ext = np.array([mask[:, x].sum() for x in range(x0, x1)], dtype=np.float64)
    k = max(3, int((x1 - x0) * window))
    return float(ext[-k:].mean() / max(1.0, ext[:k].mean()))


def mid_fraction(img: Image.Image, threshold: int = 128) -> float | None:
    """Middle width over the body height, the shape the shin warp exists to raise."""
    arr = np.asarray(img)[:, :, 3]
    mask = arr >= threshold
    rows = np.nonzero(mask.any(axis=1))[0]
    if len(rows) < 20:
        return None
    y0, y1 = int(rows.min()), int(rows.max()) + 1
    heights = y1 - y0
    mid = mask[y0 + int(heights * 0.42):y0 + int(heights * 0.58)]
    return float(np.array([r.sum() for r in mid], dtype=np.float64).mean() / heights)


def needs_reshape(part: str, img: Image.Image, pristine: Image.Image) -> tuple[bool, str]:
    """Is the CURRENT canvas still the delivered shape? Compared against the pristine
    copy with the same measurement, so a second pipeline run is a no-op."""
    if part == "neck":
        cur, before = end_ratio(img), end_ratio(pristine)
        if cur is None or before is None:
            return False, "no body"
        return cur > before - 0.12, (f"head/chest end height ratio {cur:.3f} "
                                     f"(delivered {before:.3f}, reshaped ~{before * NECK_KEEP:.3f})")
    if part == "shin":
        cur, before = mid_fraction(img), mid_fraction(pristine)
        if cur is None or before is None:
            return False, "no body"
        return cur < before + 0.03, (f"middle width {cur:.3f} of the body height "
                                     f"(delivered {before:.3f}, reshaped ~0.17)")
    return False, "no test"


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--label", default="art1dog13")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--parts", default=",".join(RESHAPERS))
    args = ap.parse_args(argv)

    before_dir = RUNS / f"{args.label}-before"
    wanted = [p.strip() for p in args.parts.split(",") if p.strip()]
    failures = []
    reshaped = 0
    skipped = 0
    for part in wanted:
        if part not in RESHAPERS:
            failures.append(f"no profile for {part}")
            continue
        pristine = before_dir / f"{part}.png"
        current = ART / f"{part}.png"
        if not pristine.exists():
            if not args.dry_run:
                before_dir.mkdir(parents=True, exist_ok=True)
                shutil.copy2(current, pristine)
            print(f"{part}: pristine copy written from the delivered canvas")
        img = Image.open(current).convert("RGBA")
        src = Image.open(pristine).convert("RGBA")
        need, measure = needs_reshape(part, img, src)
        if not need:
            print(f"{part}: already reshaped - left alone ({measure})")
            skipped += 1
            continue
        before_bb = alpha_bbox(src)
        out = RESHAPERS[part](src)
        after_bb = alpha_bbox(out)
        print(f"{part}: {measure}; body bbox {before_bb[2] - before_bb[0]}x{before_bb[3] - before_bb[1]} px"
              f" -> {after_bb[2] - after_bb[0]}x{after_bb[3] - after_bb[1]} px"
              f" (canvas kept {src.width}x{src.height})")
        if not args.dry_run:
            out.save(current, "PNG", optimize=True)
        reshaped += 1

    print(f"\nDOG-RESHAPE {'OK' if not failures else 'FAIL'} reshaped={reshaped} "
          f"skipped={skipped} parts={len(wanted)}")
    for f in failures:
        print("DOG-RESHAPE FAIL " + f)
    return 0 if not failures else 1


def alpha_bbox(img: Image.Image, threshold: int = ALPHA_ANY):
    a = np.asarray(img)[:, :, 3]
    ys, xs = np.nonzero(a >= threshold)
    if len(xs) == 0:
        return (0, 0, 0, 0)
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
