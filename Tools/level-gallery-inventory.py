#!/usr/bin/env python3
"""Read one gallery capture JSON: identity, the acceptance counters, and the
object -> texture -> real/missing inventory as a plain list.

  python Tools/level-gallery-inventory.py gallery8
"""
from __future__ import annotations

import json
import pathlib
import sys

PROJECT = pathlib.Path(__file__).resolve().parent.parent
RUNS = PROJECT / "Trials" / "Runs"


def main(argv: list[str]) -> int:
    label = argv[0] if argv else "gallery8"
    path = RUNS / f"{label}-gallery.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    print(f"label            {data.get('label')}")
    print(f"buildGuid        {data.get('buildGuid')}")
    print(f"capturedUtc      {data.get('capturedUtc')}")
    print(f"realArtOn        {data.get('realArtOn')}")
    for key in ("width", "height", "panSeconds", "panFrames", "realItems",
                "missingItems", "dressedRenderers", "boundsDeltaMeters",
                "giantSkips", "emptySkips", "revertedMoves"):
        if key in data:
            print(f"{key:<16} {data[key]}")

    inv = data.get("inventory") or []
    if isinstance(inv, dict):
        rows = [dict(entry, name=name) for name, entry in inv.items()]
    else:
        rows = list(inv)
    real = [r for r in rows if r.get("state") == "real"]
    missing = [r for r in rows if r.get("state") != "real"]
    print(f"\n== inventory {len(rows)}: real {len(real)} / not real {len(missing)} ==")
    for row in rows:
        state = row.get("state")
        mark = "real   " if state == "real" else f"{str(state):<7}"
        note = (row.get("note") or "").replace("\n", " ")
        print(f"{mark} {row.get('name'):<34} {row.get('texture')}")
        if state != "real" or "log" in note.lower() or "dressed" in note.lower():
            print(f"        note: {note[:400]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
