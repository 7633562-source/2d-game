#!/usr/bin/env python3
"""Compare two one-species runs field by field (same checker rules as the refresh check).

    python Tools/species-run-diff.py treeplum treeplum_r2
    python Tools/species-run-diff.py treeapple treeapple --snapshot Trials/Runs/species-pre-art0leaf

`species-refresh-check.py` answers "did the re-shoot repaint the picture and change nothing
else" against a whole snapshot folder. This tool answers the narrower question a single row
of that table raises: two runs, one scene - which acceptance fields moved, by how much, and
in what units. It is what tells a real change apart from the LIVE player's own run-to-run
wobble (the same build read twice is not bit-identical: the species report measured the
cedar's 33.5 m drawn box moving 1.45e-4 m between back-to-back runs).

    <labelA> and <labelB> are read from Trials/Runs/<label>-tree.json, unless --snapshot
    <folder> is given, in which case <labelA> is read from that folder.
"""
import json
import pathlib
import sys

RUNS = pathlib.Path(__file__).resolve().parent.parent / "Trials" / "Runs"

FIELDS = ["scene", "species", "treeCount", "plantTreeCount", "appleTreeCount",
          "fits", "missFramesAfterOneSecond", "rendererCount", "partsOverFiveMeters",
          "colliderCount", "width", "height"]
NEAR_FIELDS = ["heightMeters", "widthMeters", "orthoSize"]


def load(folder, label):
    path = pathlib.Path(folder) / (label + "-tree.json")
    if not path.exists():
        print("FAIL no json", path)
        raise SystemExit(2)
    doc = json.loads(path.read_text(encoding="utf-8-sig"))
    print("# {0}  scene={1} species={2} buildGuid={3} capturedUtc={4}".format(
        path, doc.get("scene"), doc.get("species"), doc.get("buildGuid"), doc.get("capturedUtc")))
    return doc


def main():
    args = sys.argv[1:]
    if len(args) < 2:
        print(__doc__)
        return 2
    label_a, label_b = args[0], args[1]
    folder_a = RUNS
    if len(args) > 3 and args[2] == "--snapshot":
        folder_a = pathlib.Path(args[3])
    a = load(folder_a, label_a)
    b = load(RUNS, label_b)

    moved = 0
    for key in FIELDS:
        if a.get(key) != b.get(key):
            moved += 1
            print("  MOVED  {0:26s} {1!r} -> {2!r}".format(key, a.get(key), b.get(key)))
    for key in NEAR_FIELDS:
        if a.get(key) is None or b.get(key) is None:
            continue
        d = abs(float(a[key]) - float(b[key]))
        rel = d / max(1.0, abs(float(a[key])))
        tag = "MOVED " if rel > 1e-5 else "drift "
        print("  {0} {1:26s} {2:.9f} -> {3:.9f}   d={4:.3e} m  rel={5:.3e}".format(
            tag, key, float(a[key]), float(b[key]), d, rel))
    print("SPECIES-RUN-DIFF {0} vs {1}: discrete-moved={2} of {3}".format(
        label_a, label_b, moved, len(FIELDS)))
    return 1 if moved else 0


if __name__ == "__main__":
    raise SystemExit(main())
