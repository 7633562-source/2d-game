#!/usr/bin/env python3
"""The two frames impact-video.py does not pick, for the verdict cycle.

    python Tools/impact-verdict-stills.py cv_v1L cv_v2L

The shared fall criterion fires late by construction (it needs 0.25 m of drop or 60 deg of
tilt held for 0.15 s), and the controller's own geometric ladder fires first; the cycle's
whole point is that gap, so two stills are taken where it is visible:

  photo_ctrlfall   the frame at the CONTROLLER's own fall time (contact + ctrlFallen...)
  photo_windowend  the frame after the throw's window closes (contact + windowSeconds +
                   one step), where the banner carries the throw's final numbers

Both are copies of the run's own shot_<label>_<n>.png: nothing is re-rendered.
"""
import io
import json
import os
import shutil
import sys

RUNS = r"E:/Games/2D Game/My project/Trials/Runs"
INTERVAL = 0.05          # -shotInterval of the takes


def main(argv):
    labels = argv or ["cv_v1L", "cv_v2L"]
    for label in labels:
        path = os.path.join(RUNS, label + "-impacts.json")
        if not os.path.exists(path):
            print(label, "no impacts json")
            continue
        with io.open(path, encoding="utf-8-sig") as handle:
            doc = json.load(handle)
        row = doc["throws"][0]
        window = doc["windowSeconds"]
        picks = []
        seconds = row.get("ctrlFallenAfterContactSeconds", -1)
        if seconds is not None and seconds >= 0:
            picks.append(("ctrlfall", row["contactTime"] + seconds))
        else:
            print(label, "no controller fall in the window")
        picks.append(("windowend", row["contactTime"] + window + 0.005))
        for tag, moment in picks:
            index = int(round(moment / INTERVAL))
            src = os.path.join(RUNS, "shot_%s_%d.png" % (label, index))
            if not os.path.exists(src):
                print(label, tag, "missing", src)
                continue
            dst = os.path.join(RUNS, "impact_%s_photo_%s.png" % (label, tag))
            shutil.copyfile(src, dst)
            print(label, tag, "frame", index, "(%0.3f s)" % moment, "->",
                  os.path.basename(dst), "%.0f KB" % (os.path.getsize(dst) / 1024.0))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
