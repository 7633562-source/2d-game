#!/usr/bin/env python3
"""Measure caption ink in a gallery still: how wide is one label, in metres?

The gallery's TextMesh labels (GalleryLabels.Readable) are drawn at a cap height in
metres; the built-in font's advance width is not documented anywhere in the repo, so
the only honest source is a delivered still. This probe prints, for one y-band, the
columns that carry label ink and merges them into runs, so a label's own width - and
whether neighbouring labels overlap - is measured, not guessed.

  python Tools/art1-label-metrics.py Trials/Runs/gallery12_human.png 735 765

The still must have been captured at 1920x1080; metres/px is read from the JSON
`fits[].orthoSize` next to it (`<label>-gallery.json`) so the numbers are the
camera's own, not a hard-coded lens.
"""
from __future__ import annotations

import json
import pathlib
import sys

import numpy as np
from PIL import Image

PROJECT = pathlib.Path(__file__).resolve().parent.parent
RUNS = PROJECT / "Trials" / "Runs"


def ortho_for(still: pathlib.Path) -> float:
    label = still.name.split("_")[0]
    data = json.loads((RUNS / f"{label}-gallery.json").read_text(encoding="utf-8"))
    wanted = still.name[len(label) + 1:].rsplit(".", 1)[0]
    for fit in data["fits"]:
        if fit["file"] == still.name or fit["file"].endswith(wanted):
            return float(fit["orthoSize"])
    return float(data["fits"][0]["orthoSize"])


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print(__doc__)
        return 2
    still = pathlib.Path(argv[0])
    y0, y1 = (int(v) for v in argv[1].split(","))
    if not still.exists() and (RUNS / still.name).exists():
        still = RUNS / still.name
    img = np.asarray(Image.open(still).convert("RGB")).astype(np.int16)
    height = img.shape[0]
    m_per_px = 2.0 * ortho_for(still) / height

    band = img[y0:y1]
    # label ink: light pixels (the text is near-white with a dark copy underneath)
    light = (band > 170).all(axis=2)
    cols = light.any(axis=0)
    runs = []
    start = None
    for x, on in enumerate(cols):
        if on and start is None:
            start = x
        elif not on and start is not None:
            runs.append((start, x - 1))
            start = None
    if start is not None:
        runs.append((start, len(cols) - 1))

    print(f"still {still.name} band y={y0}..{y1} m/px={m_per_px:.5f} "
          f"({m_per_px * 1000:.2f} mm/px)")
    print(f"ink runs ({len(runs)}):")
    for a, b in runs:
        print(f"  x {a:4d}..{b:4d}  width {b - a + 1:4d} px = {(b - a + 1) * m_per_px:.3f} m")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
