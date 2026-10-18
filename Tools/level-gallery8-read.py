"""LEVEL 20.09: read one gallery catalogue JSON's identity + fire row.

Usage:
    python Tools/level-gallery8-read.py Trials/Runs/gallery8-gallery.json
"""
import io
import json
import sys

path = sys.argv[1]
d = json.load(io.open(path, encoding="utf-8-sig"))
for key in ("label", "scene", "buildGuid", "capturedUtc", "realArtOn", "width", "height",
            "panSeconds", "panFps", "panFrames", "realItems", "missingItems",
            "dressedRenderers", "boundsBeforeDress", "boundsAfterDress", "boundsDeltaMeters"):
    print("%-20s %s" % (key, d.get(key)))
print("rowBoxes         %d" % len(d.get("rowBoxes", [])))
print("inventory        %d" % len(d.get("inventory", [])))
print("fits             %d" % len(d.get("fits", [])))
print("--- fire / campfire rows ---")
for entry in d.get("inventory", []):
    blob = json.dumps(entry, ensure_ascii=False).lower()
    if "fire" in blob or "campfire" in blob or "log" in blob:
        print(json.dumps(entry, ensure_ascii=False))
print("--- rowBoxes ---")
for row in d.get("rowBoxes", []):
    print(row)
