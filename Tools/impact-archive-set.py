#!/usr/bin/env python3
"""Move a superseded impact-evidence set aside instead of deleting it.

    python Tools/impact-archive-set.py <folder> --build <buildGuid-prefix>
    python Tools/impact-archive-set.py <folder> <label> [<label> ...]

The card's evidence clause is a fingerprint equality: the delivered run's
`sourceFingerprint` must be the manifest's own.  When another lane rebuilds the player,
every run in `Trials/Runs` stops naming it, and the delivered video/stills were cut from
those runs.  A superseded run is still the only record of that build, so it is MOVED into
`Trials/Runs/<folder>/` and never deleted (the house rule this lane has followed for the
19:24, 22:32 and 23:33 sets).

A whole build is moved with `--build <guid-prefix>`; naming labels one by one is the
explicit form.  There is deliberately NO "move every impact label" default: the folder
holds the accepted evidence of every earlier build (imp_v1, imp_s1a, l2_hit_s1, grid_*...),
and an over-broad move relocates history that no run is superseding.  An archive folder
that already holds a file keeps it and does not overwrite.

What moves, per label: `<label>.json/.csv`, `<label>-impacts.json/.csv`, `shot_<label>_*.png`,
`impact_<label>_*` (take video + delivered stills), plus the two cut videos
`impact_current_build.mp4` / `impact_three_throws.mp4`.

Nothing is reloaded or re-rendered here: this is a file move, and it prints every label it
touched so a half-move is visible.
"""
from __future__ import annotations

import glob
import io
import json
import os
import shutil
import sys

PROJECT = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJECT, "Trials", "Runs")
CUT_VIDEOS = ["impact_current_build.mp4", "impact_three_throws.mp4"]


def labels_in_folder():
    out = {}
    for path in glob.glob(os.path.join(RUNS, "*-impacts.json")):
        name = os.path.basename(path)
        if not name.startswith(("imp_", "impact_")):
            continue
        try:
            with io.open(path, encoding="utf-8-sig") as handle:
                doc = json.load(handle)
        except Exception:  # noqa: BLE001
            continue
        out[name[: -len("-impacts.json")]] = (doc.get("buildGuid") or "")[:12]
    return out


def main(argv):
    if len(argv) < 3:
        print(__doc__)
        return 2
    folder = os.path.join(RUNS, argv[1])
    known = labels_in_folder()
    rest = argv[2:]
    if rest[0] == "--build":
        if len(rest) < 2:
            print("--build needs a buildGuid prefix")
            return 2
        wanted = sorted(label for label, guid in known.items() if guid.startswith(rest[1]))
        if not wanted:
            print("no impact label names build {0}".format(rest[1]))
            return 1
    else:
        wanted = rest
    unknown = [label for label in wanted if label not in known]
    if unknown:
        print("no such impact label: {0}".format(", ".join(unknown)))
        return 2

    targets = []
    for label in wanted:
        for pattern in ("{0}.json", "{0}.csv", "{0}-impacts.json", "{0}-impacts.csv",
                        "shot_{0}_*.png", "impact_{0}_*"):
            targets.extend(glob.glob(os.path.join(RUNS, pattern.format(label))))
    targets.extend(os.path.join(RUNS, name) for name in CUT_VIDEOS)
    targets = sorted({path for path in targets if os.path.isfile(path)})
    if not targets:
        print("nothing to move for {0}".format(", ".join(wanted)))
        return 1

    os.makedirs(folder, exist_ok=True)
    moved = 0
    for path in targets:
        dest = os.path.join(folder, os.path.basename(path))
        if os.path.exists(dest):
            print("KEEP  (already archived) {0}".format(os.path.basename(path)))
            continue
        shutil.move(path, dest)
        moved += 1
    print("ARCHIVED {0} file(s) of {1} label(s) -> {2}".format(moved, len(wanted), folder))
    for label in wanted:
        print("  {0:14s} buildGuid {1}".format(label, known[label]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
