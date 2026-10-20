#!/usr/bin/env python3
"""Gallery inventory: what the catalogue shows and where each texture comes from.

Reads Trials/Runs/<label>-gallery.json (written by Assets/GalleryCapture.cs from
the built player) and prints the ART-1 acceptance item 5 list - object, texture
file, real or missing - plus the four level files' status.

  python Tools/gallery-inventory.py            # newest gallery JSON
  python Tools/gallery-inventory.py gallery3
"""
from __future__ import annotations

import json
import pathlib
import sys

PROJECT = pathlib.Path(__file__).resolve().parent.parent
RUNS = PROJECT / "Trials" / "Runs"
LEVEL_FILES = ["ground_fill", "ground_cap", "sky", "sign_finish"]


def newest_label() -> str:
    files = sorted(RUNS.glob("*-gallery.json"), key=lambda p: p.stat().st_mtime)
    if not files:
        raise SystemExit("FAIL no *-gallery.json in " + str(RUNS))
    return files[-1].name[: -len("-gallery.json")]


def main() -> int:
    label = sys.argv[1] if len(sys.argv) > 1 else newest_label()
    path = RUNS / (label + "-gallery.json")
    if not path.exists():
        raise SystemExit("FAIL no " + str(path))
    d = json.loads(path.read_text(encoding="utf-8-sig"))

    print(f"# gallery inventory — label {label}, buildGuid {d['buildGuid']}, "
          f"real {d['realItems']}, missing {d['missingItems']}")
    print()
    print("| object | texture | state | note |")
    print("|---|---|---|---|")
    for item in d["inventory"]:
        print("| {name} | `{texture}` | {state} | {note} |".format(
            name=item["name"], texture=item["texture"], state=item["state"],
            note=item["note"] or ""))

    print()
    print("| level file | on disk | meta |")
    print("|---|---|---|")
    for key in LEVEL_FILES:
        png = PROJECT / "Assets" / "Resources" / "Level" / (key + ".png")
        meta = png.with_suffix(".png.meta")
        print(f"| `{key}` | {png.exists()} ({png.stat().st_size if png.exists() else 0} B) "
              f"| {meta.exists()} |")

    fits_ok = sum(1 for f in d["fits"] if f["fits"])
    print()
    print(f"GALLERY-INVENTORY label={label} items={len(d['inventory'])} "
          f"real={d['realItems']} missing={d['missingItems']} "
          f"stills_fit={fits_ok}/{len(d['fits'])} dressed={d['dressedRenderers']} "
          f"pan={d['panFrames']}f/{d['panSeconds']}s")
    return 0


if __name__ == "__main__":
    sys.exit(main())
