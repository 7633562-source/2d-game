"""Print the gallery capture JSON's own real/missing accounting (level worker helper).

    python Tools/level-gallery-lists.py Trials/Runs/gallery10-gallery.json

The catalogue record carries `realItems` / `missingItems` as counters and the inventory
printout (`Tools/level-gallery-inventory.py`) carries the same rows as text; the two counted
differently once (7 vs 6) and a counter nobody can reconcile is a counter nobody can quote,
so this prints the JSON's own lists side by side: every named entry, its texture and its
verdict, followed by the two counts. Read-only.
"""
from __future__ import annotations

import io
import json
import sys


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    with io.open(sys.argv[1], encoding="utf-8-sig") as handle:
        data = json.load(handle)

    print("label %s buildGuid %s capturedUtc %s"
          % (data.get("label"), data.get("buildGuid"), data.get("capturedUtc")))
    for key in ("realItems", "missingItems", "dressedRenderers", "rowBoxes", "panFrames", "panSeconds"):
        print("%-18s %s" % (key, data.get(key)))

    for key in ("items", "inventory"):
        rows = data.get(key)
        if not isinstance(rows, list):
            continue
        print("\n== %s (%d) ==" % (key, len(rows)))
        for row in rows:
            if not isinstance(row, dict):
                print("   %s" % row)
                continue
            print("  %-9s %-32s %s"
                  % (row.get("state") or ("real" if row.get("real") else "?"),
                     row.get("name") or row.get("label"),
                     row.get("texture") or row.get("files") or row.get("note") or ""))
    rows = data.get("inventory") or []
    real = sum(1 for row in rows if isinstance(row, dict) and row.get("state") == "real")
    missing = sum(1 for row in rows if isinstance(row, dict) and row.get("state") == "missing")
    print("\ninventory entries: %d (state real %d / missing %d); JSON counters: realItems %s / "
          "missingItems %s" % (len(rows), real, missing, data.get("realItems"), data.get("missingItems")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
