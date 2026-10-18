#!/usr/bin/env python3
"""The two extra stills per take, from the run's OWN numbers.

    python Tools/_ld9-stills.py imp_cb1L imp_cb2L imp_cb3L

`F:/hermes-data/scripts/impact-video.py` writes the contact and the criterion-fall still;
the delivered protocol has carried four per take since the push-back cycle (contact, fall,
the controller's own fall, the window end).  The two extra frames are copied out of the run's
own `shot_<label>_<n>.png` sequence at frames computed from that run's
`throws[0].ctrlFallenAfterContactSeconds` and the record's `windowSeconds` - nothing is
timed by hand.  Run AFTER impact-video.py.
"""
from __future__ import annotations

import json
import os
import shutil
import sys

PROJECT = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJECT, "Trials", "Runs")
INTERVAL = 0.05


def frame_index(seconds):
    return int(round(seconds / INTERVAL))


def main(labels):
    problems = 0
    for label in labels:
        with open(os.path.join(RUNS, label + "-impacts.json"), encoding="utf-8") as handle:
            doc = json.load(handle)
        throw = doc["throws"][0]
        contact = float(throw["contactTime"])
        window = float(doc.get("windowSeconds") or 0.0) or 4.5
        ctrlfall = float(throw.get("ctrlFallenAfterContactSeconds") or -1.0)
        picks = []
        if ctrlfall > 0.0:
            picks.append(("ctrlfall", frame_index(contact + ctrlfall)))
        picks.append(("windowend", frame_index(contact + window)))
        for tag, index in picks:
            src = os.path.join(RUNS, "shot_{0}_{1}.png".format(label, index))
            if not os.path.exists(src):
                print("MISSING %s" % src)
                problems += 1
                continue
            dst = os.path.join(RUNS, "impact_{0}_photo_{1}.png".format(label, tag))
            shutil.copyfile(src, dst)
            print("%-46s frame %4d  (contact + %.6f s)"
                  % (os.path.basename(dst), index, (index * INTERVAL) - contact))
    print("LD9-STILLS %s problems=%d" % ("OK" if problems == 0 else "FAIL", problems))
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:] or ["imp_cb1L", "imp_cb2L", "imp_cb3L"]))
