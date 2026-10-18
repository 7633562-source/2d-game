"""ART-0 cycle: read the newest gallery capture json - the row bounds the stills were framed with.

Read-only. Prints the DONE/meta lines and every RowBounds entry so a still can be cropped to the
row it names instead of guessing pixel windows. Usage:  python Tools/art0-gallery-rows.py [label]
"""
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RUNS = os.path.join(ROOT, "Trials", "Runs")

label = sys.argv[1] if len(sys.argv) > 1 else "gallery5"
path = os.path.join(RUNS, label + "-gallery.json")
with open(path, "r", encoding="utf-8") as fh:
    data = json.load(fh)

print("file:", path)
for key in sorted(data.keys()):
    value = data[key]
    if isinstance(value, (str, int, float, bool)):
        print("  %-24s %s" % (key, value))

bounds = data.get("RowBounds") or {}
print("RowBounds (%d):" % len(bounds))
for key in sorted(bounds.keys()):
    print("  %-14s %s" % (key, json.dumps(bounds[key])))

stills = data.get("Stills") or []
print("Stills (%d):" % len(stills))
for item in stills:
    print("  %s" % json.dumps(item))
