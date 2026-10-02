"""Read the newest gallery catalogue json and print its inventory rows."""
import glob
import json
import os

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Trials", "Runs")
files = sorted(glob.glob(os.path.join(RUNS, "*gallery*.json")), key=os.path.getmtime)
print("files:", [os.path.basename(f) for f in files[-6:]])
p = files[-1]
print("newest:", os.path.basename(p))
d = json.load(open(p, encoding="utf-8"))
print("keys:", sorted(d.keys()))
for k in ("label", "buildGuid", "capturedUtc", "realItems", "missingItems", "rowBoxes",
          "dressedRenderers", "inventoryCount"):
    if k in d:
        print("  %s = %s" % (k, d[k]))
inv = d.get("inventory") or []
print("inventory rows:", len(inv))
for e in inv:
    if isinstance(e, dict):
        print("  %-28s %-10s %s" % (e.get("object", e.get("name", "?")), e.get("texture", "-"),
                                    e.get("status", e.get("note", ""))))
    else:
        print("  ", e)
