#!/usr/bin/env python3
"""Species-scene table: reads Trials/Runs/<label>-tree.json and prints the numbers.

  python Tools/species-table.py                       # all accepted labels
  python Tools/species-table.py treecedar             # just one

The JSONs are written by Assets/SpeciesScene.cs from the built player, so this
table is a reading of the runs, not a second source of truth.
"""
from __future__ import annotations

import json
import pathlib
import sys

PROJECT = pathlib.Path(__file__).resolve().parent.parent
RUNS = PROJECT / "Trials" / "Runs"
LABELS = ["treeapple", "treeoak", "treepine", "treewillow", "treebush",
          "treepoplar", "treelilac", "treehazel", "treecedar", "treeplum"]

HEADER = ("| label | scene | species | trees (plant/apple) | height m | width m | ortho | "
          "fits | miss | miss after 1 s | dress outside m | bounds growth m | frames | "
          "viewport x / y | painted |")
BAR = "|" + "---|" * 15


def main() -> int:
    labels = sys.argv[1:] or LABELS
    print(HEADER)
    print(BAR)
    problems = []
    law = []
    totals = []
    for label in labels:
        path = RUNS / (label + "-tree.json")
        if not path.exists():
            problems.append(f"{label}: no JSON")
            continue
        d = json.loads(path.read_text(encoding="utf-8-sig"))
        print("| `{label}` | {scene} | {species} | {treeCount} ({plantTreeCount}/{appleTreeCount}) | "
              "{h} | {w} | {ortho} | {fits} | {miss} | {m1} | {dress} | {grow} | {frames} | "
              "{vx0}-{vx1} / {vy0}-{vy1} | {paint} |".format(
                  label=d["label"], scene=d["scene"], species=d["species"],
                  treeCount=d["treeCount"], plantTreeCount=d["plantTreeCount"],
                  appleTreeCount=d["appleTreeCount"],
                  h=f'{d["heightMeters"]:.3f}', w=f'{d["widthMeters"]:.3f}',
                  ortho=f'{d["orthoSize"]:.3f}', fits=d["fits"], miss=d["missFrames"],
                  m1=d["missFramesAfterOneSecond"], dress=f'{d["dressOutsideMeters"]:.3f}',
                  grow=f'{d["boundsGrowthMeters"]:.3f}', frames=d["observedFrames"],
                  vx0=f'{d["minViewportX"]:.3f}', vx1=f'{d["maxViewportX"]:.3f}',
                  vy0=f'{d["minViewportY"]:.3f}', vy1=f'{d["maxViewportY"]:.3f}',
                  paint=f'{d["paintedFraction"]:.3f}'))
        if "rendererCount" in d:
            print(f"| `{d['label']}` parts | renderers {d['rendererCount']} | over 5 m "
                  f"{d['partsOverFiveMeters']} | biggest `{d.get('biggestPartPath')}` "
                  f"{d.get('biggestPartMetersX', 0):.3f} x {d.get('biggestPartMetersY', 0):.3f} m "
                  f"scale {d.get('biggestPartScale', 0):.3f} | colliders {d.get('colliderCount', 0)} | "
                  f"over 5 m {d.get('collidersOverFiveMeters', 0)} | biggest "
                  f"`{d.get('biggestColliderPath')}` {d.get('biggestColliderMetersX', 0):.3f} x "
                  f"{d.get('biggestColliderMetersY', 0):.3f} m | | | | | |")
        if d["treeCount"] != 1:
            problems.append(f"{label}: treeCount {d['treeCount']}")
        if not d["fits"]:
            problems.append(f"{label}: framebust")
        if d.get("missFramesAfterOneSecond", 1) != 0:
            problems.append(f"{label}: missed the frame after 1 s")
        if not (2.0 <= d["heightMeters"] <= 5.0):
            print(f"| `{label}` NOTE | measured box {d['heightMeters']:.3f} x "
                  f"{d['widthMeters']:.3f} m leaves the ordered 2-5 m band (rig, not framing) |"
                  " | | | | | | | | | | | |")
        # The framing law must hold on every species, not only on the first one it
        # was proved on: ortho = 1.10 * max(h/2, w/2 / aspect), aspect = 16:9.
        aspect = d["width"] / float(d["height"])
        predicted = max(d["heightMeters"] / 2 * 1.10,
                        d["widthMeters"] / 2 * 1.10 / aspect)
        delta = d["orthoSize"] - predicted
        law.append((label, predicted, delta))
        totals.append((d["observedFrames"], d["observedFrames"] - d["missFrames"]))
    print()
    if law:
        worst = max(law, key=lambda row: abs(row[2]))
        print("SPECIES-LAW ortho = 1.10 * max(h/2, w/2/aspect) on %d scenes; "
              "worst delta %.6f m (%s)" % (len(law), worst[2], worst[0]))
        for label, predicted, delta in law:
            print("  %-12s predicted %.6f measured %.6f delta %+.6f"
                  % (label, predicted, predicted + delta, delta))
    if totals:
        print("SPECIES-FRAMES scenes=%d observed=%d inside=%d"
              % (len(totals), sum(t[0] for t in totals), sum(t[1] for t in totals)))
    print("SPECIES-TABLE labels=%d problems=%s" % (len(labels), problems if problems else "none"))
    return 1 if problems else 0

if __name__ == "__main__":
    sys.exit(main())
