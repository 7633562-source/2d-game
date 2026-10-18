#!/usr/bin/env python3
"""Cross-check Tools/art1-fit-audit.py's PNG reader against an independent one.

The audit decodes the part PNGs with zlib only (no PIL on this host) to find the
painted pixels' bounding box. If the host does have Pillow, this proves the
decoder on the same files: same pixel size, same alpha bounding box. Without
Pillow it names the gap instead of pretending.

Run:  python Tools/art1-fit-audit-crosscheck.py
"""
from __future__ import annotations

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import importlib.util

spec = importlib.util.spec_from_file_location(
    "fit", pathlib.Path(__file__).resolve().parent / "art1-fit-audit.py")
fit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fit)

try:
    from PIL import Image
except Exception as exc:  # noqa: BLE001
    print("CROSSCHECK no Pillow on this host (" + type(exc).__name__ + ") - decoder unproven")
    raise SystemExit(0)

fails = 0
files = sorted((fit.ART / "Human").glob("*.png"))
for png in files:
    px = fit.png_size(png)
    mine = fit.alpha_bbox(png)
    im = Image.open(png)
    theirs_px = (im.width, im.height)
    theirs = im.convert("RGBA").getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox()
    # PIL's bbox is (left, upper, right, bottom) with right/bottom EXCLUSIVE;
    # alpha_bbox returns (left, upper, width, height). Same rectangle, two shapes.
    mine_rect = None if mine is None else (mine[0], mine[1], mine[0] + mine[2], mine[1] + mine[3])
    same_size = px == theirs_px
    same_bb = mine_rect is not None and theirs is not None and tuple(mine_rect) == tuple(theirs)
    print("CROSSCHECK %-9s size %s  bbox %s (mine %s theirs %s)" % (
        png.stem, "=" if same_size else "MISMATCH", "=" if same_bb else "MISMATCH",
        mine_rect, theirs))
    if not (same_size and same_bb):
        fails += 1

print("CROSSCHECK files=%d mismatches=%d pillow=%s" % (len(files), fails, Image.__version__))
sys.exit(1 if fails else 0)
