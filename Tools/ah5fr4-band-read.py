#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H5f R4 - THE BAND, READ FROM THE ENGINE'S OWN RENDER.

Assets/Editor/ArtCuffCheck.cs renders the thigh's canvas through a Sliced SpriteRenderer
with a hand-set border, at 1:1 with the sprite's own pixelsPerUnit, one PNG per
(border, k).  This tool reads those PNGs and answers the round's geometry question:

  * where the drawn rect sits (vs the rect the printed bounds claim), and
  * whether the border band really renders at its CONSTANT size (1:1 rows) inside the
    shrunk rect - or whether the whole picture is simply scaled (uniform) and the border
    is a no-op once the size shrinks.

Method: the k=1 / border=0 render IS the texture, drawn once; every other case is matched
against that ground truth row by row (the row-mean colour profile), and the best alignment
of the band template (the sprite's last `b` rows) is searched in the case's drawn rect.

Usage: python Tools/ah5fr4-band-read.py [tag]
"""
import os
import sys

import numpy as np
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(PROJECT, "Trials", "Runs", "cuff-r4")
PPU = 793.5          # the imported thigh canvas (130x512 px, the value the check printed)
H = 1080


def world_y_of_row(r):
    """PNG row (top-down) -> world y, metres (camera centred at the pivot, ortho 1080/(2*ppu))."""
    return (H / 2.0 - 0.5 - r) / PPU


def load_case(tag, name):
    path = os.path.join(OUT, "%s-%s.png" % (tag, name))
    if not os.path.exists(path):
        return None
    return np.asarray(Image.open(path).convert("RGBA"), dtype=np.float64)


def drawn_rows(im):
    a = im[:, :, 3]
    rows = np.where((a > 16).any(axis=1))[0]
    if len(rows) == 0:
        return None
    return int(rows.min()), int(rows.max())


def profile(im, r0, r1):
    """Per-row mean RGB over the drawn columns (the row's own signature)."""
    out = []
    for r in range(r0, r1 + 1):
        row = im[r]
        m = row[:, 3] > 16
        if not m.any():
            out.append(np.zeros(3))
        else:
            out.append(row[m, :3].mean(axis=0))
    return np.array(out)


def main():
    tag = sys.argv[1] if len(sys.argv) > 1 else "baseline"
    ground = load_case(tag, "band-b0-k1.00")
    if ground is None:
        print("no ground render %s-band-b0-k1.00.png" % tag)
        return 1
    g0, g1 = drawn_rows(ground)
    g_top, g_bot = world_y_of_row(g0), world_y_of_row(g1)
    print("ground b0-k1.00: rows %d..%d = world %+.5f..%+.5f m (%d rows)"
          % (g0, g1, g_top, g_bot, g1 - g0 + 1))
    g_prof = profile(ground, g0, g1)

    for name in ("band-b0-k0.60", "band-b104-k1.00", "band-b104-k0.60", "band-b150-k0.60"):
        im = load_case(tag, name)
        if im is None:
            print("%-16s missing" % name)
            continue
        dr = drawn_rows(im)
        if dr is None:
            print("%-16s nothing drawn" % name)
            continue
        r0, r1 = dr
        top, bot = world_y_of_row(r0), world_y_of_row(r1)
        n = r1 - r0 + 1
        parts = name.split("-")[1:]
        b = int(parts[0][1:])
        k = float(parts[1][1:])
        # model A: the last b rows of the sprite drawn 1:1 at the rect's bottom,
        # the rest (512-b rows) squeezed into the remaining span
        band_rows_theory = b
        body_theory = n - b
        band_scale_a = 1.0
        body_scale_a = body_theory / float(512 - b) if 512 - b > 0 else 0.0
        # model B: everything scaled by k about the pivot (no border effect)
        prof = profile(im, r0, r1)
        verdict_a = verdict_b = None
        # match: resample the ground profile to each model and compare to what is drawn
        def resample(scale, offset):
            idx = np.arange(512) * scale + offset
            lo = np.clip(np.floor(idx).astype(int), 0, 511)
            hi = np.clip(lo + 1, 0, 511)
            f = (idx - lo)[:, None]
            return g_prof[lo] * (1 - f) + g_prof[hi] * f

        # model A in drawn-row units: rows [0..n-1]; the band is the LAST b rows
        model_a = np.vstack([resample(body_scale_a * (512 - b) / float(max(1, n - b)), 0.0)[:n - b],
                             g_prof[512 - b:]]) if n >= b else None
        model_b_full = resample(k, 0.0)
        # trim/resample model B to n rows (it should be n rows already)
        if model_b_full.shape[0] != n:
            idx = np.linspace(0, 511, n)
            lo = np.clip(np.floor(idx).astype(int), 0, 511)
            model_b_full = g_prof[lo]
        drawn = prof

        def err(model):
            if model is None or model.shape[0] != drawn.shape[0]:
                return None
            return float(np.abs(model - drawn).mean())

        ea, eb = err(model_a), err(model_b_full)
        # the band's own 1:1 test: the case's bottom b rows vs the sprite's last b rows
        band_ok = None
        if b > 0 and n >= b:
            band_ok = float(np.abs(drawn[-b:] - g_prof[512 - b:]).mean())
        print("%-16s rows %d..%d = world %+.5f..%+.5f m n %d | modelA err %s | modelB err %s |"
              " band 1:1 err %s"
              % (name, r0, r1, top, bot, n,
                 "%.2f" % ea if ea is not None else "-",
                 "%.2f" % eb if eb is not None else "-",
                 "%.2f" % band_ok if band_ok is not None else "-"))
        if b > 0 and n >= b:
            band_top = world_y_of_row(r1 - b + 1)
            print("    band: bottom %+.5f -> top %+.5f m = %.5f m tall (theory %.5f m, %d px of %d)"
                  % (bot, band_top, band_top - bot, b / PPU, b, 512))
    return 0


if __name__ == "__main__":
    sys.exit(main())
